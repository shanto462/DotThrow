using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotThrow.Internal;

// The failure path of every check. A check calls one of the public methods here only after
// it failed. Each one is a small inlined switch:
//
// - Throw mode: `throw CreateException(...)`. The throw is in the inlined code, so the JIT
//   knows this branch never returns; in a Throw() chain (where the sink is a constant null)
//   the failure branch never joins the success path again, and the guard stays in registers
//   with its state known to be active, so later checks do not test it. CreateException is
//   not inlined, so each failure branch is only a call and a throw.
// - Collect mode: Collect is not inlined. It renders the message and records the failure.
//
// The mode test starts with `guard.Sink is null`. In a Throw() chain the sink is a constant
// null, and the JIT folds a null test early (before it looks for repeated expressions), so
// the collect path is gone by then. With only the type test (`is ValidationReport`), the
// JIT removes the collect path later, after it has already decided that a value such as
// HashSet.Count may change across checks and must be read again (measured: a two-check
// HashSet chain read Count three times instead of once).
//
// The throw path gets the parts of the guard (name, value, sink), not the guard itself: a
// guard is too big for registers, so passing it would put a copy on the caller's stack and
// make the JIT zero it in every call of the caller. The guard is never passed as `this` to
// a non-inlined method either, because taking its address stops the JIT from keeping it in
// registers.
//
// Content: a null value fails with the "Null" code first (the rule for content checks).
// Fail: the value is reported as it is, null or not.
internal static class Rejection
{
    // A message that is final (the public Guard.Fail and Guard.FailOutOfRange).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> WithMessage<T>(Guard<T> guard, FailureKind kind, string code, string message)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw ExceptionWithMessage(guard.Name, guard.Value, guard.Sink, kind, code, message);
        }

        return CollectWithMessage(guard, kind, code, message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Fail<T>(Guard<T> guard, FailureKind kind, string code, string? message, string? key = null)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw CreateException(guard.Name, guard.Value, guard.Sink, kind, code, message, key, false);
        }

        return Collect(guard, kind, code, message, key, false, default(NoArgs));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Fail<T, TA>(Guard<T> guard, FailureKind kind, string code, string? message, Placeholder idA, TA a, string? key = null)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw CreateException(guard.Name, guard.Value, guard.Sink, kind, code, message, key, false, new MessageArgs<TA>(idA, a));
        }

        return Collect(guard, kind, code, message, key, false, new MessageArgs<TA>(idA, a));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Content<T>(Guard<T> guard, FailureKind kind, string code, string? message, string? key = null)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw CreateException(guard.Name, guard.Value, guard.Sink, kind, code, message, key, true);
        }

        return Collect(guard, kind, code, message, key, true, default(NoArgs));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Content<T, TA>(Guard<T> guard, FailureKind kind, string code, string? message, Placeholder idA, TA a, string? key = null)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw CreateException(guard.Name, guard.Value, guard.Sink, kind, code, message, key, true, new MessageArgs<TA>(idA, a));
        }

        return Collect(guard, kind, code, message, key, true, new MessageArgs<TA>(idA, a));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Content<T, TA, TB>(
        Guard<T> guard, FailureKind kind, string code, string? message, Placeholder idA, TA a, Placeholder idB, TB b, string? key = null)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw CreateException(guard.Name, guard.Value, guard.Sink, kind, code, message, key, true, new MessageArgs<TA, TB>(idA, a, idB, b));
        }

        return Collect(guard, kind, code, message, key, true, new MessageArgs<TA, TB>(idA, a, idB, b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Content<T, TA, TB, TC>(
        Guard<T> guard, FailureKind kind, string code, string? message, Placeholder idA, TA a, Placeholder idB, TB b, Placeholder idC, TC c, string? key = null)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw CreateException(guard.Name, guard.Value, guard.Sink, kind, code, message, key, true, new MessageArgs<TA, TB, TC>(idA, a, idB, b, idC, c));
        }

        return Collect(guard, kind, code, message, key, true, new MessageArgs<TA, TB, TC>(idA, a, idB, b, idC, c));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Content<T, TA, TB, TC, TD>(
        Guard<T> guard,
        FailureKind kind,
        string code,
        string? message,
        Placeholder idA,
        TA a,
        Placeholder idB,
        TB b,
        Placeholder idC,
        TC c,
        Placeholder idD,
        TD d,
        string? key = null)
    {
        if (guard.Sink is null || !guard.Collects)
        {
            throw CreateException(guard.Name, guard.Value, guard.Sink, kind, code, message, key, true, new MessageArgs<TA, TB, TC, TD>(idA, a, idB, b, idC, c, idD, d));
        }

        return Collect(guard, kind, code, message, key, true, new MessageArgs<TA, TB, TC, TD>(idA, a, idB, b, idC, c, idD, d));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    private static System.Exception ExceptionWithMessage<T>(string name, T value, object? sink, FailureKind kind, string code, string message) =>
        Guard<T>.CreateException(name, value, sink, kind, code, message);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    private static Guard<T> CollectWithMessage<T>(Guard<T> guard, FailureKind kind, string code, string message) =>
        guard.Collect(kind, code, message);

    // No arguments: a separate overload, so the inlined failure branch does not build and pass
    // an empty struct (which takes a stack slot in the caller).
    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    private static System.Exception CreateException<T>(string name, T value, object? sink, FailureKind kind, string code, string? message, string? key, bool content) =>
        CreateException(name, value, sink, kind, code, message, key, content, default(NoArgs));

    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    private static System.Exception CreateException<T, TArgs>(
        string name, T value, object? sink, FailureKind kind, string code, string? message, string? key, bool content, TArgs args)
        where TArgs : struct, IMessageArgs =>
        content && value is null
            ? Guard<T>.CreateException(name, value, sink, FailureKind.Null, ErrorCodes.Null, Messages.Render(message, ErrorCodes.Null, null, name, value, default(NoArgs)))
            : Guard<T>.CreateException(name, value, sink, kind, code, Messages.Render(message, code, key, name, value, args));

    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    private static Guard<T> Collect<T, TArgs>(Guard<T> guard, FailureKind kind, string code, string? message, string? key, bool content, TArgs args)
        where TArgs : struct, IMessageArgs =>
        content && guard.Value is null
            ? guard.Collect(FailureKind.Null, ErrorCodes.Null, Messages.Render(message, ErrorCodes.Null, null, guard.Name, guard.Value, default(NoArgs)))
            : guard.Collect(kind, code, Messages.Render(message, code, key, guard.Name, guard.Value, args));
}
