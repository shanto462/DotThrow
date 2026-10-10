using System;
using System.Collections.Generic;

namespace DotThrow.Internal;

internal static class Descriptors
{
    public static IReadOnlyList<RuleDescriptor> Of<T>(Rule<T>[]? rules)
    {
        if (rules is null || rules.Length == 0)
        {
            return Array.Empty<RuleDescriptor>();
        }

        var result = new RuleDescriptor[rules.Length];
        for (var i = 0; i < rules.Length; i++)
        {
            result[i] = Of(rules[i]);
        }

        return result;
    }

    public static RuleDescriptor Of<T>(Rule<T> rule)
    {
        var asyncFilter = rule is IEachRuleInfo { HasAsyncFilter: true };
        return new RuleDescriptor
        {
            Kind = rule.Kind,
            Path = rule.Path,
            DisplayName = rule.DisplayNameProvider is null ? rule.DisplayName : null,
            Expression = rule.Expression,
            Groups = rule.Groups is { } groups ? Array.AsReadOnly(groups) : Array.Empty<string>(),
            ValueType = rule.ValueType,
            ChildValidatorType = rule.ChildValidatorType,
            Checks = rule.Checks,
            IsAsync = rule.IsAsync,
            HasCondition = rule.Conditions is not null,
            HasAsyncCondition = rule.HasAsyncCondition || asyncFilter,
            Cascade = rule.Cascade,
            ErrorCode = rule.ErrorCode,
            Severity = rule.Severity,
            DependentRules = Of(rule.Dependents),
        };
    }
}

internal interface IEachRuleInfo
{
    bool HasAsyncFilter { get; }
}

// Validators that can say whether a run needs ValidateAsync, child validators included.
// visited holds the validators already looked at, so cycles end.
internal interface IAsyncRuleInfo
{
    bool HasAsyncRules(HashSet<object> visited);
}

internal static class AsyncRules
{
    public static bool Of<T>(Rule<T>[]? rules, HashSet<object> visited)
    {
        if (rules is null)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            if (rule.NeedsAsync(visited))
            {
                return true;
            }
        }

        return false;
    }

    // A child validator: a known validator is looked at; one picked per object is not known
    // before the run, so it counts as async. A hand-written IValidator<T> runs through its own
    // Validate or ValidateAsync, so it never makes a sync run fail.
    public static bool OfChild(object? validator, bool picked, HashSet<object> visited) =>
        picked || (validator is IAsyncRuleInfo info && info.HasAsyncRules(visited));
}
