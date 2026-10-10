using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests.Guards;

public sealed class ValueGuardTests
{
    [Flags]
    private enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write,
        Delete = 4,
    }

    private enum Color
    {
        Red,
        Green,
        Blue,
    }

    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(true, (g, m) => g.IfTrue(m), ErrorCodes.True, Expect.Argument, "'input' must be false.");
        yield return Fail(false, (g, m) => g.IfFalse(m), ErrorCodes.False, Expect.Argument, "'input' must be true.");
        yield return Fail(Guid.Empty, (g, m) => g.IfEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be an empty GUID.");
        yield return Fail((Color)42, (g, m) => g.IfNotDefined(m), ErrorCodes.NotDefined, Expect.OutOfRange, "'input' must be a defined Color value.");
        yield return Fail((Color)(-1), (g, m) => g.IfNotDefined(m), ErrorCodes.NotDefined, Expect.OutOfRange, "'input' must be a defined Color value.");
        yield return Fail((Access)8, (g, m) => g.IfNotDefined(m), ErrorCodes.NotDefined, Expect.OutOfRange, "'input' must be a defined Access value.");
        yield return Fail(Access.ReadWrite, (g, m) => g.IfHasFlag(Access.Write, m), ErrorCodes.HasFlag, Expect.OutOfRange, "'input' must not have the flag Write.");
        yield return Fail(Access.Read, (g, m) => g.IfNotHasFlag(Access.Delete, m), ErrorCodes.NotHasFlag, Expect.OutOfRange, "'input' must have the flag Delete.");
        yield return Fail(Access.Read, (g, m) => g.IfHasFlag(Access.None, m), ErrorCodes.HasFlag, Expect.OutOfRange, "'input' must not have the flag None.");
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(false, (g, m) => g.IfTrue(m));
        yield return Pass(true, (g, m) => g.IfFalse(m));
        yield return Pass(new Guid("6f9619ff-8b86-d011-b42d-00cf4fc964ff"), (g, m) => g.IfEmpty(m));
        yield return Pass(Color.Red, (g, m) => g.IfNotDefined(m));
        yield return Pass(Color.Blue, (g, m) => g.IfNotDefined(m));
        yield return Pass(Access.None, (g, m) => g.IfNotDefined(m));
        yield return Pass(Access.Read | Access.Write, (g, m) => g.IfNotDefined(m));
        yield return Pass(Access.Delete, (g, m) => g.IfNotDefined(m));
        yield return Pass(Access.Read, (g, m) => g.IfHasFlag(Access.Write, m));
        yield return Pass(Access.ReadWrite, (g, m) => g.IfNotHasFlag(Access.Read, m));

        // Any combination of defined flags passes (FluentValidation IsInEnum).
        yield return Pass(Access.Read | Access.Delete, (g, m) => g.IfNotDefined(m));
        yield return Pass(Access.Read | Access.Write | Access.Delete, (g, m) => g.IfNotDefined(m));
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
    public void IfNotDefined_carries_the_enum_value()
    {
        var color = (Color)42;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => color.Throw().IfNotDefined());

        Assert.Equal("color", exception.ParamName);
        Assert.Equal((Color)42, exception.ActualValue);
    }

    [Fact]
    public void Bool_checks_chain()
    {
        var accepted = true;

        bool result = accepted.Throw().IfFalse().IfDefault();

        Assert.True(result);
        Assert.Throws<ArgumentException>(() => accepted.Throw().IfFalse().IfTrue());
    }

    [Fact]
    public void Enum_values_are_formatted_by_name()
    {
        var exception = Assert.Throws<ArgumentException>(() => Color.Red.Throw("color").IfEqualTo(Color.Red));

        Assert.StartsWith("'color' must not be equal to Red.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Guid_values_are_formatted_in_the_default_format()
    {
        var id = new Guid("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

        var exception = Assert.Throws<ArgumentException>(() => id.Throw("id").IfEqualTo(id));

        Assert.StartsWith("'id' must not be equal to 6f9619ff-8b86-d011-b42d-00cf4fc964ff.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_without_a_format_use_ToString_and_null_text_becomes_empty()
    {
        var empty = Assert.Throws<ArgumentException>(() => new Nameless(1).Throw("item").IfNotEqualTo(new Nameless(2)));
        var text = Assert.Throws<ArgumentException>(() => new Tagged(1).Throw("item").IfNotEqualTo(new Tagged(2)));

        Assert.StartsWith("'item' must be equal to .", empty.Message, StringComparison.Ordinal);
        Assert.StartsWith("'item' must be equal to tag 2.", text.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_type_limits_of_comparisons_use_ToString()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Rank(5).Throw("rank").IfGreaterThan(new Rank(3)));

        Assert.StartsWith("'rank' must be less than or equal to rank 3.", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.ActualValue);
    }

    private readonly struct Nameless(int id) : IEquatable<Nameless>
    {
        public int Id { get; } = id;

        public bool Equals(Nameless other) => Id == other.Id;

        public override bool Equals(object? obj) => obj is Nameless other && Equals(other);

        public override int GetHashCode() => Id;

        public override string? ToString() => null;
    }

    private readonly struct Tagged(int id) : IEquatable<Tagged>
    {
        public int Id { get; } = id;

        public bool Equals(Tagged other) => Id == other.Id;

        public override bool Equals(object? obj) => obj is Tagged other && Equals(other);

        public override int GetHashCode() => Id;

        public override string ToString() => "tag " + Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class Rank(int value) : IComparable<Rank>
    {
        public int Value { get; } = value;

        public int CompareTo(Rank? other) => other is null ? 1 : Value.CompareTo(other.Value);

        public override string ToString() => "rank " + Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
