using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Internal;

// For(x => x.Address, addressValidator): runs a validator on a nested object. A null value
// is skipped. Failures keep the options of the child rules (message, code, severity).
// With a provider, the validator is picked per object; a null validator skips the value.
internal sealed class ChildRule<T, TProperty> : Rule<T>
{
    private readonly Func<T, TProperty?> _selector;
    private readonly IChildValidator<TProperty>? _child;
    private readonly Func<T, TProperty, IValidator<TProperty>?>? _provider;
    private readonly Type _validatorType;

    public ChildRule(Validator<T> owner, Func<T, TProperty?> selector, IValidator<TProperty> validator, string path, string? expression)
        : base(owner, RuleKind.Child, path, expression)
    {
        _selector = selector;
        Validator = validator;
        _child = validator as IChildValidator<TProperty>;
        _validatorType = validator.GetType();
    }

    public ChildRule(Validator<T> owner, Func<T, TProperty?> selector, Func<T, TProperty, IValidator<TProperty>?> provider, Type validatorType, string path, string? expression)
        : base(owner, RuleKind.Child, path, expression)
    {
        _selector = selector;
        _provider = provider;
        _validatorType = validatorType;
    }

    public IValidator<TProperty>? Validator { get; }

    public override Type ValueType => typeof(TProperty);

    public override Type ChildValidatorType => _validatorType;

    public override bool NeedsAsync(HashSet<object> visited) =>
        base.NeedsAsync(visited) || AsyncRules.OfChild(Validator, _provider is not null, visited);

    public override void InheritGroups(string[] groups)
    {
        base.InheritGroups(groups);
        if (Validator is Validator<TProperty> { IsInline: true } inline)
        {
            inline.InheritGroups(groups);
        }
    }

    protected override void Run(ValidationContext<T> context)
    {
        if (Select(_selector, context.Instance) is not { } child)
        {
            return;
        }

        var scope = new ChildScope(context, Path, -1, null, ChildGroups, isInclude: false);
        if (_provider is null)
        {
            ChildRunner.Run(Validator!, _child, child, scope);
        }
        else if (_provider(context.Instance, child) is { } validator)
        {
            ChildRunner.Run(validator, child, scope);
        }
    }

    protected override Task RunAsync(ValidationContext<T> context)
    {
        if (Select(_selector, context.Instance) is not { } child)
        {
            return Task.CompletedTask;
        }

        var scope = new ChildScope(context, Path, -1, null, ChildGroups, isInclude: false);
        if (_provider is null)
        {
            return ChildRunner.RunAsync(Validator!, _child, child, scope);
        }

        return _provider(context.Instance, child) is { } validator ? ChildRunner.RunAsync(validator, child, scope) : Task.CompletedTask;
    }
}

// ForEach(x => x.Lines, lineValidator): runs a validator on every item. Null items are skipped.
internal sealed class EachChildRule<T, TItem> : Rule<T>, IEachRule<TItem>, IEachRuleInfo
{
    private readonly Func<T, IEnumerable<TItem?>?> _selector;

    private readonly IChildValidator<TItem>? _child;
    private readonly Func<T, TItem, IValidator<TItem>?>? _provider;
    private readonly Type _validatorType;

    public EachChildRule(Validator<T> owner, Func<T, IEnumerable<TItem?>?> selector, IValidator<TItem> validator, string path, string? expression)
        : base(owner, RuleKind.ChildCollection, path, expression)
    {
        _selector = selector;
        Validator = validator;
        _child = validator as IChildValidator<TItem>;
        _validatorType = validator.GetType();
    }

    public EachChildRule(Validator<T> owner, Func<T, IEnumerable<TItem?>?> selector, Func<T, TItem, IValidator<TItem>?> provider, Type validatorType, string path, string? expression)
        : base(owner, RuleKind.ChildCollection, path, expression)
    {
        _selector = selector;
        _provider = provider;
        _validatorType = validatorType;
    }

    public IValidator<TItem>? Validator { get; }

    public override bool NeedsAsync(HashSet<object> visited) =>
        base.NeedsAsync(visited) || AsyncRules.OfChild(Validator, _provider is not null, visited);

    public Func<TItem, bool>? Filter { get; set; }

    public Func<TItem, CancellationToken, Task<bool>>? AsyncFilter { get; set; }

    public Func<TItem, int, string>? Indexer { get; set; }

    public bool HasAsyncFilter => AsyncFilter is not null;

    public override Type ValueType => typeof(TItem);

    public override Type ChildValidatorType => _validatorType;

    public override void InheritGroups(string[] groups)
    {
        base.InheritGroups(groups);
        if (Validator is Validator<TItem> { IsInline: true } inline)
        {
            inline.InheritGroups(groups);
        }
    }

    protected override void Run(ValidationContext<T> context)
    {
        if (Select(_selector, context.Instance) is not { } items)
        {
            return;
        }

        if (items is IList<TItem?> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                RunItem(list[i], i, context);
            }

            return;
        }

        var index = 0;
        foreach (var item in items)
        {
            RunItem(item, index++, context);
        }
    }

    protected override Task RunAsync(ValidationContext<T> context)
    {
        if (Select(_selector, context.Instance) is not { } items)
        {
            return Task.CompletedTask;
        }

        if (AsyncFilter is not null)
        {
            return RunAllAsync(items.GetEnumerator(), 0, context);
        }

        if (items is IList<TItem?> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var task = RunItemAsync(list[i], i, context);
                if (!task.IsDone())
                {
                    return ContinueListAsync(task, list, i, context);
                }
            }

            return Task.CompletedTask;
        }

        var enumerator = items.GetEnumerator();
        var handedOff = false;
        try
        {
            var index = 0;
            while (enumerator.MoveNext())
            {
                var task = RunItemAsync(enumerator.Current, index++, context);
                if (!task.IsDone())
                {
                    handedOff = true;
                    return ContinueEnumeratorAsync(task, enumerator, index, context);
                }
            }

            return Task.CompletedTask;
        }
        finally
        {
            if (!handedOff)
            {
                enumerator.Dispose();
            }
        }
    }

    private void RunItem(TItem? item, int index, ValidationContext<T> context)
    {
        if (item is null || (Filter is not null && !Filter(item)))
        {
            return;
        }

        if (AsyncFilter is not null)
        {
            throw AsyncInSyncRun();
        }

        if (_provider is null)
        {
            ChildRunner.Run(Validator!, _child, item, Scope(context, item, index));
        }
        else if (_provider(context.Instance, item) is { } validator)
        {
            ChildRunner.Run(validator, item, Scope(context, item, index));
        }
    }

    private Task RunItemAsync(TItem? item, int index, ValidationContext<T> context)
    {
        if (item is null || (Filter is not null && !Filter(item)))
        {
            return Task.CompletedTask;
        }

        return RunChildAsync(item, index, context);
    }

    private Task RunChildAsync(TItem item, int index, ValidationContext<T> context)
    {
        if (_provider is null)
        {
            return ChildRunner.RunAsync(Validator!, _child, item, Scope(context, item, index));
        }

        return _provider(context.Instance, item) is { } validator
            ? ChildRunner.RunAsync(validator, item, Scope(context, item, index))
            : Task.CompletedTask;
    }

    private ChildScope Scope(ValidationContext<T> context, TItem item, int index) =>
        new(context, Path, index, Indexer?.Invoke(item, index), ChildGroups, isInclude: false);

    private async Task ContinueListAsync(Task pending, IList<TItem?> list, int index, ValidationContext<T> context)
    {
        await pending.ConfigureAwait(false);
        for (var i = index + 1; i < list.Count; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await RunItemAsync(list[i], i, context).ConfigureAwait(false);
        }
    }

    private async Task ContinueEnumeratorAsync(Task pending, IEnumerator<TItem?> enumerator, int index, ValidationContext<T> context)
    {
        using (enumerator)
        {
            await pending.ConfigureAwait(false);
            while (enumerator.MoveNext())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                await RunItemAsync(enumerator.Current, index++, context).ConfigureAwait(false);
            }
        }
    }

    private async Task RunAllAsync(IEnumerator<TItem?> enumerator, int index, ValidationContext<T> context)
    {
        using (enumerator)
        {
            while (enumerator.MoveNext())
            {
                var item = enumerator.Current;
                var current = index++;
                if (item is null
                    || (Filter is not null && !Filter(item))
                    || !await AsyncFilter!(item, context.CancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                await RunChildAsync(item, current, context).ConfigureAwait(false);
            }
        }
    }
}

// Include(otherValidator): the rules of another validator for the same object, as if they
// were rules of this one (same path, same groups).
internal sealed class IncludeRule<T> : Rule<T>
{
    private readonly Func<T, IValidator<T>?>? _provider;
    private readonly Type _validatorType;

    public IncludeRule(Validator<T> owner, IValidator<T> validator)
        : base(owner, RuleKind.Include, string.Empty, null)
    {
        Validator = validator;
        _validatorType = validator.GetType();
    }

    public IncludeRule(Validator<T> owner, Func<T, IValidator<T>?> provider, Type validatorType)
        : base(owner, RuleKind.Include, string.Empty, null)
    {
        _provider = provider;
        _validatorType = validatorType;
    }

    // Null for a provider; Include cycles through providers are caught at run time.
    public IValidator<T>? Validator { get; }

    public override Type ChildValidatorType => _validatorType;

    public override bool NeedsAsync(HashSet<object> visited) =>
        base.NeedsAsync(visited) || AsyncRules.OfChild(Validator, _provider is not null, visited);

    protected override void Run(ValidationContext<T> context)
    {
        if (Pick(context) is { } validator)
        {
            ChildRunner.Run(validator, context.Instance, new ChildScope(context, string.Empty, -1, null, ChildGroups, isInclude: true));
        }
    }

    protected override Task RunAsync(ValidationContext<T> context) =>
        Pick(context) is { } validator
            ? ChildRunner.RunAsync(validator, context.Instance, new ChildScope(context, string.Empty, -1, null, ChildGroups, isInclude: true))
            : Task.CompletedTask;

    private IValidator<T>? Pick(ValidationContext<T> context) => Validator ?? _provider!(context.Instance);
}
