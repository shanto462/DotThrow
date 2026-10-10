using System.Threading.Tasks;

namespace DotThrow.Internal;

// Where a child validator runs: under which parent, with which path segment and groups.
internal readonly struct ChildScope
{
    public ChildScope(ValidationContext parent, string segment, int index, string? indexText, string[]? groups, bool isInclude)
    {
        Parent = parent;
        Segment = segment;
        Index = index;
        IndexText = indexText;
        Groups = groups;
        IsInclude = isInclude;
    }

    public ValidationContext Parent { get; }

    // The path from the parent, for example "Address" or "Lines" (with Index 2: "Lines[2]").
    // Empty for included validators, which share the parent path.
    public string Segment { get; }

    public int Index { get; }

    public string? IndexText { get; }

    // Groups that replace the parent groups for this child, or null to keep them.
    public string[]? Groups { get; }

    public bool IsInclude { get; }

    public ChildScope WithGroups(string[]? groups) =>
        groups is null ? this : new ChildScope(Parent, Segment, Index, IndexText, groups, IsInclude);

    public string Prefix() =>
        Index >= 0
            ? PropertyPaths.AppendIndex(PropertyPaths.Join(Parent.PropertyPath, Segment), Index, IndexText)
            : PropertyPaths.Join(Parent.PropertyPath, Segment);
}

// Validators that can run inside another run: they share its report, groups and items,
// and name failures with the parent path. Validator<T> and PolymorphicValidator<T> do.
internal interface IChildValidator<in TChild>
{
    void RunAsChild(TChild instance, in ChildScope scope);

    Task RunAsChildAsync(TChild instance, ChildScope scope);
}

internal static class ChildRunner
{
    public static void Run<TChild>(IValidator<TChild> validator, IChildValidator<TChild>? child, TChild instance, in ChildScope scope)
    {
        if (child is not null)
        {
            child.RunAsChild(instance, scope);
            return;
        }

        // A hand-written IValidator<T>: run it on its own and copy its failures.
        var report = validator.Validate(instance, ExternalOptions(scope));
        scope.Parent.Report.AddRange(report, scope.Prefix());
    }

    public static void Run<TChild>(IValidator<TChild> validator, TChild instance, in ChildScope scope) =>
        Run(validator, validator as IChildValidator<TChild>, instance, scope);

    public static Task RunAsync<TChild>(IValidator<TChild> validator, TChild instance, ChildScope scope) =>
        RunAsync(validator, validator as IChildValidator<TChild>, instance, scope);

    public static Task RunAsync<TChild>(IValidator<TChild> validator, IChildValidator<TChild>? child, TChild instance, ChildScope scope)
    {
        if (child is not null)
        {
            return child.RunAsChildAsync(instance, scope);
        }

        var task = validator.ValidateAsync(instance, ExternalOptions(scope), scope.Parent.CancellationToken);
        if (task.IsDone())
        {
            scope.Parent.Report.AddRange(task.Result, scope.Prefix());
            return Task.CompletedTask;
        }

        return AddWhenDoneAsync(task, scope);
    }

    // A validator known only as the non-generic IValidator (PolymorphicValidator.Add(Type, ...)).
    public static void RunUntyped(IValidator validator, object instance, in ChildScope scope) =>
        scope.Parent.Report.AddRange(validator.Validate(instance, ExternalOptions(scope)), scope.Prefix());

    public static Task RunUntypedAsync(IValidator validator, object instance, ChildScope scope)
    {
        var task = validator.ValidateAsync(instance, ExternalOptions(scope), scope.Parent.CancellationToken);
        if (task.IsDone())
        {
            scope.Parent.Report.AddRange(task.Result, scope.Prefix());
            return Task.CompletedTask;
        }

        return AddWhenDoneAsync(task, scope);
    }

    private static async Task AddWhenDoneAsync(Task<ValidationReport> task, ChildScope scope)
    {
        var report = await task.ConfigureAwait(false);
        scope.Parent.Report.AddRange(report, scope.Prefix());
    }

    private static ValidationOptions ExternalOptions(in ChildScope scope)
    {
        var parent = scope.Parent;
        var options = new ValidationOptions
        {
            Groups = scope.Groups ?? (parent.GroupsRequested ? parent.SelectedGroups : null),
            ClassCascade = parent.ClassCascadeOverride,

            // Child groups replace the custom selector too (see ValidationContext.GroupsOnly).
            RuleSelector = scope.Groups is null && !parent.GroupsOnly ? parent.Run.RuleSelector : null,
            Depth = parent.Depth + 1,
        };

        options.ShareItems(parent.Items);
        return options;
    }
}
