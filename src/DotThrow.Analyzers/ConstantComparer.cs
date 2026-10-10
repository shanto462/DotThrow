using System;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace DotThrow.Analyzers;

/// <summary>Compares constant numbers of any built-in numeric type, as the compiler folded them.</summary>
internal static class ConstantComparer
{
    /// <summary>
    /// Compares two constants. Returns <see langword="null"/> when either one is missing, is not
    /// a number, or is NaN.
    /// </summary>
    public static int? Compare(Optional<object?> first, Optional<object?> second)
    {
        if (!first.HasValue || !second.HasValue || first.Value is null || second.Value is null)
        {
            return null;
        }

        var a = first.Value;
        var b = second.Value;
        if (IsFloatingPoint(a) || IsFloatingPoint(b))
        {
            if (!IsNumber(a) || !IsNumber(b))
            {
                return null;
            }

            var x = Convert.ToDouble(a, CultureInfo.InvariantCulture);
            var y = Convert.ToDouble(b, CultureInfo.InvariantCulture);
            return double.IsNaN(x) || double.IsNaN(y) ? null : x.CompareTo(y);
        }

        // Every integer type and decimal fit in decimal without loss.
        return IsNumber(a) && IsNumber(b)
            ? Convert.ToDecimal(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(b, CultureInfo.InvariantCulture))
            : null;
    }

    /// <summary>Returns <see langword="true"/> for a constant zero of any numeric type.</summary>
    public static bool IsZero(object value) => Compare(new Optional<object?>(value), new Optional<object?>(0)) == 0;

    /// <summary>Returns <see langword="true"/> for a negative constant of any numeric type.</summary>
    public static bool IsNegative(object? value) => value is not null && Compare(new Optional<object?>(value), new Optional<object?>(0)) < 0;

    /// <summary>Returns <see langword="true"/> for a float or double NaN constant.</summary>
    public static bool IsNaN(object? value) =>
        value switch
        {
            double d => double.IsNaN(d),
            float f => float.IsNaN(f),
            _ => false,
        };

    private static bool IsFloatingPoint(object value) => value is double or float;

    private static bool IsNumber(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
