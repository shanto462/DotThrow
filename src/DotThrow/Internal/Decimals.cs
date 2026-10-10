#if NET5_0_OR_GREATER
using System;
#endif

namespace DotThrow.Internal;

// Digits of a decimal, for IfPrecisionScaleExceeded, with the same numbers as
// FluentValidation 12's PrecisionScaleValidator: scale is the number of decimals as stored
// (trailing zeros count unless ignoreTrailingZeros), precision is the number of digits of the
// unscaled integer (0 for zero), and the integer digits are precision - scale.
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

        // FluentValidation 12: precision is the number of digits of the unscaled integer, so
        // zero has precision 0 (0.00m has scale 2 and no integer digits, and passes (2, 2)).
        precision = mantissa == 0m ? 0 : DigitCount(mantissa) - trailingZeros;
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
