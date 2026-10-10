using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// Calendar checks: UTC, weekends, time of day and age. Weekends are Saturday and Sunday,
// the ISO 8601 and most common convention; use If(...) for other calendars.
public static partial class GuardExtensions
{
    /// <summary>Fails when the <see cref="DateTime.Kind"/> is not <see cref="DateTimeKind.Utc"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTime> IfNotUtc(this Guard<DateTime> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value.Kind != DateTimeKind.Utc)
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.NotUtc, message);
        }

        return guard;
    }

    /// <summary>Fails when the <see cref="DateTimeOffset.Offset"/> is not zero.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTimeOffset> IfNotUtc(this Guard<DateTimeOffset> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value.Offset != TimeSpan.Zero)
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.NotUtc, message);
        }

        return guard;
    }

    /// <summary>Fails when the date is a Saturday or a Sunday.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTime> IfWeekend(this Guard<DateTime> guard, string? message = null)
    {
        if (guard.IsActive && IsWeekend(guard.Value.DayOfWeek))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Weekend, message);
        }

        return guard;
    }

    /// <summary>Fails when the date is a Monday to Friday.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTime> IfWeekday(this Guard<DateTime> guard, string? message = null)
    {
        if (guard.IsActive && !IsWeekend(guard.Value.DayOfWeek))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Weekday, message);
        }

        return guard;
    }

    /// <summary>Fails when the date (in the value's own offset) is a Saturday or a Sunday.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTimeOffset> IfWeekend(this Guard<DateTimeOffset> guard, string? message = null)
    {
        if (guard.IsActive && IsWeekend(guard.Value.DayOfWeek))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Weekend, message);
        }

        return guard;
    }

    /// <summary>Fails when the date (in the value's own offset) is a Monday to Friday.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTimeOffset> IfWeekday(this Guard<DateTimeOffset> guard, string? message = null)
    {
        if (guard.IsActive && !IsWeekend(guard.Value.DayOfWeek))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Weekday, message);
        }

        return guard;
    }

    /// <summary>Fails when the value has a time of day other than midnight.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTime> IfHasTimeOfDay(this Guard<DateTime> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value.TimeOfDay != TimeSpan.Zero)
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.HasTimeOfDay, message);
        }

        return guard;
    }

    /// <summary>Fails when the value (in its own offset) has a time of day other than midnight.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTimeOffset> IfHasTimeOfDay(this Guard<DateTimeOffset> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value.TimeOfDay != TimeSpan.Zero)
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.HasTimeOfDay, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is more than <paramref name="maxAge"/> in the past, for example a
    /// token issued too long ago. A value in the future passes.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="maxAge">The largest allowed age. Must not be negative.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MaxAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A UTC or local value is compared with the current UTC time (so a change of daylight
    /// saving time does not matter); an Unspecified value with the local time.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxAge"/> is negative.</exception>
    public static Guard<DateTime> IfOlderThan(this Guard<DateTime> guard, TimeSpan maxAge, string? message = null)
    {
        Argument.NotNegative(maxAge);
        if (guard.IsActive && Age(guard.Value) > maxAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.OlderThan, message, Placeholder.MaxAge, maxAge);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is less than <paramref name="minAge"/> in the past, for example an
    /// account created too recently. A value in the future fails.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="minAge">The smallest allowed age. Must not be negative.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MinAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A UTC or local value is compared with the current UTC time (so a change of daylight
    /// saving time does not matter); an Unspecified value with the local time.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minAge"/> is negative.</exception>
    public static Guard<DateTime> IfNewerThan(this Guard<DateTime> guard, TimeSpan minAge, string? message = null)
    {
        Argument.NotNegative(minAge);
        if (guard.IsActive && Age(guard.Value) < minAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NewerThan, message, Placeholder.MinAge, minAge);
        }

        return guard;
    }

    /// <summary>Fails when the value is more than <paramref name="maxAge"/> in the past. A value in the future passes.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="maxAge">The largest allowed age. Must not be negative.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MaxAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxAge"/> is negative.</exception>
    public static Guard<DateTimeOffset> IfOlderThan(this Guard<DateTimeOffset> guard, TimeSpan maxAge, string? message = null)
    {
        Argument.NotNegative(maxAge);
        if (guard.IsActive && DateTimeOffset.UtcNow - guard.Value > maxAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.OlderThan, message, Placeholder.MaxAge, maxAge);
        }

        return guard;
    }

    /// <summary>Fails when the value is less than <paramref name="minAge"/> in the past. A value in the future fails.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="minAge">The smallest allowed age. Must not be negative.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MinAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minAge"/> is negative.</exception>
    public static Guard<DateTimeOffset> IfNewerThan(this Guard<DateTimeOffset> guard, TimeSpan minAge, string? message = null)
    {
        Argument.NotNegative(minAge);
        if (guard.IsActive && DateTimeOffset.UtcNow - guard.Value < minAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NewerThan, message, Placeholder.MinAge, minAge);
        }

        return guard;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWeekend(DayOfWeek day) => day is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static TimeSpan Age(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => DateTime.UtcNow - value,
        DateTimeKind.Local => DateTime.UtcNow - value.ToUniversalTime(),
        _ => DateTime.Now - value,
    };

#if NET8_0_OR_GREATER
    /// <summary>Fails when the date is a Saturday or a Sunday.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateOnly> IfWeekend(this Guard<DateOnly> guard, string? message = null)
    {
        if (guard.IsActive && IsWeekend(guard.Value.DayOfWeek))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Weekend, message);
        }

        return guard;
    }

    /// <summary>Fails when the date is a Monday to Friday.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateOnly> IfWeekday(this Guard<DateOnly> guard, string? message = null)
    {
        if (guard.IsActive && !IsWeekend(guard.Value.DayOfWeek))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Weekday, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is more than <paramref name="maxAge"/> in the past, as told by
    /// <paramref name="timeProvider"/>. A value in the future passes.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="maxAge">The largest allowed age. Must not be negative.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MaxAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxAge"/> is negative.</exception>
    public static Guard<DateTime> IfOlderThan(this Guard<DateTime> guard, TimeSpan maxAge, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNegative(maxAge);
        Argument.NotNull(timeProvider);
        if (guard.IsActive && Age(guard.Value, timeProvider) > maxAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.OlderThan, message, Placeholder.MaxAge, maxAge);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is less than <paramref name="minAge"/> in the past, as told by
    /// <paramref name="timeProvider"/>. A value in the future fails.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="minAge">The smallest allowed age. Must not be negative.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MinAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minAge"/> is negative.</exception>
    public static Guard<DateTime> IfNewerThan(this Guard<DateTime> guard, TimeSpan minAge, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNegative(minAge);
        Argument.NotNull(timeProvider);
        if (guard.IsActive && Age(guard.Value, timeProvider) < minAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NewerThan, message, Placeholder.MinAge, minAge);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is more than <paramref name="maxAge"/> in the past, as told by
    /// <paramref name="timeProvider"/>. A value in the future passes.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="maxAge">The largest allowed age. Must not be negative.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MaxAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxAge"/> is negative.</exception>
    public static Guard<DateTimeOffset> IfOlderThan(this Guard<DateTimeOffset> guard, TimeSpan maxAge, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNegative(maxAge);
        Argument.NotNull(timeProvider);
        if (guard.IsActive && timeProvider.GetUtcNow() - guard.Value > maxAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.OlderThan, message, Placeholder.MaxAge, maxAge);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is less than <paramref name="minAge"/> in the past, as told by
    /// <paramref name="timeProvider"/>. A value in the future fails.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="minAge">The smallest allowed age. Must not be negative.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{MinAge}</c>.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minAge"/> is negative.</exception>
    public static Guard<DateTimeOffset> IfNewerThan(this Guard<DateTimeOffset> guard, TimeSpan minAge, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNegative(minAge);
        Argument.NotNull(timeProvider);
        if (guard.IsActive && timeProvider.GetUtcNow() - guard.Value < minAge)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NewerThan, message, Placeholder.MinAge, minAge);
        }

        return guard;
    }

    private static TimeSpan Age(DateTime value, TimeProvider timeProvider) => value.Kind switch
    {
        DateTimeKind.Utc => timeProvider.GetUtcNow().UtcDateTime - value,
        DateTimeKind.Local => timeProvider.GetUtcNow().UtcDateTime - value.ToUniversalTime(),
        _ => timeProvider.GetLocalNow().DateTime - value,
    };
#endif
}
