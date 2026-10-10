using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// Date and time checks. IfBefore and IfAfter read better than IfLessThan and
// IfGreaterThan for dates. IfInPast and IfInFuture compare with the current time;
// on .NET 8 and later, pass a TimeProvider to control "now" in tests.
//
// DateTime and Kind: when both values have a known Kind (Utc or Local) and the kinds
// differ, they are compared in UTC. An Unspecified value is compared as it is.
public static partial class GuardExtensions
{
    /// <summary>Fails when the value is earlier than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The earliest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTime> IfBefore(this Guard<DateTime> guard, DateTime limit, string? message = null)
    {
        if (guard.IsActive && Ordering.Compare(guard.Value, limit) < 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Before, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the value is later than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The latest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTime> IfAfter(this Guard<DateTime> guard, DateTime limit, string? message = null)
    {
        if (guard.IsActive && Ordering.Compare(guard.Value, limit) > 0)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.After, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the value is earlier than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The earliest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTimeOffset> IfBefore(this Guard<DateTimeOffset> guard, DateTimeOffset limit, string? message = null)
    {
        if (guard.IsActive && guard.Value < limit)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Before, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the value is later than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The latest allowed value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateTimeOffset> IfAfter(this Guard<DateTimeOffset> guard, DateTimeOffset limit, string? message = null)
    {
        if (guard.IsActive && guard.Value > limit)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.After, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is earlier than now. A UTC value is compared with
    /// <see cref="DateTime.UtcNow"/>, any other value with <see cref="DateTime.Now"/>.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTime> IfInPast(this Guard<DateTime> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value < Now(guard.Value.Kind))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InPast, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is later than now. A UTC value is compared with
    /// <see cref="DateTime.UtcNow"/>, any other value with <see cref="DateTime.Now"/>.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTime> IfInFuture(this Guard<DateTime> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value > Now(guard.Value.Kind))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InFuture, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is earlier than now.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTimeOffset> IfInPast(this Guard<DateTimeOffset> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value < DateTimeOffset.UtcNow)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InPast, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is later than now.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTimeOffset> IfInFuture(this Guard<DateTimeOffset> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value > DateTimeOffset.UtcNow)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InFuture, message);
        }

        return guard;
    }

    private static DateTime Now(DateTimeKind kind) => kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;

#if NET8_0_OR_GREATER
    /// <summary>
    /// Fails when the value is earlier than now, as told by <paramref name="timeProvider"/>.
    /// A UTC value is compared with UTC time, any other value with local time.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTime> IfInPast(this Guard<DateTime> guard, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNull(timeProvider);
        if (guard.IsActive && guard.Value < Now(guard.Value.Kind, timeProvider))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InPast, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is later than now, as told by <paramref name="timeProvider"/>.
    /// A UTC value is compared with UTC time, any other value with local time.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTime> IfInFuture(this Guard<DateTime> guard, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNull(timeProvider);
        if (guard.IsActive && guard.Value > Now(guard.Value.Kind, timeProvider))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InFuture, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is earlier than now, as told by <paramref name="timeProvider"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTimeOffset> IfInPast(this Guard<DateTimeOffset> guard, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNull(timeProvider);
        if (guard.IsActive && guard.Value < timeProvider.GetUtcNow())
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InPast, message);
        }

        return guard;
    }

    /// <summary>Fails when the value is later than now, as told by <paramref name="timeProvider"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateTimeOffset> IfInFuture(this Guard<DateTimeOffset> guard, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNull(timeProvider);
        if (guard.IsActive && guard.Value > timeProvider.GetUtcNow())
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InFuture, message);
        }

        return guard;
    }

    /// <summary>Fails when the date is earlier than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The earliest allowed date.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateOnly> IfBefore(this Guard<DateOnly> guard, DateOnly limit, string? message = null)
    {
        if (guard.IsActive && guard.Value < limit)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Before, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the date is later than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The latest allowed date.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<DateOnly> IfAfter(this Guard<DateOnly> guard, DateOnly limit, string? message = null)
    {
        if (guard.IsActive && guard.Value > limit)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.After, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the date is before today (local time).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateOnly> IfInPast(this Guard<DateOnly> guard, string? message = null) =>
        guard.IfInPast(TimeProvider.System, message);

    /// <summary>Fails when the date is after today (local time).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateOnly> IfInFuture(this Guard<DateOnly> guard, string? message = null) =>
        guard.IfInFuture(TimeProvider.System, message);

    /// <summary>Fails when the date is before today, as told by <paramref name="timeProvider"/> (local time).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateOnly> IfInPast(this Guard<DateOnly> guard, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNull(timeProvider);
        if (guard.IsActive && guard.Value < Today(timeProvider))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InPast, message);
        }

        return guard;
    }

    /// <summary>Fails when the date is after today, as told by <paramref name="timeProvider"/> (local time).</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="timeProvider">The clock, for example a fake one in tests.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<DateOnly> IfInFuture(this Guard<DateOnly> guard, TimeProvider timeProvider, string? message = null)
    {
        Argument.NotNull(timeProvider);
        if (guard.IsActive && guard.Value > Today(timeProvider))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.InFuture, message);
        }

        return guard;
    }

    /// <summary>Fails when the time is earlier than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The earliest allowed time.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TimeOnly> IfBefore(this Guard<TimeOnly> guard, TimeOnly limit, string? message = null)
    {
        if (guard.IsActive && guard.Value < limit)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.Before, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    /// <summary>Fails when the time is later than <paramref name="limit"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="limit">The latest allowed time.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TimeOnly> IfAfter(this Guard<TimeOnly> guard, TimeOnly limit, string? message = null)
    {
        if (guard.IsActive && guard.Value > limit)
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.After, message, Placeholder.ComparisonValue, limit);
        }

        return guard;
    }

    private static DateTime Now(DateTimeKind kind, TimeProvider timeProvider) =>
        kind == DateTimeKind.Utc ? timeProvider.GetUtcNow().UtcDateTime : timeProvider.GetLocalNow().DateTime;

    private static DateOnly Today(TimeProvider timeProvider) => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
#endif
}
