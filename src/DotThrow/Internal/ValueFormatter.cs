using System;
using System.Globalization;
using System.Text;

namespace DotThrow.Internal;

// Writes values into messages. Messages always use the invariant culture for values, so
// logs look the same on every machine and runtime. Strings are wrapped in single quotes,
// null is written as "null", double and float use "R" and dates use "O" (both round trip).
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

        format ??= DefaultFormat(value);

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

        if (typeof(TValue) == typeof(DateTime) || typeof(TValue) == typeof(DateTimeOffset) || value is DateTime or DateTimeOffset)
        {
            return "O";
        }

#if NET6_0_OR_GREATER
        if (typeof(TValue) == typeof(DateOnly) || typeof(TValue) == typeof(TimeOnly) || value is DateOnly or TimeOnly)
        {
            return "O";
        }
#endif

        return null;
    }
}
