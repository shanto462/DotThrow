using System;
using System.Collections.Generic;

namespace DotThrow;

/// <summary>The kind of a <see cref="Validator{T}"/> rule, as reported by <see cref="Validator{T}.Describe"/>.</summary>
public enum RuleKind
{
    /// <summary>A chain of checks for one value: <c>For(x =&gt; x.Name, name =&gt; ...)</c>.</summary>
    Property = 0,

    /// <summary>A chain of checks for every item of a collection: <c>ForEach(x =&gt; x.Tags, tag =&gt; ...)</c>.</summary>
    Collection = 1,

    /// <summary>A validator for a nested object: <c>For(x =&gt; x.Address, addressValidator)</c>.</summary>
    Child = 2,

    /// <summary>A validator for every item of a collection: <c>ForEach(x =&gt; x.Lines, lineValidator)</c>.</summary>
    ChildCollection = 3,

    /// <summary>A predicate: <c>Must(...)</c>.</summary>
    Must = 4,

    /// <summary>An async predicate: <c>MustAsync(...)</c>.</summary>
    MustAsync = 5,

    /// <summary>A free-form rule: <c>Custom(...)</c>.</summary>
    Custom = 6,

    /// <summary>A free-form async rule: <c>CustomAsync(...)</c>.</summary>
    CustomAsync = 7,

    /// <summary>The rules of another validator: <c>Include(...)</c>.</summary>
    Include = 8,
}

/// <summary>
/// What a <see cref="Validator{T}"/> checks, for tools, documentation and tests. Returned by
/// <see cref="Validator{T}.Describe"/>. It is a snapshot: rules added later are not in it.
/// </summary>
public sealed class ValidatorDescriptor
{
    internal ValidatorDescriptor(Type validatorType, Type modelType, IReadOnlyList<RuleDescriptor> rules, bool hasAsyncRules)
    {
        ValidatorType = validatorType;
        ModelType = modelType;
        Rules = rules;
        HasAsyncRules = hasAsyncRules;
    }

    /// <summary>Gets the type of the validator.</summary>
    public Type ValidatorType { get; }

    /// <summary>Gets the type the validator validates.</summary>
    public Type ModelType { get; }

    /// <summary>Gets the rules, in the order they run. Dependent rules are inside their parent rule.</summary>
    public IReadOnlyList<RuleDescriptor> Rules { get; }

    /// <summary>
    /// Gets a value indicating whether a rule of this validator is async or has an async
    /// condition or filter, so the validator needs <c>ValidateAsync</c>. Dependent rules and the
    /// rules of child, collection item, included and polymorphic validators count too (each
    /// validator once, so cycles are fine). A validator picked per object (a provider or a
    /// factory) counts as async, because it is known only during the run.
    /// </summary>
    public bool HasAsyncRules { get; }

    /// <summary>Gets the rules with the given property path, for example <c>"Address.City"</c>.</summary>
    /// <param name="path">The property path, relative to the validated object.</param>
    /// <returns>The matching rules, dependent rules included.</returns>
    public IReadOnlyList<RuleDescriptor> GetRules(string path)
    {
        var result = new List<RuleDescriptor>();
        Collect(Rules, path, result);
        return result;
    }

    private static void Collect(IReadOnlyList<RuleDescriptor> rules, string path, List<RuleDescriptor> result)
    {
        foreach (var rule in rules)
        {
            if (string.Equals(rule.Path, path, StringComparison.Ordinal))
            {
                result.Add(rule);
            }

            Collect(rule.DependentRules, path, result);
        }
    }
}

/// <summary>What one rule checks. See <see cref="ValidatorDescriptor"/>.</summary>
public sealed class RuleDescriptor
{
    internal RuleDescriptor()
    {
    }

    /// <summary>Gets the kind of rule.</summary>
    public RuleKind Kind { get; internal set; }

    /// <summary>Gets the property path used in failure names, or an empty string for the whole object.</summary>
    public string Path { get; internal set; } = string.Empty;

    /// <summary>Gets the name used in messages, or <see langword="null"/> when it is computed per object.</summary>
    public string? DisplayName { get; internal set; }

    /// <summary>Gets the selector source text, for example <c>"x =&gt; x.Address.City"</c>, when known.</summary>
    public string? Expression { get; internal set; }

    /// <summary>Gets the groups of the rule. An empty list means <see cref="ValidationGroups.Default"/>.</summary>
    public IReadOnlyList<string> Groups { get; internal set; } = Array.Empty<string>();

    /// <summary>Gets the type of the checked value (the item type for collections), when known.</summary>
    public Type? ValueType { get; internal set; }

    /// <summary>Gets the type of the child or included validator, for child and include rules.</summary>
    public Type? ChildValidatorType { get; internal set; }

    /// <summary>Gets the error codes the rule is known to report, for example <c>"Must"</c>. Chains of checks are not listed.</summary>
    public IReadOnlyList<string> Checks { get; internal set; } = Array.Empty<string>();

    /// <summary>Gets a value indicating whether the rule only runs in async runs.</summary>
    public bool IsAsync { get; internal set; }

    /// <summary>Gets a value indicating whether the rule has a When or Unless condition (its own or from a block).</summary>
    public bool HasCondition { get; internal set; }

    /// <summary>Gets a value indicating whether the rule has an async condition or an async item filter.</summary>
    public bool HasAsyncCondition { get; internal set; }

    /// <summary>Gets the cascade set on the rule, or <see langword="null"/> when it uses the validator cascade.</summary>
    public CascadeMode? Cascade { get; internal set; }

    /// <summary>Gets the error code set with <c>WithErrorCode</c>, or <see langword="null"/>.</summary>
    public string? ErrorCode { get; internal set; }

    /// <summary>Gets the severity set with <c>WithSeverity</c>, or <see langword="null"/>.</summary>
    public Severity? Severity { get; internal set; }

    /// <summary>Gets the rules that run only when this rule passes.</summary>
    public IReadOnlyList<RuleDescriptor> DependentRules { get; internal set; } = Array.Empty<RuleDescriptor>();

    /// <inheritdoc/>
    public override string ToString() => Path.Length == 0 ? Kind.ToString() : $"{Kind} {Path}";
}
