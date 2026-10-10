using System;
using System.Collections.Generic;
using System.IO;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests;

public sealed class GeneralGuardTests
{
    public static IEnumerable<CheckCase> FailureCases()
    {
        object boxed = "Ada";
        object number = 42;

        yield return Fail<string?>(null, (g, m) => g.IfNull(m), ErrorCodes.Null, Expect.Null, "'input' must not be null.");
        yield return Fail<object?>(null, (g, m) => g.IfNull(m), ErrorCodes.Null, Expect.Null, "'input' must not be null.");
        yield return Fail("Ada", (g, m) => g.IfNotNull(m), ErrorCodes.NotNull, Expect.Argument, "'input' must be null.");
        yield return Fail<int?>(3, (g, m) => g.IfNotNull(m), ErrorCodes.NotNull, Expect.Argument, "'input' must be null.");
        yield return Fail(0, (g, m) => g.IfDefault(m), ErrorCodes.Default, Expect.Argument, "'input' must not be the default value.");
        yield return Fail(Guid.Empty, (g, m) => g.IfDefault(m), ErrorCodes.Default, Expect.Argument, "'input' must not be the default value.");
        yield return Fail(default(DateTime), (g, m) => g.IfDefault(m), ErrorCodes.Default, Expect.Argument, "'input' must not be the default value.");
        yield return Fail<string?>(null, (g, m) => g.IfDefault(m), ErrorCodes.Default, Expect.Argument, "'input' must not be the default value.");
        yield return Fail(5, (g, m) => g.IfEqualTo(5, message: m), ErrorCodes.EqualTo, Expect.Argument, "'input' must not be equal to 5.");
        yield return Fail(1.5, (g, m) => g.IfEqualTo(1.5, message: m), ErrorCodes.EqualTo, Expect.Argument, "'input' must not be equal to 1.5.");
        yield return Fail("Ada", (g, m) => g.IfEqualTo("Ada", message: m), ErrorCodes.EqualTo, Expect.Argument, "'input' must not be equal to 'Ada'.");
        yield return Fail("ADA", (g, m) => g.IfEqualTo("ada", StringComparer.OrdinalIgnoreCase, m), ErrorCodes.EqualTo, Expect.Argument, "'input' must not be equal to 'ada'.");
        yield return Fail<string?>(null, (g, m) => g.IfEqualTo(null, message: m), ErrorCodes.EqualTo, Expect.Argument, "'input' must not be equal to null.");
        yield return Fail(5, (g, m) => g.IfNotEqualTo(6, message: m), ErrorCodes.NotEqualTo, Expect.Argument, "'input' must be equal to 6.");
        yield return Fail("Ada", (g, m) => g.IfNotEqualTo("ada", message: m), ErrorCodes.NotEqualTo, Expect.Argument, "'input' must be equal to 'ada'.");
        yield return Fail("Ada", (g, m) => g.IfNotEqualTo("Bob", StringComparer.OrdinalIgnoreCase, m), ErrorCodes.NotEqualTo, Expect.Argument, "'input' must be equal to 'Bob'.");
        yield return Fail(2, (g, m) => g.IfOneOf([1, 2, 3], message: m), ErrorCodes.OneOf, Expect.Argument, "'input' must not be one of the rejected values.");
        yield return Fail("ADMIN", (g, m) => g.IfOneOf(["admin", "root"], StringComparer.OrdinalIgnoreCase, m), ErrorCodes.OneOf, Expect.Argument, "'input' must not be one of the rejected values.");
        yield return Fail<string?>(null, (g, m) => g.IfOneOf([null, "root"], message: m), ErrorCodes.OneOf, Expect.Argument, "'input' must not be one of the rejected values.");
        yield return Fail(4, (g, m) => g.IfNotOneOf([1, 2, 3], message: m), ErrorCodes.NotOneOf, Expect.Argument, "'input' must be one of the allowed values.");
        yield return Fail("Admin", (g, m) => g.IfNotOneOf(["admin", "user"], message: m), ErrorCodes.NotOneOf, Expect.Argument, "'input' must be one of the allowed values.");
        yield return Fail(7, (g, m) => g.If(v => v % 2 != 0, m), ErrorCodes.Condition, Expect.Argument, "'input' must not satisfy the condition: v => v % 2 != 0.");
        yield return Fail(8, (g, m) => g.IfNot(v => v % 2 != 0, m), ErrorCodes.Condition, Expect.Argument, "'input' must satisfy the condition: v => v % 2 != 0.");
        yield return Fail(8, (g, m) => g.If(IsEven, m), ErrorCodes.Condition, Expect.Argument, "'input' must not satisfy the condition: IsEven.");
        yield return Fail(8, (g, m) => g.If(IsEven, m, predicateExpression: null), ErrorCodes.Condition, Expect.Argument, "'input' is not valid.");
        yield return Fail(7, (g, m) => g.IfNot(IsEven, m, predicateExpression: ""), ErrorCodes.Condition, Expect.Argument, "'input' is not valid.");
        yield return Fail(boxed, (g, m) => g.IfType(typeof(string), m), ErrorCodes.Type, Expect.Argument, "'input' must not be of type String.");
        yield return Fail<Stream>(new MemoryStream(), (g, m) => g.IfType(typeof(Stream), m), ErrorCodes.Type, Expect.Argument, "'input' must not be of type Stream.");
        yield return Fail(number, (g, m) => g.IfNotType(typeof(string), m), ErrorCodes.NotType, Expect.Argument, "'input' must be of type String.");
        yield return Fail<object?>(null, (g, m) => g.IfNotType(typeof(object), m), ErrorCodes.NotType, Expect.Argument, "'input' must be of type Object.");
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        object boxed = "Ada";

        yield return Pass("Ada", (g, m) => g.IfNull(m));
        yield return Pass(0, (g, m) => g.IfNull(m));
        yield return Pass<string?>(null, (g, m) => g.IfNotNull(m));
        yield return Pass<int?>(null, (g, m) => g.IfNotNull(m));
        yield return Pass(1, (g, m) => g.IfDefault(m));
        yield return Pass("", (g, m) => g.IfDefault(m));
        yield return Pass(Guid.NewGuid(), (g, m) => g.IfDefault(m));
        yield return Pass(5, (g, m) => g.IfEqualTo(6, message: m));
        yield return Pass("Ada", (g, m) => g.IfEqualTo("ada", message: m));
        yield return Pass<string?>(null, (g, m) => g.IfEqualTo("Ada", message: m));
        yield return Pass(5, (g, m) => g.IfNotEqualTo(5, message: m));
        yield return Pass("ADA", (g, m) => g.IfNotEqualTo("ada", StringComparer.OrdinalIgnoreCase, m));
        yield return Pass<string?>(null, (g, m) => g.IfNotEqualTo(null, message: m));
        yield return Pass(4, (g, m) => g.IfOneOf([1, 2, 3], message: m));
        yield return Pass("ADMIN", (g, m) => g.IfOneOf(["admin", "root"], message: m));
        yield return Pass<string?>(null, (g, m) => g.IfOneOf(["admin"], message: m));
        yield return Pass(2, (g, m) => g.IfNotOneOf([1, 2, 3], message: m));
        yield return Pass("ADMIN", (g, m) => g.IfNotOneOf(["admin", "user"], StringComparer.OrdinalIgnoreCase, m));
        yield return Pass(8, (g, m) => g.If(v => v % 2 != 0, m));
        yield return Pass(7, (g, m) => g.IfNot(v => v % 2 != 0, m));
        yield return Pass(boxed, (g, m) => g.IfType(typeof(int), m));
        yield return Pass<object?>(null, (g, m) => g.IfType(typeof(object), m));
        yield return Pass(boxed, (g, m) => g.IfNotType(typeof(string), m));
        yield return Pass<Stream>(new MemoryStream(), (g, m) => g.IfNotType(typeof(IDisposable), m));
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
    public void IfOneOf_with_params_fails_for_a_listed_value()
    {
        var role = "root";

        var exception = Assert.Throws<ArgumentException>(() => role.Throw().IfOneOf("admin", "root"));

        Assert.Equal("role", exception.ParamName);
        Assert.StartsWith("'role' must not be one of the rejected values.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IfOneOf_with_params_passes_for_other_values()
    {
        int status = 200.Throw().IfOneOf(500, 502, 503);
        int noValues = 1.Throw().IfOneOf();

        Assert.Equal(200, status);
        Assert.Equal(1, noValues);
    }

    [Fact]
    public void IfNotOneOf_with_params_checks_membership()
    {
        int allowed = 2.Throw().IfNotOneOf(1, 2, 3);

        Assert.Equal(2, allowed);
        Assert.Throws<ArgumentException>(() => 0.Throw().IfNotOneOf(1, 2, 3));
        Assert.Throws<ArgumentException>(() => 0.Throw().IfNotOneOf());
    }

    [Fact]
    public void IfOneOf_with_a_lazy_sequence_stops_at_the_first_match()
    {
        var sequence = new CountingSequence<int>(i => i);

        Assert.Throws<ArgumentException>(() => 3.Throw().IfOneOf(sequence));

        Assert.Equal(4, sequence.ItemsRead);
        Assert.Equal(1, sequence.Disposals);
    }

    [Fact]
    public void IfOneOf_uses_the_set_comparer_when_no_comparer_is_given()
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin" };

        Assert.Throws<ArgumentException>(() => "ADMIN".Throw().IfOneOf(reserved));
        Assert.Throws<ArgumentException>(() => "ADMIN".Throw().IfOneOf(reserved, StringComparer.OrdinalIgnoreCase));
        "ADMIN".Throw().IfOneOf(reserved, StringComparer.Ordinal);
    }

    [Fact]
    public void Null_value_lists_throw_even_in_collect_mode()
    {
        var report = new ValidationReport();

        var oneOf = Assert.Throws<ArgumentNullException>(() => report.Check(1, "n").IfOneOf((int[])null!));
        var oneOfEnumerable = Assert.Throws<ArgumentNullException>(() => report.Check(1, "n").IfOneOf((IEnumerable<int>)null!));
        var notOneOf = Assert.Throws<ArgumentNullException>(() => report.Check(1, "n").IfNotOneOf((IEnumerable<int>)null!));

        Assert.Equal("values", oneOf.ParamName);
        Assert.Equal("values", oneOfEnumerable.ParamName);
        Assert.Equal("values", notOneOf.ParamName);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Null_predicates_and_types_throw_ArgumentNullException()
    {
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => 1.Throw().If(null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => 1.Throw().IfNot(null!)).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentNullException>(() => 1.Throw().IfType(null!)).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentNullException>(() => 1.Throw().IfNotType(null!)).ParamName);
    }

    [Fact]
    public void Library_argument_checks_run_even_on_an_inactive_guard()
    {
        var report = new ValidationReport();
        var failed = report.Check(1, "n").Fail("Earlier", "An earlier failure.");

        Assert.Throws<ArgumentNullException>(() => report.Check(1, "n").Fail("Earlier", "An earlier failure.").If(null!));
        Assert.True(failed.HasFailed);
    }

    [Fact]
    public void General_checks_pass_null_through_to_the_predicate()
    {
        string? name = null;
        string? seen = "not called";

        name.Throw().If(v =>
        {
            seen = v;
            return false;
        });

        Assert.Null(seen);
    }

    [Fact]
    public void IfEqualTo_uses_the_given_comparer()
    {
        var comparer = new ModuloComparer(10);

        Assert.Throws<ArgumentException>(() => 13.Throw().IfEqualTo(3, comparer));
        13.Throw().IfNotEqualTo(23, comparer);
    }

    [Fact]
    public void IfType_matches_derived_types_and_interfaces()
    {
        object stream = new MemoryStream();

        Assert.Throws<ArgumentException>(() => stream.Throw().IfType(typeof(IDisposable)));
        Assert.Throws<ArgumentException>(() => stream.Throw().IfNotType(typeof(string)));
        stream.Throw().IfNotType(typeof(Stream));
    }

    [Fact]
    public void IfDefault_on_a_struct_uses_its_equality()
    {
        Assert.Throws<ArgumentException>(() => default(Point).Throw().IfDefault());
        new Point(1, 0).Throw().IfDefault();
    }

    private static bool IsEven(int value) => value % 2 == 0;

    private sealed class ModuloComparer(int modulo) : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x % modulo == y % modulo;

        public int GetHashCode(int obj) => obj % modulo;
    }

    private readonly struct Point(int x, int y) : IEquatable<Point>
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public bool Equals(Point other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Point other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;
    }
}
