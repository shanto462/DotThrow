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
