using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotThrow.Internal;

// One rule of a Validator<T>. Its settings are written while the rule is configured (under
// the validator lock) and never change after the rule is published, so many threads can
// run it at once.
internal abstract class Rule<T> : FailureSink
{
    private RuleDescriptor? _descriptor;

    protected Rule(Validator<T> owner, RuleKind kind, string path, string? expression)
    {
        Owner = owner;
        Kind = kind;
        Path = path;
        DisplayName = path;
        Expression = expression;
    }

    public Validator<T> Owner { get; }

    public RuleKind Kind { get; }

    // The path relative to the validated object ("Address.City"), or "" for the object itself.
    public string Path { get; set; }

    public bool PathOverridden { get; set; }

    // The name used in messages ({PropertyName}).
    public string DisplayName { get; set; }

    public bool DisplayNameSet { get; set; }

    public Func<T, string>? DisplayNameProvider { get; set; }

    public string? Expression { get; }

    public string[]? Groups { get; set; }

    public string[]? ChildGroups { get; set; }

    public Condition<T>[]? Conditions { get; private set; }

    public bool HasAsyncCondition { get; private set; }

    public Rule<T>[]? Dependents { get; set; }

    public CascadeMode? Cascade { get; set; }

    public string? ErrorCode { get; set; }

    public string? Message { get; set; }

    public Func<ValidationContext<T>, object?, string>? MessageProvider { get; set; }

    public Severity? Severity { get; set; }

    public Func<ValidationContext<T>, object?, Severity>? SeverityProvider { get; set; }

    public Func<ValidationContext<T>, object?, object?>? StateProvider { get; set; }

    // What a custom IRuleSelector sees; built once, when first needed.
    public RuleDescriptor Descriptor => _descriptor ??= Descriptors.Of(this);

    // The rule only works in async runs (MustAsync, CustomAsync).
    public virtual bool IsAsync => false;

    // The rule is about the object itself, so WithName also sets its path (FluentValidation
    // does the same for model-level rules).
    public virtual bool IsObjectLevel => false;

    public virtual Type? ValueType => null;

    public virtual Type? ChildValidatorType => null;

    public virtual IReadOnlyList<string> Checks => Array.Empty<string>();

    public void AddCondition(Condition<T> condition, bool first)
    {
        var current = Conditions ?? [];
        var next = new Condition<T>[current.Length + 1];
        if (first)
        {
            next[0] = condition;
            Array.Copy(current, 0, next, 1, current.Length);
        }
        else
        {
            Array.Copy(current, next, current.Length);
            next[current.Length] = condition;
        }

        Conditions = next;
        HasAsyncCondition |= condition.IsAsync;
    }

    // Dependent rules and inline child rules without their own groups run in the groups
    // of their parent rule, the same as FluentValidation.
    public virtual void InheritGroups(string[] groups)
    {
        Groups ??= groups;
        if (Dependents is not null)
        {
            foreach (var dependent in Dependents)
            {
                dependent.InheritGroups(groups);
            }
        }
    }

    public void Execute(ValidationContext<T> context)
    {
        // Message arguments belong to one rule (FluentValidation resets them per validator).
        context.Run.Arguments?.Clear();
        if ((Groups is not null || context.Filters) && !RuleSelection.IsSelected(this, context))
        {
            return;
        }

        if (Conditions is not null && !ConditionsPass(context))
        {
            return;
        }

        if (Dependents is null)
        {
            Run(context);
            return;
        }

        var before = context.Report.Count;
        Run(context);
        if (context.Report.Count == before)
        {
            RuleList<T>.Run(Dependents, context, stopOnFailure: false);
        }
    }

    public Task ExecuteAsync(ValidationContext<T> context)
    {
        context.Run.Arguments?.Clear();
        if ((Groups is not null || context.Filters) && !RuleSelection.IsSelected(this, context))
        {
            return Task.CompletedTask;
        }

        if (Conditions is not null)
        {
            if (HasAsyncCondition)
            {
                return ExecuteAfterAsyncConditionsAsync(context);
            }

            if (!ConditionsPass(context))
            {
                return Task.CompletedTask;
            }
        }

        return RunWithDependentsAsync(context);
    }

    public override bool OnCheckFailure(ValidationReport report, in RuleFrame frame, string name, string message, string errorCode, object? value)
    {
        AddFailure(report, (ValidationContext<T>)frame.Context!, frame.Index, frame.Item, name, message, errorCode, value);
        return (Cascade ?? Owner.RuleCascade) == CascadeMode.Stop;
    }

    // True when running this rule may need ValidateAsync (see ValidatorDescriptor.HasAsyncRules).
    public virtual bool NeedsAsync(HashSet<object> visited) =>
        IsAsync || HasAsyncCondition || this is IEachRuleInfo { HasAsyncFilter: true } || AsyncRules.Of(Dependents, visited);

    public override string? RenderMessage<TValue, TArgs>(in RuleFrame frame, string name, TValue value, TArgs args)
    {
        if (MessageProvider is not null || Message is null)
        {
            return null;
        }

        var context = (ValidationContext<T>)frame.Context!;
        return RuleMessages.Render(
            Message,
            name,
            FailurePath(context, frame.Index, frame.Item, name),
            value,
            frame.Index,
            context.Run.Arguments is { Count: > 0 } appended ? appended : null,
            args);
    }

    // Creates the failure with the rule options applied.
    public void AddFailure(ValidationReport report, ValidationContext<T> context, int index, object? item, string name, string message, string errorCode, object? value)
    {
        var path = FailurePath(context, index, item, name);
        var arguments = context.Run.Arguments is { Count: > 0 } appended
            ? new Dictionary<string, object?>(appended, StringComparer.Ordinal)
            : null;
        string text;
        if (MessageProvider is not null)
        {
            text = MessageProvider(context, value);
        }
        else if (Message is not null)
        {
            // A failed check already rendered the rule message with its own arguments.
            text = context.Run.RuleMessage ?? RuleMessages.Format(Message, name, path, value, index, arguments);
        }
        else
        {
            text = RuleMessages.Format(message, arguments);
        }

        var code = CheckErrorCode(item) ?? ErrorCode ?? (ValidatorDefaults.ErrorCodeResolver is { } resolve ? resolve(errorCode) ?? errorCode : errorCode);
        var severity = SeverityProvider is not null ? SeverityProvider(context, value) : Severity ?? ValidatorDefaults.Severity;
        var state = StateProvider?.Invoke(context, value);
        report.AddRuleFailure(ValidationFailure.Create(path, text, code, severity, value, state, name, index, arguments));
    }

    public string NameFor(T instance) => DisplayNameProvider is null ? DisplayName : DisplayNameProvider(instance);

    // An error code set on one check of the rule (dynamic rules pass the check as the frame item).
    protected virtual string? CheckErrorCode(object? item) => null;

    protected abstract void Run(ValidationContext<T> context);

    protected virtual Task RunAsync(ValidationContext<T> context)
    {
        Run(context);
        return Task.CompletedTask;
    }

    // The failure name: the context path plus the rule path. ForEach rules add the index,
    // custom rules use the name given to Check or AddFailure.
    protected virtual string FailurePath(ValidationContext<T> context, int index, object? item, string name) =>
        PropertyPaths.Join(context.PropertyPath, Path);

    protected AsyncRuleInvokedSynchronouslyException AsyncInSyncRun() => new(Owner.GetType());

    // RuleList turns a NullReferenceException from a selector rule into this clearer error.
    public Exception NullReference(NullReferenceException exception) =>
        Expression is null
            ? exception
            : new InvalidOperationException(
                $"A NullReferenceException occurred while running the rule for '{Expression}' (its selector, checks, filters, or a validator it calls; see the inner exception). If a part of the path can be null, add a When condition or use '?.' in the selector.",
                exception);

    protected static TValue Select<TValue>(Func<T, TValue> selector, T instance) => selector(instance);

    // FluentValidation names a model-level rule after its display name when it has no path.
    protected static string ObjectLevelPath(Rule<T> rule, ValidationContext<T> context, string name) =>
        PropertyPaths.Join(context.PropertyPath, rule.IsObjectLevel && !rule.PathOverridden && rule.DisplayNameProvider is not null ? name : rule.Path);

    private bool ConditionsPass(ValidationContext<T> context)
    {
        foreach (var condition in Conditions!)
        {
            if (condition.IsAsync)
            {
                throw AsyncInSyncRun();
            }

            if (!condition.Evaluate(context))
            {
                return false;
            }
        }

        return true;
    }

    private async Task ExecuteAfterAsyncConditionsAsync(ValidationContext<T> context)
    {
        foreach (var condition in Conditions!)
        {
            var passes = condition.IsAsync
                ? await condition.EvaluateAsync(context).ConfigureAwait(false)
                : condition.Evaluate(context);
            if (!passes)
            {
                return;
            }
        }

        await RunWithDependentsAsync(context).ConfigureAwait(false);
    }

    private Task RunWithDependentsAsync(ValidationContext<T> context)
    {
        if (Dependents is null)
        {
            return RunAsync(context);
        }

        var before = context.Report.Count;
        var task = RunAsync(context);
        if (!task.IsDone())
        {
            return DependentsAfterAsync(task, context, before);
        }

        return context.Report.Count == before
            ? RuleList<T>.RunAsync(Dependents, 0, context, stopOnFailure: false)
            : Task.CompletedTask;
    }

    private async Task DependentsAfterAsync(Task task, ValidationContext<T> context, int before)
    {
        await task.ConfigureAwait(false);
        if (context.Report.Count == before)
        {
            await RuleList<T>.RunAsync(Dependents!, 0, context, stopOnFailure: false).ConfigureAwait(false);
        }
    }
}

// Runs a list of rules. The async version stays synchronous (no state machine, no
// allocation) until a rule really goes async.
internal static class RuleList<T>
{
    public static void Run(Rule<T>[] rules, ValidationContext<T> context, bool stopOnFailure)
    {
        // One try region for the whole loop; it costs nothing until an exception is thrown.
        var current = 0;
        try
        {
            if (!stopOnFailure)
            {
                for (; current < rules.Length; current++)
                {
                    rules[current].Execute(context);
                }

                return;
            }

            var report = context.Report;
            for (; current < rules.Length; current++)
            {
                var before = report.Count;
                rules[current].Execute(context);
                if (report.Count > before)
                {
                    return;
                }
            }
        }
        catch (NullReferenceException exception)
        {
            var translated = rules[current].NullReference(exception);
            if (ReferenceEquals(translated, exception))
            {
                throw;
            }

            throw translated;
        }
    }

    public static Task RunAsync(Rule<T>[] rules, int start, ValidationContext<T> context, bool stopOnFailure)
    {
        var report = context.Report;
        for (var i = start; i < rules.Length; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var before = report.Count;
            Task task;
            try
            {
                task = rules[i].ExecuteAsync(context);
            }
            catch (NullReferenceException exception)
            {
                var translated = rules[i].NullReference(exception);
                if (ReferenceEquals(translated, exception))
                {
                    throw;
                }

                throw translated;
            }

            if (!task.IsDone())
            {
                return ContinueAsync(task, rules, i, context, stopOnFailure, before);
            }

            if (stopOnFailure && report.Count > before)
            {
                break;
            }
        }

        return Task.CompletedTask;
    }

    private static async Task ContinueAsync(Task pending, Rule<T>[] rules, int index, ValidationContext<T> context, bool stopOnFailure, int before)
    {
        await pending.ConfigureAwait(false);
        if (stopOnFailure && context.Report.Count > before)
        {
            return;
        }

        await RunAsync(rules, index + 1, context, stopOnFailure).ConfigureAwait(false);
    }
}

// Decides if a rule runs for the selected groups and properties. Group rules follow
// FluentValidation rule sets: names ignore case, "*" runs everything, rules without a group
// are "default", and Include rules without a group always run (their own rules are filtered).
internal static class RuleSelection
{
    // Like FluentValidation's composite selector: when a custom selector, a property filter or
    // groups are given, a rule runs when any of them selects it (checked in that order, so
    // GroupsExecuted only records groups that decided). With none of them, or inside a child
    // that runs with its own groups, the group filter alone decides.
    public static bool IsSelected<T>(Rule<T> rule, ValidationContext context)
    {
        var selector = context.Run.RuleSelector;
        var properties = context.Properties;
        if (context.GroupsOnly || (selector is null && properties is null))
        {
            return InGroups(rule.Groups, rule.Kind, context);
        }

        return (selector is not null && selector.CanRun(rule.Descriptor, PropertyPaths.Join(context.PropertyPath, rule.Path), context))
            || (properties is not null && PropertyFilter.Allows(rule, context))
            || (context.GroupsRequested && InGroups(rule.Groups, rule.Kind, context));
    }

    private static bool InGroups(string[]? groups, RuleKind kind, ValidationContext context)
    {
        if (!context.GroupsRequested)
        {
            return groups is null || Contains(groups, ValidationGroups.Default);
        }

        var requested = context.SelectedGroups;
        if (groups is null && kind == RuleKind.Include)
        {
            return true;
        }

        if (Contains(requested, ValidationGroups.Default) && (groups is null || Contains(groups, ValidationGroups.Default)))
        {
            context.MarkGroupExecuted(ValidationGroups.Default);
            return true;
        }

        var selected = false;
        if (groups is not null)
        {
            foreach (var group in groups)
            {
                if (Contains(requested, group))
                {
                    context.MarkGroupExecuted(group);
                    selected = true;
                }
            }
        }

        if (selected)
        {
            return true;
        }

        if (!Contains(requested, ValidationGroups.All))
        {
            return false;
        }

        if (groups is null)
        {
            context.MarkGroupExecuted(ValidationGroups.Default);
        }
        else
        {
            foreach (var group in groups)
            {
                context.MarkGroupExecuted(group);
            }
        }

        return true;
    }

    internal static bool Contains(string[] values, string value)
    {
        foreach (var item in values)
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

// ValidationOptions.Properties, with the rules of FluentValidation's IncludeProperties.
internal static class PropertyFilter
{
    public static bool Allows<T>(Rule<T> rule, ValidationContext context)
    {
        var names = context.Properties!;

        // Rules of a child validator all run once the parent rule was selected, unless a
        // selected name points inside the child, or the child is an included validator.
        if (context.Parent is not null && !context.InsideInclude && !AnyHasDot(names))
        {
            return true;
        }

        if (rule.Kind == RuleKind.Include)
        {
            return true;
        }

        var path = PropertyPaths.Join(context.PropertyPath, rule.Path);
        string? normalized = null;
        foreach (var name in names)
        {
            if (Matches(name, path))
            {
                return true;
            }

            if (PropertyPaths.HasAnyIndex(name))
            {
                normalized ??= PropertyPaths.NormalizeIndexes(path);
                if (Matches(name, normalized))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Matches(string name, string path) =>
        string.Equals(name, path, StringComparison.Ordinal)
        || PropertyPaths.StartsWithMember(path, name)
        || PropertyPaths.StartsWithMember(name, path)
        || PropertyPaths.StartsWithIndex(name, path);

    private static bool AnyHasDot(string[] names)
    {
        foreach (var name in names)
        {
            if (PropertyPaths.HasSeparator(name))
            {
                return true;
            }
        }

        return false;
    }
}
