using System;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// Options for one <see cref="Validator{T}"/> rule, set in the <c>configure</c> callback of
/// <c>For</c>, <c>ForEach</c>, <c>Must</c>, <c>Custom</c> and <c>Include</c>. Every method
/// returns the options, so calls chain.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
/// <typeparam name="TValue">The checked value type: the property type, the item type for collections, or <typeparamref name="T"/> for rules about the whole object.</typeparam>
/// <typeparam name="TOptions">The concrete options type, so chained calls keep it.</typeparam>
/// <remarks>
/// <para>
/// Message, error code, severity and state apply to every failure of the rule. Child and
/// included validators keep the options of their own rules, so on <c>For(x =&gt; x.Address,
/// validator)</c> only the name, conditions, groups, child groups and dependent rules apply.
/// Conditions apply to the whole rule (FluentValidation's <c>ApplyConditionTo.AllValidators</c>);
/// use a separate rule, or the <c>message</c> of a check, for one check.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// For(x =&gt; x.Email, email =&gt; email.IfNullOrWhiteSpace().IfNotEmailAddress(), rule =&gt; rule
///     .WithName("E-mail")
///     .WithSeverity(Severity.Warning)
///     .When(x =&gt; x.WantsNewsletter));
/// </code>
/// </example>
#pragma warning disable CA1005 // Avoid excessive parameters on generic types: the third parameter keeps fluent chains typed.
public abstract class RuleOptionsBase<T, TValue, TOptions>
#pragma warning restore CA1005
    where TOptions : RuleOptionsBase<T, TValue, TOptions>
{
    private protected RuleOptionsBase(Rule<T> rule, Validator<T> validator)
    {
        Rule = rule;
        Validator = validator;
    }

    private protected Rule<T> Rule { get; }

    private protected Validator<T> Validator { get; }

    private TOptions Self => (TOptions)this;

    /// <summary>Gets the property path of the rule, for example <c>"Address.City"</c>, or an empty string for the whole object.</summary>
    public string PropertyPath => Rule.Path;

    /// <summary>Gets the kind of the rule.</summary>
    public RuleKind Kind => Rule.Kind;

    /// <summary>
    /// Sets the name used in messages (<c>{PropertyName}</c>). The failure name (the property
    /// path) does not change, except for rules about the whole object, which have no path of
    /// their own. Use <see cref="OverridePropertyName"/> to change the failure name.
    /// </summary>
    /// <param name="name">The name, for example <c>"E-mail address"</c>.</param>
    /// <returns>These options.</returns>
    public TOptions WithName(string name)
    {
        Argument.NotNull(name);
        Rule.DisplayName = name;
        Rule.DisplayNameProvider = null;
        Rule.DisplayNameSet = true;
        if (Rule.IsObjectLevel && !Rule.PathOverridden)
        {
            Rule.Path = name;
        }

        return Self;
    }

    /// <summary>Sets the name used in messages from the validated object. It runs every time the rule runs.</summary>
    /// <param name="name">Returns the name.</param>
    /// <returns>These options.</returns>
    public TOptions WithName(Func<T, string> name)
    {
        Argument.NotNull(name);
        Rule.DisplayNameProvider = name;
        Rule.DisplayNameSet = true;
        return Self;
    }

    /// <summary>
    /// Sets the failure name (the property path, for example the key in
    /// <see cref="ValidationReport.ToDictionary"/>). The message name changes too, unless
    /// <see cref="WithName(string)"/> sets it. For child validators it is the prefix of the
    /// child failure names.
    /// </summary>
    /// <param name="path">The new path. An empty string means the whole object.</param>
    /// <returns>These options.</returns>
    public TOptions OverridePropertyName(string path)
    {
        Argument.NotNull(path);
        Rule.Path = path;
        Rule.PathOverridden = true;
        if (!Rule.DisplayNameSet)
        {
            Rule.DisplayName = path;
        }

        return Self;
    }

    /// <summary>
    /// Replaces the message of every failure of the rule. The text may use <c>{PropertyName}</c>,
    /// <c>{PropertyPath}</c>, <c>{PropertyValue}</c> and, for collections, <c>{CollectionIndex}</c>,
    /// with an optional format such as <c>{PropertyValue:N2}</c>.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>These options.</returns>
    public TOptions WithMessage(string message)
    {
        Argument.NotNull(message);
        Rule.Message = message;
        Rule.MessageProvider = null;
        return Self;
    }

    /// <summary>Replaces the message of every failure of the rule with text built from the validated object.</summary>
    /// <param name="message">Returns the message.</param>
    /// <returns>These options.</returns>
    public TOptions WithMessage(Func<T, string> message)
    {
        Argument.NotNull(message);
        Rule.MessageProvider = (context, _) => message(context.Instance);
        return Self;
    }

    /// <summary>Replaces the message of every failure of the rule with text built from the validated object and the failed value.</summary>
    /// <param name="message">Returns the message.</param>
    /// <returns>These options.</returns>
    public TOptions WithMessage(Func<T, TValue, string> message)
    {
        Argument.NotNull(message);
        Rule.MessageProvider = (context, value) => message(context.Instance, As(value));
        return Self;
    }

    /// <summary>Replaces the error code of every failure of the rule.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>These options.</returns>
    public TOptions WithErrorCode(string errorCode)
    {
        Argument.NotNull(errorCode);
        Rule.ErrorCode = errorCode;
        return Self;
    }

    /// <summary>Sets the severity of every failure of the rule.</summary>
    /// <param name="severity">The severity.</param>
    /// <returns>These options.</returns>
    public TOptions WithSeverity(Severity severity)
    {
        Rule.Severity = severity;
        Rule.SeverityProvider = null;
        return Self;
    }

    /// <summary>Sets the severity of every failure of the rule from the validated object.</summary>
    /// <param name="severity">Returns the severity.</param>
    /// <returns>These options.</returns>
    public TOptions WithSeverity(Func<T, Severity> severity)
    {
        Argument.NotNull(severity);
        Rule.SeverityProvider = (context, _) => severity(context.Instance);
        return Self;
    }

    /// <summary>Sets the severity of every failure of the rule from the validated object and the failed value.</summary>
    /// <param name="severity">Returns the severity.</param>
    /// <returns>These options.</returns>
    public TOptions WithSeverity(Func<T, TValue, Severity> severity)
    {
        Argument.NotNull(severity);
        Rule.SeverityProvider = (context, value) => severity(context.Instance, As(value));
        return Self;
    }

    /// <summary>Sets the severity of every failure of the rule from the object, the failed value and the context.</summary>
    /// <param name="severity">Returns the severity.</param>
    /// <returns>These options.</returns>
    public TOptions WithSeverity(Func<T, TValue, ValidationContext<T>, Severity> severity)
    {
        Argument.NotNull(severity);
        Rule.SeverityProvider = (context, value) => severity(context.Instance, As(value), context);
        return Self;
    }

    /// <summary>Sets <see cref="ValidationFailure.CustomState"/> of every failure of the rule.</summary>
    /// <param name="state">Returns the state from the validated object.</param>
    /// <returns>These options.</returns>
    public TOptions WithState(Func<T, object?> state)
    {
        Argument.NotNull(state);
        Rule.StateProvider = (context, _) => state(context.Instance);
        return Self;
    }

    /// <summary>Sets <see cref="ValidationFailure.CustomState"/> of every failure of the rule.</summary>
    /// <param name="state">Returns the state from the validated object and the failed value.</param>
    /// <returns>These options.</returns>
    public TOptions WithState(Func<T, TValue, object?> state)
    {
        Argument.NotNull(state);
        Rule.StateProvider = (context, value) => state(context.Instance, As(value));
        return Self;
    }

    /// <summary>Sets <see cref="ValidationFailure.CustomState"/> of every failure of the rule.</summary>
    /// <param name="state">Returns the state from the object, the failed value and the context.</param>
    /// <returns>These options.</returns>
    public TOptions WithState(Func<T, TValue, ValidationContext<T>, object?> state)
    {
        Argument.NotNull(state);
        Rule.StateProvider = (context, value) => state(context.Instance, As(value), context);
        return Self;
    }

    /// <summary>Runs the rule only when <paramref name="condition"/> returns <see langword="true"/>. Several conditions must all pass.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>These options.</returns>
    public TOptions When(Func<T, bool> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>((instance, _) => condition(instance), negate: false, shared: false), first: false);
        return Self;
    }

    /// <summary>Runs the rule only when <paramref name="condition"/> returns <see langword="true"/>.</summary>
    /// <param name="condition">The condition, with the validation context.</param>
    /// <returns>These options.</returns>
    public TOptions When(Func<T, ValidationContext<T>, bool> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>(condition, negate: false, shared: false), first: false);
        return Self;
    }

    /// <summary>Runs the rule only when <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>These options.</returns>
    public TOptions Unless(Func<T, bool> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>((instance, _) => condition(instance), negate: true, shared: false), first: false);
        return Self;
    }

    /// <summary>Runs the rule only when <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The condition, with the validation context.</param>
    /// <returns>These options.</returns>
    public TOptions Unless(Func<T, ValidationContext<T>, bool> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>(condition, negate: true, shared: false), first: false);
        return Self;
    }

    /// <summary>Runs the rule only when the async <paramref name="condition"/> returns <see langword="true"/>. The validator then needs <c>ValidateAsync</c>.</summary>
    /// <param name="condition">The async condition.</param>
    /// <returns>These options.</returns>
    public TOptions WhenAsync(Func<T, CancellationToken, Task<bool>> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>((instance, _, token) => condition(instance, token), negate: false, shared: false), first: false);
        return Self;
    }

    /// <summary>Runs the rule only when the async <paramref name="condition"/> returns <see langword="true"/>.</summary>
    /// <param name="condition">The async condition, with the validation context.</param>
    /// <returns>These options.</returns>
    public TOptions WhenAsync(Func<T, ValidationContext<T>, CancellationToken, Task<bool>> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>(condition, negate: false, shared: false), first: false);
        return Self;
    }

    /// <summary>Runs the rule only when the async <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The async condition.</param>
    /// <returns>These options.</returns>
    public TOptions UnlessAsync(Func<T, CancellationToken, Task<bool>> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>((instance, _, token) => condition(instance, token), negate: true, shared: false), first: false);
        return Self;
    }

    /// <summary>Runs the rule only when the async <paramref name="condition"/> returns <see langword="false"/>.</summary>
    /// <param name="condition">The async condition, with the validation context.</param>
    /// <returns>These options.</returns>
    public TOptions UnlessAsync(Func<T, ValidationContext<T>, CancellationToken, Task<bool>> condition)
    {
        Argument.NotNull(condition);
        Rule.AddCondition(new Condition<T>(condition, negate: true, shared: false), first: false);
        return Self;
    }

    /// <summary>
    /// Puts the rule in one or more groups (FluentValidation rule sets). A rule in groups runs
    /// only when one of them is asked for. Add <see cref="ValidationGroups.Default"/> to run it
    /// in default runs too. Dependent rules and inline child rules without groups get the same groups.
    /// </summary>
    /// <param name="groups">The group names. Names ignore case.</param>
    /// <returns>These options.</returns>
    public TOptions InGroup(params string[] groups)
    {
        Rule.Groups = Validator<T>.CheckGroups(groups);
        return Self;
    }

    /// <summary>
    /// Sets what the rule does after a failed check: <see cref="CascadeMode.Stop"/> ends the
    /// chain (one failure), <see cref="CascadeMode.Continue"/> runs the remaining checks. A
    /// <see langword="null"/> value always stops the chain. For collections the cascade
    /// applies to each item.
    /// </summary>
    /// <param name="mode">The cascade mode.</param>
    /// <returns>These options.</returns>
    public TOptions Cascade(CascadeMode mode)
    {
        Rule.Cascade = mode;
        return Self;
    }

    /// <summary>
    /// Adds rules that run only when this rule ran and reported no failure. Add them to the
    /// validator passed to <paramref name="rules"/>; it is the same validator.
    /// </summary>
    /// <param name="rules">Adds the dependent rules.</param>
    /// <returns>These options.</returns>
    /// <example><c>rule =&gt; rule.DependentRules(v =&gt; v.For(x =&gt; x.Surname, s =&gt; s.IfNullOrEmpty()))</c></example>
    public TOptions DependentRules(Action<Validator<T>> rules)
    {
        Argument.NotNull(rules);
        var captured = Validator.CaptureRules(rules);
        if (captured.Length > 0)
        {
            var current = Rule.Dependents ?? [];
            var all = new Rule<T>[current.Length + captured.Length];
            Array.Copy(current, all, current.Length);
            Array.Copy(captured, 0, all, current.Length, captured.Length);
            Rule.Dependents = all;
        }

        return Self;
    }

    /// <summary>
    /// Sets the groups the child or included validator runs with, instead of the groups of the
    /// run (FluentValidation's <c>SetValidator(validator, ruleSets)</c>). Inside the child only
    /// these groups select rules: a property filter or custom selector of the run still decides
    /// whether this rule runs, but not which child rules run. Use it to run the
    /// default rules of a child that is added in a named group:
    /// <c>rule =&gt; rule.InGroup("shipping").WithChildGroups(ValidationGroups.Default)</c>.
    /// </summary>
    /// <param name="groups">The group names.</param>
    /// <returns>These options.</returns>
    public TOptions WithChildGroups(params string[] groups)
    {
        Rule.ChildGroups = Validator<T>.CheckGroups(groups);
        return Self;
    }

    private static TValue As(object? value) => value is TValue typed ? typed : default!;
}

/// <summary>Options for a <see cref="Validator{T}"/> rule. See <see cref="RuleOptionsBase{T, TValue, TOptions}"/>.</summary>
/// <typeparam name="T">The type being validated.</typeparam>
/// <typeparam name="TValue">The checked value type.</typeparam>
public sealed class RuleOptions<T, TValue> : RuleOptionsBase<T, TValue, RuleOptions<T, TValue>>
{
    internal RuleOptions(Rule<T> rule, Validator<T> validator)
        : base(rule, validator)
    {
    }
}

/// <summary>
/// Options for a <c>ForEach</c> rule: everything in <see cref="RuleOptionsBase{T, TValue, TOptions}"/>
/// plus item filters and custom indexers.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
/// <typeparam name="TItem">The item type.</typeparam>
public sealed class EachRuleOptions<T, TItem> : RuleOptionsBase<T, TItem, EachRuleOptions<T, TItem>>
{
    private readonly IEachRule<TItem> _each;

    internal EachRuleOptions(Rule<T> rule, Validator<T> validator)
        : base(rule, validator)
    {
        _each = (IEachRule<TItem>)rule;
    }

    /// <summary>
    /// Checks only the items for which <paramref name="predicate"/> returns <see langword="true"/>.
    /// Failure names keep the original index, for example <c>"Lines[3]"</c>.
    /// </summary>
    /// <param name="predicate">The item filter.</param>
    /// <returns>These options.</returns>
    public EachRuleOptions<T, TItem> Where(Func<TItem, bool> predicate)
    {
        Argument.NotNull(predicate);
        _each.Filter = predicate;
        return this;
    }

    /// <summary>Checks only the items for which the async <paramref name="predicate"/> returns <see langword="true"/>. The validator then needs <c>ValidateAsync</c>.</summary>
    /// <param name="predicate">The async item filter.</param>
    /// <returns>These options.</returns>
    public EachRuleOptions<T, TItem> WhereAsync(Func<TItem, CancellationToken, Task<bool>> predicate)
    {
        Argument.NotNull(predicate);
        _each.AsyncFilter = predicate;
        return this;
    }

    /// <summary>
    /// Replaces the default <c>[index]</c> after the collection name in failure names. The text
    /// is added as it is, so include the brackets you want, for example
    /// <c>(line, index) =&gt; $"[{line.Sku}]"</c> gives <c>"Lines[SKU-1].Quantity"</c>.
    /// </summary>
    /// <param name="indexer">Returns the text for an item and its index.</param>
    /// <returns>These options.</returns>
    public EachRuleOptions<T, TItem> WithIndexer(Func<TItem, int, string> indexer)
    {
        Argument.NotNull(indexer);
        _each.Indexer = indexer;
        return this;
    }
}
