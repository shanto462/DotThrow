using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// Checks for any IComparable<T>: every number type, DateTime, TimeSpan, string, Version,
// BigInteger, Int128, UInt128, Half, char, DateOnly, TimeOnly and your own types. A null
// value fails with "Null", and NaN fails every check here. Strings compare by ordinal, so
// the result does not depend on the current culture. A UTC DateTime and a local DateTime
// compare as UTC.
//
// A null or NaN limit, or a maximum below the minimum, is a programming error and throws
// even in collect mode. A null limit is caught on every call; the others make every value
// fail, so they are caught on the failure path and cost nothing on the happy path.
public static partial class GuardExtensions
{
    /// <summary>Fails when the value is greater than <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The largest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is NaN.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfGreaterThan<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        if (limit is null)
        {
            Argument.ThrowNull(nameof(limit));
        }

        if (guard.IsActive && (guard.Value is null || !Ordering.AtMost(guard.Value, limit)))
        {
            Argument.Limit(limit);
            return Rejection.Content(guard, FailureKind.OutOfRange, ErrorCodes.GreaterThan, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the value is greater than or equal to <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The value must be less than this.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is NaN.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfGreaterThanOrEqualTo<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        if (limit is null)
        {
            Argument.ThrowNull(nameof(limit));
        }

        if (guard.IsActive && (guard.Value is null || !Ordering.Below(guard.Value, limit)))
        {
            Argument.Limit(limit);
            return Rejection.Content(guard, FailureKind.OutOfRange, ErrorCodes.GreaterThanOrEqualTo, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the value is less than <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The smallest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is NaN.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfLessThan<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        if (limit is null)
        {
            Argument.ThrowNull(nameof(limit));
        }

        if (guard.IsActive && (guard.Value is null || !Ordering.AtLeast(guard.Value, limit)))
        {
            Argument.Limit(limit);
            return Rejection.Content(guard, FailureKind.OutOfRange, ErrorCodes.LessThan, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the value is less than or equal to <paramref name="limit"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The value must be greater than this.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is NaN.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfLessThanOrEqualTo<T>(this Guard<T> guard, T limit, string? message = null)
        where T : IComparable<T>
    {
        if (limit is null)
        {
            Argument.ThrowNull(nameof(limit));
        }

        if (guard.IsActive && (guard.Value is null || !Ordering.Above(guard.Value, limit)))
        {
            Argument.Limit(limit);
            return Rejection.Content(guard, FailureKind.OutOfRange, ErrorCodes.LessThanOrEqualTo, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is outside the range from <paramref name="min"/> to
    /// <paramref name="max"/>. Both ends are allowed (FluentValidation <c>InclusiveBetween</c>).
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="min">The smallest allowed value.</param>
    /// <param name="max">The largest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholders: <c>{From}</c>, <c>{To}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="min"/> or <paramref name="max"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>, or either is NaN.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfOutOfRange<T>(this Guard<T> guard, T min, T max, string? message = null)
        where T : IComparable<T>
    {
        if (min is null || max is null)
        {
            Argument.ThrowNull(min is null ? nameof(min) : nameof(max));
        }

        if (guard.IsActive && (guard.Value is null || !(Ordering.AtLeast(guard.Value, min) && Ordering.AtMost(guard.Value, max))))
        {
            Argument.Range(min, max);
            return Rejection.Content(guard, FailureKind.OutOfRange, ErrorCodes.OutOfRange, message, Placeholder.From, min, Placeholder.To, max);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is not strictly between <paramref name="min"/> and
    /// <paramref name="max"/>: the ends themselves fail too (FluentValidation
    /// <c>ExclusiveBetween</c>).
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="min">The value must be greater than this.</param>
    /// <param name="max">The value must be less than this.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholders: <c>{From}</c>, <c>{To}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="min"/> or <paramref name="max"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>, or either is NaN.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfOutOfRangeExclusive<T>(this Guard<T> guard, T min, T max, string? message = null)
        where T : IComparable<T>
    {
        if (min is null || max is null)
        {
            Argument.ThrowNull(min is null ? nameof(min) : nameof(max));
        }

        if (guard.IsActive && (guard.Value is null || !(Ordering.Above(guard.Value, min) && Ordering.Below(guard.Value, max))))
        {
            Argument.Range(min, max);
            return Rejection.Content(guard, FailureKind.OutOfRange, ErrorCodes.OutOfRangeExclusive, message, Placeholder.From, min, Placeholder.To, max);
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
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholders: <c>{From}</c>, <c>{To}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="min"/> or <paramref name="max"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>, or either is NaN.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfInRange<T>(this Guard<T> guard, T min, T max, string? message = null)
        where T : IComparable<T>
    {
        // A reversed or NaN range would let every value pass, so it is checked on every call.
        // With constant limits the JIT removes this check.
        Argument.Range(min, max);
        if (guard.IsActive && (guard.Value is null || !(Ordering.Below(guard.Value, min) || Ordering.Above(guard.Value, max))))
        {
            return Rejection.Content(guard, FailureKind.OutOfRange, ErrorCodes.InRange, message, Placeholder.From, min, Placeholder.To, max);
        }

        return guard;
    }

    /// <summary>Fails when the value is less than zero. NaN fails.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNegative<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && !Ordering.AtLeast(guard.Value, default(T)))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Negative, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is less than or equal to zero. NaN fails.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNegativeOrZero<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && !Ordering.Above(guard.Value, default(T)))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NegativeOrZero, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is greater than zero. NaN fails.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfPositive<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && !Ordering.AtMost(guard.Value, default(T)))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Positive, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is greater than or equal to zero. NaN fails.</summary>
    /// <typeparam name="T">A number type, or any value type whose default value means zero, like <see cref="TimeSpan"/>.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfPositiveOrZero<T>(this Guard<T> guard, string? message = null)
        where T : struct, IComparable<T>
    {
        if (guard.IsActive && !Ordering.Below(guard.Value, default(T)))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.PositiveOrZero, message);
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
        if (guard.IsActive && Ordering.IsZero(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Zero, message);
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
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NaN, message);
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
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NaN, message);
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
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Infinity, message);
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
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Infinity, message);
        }

        return guard;
    }
}
