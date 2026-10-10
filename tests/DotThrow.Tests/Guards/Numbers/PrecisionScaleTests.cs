using System;
using System.Globalization;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>Review fix: IfPrecisionScaleExceeded matches FluentValidation 12 (zero has precision 0).</summary>
public sealed class PrecisionScaleTests
{
    // Expected results computed with FluentValidation 12's PrecisionScaleValidator algorithm.
    [Theory]
    [InlineData("0.00", 2, 2, false, true)]
    [InlineData("0.05", 2, 2, false, true)]
    [InlineData("0", 2, 2, false, true)]
    [InlineData("0.000", 2, 2, false, false)]
    [InlineData("0.000", 2, 2, true, true)]
    [InlineData("1.00", 2, 2, false, false)]
    [InlineData("0.99", 2, 2, false, true)]
    [InlineData("-0.05", 2, 2, false, true)]
    [InlineData("0", 1, 0, false, true)]
    [InlineData("0.0", 1, 0, false, false)]
    [InlineData("0.0", 1, 0, true, true)]
    [InlineData("123.45", 5, 2, false, true)]
    [InlineData("1234.5", 5, 2, false, false)]
    public void Results_match_FluentValidation(string text, int precision, int scale, bool ignoreTrailingZeros, bool valid)
    {
        var value = decimal.Parse(text, CultureInfo.InvariantCulture);
        if (valid)
        {
            Check.Passes(value, g => g.IfPrecisionScaleExceeded(precision, scale, ignoreTrailingZeros));
        }
        else
        {
            Check.Fails<ArgumentOutOfRangeException, decimal>(value, g => g.IfPrecisionScaleExceeded(precision, scale, ignoreTrailingZeros), ErrorCodes.PrecisionScaleExceeded);
        }
    }
}
