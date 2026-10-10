using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// Checks for any IComparable<T>: every number type, DateTime, TimeSpan, string, Version,
// and your own types. A null value fails with "Null", and NaN fails every check here.
public static partial class GuardExtensions
{
    /// <summary>Fails when the value is greater than <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The largest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfGreaterThan<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        Argument.NotNull(limit);
        if (!guard.IsActive)
        {
            return guard;
        }

        var value = guard.Value;
        if (value is null)
        {
            return guard.FailNull(message);
        }

        if (Numeric.IsNaN(value) || value.CompareTo(limit) > 0)
        {
            return guard.FailOutOfRange(ErrorCodes.GreaterThan, message ?? Messages.GreaterThan(guard.Name, limit));
        }

        return guard;
    }

    /// <summary>Fails when the value is greater than or equal to <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The value must be less than this.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfGreaterThanOrEqualTo<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        Argument.NotNull(limit);
        if (!guard.IsActive)
        {
            return guard;
        }

        var value = guard.Value;
        if (value is null)
        {
            return guard.FailNull(message);
        }

        if (Numeric.IsNaN(value) || value.CompareTo(limit) >= 0)
        {
            return guard.FailOutOfRange(ErrorCodes.GreaterThanOrEqualTo, message ?? Messages.GreaterThanOrEqualTo(guard.Name, limit));
        }

        return guard;
    }

    /// <summary>Fails when the value is less than <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The smallest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfLessThan<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        Argument.NotNull(limit);
        if (!guard.IsActive)
        {
            return guard;
        }

        var value = guard.Value;
        if (value is null)
        {
            return guard.FailNull(message);
        }

        if (Numeric.IsNaN(value) || value.CompareTo(limit) < 0)
        {
            return guard.FailOutOfRange(ErrorCodes.LessThan, message ?? Messages.LessThan(guard.Name, limit));
        }

        return guard;
    }

    /// <summary>Fails when the value is less than or equal to <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The value must be greater than this.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfLessThanOrEqualTo<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        Argument.NotNull(limit);
        if (!guard.IsActive)
        {
            return guard;
        }

        var value = guard.Value;
        if (value is null)
        {
            return guard.FailNull(message);
        }

        if (Numeric.IsNaN(value) || value.CompareTo(limit) <= 0)
        {
            return guard.FailOutOfRange(ErrorCodes.LessThanOrEqualTo, message ?? Messages.LessThanOrEqualTo(guard.Name, limit));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is outside the range from <paramref name="min"/> to
    /// <paramref name="max"/>. Both ends are allowed.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="min">The smallest allowed value.</param>
    /// <param name="max">The largest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfOutOfRange<T>(this Guard<T> guard, T min, T max, string? message = null)
        where T : IComparable<T>
    {
        Argument.Range(min, max);
        if (!guard.IsActive)
        {
            return guard;
        }

        var value = guard.Value;
        if (value is null)
        {
            return guard.FailNull(message);
        }

        if (Numeric.IsNaN(value) || value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            return guard.FailOutOfRange(ErrorCodes.OutOfRange, message ?? Messages.OutOfRange(guard.Name, min, max));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is inside the range from <paramref name="min"/> to
    /// <paramref name="max"/>, ends included.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="min">The start of the rejected range.</param>
    /// <param name="max">The end of the rejected range.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfInRange<T>(this Guard<T> guard, T min, T max, string? message = null)
        where T : IComparable<T>
    {
        Argument.Range(min, max);
        if (!guard.IsActive)
        {
            return guard;
        }

        var value = guard.Value;
        if (value is null)
        {
            return guard.FailNull(message);
        }

        if (Numeric.IsNaN(value) || (value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0))
        {
            return guard.FailOutOfRange(ErrorCodes.InRange, message ?? Messages.InRange(guard.Name, min, max));
        }

        return guard;
    }

    /// <summary>Fails when the value is less than zero.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNegative<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && (Numeric.IsNaN(guard.Value) || guard.Value.CompareTo(default) < 0))
        {
            return guard.FailOutOfRange(ErrorCodes.Negative, message ?? Messages.Negative(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is less than or equal to zero.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNegativeOrZero<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && (Numeric.IsNaN(guard.Value) || guard.Value.CompareTo(default) <= 0))
        {
            return guard.FailOutOfRange(ErrorCodes.NegativeOrZero, message ?? Messages.NegativeOrZero(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is greater than zero.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfPositive<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && (Numeric.IsNaN(guard.Value) || guard.Value.CompareTo(default) > 0))
        {
            return guard.FailOutOfRange(ErrorCodes.Positive, message ?? Messages.Positive(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is greater than or equal to zero.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfPositiveOrZero<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && (Numeric.IsNaN(guard.Value) || guard.Value.CompareTo(default) >= 0))
        {
            return guard.FailOutOfRange(ErrorCodes.PositiveOrZero, message ?? Messages.PositiveOrZero(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is zero. NaN is not zero, so it passes.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfZero<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && !Numeric.IsNaN(guard.Value) && guard.Value.CompareTo(default) == 0)
        {
            return guard.FailOutOfRange(ErrorCodes.Zero, message ?? Messages.Zero(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is NaN (not a number).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<double> IfNaN(this Guard<double> guard, string? message = null)
    {
        if (guard.IsActive && double.IsNaN(guard.Value))
        {
            return guard.FailOutOfRange(ErrorCodes.NaN, message ?? Messages.NaN(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is NaN (not a number).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<float> IfNaN(this Guard<float> guard, string? message = null)
    {
        if (guard.IsActive && float.IsNaN(guard.Value))
        {
            return guard.FailOutOfRange(ErrorCodes.NaN, message ?? Messages.NaN(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is positive or negative infinity.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<double> IfInfinity(this Guard<double> guard, string? message = null)
    {
        if (guard.IsActive && double.IsInfinity(guard.Value))
        {
            return guard.FailOutOfRange(ErrorCodes.Infinity, message ?? Messages.Infinity(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is positive or negative infinity.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<float> IfInfinity(this Guard<float> guard, string? message = null)
    {
        if (guard.IsActive && float.IsInfinity(guard.Value))
        {
            return guard.FailOutOfRange(ErrorCodes.Infinity, message ?? Messages.Infinity(guard.Name));
        }

        return guard;
    }
}
