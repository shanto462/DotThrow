using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Internal;

// For(x => x.Name, name => name.IfNullOrEmpty()), with or without the root object.
internal sealed class PropertyRule<T, TProperty> : Rule<T>
{
    private readonly Func<T, TProperty?> _selector;
    private readonly GuardChain<TProperty>? _chain;
    private readonly GuardChain<TProperty, T>? _rootChain;

    public PropertyRule(Validator<T> owner, Func<T, TProperty?> selector, GuardChain<TProperty>? chain, GuardChain<TProperty, T>? rootChain, string path, string? expression)
        : base(owner, RuleKind.Property, path, expression)
    {
        _selector = selector;
        _chain = chain;
        _rootChain = rootChain;
    }

    public override Type ValueType => typeof(TProperty);

    protected override void Run(ValidationContext<T> context)
    {
        var instance = context.Instance;
        var value = Select(_selector, instance);
        var root = context.Run;
        root.Frame = new RuleFrame(this, context, -1, null);
        var guard = new Guard<TProperty>(value!, NameFor(instance), context.Report);
        if (_chain is not null)
        {
            _chain(guard);
        }
        else
        {
            _rootChain!(guard, instance);
        }

        root.Frame = default;
    }
}

// Settings that only collection rules have (EachRuleOptions).
internal interface IEachRule<TItem>
{
    Func<TItem, bool>? Filter { get; set; }

    Func<TItem, CancellationToken, Task<bool>>? AsyncFilter { get; set; }

    Func<TItem, int, string>? Indexer { get; set; }
}

// ForEach(x => x.Tags, tag => tag.IfLongerThan(20)): the chain runs for every item.
// Stop cascade stops the chain of one item; the other items are still checked.
internal sealed class EachRule<T, TItem> : Rule<T>, IEachRule<TItem>, IEachRuleInfo
{
    private readonly Func<T, IEnumerable<TItem?>?> _selector;
    private readonly GuardChain<TItem>? _chain;
    private readonly GuardChain<TItem, T>? _rootChain;

    public EachRule(Validator<T> owner, Func<T, IEnumerable<TItem?>?> selector, GuardChain<TItem>? chain, GuardChain<TItem, T>? rootChain, string path, string? expression)
        : base(owner, RuleKind.Collection, path, expression)
    {
        _selector = selector;
        _chain = chain;
        _rootChain = rootChain;
    }

    public Func<TItem, bool>? Filter { get; set; }

    public Func<TItem, CancellationToken, Task<bool>>? AsyncFilter { get; set; }

    public Func<TItem, int, string>? Indexer { get; set; }

    public bool HasAsyncFilter => AsyncFilter is not null;

    public override Type ValueType => typeof(TItem);

    protected override void Run(ValidationContext<T> context)
    {
        var instance = context.Instance;
        if (Select(_selector, instance) is not { } items)
        {
            return;
        }

        var report = context.Report;
        var name = NameFor(instance);
        if (items is IList<TItem?> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                RunItem(list[i], i, context, report, name);
            }
        }
        else
        {
            var index = 0;
            foreach (var item in items)
            {
                RunItem(item, index++, context, report, name);
            }
        }

        context.Run.Frame = default;
    }

    protected override Task RunAsync(ValidationContext<T> context)
    {
        if (AsyncFilter is null)
        {
            Run(context);
            return Task.CompletedTask;
        }

        return RunWithAsyncFilterAsync(context);
    }

    protected override string FailurePath(ValidationContext<T> context, int index, object? item, string name) =>
        PropertyPaths.AppendIndex(PropertyPaths.Join(context.PropertyPath, Path), index, Indexer is null ? null : Indexer((TItem)item!, index));

    private void RunItem(TItem? item, int index, ValidationContext<T> context, ValidationReport report, string name)
    {
        if (Filter is not null && !Filter(item!))
        {
            return;
        }

        if (AsyncFilter is not null)
        {
            throw AsyncInSyncRun();
        }

        Check(item, index, context, report, name);
    }

    private void Check(TItem? item, int index, ValidationContext<T> context, ValidationReport report, string name)
    {
        context.Run.Frame = new RuleFrame(this, context, index, Indexer is null ? null : item);
        var guard = new Guard<TItem>(item!, name, report);
        if (_chain is not null)
        {
            _chain(guard);
        }
        else
        {
            _rootChain!(guard, context.Instance);
        }
    }

    private async Task RunWithAsyncFilterAsync(ValidationContext<T> context)
    {
        var instance = context.Instance;
        if (Select(_selector, instance) is not { } items)
        {
            return;
        }

        var report = context.Report;
        var name = NameFor(instance);
        var index = 0;
        foreach (var item in items)
        {
            var current = index++;
            if ((Filter is not null && !Filter(item!)) || !await AsyncFilter!(item!, context.CancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            Check(item, current, context, report, name);
        }

        context.Run.Frame = default;
    }
}

// Must(x => ...) and Must(x => x.Email, email => ...): one predicate, one failure.
internal sealed class MustRule<T, TValue> : Rule<T>
{
    private readonly Func<T, TValue?> _selector;
    private readonly Func<T, TValue, bool> _predicate;
    private readonly string? _message;
    private readonly bool _objectLevel;

    public MustRule(Validator<T> owner, Func<T, TValue?> selector, Func<T, TValue, bool> predicate, string? message, string path, string? expression, bool objectLevel)
        : base(owner, RuleKind.Must, path, expression)
    {
        _selector = selector;
        _predicate = predicate;
        _message = message;
        _objectLevel = objectLevel;

        // The message works like WithMessage: placeholders such as {PropertyName} are filled.
        Message = message;
    }

    public override bool IsObjectLevel => _objectLevel;

    public override Type ValueType => typeof(TValue);

    public override IReadOnlyList<string> Checks => [ErrorCodes.Must];

    // A rule about the whole object takes its name from WithName, also a computed one.
    protected override string FailurePath(ValidationContext<T> context, int index, object? item, string name) =>
        ObjectLevelPath(this, context, name);

    protected override void Run(ValidationContext<T> context)
    {
        var instance = context.Instance;
        var value = Select(_selector, instance);
        if (!_predicate(instance, value!))
        {
            var name = NameFor(instance);
            AddFailure(context.Report, context, -1, null, name, _message ?? RuleMessages.Must(name), ErrorCodes.Must, value);
        }
    }
}

// MustAsync: like Must, with an async predicate. It only runs in async runs.
internal sealed class MustAsyncRule<T, TValue> : Rule<T>
{
    private readonly Func<T, TValue?> _selector;
    private readonly Func<T, TValue, CancellationToken, Task<bool>> _predicate;
    private readonly string? _message;
    private readonly bool _objectLevel;

    public MustAsyncRule(Validator<T> owner, Func<T, TValue?> selector, Func<T, TValue, CancellationToken, Task<bool>> predicate, string? message, string path, string? expression, bool objectLevel)
        : base(owner, RuleKind.MustAsync, path, expression)
    {
        _selector = selector;
        _predicate = predicate;
        _message = message;
        _objectLevel = objectLevel;

        // The message works like WithMessage: placeholders such as {PropertyName} are filled.
        Message = message;
    }

    public override bool IsAsync => true;

    public override bool IsObjectLevel => _objectLevel;

    public override Type ValueType => typeof(TValue);

    public override IReadOnlyList<string> Checks => [ErrorCodes.Must];

    protected override string FailurePath(ValidationContext<T> context, int index, object? item, string name) =>
        ObjectLevelPath(this, context, name);

    protected override void Run(ValidationContext<T> context) => throw AsyncInSyncRun();

    protected override Task RunAsync(ValidationContext<T> context)
    {
        var instance = context.Instance;
        var value = Select(_selector, instance);
        var task = _predicate(instance, value!, context.CancellationToken);
        if (!task.IsDone())
        {
            return CompleteAsync(task, context, value);
        }

        Complete(task.Result, context, value);
        return Task.CompletedTask;
    }

    private async Task CompleteAsync(Task<bool> task, ValidationContext<T> context, TValue? value) =>
        Complete(await task.ConfigureAwait(false), context, value);

    private void Complete(bool valid, ValidationContext<T> context, TValue? value)
    {
        if (!valid)
        {
            var name = NameFor(context.Instance);
            AddFailure(context.Report, context, -1, null, name, _message ?? RuleMessages.Must(name), ErrorCodes.Must, value);
        }
    }
}

// Custom((x, context) => ...): free-form checks through the context.
internal sealed class CustomRule<T> : Rule<T>
{
    private readonly Action<T, ValidationContext<T>>? _rule;
    private readonly Func<T, ValidationContext<T>, CancellationToken, Task>? _asyncRule;

    public CustomRule(Validator<T> owner, Action<T, ValidationContext<T>> rule)
        : base(owner, RuleKind.Custom, string.Empty, null)
    {
        _rule = rule;
    }

    public CustomRule(Validator<T> owner, Func<T, ValidationContext<T>, CancellationToken, Task> rule)
        : base(owner, RuleKind.CustomAsync, string.Empty, null)
    {
        _asyncRule = rule;
    }

    public override bool IsAsync => _asyncRule is not null;

    protected override void Run(ValidationContext<T> context)
    {
        if (_rule is null)
        {
            throw AsyncInSyncRun();
        }

        var root = context.Run;
        root.Frame = new RuleFrame(this, context, -1, null);
        _rule(context.Instance, context);
        root.Frame = default;
        root.ClearArguments();
    }

    protected override Task RunAsync(ValidationContext<T> context)
    {
        if (_asyncRule is null)
        {
            Run(context);
            return Task.CompletedTask;
        }

        var root = context.Run;
        root.Frame = new RuleFrame(this, context, -1, null);
        var task = _asyncRule(context.Instance, context, context.CancellationToken);
        if (task.IsDone())
        {
            root.Frame = default;
            root.ClearArguments();
            return Task.CompletedTask;
        }

        return ClearAfterAsync(task, root);
    }

    protected override string FailurePath(ValidationContext<T> context, int index, object? item, string name) =>
        PropertyPaths.Join(context.PropertyPath, name);

    private static async Task ClearAfterAsync(Task task, RunState root)
    {
        await task.ConfigureAwait(false);
        root.Frame = default;
        root.ClearArguments();
    }
}
