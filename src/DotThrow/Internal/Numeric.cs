using System;
using System.Runtime.CompilerServices;

namespace DotThrow.Internal;

internal static class Numeric
{
    // NaN has no order, so every ordering check (greater, less, range, sign) rejects it.
    // The typeof checks are constants for the JIT, so for other types this is free and
    // the casts through object do not box on .NET Core and later.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN<T>(T value)
    {
        if (typeof(T) == typeof(double))
        {
            return double.IsNaN((double)(object)value!);
        }

        if (typeof(T) == typeof(float))
        {
            return float.IsNaN((float)(object)value!);
        }

#if NET8_0_OR_GREATER
        if (typeof(T) == typeof(Half))
        {
            return Half.IsNaN((Half)(object)value!);
        }

        if (typeof(T) == typeof(System.Runtime.InteropServices.NFloat))
        {
            return System.Runtime.InteropServices.NFloat.IsNaN((System.Runtime.InteropServices.NFloat)(object)value!);
        }
#endif

        return false;
    }
}

// The order used by every ordering check. Each "pass" test below is false when either
// side is NaN, so a NaN value fails every ordering check, and a NaN limit makes the check
// fail, where the failure path reports it as a programming error. That keeps the NaN
// tests off the happy path.
//
// Common number types use their own operators (the typeof checks are constants for the
// JIT, so only one branch is compiled). Strings compare by ordinal, so a check gives the
// same answer on every machine. A UTC DateTime and a local DateTime compare as UTC; an
// Unspecified one compares as is. Every other type uses its own CompareTo.
internal static class Ordering
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AtMost<T>(T value, T limit)
        where T : IComparable<T>
    {
        if (typeof(T) == typeof(int))
        {
            return (int)(object)value! <= (int)(object)limit!;
        }

        if (typeof(T) == typeof(long))
        {
            return (long)(object)value! <= (long)(object)limit!;
        }

        if (typeof(T) == typeof(double))
        {
            return (double)(object)value! <= (double)(object)limit!;
        }

        if (typeof(T) == typeof(float))
        {
            return (float)(object)value! <= (float)(object)limit!;
        }

        if (typeof(T) == typeof(decimal))
        {
            return (decimal)(object)value! <= (decimal)(object)limit!;
        }

        return !Numeric.IsNaN(value) && !Numeric.IsNaN(limit) && Compare(value, limit) <= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Below<T>(T value, T limit)
        where T : IComparable<T>
    {
        if (typeof(T) == typeof(int))
        {
            return (int)(object)value! < (int)(object)limit!;
        }

        if (typeof(T) == typeof(long))
        {
            return (long)(object)value! < (long)(object)limit!;
        }

        if (typeof(T) == typeof(double))
        {
            return (double)(object)value! < (double)(object)limit!;
        }

        if (typeof(T) == typeof(float))
        {
            return (float)(object)value! < (float)(object)limit!;
        }

        if (typeof(T) == typeof(decimal))
        {
            return (decimal)(object)value! < (decimal)(object)limit!;
        }

        return !Numeric.IsNaN(value) && !Numeric.IsNaN(limit) && Compare(value, limit) < 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AtLeast<T>(T value, T limit)
        where T : IComparable<T>
    {
        if (typeof(T) == typeof(int))
        {
            return (int)(object)value! >= (int)(object)limit!;
        }

        if (typeof(T) == typeof(long))
        {
            return (long)(object)value! >= (long)(object)limit!;
        }

        if (typeof(T) == typeof(double))
        {
            return (double)(object)value! >= (double)(object)limit!;
        }

        if (typeof(T) == typeof(float))
        {
            return (float)(object)value! >= (float)(object)limit!;
        }

        if (typeof(T) == typeof(decimal))
        {
            return (decimal)(object)value! >= (decimal)(object)limit!;
        }

        return !Numeric.IsNaN(value) && !Numeric.IsNaN(limit) && Compare(value, limit) >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Above<T>(T value, T limit)
        where T : IComparable<T>
    {
        if (typeof(T) == typeof(int))
        {
            return (int)(object)value! > (int)(object)limit!;
        }

        if (typeof(T) == typeof(long))
        {
            return (long)(object)value! > (long)(object)limit!;
        }

        if (typeof(T) == typeof(double))
        {
            return (double)(object)value! > (double)(object)limit!;
        }

        if (typeof(T) == typeof(float))
        {
            return (float)(object)value! > (float)(object)limit!;
        }

        if (typeof(T) == typeof(decimal))
        {
            return (decimal)(object)value! > (decimal)(object)limit!;
        }

        return !Numeric.IsNaN(value) && !Numeric.IsNaN(limit) && Compare(value, limit) > 0;
    }

    // Zero is default(T). NaN is not zero.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero<T>(T value)
        where T : IComparable<T>
    {
        if (typeof(T) == typeof(int))
        {
            return (int)(object)value! == 0;
        }

        if (typeof(T) == typeof(long))
        {
            return (long)(object)value! == 0;
        }

        if (typeof(T) == typeof(double))
        {
            return (double)(object)value! == 0;
        }

        if (typeof(T) == typeof(float))
        {
            return (float)(object)value! == 0;
        }

        if (typeof(T) == typeof(decimal))
        {
            return (decimal)(object)value! == 0;
        }

        return !Numeric.IsNaN(value) && Compare(value, default!) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare<T>(T value, T other)
        where T : IComparable<T>
    {
        if (typeof(T) == typeof(string))
        {
            return string.CompareOrdinal((string)(object)value!, (string)(object)other!);
        }

        if (typeof(T) == typeof(DateTime))
        {
            return Compare((DateTime)(object)value!, (DateTime)(object)other!);
        }

        return value.CompareTo(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(DateTime value, DateTime other) =>
        value.Kind == other.Kind || value.Kind == DateTimeKind.Unspecified || other.Kind == DateTimeKind.Unspecified
            ? value.CompareTo(other)
            : value.ToUniversalTime().CompareTo(other.ToUniversalTime());
}
