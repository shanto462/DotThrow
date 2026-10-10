using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Internal;

// A When/Unless condition. Conditions of When blocks are shared by many rules, so their
// result is kept per context and the predicate runs once per object.
internal sealed class Condition<T>
{
    private readonly Func<T, ValidationContext<T>, bool>? _predicate;
    private readonly Func<T, ValidationContext<T>, CancellationToken, Task<bool>>? _asyncPredicate;
    private readonly Condition<T>? _source;
    private readonly bool _negate;
    private readonly bool _shared;

    public Condition(Func<T, ValidationContext<T>, bool> predicate, bool negate, bool shared)
    {
        _predicate = predicate;
        _negate = negate;
        _shared = shared;
    }

    public Condition(Func<T, ValidationContext<T>, CancellationToken, Task<bool>> predicate, bool negate, bool shared)
    {
        _asyncPredicate = predicate;
        _negate = negate;
        _shared = shared;
        IsAsync = true;
    }

    // The "otherwise" side of a When block: the same predicate (and cached result), negated.
    private Condition(Condition<T> source)
    {
        _source = source;
        _negate = true;
        IsAsync = source.IsAsync;
    }

    public bool IsAsync { get; }

    public Condition<T> Negated() => new(this);

    public bool Evaluate(ValidationContext<T> context) =>
        _source is not null ? !_source.Evaluate(context) : _negate != Raw(context);

    public Task<bool> EvaluateAsync(ValidationContext<T> context)
    {
        var raw = _source is not null ? _source.EvaluateAsync(context) : RawAsync(context);
        if (raw.IsDone())
        {
            return raw.Result != _negate ? TrueTask : FalseTask;
        }

        return _negate ? NegateAsync(raw) : raw;
    }

    private static readonly Task<bool> TrueTask = Task.FromResult(true);

    private static readonly Task<bool> FalseTask = Task.FromResult(false);

    private static async Task<bool> NegateAsync(Task<bool> task) => !await task.ConfigureAwait(false);

    private bool Raw(ValidationContext<T> context)
    {
        if (!_shared)
        {
            return _predicate!(context.Instance, context);
        }

        if (context.TryGetConditionResult(this, out var cached))
        {
            return cached;
        }

        var result = _predicate!(context.Instance, context);
        context.SetConditionResult(this, result);
        return result;
    }

    private Task<bool> RawAsync(ValidationContext<T> context)
    {
        if (!_shared)
        {
            return _asyncPredicate!(context.Instance, context, context.CancellationToken);
        }

        if (context.TryGetConditionResult(this, out var cached))
        {
            return cached ? TrueTask : FalseTask;
        }

        return StoreAsync(context);
    }

    private async Task<bool> StoreAsync(ValidationContext<T> context)
    {
        var result = await _asyncPredicate!(context.Instance, context, context.CancellationToken).ConfigureAwait(false);
        context.SetConditionResult(this, result);
        return result;
    }
}
