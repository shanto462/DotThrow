using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// The state of one validation run as seen by one validator: the groups that run, shared
/// items, the property path of a child validator, and ways to add failures. Rules get the
/// typed <see cref="ValidationContext{T}"/>.
/// </summary>
/// <remarks>
/// A context belongs to one run and is reused by later runs on the same thread, so do not
/// keep it after the rule or condition returns.
/// </remarks>
public abstract class ValidationContext
{
    private static readonly string[] DefaultGroups = [ValidationGroups.Default];

    // Callers only ever see a read-only view of the shared default array.
    private static readonly IReadOnlyList<string> DefaultGroupsView = Array.AsReadOnly(DefaultGroups);

    private readonly RunState _run;
    private readonly string _segment = string.Empty;
    private readonly int _index = -1;
    private readonly string? _indexText;
    private string? _path;
    private Dictionary<object, bool>? _conditionResults;

    private protected ValidationContext()
    {
        _run = new RunState();
        _path = string.Empty;
    }

    private protected ValidationContext(in ChildScope scope)
    {
        var parent = scope.Parent;
        _run = parent._run;
        Parent = parent;
        _segment = scope.Segment;
        _index = scope.Index;
        _indexText = scope.IndexText;
        Depth = parent.Depth + 1;
        IsInclude = scope.IsInclude;
        InsideInclude = scope.IsInclude || parent.InsideInclude;
        if (scope.Groups is { Length: > 0 } groups)
        {
            // Like FluentValidation's rule sets on a child validator: the groups replace the
            // whole selection inside the child (groups, property filter and custom selector).
            SelectedGroups = groups;
            GroupsRequested = true;
            GroupsOnly = true;
        }
        else
        {
            SelectedGroups = parent.SelectedGroups;
            GroupsRequested = parent.GroupsRequested;
            GroupsOnly = parent.GroupsOnly;
        }

        Filters = GroupsRequested || (!GroupsOnly && (_run.Properties is not null || _run.RuleSelector is not null));
        CheckDepth();
    }

    /// <summary>
    /// Gets the context of the validator that called this one, or <see langword="null"/> for the
    /// validator the run started with. Child, collection item and included validators have a parent.
    /// </summary>
    public ValidationContext? Parent { get; }

    /// <summary>
    /// Gets the property path of the object this validator checks, for example
    /// <c>"Address"</c> or <c>"Lines[2]"</c>. It is empty for the validator the run started with.
    /// Failures added through this context start with it.
    /// </summary>
    public string PropertyPath => _path ??= BuildPath();

    /// <summary>Gets the groups that run, for example <c>["default"]</c> when no group was asked for.</summary>
    public IReadOnlyList<string> Groups => GroupsRequested ? Array.AsReadOnly(SelectedGroups) : DefaultGroupsView;

    /// <summary>
    /// Gets data shared by every rule of the run, child validators included. It starts with
    /// <see cref="ValidationOptions.Items"/>. FluentValidation calls it root context data.
    /// </summary>
    public IDictionary<string, object?> Items => _run.Items ??= new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Gets a value indicating whether the run is async (<c>ValidateAsync</c>).</summary>
    public bool IsAsync => _run.IsAsync;

    /// <summary>Gets the token of an async run. It is <see cref="CancellationToken.None"/> for <c>Validate</c>.</summary>
    public CancellationToken CancellationToken => _run.CancellationToken;

    internal ValidationReport Report => _run.Report;

    internal RunState Run => _run;

    internal string[] SelectedGroups { get; private set; } = DefaultGroups;

    internal bool GroupsRequested { get; private set; }

    // True when groups or properties were asked for, so every rule must be checked against them.
    internal bool Filters { get; private set; }

    // True inside a child that runs with its own groups (WithChildGroups, PolymorphicValidator
    // registration groups): only those groups select rules there.
    internal bool GroupsOnly { get; private set; }

    internal string[]? Properties => _run.Properties;

    internal CascadeMode? ClassCascadeOverride => _run.ClassCascadeOverride;

    internal int Depth { get; private set; }

    internal bool IsInclude { get; }

    // FluentValidation runs every rule of a child validator when its parent rule was
    // selected by a property filter, except inside an included validator.
    internal bool InsideInclude { get; }

    /// <summary>
    /// Starts a chain of checks in collect mode, like <see cref="ValidationReport.Check{T}(T, string?)"/>.
    /// Failures are named <see cref="PropertyPath"/> plus <paramref name="name"/>, and the options
    /// of the running rule apply (for example its severity).
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="name">The name used in failures. The compiler fills it in with the expression you pass.</param>
    /// <returns>A guard for the value.</returns>
    /// <example><c>context.Check(order.Total, "Total").IfNegative();</c></example>
    public Guard<TValue> Check<TValue>(TValue? value, [CallerArgumentExpression(nameof(value))] string? name = null) =>
        new(value!, name ?? ThrowExtensions.DefaultName, Report);

    /// <summary>
    /// Starts a chain of checks in collect mode for a property of <paramref name="root"/>. The
    /// name comes from the selector, the same as in <see cref="Validator{T}"/> rules, so
    /// <c>context.Check(order, o =&gt; o.Total)</c> is named <c>"Total"</c>.
    /// </summary>
    /// <typeparam name="TRoot">The type of the object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="root">The object that has the value.</param>
    /// <param name="selector">Selects the value.</param>
    /// <param name="selectorExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>A guard for the value.</returns>
    public Guard<TValue> Check<TRoot, TValue>(TRoot root, Func<TRoot, TValue?> selector, [CallerArgumentExpression(nameof(selector))] string? selectorExpression = null)
    {
        Argument.NotNull(selector);
        return new Guard<TValue>(selector(root)!, RuleNames.Path(typeof(TRoot), selectorExpression), Report);
    }

    /// <summary>
    /// Adds a failure named <see cref="PropertyPath"/> plus <paramref name="name"/>. The options of
    /// the running rule apply (message, error code, severity, state).
    /// </summary>
    /// <param name="name">The name of what failed, or an empty string for the object of this validator.</param>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">A stable code, or <see langword="null"/> for <see cref="ErrorCodes.Invalid"/>.</param>
    public void AddFailure(string name, string message, string? errorCode = null)
    {
        Argument.NotNull(name);
        Argument.NotNull(message);
        var frame = _run.Frame;
        if (frame.Sink is not null && ReferenceEquals(frame.Context, this))
        {
            frame.Sink.OnCheckFailure(Report, frame, name, message, errorCode ?? ErrorCodes.Invalid, null);
            return;
        }

        Report.AddRuleFailure(ValidationFailure.Create(
            PropertyPaths.Join(PropertyPath, name),
            RuleMessages.Format(message, _run.Arguments),
            errorCode ?? ErrorCodes.Invalid,
            ValidatorDefaults.Severity,
            null,
            null));
    }

    /// <summary>
    /// Adds a message argument for the running rule (FluentValidation's
    /// <c>MessageFormatter.AppendArgument</c>). Messages of failures the rule adds afterwards can
    /// use it as a placeholder, for example <c>{Limit}</c>, and it is stored in
    /// <see cref="ValidationFailure.MessageArguments"/>. Arguments last until the rule ends.
    /// </summary>
    /// <param name="name">The placeholder name, without braces.</param>
    /// <param name="value">The value.</param>
    /// <example>
    /// <code>
    /// context.AppendArgument("Limit", 10);
    /// context.AddFailure("Lines", "An order can have at most {Limit} lines.");
    /// </code>
    /// </example>
    public void AppendArgument(string name, object? value)
    {
        Argument.NotNull(name);
        (_run.Arguments ??= new Dictionary<string, object?>(StringComparer.Ordinal))[name] = value;
    }

    /// <summary>Adds a failure for the object of this validator (named <see cref="PropertyPath"/>).</summary>
    /// <param name="message">The error message.</param>
    public void AddFailure(string message) => AddFailure(string.Empty, message);

    /// <summary>Adds a failure exactly as given. Its name is not changed.</summary>
    /// <param name="failure">The failure.</param>
    public void AddFailure(ValidationFailure failure)
    {
        Argument.NotNull(failure);
        Report.Add(failure);
    }

    internal bool TryGetConditionResult(object condition, out bool result)
    {
        if (_conditionResults is not null && _conditionResults.TryGetValue(condition, out result))
        {
            return true;
        }

        result = false;
        return false;
    }

    internal void SetConditionResult(object condition, bool result) => (_conditionResults ??= [])[condition] = result;

    // Keeps the order in which groups first ran, so GroupsExecuted is stable.
    internal void MarkGroupExecuted(string group)
    {
        var executed = _run.ExecutedGroups ??= [];
        foreach (var name in executed)
        {
            if (string.Equals(name, group, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        executed.Add(group);
    }

    // Starts a run on a root context (new or reused).
    private protected void StartRun(
        ValidationReport report,
        IReadOnlyList<string>? groups,
        IReadOnlyList<string>? properties,
        IDictionary<string, object?>? items,
        CascadeMode? classCascade,
        IRuleSelector? ruleSelector,
        bool isAsync,
        CancellationToken cancellationToken)
    {
        var run = _run;
        run.RuleSelector = ruleSelector;
        run.Report = report;
        report.Run = run;
        if (groups is { Count: > 0 } && Normalize(groups) is { } selected)
        {
            SelectedGroups = selected;
            GroupsRequested = true;
        }
        else
        {
            SelectedGroups = DefaultGroups;
            GroupsRequested = false;
        }

        run.Properties = properties is { Count: > 0 } ? Normalize(properties) : null;
        Filters = GroupsRequested || run.Properties is not null || ruleSelector is not null;
        run.Items = items;
        run.ClassCascadeOverride = classCascade;
        run.IsAsync = isAsync;
        run.CancellationToken = cancellationToken;
    }

    // A run started by a parent run (a hand-written or polymorphic child validator) continues
    // the parent's depth, so cycles through such validators stop too.
    private protected void StartDepth(int depth)
    {
        Depth = depth;
        if (depth > 0)
        {
            CheckDepth();
        }
    }

    // Ends a run on the root context: sets ValidationReport.GroupsExecuted and detaches the report.
    private protected void EndRun()
    {
        var run = _run;
        var report = run.Report;
        var executed = run.ExecutedGroups;
        if (!GroupsRequested)
        {
            if (executed is not null)
            {
                MarkGroupExecuted(ValidationGroups.Default);
            }

            report.GroupsExecuted = executed is null ? DefaultGroupsView : [.. executed];
        }
        else
        {
            report.GroupsExecuted = executed is null ? Array.Empty<string>() : [.. executed];
        }

        // Items created by the rules go back to the caller's options, as documented.
        if (run.Options is { ItemsOrNull: null } options && run.Items is { } items)
        {
            options.ShareItems(items);
        }

        report.Run = null;
        run.Clear();
        _conditionResults?.Clear();
        Depth = 0;
    }

    private static string[]? Normalize(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0)
        {
            return null;
        }

        var result = new List<string>(names.Count);
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name.Trim());
            }
        }

        return result.Count == 0 ? null : [.. result];
    }

    // Builds the path without recursion (deep object graphs must not overflow the stack):
    // walks up to the nearest context with a known path, then joins the segments downwards
    // and keeps each result.
    private string BuildPath()
    {
        var chain = new List<ValidationContext>();
        var current = this;
        while (current._path is null)
        {
            chain.Add(current);
            current = current.Parent!;
        }

        var path = current._path;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var context = chain[i];
            path = PropertyPaths.Join(path, context._segment);
            if (context._index >= 0)
            {
                path = PropertyPaths.AppendIndex(path, context._index, context._indexText);
            }

            context._path = path;
        }

        return path;
    }

    // The stack check costs a runtime call, so it runs every fourth level; the runtime keeps
    // a large safety margin, far more than four levels need.
    private void CheckDepth()
    {
        if (Depth > ValidatorDefaults.MaxDepth)
        {
            throw new InvalidOperationException(
                $"Validation nested more than {ValidatorDefaults.MaxDepth} validators deep at '{PropertyPath}'. " +
                "The object graph may have a cycle, or validators include each other. Raise ValidatorDefaults.MaxDepth for deeper data.");
        }

        if ((Depth & 3) != 0)
        {
            return;
        }

        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
        }
        catch (InsufficientExecutionStackException exception)
        {
            // Little stack is left, so the message names only the last segment.
            throw new InvalidOperationException(
                $"Validation ran out of stack space at '{_segment}' ({Depth} validators deep). The object graph may be too deep or have a cycle.",
                exception);
        }
    }
}

/// <summary>
/// The state of one validation run for a validator of <typeparamref name="T"/>. Custom rules,
/// conditions and <see cref="Validator{T}.PreValidate(ValidationContext{T})"/> get it.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class ValidationContext<T> : ValidationContext
{
    // One root context per thread is kept for the next run, so a sync run that passes
    // allocates only its report.
    [ThreadStatic]
    private static ValidationContext<T>? _cached;

    private ValidationContext()
    {
        Instance = default!;
    }

    internal ValidationContext(T instance, in ChildScope scope)
        : base(scope)
    {
        Instance = instance;
    }

    /// <summary>Gets the object being validated. FluentValidation calls it <c>InstanceToValidate</c>.</summary>
    public T Instance { get; private set; }

    internal static ValidationContext<T> Rent(
        T instance,
        ValidationReport report,
        ValidationOptions? options,
        IReadOnlyList<string>? groups,
        bool isAsync,
        CancellationToken cancellationToken)
    {
        var context = _cached ?? new ValidationContext<T>();
        _cached = null;
        context.Instance = instance;
        context.StartRun(
            report,
            options?.Groups ?? groups,
            options?.Properties,
            options?.ItemsOrNull,
            options?.ClassCascade,
            options?.RuleSelector,
            isAsync,
            cancellationToken);
        context.Run.Options = options;
        context.StartDepth(options?.Depth ?? 0);
        return context;
    }

    // Ends the run and keeps the context for the next run on this thread.
    internal void Return()
    {
        EndRun();
        Instance = default!;
        _cached = this;
    }
}
