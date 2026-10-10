using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// A reusable set of rules for <typeparamref name="T"/>. Rules use the same checks as
/// <c>Throw()</c>, but collect every failure into a <see cref="ValidationReport"/>.
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
/// <remarks>
/// <para>
/// Build a validator once (for example as a singleton) and share it. Validating is
/// thread-safe. Adding rules while other threads validate is safe too, but a run that
/// already started does not see the new rule.
/// </para>
/// <para>
/// Each <c>For</c> rule reports at most one failure: the first failed check in its chain.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var validator = new Validator&lt;SignUp&gt;()
///     .For(x =&gt; x.Email, email =&gt; email.IfNullOrWhiteSpace().IfNotEmailAddress())
///     .For(x =&gt; x.Password, password =&gt; password.IfNullOrEmpty().IfShorterThan(12))
///     .Must(x =&gt; x.Password != x.Email, "The password must not be the email address.");
///
/// ValidationReport report = validator.Validate(signUp);
/// </code>
/// </example>
public class Validator<T> : IValidator<T>
{
    private static readonly string[] DefaultGroups = [ValidationGroups.Default];

    private readonly object _gate = new();
    private Rule[] _rules = [];

    /// <summary>
    /// Adds a chain of checks for one property. A failure is named after the property path,
    /// for example <c>"Address.City"</c> for <c>x =&gt; x.Address.City</c>.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the value to check.</param>
    /// <param name="rules">The checks, for example <c>name =&gt; name.IfNullOrWhiteSpace().IfLongerThan(50)</c>.</param>
    /// <param name="name">The name used in failures, or <see langword="null"/> to take it from <paramref name="selector"/>.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> For<TProperty>(
        Func<T, TProperty> selector,
        GuardChain<TProperty> rules,
        string? name = null,
        string? group = null,
        Func<T, bool>? when = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(rules);
        return AddRule(new PropertyRule<TProperty>(selector, rules, name ?? SelectorNames.FromExpression(selectorExpression), group, when));
    }

    /// <summary>
    /// Validates a nested object with its own validator. Failure names get the property path
    /// as a prefix, for example <c>"Address.City"</c>. A <see langword="null"/> value is skipped;
    /// add an <c>IfNull()</c> rule to require it.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the nested object.</param>
    /// <param name="validator">The validator for the nested object.</param>
    /// <param name="name">The name prefix, or <see langword="null"/> to take it from <paramref name="selector"/>.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> For<TProperty>(
        Func<T, TProperty?> selector,
        IValidator<TProperty> validator,
        string? name = null,
        string? group = null,
        Func<T, bool>? when = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(validator);
        return AddRule(new ChildRule<TProperty>(selector, validator, name ?? SelectorNames.FromExpression(selectorExpression), group, when));
    }

    /// <summary>
    /// Adds a chain of checks that runs for every item of a collection. Failure names carry the
    /// index, for example <c>"Tags[2]"</c>. A <see langword="null"/> collection is skipped.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="rules">The checks for one item.</param>
    /// <param name="name">The name used in failures, or <see langword="null"/> to take it from <paramref name="selector"/>.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem>?> selector,
        GuardChain<TItem> rules,
        string? name = null,
        string? group = null,
        Func<T, bool>? when = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(rules);
        return AddRule(new EachRule<TItem>(selector, rules, name ?? SelectorNames.FromExpression(selectorExpression), group, when));
    }

    /// <summary>
    /// Validates every item of a collection with its own validator. Failure names carry the
    /// index, for example <c>"Lines[2].Quantity"</c>. A <see langword="null"/> collection and
    /// <see langword="null"/> items are skipped.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="validator">The validator for one item.</param>
    /// <param name="name">The name prefix, or <see langword="null"/> to take it from <paramref name="selector"/>.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem>?> selector,
        IValidator<TItem> validator,
        string? name = null,
        string? group = null,
        Func<T, bool>? when = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(validator);
        return AddRule(new EachChildRule<TItem>(selector, validator, name ?? SelectorNames.FromExpression(selectorExpression), group, when));
    }

    /// <summary>Adds a rule about the whole object. It fails when <paramref name="predicate"/> returns <see langword="false"/>.</summary>
    /// <param name="predicate">The condition the object must meet.</param>
    /// <param name="message">The error message.</param>
    /// <param name="name">The name used in the failure. The default is an empty string, which means the whole object.</param>
    /// <param name="errorCode">The error code, or <see langword="null"/> for <see cref="ErrorCodes.Must"/>.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.Must(x =&gt; x.End &gt; x.Start, "The end must be after the start.", name: "End")</c></example>
    public Validator<T> Must(
        Func<T, bool> predicate,
        string message,
        string? name = null,
        string? errorCode = null,
        string? group = null,
        Func<T, bool>? when = null)
    {
        Argument.NotNull(predicate);
        Argument.NotNull(message);
        return AddRule(new MustRule(predicate, message, name ?? string.Empty, errorCode ?? ErrorCodes.Must, group, when));
    }

    /// <summary>
    /// Adds an async rule about the whole object, for example a database lookup. It fails when
    /// <paramref name="predicate"/> returns <see langword="false"/>. A validator with async
    /// rules must be run with <see cref="ValidateAsync(T, CancellationToken)"/>.
    /// </summary>
    /// <param name="predicate">The async condition the object must meet.</param>
    /// <param name="message">The error message.</param>
    /// <param name="name">The name used in the failure. The default is an empty string, which means the whole object.</param>
    /// <param name="errorCode">The error code, or <see langword="null"/> for <see cref="ErrorCodes.Must"/>.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.MustAsync(async (x, ct) =&gt; !await users.ExistsAsync(x.Email, ct), "The email is already registered.", name: "Email")</c></example>
    public Validator<T> MustAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        string message,
        string? name = null,
        string? errorCode = null,
        string? group = null,
        Func<T, bool>? when = null)
    {
        Argument.NotNull(predicate);
        Argument.NotNull(message);
        return AddRule(new MustAsyncRule(predicate, message, name ?? string.Empty, errorCode ?? ErrorCodes.Must, group, when));
    }

    /// <summary>
    /// Adds a free-form rule. It gets the object and the report, so it can run any checks,
    /// compare several properties, and add several failures.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.Custom((x, report) =&gt; report.Check(x.Total, "Total").IfNotEqualTo(x.Lines.Sum(l =&gt; l.Price)))</c></example>
    public Validator<T> Custom(Action<T, ValidationReport> rule, string? group = null, Func<T, bool>? when = null)
    {
        Argument.NotNull(rule);
        return AddRule(new CustomRule(rule, group, when));
    }

    /// <summary>
    /// Adds a free-form async rule. A validator with async rules must be run with
    /// <see cref="ValidateAsync(T, CancellationToken)"/>.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> CustomAsync(Func<T, ValidationReport, CancellationToken, Task> rule, string? group = null, Func<T, bool>? when = null)
    {
        Argument.NotNull(rule);
        return AddRule(new CustomAsyncRule(rule, group, when));
    }

    /// <summary>Adds every rule of another validator for the same type. Its failures keep their names.</summary>
    /// <param name="validator">The validator to include.</param>
    /// <param name="group">The group of the rule, or <see langword="null"/> for <see cref="ValidationGroups.Default"/>.</param>
    /// <param name="when">Runs the rule only when this returns <see langword="true"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> Include(IValidator<T> validator, string? group = null, Func<T, bool>? when = null)
    {
        Argument.NotNull(validator);
        if (ReferenceEquals(validator, this))
        {
            throw new ArgumentException("A validator cannot include itself.", nameof(validator));
        }

        return AddRule(new IncludeRule(validator, group, when));
    }

    /// <summary>Runs the rules of the given groups and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">
    /// The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.
    /// Pass <see cref="ValidationGroups.All"/> to run every rule.
    /// </param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A selected rule is async. Use <see cref="ValidateAsync(T, CancellationToken)"/>.</exception>
    public ValidationReport Validate(T value, params string[] groups)
    {
        Argument.NotNull(value);
        var selected = Normalize(groups);
        var report = new ValidationReport();
        foreach (var rule in Volatile.Read(ref _rules))
        {
            if (rule.AppliesTo(value, selected))
            {
                rule.Validate(value, report, selected);
            }
        }

        return report;
    }

    /// <summary>Runs the default rules, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the async rules.</param>
    /// <returns>The report.</returns>
    public Task<ValidationReport> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
        ValidateAsync(value, groups: null, cancellationToken);

    /// <summary>Runs the rules of one group, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="group">The group to run.</param>
    /// <param name="cancellationToken">A token to cancel the async rules.</param>
    /// <returns>The report.</returns>
    public Task<ValidationReport> ValidateAsync(T value, string group, CancellationToken cancellationToken = default) =>
        ValidateAsync(value, [group], cancellationToken);

    /// <inheritdoc/>
    public async Task<ValidationReport> ValidateAsync(T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default)
    {
        Argument.NotNull(value);
        var selected = Normalize(groups is null ? null : [.. groups]);
        var report = new ValidationReport();
        foreach (var rule in Volatile.Read(ref _rules))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rule.AppliesTo(value, selected))
            {
                await rule.ValidateAsync(value, report, selected, cancellationToken).ConfigureAwait(false);
            }
        }

        return report;
    }

    /// <summary>Runs the rules of the given groups and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public void ValidateAndThrow(T value, params string[] groups) => Validate(value, groups).ThrowIfInvalid();

    /// <summary>Runs the default rules, including async rules, and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the async rules.</param>
    /// <returns>A task that completes when validation passes.</returns>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public Task ValidateAndThrowAsync(T value, CancellationToken cancellationToken = default) =>
        ValidateAndThrowAsync(value, groups: null, cancellationToken);

    /// <summary>Runs the rules of the given groups, including async rules, and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With <see langword="null"/> or no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <param name="cancellationToken">A token to cancel the async rules.</param>
    /// <returns>A task that completes when validation passes.</returns>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public async Task ValidateAndThrowAsync(T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default) =>
        (await ValidateAsync(value, groups, cancellationToken).ConfigureAwait(false)).ThrowIfInvalid();

    private Validator<T> AddRule(Rule rule)
    {
        lock (_gate)
        {
            var rules = new Rule[_rules.Length + 1];
            Array.Copy(_rules, rules, _rules.Length);
            rules[_rules.Length] = rule;
            Volatile.Write(ref _rules, rules);
        }

        return this;
    }

    private static string[] Normalize(string[]? groups) => groups is null || groups.Length == 0 ? DefaultGroups : groups;

    private abstract class Rule(string? group, Func<T, bool>? when)
    {
        private readonly string _group = group ?? ValidationGroups.Default;

        public bool AppliesTo(T value, string[] groups) => InGroups(groups) && (when is null || when(value));

        public abstract void Validate(T value, ValidationReport report, string[] groups);

        public virtual Task ValidateAsync(T value, ValidationReport report, string[] groups, CancellationToken cancellationToken)
        {
            Validate(value, report, groups);
            return Task.CompletedTask;
        }

        private bool InGroups(string[] groups)
        {
            foreach (var group in groups)
            {
                if (group == ValidationGroups.All || string.Equals(group, _group, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        protected static InvalidOperationException AsyncRuleInSyncRun() =>
            new("This validator has async rules. Call ValidateAsync instead of Validate.");
    }

    private sealed class PropertyRule<TProperty>(
        Func<T, TProperty> selector, GuardChain<TProperty> rules, string name, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups) =>
            rules(new Guard<TProperty>(selector(value), name, report));
    }

    private sealed class EachRule<TItem>(
        Func<T, IEnumerable<TItem>?> selector, GuardChain<TItem> rules, string name, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups)
        {
            if (selector(value) is not { } items)
            {
                return;
            }

            var index = 0;
            foreach (var item in items)
            {
                rules(new Guard<TItem>(item, Indexed(name, index++), report));
            }
        }
    }

    private sealed class ChildRule<TProperty>(
        Func<T, TProperty?> selector, IValidator<TProperty> validator, string name, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups)
        {
            if (selector(value) is { } child)
            {
                report.AddRange(validator.Validate(child, groups), name);
            }
        }

        public override async Task ValidateAsync(T value, ValidationReport report, string[] groups, CancellationToken cancellationToken)
        {
            if (selector(value) is { } child)
            {
                report.AddRange(await validator.ValidateAsync(child, groups, cancellationToken).ConfigureAwait(false), name);
            }
        }
    }

    private sealed class EachChildRule<TItem>(
        Func<T, IEnumerable<TItem>?> selector, IValidator<TItem> validator, string name, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups)
        {
            if (selector(value) is not { } items)
            {
                return;
            }

            var index = 0;
            foreach (var item in items)
            {
                var prefix = Indexed(name, index++);
                if (item is not null)
                {
                    report.AddRange(validator.Validate(item, groups), prefix);
                }
            }
        }

        public override async Task ValidateAsync(T value, ValidationReport report, string[] groups, CancellationToken cancellationToken)
        {
            if (selector(value) is not { } items)
            {
                return;
            }

            var index = 0;
            foreach (var item in items)
            {
                var prefix = Indexed(name, index++);
                if (item is not null)
                {
                    report.AddRange(await validator.ValidateAsync(item, groups, cancellationToken).ConfigureAwait(false), prefix);
                }
            }
        }
    }

    private sealed class MustRule(
        Func<T, bool> predicate, string message, string name, string errorCode, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups)
        {
            if (!predicate(value))
            {
                report.Add(name, message, errorCode);
            }
        }
    }

    private sealed class MustAsyncRule(
        Func<T, CancellationToken, Task<bool>> predicate, string message, string name, string errorCode, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups) => throw AsyncRuleInSyncRun();

        public override async Task ValidateAsync(T value, ValidationReport report, string[] groups, CancellationToken cancellationToken)
        {
            if (!await predicate(value, cancellationToken).ConfigureAwait(false))
            {
                report.Add(name, message, errorCode);
            }
        }
    }

    private sealed class CustomRule(Action<T, ValidationReport> rule, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups) => rule(value, report);
    }

    private sealed class CustomAsyncRule(Func<T, ValidationReport, CancellationToken, Task> rule, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups) => throw AsyncRuleInSyncRun();

        public override Task ValidateAsync(T value, ValidationReport report, string[] groups, CancellationToken cancellationToken) =>
            rule(value, report, cancellationToken);
    }

    private sealed class IncludeRule(IValidator<T> validator, string? group, Func<T, bool>? when)
        : Rule(group, when)
    {
        public override void Validate(T value, ValidationReport report, string[] groups) =>
            report.AddRange(validator.Validate(value, groups));

        public override async Task ValidateAsync(T value, ValidationReport report, string[] groups, CancellationToken cancellationToken) =>
            report.AddRange(await validator.ValidateAsync(value, groups, cancellationToken).ConfigureAwait(false));
    }

    private static string Indexed(string name, int index) => $"{name}[{index.ToString(CultureInfo.InvariantCulture)}]";
}
