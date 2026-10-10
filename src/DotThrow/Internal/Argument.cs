using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotThrow.Internal;

// Checks for the library's own arguments, such as a null predicate or a NaN limit. These
// are programming errors, so they always throw, even in collect mode. The throw helpers
// are separate methods, so the checks stay small enough to inline.
internal static class Argument
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value is null)
        {
            ThrowNull(name);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value is null)
        {
            ThrowNull(name);
        }

        if (value.Length == 0)
        {
            ThrowArgument(name, "The value must not be empty.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNegative(int value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value < 0)
        {
            ThrowOutOfRange(name, value, "The value must not be negative.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNegative(TimeSpan value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value < TimeSpan.Zero)
        {
            ThrowOutOfRange(name, value, "The value must not be negative.");
        }
    }

    // A limit for an ordering check: not null, and not NaN (NaN has no order, so a NaN limit
    // would fail every value or pass every value).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Limit<T>([NotNull] T? limit, [CallerArgumentExpression(nameof(limit))] string? name = null)
    {
        NotNull(limit, name);
        if (Numeric.IsNaN(limit))
        {
            ThrowOutOfRange(name, limit, "The limit must not be NaN.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Range<T>(
        [NotNull] T? min,
        [NotNull] T? max,
        [CallerArgumentExpression(nameof(min))] string? minName = null,
        [CallerArgumentExpression(nameof(max))] string? maxName = null)
        where T : IComparable<T>
    {
        Limit(min, minName);
        Limit(max, maxName);
        if (Ordering.Compare(min, max) > 0)
        {
            ThrowOutOfRange(maxName, max, "The maximum must be greater than or equal to the minimum.");
        }
    }

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNull(string? name) => throw new ArgumentNullException(name);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowArgument(string? name, string message) => throw new ArgumentException(message, name);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOutOfRange<T>(string? name, T value, string message) =>
        throw new ArgumentOutOfRangeException(name, value, message);
}
