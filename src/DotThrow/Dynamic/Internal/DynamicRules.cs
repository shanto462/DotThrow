using System;
using System.Collections.Generic;
using DotThrow.Internal;

namespace DotThrow.Dynamic.Internal;

// A rule built from a definition: reads one value and runs the compiled checks on it, with
// exactly the guard methods a code rule would call. Each check is in the rule frame, so its
// own error code applies.
internal sealed class DynamicValueRule<T, TValue> : Rule<T>
{
    private readonly Func<T, TValue?> _getter;
    private readonly CompiledCheck<TValue>[] _checks;
    private readonly bool _allowNull;
    private readonly string[] _names;

    public DynamicValueRule(Validator<T> owner, string path, Func<T, TValue?> getter, CompiledCheck<TValue>[] checks, bool allowNull)
        : base(owner, RuleKind.Property, path, null)
    {
        _getter = getter;
        _checks = checks;
        _allowNull = allowNull;
        _names = Names(checks);
        if (path.Length == 0)
        {
            // Like For(x => x, ...): the object itself is named "value" in messages.
            DisplayName = ThrowExtensions.DefaultName;
        }
    }

    public override Type ValueType => typeof(TValue);

    public override IReadOnlyList<string> Checks => _names;

    protected override void Run(ValidationContext<T> context)
    {
        var instance = context.Instance;
        var run = context.Run;
        var guard = new Guard<TValue>(_getter(instance)!, NameFor(instance), context.Report);
        if (_allowNull)
        {
            guard = guard.AllowNull();
        }

        foreach (var check in _checks)
        {
            if (!guard.IsActive)
            {
                break;
            }

            run.Frame = new RuleFrame(this, context, -1, check);
            guard = check.Step(guard);
        }

        run.Frame = default;
    }

    protected override string? CheckErrorCode(object? item) => (item as CompiledCheck<TValue>)?.ErrorCode;

    internal static string[] Names(CompiledCheck<TValue>[] checks)
    {
        var names = new string[checks.Length];
        for (var i = 0; i < checks.Length; i++)
        {
            names[i] = checks[i].Name;
        }

        return names;
    }
}

// Each = true: the checks run on every item, like ForEach(x => x.Tags, tag => ...). Failure
// names carry the index; messages use the collection name.
internal sealed class DynamicEachRule<T, TItem> : Rule<T>
{
    private readonly Func<T, IEnumerable<TItem?>?> _getter;
    private readonly CompiledCheck<TItem>[] _checks;
    private readonly bool _allowNull;
    private readonly string[] _names;

    public DynamicEachRule(Validator<T> owner, string path, Func<T, IEnumerable<TItem?>?> getter, CompiledCheck<TItem>[] checks, bool allowNull)
        : base(owner, RuleKind.Collection, path, null)
    {
        _getter = getter;
        _checks = checks;
        _allowNull = allowNull;
        _names = DynamicValueRule<T, TItem>.Names(checks);
    }

    public override Type ValueType => typeof(TItem);

    public override IReadOnlyList<string> Checks => _names;

    protected override void Run(ValidationContext<T> context)
    {
        var instance = context.Instance;
        if (_getter(instance) is not { } items)
        {
            return;
        }

        var name = NameFor(instance);
        if (items is IList<TItem?> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                RunItem(list[i], i, context, name);
            }
        }
        else
        {
            var index = 0;
            foreach (var item in items)
            {
                RunItem(item, index++, context, name);
            }
        }

        context.Run.Frame = default;
    }

    protected override string FailurePath(ValidationContext<T> context, int index, object? item, string name) =>
        PropertyPaths.AppendIndex(PropertyPaths.Join(context.PropertyPath, Path), index, null);

    protected override string? CheckErrorCode(object? item) => (item as CompiledCheck<TItem>)?.ErrorCode;

    private void RunItem(TItem? item, int index, ValidationContext<T> context, string name)
    {
        var run = context.Run;
        var guard = new Guard<TItem>(item!, name, context.Report);
        if (_allowNull)
        {
            guard = guard.AllowNull();
        }

        foreach (var check in _checks)
        {
            if (!guard.IsActive)
            {
                break;
            }

            run.Frame = new RuleFrame(this, context, index, check);
            guard = check.Step(guard);
        }
    }
}
