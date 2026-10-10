using System;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

public sealed class CalendarTests
{
    private static readonly DateTime Saturday = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Monday = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IfNotUtc()
    {
        Check.Passes(Monday, g => g.IfNotUtc());
        Check.Fails<ArgumentException, DateTime>(DateTime.SpecifyKind(Monday, DateTimeKind.Local), g => g.IfNotUtc(), ErrorCodes.NotUtc, "'input' must be in UTC.");
        Check.Fails<ArgumentException, DateTime>(DateTime.SpecifyKind(Monday, DateTimeKind.Unspecified), g => g.IfNotUtc(), ErrorCodes.NotUtc);
        Check.Passes(new DateTimeOffset(Monday), g => g.IfNotUtc());
        Check.Fails<ArgumentException, DateTimeOffset>(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)), g => g.IfNotUtc(), ErrorCodes.NotUtc);
    }

    [Fact]
    public void IfWeekend_and_IfWeekday()
    {
        Check.Fails<ArgumentOutOfRangeException, DateTime>(Saturday, g => g.IfWeekend(), ErrorCodes.Weekend, "'input' must be a weekday.");
        Check.Fails<ArgumentOutOfRangeException, DateTime>(Saturday.AddDays(1), g => g.IfWeekend(), ErrorCodes.Weekend);
        Check.Passes(Monday, g => g.IfWeekend());
        Check.Fails<ArgumentOutOfRangeException, DateTime>(Monday, g => g.IfWeekday(), ErrorCodes.Weekday, "'input' must be a weekend day.");
        Check.Passes(Saturday, g => g.IfWeekday());

        // A DateTimeOffset uses its own clock: Saturday 23:00 at -05:00 is a Saturday, not a Sunday.
        var lateSaturday = new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.FromHours(-5));
        Check.Fails<ArgumentOutOfRangeException, DateTimeOffset>(lateSaturday, g => g.IfWeekend(), ErrorCodes.Weekend);
        Check.Passes(new DateTimeOffset(Monday), g => g.IfWeekend());
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Check.Fails<ArgumentOutOfRangeException, DateOnly>(DateOnly.FromDateTime(Saturday), g => g.IfWeekend(), ErrorCodes.Weekend);
        Check.Fails<ArgumentOutOfRangeException, DateOnly>(DateOnly.FromDateTime(Monday), g => g.IfWeekday(), ErrorCodes.Weekday);
#endif
    }

    [Fact]
    public void IfHasTimeOfDay()
    {
        Check.Passes(Monday, g => g.IfHasTimeOfDay());
        Check.Fails<ArgumentException, DateTime>(Monday.AddTicks(1), g => g.IfHasTimeOfDay(), ErrorCodes.HasTimeOfDay, "'input' must not have a time of day.");
        Check.Passes(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)), g => g.IfHasTimeOfDay());
        Check.Fails<ArgumentException, DateTimeOffset>(new DateTimeOffset(Monday).AddHours(1), g => g.IfHasTimeOfDay(), ErrorCodes.HasTimeOfDay);
    }

    [Fact]
    public void IfOlderThan_and_IfNewerThan_with_the_system_clock()
    {
        var hourAgo = DateTime.UtcNow.AddHours(-1);
        Check.Passes(hourAgo, g => g.IfOlderThan(TimeSpan.FromHours(2)).IfNewerThan(TimeSpan.FromMinutes(30)));
        Check.Fails<ArgumentOutOfRangeException, DateTime>(hourAgo, g => g.IfOlderThan(TimeSpan.FromMinutes(30)), ErrorCodes.OlderThan, "'input' must not be more than 00:30:00 in the past.");
        Check.Fails<ArgumentOutOfRangeException, DateTime>(hourAgo, g => g.IfNewerThan(TimeSpan.FromDays(1)), ErrorCodes.NewerThan, "'input' must be at least 1.00:00:00 in the past.");
        Check.Passes(DateTime.UtcNow.AddDays(1), g => g.IfOlderThan(TimeSpan.Zero));
        Check.Fails<ArgumentOutOfRangeException, DateTime>(DateTime.UtcNow.AddDays(1), g => g.IfNewerThan(TimeSpan.Zero), ErrorCodes.NewerThan);

        // Local and Unspecified values.
        Check.Passes(DateTime.Now.AddHours(-1), g => g.IfOlderThan(TimeSpan.FromHours(2)).IfNewerThan(TimeSpan.FromMinutes(30)));
        Check.Passes(DateTime.SpecifyKind(DateTime.Now.AddHours(-1), DateTimeKind.Unspecified), g => g.IfOlderThan(TimeSpan.FromHours(2)));
        Check.Passes(DateTimeOffset.UtcNow.AddHours(-1), g => g.IfOlderThan(TimeSpan.FromHours(2)).IfNewerThan(TimeSpan.FromMinutes(30)));
        Check.Fails<ArgumentOutOfRangeException, DateTimeOffset>(DateTimeOffset.Now.AddHours(-3), g => g.IfOlderThan(TimeSpan.FromHours(2)), ErrorCodes.OlderThan);
        Check.Passes(DateTime.MinValue, g => g.IfNewerThan(TimeSpan.FromDays(365)));
        Assert.Throws<ArgumentOutOfRangeException>(() => DateTime.UtcNow.Throw().IfOlderThan(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => DateTimeOffset.UtcNow.Throw().IfNewerThan(TimeSpan.FromSeconds(-1)));
    }

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    [Fact]
    public void IfOlderThan_and_IfNewerThan_with_a_TimeProvider()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        var issued = new DateTime(2026, 10, 10, 11, 50, 0, DateTimeKind.Utc);
        Check.Passes(issued, g => g.IfOlderThan(TimeSpan.FromMinutes(15), clock));
        Check.Fails<ArgumentOutOfRangeException, DateTime>(issued, g => g.IfOlderThan(TimeSpan.FromMinutes(5), clock), ErrorCodes.OlderThan);
        Check.Fails<ArgumentOutOfRangeException, DateTime>(issued, g => g.IfNewerThan(TimeSpan.FromMinutes(15), clock), ErrorCodes.NewerThan);
        Check.Passes(new DateTimeOffset(issued), g => g.IfOlderThan(TimeSpan.FromMinutes(15), clock).IfNewerThan(TimeSpan.FromMinutes(5), clock));
        Check.Passes(DateTime.SpecifyKind(issued, DateTimeKind.Unspecified), g => g.IfOlderThan(TimeSpan.FromMinutes(15), clock));
        Assert.Throws<ArgumentNullException>(() => issued.Throw().IfOlderThan(TimeSpan.Zero, (TimeProvider)null!));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
#endif
}
