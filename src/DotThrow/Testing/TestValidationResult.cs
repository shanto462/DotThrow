using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow.Testing;

/// <summary>
/// The result of <see cref="TestValidationExtensions.TestValidate{T}(IValidator{T}, T, ValidationOptions?)"/>,
/// with assertions in the shape of FluentValidation's TestHelper. A failed assertion throws a
/// <see cref="ValidationTestException"/>.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <example>
/// <code>
/// var result = validator.TestValidate(new User { Email = "nope" });
/// result.ShouldHaveValidationErrorFor(x =&gt; x.Email).WithErrorCode(ErrorCodes.NotEmailAddress).Only();
/// result.ShouldNotHaveValidationErrorFor(x =&gt; x.Name);
/// </code>
/// </example>
public sealed class TestValidationResult<T>
{
    internal TestValidationResult(ValidationReport report)
    {
        Report = report;
    }

    /// <summary>Gets the report of the run.</summary>
    public ValidationReport Report { get; }

    /// <summary>Gets a value indicating whether no rule failed.</summary>
    public bool IsValid => Report.IsValid;

    /// <summary>Gets the failures of the run.</summary>
    public IReadOnlyList<ValidationFailure> Failures => Report.Failures;

    /// <summary>Gets the groups whose rules ran.</summary>
    public IReadOnlyList<string> GroupsExecuted => Report.GroupsExecuted;

    /// <summary>
    /// Asserts that the property has at least one failure. The name comes from the selector, the
    /// same as in <see cref="Validator{T}"/> rules, and collection indexes in failure names are
    /// ignored, so <c>x =&gt; x.Lines</c> matches <c>"Lines[2]"</c>.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the property, for example <c>x =&gt; x.Address.City</c>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>The matched failures, for more conditions.</returns>
    /// <exception cref="ValidationTestException">The property has no failure.</exception>
    public ITestValidationWith ShouldHaveValidationErrorFor<TProperty>(
        Func<T, TProperty> selector,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        return ShouldHave(RuleNames.Path(typeof(T), selectorExpression), ignoreIndexes: true);
    }

    /// <summary>Asserts that the failure name has at least one failure. The name must match exactly, for example <c>"Lines[2].Quantity"</c>.</summary>
    /// <param name="propertyName">The failure name. An empty string means the whole object.</param>
    /// <returns>The matched failures, for more conditions.</returns>
    /// <exception cref="ValidationTestException">The name has no failure.</exception>
    public ITestValidationWith ShouldHaveValidationErrorFor(string propertyName)
    {
        Argument.NotNull(propertyName);
        return ShouldHave(propertyName, ignoreIndexes: false);
    }

    /// <summary>Asserts that the property has no failure. Collection indexes in failure names are ignored.</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the property.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <exception cref="ValidationTestException">The property has a failure.</exception>
    public void ShouldNotHaveValidationErrorFor<TProperty>(
        Func<T, TProperty> selector,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        ShouldNotHave(RuleNames.Path(typeof(T), selectorExpression), ignoreIndexes: true);
    }

    /// <summary>Asserts that the failure name has no failure. The name must match exactly.</summary>
    /// <param name="propertyName">The failure name.</param>
    /// <exception cref="ValidationTestException">The name has a failure.</exception>
    public void ShouldNotHaveValidationErrorFor(string propertyName)
    {
        Argument.NotNull(propertyName);
        ShouldNotHave(propertyName, ignoreIndexes: false);
    }

    /// <summary>Asserts that no rule failed.</summary>
    /// <exception cref="ValidationTestException">At least one rule failed.</exception>
    public void ShouldNotHaveAnyValidationErrors()
    {
        if (!Report.IsValid)
        {
            throw new ValidationTestException(TestMessages.Describe("Expected no validation errors", "Validation errors:", Report.Failures, useNames: false), Report.Failures);
        }
    }

    /// <summary>Asserts that at least one rule failed.</summary>
    /// <returns>All failures, for more conditions.</returns>
    /// <exception cref="ValidationTestException">No rule failed.</exception>
    public ITestValidationContinuation ShouldHaveValidationErrors()
    {
        if (Report.IsValid)
        {
            throw new ValidationTestException("Expected at least one validation error, but none were found.");
        }

        return new TestValidationContinuation(Report.Failures);
    }

    private static bool NameMatches(ValidationFailure failure, string name, bool ignoreIndexes) =>
        string.Equals(ignoreIndexes ? PropertyPaths.RemoveIndexes(failure.Name) : failure.Name, name, StringComparison.Ordinal)
        || string.Equals(failure.Name, name, StringComparison.Ordinal);

    private TestValidationContinuation ShouldHave(string name, bool ignoreIndexes)
    {
        var result = new TestValidationContinuation(Report.Failures);
        result.Add(failure => NameMatches(failure, name, ignoreIndexes));
        if (result.Matched().Count > 0)
        {
            return result;
        }

        throw new ValidationTestException(TestMessages.Describe($"Expected a validation error for property {name}", "Properties with validation errors:", Report.Failures, useNames: true), Report.Failures);
    }

    private void ShouldNotHave(string name, bool ignoreIndexes)
    {
        var failures = new List<ValidationFailure>();
        foreach (var failure in Report.Failures)
        {
            if (NameMatches(failure, name, ignoreIndexes))
            {
                failures.Add(failure);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationTestException(TestMessages.Describe($"Expected no validation errors for property {name}", "Validation errors:", failures, useNames: false), failures);
        }
    }
}
