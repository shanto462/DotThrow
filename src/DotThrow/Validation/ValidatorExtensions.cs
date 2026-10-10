using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// Short forms for any <see cref="IValidator{T}"/>, so code that depends on the interface
/// reads the same as code that uses <see cref="Validator{T}"/>.
/// </summary>
public static class ValidatorExtensions
{
    /// <summary>Runs the rules of the given groups and returns every failure.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <returns>The report.</returns>
    public static ValidationReport Validate<T>(this IValidator<T> validator, T value, params string[] groups)
    {
        Argument.NotNull(validator);
        return validator.Validate(value, Options(groups, throwOnFailures: false));
    }

    /// <summary>Runs the rules with options set in a callback (FluentValidation's <c>Validate(instance, options =&gt; ...)</c>).</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Sets the options.</param>
    /// <returns>The report.</returns>
    public static ValidationReport Validate<T>(this IValidator<T> validator, T value, Action<ValidationOptions<T>> configure)
    {
        Argument.NotNull(validator);
        return validator.Validate(value, ValidationOptions<T>.Create(configure));
    }

    /// <summary>Runs the rules, including async rules, with options set in a callback.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Sets the options.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public static Task<ValidationReport> ValidateAsync<T>(this IValidator<T> validator, T value, Action<ValidationOptions<T>> configure, CancellationToken cancellationToken = default)
    {
        Argument.NotNull(validator);
        return validator.ValidateAsync(value, ValidationOptions<T>.Create(configure), cancellationToken);
    }

    /// <summary>Runs the default rules, including async rules, and returns every failure.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public static Task<ValidationReport> ValidateAsync<T>(this IValidator<T> validator, T value, CancellationToken cancellationToken = default)
    {
        Argument.NotNull(validator);
        return validator.ValidateAsync(value, null, cancellationToken);
    }

    /// <summary>Runs the rules of one group, including async rules, and returns every failure.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="group">The group to run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public static Task<ValidationReport> ValidateAsync<T>(this IValidator<T> validator, T value, string group, CancellationToken cancellationToken = default)
    {
        Argument.NotNull(validator);
        return validator.ValidateAsync(value, Options(group is null ? null : [group], throwOnFailures: false), cancellationToken);
    }

    /// <summary>Runs the rules of the given groups, including async rules, and returns every failure.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run, or <see langword="null"/> for the default rules.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public static Task<ValidationReport> ValidateAsync<T>(this IValidator<T> validator, T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default)
    {
        Argument.NotNull(validator);
        return validator.ValidateAsync(value, Options(groups is null ? null : [.. groups], throwOnFailures: false), cancellationToken);
    }

    /// <summary>Runs the rules of the given groups and throws when any rule fails.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public static void ValidateAndThrow<T>(this IValidator<T> validator, T value, params string[] groups)
    {
        Argument.NotNull(validator);
        validator.Validate(value, Options(groups, throwOnFailures: true)).ThrowIfInvalid();
    }

    /// <summary>Runs the default rules, including async rules, and throws when any rule fails.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>A task that completes when validation passes.</returns>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public static Task ValidateAndThrowAsync<T>(this IValidator<T> validator, T value, CancellationToken cancellationToken = default) =>
        ValidateAndThrowAsync(validator, value, null, cancellationToken);

    /// <summary>Runs the rules of the given groups, including async rules, and throws when any rule fails.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="validator">The validator.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run, or <see langword="null"/> for the default rules.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>A task that completes when validation passes.</returns>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public static async Task ValidateAndThrowAsync<T>(this IValidator<T> validator, T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default)
    {
        Argument.NotNull(validator);
        var report = await validator.ValidateAsync(value, Options(groups is null ? null : [.. groups], throwOnFailures: true), cancellationToken).ConfigureAwait(false);
        report.ThrowIfInvalid();
    }

    private static ValidationOptions? Options(string[]? groups, bool throwOnFailures) =>
        (groups is null || groups.Length == 0) && !throwOnFailures
            ? null
            : new ValidationOptions { Groups = groups, ThrowOnFailures = throwOnFailures };
}
