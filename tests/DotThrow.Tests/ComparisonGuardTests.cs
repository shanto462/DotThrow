using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests;

public sealed class ComparisonGuardTests
{
    public static IEnumerable<CheckCase> FailureCases()
    {
        // Greater / less, with the limit itself as the boundary.
        yield return Fail(11, (g, m) => g.IfGreaterThan(10, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 10.");
        yield return Fail(int.MaxValue, (g, m) => g.IfGreaterThan(int.MaxValue - 1, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 2147483646.");
        yield return Fail(10, (g, m) => g.IfGreaterThanOrEqualTo(10, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 10.");
        yield return Fail(9, (g, m) => g.IfLessThan(10, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 10.");
        yield return Fail(10, (g, m) => g.IfLessThanOrEqualTo(10, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 10.");
        yield return Fail(0.1m, (g, m) => g.IfLessThan(0.5m, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 0.5.");
        yield return Fail(1.5, (g, m) => g.IfGreaterThan(1.25, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 1.25.");
        yield return Fail(double.PositiveInfinity, (g, m) => g.IfGreaterThan(double.MaxValue, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 1.7976931348623157E+308.");
        yield return Fail(double.NegativeInfinity, (g, m) => g.IfLessThan(double.MinValue, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to -1.7976931348623157E+308.");
        yield return Fail(TimeSpan.FromMinutes(61), (g, m) => g.IfGreaterThan(TimeSpan.FromHours(1), m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 01:00:00.");
        yield return Fail("b", (g, m) => g.IfGreaterThan("a", m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 'a'.");
        yield return Fail(new Version(2, 0), (g, m) => g.IfGreaterThanOrEqualTo(new Version(2, 0), m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 2.0.");

        // NaN has no order, so every ordering check rejects it.
        yield return Fail(double.NaN, (g, m) => g.IfGreaterThan(10.0, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 10.");
        yield return Fail(double.NaN, (g, m) => g.IfGreaterThanOrEqualTo(10.0, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 10.");
        yield return Fail(double.NaN, (g, m) => g.IfLessThan(10.0, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 10.");
        yield return Fail(double.NaN, (g, m) => g.IfLessThanOrEqualTo(10.0, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 10.");
        yield return Fail(double.NaN, (g, m) => g.IfOutOfRange(0.0, 1.0, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 0 and 1.");
        yield return Fail(double.NaN, (g, m) => g.IfInRange(0.0, 1.0, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 0 and 1.");
        yield return Fail(double.NaN, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
        yield return Fail(double.NaN, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.");
        yield return Fail(double.NaN, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.");
        yield return Fail(double.NaN, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.");
        yield return Fail(float.NaN, (g, m) => g.IfLessThan(1f, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 1.");
        yield return Fail(float.NaN, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.");

        // Ranges include both ends.
        yield return Fail(0, (g, m) => g.IfOutOfRange(1, 10, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1 and 10.");
        yield return Fail(11, (g, m) => g.IfOutOfRange(1, 10, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1 and 10.");
        yield return Fail(1, (g, m) => g.IfInRange(1, 10, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1 and 10.");
        yield return Fail(10, (g, m) => g.IfInRange(1, 10, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1 and 10.");
        yield return Fail(5, (g, m) => g.IfInRange(5, 5, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 5 and 5.");
        yield return Fail(-0.01m, (g, m) => g.IfOutOfRange(0m, 100m, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 0 and 100.");

        // Sign checks.
        yield return Fail(-1, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
        yield return Fail(int.MinValue, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
        yield return Fail(-0.0001m, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
        yield return Fail(double.NegativeInfinity, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
        yield return Fail(TimeSpan.FromTicks(-1), (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
        yield return Fail(0, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.");
        yield return Fail(-0.0, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.");
        yield return Fail(TimeSpan.Zero, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.");
        yield return Fail(1, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.");
        yield return Fail(double.Epsilon, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.");
        yield return Fail(0L, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.");
        yield return Fail(-0.0, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.");
        yield return Fail(0, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.");
        yield return Fail(0.0m, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.");
        yield return Fail(-0.0, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.");
        yield return Fail(0f, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.");
        yield return Fail(TimeSpan.Zero, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.");

        // NaN and infinity checks.
        yield return Fail(double.NaN, (g, m) => g.IfNaN(m), ErrorCodes.NaN, Expect.OutOfRange, "'input' must be a number, not NaN.");
        yield return Fail(float.NaN, (g, m) => g.IfNaN(m), ErrorCodes.NaN, Expect.OutOfRange, "'input' must be a number, not NaN.");
        yield return Fail(double.PositiveInfinity, (g, m) => g.IfInfinity(m), ErrorCodes.Infinity, Expect.OutOfRange, "'input' must be a finite number.");
        yield return Fail(double.NegativeInfinity, (g, m) => g.IfInfinity(m), ErrorCodes.Infinity, Expect.OutOfRange, "'input' must be a finite number.");
        yield return Fail(float.PositiveInfinity, (g, m) => g.IfInfinity(m), ErrorCodes.Infinity, Expect.OutOfRange, "'input' must be a finite number.");
        yield return Fail(float.NegativeInfinity, (g, m) => g.IfInfinity(m), ErrorCodes.Infinity, Expect.OutOfRange, "'input' must be a finite number.");

        // A null value fails every comparison with the "Null" code.
        yield return Null<string>((g, m) => g.IfGreaterThan("a", m));
        yield return Null<string>((g, m) => g.IfGreaterThanOrEqualTo("a", m));
        yield return Null<string>((g, m) => g.IfLessThan("a", m));
        yield return Null<string>((g, m) => g.IfLessThanOrEqualTo("a", m));
        yield return Null<Version>((g, m) => g.IfOutOfRange(new Version(1, 0), new Version(2, 0), m));
        yield return Null<Version>((g, m) => g.IfInRange(new Version(1, 0), new Version(2, 0), m));

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        // Only the .NET 8 and later builds of the library know that Half has NaN.
        yield return Fail(Half.NaN, (g, m) => g.IfGreaterThan((Half)1, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 1.");
        yield return Fail(Half.NaN, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
#endif

#if NET8_0_OR_GREATER
        yield return Fail((Half)(-1), (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.");
        yield return Fail(Int128.MaxValue, (g, m) => g.IfGreaterThan(Int128.MaxValue - 1, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 170141183460469231731687303715884105726.");
#endif
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(10, (g, m) => g.IfGreaterThan(10, m));
        yield return Pass(int.MaxValue, (g, m) => g.IfGreaterThan(int.MaxValue, m));
        yield return Pass(9, (g, m) => g.IfGreaterThanOrEqualTo(10, m));
        yield return Pass(10, (g, m) => g.IfLessThan(10, m));
        yield return Pass(int.MinValue, (g, m) => g.IfLessThan(int.MinValue, m));
        yield return Pass(11, (g, m) => g.IfLessThanOrEqualTo(10, m));
        yield return Pass(0.5m, (g, m) => g.IfLessThan(0.5m, m));
        yield return Pass(double.PositiveInfinity, (g, m) => g.IfLessThan(double.MaxValue, m));
        yield return Pass(double.MaxValue, (g, m) => g.IfGreaterThan(double.PositiveInfinity, m));
        yield return Pass(TimeSpan.FromHours(1), (g, m) => g.IfGreaterThan(TimeSpan.FromHours(1), m));
        yield return Pass("a", (g, m) => g.IfGreaterThan("b", m));
        yield return Pass(new Version(1, 9), (g, m) => g.IfGreaterThanOrEqualTo(new Version(2, 0), m));
        yield return Pass(1, (g, m) => g.IfOutOfRange(1, 10, m));
        yield return Pass(10, (g, m) => g.IfOutOfRange(1, 10, m));
        yield return Pass(5, (g, m) => g.IfOutOfRange(5, 5, m));
        yield return Pass(0, (g, m) => g.IfInRange(1, 10, m));
        yield return Pass(11, (g, m) => g.IfInRange(1, 10, m));
        yield return Pass(0.0, (g, m) => g.IfOutOfRange(-0.0, 0.0, m));
        yield return Pass(0, (g, m) => g.IfNegative(m));
        yield return Pass(-0.0, (g, m) => g.IfNegative(m));
        yield return Pass(double.PositiveInfinity, (g, m) => g.IfNegative(m));
        yield return Pass(TimeSpan.Zero, (g, m) => g.IfNegative(m));
        yield return Pass(1, (g, m) => g.IfNegativeOrZero(m));
        yield return Pass(double.Epsilon, (g, m) => g.IfNegativeOrZero(m));
        yield return Pass(0, (g, m) => g.IfPositive(m));
        yield return Pass(-0.0, (g, m) => g.IfPositive(m));
        yield return Pass(-1L, (g, m) => g.IfPositiveOrZero(m));
        yield return Pass(1, (g, m) => g.IfZero(m));
        yield return Pass(double.NaN, (g, m) => g.IfZero(m));
        yield return Pass(float.NaN, (g, m) => g.IfZero(m));
        yield return Pass(double.Epsilon, (g, m) => g.IfZero(m));
        yield return Pass(0.0, (g, m) => g.IfNaN(m));
        yield return Pass(double.PositiveInfinity, (g, m) => g.IfNaN(m));
        yield return Pass(1f, (g, m) => g.IfNaN(m));
        yield return Pass(double.MaxValue, (g, m) => g.IfInfinity(m));
        yield return Pass(double.NaN, (g, m) => g.IfInfinity(m));
        yield return Pass(float.MinValue, (g, m) => g.IfInfinity(m));

#if NET8_0_OR_GREATER
        yield return Pass((Half)0.5, (g, m) => g.IfGreaterThan((Half)1, m));
        yield return Pass(Half.NaN, (g, m) => g.IfZero(m));
        yield return Pass(UInt128.Zero, (g, m) => g.IfNegative(m));
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

    [Fact]
    public void Range_checks_reject_min_greater_than_max()
    {
        var outOfRange = Assert.Throws<ArgumentOutOfRangeException>(() => 5.Throw().IfOutOfRange(10, 1));
        var inRange = Assert.Throws<ArgumentOutOfRangeException>(() => 5.Throw().IfInRange(10, 1));

        Assert.Equal("max", outOfRange.ParamName);
        Assert.Equal(1, outOfRange.ActualValue);
        Assert.Equal("max", inRange.ParamName);
    }

    [Fact]
    public void Range_checks_reject_min_greater_than_max_even_in_collect_mode()
    {
        var report = new ValidationReport();

        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(5, "n").IfOutOfRange(10, 1));

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Null_limits_throw_ArgumentNullException()
    {
        Assert.Equal("limit", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfGreaterThan(null!)).ParamName);
        Assert.Equal("limit", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfGreaterThanOrEqualTo(null!)).ParamName);
        Assert.Equal("limit", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfLessThan(null!)).ParamName);
        Assert.Equal("limit", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfLessThanOrEqualTo(null!)).ParamName);
        Assert.Equal("min", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfOutOfRange(null!, "b")).ParamName);
        Assert.Equal("max", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfInRange("a", null!)).ParamName);
    }

    [Fact]
    public void Negative_zero_is_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (-0.0).Throw().IfZero());
        Assert.Throws<ArgumentOutOfRangeException>(() => (-0.0f).Throw().IfNegativeOrZero());
        double value = (-0.0).Throw().IfNegative().IfPositive();

        Assert.Equal(0.0, value);
    }

    [Fact]
    public void Comparison_failure_carries_the_actual_value()
    {
        var age = 151;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => age.Throw().IfGreaterThan(150));

        Assert.Equal("age", exception.ParamName);
        Assert.Equal(151, exception.ActualValue);
        Assert.Contains("151", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void String_comparison_failure_hides_the_value()
    {
        var password = "hunter2-secret";

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => password.Throw().IfLessThan("zzz"));

        Assert.Null(exception.ActualValue);
        Assert.DoesNotContain("hunter2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Limits_are_formatted_with_the_invariant_culture()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => 1234.5m.Throw("price").IfGreaterThan(1000.25m));

        Assert.StartsWith("'price' must be less than or equal to 1000.25.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_comparable_types_work()
    {
        var small = new Size(1);
        var large = new Size(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => large.Throw().IfGreaterThan(small));
        Size passed = small.Throw().IfOutOfRange(small, large);

        Assert.Same(small, passed);
    }

    private sealed class Size(int value) : IComparable<Size>
    {
        public int Value { get; } = value;

        public int CompareTo(Size? other) => other is null ? 1 : Value.CompareTo(other.Value);

        public override string ToString() => $"Size {Value}";
    }
}
