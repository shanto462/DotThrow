using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotThrow.Internal;

// Checks for the library's own arguments, such as a null predicate. These are
// programming errors, so they always throw, even in collect mode.
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

    public static void NotNegative(int value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value < 0)
        {
            ThrowOutOfRange(name, value, "The value must not be negative.");
        }
    }

    public static void Range<T>(T min, T max, [CallerArgumentExpression(nameof(max))] string? name = null)
        where T : IComparable<T>
    {
        NotNull(min);
        NotNull(max);
        if (min.CompareTo(max) > 0)
        {
            ThrowOutOfRange(name, max, "The maximum must be greater than or equal to the minimum.");
        }
    }

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowNull(string? name) => throw new ArgumentNullException(name);

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowOutOfRange(string? name, object? value, string message) =>
        throw new ArgumentOutOfRangeException(name, value, message);
}
