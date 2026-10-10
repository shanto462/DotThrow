using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests;

public sealed class DateTimeGuardTests
{
    private static readonly DateTime NewYearUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NewYearUnspecified = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTimeOffset NewYearOffset = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(NewYearUtc.AddTicks(-1), (g, m) => g.IfBefore(NewYearUtc, m), ErrorCodes.Before, Expect.OutOfRange, "'input' must not be before 2026-01-01T00:00:00.0000000Z.");
        yield return Fail(NewYearUnspecified.AddDays(1), (g, m) => g.IfAfter(NewYearUnspecified, m), ErrorCodes.After, Expect.OutOfRange, "'input' must not be after 2026-01-01T00:00:00.0000000.");
        yield return Fail(NewYearOffset.AddSeconds(-1), (g, m) => g.IfBefore(NewYearOffset, m), ErrorCodes.Before, Expect.OutOfRange, "'input' must not be before 2026-01-01T00:00:00.0000000+00:00.");
        yield return Fail(NewYearOffset.AddSeconds(1), (g, m) => g.IfAfter(NewYearOffset, m), ErrorCodes.After, Expect.OutOfRange, "'input' must not be after 2026-01-01T00:00:00.0000000+00:00.");
        yield return Fail(DateTime.UtcNow.AddDays(-1), (g, m) => g.IfInPast(m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(DateTime.Now.AddDays(-1), (g, m) => g.IfInPast(m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(DateTime.MinValue, (g, m) => g.IfInPast(m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(DateTime.UtcNow.AddDays(1), (g, m) => g.IfInFuture(m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");
        yield return Fail(DateTime.Now.AddDays(1), (g, m) => g.IfInFuture(m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");
        yield return Fail(DateTimeOffset.UtcNow.AddDays(-1), (g, m) => g.IfInPast(m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(DateTimeOffset.UtcNow.AddDays(1), (g, m) => g.IfInFuture(m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), FakeTimeProvider.PlusFive);
        var lateClock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero), FakeTimeProvider.PlusFive);

        yield return Fail(new DateTime(2026, 1, 1, 11, 59, 59, DateTimeKind.Utc), (g, m) => g.IfInPast(clock, m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Unspecified), (g, m) => g.IfInPast(clock, m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(new DateTime(2026, 1, 1, 12, 0, 1, DateTimeKind.Utc), (g, m) => g.IfInFuture(clock, m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");
        yield return Fail(new DateTime(2026, 1, 1, 17, 0, 1, DateTimeKind.Unspecified), (g, m) => g.IfInFuture(clock, m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");
        yield return Fail(new DateTimeOffset(2026, 1, 1, 16, 59, 0, TimeSpan.FromHours(5)), (g, m) => g.IfInPast(clock, m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(new DateTimeOffset(2026, 1, 1, 12, 0, 1, TimeSpan.Zero), (g, m) => g.IfInFuture(clock, m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");
        yield return Fail(new DateOnly(2026, 1, 1), (g, m) => g.IfInPast(lateClock, m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(new DateOnly(2026, 1, 3), (g, m) => g.IfInFuture(lateClock, m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");
        yield return Fail(DateOnly.FromDateTime(DateTime.Now.AddDays(-2)), (g, m) => g.IfInPast(m), ErrorCodes.InPast, Expect.OutOfRange, "'input' must not be in the past.");
        yield return Fail(DateOnly.FromDateTime(DateTime.Now.AddDays(2)), (g, m) => g.IfInFuture(m), ErrorCodes.InFuture, Expect.OutOfRange, "'input' must not be in the future.");
        yield return Fail(new DateOnly(2025, 12, 31), (g, m) => g.IfBefore(new DateOnly(2026, 1, 1), m), ErrorCodes.Before, Expect.OutOfRange, "'input' must not be before 2026-01-01.");
        yield return Fail(new DateOnly(2026, 1, 2), (g, m) => g.IfAfter(new DateOnly(2026, 1, 1), m), ErrorCodes.After, Expect.OutOfRange, "'input' must not be after 2026-01-01.");
        yield return Fail(new TimeOnly(8, 59), (g, m) => g.IfBefore(new TimeOnly(9, 0), m), ErrorCodes.Before, Expect.OutOfRange, "'input' must not be before 09:00:00.0000000.");
        yield return Fail(new TimeOnly(17, 0, 1), (g, m) => g.IfAfter(new TimeOnly(17, 0), m), ErrorCodes.After, Expect.OutOfRange, "'input' must not be after 17:00:00.0000000.");
        yield return Fail(new DateOnly(2025, 12, 31), (g, m) => g.IfLessThan(new DateOnly(2026, 1, 1), m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 2026-01-01.");
#endif
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(NewYearUtc, (g, m) => g.IfBefore(NewYearUtc, m));
        yield return Pass(NewYearUtc, (g, m) => g.IfAfter(NewYearUtc, m));
        yield return Pass(NewYearOffset, (g, m) => g.IfBefore(new DateTimeOffset(2026, 1, 1, 5, 0, 0, TimeSpan.FromHours(5)), m));
        yield return Pass(NewYearOffset, (g, m) => g.IfAfter(new DateTimeOffset(2026, 1, 1, 5, 0, 0, TimeSpan.FromHours(5)), m));
        yield return Pass(DateTime.UtcNow.AddDays(1), (g, m) => g.IfInPast(m));
        yield return Pass(DateTime.Now.AddDays(1), (g, m) => g.IfInPast(m));
        yield return Pass(DateTime.UtcNow.AddDays(-1), (g, m) => g.IfInFuture(m));
        yield return Pass(DateTime.MaxValue, (g, m) => g.IfInPast(m));
        yield return Pass(DateTimeOffset.UtcNow.AddDays(1), (g, m) => g.IfInPast(m));
        yield return Pass(DateTimeOffset.UtcNow.AddDays(-1), (g, m) => g.IfInFuture(m));

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), FakeTimeProvider.PlusFive);
        var lateClock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero), FakeTimeProvider.PlusFive);

        yield return Pass(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), (g, m) => g.IfInPast(clock, m));
        yield return Pass(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), (g, m) => g.IfInFuture(clock, m));
        yield return Pass(new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc), (g, m) => g.IfInPast(clock, m));
        yield return Pass(new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Unspecified), (g, m) => g.IfInFuture(clock, m));
        yield return Pass(new DateTimeOffset(2026, 1, 1, 17, 0, 0, TimeSpan.FromHours(5)), (g, m) => g.IfInPast(clock, m));
        yield return Pass(new DateTimeOffset(2026, 1, 1, 17, 0, 0, TimeSpan.FromHours(5)), (g, m) => g.IfInFuture(clock, m));
        yield return Pass(new DateOnly(2026, 1, 2), (g, m) => g.IfInPast(lateClock, m));
        yield return Pass(new DateOnly(2026, 1, 2), (g, m) => g.IfInFuture(lateClock, m));
        yield return Pass(DateOnly.FromDateTime(DateTime.Now.AddDays(2)), (g, m) => g.IfInPast(m));
        yield return Pass(DateOnly.FromDateTime(DateTime.Now.AddDays(-2)), (g, m) => g.IfInFuture(m));
        yield return Pass(new DateOnly(2026, 1, 1), (g, m) => g.IfBefore(new DateOnly(2026, 1, 1), m));
        yield return Pass(new DateOnly(2026, 1, 1), (g, m) => g.IfAfter(new DateOnly(2026, 1, 1), m));
        yield return Pass(new TimeOnly(9, 0), (g, m) => g.IfBefore(new TimeOnly(9, 0), m));
        yield return Pass(new TimeOnly(17, 0), (g, m) => g.IfAfter(new TimeOnly(17, 0), m));
#endif
    }

    public static TheoryData<CheckCase> Failures => Data(FailureCases());

    public static TheoryData<CheckCase> Passes => Data(PassCases());

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_throws_the_default_exception(CheckCase check) => check.AssertThrows();

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_uses_a_custom_message(CheckCase check) => check.AssertCustomMessage();

    [Theory]
    [MemberData(nameof(Passes))]
    public void Passing_check_returns_the_value(CheckCase check) => check.AssertPasses();

    [Fact]
    public void Date_failure_carries_the_actual_value()
    {
        var start = NewYearUtc.AddDays(-1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => start.Throw().IfBefore(NewYearUtc));

        Assert.Equal("start", exception.ParamName);
        Assert.Equal(start, exception.ActualValue);
    }

    [Fact]
    public void IfBefore_and_IfAfter_compare_ticks_and_ignore_the_kind()
    {
        // DateTime comparison ignores Kind. This test documents that behavior.
        var localNewYear = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);

        localNewYear.Throw().IfBefore(NewYearUtc).IfAfter(NewYearUtc);
    }

    [Fact]
    public void IfInPast_compares_a_utc_value_with_utc_now_and_other_values_with_local_now()
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        Assert.SkipWhen(offset.Duration() < TimeSpan.FromMinutes(30), "Needs a local time zone at least 30 minutes away from UTC.");

        // The same wall-clock time, halfway between UTC now and local now.
        var wallClock = DateTime.SpecifyKind(DateTime.UtcNow + TimeSpan.FromTicks(offset.Ticks / 2), DateTimeKind.Unspecified);
        var asUtc = DateTime.SpecifyKind(wallClock, DateTimeKind.Utc);
        var asLocal = DateTime.SpecifyKind(wallClock, DateTimeKind.Local);

        var utcIsPast = Record.Exception(() => asUtc.Throw().IfInPast()) is not null;
        var unspecifiedIsPast = Record.Exception(() => wallClock.Throw().IfInPast()) is not null;
        var localIsPast = Record.Exception(() => asLocal.Throw().IfInPast()) is not null;

        Assert.Equal(offset < TimeSpan.Zero, utcIsPast);
        Assert.Equal(offset > TimeSpan.Zero, unspecifiedIsPast);
        Assert.Equal(offset > TimeSpan.Zero, localIsPast);
    }

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    [Fact]
    public void TimeProvider_overloads_use_the_local_time_zone_of_the_provider()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), FakeTimeProvider.PlusFive);
        var wallClock = new DateTime(2026, 1, 1, 14, 0, 0);

        // 14:00 UTC is after 12:00 UTC, but 14:00 local time is before 17:00 local time.
        DateTime.SpecifyKind(wallClock, DateTimeKind.Utc).Throw().IfInPast(clock);
        Assert.Throws<ArgumentOutOfRangeException>(() => DateTime.SpecifyKind(wallClock, DateTimeKind.Local).Throw().IfInPast(clock));
        Assert.Throws<ArgumentOutOfRangeException>(() => wallClock.Throw().IfInPast(clock));
    }

    [Fact]
    public void DateOnly_today_follows_the_local_date_of_the_provider()
    {
        // 22:00 UTC on Jan 1 is already Jan 2 at UTC+5.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero), FakeTimeProvider.PlusFive);
        var utcClock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero));
        var newYear = new DateOnly(2026, 1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => newYear.Throw().IfInPast(clock));
        newYear.Throw().IfInPast(utcClock).IfInFuture(utcClock);
    }

    [Fact]
    public void Null_time_provider_throws_ArgumentNullException()
    {
        TimeProvider provider = null!;

        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => DateTime.UtcNow.Throw().IfInPast(provider)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => DateTime.UtcNow.Throw().IfInFuture(provider)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => DateTimeOffset.UtcNow.Throw().IfInPast(provider)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => DateTimeOffset.UtcNow.Throw().IfInFuture(provider)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new DateOnly(2026, 1, 1).Throw().IfInPast(provider)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new DateOnly(2026, 1, 1).Throw().IfInFuture(provider)).ParamName);
    }

    [Fact]
    public void TimeProvider_system_matches_the_overloads_without_a_provider()
    {
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);

        Assert.Throws<ArgumentOutOfRangeException>(() => yesterday.Throw().IfInPast(TimeProvider.System));
        yesterday.Throw().IfInFuture(TimeProvider.System);
    }
#endif
}
