#if NET5_0_OR_GREATER
using System;
#endif

namespace DotThrow.Internal;

// Digits of a decimal, for IfPrecisionScaleExceeded. Same numbers as FluentValidation's
// PrecisionScaleValidator: precision is the number of significant digits of the unscaled
// integer, scale is the number of decimals, and zero has precision scale + 1.
internal static class Decimals
{
    private static readonly decimal[] PowersOfTen = CreatePowersOfTen();

    public static void Measure(decimal value, bool ignoreTrailingZeros, out int precision, out int scale)
    {
#if NET5_0_OR_GREATER
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
#else
        var bits = decimal.GetBits(value);
#endif
        var rawScale = (bits[3] >> 16) & 0x1F;
        var mantissa = new decimal(bits[0], bits[1], bits[2], isNegative: false, scale: 0);

        var trailingZeros = 0;
        if (ignoreTrailingZeros)
        {
            var rest = mantissa;
            while (trailingZeros < rawScale && rest % 10m == 0m)
            {
                rest /= 10m;
                trailingZeros++;
            }
        }

        scale = rawScale - trailingZeros;
        precision = mantissa == 0m ? scale + 1 : DigitCount(mantissa) - trailingZeros;
    }

    private static int DigitCount(decimal integer)
    {
        var digits = 1;
        while (digits < PowersOfTen.Length && integer >= PowersOfTen[digits])
        {
            digits++;
        }

        return digits;
    }

    private static decimal[] CreatePowersOfTen()
    {
        var powers = new decimal[29];
        powers[0] = 1m;
        for (var i = 1; i < powers.Length; i++)
        {
            powers[i] = powers[i - 1] * 10m;
        }

        return powers;
    }
}
