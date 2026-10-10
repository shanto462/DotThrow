using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;

namespace DotThrow.Testing;

/// <summary>
/// Test helpers for validators, in the shape of FluentValidation's TestHelper. They do not
/// depend on a test framework: a failed assertion throws a <see cref="ValidationTestException"/>.
/// </summary>
public static class TestValidationExtensions
{
    /// <summary>Validates <paramref name="value"/> for a test.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator under test.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <returns>The result with assertions.</returns>
    /// <exception cref="AsyncRuleInvokedSynchronouslyException">The validator has async rules; use <see cref="TestValidateAsync{T}(IValidator{T}, T, ValidationOptions?, CancellationToken)"/>.</exception>
    [OverloadResolutionPriority(1)]
    public static TestValidationResult<T> TestValidate<T>(this IValidator<T> validator, T value, ValidationOptions? options = null)
    {
        Argument.NotNull(validator);
        try
        {
            return new TestValidationResult<T>(validator.Validate(value, options));
        }
        catch (AsyncRuleInvokedSynchronouslyException exception)
        {
            throw new AsyncRuleInvokedSynchronouslyException(
                $"{(exception.ValidatorType is null ? "The validator" : TypeNames.Display(exception.ValidatorType))} has async rules. Use TestValidateAsync instead.",
                exception);
        }
    }

    /// <summary>Validates <paramref name="value"/> for a test, running the given groups.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator under test.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run.</param>
    /// <returns>The result with assertions.</returns>
    public static TestValidationResult<T> TestValidate<T>(this IValidator<T> validator, T value, params string[] groups) =>
        TestValidate(validator, value, new ValidationOptions { Groups = groups });

    /// <summary>Validates <paramref name="value"/> for a test with options set in a callback (FluentValidation's <c>TestValidate(x, options =&gt; ...)</c>).</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator under test.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Sets the options.</param>
    /// <returns>The result with assertions.</returns>
    public static TestValidationResult<T> TestValidate<T>(this IValidator<T> validator, T value, Action<ValidationOptions<T>> configure) =>
        TestValidate(validator, value, ValidationOptions<T>.Create(configure));

    /// <summary>Validates <paramref name="value"/> for a test, including async rules, with options set in a callback.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator under test.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Sets the options.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The result with assertions.</returns>
    public static Task<TestValidationResult<T>> TestValidateAsync<T>(this IValidator<T> validator, T value, Action<ValidationOptions<T>> configure, CancellationToken cancellationToken = default) =>
        TestValidateAsync(validator, value, ValidationOptions<T>.Create(configure), cancellationToken);

    /// <summary>Validates <paramref name="value"/> for a test, including async rules, with a cancellation token.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator under test.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The result with assertions.</returns>
    public static Task<TestValidationResult<T>> TestValidateAsync<T>(this IValidator<T> validator, T value, CancellationToken cancellationToken) =>
        TestValidateAsync(validator, value, (ValidationOptions?)null, cancellationToken);

    /// <summary>Validates <paramref name="value"/> for a test, including async rules.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator under test.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The result with assertions.</returns>
    [OverloadResolutionPriority(1)]
    public static async Task<TestValidationResult<T>> TestValidateAsync<T>(this IValidator<T> validator, T value, ValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        Argument.NotNull(validator);
        return new TestValidationResult<T>(await validator.ValidateAsync(value, options, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Asserts that at least one matched failure meets <paramref name="predicate"/>, and keeps only those.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="predicate">The condition.</param>
    /// <param name="message">The assertion message, or <see langword="null"/> for a default one. It may use <c>{Code}</c>, <c>{Message}</c>, <c>{State}</c> and <c>{Severity}</c> of the first unmatched failure.</param>
    /// <returns>The failures that meet the condition.</returns>
    /// <exception cref="ValidationTestException">No failure meets the condition.</exception>
    public static ITestValidationWith When(this ITestValidationContinuation failures, Func<ValidationFailure, bool> predicate, string? message = null)
    {
        Argument.NotNull(predicate);
        var result = Continue(failures);
        result.Add(predicate);
        if (result.Matched().Count == 0)
        {
            var unmatched = result.Unmatched();
            throw new ValidationTestException(Format(unmatched.Count > 0 ? unmatched[0] : null, message, "Expected validation error was not found"), unmatched);
        }

        return result;
    }

    /// <summary>Asserts that every matched failure meets <paramref name="predicate"/>.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="predicate">The condition.</param>
    /// <param name="message">The assertion message, or <see langword="null"/> for a default one.</param>
    /// <returns>The failures.</returns>
    /// <exception cref="ValidationTestException">A failure does not meet the condition.</exception>
    public static ITestValidationContinuation WhenAll(this ITestValidationContinuation failures, Func<ValidationFailure, bool> predicate, string? message = null)
    {
        Argument.NotNull(predicate);
        var result = Continue(failures);
        result.Add(predicate);
        var unmatched = result.Unmatched();
        if (unmatched.Count > 0)
        {
            throw new ValidationTestException(Format(unmatched[0], message, "Found an unexpected validation error"), unmatched);
        }

        return result;
    }

    /// <summary>Asserts that a matched failure has the message.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="expectedMessage">The expected message.</param>
    /// <returns>The failures with that message.</returns>
    public static ITestValidationWith WithErrorMessage(this ITestValidationContinuation failures, string expectedMessage) =>
        failures.When(f => f.Message == expectedMessage, $"Expected an error message of '{expectedMessage}'. Actual message was '{{Message}}'");

    /// <summary>Asserts that a matched failure has the error code.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="expectedErrorCode">The expected code, for example <see cref="ErrorCodes.LongerThan"/>.</param>
    /// <returns>The failures with that code.</returns>
    public static ITestValidationWith WithErrorCode(this ITestValidationContinuation failures, string expectedErrorCode) =>
        failures.When(f => f.ErrorCode == expectedErrorCode, $"Expected an error code of '{expectedErrorCode}'. Actual error code was '{{Code}}'");

    /// <summary>Asserts that a matched failure has the severity.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="expectedSeverity">The expected severity.</param>
    /// <returns>The failures with that severity.</returns>
    public static ITestValidationWith WithSeverity(this ITestValidationContinuation failures, Severity expectedSeverity) =>
        failures.When(f => f.Severity == expectedSeverity, $"Expected a severity of '{expectedSeverity}'. Actual severity was '{{Severity}}'");

    /// <summary>Asserts that a matched failure has the custom state.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="expectedState">The expected state.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="object.Equals(object, object)"/>.</param>
    /// <returns>The failures with that state.</returns>
    public static ITestValidationWith WithCustomState(this ITestValidationContinuation failures, object? expectedState, IEqualityComparer? comparer = null) =>
        failures.When(
            f => comparer?.Equals(f.CustomState, expectedState) ?? Equals(f.CustomState, expectedState),
            $"Expected custom state of '{expectedState}'. Actual state was '{{State}}'");

    /// <summary>Asserts that a matched failure has the attempted value.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="expectedValue">The expected attempted value.</param>
    /// <returns>The failures with that attempted value.</returns>
    public static ITestValidationWith WithAttemptedValue(this ITestValidationContinuation failures, object? expectedValue) =>
        failures.When(f => Equals(f.AttemptedValue, expectedValue), $"Expected an attempted value of '{expectedValue}'.");

    /// <summary>
    /// Asserts that a matched failure has the message argument (FluentValidation's
    /// <c>WithMessageArgument</c>), from <see cref="ValidationFailure.MessageArguments"/>.
    /// </summary>
    /// <typeparam name="TValue">The argument type.</typeparam>
    /// <param name="failures">The failures so far.</param>
    /// <param name="name">The argument name, for example <c>"PropertyName"</c> or a name added with <c>AppendArgument</c>.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>The failures with that argument.</returns>
    public static ITestValidationWith WithMessageArgument<TValue>(this ITestValidationContinuation failures, string name, TValue value) =>
        failures.When(
            f => f.MessageArguments.TryGetValue(name, out var actual) && Equals(actual, value),
            $"Expected message argument '{name}' with value '{value}'.");

    /// <summary>Asserts that no matched failure has the message.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="unexpectedMessage">The message that must not appear.</param>
    /// <returns>The failures.</returns>
    public static ITestValidationContinuation WithoutErrorMessage(this ITestValidationContinuation failures, string unexpectedMessage) =>
        failures.WhenAll(f => f.Message != unexpectedMessage, $"Found an unexpected error message of '{unexpectedMessage}'");

    /// <summary>Asserts that no matched failure has the error code.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="unexpectedErrorCode">The code that must not appear.</param>
    /// <returns>The failures.</returns>
    public static ITestValidationContinuation WithoutErrorCode(this ITestValidationContinuation failures, string unexpectedErrorCode) =>
        failures.WhenAll(f => f.ErrorCode != unexpectedErrorCode, $"Found an unexpected error code of '{unexpectedErrorCode}'");

    /// <summary>Asserts that no matched failure has the severity.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="unexpectedSeverity">The severity that must not appear.</param>
    /// <returns>The failures.</returns>
    public static ITestValidationContinuation WithoutSeverity(this ITestValidationContinuation failures, Severity unexpectedSeverity) =>
        failures.WhenAll(f => f.Severity != unexpectedSeverity, $"Found an unexpected severity of '{unexpectedSeverity}'");

    /// <summary>Asserts that no matched failure has the custom state.</summary>
    /// <param name="failures">The failures so far.</param>
    /// <param name="unexpectedState">The state that must not appear.</param>
    /// <returns>The failures.</returns>
    public static ITestValidationContinuation WithoutCustomState(this ITestValidationContinuation failures, object? unexpectedState) =>
        failures.WhenAll(f => !Equals(f.CustomState, unexpectedState), $"Found an unexpected custom state of '{unexpectedState}'");

    /// <summary>
    /// Asserts that the run has no failures other than the ones matched so far (by the property
    /// name and every <c>With...</c> condition).
    /// </summary>
    /// <param name="failures">The failures so far.</param>
    /// <returns>The failures.</returns>
    /// <exception cref="ValidationTestException">The run has other failures.</exception>
    public static ITestValidationWith Only(this ITestValidationWith failures)
    {
        Argument.NotNull(failures);
        var unmatched = new List<ValidationFailure>();
        if (failures is TestValidationContinuation continuation)
        {
            for (var current = continuation; current is not null; current = current.Parent)
            {
                AddNew(unmatched, current.Unmatched());
            }
        }
        else
        {
            AddNew(unmatched, failures.UnmatchedFailures);
        }

        if (unmatched.Count > 0)
        {
            throw new ValidationTestException(
                TestMessages.Describe("Expected to have errors only matching specified conditions", "Unexpected errors:", unmatched, useNames: false),
                unmatched);
        }

        return failures;
    }

    /// <summary>
    /// Asserts that the rules for the property include a child validator of the given type
    /// (FluentValidation's <c>ShouldHaveChildValidator</c>). Works for <see cref="Validator{T}"/>.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="validator">The validator under test.</param>
    /// <param name="selector">Selects the property.</param>
    /// <param name="childValidatorType">The expected child validator type, or a base type of it.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <exception cref="ValidationTestException">No such child validator.</exception>
    public static void ShouldHaveChildValidator<T, TProperty>(
        this Validator<T> validator,
        Func<T, TProperty> selector,
        Type childValidatorType,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(validator);
        Argument.NotNull(selector);
        Argument.NotNull(childValidatorType);
        var path = RuleNames.Path(typeof(T), selectorExpression);
        var found = new List<string>();
        foreach (var rule in validator.Describe().GetRules(path))
        {
            if (rule.ChildValidatorType is { } type)
            {
                if (childValidatorType.IsAssignableFrom(type))
                {
                    return;
                }

                found.Add(type.Name);
            }
        }

        throw new ValidationTestException(string.Format(
            CultureInfo.InvariantCulture,
            "Expected property '{0}' to have a child validator of type '{1}'. Instead found '{2}'.",
            path,
            childValidatorType.Name,
            found.Count == 0 ? "none" : string.Join(", ", found)));
    }

    private static void AddNew(List<ValidationFailure> target, IEnumerable<ValidationFailure> failures)
    {
        foreach (var failure in failures)
        {
            if (!target.Contains(failure))
            {
                target.Add(failure);
            }
        }
    }

    private static TestValidationContinuation Continue(ITestValidationContinuation failures)
    {
        Argument.NotNull(failures);
        return failures is TestValidationContinuation continuation
            ? new TestValidationContinuation(continuation.Matched(), continuation)
            : new TestValidationContinuation([.. failures.MatchedFailures]);
    }

    private static string Format(ValidationFailure? failure, string? message, string fallback)
    {
        if (message is null || failure is null)
        {
            return message ?? fallback;
        }

        return message
            .Replace("{Code}", failure.ErrorCode)
            .Replace("{Message}", failure.Message)
            .Replace("{State}", failure.CustomState?.ToString() ?? string.Empty)
            .Replace("{Severity}", failure.Severity.ToString());
    }
}
