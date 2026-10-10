using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;
#if NET8_0_OR_GREATER
using System.Numerics;
#endif

namespace DotThrow;

// Number checks beyond ordering: finiteness, parity, multiples, powers of two, whole
// numbers and decimal precision. The int, long, double, float and decimal overloads work
// on every framework; on .NET 8 and later generic overloads cover every number type
// through generic math (IBinaryInteger<T>, IFloatingPointIeee754<T>), for example short,
// UInt128, Half and NFloat. A failure throws ArgumentOutOfRangeException.
public static partial class GuardExtensions
{
    /// <summary>Fails when the value is NaN or positive or negative infinity.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<double> IfNotFinite(this Guard<double> guard, string? message = null)
    {
        // NaN and infinity are the only values whose difference with themselves is not 0.
        var value = guard.Value;
        if (guard.IsActive && value - value != 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotFinite, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is NaN or positive or negative infinity.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<float> IfNotFinite(this Guard<float> guard, string? message = null)
    {
        var value = guard.Value;
        if (guard.IsActive && value - value != 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotFinite, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is odd. Negative odd numbers fail too.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<int> IfOdd(this Guard<int> guard, string? message = null)
    {
        if (guard.IsActive && (guard.Value & 1) != 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Odd, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is odd. Negative odd numbers fail too.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<long> IfOdd(this Guard<long> guard, string? message = null)
    {
        if (guard.IsActive && (guard.Value & 1) != 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Odd, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is even. Zero is even.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<int> IfEven(this Guard<int> guard, string? message = null)
    {
        if (guard.IsActive && (guard.Value & 1) == 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Even, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is even. Zero is even.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<long> IfEven(this Guard<long> guard, string? message = null)
    {
        if (guard.IsActive && (guard.Value & 1) == 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Even, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is not a multiple of <paramref name="divisor"/>. Zero is a multiple of every divisor.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="divisor">The divisor. Must not be zero; a negative divisor works like its absolute value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Divisor}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="divisor"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<int> IfNotMultipleOf(this Guard<int> guard, int divisor, string? message = null)
    {
        // -1 divides everything; testing it first also avoids the overflow of int.MinValue % -1.
        if (guard.IsActive && (divisor == 0 || (divisor != -1 && guard.Value % divisor != 0)))
        {
            NotZero(divisor);
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotMultipleOf, message, Placeholder.Divisor, divisor);
        }

        return guard;
    }

    /// <summary>Fails when the number is not a multiple of <paramref name="divisor"/>. Zero is a multiple of every divisor.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="divisor">The divisor. Must not be zero; a negative divisor works like its absolute value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Divisor}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="divisor"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<long> IfNotMultipleOf(this Guard<long> guard, long divisor, string? message = null)
    {
        if (guard.IsActive && (divisor == 0 || (divisor != -1 && guard.Value % divisor != 0)))
        {
            NotZero(divisor);
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotMultipleOf, message, Placeholder.Divisor, divisor);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the number is not a whole multiple of <paramref name="divisor"/>, for
    /// example a price that must be a multiple of <c>0.05m</c>. Zero is a multiple of every divisor.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="divisor">The divisor. Must not be zero.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Divisor}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="divisor"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<decimal> IfNotMultipleOf(this Guard<decimal> guard, decimal divisor, string? message = null)
    {
        if (guard.IsActive && (divisor == 0 || guard.Value % divisor != 0))
        {
            NotZero(divisor);
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotMultipleOf, message, Placeholder.Divisor, divisor);
        }

        return guard;
    }

    /// <summary>Fails when the number is not a power of two (1, 2, 4, 8, ...). Zero and negative numbers fail.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<int> IfNotPowerOfTwo(this Guard<int> guard, string? message = null)
    {
        var value = guard.Value;
        if (guard.IsActive && (value <= 0 || (value & (value - 1)) != 0))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotPowerOfTwo, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is not a power of two (1, 2, 4, 8, ...). Zero and negative numbers fail.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<long> IfNotPowerOfTwo(this Guard<long> guard, string? message = null)
    {
        var value = guard.Value;
        if (guard.IsActive && (value <= 0 || (value & (value - 1)) != 0))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotPowerOfTwo, message);
        }

        return guard;
    }

    /// <summary>Fails when the number has a fractional part. NaN and infinity fail.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<double> IfNotInteger(this Guard<double> guard, string? message = null)
    {
#if NET8_0_OR_GREATER
        var whole = double.IsInteger(guard.Value);
#else
        var value = guard.Value;
        var whole = value - value == 0 && Math.Floor(value) == value;
#endif
        if (guard.IsActive && !whole)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotInteger, message);
        }

        return guard;
    }

    /// <summary>Fails when the number has a fractional part. NaN and infinity fail.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<float> IfNotInteger(this Guard<float> guard, string? message = null)
    {
#if NET8_0_OR_GREATER
        var whole = float.IsInteger(guard.Value);
#else
        double value = guard.Value;
        var whole = value - value == 0 && Math.Floor(value) == value;
#endif
        if (guard.IsActive && !whole)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotInteger, message);
        }

        return guard;
    }

    /// <summary>Fails when the number has a fractional part, for example <c>1.5m</c>. <c>2.00m</c> passes.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<decimal> IfNotInteger(this Guard<decimal> guard, string? message = null)
    {
        if (guard.IsActive && decimal.Truncate(guard.Value) != guard.Value)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotInteger, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the number has more than <paramref name="precision"/> digits in total, or
    /// more than <paramref name="scale"/> digits after the decimal point, like FluentValidation's
    /// <c>PrecisionScale</c> (a SQL <c>decimal(precision, scale)</c> column). So the number may
    /// have at most <c>precision - scale</c> digits before the decimal point.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="precision">The largest number of digits in total. At least 1.</param>
    /// <param name="scale">The largest number of digits after the decimal point. From 0 to <paramref name="precision"/>.</param>
    /// <param name="ignoreTrailingZeros">
    /// When <see langword="true"/>, trailing zeros after the decimal point do not count, so
    /// <c>1.50m</c> has scale 1. When <see langword="false"/> (the default) they count, because
    /// <see cref="decimal"/> keeps them: <c>1.50m</c> has scale 2.
    /// </param>
    /// <param name="message">
    /// A custom message, or <see langword="null"/> for the default one. Placeholders:
    /// <c>{ExpectedPrecision}</c>, <c>{ExpectedScale}</c>, <c>{Digits}</c> (digits found
    /// before the decimal point) and <c>{ActualScale}</c>.
    /// </param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="precision"/> is less than 1, or <paramref name="scale"/> is negative or greater than <paramref name="precision"/>.</exception>
    public static Guard<decimal> IfPrecisionScaleExceeded(this Guard<decimal> guard, int precision, int scale, bool ignoreTrailingZeros = false, string? message = null)
    {
        if (precision < 1)
        {
            Argument.ThrowOutOfRange(nameof(precision), precision, "The precision must be at least 1.");
        }

        if (scale < 0 || scale > precision)
        {
            Argument.ThrowOutOfRange(nameof(scale), scale, "The scale must be from 0 to the precision.");
        }

        if (!guard.IsActive)
        {
            return guard;
        }

        Decimals.Measure(guard.Value, ignoreTrailingZeros, out var actualPrecision, out var actualScale);
        var integerDigits = actualPrecision - actualScale;
        if (actualScale > scale || integerDigits > precision - scale)
        {
            return Rejection.Content(
                guard,
                FailureKind.OutOfRange,
                ErrorCodes.PrecisionScaleExceeded,
                message,
                Placeholder.ExpectedPrecision,
                precision,
                Placeholder.ExpectedScale,
                scale,
                Placeholder.Digits,
                Math.Max(integerDigits, 0),
                Placeholder.ActualScale,
                actualScale);
        }

        return guard;
    }

#if NET8_0_OR_GREATER
    /// <summary>Fails when the value is NaN. Covers every IEEE 754 type, such as <see cref="Half"/> and <see cref="System.Runtime.InteropServices.NFloat"/>. Available on .NET 8 and later.</summary>
    /// <typeparam name="T">A floating-point type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNaN<T>(this Guard<T> guard, string? message = null)
        where T : IFloatingPointIeee754<T>
    {
        if (guard.IsActive && T.IsNaN(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NaN, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is positive or negative infinity. Available on .NET 8 and later.</summary>
    /// <typeparam name="T">A floating-point type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfInfinity<T>(this Guard<T> guard, string? message = null)
        where T : IFloatingPointIeee754<T>
    {
        if (guard.IsActive && T.IsInfinity(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Infinity, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is NaN or positive or negative infinity. Available on .NET 8 and later.</summary>
    /// <typeparam name="T">A floating-point type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNotFinite<T>(this Guard<T> guard, string? message = null)
        where T : IFloatingPointIeee754<T>
    {
        if (guard.IsActive && !T.IsFinite(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotFinite, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is odd. Covers every integer type through generic math. Available on .NET 8 and later.</summary>
    /// <typeparam name="T">An integer type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfOdd<T>(this Guard<T> guard, string? message = null)
        where T : IBinaryInteger<T>
    {
        if (guard.IsActive && T.IsOddInteger(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Odd, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is even. Covers every integer type through generic math. Available on .NET 8 and later.</summary>
    /// <typeparam name="T">An integer type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfEven<T>(this Guard<T> guard, string? message = null)
        where T : IBinaryInteger<T>
    {
        if (guard.IsActive && T.IsEvenInteger(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Even, message);
        }

        return guard;
    }

    /// <summary>Fails when the number is not a multiple of <paramref name="divisor"/>. Covers every integer type through generic math. Available on .NET 8 and later.</summary>
    /// <typeparam name="T">An integer type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="divisor">The divisor. Must not be zero.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Divisor}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="divisor"/> is zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNotMultipleOf<T>(this Guard<T> guard, T divisor, string? message = null)
        where T : IBinaryInteger<T>
    {
        // A divisor of -1 divides everything (and MinValue % -1 overflows), so it is tested first.
        if (guard.IsActive && (T.IsZero(divisor) || (!(T.IsNegative(divisor) && divisor == -T.One) && !T.IsZero(guard.Value % divisor))))
        {
            if (T.IsZero(divisor))
            {
                Argument.ThrowOutOfRange(nameof(divisor), divisor, "The divisor must not be zero.");
            }

            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotMultipleOf, message, Placeholder.Divisor, divisor);
        }

        return guard;
    }

    /// <summary>Fails when the number is not a power of two. Zero and negative numbers fail. Available on .NET 8 and later.</summary>
    /// <typeparam name="T">An integer type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNotPowerOfTwo<T>(this Guard<T> guard, string? message = null)
        where T : IBinaryInteger<T>
    {
        if (guard.IsActive && !T.IsPow2(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotPowerOfTwo, message);
        }

        return guard;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void NotZero<T>(T divisor, [CallerArgumentExpression(nameof(divisor))] string? name = null)
        where T : IEquatable<T>
    {
        if (divisor.Equals(default!))
        {
            Argument.ThrowOutOfRange(name, divisor, "The divisor must not be zero.");
        }
    }
}
