using System;
using DotThrow.Tests.Support;
using Xunit;
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System.Runtime.InteropServices;
#endif

namespace DotThrow.Tests.Guards;

public sealed class NumberTests
{
    [Fact]
    public void IfOutOfRangeExclusive_rejects_the_ends()
    {
        Check.Passes(5, g => g.IfOutOfRangeExclusive(1, 10));
        Check.Fails<ArgumentOutOfRangeException, int>(1, g => g.IfOutOfRangeExclusive(1, 10), ErrorCodes.OutOfRangeExclusive, "'input' must be between 1 and 10 (exclusive).");
        Check.Fails<ArgumentOutOfRangeException, int>(10, g => g.IfOutOfRangeExclusive(1, 10), ErrorCodes.OutOfRangeExclusive);
        Check.Fails<ArgumentOutOfRangeException, double>(double.NaN, g => g.IfOutOfRangeExclusive(1.0, 10.0), ErrorCodes.OutOfRangeExclusive);
        Check.Fails<ArgumentOutOfRangeException, int>(3, g => g.IfOutOfRangeExclusive(3, 3), ErrorCodes.OutOfRangeExclusive);
        Check.Passes("b", g => g.IfOutOfRangeExclusive("a", "c"));
        Check.Fails<ArgumentNullException, string>(null!, g => g.IfOutOfRangeExclusive("a", "c"), ErrorCodes.Null);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IfNotFinite_rejects_NaN_and_infinity(double value)
    {
        Check.Fails<ArgumentOutOfRangeException, double>(value, g => g.IfNotFinite(), ErrorCodes.NotFinite, "'input' must be a finite number.");
        Check.Fails<ArgumentOutOfRangeException, float>((float)value, g => g.IfNotFinite(), ErrorCodes.NotFinite);
    }

    [Fact]
    public void IfNotFinite_passes_finite_values() =>
        Check.Passes(double.MaxValue, g => g.IfNotFinite().IfNaN().IfInfinity());

    [Theory]
    [InlineData(3, true)]
    [InlineData(-3, true)]
    [InlineData(0, false)]
    [InlineData(-4, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MaxValue, true)]
    public void IfOdd_and_IfEven(int value, bool odd)
    {
        if (odd)
        {
            Check.Fails<ArgumentOutOfRangeException, int>(value, g => g.IfOdd(), ErrorCodes.Odd, "'input' must be an even number.");
            Check.Passes(value, g => g.IfEven());
            Check.Fails<ArgumentOutOfRangeException, long>(value, g => g.IfOdd(), ErrorCodes.Odd);
        }
        else
        {
            Check.Fails<ArgumentOutOfRangeException, int>(value, g => g.IfEven(), ErrorCodes.Even, "'input' must be an odd number.");
            Check.Passes(value, g => g.IfOdd());
            Check.Fails<ArgumentOutOfRangeException, long>(value, g => g.IfEven(), ErrorCodes.Even);
        }
    }

    [Theory]
    [InlineData(10, 5, true)]
    [InlineData(10, -5, true)]
    [InlineData(0, 7, true)]
    [InlineData(int.MinValue, -1, true)]
    [InlineData(int.MinValue, 2, true)]
    [InlineData(10, 3, false)]
    [InlineData(-10, 3, false)]
    public void IfNotMultipleOf_int_and_long(int value, int divisor, bool multiple)
    {
        if (multiple)
        {
            Check.Passes(value, g => g.IfNotMultipleOf(divisor));
            Check.Passes((long)value, g => g.IfNotMultipleOf((long)divisor));
        }
        else
        {
            var failure = Check.Fails<ArgumentOutOfRangeException, int>(value, g => g.IfNotMultipleOf(divisor), ErrorCodes.NotMultipleOf);
            Assert.Equal($"'input' must be a multiple of {divisor}.", failure.Message);
            Check.Fails<ArgumentOutOfRangeException, long>(value, g => g.IfNotMultipleOf((long)divisor), ErrorCodes.NotMultipleOf);
        }
    }

    [Fact]
    public void IfNotMultipleOf_long_minimum_and_decimal_steps()
    {
        Check.Passes(long.MinValue, g => g.IfNotMultipleOf(-1L));
        Check.Passes(1.25m, g => g.IfNotMultipleOf(0.05m));
        Check.Fails<ArgumentOutOfRangeException, decimal>(1.26m, g => g.IfNotMultipleOf(0.05m), ErrorCodes.NotMultipleOf, "'input' must be a multiple of 0.05.");
    }

    [Fact]
    public void A_zero_divisor_throws_even_in_collect_mode()
    {
        var report = new ValidationReport();
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(4).IfNotMultipleOf(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(4L).IfNotMultipleOf(0L));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(4m).IfNotMultipleOf(0m));
        Assert.Empty(report.Failures);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(1024, true)]
    [InlineData(1 << 30, true)]
    [InlineData(0, false)]
    [InlineData(-2, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(6, false)]
    public void IfNotPowerOfTwo(int value, bool power)
    {
        if (power)
        {
            Check.Passes(value, g => g.IfNotPowerOfTwo());
            Check.Passes((long)value, g => g.IfNotPowerOfTwo());
        }
        else
        {
            Check.Fails<ArgumentOutOfRangeException, int>(value, g => g.IfNotPowerOfTwo(), ErrorCodes.NotPowerOfTwo, "'input' must be a power of two.");
            Check.Fails<ArgumentOutOfRangeException, long>(value, g => g.IfNotPowerOfTwo(), ErrorCodes.NotPowerOfTwo);
        }
    }

    [Theory]
    [InlineData(2.0, true)]
    [InlineData(-0.0, true)]
    [InlineData(1e300, true)]
    [InlineData(2.5, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.Epsilon, false)]
    public void IfNotInteger_double_and_float(double value, bool whole)
    {
        if (whole)
        {
            Check.Passes(value, g => g.IfNotInteger());
            if (Math.Abs(value) <= float.MaxValue)
            {
                Check.Passes((float)value, g => g.IfNotInteger());
            }
        }
        else
        {
            Check.Fails<ArgumentOutOfRangeException, double>(value, g => g.IfNotInteger(), ErrorCodes.NotInteger, "'input' must be a whole number.");
            if (value != double.Epsilon)
            {
                Check.Fails<ArgumentOutOfRangeException, float>((float)value, g => g.IfNotInteger(), ErrorCodes.NotInteger);
            }
        }
    }

    [Fact]
    public void IfNotInteger_decimal()
    {
        Check.Passes(2.00m, g => g.IfNotInteger());
        Check.Passes(decimal.MaxValue, g => g.IfNotInteger());
        Check.Fails<ArgumentOutOfRangeException, decimal>(0.0000001m, g => g.IfNotInteger(), ErrorCodes.NotInteger);
    }

    // Cases from FluentValidation's ScalePrecisionValidatorTests.
    [Theory]
    [InlineData("123.45", 5, 2, false, true)]
    [InlineData("-123.45", 5, 2, false, true)]
    [InlineData("1234.5", 5, 2, false, false)]
    [InlineData("123.456", 5, 2, false, false)]
    [InlineData("0.001", 4, 3, false, true)]
    [InlineData("0.0001", 4, 3, false, false)]
    [InlineData("1.00", 3, 1, false, false)]
    [InlineData("1.00", 3, 1, true, true)]
    [InlineData("0", 1, 0, false, true)]
    [InlineData("0.00", 2, 1, false, false)]
    [InlineData("0.00", 2, 1, true, true)]
    [InlineData("12", 2, 0, false, true)]
    [InlineData("123", 2, 0, false, false)]
    [InlineData("79228162514264337593543950335", 29, 0, false, true)]
    public void IfPrecisionScaleExceeded_matches_FluentValidation(string text, int precision, int scale, bool ignoreTrailingZeros, bool valid)
    {
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        if (valid)
        {
            Check.Passes(value, g => g.IfPrecisionScaleExceeded(precision, scale, ignoreTrailingZeros));
        }
        else
        {
            Check.Fails<ArgumentOutOfRangeException, decimal>(value, g => g.IfPrecisionScaleExceeded(precision, scale, ignoreTrailingZeros), ErrorCodes.PrecisionScaleExceeded);
        }
    }

    [Fact]
    public void IfPrecisionScaleExceeded_message_shows_the_digits()
    {
        Assert.Equal(
            "'input' must have at most 5 digits in total, with at most 2 decimals. Found 4 digits before the decimal point and 3 decimals.",
            Check.MessageOf(1234.567m, g => g.IfPrecisionScaleExceeded(5, 2)));
        Assert.Equal(
            "p=4 s=2 d=0 a=4",
            Check.MessageOf(0.0001m, g => g.IfPrecisionScaleExceeded(4, 2, message: "p={ExpectedPrecision} s={ExpectedScale} d={Digits} a={ActualScale}")));
    }

    [Fact]
    public void IfPrecisionScaleExceeded_validates_its_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => 1m.Throw().IfPrecisionScaleExceeded(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => 1m.Throw().IfPrecisionScaleExceeded(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => 1m.Throw().IfPrecisionScaleExceeded(2, -1));
    }

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    [Fact]
    public void Generic_math_overloads_cover_other_number_types()
    {
        Check.Fails<ArgumentOutOfRangeException, Half>(Half.NaN, g => g.IfNaN(), ErrorCodes.NaN);
        Check.Fails<ArgumentOutOfRangeException, Half>(Half.PositiveInfinity, g => g.IfInfinity(), ErrorCodes.Infinity);
        Check.Fails<ArgumentOutOfRangeException, NFloat>(NFloat.NaN, g => g.IfNotFinite(), ErrorCodes.NotFinite);
        Check.Passes((Half)1.5f, g => g.IfNotFinite().IfNaN().IfInfinity());
        Check.Fails<ArgumentOutOfRangeException, short>((short)3, g => g.IfOdd(), ErrorCodes.Odd);
        Check.Fails<ArgumentOutOfRangeException, UInt128>(UInt128.MaxValue, g => g.IfOdd(), ErrorCodes.Odd);
        Check.Fails<ArgumentOutOfRangeException, byte>((byte)4, g => g.IfEven(), ErrorCodes.Even);
        Check.Passes((short)short.MinValue, g => g.IfNotMultipleOf((short)-1));
        Check.Passes((uint)12, g => g.IfNotMultipleOf(4u));
        Check.Fails<ArgumentOutOfRangeException, uint>(13u, g => g.IfNotMultipleOf(uint.MaxValue), ErrorCodes.NotMultipleOf);
        Check.Passes(Int128.One << 100, g => g.IfNotPowerOfTwo());
        Check.Fails<ArgumentOutOfRangeException, sbyte>((sbyte)-8, g => g.IfNotPowerOfTwo(), ErrorCodes.NotPowerOfTwo);
        Assert.Throws<ArgumentOutOfRangeException>(() => ((short)4).Throw().IfNotMultipleOf((short)0));
    }

    [Fact]
    public void Comparable_types_work_with_the_ordering_checks()
    {
        Check.Passes(new Version(1, 2), g => g.IfLessThan(new Version(1, 0)).IfGreaterThan(new Version(2, 0)));
        Check.Passes(System.Numerics.BigInteger.Pow(10, 40), g => g.IfNegativeOrZero());
        Check.Passes(Int128.MaxValue, g => g.IfNegative());
        Check.Passes(UInt128.One, g => g.IfZero());
        Check.Fails<ArgumentOutOfRangeException, Half>(Half.NaN, g => g.IfLessThan((Half)0), ErrorCodes.LessThan);
        Check.Passes('m', g => g.IfOutOfRange('a', 'z'));
        Check.Passes(TimeSpan.FromMinutes(5), g => g.IfNegative().IfGreaterThan(TimeSpan.FromHours(1)));
        Check.Passes(new DateOnly(2026, 1, 1), g => g.IfLessThan(new DateOnly(2025, 1, 1)));
        Check.Passes(new TimeOnly(12, 0), g => g.IfOutOfRange(new TimeOnly(9, 0), new TimeOnly(17, 0)));
        Check.Fails<ArgumentOutOfRangeException, NFloat>(NFloat.NaN, g => g.IfGreaterThan((NFloat)1), ErrorCodes.GreaterThan);
    }
#endif
}
