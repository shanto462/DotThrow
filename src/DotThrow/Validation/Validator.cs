using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// A reusable set of rules for <typeparamref name="T"/>. Rules use the same checks as
/// <c>Throw()</c>, but collect every failure into a <see cref="ValidationReport"/>.
/// This is DotThrow's version of FluentValidation's <c>AbstractValidator&lt;T&gt;</c> and
/// <c>InlineValidator&lt;T&gt;</c>: subclass it, or build it inline.
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
/// <remarks>
/// <para>
/// Build a validator once (for example as a singleton) and share it. Validating is
/// thread-safe and lock-free. Adding rules while other threads validate is safe too, but a
/// run that already started does not see the new rule.
/// </para>
/// <para>
/// By default each rule reports at most one failure: the first failed check in its chain
/// (<see cref="RuleCascade"/> is <see cref="CascadeMode.Stop"/>), and every rule runs
/// (<see cref="ClassCascade"/> is <see cref="CascadeMode.Continue"/>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var validator = new Validator&lt;SignUp&gt;()
///     .For(x =&gt; x.Email, email =&gt; email.IfNullOrWhiteSpace().IfNotEmailAddress())
///     .For(x =&gt; x.Password, password =&gt; password.IfNullOrEmpty().IfShorterThan(12))
///     .For(x =&gt; x.ConfirmPassword, (confirm, x) =&gt; confirm.IfNotEqualTo(x.Password, StringComparison.Ordinal))
///     .Must(x =&gt; x.Password != x.Email, "The password must not be the email address.");
///
/// ValidationReport report = validator.Validate(signUp);
/// </code>
/// </example>
public partial class Validator<T> : IValidator<T>, IChildValidator<T>, IIncludeGraph
{
    private readonly object _gate = new();
    private readonly bool _callsPreValidate;
    private Rule<T>[] _rules = [];
    private BuildScope? _scope;
    private CascadeMode? _ruleCascade;
    private CascadeMode? _classCascade;

    /// <summary>Initializes a new instance of the <see cref="Validator{T}"/> class with no rules.</summary>
    public Validator()
    {
        // Only a subclass can override PreValidate; a plain validator skips the call.
        _callsPreValidate = GetType() != typeof(Validator<T>);
    }

    /// <summary>
    /// Gets or sets what a rule does after a failed check, for rules that do not set their own
    /// cascade. The default comes from <see cref="ValidatorDefaults.RuleCascade"/>
    /// (<see cref="CascadeMode.Stop"/>: one failure per rule).
    /// </summary>
    public CascadeMode RuleCascade
    {
        get => _ruleCascade ?? ValidatorDefaults.RuleCascade;
        set => _ruleCascade = value;
    }

    /// <summary>
    /// Gets or sets what the validator does after a failed rule: <see cref="CascadeMode.Continue"/>
    /// runs every rule, <see cref="CascadeMode.Stop"/> stops at the first rule that fails. The
    /// default comes from <see cref="ValidatorDefaults.ClassCascade"/>.
    /// </summary>
    public CascadeMode ClassCascade
    {
        get => _classCascade ?? ValidatorDefaults.ClassCascade;
        set => _classCascade = value;
    }

    // Created by For/ForEach with inline child rules; its rules take the groups of the parent rule.
    internal bool IsInline { get; private set; }

    /// <summary>
    /// Adds a chain of checks for one property. A failure is named after the property path,
    /// for example <c>"Address.City"</c> for <c>x =&gt; x.Address.City</c>. The selector may
    /// compute a value, for example <c>x =&gt; x.Name.Trim()</c>; name such rules with
    /// <see cref="RuleOptionsBase{T, TValue, TOptions}.OverridePropertyName(string)"/>.
    /// </summary>
    /// <typeparam name="TProperty">The property type. For a <c>string?</c> property it is <c>string</c>.</typeparam>
    /// <param name="selector">Selects the value to check.</param>
    /// <param name="rules">The checks, for example <c>name =&gt; name.IfNullOrWhiteSpace().IfLongerThan(50)</c>.</param>
    /// <param name="configure">Sets rule options such as <c>WithName</c>, <c>When</c> or <c>InGroup</c>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> For<TProperty>(
        Func<T, TProperty?> selector,
        GuardChain<TProperty> rules,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(rules);
        return Add(new PropertyRule<T, TProperty>(this, selector, rules, null, PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>
    /// Adds a chain of checks for one property that can read the whole object, for example to
    /// compare two properties: <c>For(x =&gt; x.End, (end, x) =&gt; end.IfBefore(x.Start))</c>.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the value to check.</param>
    /// <param name="rules">The checks. The second parameter is the validated object.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> For<TProperty>(
        Func<T, TProperty?> selector,
        GuardChain<TProperty, T> rules,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(rules);
        return Add(new PropertyRule<T, TProperty>(this, selector, null, rules, PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>
    /// Validates a nested object with its own validator (FluentValidation's <c>SetValidator</c>).
    /// Failure names get the property path as a prefix, for example <c>"Address.City"</c>; messages
    /// keep the child name (<c>'City'</c>). A <see langword="null"/> value is skipped; add an
    /// <c>IfNull()</c> rule to require it. The child runs with the same groups as the parent,
    /// unless <see cref="RuleOptionsBase{T, TValue, TOptions}.WithChildGroups(string[])"/> says otherwise.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the nested object.</param>
    /// <param name="validator">The validator for the nested object. A <see cref="PolymorphicValidator{T}"/> picks one by runtime type.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> For<TProperty>(
        Func<T, TProperty?> selector,
        IValidator<TProperty> validator,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(validator);
        return Add(new ChildRule<T, TProperty>(this, selector, validator, PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>
    /// Adds rules for a nested object inline (FluentValidation's <c>ChildRules</c>):
    /// <c>For(x =&gt; x.Address, address =&gt; address.For(a =&gt; a.City, city =&gt; city.IfNullOrEmpty()))</c>.
    /// The inline rules take the groups of this rule when they have none.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the nested object.</param>
    /// <param name="childRules">Adds the rules for the nested object.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> For<TProperty>(
        Func<T, TProperty?> selector,
        Action<Validator<TProperty>> childRules,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(childRules);
        return For(selector, Inline(childRules), configure, selectorExpression);
    }

    /// <summary>
    /// Validates a nested object with a validator picked per object (FluentValidation's
    /// <c>SetValidator(x =&gt; validator)</c>). Returning <see langword="null"/> skips the value.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TValidator">The validator type, reported by <see cref="Describe"/>.</typeparam>
    /// <param name="selector">Selects the nested object.</param>
    /// <param name="validatorProvider">Picks the validator from the validated object.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.For(x =&gt; x.Address, x =&gt; x.IsAbroad ? foreignAddress : localAddress)</c></example>
    public Validator<T> For<TProperty, TValidator>(
        Func<T, TProperty?> selector,
        Func<T, TValidator> validatorProvider,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
        where TValidator : IValidator<TProperty>?
    {
        Argument.NotNull(selector);
        Argument.NotNull(validatorProvider);
        return Add(new ChildRule<T, TProperty>(this, selector, (instance, _) => validatorProvider(instance), typeof(TValidator), PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>Validates a nested object with a validator picked from the object and the value (FluentValidation's <c>SetValidator((x, value) =&gt; validator)</c>).</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TValidator">The validator type, reported by <see cref="Describe"/>.</typeparam>
    /// <param name="selector">Selects the nested object.</param>
    /// <param name="validatorProvider">Picks the validator from the validated object and the nested value.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> For<TProperty, TValidator>(
        Func<T, TProperty?> selector,
        Func<T, TProperty, TValidator> validatorProvider,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
        where TValidator : IValidator<TProperty>?
    {
        Argument.NotNull(selector);
        Argument.NotNull(validatorProvider);
        return Add(new ChildRule<T, TProperty>(this, selector, (instance, value) => validatorProvider(instance, value), typeof(TValidator), PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>
    /// Adds a chain of checks that runs for every item of a collection (FluentValidation's
    /// <c>RuleForEach</c>). Failure names carry the index, for example <c>"Tags[2]"</c>; messages
    /// use the collection name. A <see langword="null"/> collection is skipped. The rule cascade
    /// applies per item: every item is checked.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="rules">The checks for one item.</param>
    /// <param name="configure">Sets rule options, including <c>Where</c> and <c>WithIndexer</c>.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem?>?> selector,
        GuardChain<TItem> rules,
        Action<EachRuleOptions<T, TItem>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(rules);
        return AddEach(new EachRule<T, TItem>(this, selector, rules, null, PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>Adds a chain of checks for every item of a collection that can read the whole object.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="rules">The checks for one item. The second parameter is the validated object.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem?>?> selector,
        GuardChain<TItem, T> rules,
        Action<EachRuleOptions<T, TItem>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(rules);
        return AddEach(new EachRule<T, TItem>(this, selector, null, rules, PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>
    /// Validates every item of a collection with its own validator. Failure names carry the
    /// index, for example <c>"Lines[2].Quantity"</c>. A <see langword="null"/> collection and
    /// <see langword="null"/> items are skipped.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="validator">The validator for one item.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem?>?> selector,
        IValidator<TItem> validator,
        Action<EachRuleOptions<T, TItem>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(validator);
        return AddEach(new EachChildRule<T, TItem>(this, selector, validator, PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>Validates every item of a collection with a validator picked per object (FluentValidation's <c>RuleForEach(...).SetValidator(x =&gt; validator)</c>).</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <typeparam name="TValidator">The validator type, reported by <see cref="Describe"/>.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="validatorProvider">Picks the validator from the validated object. <see langword="null"/> skips the items.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem, TValidator>(
        Func<T, IEnumerable<TItem?>?> selector,
        Func<T, TValidator> validatorProvider,
        Action<EachRuleOptions<T, TItem>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
        where TValidator : IValidator<TItem>?
    {
        Argument.NotNull(selector);
        Argument.NotNull(validatorProvider);
        return AddEach(new EachChildRule<T, TItem>(this, selector, (instance, _) => validatorProvider(instance), typeof(TValidator), PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>Validates every item of a collection with a validator picked per item.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <typeparam name="TValidator">The validator type, reported by <see cref="Describe"/>.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="validatorProvider">Picks the validator from the validated object and the item. <see langword="null"/> skips the item.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem, TValidator>(
        Func<T, IEnumerable<TItem?>?> selector,
        Func<T, TItem, TValidator> validatorProvider,
        Action<EachRuleOptions<T, TItem>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
        where TValidator : IValidator<TItem>?
    {
        Argument.NotNull(selector);
        Argument.NotNull(validatorProvider);
        return AddEach(new EachChildRule<T, TItem>(this, selector, (instance, item) => validatorProvider(instance, item), typeof(TValidator), PathOf(selectorExpression), selectorExpression), configure);
    }

    /// <summary>
    /// Adds rules for every item of a collection inline (FluentValidation's
    /// <c>RuleForEach(...).ChildRules(...)</c>). The inline rules take the groups of this rule
    /// when they have none.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="selector">Selects the collection.</param>
    /// <param name="childRules">Adds the rules for one item.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem?>?> selector,
        Action<Validator<TItem>> childRules,
        Action<EachRuleOptions<T, TItem>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(childRules);
        return ForEach(selector, Inline(childRules), configure, selectorExpression);
    }

    /// <summary>Adds a rule about the whole object. It fails when <paramref name="predicate"/> returns <see langword="false"/>.</summary>
    /// <param name="predicate">The condition the object must meet.</param>
    /// <param name="message">The error message, or <see langword="null"/> for a default one.</param>
    /// <param name="configure">Sets rule options. <c>WithName</c> also names the failure, which is empty (the whole object) by default.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.Must(x =&gt; x.End &gt; x.Start, "The end must be after the start.", rule =&gt; rule.WithName("End"))</c></example>
    public Validator<T> Must(Func<T, bool> predicate, string? message = null, Action<RuleOptions<T, T>>? configure = null)
    {
        Argument.NotNull(predicate);
        return Add(new MustRule<T, T>(this, Identity, (instance, _) => predicate(instance), message, string.Empty, null, objectLevel: true), configure);
    }

    /// <summary>Adds a rule for one property that fails when <paramref name="predicate"/> returns <see langword="false"/>.</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the value.</param>
    /// <param name="predicate">The condition the value must meet.</param>
    /// <param name="message">The error message, or <see langword="null"/> for a default one.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> Must<TProperty>(
        Func<T, TProperty?> selector,
        Func<TProperty, bool> predicate,
        string? message = null,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(predicate);
        return Add(new MustRule<T, TProperty>(this, selector, (_, value) => predicate(value), message, PathOf(selectorExpression), selectorExpression, objectLevel: false), configure);
    }

    /// <summary>Adds a rule for one property that can read the whole object. It fails when <paramref name="predicate"/> returns <see langword="false"/>.</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the value.</param>
    /// <param name="predicate">The condition, with the object first and the value second.</param>
    /// <param name="message">The error message, or <see langword="null"/> for a default one.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> Must<TProperty>(
        Func<T, TProperty?> selector,
        Func<T, TProperty, bool> predicate,
        string? message = null,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(predicate);
        return Add(new MustRule<T, TProperty>(this, selector, predicate, message, PathOf(selectorExpression), selectorExpression, objectLevel: false), configure);
    }

    /// <summary>
    /// Adds an async rule about the whole object, for example a database lookup. It fails when
    /// <paramref name="predicate"/> returns <see langword="false"/>. A validator with async
    /// rules must be run with <c>ValidateAsync</c>.
    /// </summary>
    /// <param name="predicate">The async condition the object must meet.</param>
    /// <param name="message">The error message, or <see langword="null"/> for a default one.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <returns>This validator.</returns>
    public Validator<T> MustAsync(Func<T, CancellationToken, Task<bool>> predicate, string? message = null, Action<RuleOptions<T, T>>? configure = null)
    {
        Argument.NotNull(predicate);
        return Add(new MustAsyncRule<T, T>(this, Identity, (instance, _, token) => predicate(instance, token), message, string.Empty, null, objectLevel: true), configure);
    }

    /// <summary>Adds an async rule for one property, for example a uniqueness lookup.</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the value.</param>
    /// <param name="predicate">The async condition the value must meet.</param>
    /// <param name="message">The error message, or <see langword="null"/> for a default one.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.MustAsync(x =&gt; x.Email, async (email, ct) =&gt; !await users.ExistsAsync(email, ct), "The email is already registered.")</c></example>
    public Validator<T> MustAsync<TProperty>(
        Func<T, TProperty?> selector,
        Func<TProperty, CancellationToken, Task<bool>> predicate,
        string? message = null,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(predicate);
        return Add(new MustAsyncRule<T, TProperty>(this, selector, (_, value, token) => predicate(value, token), message, PathOf(selectorExpression), selectorExpression, objectLevel: false), configure);
    }

    /// <summary>Adds an async rule for one property that can read the whole object.</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Selects the value.</param>
    /// <param name="predicate">The async condition, with the object first and the value second.</param>
    /// <param name="message">The error message, or <see langword="null"/> for a default one.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>This validator.</returns>
    public Validator<T> MustAsync<TProperty>(
        Func<T, TProperty?> selector,
        Func<T, TProperty, CancellationToken, Task<bool>> predicate,
        string? message = null,
        Action<RuleOptions<T, TProperty>>? configure = null,
        [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        Argument.NotNull(predicate);
        return Add(new MustAsyncRule<T, TProperty>(this, selector, predicate, message, PathOf(selectorExpression), selectorExpression, objectLevel: false), configure);
    }

    /// <summary>
    /// Adds a free-form rule. It gets the object and the <see cref="ValidationContext{T}"/>, so it
    /// can run any checks with <see cref="ValidationContext.Check{TValue}(TValue, string?)"/>,
    /// compare several properties, read <see cref="ValidationContext.Items"/>, and add several failures.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="configure">Sets rule options. Message, code, severity and state apply to failures added through the context.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.Custom((x, context) =&gt; context.Check(x.Total, "Total").IfNotEqualTo(x.Lines.Sum(l =&gt; l.Price)))</c></example>
    public Validator<T> Custom(Action<T, ValidationContext<T>> rule, Action<RuleOptions<T, T>>? configure = null)
    {
        Argument.NotNull(rule);
        return Add(new CustomRule<T>(this, rule), configure);
    }

    /// <summary>
    /// Adds a free-form async rule. A validator with async rules must be run with <c>ValidateAsync</c>.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <returns>This validator.</returns>
    public Validator<T> CustomAsync(Func<T, ValidationContext<T>, CancellationToken, Task> rule, Action<RuleOptions<T, T>>? configure = null)
    {
        Argument.NotNull(rule);
        return Add(new CustomRule<T>(this, rule), configure);
    }

    /// <summary>
    /// Adds every rule of another validator for the same object (or a base type). Its failures
    /// keep their names. Without a group of its own, the include runs in every run and its rules
    /// are filtered by the groups of the run, the same as FluentValidation.
    /// </summary>
    /// <param name="validator">The validator to include.</param>
    /// <param name="configure">Sets rule options (conditions, groups, child groups).</param>
    /// <returns>This validator.</returns>
    /// <exception cref="ArgumentException">The validator includes this one, directly or through other validators.</exception>
    public Validator<T> Include(IValidator<T> validator, Action<RuleOptions<T, T>>? configure = null)
    {
        Argument.NotNull(validator);
        if (ReferenceEquals(validator, this)
            || (validator is IIncludeGraph graph && graph.Includes(this, new HashSet<object>())))
        {
            throw new ArgumentException("A validator cannot include itself, directly or through other validators.", nameof(validator));
        }

        return Add(new IncludeRule<T>(this, validator), configure);
    }

    /// <summary>
    /// Adds the rules of a validator picked per object (FluentValidation's
    /// <c>Include(x =&gt; validator)</c>). Returning <see langword="null"/> includes nothing.
    /// Cycles through providers are not found when the rule is added; the run stops at
    /// <see cref="ValidatorDefaults.MaxDepth"/> instead.
    /// </summary>
    /// <typeparam name="TValidator">The validator type, reported by <see cref="Describe"/>.</typeparam>
    /// <param name="validatorProvider">Picks the validator from the validated object.</param>
    /// <param name="configure">Sets rule options.</param>
    /// <returns>This validator.</returns>
    public Validator<T> Include<TValidator>(Func<T, TValidator> validatorProvider, Action<RuleOptions<T, T>>? configure = null)
        where TValidator : IValidator<T>?
    {
        Argument.NotNull(validatorProvider);
        return Add(new IncludeRule<T>(this, instance => validatorProvider(instance), typeof(TValidator)), configure);
    }

    /// <summary>
    /// Runs the rules added in <paramref name="rules"/> only when <paramref name="condition"/>
    /// returns <see langword="true"/>, and the rules added in <paramref name="otherwise"/> only
    /// when it returns <see langword="false"/>. The condition runs once per object.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.When(x =&gt; x.IsCompany, v =&gt; v.For(x =&gt; x.VatNumber, vat =&gt; vat.IfNullOrWhiteSpace()))</c></example>
    public Validator<T> When(Func<T, bool> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>((instance, _) => condition(instance), negate: false, shared: true), rules, otherwise);
    }

    /// <summary>Runs the rules added in <paramref name="rules"/> only when <paramref name="condition"/> returns <see langword="true"/>.</summary>
    /// <param name="condition">The condition, with the validation context.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> When(Func<T, ValidationContext<T>, bool> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>(condition, negate: false, shared: true), rules, otherwise);
    }

    /// <summary>Runs the rules added in <paramref name="rules"/> only when <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> Unless(Func<T, bool> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>((instance, _) => condition(instance), negate: true, shared: true), rules, otherwise);
    }

    /// <summary>Runs the rules added in <paramref name="rules"/> only when <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The condition, with the validation context.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> Unless(Func<T, ValidationContext<T>, bool> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>(condition, negate: true, shared: true), rules, otherwise);
    }

    /// <summary>Runs the rules added in <paramref name="rules"/> only when the async <paramref name="condition"/> returns <see langword="true"/>. The validator then needs <c>ValidateAsync</c>.</summary>
    /// <param name="condition">The async condition.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> WhenAsync(Func<T, CancellationToken, Task<bool>> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>((instance, _, token) => condition(instance, token), negate: false, shared: true), rules, otherwise);
    }

    /// <summary>Runs the rules added in <paramref name="rules"/> only when the async <paramref name="condition"/> returns <see langword="true"/>.</summary>
    /// <param name="condition">The async condition, with the validation context.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> WhenAsync(Func<T, ValidationContext<T>, CancellationToken, Task<bool>> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>(condition, negate: false, shared: true), rules, otherwise);
    }

    /// <summary>Runs the rules added in <paramref name="rules"/> only when the async <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The async condition.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> UnlessAsync(Func<T, CancellationToken, Task<bool>> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>((instance, _, token) => condition(instance, token), negate: true, shared: true), rules, otherwise);
    }

    /// <summary>Runs the rules added in <paramref name="rules"/> only when the async <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The async condition, with the validation context.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <param name="otherwise">Adds the rules for the other case, or <see langword="null"/>.</param>
    /// <returns>This validator.</returns>
    public Validator<T> UnlessAsync(Func<T, ValidationContext<T>, CancellationToken, Task<bool>> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise = null)
    {
        Argument.NotNull(condition);
        return Block(new Condition<T>(condition, negate: true, shared: true), rules, otherwise);
    }

    /// <summary>
    /// Puts the rules added in <paramref name="rules"/> in a group (FluentValidation's
    /// <c>RuleSet</c>). Rules that set their own groups keep them.
    /// </summary>
    /// <param name="group">The group name. Names ignore case.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <returns>This validator.</returns>
    /// <example><c>.Group("admin", v =&gt; v.For(x =&gt; x.Role, role =&gt; role.IfNullOrEmpty()))</c></example>
    public Validator<T> Group(string group, Action<Validator<T>> rules) => Group([group], rules);

    /// <summary>Puts the rules added in <paramref name="rules"/> in several groups.</summary>
    /// <param name="groups">The group names. Names ignore case.</param>
    /// <param name="rules">Adds the rules to this validator.</param>
    /// <returns>This validator.</returns>
    public Validator<T> Group(IReadOnlyList<string> groups, Action<Validator<T>> rules)
    {
        Argument.NotNull(groups);
        Argument.NotNull(rules);
        var names = new string[groups.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = groups[i];
        }

        var checkedNames = CheckGroups(names);
        lock (_gate)
        {
            RunScope(new BuildScope(_scope, checkedNames, null, null), rules);
        }

        return this;
    }

    /// <summary>
    /// Runs for every rule added to this validator, after its <c>configure</c> callback and
    /// before the rule is used (FluentValidation's <c>OnRuleAdded</c>). Override it to apply
    /// defaults to every rule, for example a severity or a condition.
    /// </summary>
    /// <param name="rule">The options of the new rule. The value type is <see cref="object"/>, so typed value overloads get the boxed value.</param>
    /// <example>
    /// <code>
    /// protected override void OnRuleAdded(RuleOptions&lt;Order, object?&gt; rule)
    /// {
    ///     if (rule.PropertyPath.StartsWith("Internal", StringComparison.Ordinal))
    ///     {
    ///         rule.WithSeverity(Severity.Warning);
    ///     }
    /// }
    /// </code>
    /// </example>
    protected virtual void OnRuleAdded(RuleOptions<T, object?> rule)
    {
    }

    internal static string[] CheckGroups(string[] groups)
    {
        Argument.NotNull(groups);
        if (groups.Length == 0)
        {
            throw new ArgumentException("At least one group is required.", nameof(groups));
        }

        var result = new string[groups.Length];
        for (var i = 0; i < groups.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(groups[i]))
            {
                throw new ArgumentException("Group names must not be empty.", nameof(groups));
            }

            result[i] = groups[i].Trim();
        }

        return result;
    }

    // DependentRules: runs the callback and returns the rules it added, instead of adding them.
    internal Rule<T>[] CaptureRules(Action<Validator<T>> rules)
    {
        var captured = new List<Rule<T>>();
        lock (_gate)
        {
            RunScope(new BuildScope(_scope, null, null, captured), rules);
        }

        return [.. captured];
    }

    // Inline child rules get the groups of their parent rule when they have none.
    internal void InheritGroups(string[] groups)
    {
        lock (_gate)
        {
            foreach (var rule in _rules)
            {
                rule.InheritGroups(groups);
            }
        }
    }

    bool IIncludeGraph.Includes(object target, HashSet<object> visited) =>
        visited.Add(this) && IncludesAny(Volatile.Read(ref _rules), target, visited);

    private static bool IncludesAny(Rule<T>[]? rules, object target, HashSet<object> visited)
    {
        if (rules is null)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            if (rule is IncludeRule<T> { Validator: { } included }
                && (ReferenceEquals(included, target)
                    || (included is IIncludeGraph graph && graph.Includes(target, visited))))
            {
                return true;
            }

            if (IncludesAny(rule.Dependents, target, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static T Identity(T value) => value;

    private Validator<TChild> Inline<TChild>(Action<Validator<TChild>> childRules)
    {
        var inline = new Validator<TChild>
        {
            IsInline = true,
            _ruleCascade = _ruleCascade,
            _classCascade = _classCascade,
        };
        childRules(inline);
        return inline;
    }

    private static string PathOf(string? selectorExpression) => RuleNames.Path(typeof(T), selectorExpression);

    private Validator<T> Add<TValue>(Rule<T> rule, Action<RuleOptions<T, TValue>>? configure)
    {
        lock (_gate)
        {
            SetDisplayName(rule);
            configure?.Invoke(new RuleOptions<T, TValue>(rule, this));
            OnRuleAdded(new RuleOptions<T, object?>(rule, this));
            Commit(rule);
        }

        return this;
    }

    private Validator<T> AddEach<TItem>(Rule<T> rule, Action<EachRuleOptions<T, TItem>>? configure)
    {
        lock (_gate)
        {
            SetDisplayName(rule);
            configure?.Invoke(new EachRuleOptions<T, TItem>(rule, this));
            OnRuleAdded(new RuleOptions<T, object?>(rule, this));
            Commit(rule);
        }

        return this;
    }

    // A rule on a property shows its path in messages. A rule whose selector returns the object
    // itself ("x => x") has an empty path (the whole object) and shows "value".
    private static void SetDisplayName(Rule<T> rule)
    {
        if (rule.Path.Length > 0)
        {
            rule.DisplayName = RuleNames.DisplayName(typeof(T), rule.Path);
        }
        else if (rule.Expression is not null)
        {
            rule.DisplayName = ThrowExtensions.DefaultName;
        }
    }

    // Applies the open scopes (groups, block conditions, dependent rule capture) and adds the
    // rule. Runs under the lock. Readers see either the old or the new rule array.
    private void Commit(Rule<T> rule)
    {
        List<Rule<T>>? capture = null;
        string[]? scopeGroups = null;
        for (var scope = _scope; scope is not null; scope = scope.Outer)
        {
            if (scope.Condition is not null)
            {
                rule.AddCondition(scope.Condition, first: true);
            }

            scopeGroups ??= scope.Groups;
            capture ??= scope.Capture;
        }

        if (rule.Groups is null && scopeGroups is not null)
        {
            rule.Groups = scopeGroups;
        }

        if (rule.Groups is { } groups)
        {
            rule.InheritGroups(groups);
        }

        if (capture is not null)
        {
            capture.Add(rule);
            return;
        }

        var current = _rules;
        var next = new Rule<T>[current.Length + 1];
        Array.Copy(current, next, current.Length);
        next[current.Length] = rule;
        Volatile.Write(ref _rules, next);
    }

    private Validator<T> Block(Condition<T> condition, Action<Validator<T>> rules, Action<Validator<T>>? otherwise)
    {
        Argument.NotNull(rules);
        lock (_gate)
        {
            RunScope(new BuildScope(_scope, null, condition, null), rules);
            if (otherwise is not null)
            {
                RunScope(new BuildScope(_scope, null, condition.Negated(), null), otherwise);
            }
        }

        return this;
    }

    private void RunScope(BuildScope scope, Action<Validator<T>> rules)
    {
        var outer = _scope;
        _scope = scope;
        try
        {
            rules(this);
        }
        finally
        {
            _scope = outer;
        }
    }

    // An open Group, When or DependentRules callback while rules are added.
    private sealed class BuildScope(BuildScope? outer, string[]? groups, Condition<T>? condition, List<Rule<T>>? capture)
    {
        public BuildScope? Outer { get; } = outer;

        public string[]? Groups { get; } = groups;

        public Condition<T>? Condition { get; } = condition;

        public List<Rule<T>>? Capture { get; } = capture;
    }
}

// Lets Include find cycles (A includes B includes A) when they are built.
internal interface IIncludeGraph
{
    bool Includes(object target, HashSet<object> visited);
}
