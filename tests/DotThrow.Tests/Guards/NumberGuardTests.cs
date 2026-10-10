using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System;
#endif

namespace DotThrow.Tests.Guards;

// Case tables for the number checks added in v2. NumberTests holds the detailed edge cases.
public sealed class NumberGuardTests
{
    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(1, (g, m) => g.IfOutOfRangeExclusive(1, 10, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1 and 10 (exclusive).");
        yield return Fail(10.0, (g, m) => g.IfOutOfRangeExclusive(1.0, 10.0, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1 and 10 (exclusive).");
        yield return Null<string>((g, m) => g.IfOutOfRangeExclusive("a", "c", m));
        yield return Fail(double.NaN, (g, m) => g.IfNotFinite(m), ErrorCodes.NotFinite, Expect.OutOfRange, "'input' must be a finite number.");
        yield return Fail(float.PositiveInfinity, (g, m) => g.IfNotFinite(m), ErrorCodes.NotFinite, Expect.OutOfRange, "'input' must be a finite number.");
        yield return Fail(3, (g, m) => g.IfOdd(m), ErrorCodes.Odd, Expect.OutOfRange, "'input' must be an even number.");
        yield return Fail(-3L, (g, m) => g.IfOdd(m), ErrorCodes.Odd, Expect.OutOfRange, "'input' must be an even number.");
        yield return Fail(4, (g, m) => g.IfEven(m), ErrorCodes.Even, Expect.OutOfRange, "'input' must be an odd number.");
        yield return Fail(0L, (g, m) => g.IfEven(m), ErrorCodes.Even, Expect.OutOfRange, "'input' must be an odd number.");
        yield return Fail(10, (g, m) => g.IfNotMultipleOf(3, m), ErrorCodes.NotMultipleOf, Expect.OutOfRange, "'input' must be a multiple of 3.");
        yield return Fail(10L, (g, m) => g.IfNotMultipleOf(4L, m), ErrorCodes.NotMultipleOf, Expect.OutOfRange, "'input' must be a multiple of 4.");
        yield return Fail(1.25m, (g, m) => g.IfNotMultipleOf(0.5m, m), ErrorCodes.NotMultipleOf, Expect.OutOfRange, "'input' must be a multiple of 0.5.");
        yield return Fail(6, (g, m) => g.IfNotPowerOfTwo(m), ErrorCodes.NotPowerOfTwo, Expect.OutOfRange, "'input' must be a power of two.");
        yield return Fail(0, (g, m) => g.IfNotPowerOfTwo(m), ErrorCodes.NotPowerOfTwo, Expect.OutOfRange, "'input' must be a power of two.");
        yield return Fail(-8L, (g, m) => g.IfNotPowerOfTwo(m), ErrorCodes.NotPowerOfTwo, Expect.OutOfRange, "'input' must be a power of two.");
        yield return Fail(1.5, (g, m) => g.IfNotInteger(m), ErrorCodes.NotInteger, Expect.OutOfRange, "'input' must be a whole number.");
        yield return Fail(double.NaN, (g, m) => g.IfNotInteger(m), ErrorCodes.NotInteger, Expect.OutOfRange, "'input' must be a whole number.");
        yield return Fail(-0.5f, (g, m) => g.IfNotInteger(m), ErrorCodes.NotInteger, Expect.OutOfRange, "'input' must be a whole number.");
        yield return Fail(2.001m, (g, m) => g.IfNotInteger(m), ErrorCodes.NotInteger, Expect.OutOfRange, "'input' must be a whole number.");
        yield return Fail(123.456m, (g, m) => g.IfPrecisionScaleExceeded(5, 2, message: m), ErrorCodes.PrecisionScaleExceeded, Expect.OutOfRange, "'input' must have at most 5 digits in total, with at most 2 decimals. Found 3 digits before the decimal point and 3 decimals.");
        yield return Fail(1234.5m, (g, m) => g.IfPrecisionScaleExceeded(4, 1, message: m), ErrorCodes.PrecisionScaleExceeded, Expect.OutOfRange, "'input' must have at most 4 digits in total, with at most 1 decimals. Found 4 digits before the decimal point and 1 decimals.");

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        // Generic math overloads (.NET 8 and later builds of the library).
        yield return Fail(Half.NaN, (g, m) => g.IfNaN(m), ErrorCodes.NaN, Expect.OutOfRange, "'input' must be a number, not NaN.");
        yield return Fail(Half.NegativeInfinity, (g, m) => g.IfInfinity(m), ErrorCodes.Infinity, Expect.OutOfRange, "'input' must be a finite number.");
        yield return Fail(Half.PositiveInfinity, (g, m) => g.IfNotFinite(m), ErrorCodes.NotFinite, Expect.OutOfRange, "'input' must be a finite number.");
        yield return Fail((short)3, (g, m) => g.IfOdd(m), ErrorCodes.Odd, Expect.OutOfRange, "'input' must be an even number.");
        yield return Fail((byte)4, (g, m) => g.IfEven(m), ErrorCodes.Even, Expect.OutOfRange, "'input' must be an odd number.");
        yield return Fail((short)10, (g, m) => g.IfNotMultipleOf((short)3, m), ErrorCodes.NotMultipleOf, Expect.OutOfRange, "'input' must be a multiple of 3.");
        yield return Fail((ushort)6, (g, m) => g.IfNotPowerOfTwo(m), ErrorCodes.NotPowerOfTwo, Expect.OutOfRange, "'input' must be a power of two.");
        yield return Fail(Int128.MaxValue, (g, m) => g.IfOdd(m), ErrorCodes.Odd, Expect.OutOfRange, "'input' must be an even number.");
#endif
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(2, (g, m) => g.IfOutOfRangeExclusive(1, 10, m));
        yield return Pass(9.999, (g, m) => g.IfOutOfRangeExclusive(1.0, 10.0, m));
        yield return Pass(double.MaxValue, (g, m) => g.IfNotFinite(m));
        yield return Pass(-0f, (g, m) => g.IfNotFinite(m));
        yield return Pass(4, (g, m) => g.IfOdd(m));
        yield return Pass(0L, (g, m) => g.IfOdd(m));
        yield return Pass(int.MaxValue, (g, m) => g.IfEven(m));
        yield return Pass(-1L, (g, m) => g.IfEven(m));
        yield return Pass(12, (g, m) => g.IfNotMultipleOf(3, m));
        yield return Pass(0, (g, m) => g.IfNotMultipleOf(7, m));
        yield return Pass(long.MinValue, (g, m) => g.IfNotMultipleOf(-1L, m));
        yield return Pass(1.5m, (g, m) => g.IfNotMultipleOf(0.5m, m));
        yield return Pass(1, (g, m) => g.IfNotPowerOfTwo(m));
        yield return Pass(1 << 30, (g, m) => g.IfNotPowerOfTwo(m));
        yield return Pass(1L << 62, (g, m) => g.IfNotPowerOfTwo(m));
        yield return Pass(-0.0, (g, m) => g.IfNotInteger(m));
        yield return Pass(1e15, (g, m) => g.IfNotInteger(m));
        yield return Pass(2f, (g, m) => g.IfNotInteger(m));
        yield return Pass(2.000m, (g, m) => g.IfNotInteger(m));
        yield return Pass(123.45m, (g, m) => g.IfPrecisionScaleExceeded(5, 2, message: m));
        yield return Pass(1.500m, (g, m) => g.IfPrecisionScaleExceeded(2, 1, ignoreTrailingZeros: true, message: m));
        yield return Pass(0m, (g, m) => g.IfPrecisionScaleExceeded(1, 0, message: m));

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        yield return Pass((Half)1, (g, m) => g.IfNaN(m));
        yield return Pass(Half.MaxValue, (g, m) => g.IfInfinity(m));
        yield return Pass(Half.MinValue, (g, m) => g.IfNotFinite(m));
        yield return Pass((short)4, (g, m) => g.IfOdd(m));
        yield return Pass((byte)5, (g, m) => g.IfEven(m));
        yield return Pass((short)9, (g, m) => g.IfNotMultipleOf((short)3, m));
        yield return Pass((ushort)64, (g, m) => g.IfNotPowerOfTwo(m));
#endif
    }

    public static TheoryData<CheckCase> Failures => Data(FailureCases());

    public static TheoryData<CheckCase> Passes => Data(PassCases());

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_throws_the_default_exception(CheckCase check) => check.AssertThrows();

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_uses_a_custom_message(CheckCase check) => check.AssertCustomMessage();

    [Theory]
    [MemberData(nameof(Passes))]
    public void Passing_check_returns_the_value(CheckCase check) => check.AssertPasses();
}
