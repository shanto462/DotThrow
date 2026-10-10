using System;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Messages;

// Dates and times in default messages are written for people and the same way on every
// runtime (ValueFormatter): a plain date, a time with "Z" for UTC, a DateTimeOffset with its
// offset, fractions of a second only when present. A {Name:format} in a message still wins.
public sealed class ValueFormatTests
{
    [Theory]
    [InlineData(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Unspecified, "2026-05-02")]
    [InlineData(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Local, "2026-05-02")]
    [InlineData(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc, "2026-05-02 00:00:00Z")]
    [InlineData(2026, 5, 2, 14, 30, 0, 0, DateTimeKind.Unspecified, "2026-05-02 14:30:00")]
    [InlineData(2026, 5, 2, 14, 30, 0, 0, DateTimeKind.Local, "2026-05-02 14:30:00")]
    [InlineData(2026, 5, 2, 14, 30, 0, 500, DateTimeKind.Utc, "2026-05-02 14:30:00.5Z")]
    [InlineData(5, 1, 9, 23, 59, 59, 120, DateTimeKind.Unspecified, "0005-01-09 23:59:59.12")]
    public void DateTime_limits(int year, int month, int day, int hour, int minute, int second, int millisecond, DateTimeKind kind, string text)
    {
        var limit = new DateTime(year, month, day, hour, minute, second, millisecond, kind);

        Assert.Equal($"'input' must not be before {text}.", Check.MessageOf(DateTime.MinValue, g => g.IfBefore(limit)));
    }

    [Fact]
    public void DateTime_with_one_tick_and_the_largest_value()
    {
        var oneTick = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1);

        Assert.Equal("'input' must not be before 2026-01-01 00:00:00.0000001Z.", Check.MessageOf(DateTime.MinValue, g => g.IfBefore(oneTick)));
        Assert.Equal("'input' must not be after 0001-01-01.", Check.MessageOf(DateTime.MaxValue, g => g.IfAfter(DateTime.MinValue)));
    }

    [Theory]
    [InlineData(0, 0, "2026-05-02 14:30:00 +00:00")]
    [InlineData(2, 0, "2026-05-02 14:30:00 +02:00")]
    [InlineData(-5, -30, "2026-05-02 14:30:00 -05:30")]
    [InlineData(14, 0, "2026-05-02 14:30:00 +14:00")]
    public void DateTimeOffset_limits_always_show_the_offset(int hours, int minutes, string text)
    {
        var limit = new DateTimeOffset(2026, 5, 2, 14, 30, 0, new TimeSpan(hours, minutes, 0));

        Assert.Equal($"'input' must not be before {text}.", Check.MessageOf(DateTimeOffset.MinValue, g => g.IfBefore(limit)));
    }

    [Fact]
    public void Custom_message_format_wins()
    {
        var limit = new DateTime(2026, 5, 2, 14, 30, 0, DateTimeKind.Utc);

        Assert.Equal("Before 2026/05/02.", Check.MessageOf(DateTime.MinValue, g => g.IfBefore(limit, "Before {ComparisonValue:yyyy/MM/dd}.")));
        Assert.Equal("Before 14:30.", Check.MessageOf(DateTime.MinValue, g => g.IfBefore(limit, "Before {ComparisonValue:HH:mm}.")));
    }

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    [Theory]
    [InlineData(9, 0, 0, 0, "09:00:00")]
    [InlineData(14, 30, 0, 500, "14:30:00.5")]
    [InlineData(23, 59, 59, 999, "23:59:59.999")]
    public void TimeOnly_limits(int hour, int minute, int second, int millisecond, string text)
    {
        var limit = new TimeOnly(hour, minute, second, millisecond);

        Assert.Equal($"'input' must not be before {text}.", Check.MessageOf(TimeOnly.MinValue, g => g.IfBefore(limit)));
    }

    [Fact]
    public void DateOnly_limits_are_plain_dates()
    {
        Assert.Equal("'input' must not be before 2026-05-02.", Check.MessageOf(DateOnly.MinValue, g => g.IfBefore(new DateOnly(2026, 5, 2))));
    }
#endif
}
