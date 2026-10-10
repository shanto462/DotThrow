using System;
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
    private List<ValidationFailure>? _failures;

    /// <summary>Gets a value indicating whether no check failed. Failures of every severity count.</summary>
    public bool IsValid => Count == 0;

    /// <summary>Gets the failures, in the order the checks ran.</summary>
    public IReadOnlyList<ValidationFailure> Failures => (IReadOnlyList<ValidationFailure>?)_failures ?? Array.Empty<ValidationFailure>();

    /// <summary>
    /// Gets the groups whose rules ran, set by <see cref="Validator{T}"/>. A run without groups
    /// reports <see cref="ValidationGroups.Default"/>. A run with groups reports the groups that
    /// selected at least one rule. A report made by hand reports none.
    /// </summary>
    public IReadOnlyList<string> GroupsExecuted { get; internal set; } = Array.Empty<string>();

    internal int Count => _failures?.Count ?? 0;

    // The Validator<T> run that fills this report, or null. A failed check in collect mode
    // goes through the rule that runs right now (its Frame), so rule options (path, message,
    // code, severity, state, cascade) apply. Outside a run failures are added as they are.
    internal RunState? Run { get; set; }

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

    /// <summary>
    /// Starts a chain of checks in collect mode for a property of <paramref name="root"/>. The
    /// name comes from the selector, the same as in <see cref="Validator{T}"/> rules, so
    /// <c>report.Check(form, f =&gt; f.Name)</c> is named <c>"Name"</c> instead of <c>"form.Name"</c>.
    /// <see cref="ValidatorDefaults.PropertyNameResolver"/> applies.
    /// </summary>
    /// <typeparam name="TRoot">The type of the object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="root">The object that has the value.</param>
    /// <param name="selector">Selects the value, for example <c>f =&gt; f.Name</c>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>A guard for the value.</returns>
    /// <example><c>report.Check(form, f =&gt; f.Email).IfNullOrWhiteSpace().IfNotEmailAddress();</c></example>
    public Guard<TValue> Check<TRoot, TValue>(TRoot root, Func<TRoot, TValue?> selector, [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        return new Guard<TValue>(selector(root)!, RuleNames.Path(typeof(TRoot), selectorExpression), this);
    }

    /// <summary>Adds a failure.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>This report.</returns>
    public ValidationReport Add(ValidationFailure failure)
    {
        Argument.NotNull(failure);
        AddCore(failure);
        return this;
    }

    /// <summary>Adds a failure.</summary>
    /// <param name="name">The name of what failed. Use an empty string for the whole object.</param>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">A stable code, or <see langword="null"/> for <see cref="ErrorCodes.Invalid"/>.</param>
    /// <returns>This report.</returns>
    public ValidationReport Add(string name, string message, string? errorCode = null) =>
        Add(new ValidationFailure(name, message, errorCode ?? ErrorCodes.Invalid));

    /// <summary>Adds every failure of another report. Adding a report to itself is allowed.</summary>
    /// <param name="other">The other report.</param>
    /// <param name="prefix">
    /// A prefix for the failure names, for example <c>"Address"</c> turns <c>"City"</c> into
    /// <c>"Address.City"</c>. Use <see langword="null"/> or an empty string for no prefix.
    /// </param>
    /// <returns>This report.</returns>
    public ValidationReport AddRange(ValidationReport other, string? prefix = null)
    {
        Argument.NotNull(other);
        if (other._failures is not { Count: > 0 } source)
        {
            return this;
        }

        // Copy first, so adding a report to itself does not change the list while reading it.
        var failures = ReferenceEquals(other, this) ? source.ToArray() : (IReadOnlyList<ValidationFailure>)source;
        var list = _failures ??= new List<ValidationFailure>(failures.Count);
        for (var i = 0; i < failures.Count; i++)
        {
            list.Add(failures[i].WithPrefix(prefix ?? string.Empty));
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
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (_failures is not null)
        {
            foreach (var failure in _failures)
            {
                if (!grouped.TryGetValue(failure.Name, out var messages))
                {
                    messages = [];
                    grouped.Add(failure.Name, messages);
                }

                messages.Add(failure.Message);
            }
        }

        var result = new Dictionary<string, string[]>(grouped.Count, StringComparer.Ordinal);
        foreach (var pair in grouped)
        {
            result.Add(pair.Key, [.. pair.Value]);
        }

        return result;
    }

    /// <summary>Joins the failure messages with a separator, like FluentValidation's <c>ToString(separator)</c>.</summary>
    /// <param name="separator">The text between two messages, for example <c>"; "</c>.</param>
    /// <returns>The messages, or an empty string when the report is valid.</returns>
    public string ToString(string separator)
    {
        Argument.NotNull(separator);
        if (_failures is null)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        for (var i = 0; i < _failures.Count; i++)
        {
            if (i > 0)
            {
                text.Append(separator);
            }

            text.Append(_failures[i].Message);
        }

        return text.ToString();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (_failures is null)
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

    // Called by Guard<T> for a failed check in collect mode. Returns true when the chain stops.
    internal bool AddCheckFailure(string name, string message, string errorCode, object? attemptedValue, bool alwaysStop)
    {
        var run = Run;
        if (run is null || run.Frame.Sink is not { } sink)
        {
            AddCore(new ValidationFailure(name, message, errorCode) { AttemptedValue = attemptedValue });
            return true;
        }

        return sink.OnCheckFailure(this, run.Frame, name, message, errorCode, attemptedValue) || alwaysStop;
    }

    // Adds a failure created by a validator rule, after the global hook.
    internal void AddRuleFailure(ValidationFailure failure)
    {
        if (ValidatorDefaults.OnFailureCreated is { } hook)
        {
            failure = hook(failure) ?? failure;
        }

        AddCore(failure);
    }

    private void AddCore(ValidationFailure failure) => (_failures ??= []).Add(failure);
}
