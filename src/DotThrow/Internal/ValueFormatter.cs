using System;
using System.Globalization;
using System.Text;

namespace DotThrow.Internal;

// Writes values into messages. Messages always use the invariant culture for values, so
// logs look the same on every machine and runtime. Strings are wrapped in single quotes,
// null is written as "null", and double and float use "R" (round trip). Dates and times are
// written for people, the same way on every runtime (see AppendDateTime): "2026-05-02",
// "2026-05-02 14:30:00", "2026-05-02 14:30:00Z" (UTC), "2026-05-02 14:30:00 +02:00"
// (DateTimeOffset), "14:30:00.5" (TimeOnly). A {Name:format} in a template still wins.
internal static class ValueFormatter
{
    public static void Append<TValue>(StringBuilder builder, Placeholder id, TValue value, string? format)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        if (value is string text)
        {
            if (Placeholders.IsText(id))
            {
                builder.Append(text);
            }
            else
            {
                builder.Append('\'').Append(text).Append('\'');
            }

            return;
        }

        if (format is null)
        {
            if (value is DateTime dateTime)
            {
                AppendDateTime(builder, dateTime);
                return;
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                AppendDateTimeOffset(builder, dateTimeOffset);
                return;
            }

#if NET6_0_OR_GREATER
            if (value is TimeOnly time)
            {
                AppendTime(builder, time.Ticks);
                return;
            }
#endif

            format = DefaultFormat(value);
        }

#if NET6_0_OR_GREATER
        // Formats ISpanFormattable values (numbers, dates, Guid...) straight into the builder,
        // without boxing and without a temporary string.
        var handler = new StringBuilder.AppendInterpolatedStringHandler(0, 1, builder, CultureInfo.InvariantCulture);
        handler.AppendFormatted(value, format);
        builder.Append(CultureInfo.InvariantCulture, ref handler);
#else
        builder.Append(value is IFormattable formattable ? formattable.ToString(format, CultureInfo.InvariantCulture) : value.ToString());
#endif
    }

    // The typeof checks are constants for the JIT, so a value type pays only for its own branch.
    private static string? DefaultFormat<TValue>(TValue value)
    {
        // "R" (round trip) gives the same digits on .NET Framework and .NET: without it,
        // .NET Framework prints 15 significant digits and .NET prints up to 17.
        if (typeof(TValue) == typeof(double) || typeof(TValue) == typeof(float) || value is double or float)
        {
            return "R";
        }

#if NET6_0_OR_GREATER
        // "yyyy-MM-dd".
        if (typeof(TValue) == typeof(DateOnly) || value is DateOnly)
        {
            return "O";
        }
#endif

        return null;
    }

    // "2026-05-02" for a date at midnight that is not UTC (a plain date), otherwise
    // "2026-05-02 14:30:00", with fractional seconds only when they are not zero and "Z" for
    // UTC. A local time is written without an offset, because the offset depends on the machine.
    private static void AppendDateTime(StringBuilder builder, DateTime value)
    {
        AppendDate(builder, value.Year, value.Month, value.Day);
        var time = value.TimeOfDay.Ticks;
        if (time == 0 && value.Kind != DateTimeKind.Utc)
        {
            return;
        }

        builder.Append(' ');
        AppendTime(builder, time);
        if (value.Kind == DateTimeKind.Utc)
        {
            builder.Append('Z');
        }
    }

    // "2026-05-02 14:30:00 +02:00"; the offset is always written, also "+00:00".
    private static void AppendDateTimeOffset(StringBuilder builder, DateTimeOffset value)
    {
        AppendDate(builder, value.Year, value.Month, value.Day);
        builder.Append(' ');
        AppendTime(builder, value.TimeOfDay.Ticks);
        var offset = value.Offset;
        builder.Append(' ').Append(offset < TimeSpan.Zero ? '-' : '+');
        var minutes = Math.Abs((int)offset.TotalMinutes);
        AppendDigits(builder, minutes / 60, 2);
        builder.Append(':');
        AppendDigits(builder, minutes % 60, 2);
    }

    private static void AppendDate(StringBuilder builder, int year, int month, int day)
    {
        AppendDigits(builder, year, 4);
        builder.Append('-');
        AppendDigits(builder, month, 2);
        builder.Append('-');
        AppendDigits(builder, day, 2);
    }

    // "HH:mm:ss", then "." and up to seven digits without trailing zeros when the time has a
    // fraction of a second.
    private static void AppendTime(StringBuilder builder, long ticks)
    {
        var seconds = ticks / TimeSpan.TicksPerSecond;
        AppendDigits(builder, (int)(seconds / 3600), 2);
        builder.Append(':');
        AppendDigits(builder, (int)(seconds / 60 % 60), 2);
        builder.Append(':');
        AppendDigits(builder, (int)(seconds % 60), 2);
        var fraction = (int)(ticks % TimeSpan.TicksPerSecond);
        if (fraction == 0)
        {
            return;
        }

        var digits = 7;
        while (fraction % 10 == 0)
        {
            fraction /= 10;
            digits--;
        }

        builder.Append('.');
        AppendDigits(builder, fraction, digits);
    }

    private static void AppendDigits(StringBuilder builder, int value, int count)
    {
        for (var divisor = Pow10(count - 1); divisor > 0; divisor /= 10)
        {
            builder.Append((char)('0' + (value / divisor % 10)));
        }
    }

    private static int Pow10(int exponent)
    {
        var result = 1;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10;
        }

        return result;
    }
}
