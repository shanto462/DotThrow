using System;
using System.Collections.Generic;
using System.Globalization;

namespace DotThrow.Dynamic.Internal;

// Builds a Validator<T> from a definition. Every problem is collected; the build fails at the
// end with all of them.
internal static class RuleCompiler
{
    public static Validator<T> Build<T>(RuleSetDefinition? definition, PropertySource<T> source, CheckRegistry registry, out IReadOnlyList<string> errors)
    {
        var context = new BuildContext(registry, source.Types);
        var validator = new Validator<T>();
        if (definition is null)
        {
            context.Error(string.Empty, "the definition is null");
            errors = context.Errors;
            return validator;
        }

        if (Cascade(definition.RuleCascade, context, nameof(RuleSetDefinition.RuleCascade)) is { } ruleCascade)
        {
            validator.RuleCascade = ruleCascade;
        }

        if (Cascade(definition.ClassCascade, context, nameof(RuleSetDefinition.ClassCascade)) is { } classCascade)
        {
            validator.ClassCascade = classCascade;
        }

        if (definition.Rules is null)
        {
            context.Error(nameof(RuleSetDefinition.Rules), "the rules are null");
        }
        else
        {
            for (var i = 0; i < definition.Rules.Count; i++)
            {
                AddRule(validator, definition.Rules[i], source, context, "Rules[" + i.ToString(CultureInfo.InvariantCulture) + "]", inheritedGroups: null);
            }
        }

        errors = context.Errors;
        return validator;
    }

    public static void AddRule<T>(Validator<T> validator, RuleDefinition? rule, PropertySource<T> source, BuildContext context, string location, string[]? inheritedGroups)
    {
        if (rule is null)
        {
            context.Error(location, "the rule is null");
            return;
        }

        // "Tags[]" is the same rule as "Tags" with Each = true (groups and conditions included).
        var path = (rule.Property ?? string.Empty).Trim();
        if (path.Length > 2 && path.IndexOf("[]", StringComparison.Ordinal) == path.Length - 2)
        {
            if (rule.Each)
            {
                context.Error(location, "use either 'Each' or '[]' at the end of the path, not both");
                return;
            }

            AddRule(validator, Copy(rule, path.Substring(0, path.Length - 2), each: true, withScope: true), source, context, location, inheritedGroups);
            return;
        }

        var errorsBefore = context.Errors.Count;
        var groups = Groups(rule, context, location) ?? inheritedGroups;
        var when = rule.When is null ? null : Condition(rule.When, source, context, location + ".When");
        var unless = rule.Unless is null ? null : Condition(rule.Unless, source, context, location + ".Unless");
        if (rule.Checks is null || rule.Checks.Count == 0)
        {
            context.Error(location, "the rule has no checks");
            return;
        }

        // "[]" rules hand the failure options to the item rule, which checks them.
        var each = path.IndexOf("[]", StringComparison.Ordinal);
        if (each >= 0)
        {
            AddItemRule(validator, rule, path, each, source, context, location, new RuleScope<T>(groups, when, unless));
            return;
        }

        var severity = Severity(rule.Severity, context, location);
        var cascade = Cascade(rule.Cascade, context, location + ".Cascade");
        if (rule.ErrorCode is { } code && string.IsNullOrWhiteSpace(code))
        {
            context.Error(location + ".ErrorCode", "the error code is empty");
        }

        if (rule.Name is { } name && string.IsNullOrWhiteSpace(name))
        {
            context.Error(location + ".Name", "the name is empty");
        }

        var entry = path.Length == 0 ? source.Root : source.Find(path);
        if (entry is null)
        {
            UnknownProperty(path, source, context, location + ".Property");
            return;
        }

        var resolved = Resolve<T>(entry.Path);
        var built = rule.Each
            ? entry.CreateEachRule(validator, resolved, rule, context, location)
            : entry.CreateRule(validator, resolved, rule, context, location);
        if (built is null || context.Errors.Count > errorsBefore)
        {
            return;
        }

        validator.AddRule<object?>(built, options =>
        {
            if (groups is not null)
            {
                options.InGroup(groups);
            }

            if (when is not null)
            {
                options.When(when);
            }

            if (unless is not null)
            {
                options.Unless(unless);
            }

            if (severity is { } level)
            {
                options.WithSeverity(level);
            }

            if (cascade is { } mode)
            {
                options.Cascade(mode);
            }

            if (rule.ErrorCode is { } errorCode)
            {
                options.WithErrorCode(errorCode);
            }

            if (rule.Name is { } displayName)
            {
                options.WithName(displayName);
            }
        });
    }

    // "Lines[].Sku" (or "Lines[]", the same as Each = true on "Lines").
    private static void AddItemRule<T>(Validator<T> validator, RuleDefinition rule, string path, int each, PropertySource<T> source, BuildContext context, string location, RuleScope<T> scope)
    {
        var collectionPath = path.Substring(0, each);
        var rest = path.Substring(each + 2);
        if (collectionPath.Length == 0 || (rest.Length > 0 && rest[0] != '.'))
        {
            context.Error(location + ".Property", $"'{path}' is not a valid path; use 'Lines[].Sku' to check a property of every item");
            return;
        }

        var entry = source.Find(collectionPath);
        if (entry is null)
        {
            UnknownProperty(collectionPath, source, context, location + ".Property");
            return;
        }

        // The item rule keeps the checks and options that make failures; groups and conditions
        // stay on the outer ForEach (conditions read the validated object).
        entry.AddItemRule(validator, Copy(rule, rest.Substring(1), rule.Each, withScope: false), scope, context, location);
    }

    // withScope: also the groups and conditions (they stay on the outer rule of an item path).
    private static RuleDefinition Copy(RuleDefinition rule, string property, bool each, bool withScope) => new()
    {
        Property = property,
        Each = each,
        Group = withScope ? rule.Group : null,
        Groups = withScope ? rule.Groups : null,
        When = withScope ? rule.When : null,
        Unless = withScope ? rule.Unless : null,
        AllowNull = rule.AllowNull,
        Severity = rule.Severity,
        ErrorCode = rule.ErrorCode,
        Message = rule.Message,
        Name = rule.Name,
        Cascade = rule.Cascade,
        Checks = rule.Checks,
    };

    private static Func<T, bool>? Condition<T>(ConditionDefinition condition, PropertySource<T> source, BuildContext context, string location)
    {
        var path = (condition.Property ?? string.Empty).Trim();
        var entry = path.Length == 0 ? source.Root : source.Find(path);
        if (entry is null)
        {
            UnknownProperty(path, source, context, location + ".Property");
            return null;
        }

        return entry.CreateCondition(condition, context, location);
    }

    private static string[]? Groups(RuleDefinition rule, BuildContext context, string location)
    {
        var groups = new List<string>();
        if (rule.Group is { } group)
        {
            groups.Add(group);
        }

        if (rule.Groups is { } list)
        {
            groups.AddRange(list);
        }

        if (groups.Count == 0)
        {
            return null;
        }

        foreach (var name in groups)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                context.Error(location + ".Groups", "a group name is empty");
                return null;
            }
        }

        return [.. groups];
    }

    private static Severity? Severity(string? text, BuildContext context, string location)
    {
        if (text is null)
        {
            return null;
        }

        if (EnumText.TryParse<Severity>(text, out var severity))
        {
            return severity;
        }

        context.Error(location + ".Severity", $"'{text}' is not a severity (use Error, Warning or Info)");
        return null;
    }

    private static CascadeMode? Cascade(string? text, BuildContext context, string location)
    {
        if (text is null)
        {
            return null;
        }

        if (EnumText.TryParse<CascadeMode>(text, out var mode))
        {
            return mode;
        }

        context.Error(location, $"'{text}' is not a cascade mode (use Stop or Continue)");
        return null;
    }

    private static void UnknownProperty<T>(string path, PropertySource<T> source, BuildContext context, string location)
    {
        if (source.Problem(path) is { } problem)
        {
            context.Error(location, problem);
            return;
        }

        var hint = Suggestions.Closest(path, source.Paths);
        context.Error(
            location,
            $"'{path}' is not a property of {TypeText.Of(typeof(T))}" + (hint is null ? string.Empty : $" (did you mean '{hint}'?)"));
    }

    // The failure name of a path, after the global name hook (as for code rules).
    private static string Resolve<T>(string path) =>
        path.Length > 0 && ValidatorDefaults.PropertyNameResolver is { } resolve ? resolve(typeof(T), path) ?? path : path;
}

internal static class CheckCompiler
{
    public static CompiledCheck<TValue>[]? Compile<TValue>(CheckFamily<TValue> family, RuleDefinition rule, string propertyName, BuildContext context, string location)
    {
        var checks = new List<CompiledCheck<TValue>>(rule.Checks.Count);
        var failed = false;
        for (var i = 0; i < rule.Checks.Count; i++)
        {
            var checkLocation = location + ".Checks[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            var definition = rule.Checks[i];
            if (definition is null)
            {
                context.Error(checkLocation, "the check is null");
                failed = true;
                continue;
            }

            if (Compile(family, definition, rule.Message, propertyName, context, checkLocation) is { } check)
            {
                checks.Add(check);
            }
            else
            {
                failed = true;
            }
        }

        return failed ? null : [.. checks];
    }

    private static CompiledCheck<TValue>? Compile<TValue>(CheckFamily<TValue> family, CheckDefinition definition, string? ruleMessage, string propertyName, BuildContext context, string location)
    {
        var name = definition.Check?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            context.Error(location, "the check has no name");
            return null;
        }

        if (definition.ErrorCode is { } code && string.IsNullOrWhiteSpace(code))
        {
            context.Error(location + ".ErrorCode", "the error code is empty");
            return null;
        }

        // A check's own message wins; the rule message is the default of every check.
        var effective = definition.Message is null && ruleMessage is not null ? WithMessage(definition, ruleMessage) : definition;
        var builtIn = CheckNames.Find(name!);
        var args = new CheckArgs(effective, context, location, builtIn ?? name!, propertyName.Length == 0 ? "value" : propertyName, typeof(TValue));
        GuardStep<TValue>? step;
        if (builtIn is not null)
        {
            if (!family.TryCreate(builtIn, args, out step))
            {
                args.Error($"does not apply to {TypeText.Of(typeof(TValue))}; it applies to {CheckNames.AppliesTo(builtIn)}");
                return null;
            }

            if (!args.Failed)
            {
                args.ReportUnused();
            }
        }
        else if (context.Registry.TryCreate(name!, args, out step, out var customName))
        {
            name = customName;
        }
        else
        {
            if (CheckNames.IsCodeOnly(name!))
            {
                args.Error("needs code (a lambda), so it cannot come from a definition; register a custom check in a CheckRegistry");
            }
            else
            {
                var hint = Suggestions.Closest(name!, Candidates(context.Registry));
                args.Error("is not a known check" + (hint is null ? string.Empty : $" (did you mean '{hint}'?)"));
            }

            return null;
        }

        if (step is null && !args.Failed)
        {
            // A custom check factory returned null.
            args.Error("the check factory returned no check");
        }

        return step is null || args.Failed ? null : new CompiledCheck<TValue>(builtIn ?? name!, step, definition.ErrorCode);
    }

    private static CheckDefinition WithMessage(CheckDefinition definition, string message) => new()
    {
        Check = definition.Check,
        Value = definition.Value,
        Min = definition.Min,
        Max = definition.Max,
        Values = definition.Values,
        Comparison = definition.Comparison,
        Options = definition.Options,
        Message = message,
        ErrorCode = definition.ErrorCode,
    };

    private static IEnumerable<string> Candidates(CheckRegistry registry)
    {
        foreach (var name in CheckNames.All)
        {
            yield return name;
        }

        foreach (var name in registry.Names)
        {
            yield return name;
        }
    }
}
