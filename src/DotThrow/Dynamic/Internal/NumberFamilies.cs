using System;
using System.Globalization;
#if NET8_0_OR_GREATER
using System.Numerics;
#endif

namespace DotThrow.Dynamic.Internal;

internal sealed class Int32Family() : StructComparableFamily<int>(ValueText.Parser<int>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<int>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.Odd => g => g.IfOdd(message),
            CheckNames.Even => g => g.IfEven(message),
            CheckNames.NotPowerOfTwo => g => g.IfNotPowerOfTwo(message),
            _ => null,
        };
        if (step is not null)
        {
            return true;
        }

        if (name == CheckNames.NotMultipleOf)
        {
            if (Divisor.Read(args, Parser, 0, out var divisor))
            {
                step = g => g.IfNotMultipleOf(divisor, message);
            }

            return true;
        }

        return false;
    }
}

internal sealed class Int64Family() : StructComparableFamily<long>(ValueText.Parser<long>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<long>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.Odd => g => g.IfOdd(message),
            CheckNames.Even => g => g.IfEven(message),
            CheckNames.NotPowerOfTwo => g => g.IfNotPowerOfTwo(message),
            _ => null,
        };
        if (step is not null)
        {
            return true;
        }

        if (name == CheckNames.NotMultipleOf)
        {
            if (Divisor.Read(args, Parser, 0L, out var divisor))
            {
                step = g => g.IfNotMultipleOf(divisor, message);
            }

            return true;
        }

        return false;
    }
}

internal sealed class DecimalFamily() : StructComparableFamily<decimal>(ValueText.Parser<decimal>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<decimal>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.NotInteger:
                step = g => g.IfNotInteger(message);
                return true;
            case CheckNames.NotMultipleOf:
                if (Divisor.Read(args, Parser, 0m, out var divisor))
                {
                    step = g => g.IfNotMultipleOf(divisor, message);
                }

                return true;
            case CheckNames.PrecisionScaleExceeded:
                var ignoreTrailingZeros = args.Flag("IgnoreTrailingZeros");
                if (args.TryParseList(static text => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture), out var numbers) && ignoreTrailingZeros is { } ignore)
                {
                    if (numbers.Length != 2 || numbers[0] < 1 || numbers[1] < 0 || numbers[1] > numbers[0])
                    {
                        args.Error("'Values' must be [precision, scale] with precision >= 1 and 0 <= scale <= precision");
                    }
                    else
                    {
                        var precision = numbers[0];
                        var scale = numbers[1];
                        step = g => g.IfPrecisionScaleExceeded(precision, scale, ignore, message);
                    }
                }

                return true;
            default:
                return false;
        }
    }
}

internal sealed class DoubleFamily() : StructComparableFamily<double>(ValueText.Parser<double>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<double>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.NaN => g => g.IfNaN(message),
            CheckNames.Infinity => g => g.IfInfinity(message),
            CheckNames.NotFinite => g => g.IfNotFinite(message),
            CheckNames.NotInteger => g => g.IfNotInteger(message),
            _ => null,
        };
        return step is not null;
    }
}

internal sealed class SingleFamily() : StructComparableFamily<float>(ValueText.Parser<float>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<float>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.NaN => g => g.IfNaN(message),
            CheckNames.Infinity => g => g.IfInfinity(message),
            CheckNames.NotFinite => g => g.IfNotFinite(message),
            CheckNames.NotInteger => g => g.IfNotInteger(message),
            _ => null,
        };
        return step is not null;
    }
}

#if NET8_0_OR_GREATER
// byte, sbyte, short, ushort, uint, ulong, Int128, UInt128, BigInteger: the generic math checks.
internal sealed class BinaryIntegerFamily<TValue>() : StructComparableFamily<TValue>(ValueText.Parser<TValue>())
    where TValue : struct, IBinaryInteger<TValue>
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.Odd => g => g.IfOdd(message),
            CheckNames.Even => g => g.IfEven(message),
            CheckNames.NotPowerOfTwo => g => g.IfNotPowerOfTwo(message),
            _ => null,
        };
        if (step is not null)
        {
            return true;
        }

        if (name == CheckNames.NotMultipleOf)
        {
            if (Divisor.Read(args, Parser, TValue.Zero, out var divisor))
            {
                step = g => g.IfNotMultipleOf(divisor, message);
            }

            return true;
        }

        return false;
    }
}

// Half (and other IEEE floating point types) on .NET 8+.
internal sealed class FloatingPointFamily<TValue>() : StructComparableFamily<TValue>(ValueText.Parser<TValue>())
    where TValue : struct, IFloatingPointIeee754<TValue>
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.NaN => g => g.IfNaN(message),
            CheckNames.Infinity => g => g.IfInfinity(message),
            CheckNames.NotFinite => g => g.IfNotFinite(message),
            _ => null,
        };
        return step is not null;
    }
}
#endif

internal static class Divisor
{
    // A zero divisor makes every value fail, so it is a definition error.
    public static bool Read<T>(CheckArgs args, Func<string, T>? parser, T zero, out T divisor)
    {
        if (!args.TryParse(Arg.Value, parser, out divisor))
        {
            return false;
        }

        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(divisor, zero))
        {
            args.Error("'Value' (the divisor) must not be zero");
            return false;
        }

        return true;
    }
}
