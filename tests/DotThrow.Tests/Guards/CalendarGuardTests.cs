using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests.Guards;

// Case tables for the calendar checks added in v2. CalendarTests holds the detailed edge cases.
// IfNotUtc and IfHasTimeOfDay throw ArgumentException (a wrong kind of value, not a value out of
// range); the other calendar checks throw ArgumentOutOfRangeException.
public sealed class CalendarGuardTests
{
    private static readonly DateTime Saturday = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Monday = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local), (g, m) => g.IfNotUtc(m), ErrorCodes.NotUtc, Expect.Argument, "'input' must be in UTC.");
        yield return Fail(new DateTime(2026, 1, 1), (g, m) => g.IfNotUtc(m), ErrorCodes.NotUtc, Expect.Argument, "'input' must be in UTC.");
        yield return Fail(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(5)), (g, m) => g.IfNotUtc(m), ErrorCodes.NotUtc, Expect.Argument, "'input' must be in UTC.");
        yield return Fail(Saturday, (g, m) => g.IfWeekend(m), ErrorCodes.Weekend, Expect.OutOfRange, "'input' must be a weekday.");
        yield return Fail(Saturday.AddDays(1), (g, m) => g.IfWeekend(m), ErrorCodes.Weekend, Expect.OutOfRange, "'input' must be a weekday.");
        yield return Fail(Monday, (g, m) => g.IfWeekday(m), ErrorCodes.Weekday, Expect.OutOfRange, "'input' must be a weekend day.");
        yield return Fail(new DateTimeOffset(Saturday), (g, m) => g.IfWeekend(m), ErrorCodes.Weekend, Expect.OutOfRange, "'input' must be a weekday.");
        yield return Fail(new DateTimeOffset(Monday), (g, m) => g.IfWeekday(m), ErrorCodes.Weekday, Expect.OutOfRange, "'input' must be a weekend day.");
        yield return Fail(Monday.AddHours(9), (g, m) => g.IfHasTimeOfDay(m), ErrorCodes.HasTimeOfDay, Expect.Argument, "'input' must not have a time of day.");
        yield return Fail(new DateTimeOffset(Monday).AddTicks(1), (g, m) => g.IfHasTimeOfDay(m), ErrorCodes.HasTimeOfDay, Expect.Argument, "'input' must not have a time of day.");
        yield return Fail(DateTime.UtcNow.AddDays(-10), (g, m) => g.IfOlderThan(TimeSpan.FromDays(1), m), ErrorCodes.OlderThan, Expect.OutOfRange, "'input' must not be more than 1.00:00:00 in the past.");
        yield return Fail(DateTimeOffset.UtcNow.AddDays(-10), (g, m) => g.IfOlderThan(TimeSpan.FromDays(1), m), ErrorCodes.OlderThan, Expect.OutOfRange, "'input' must not be more than 1.00:00:00 in the past.");
        yield return Fail(DateTime.UtcNow.AddMinutes(-5), (g, m) => g.IfNewerThan(TimeSpan.FromHours(1), m), ErrorCodes.NewerThan, Expect.OutOfRange, "'input' must be at least 01:00:00 in the past.");
        yield return Fail(DateTime.UtcNow.AddDays(1), (g, m) => g.IfNewerThan(TimeSpan.FromHours(1), m), ErrorCodes.NewerThan, Expect.OutOfRange, "'input' must be at least 01:00:00 in the past.");
        yield return Fail(DateTimeOffset.UtcNow.AddMinutes(-5), (g, m) => g.IfNewerThan(TimeSpan.FromHours(1), m), ErrorCodes.NewerThan, Expect.OutOfRange, "'input' must be at least 01:00:00 in the past.");

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        yield return Fail(DateOnly.FromDateTime(Saturday), (g, m) => g.IfWeekend(m), ErrorCodes.Weekend, Expect.OutOfRange, "'input' must be a weekday.");
        yield return Fail(DateOnly.FromDateTime(Monday), (g, m) => g.IfWeekday(m), ErrorCodes.Weekday, Expect.OutOfRange, "'input' must be a weekend day.");
        yield return Fail(new DateTime(2026, 10, 10, 11, 59, 0, DateTimeKind.Utc), (g, m) => g.IfOlderThan(TimeSpan.FromDays(2), clock, m), ErrorCodes.OlderThan, Expect.OutOfRange, "'input' must not be more than 2.00:00:00 in the past.");
        yield return Fail(new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc), (g, m) => g.IfNewerThan(TimeSpan.FromHours(2), clock, m), ErrorCodes.NewerThan, Expect.OutOfRange, "'input' must be at least 02:00:00 in the past.");
        yield return Fail(new DateTimeOffset(2026, 10, 10, 11, 59, 0, TimeSpan.Zero), (g, m) => g.IfOlderThan(TimeSpan.FromDays(2), clock, m), ErrorCodes.OlderThan, Expect.OutOfRange, "'input' must not be more than 2.00:00:00 in the past.");
        yield return Fail(new DateTimeOffset(2026, 10, 12, 16, 0, 0, TimeSpan.FromHours(5)), (g, m) => g.IfNewerThan(TimeSpan.FromHours(2), clock, m), ErrorCodes.NewerThan, Expect.OutOfRange, "'input' must be at least 02:00:00 in the past.");
#endif
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(Monday, (g, m) => g.IfNotUtc(m));
        yield return Pass(new DateTimeOffset(Monday), (g, m) => g.IfNotUtc(m));
        yield return Pass(Monday, (g, m) => g.IfWeekend(m));
        yield return Pass(Saturday, (g, m) => g.IfWeekday(m));
        yield return Pass(new DateTimeOffset(Monday), (g, m) => g.IfWeekend(m));
        yield return Pass(new DateTimeOffset(Saturday), (g, m) => g.IfWeekday(m));
        yield return Pass(Monday, (g, m) => g.IfHasTimeOfDay(m));
        yield return Pass(new DateTimeOffset(Monday), (g, m) => g.IfHasTimeOfDay(m));
        yield return Pass(DateTime.UtcNow.AddHours(-1), (g, m) => g.IfOlderThan(TimeSpan.FromDays(1), m));
        yield return Pass(DateTime.UtcNow.AddDays(1), (g, m) => g.IfOlderThan(TimeSpan.FromDays(1), m));
        yield return Pass(DateTimeOffset.UtcNow.AddHours(-1), (g, m) => g.IfOlderThan(TimeSpan.FromDays(1), m));
        yield return Pass(DateTime.UtcNow.AddDays(-2), (g, m) => g.IfNewerThan(TimeSpan.FromDays(1), m));
        yield return Pass(DateTimeOffset.UtcNow.AddDays(-2), (g, m) => g.IfNewerThan(TimeSpan.FromDays(1), m));

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        yield return Pass(DateOnly.FromDateTime(Monday), (g, m) => g.IfWeekend(m));
        yield return Pass(DateOnly.FromDateTime(Saturday), (g, m) => g.IfWeekday(m));
        yield return Pass(new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), (g, m) => g.IfOlderThan(TimeSpan.FromDays(2), clock, m));
        yield return Pass(new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc), (g, m) => g.IfNewerThan(TimeSpan.FromHours(2), clock, m));
        yield return Pass(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), (g, m) => g.IfOlderThan(TimeSpan.FromDays(2), clock, m));
        yield return Pass(new DateTimeOffset(2026, 10, 12, 15, 0, 0, TimeSpan.FromHours(5)), (g, m) => g.IfNewerThan(TimeSpan.FromHours(2), clock, m));
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
}
