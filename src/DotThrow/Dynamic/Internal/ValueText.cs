using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Numerics;

namespace DotThrow.Dynamic.Internal;

// Converts argument text to typed values with the invariant culture. A static type switch
// (no reflection), so it works the same with Native AOT.
internal static class ValueText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // A parser for TValue, or null when the type has no built-in text form.
    public static Func<string, TValue>? Parser<TValue>()
    {
        var type = typeof(TValue);
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return NullableParser<TValue>(underlying);
        }

        if (type.IsEnum)
        {
            return text => (TValue)Enum.Parse(type, text.Trim(), ignoreCase: true);
        }

        return (Func<string, TValue>?)(object?)Scalar(type);
    }

    // Converts text to a boxed value of type (used for dictionary keys and nullable values).
    public static object? ParseObject(Type type, string text)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            type = underlying;
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, text.Trim(), ignoreCase: true);
        }

        return Scalar(type) switch
        {
            Func<string, string> p => p(text),
            Func<string, bool> p => p(text),
            Func<string, char> p => p(text),
            Func<string, byte> p => p(text),
            Func<string, sbyte> p => p(text),
            Func<string, short> p => p(text),
            Func<string, ushort> p => p(text),
            Func<string, int> p => p(text),
            Func<string, uint> p => p(text),
            Func<string, long> p => p(text),
            Func<string, ulong> p => p(text),
            Func<string, float> p => p(text),
            Func<string, double> p => p(text),
            Func<string, decimal> p => p(text),
            Func<string, DateTime> p => p(text),
            Func<string, DateTimeOffset> p => p(text),
            Func<string, TimeSpan> p => p(text),
            Func<string, Guid> p => p(text),
            Func<string, Version> p => p(text),
            Func<string, Uri> p => p(text),
            Func<string, IPAddress> p => p(text),
            Func<string, BigInteger> p => p(text),
#if NET8_0_OR_GREATER
            Func<string, DateOnly> p => p(text),
            Func<string, TimeOnly> p => p(text),
            Func<string, Half> p => p(text),
            Func<string, Int128> p => p(text),
            Func<string, UInt128> p => p(text),
#endif
            _ => throw new FormatException($"A {type.Name} cannot be written as text."),
        };
    }

    public static bool HasParser(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        return target.IsEnum || Scalar(target) is not null;
    }

    private static Func<string, TValue>? NullableParser<TValue>(Type underlying)
    {
        if (underlying.IsEnum)
        {
            return text => (TValue)Enum.Parse(underlying, text.Trim(), ignoreCase: true);
        }

        // Box the underlying value; unboxing it as TValue (Nullable<U>) gives a value.
        return Scalar(underlying) switch
        {
            Func<string, bool> p => text => (TValue)(object)p(text),
            Func<string, char> p => text => (TValue)(object)p(text),
            Func<string, byte> p => text => (TValue)(object)p(text),
            Func<string, sbyte> p => text => (TValue)(object)p(text),
            Func<string, short> p => text => (TValue)(object)p(text),
            Func<string, ushort> p => text => (TValue)(object)p(text),
            Func<string, int> p => text => (TValue)(object)p(text),
            Func<string, uint> p => text => (TValue)(object)p(text),
            Func<string, long> p => text => (TValue)(object)p(text),
            Func<string, ulong> p => text => (TValue)(object)p(text),
            Func<string, float> p => text => (TValue)(object)p(text),
            Func<string, double> p => text => (TValue)(object)p(text),
            Func<string, decimal> p => text => (TValue)(object)p(text),
            Func<string, DateTime> p => text => (TValue)(object)p(text),
            Func<string, DateTimeOffset> p => text => (TValue)(object)p(text),
            Func<string, TimeSpan> p => text => (TValue)(object)p(text),
            Func<string, Guid> p => text => (TValue)(object)p(text),
            Func<string, BigInteger> p => text => (TValue)(object)p(text),
#if NET8_0_OR_GREATER
            Func<string, DateOnly> p => text => (TValue)(object)p(text),
            Func<string, TimeOnly> p => text => (TValue)(object)p(text),
            Func<string, Half> p => text => (TValue)(object)p(text),
            Func<string, Int128> p => text => (TValue)(object)p(text),
            Func<string, UInt128> p => text => (TValue)(object)p(text),
#endif
            _ => null,
        };
    }

    private static object? Scalar(Type type) => Scalars.TryGetValue(type, out var parser) ? parser : null;

    // The built-in text forms. Numbers allow a sign, decimals and exponents where the type
    // has them; dates use round-trip-friendly invariant parsing.
    private static readonly Dictionary<Type, object> Scalars = new()
    {
        [typeof(string)] = (Func<string, string>)(static text => text),
        [typeof(bool)] = (Func<string, bool>)(static text => bool.Parse(text.Trim())),
        [typeof(char)] = (Func<string, char>)(static text => text.Length == 1 ? text[0] : throw new FormatException("A char needs exactly one character.")),
        [typeof(byte)] = (Func<string, byte>)(static text => byte.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(sbyte)] = (Func<string, sbyte>)(static text => sbyte.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(short)] = (Func<string, short>)(static text => short.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(ushort)] = (Func<string, ushort>)(static text => ushort.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(int)] = (Func<string, int>)(static text => int.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(uint)] = (Func<string, uint>)(static text => uint.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(long)] = (Func<string, long>)(static text => long.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(ulong)] = (Func<string, ulong>)(static text => ulong.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(float)] = (Func<string, float>)(static text => float.Parse(text, NumberStyles.Float, Invariant)),
        [typeof(double)] = (Func<string, double>)(static text => double.Parse(text, NumberStyles.Float, Invariant)),
        [typeof(decimal)] = (Func<string, decimal>)(static text => decimal.Parse(text, NumberStyles.Float, Invariant)),
        [typeof(DateTime)] = (Func<string, DateTime>)(static text => DateTime.Parse(text, Invariant, DateTimeStyles.RoundtripKind)),
        [typeof(DateTimeOffset)] = (Func<string, DateTimeOffset>)(static text => DateTimeOffset.Parse(text, Invariant, DateTimeStyles.RoundtripKind)),
        [typeof(TimeSpan)] = (Func<string, TimeSpan>)(static text => TimeSpan.Parse(text, Invariant)),
        [typeof(Guid)] = (Func<string, Guid>)(static text => Guid.Parse(text)),
        [typeof(Version)] = (Func<string, Version>)(static text => Version.Parse(text)),
        [typeof(Uri)] = (Func<string, Uri>)(static text => new Uri(text, UriKind.RelativeOrAbsolute)),
        [typeof(IPAddress)] = (Func<string, IPAddress>)(static text => IPAddress.Parse(text)),
        [typeof(BigInteger)] = (Func<string, BigInteger>)(static text => BigInteger.Parse(text, NumberStyles.Integer, Invariant)),
#if NET8_0_OR_GREATER
        [typeof(DateOnly)] = (Func<string, DateOnly>)(static text => DateOnly.Parse(text, Invariant)),
        [typeof(TimeOnly)] = (Func<string, TimeOnly>)(static text => TimeOnly.Parse(text, Invariant)),
        [typeof(Half)] = (Func<string, Half>)(static text => Half.Parse(text, NumberStyles.Float, Invariant)),
        [typeof(Int128)] = (Func<string, Int128>)(static text => Int128.Parse(text, NumberStyles.Integer, Invariant)),
        [typeof(UInt128)] = (Func<string, UInt128>)(static text => UInt128.Parse(text, NumberStyles.Integer, Invariant)),
#endif
    };
}
