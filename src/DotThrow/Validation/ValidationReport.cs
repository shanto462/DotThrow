using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// Collects validation failures instead of throwing. Use <see cref="Check{T}(T, string?)"/>
/// to run the same checks as <c>Throw()</c> in collect mode, or get a report from
/// <see cref="Validator{T}.Validate(T, string[])"/>.
/// </summary>
/// <remarks>A report is not thread-safe. Use one report per validation run.</remarks>
/// <example>
/// <code>
/// var report = new ValidationReport();
/// report.Check(request.Email).IfNullOrWhiteSpace().IfNotEmailAddress();
/// report.Check(request.Age).IfLessThan(13);
/// if (!report.IsValid)
/// {
///     return Results.ValidationProblem(report.ToDictionary());
/// }
/// </code>
/// </example>
public sealed class ValidationReport
{
    private readonly List<ValidationFailure> _failures = [];

    /// <summary>Gets a value indicating whether no check failed.</summary>
    public bool IsValid => _failures.Count == 0;

    /// <summary>Gets the failures, in the order the checks ran.</summary>
    public IReadOnlyList<ValidationFailure> Failures => _failures;

    /// <summary>
    /// Starts a chain of checks in collect mode. A failed check adds a
    /// <see cref="ValidationFailure"/> to this report, and the rest of that chain is skipped,
    /// so each value reports at most one failure.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="name">The name used in failures. The compiler fills it in with the expression you pass.</param>
    /// <returns>A guard for the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Guard<T> Check<T>(T? value, [CallerArgumentExpression(nameof(value))] string? name = null) =>
        new(value!, name ?? ThrowExtensions.DefaultName, this);

    /// <summary>Adds a failure.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>This report.</returns>
    public ValidationReport Add(ValidationFailure failure)
    {
        Argument.NotNull(failure);
        _failures.Add(failure);
        return this;
    }

    /// <summary>Adds a failure.</summary>
    /// <param name="name">The name of what failed. Use an empty string for the whole object.</param>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">A stable code, or <see langword="null"/> for <see cref="ErrorCodes.Invalid"/>.</param>
    /// <returns>This report.</returns>
    public ValidationReport Add(string name, string message, string? errorCode = null) =>
        Add(new ValidationFailure(name, message, errorCode ?? ErrorCodes.Invalid));

    /// <summary>Adds every failure of another report.</summary>
    /// <param name="other">The other report.</param>
    /// <param name="prefix">
    /// A prefix for the failure names, for example <c>"Address"</c> turns <c>"City"</c> into
    /// <c>"Address.City"</c>. Use <see langword="null"/> or an empty string for no prefix.
    /// </param>
    /// <returns>This report.</returns>
    public ValidationReport AddRange(ValidationReport other, string? prefix = null)
    {
        Argument.NotNull(other);
        foreach (var failure in other._failures)
        {
            _failures.Add(failure.WithPrefix(prefix ?? string.Empty));
        }

        return this;
    }

    /// <summary>Throws a <see cref="ValidationFailedException"/> when the report has failures.</summary>
    /// <exception cref="ValidationFailedException">The report has at least one failure.</exception>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new ValidationFailedException(this);
        }
    }

    /// <summary>
    /// Groups the failure messages by name. The shape matches ASP.NET Core
    /// <c>ValidationProblemDetails.Errors</c>, so it can be passed to
    /// <c>Results.ValidationProblem</c> directly.
    /// </summary>
    /// <returns>A new dictionary from name to messages.</returns>
    public Dictionary<string, string[]> ToDictionary()
    {
        var grouped = new Dictionary<string, List<string>>();
        foreach (var failure in _failures)
        {
            if (!grouped.TryGetValue(failure.Name, out var messages))
            {
                messages = [];
                grouped.Add(failure.Name, messages);
            }

            messages.Add(failure.Message);
        }

        var result = new Dictionary<string, string[]>(grouped.Count);
        foreach (var pair in grouped)
        {
            result.Add(pair.Key, [.. pair.Value]);
        }

        return result;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsValid)
        {
            return "Validation passed.";
        }

        var text = new StringBuilder("Validation failed with ")
            .Append(_failures.Count)
            .Append(_failures.Count == 1 ? " error:" : " errors:");
        foreach (var failure in _failures)
        {
            text.AppendLine().Append(" - ").Append(failure);
        }

        return text.ToString();
    }
}
