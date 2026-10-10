using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests.Guards;

public sealed class CollectionGuardTests
{
    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(new List<int>(), (g, m) => g.IfNullOrEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(Array.Empty<string>(), (g, m) => g.IfNullOrEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(new Dictionary<string, int>(), (g, m) => g.IfNullOrEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(Enumerable.Empty<int>(), (g, m) => g.IfNullOrEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(new List<int> { 1, 2 }, (g, m) => g.IfCountEqualTo(2, m), ErrorCodes.CountEqualTo, Expect.Argument, "'input' must not have exactly 2 items.");
        yield return Fail(new[] { 1 }, (g, m) => g.IfCountEqualTo(1, m), ErrorCodes.CountEqualTo, Expect.Argument, "'input' must not have exactly 1 item.");
        yield return Fail(new List<int>(), (g, m) => g.IfCountEqualTo(0, m), ErrorCodes.CountEqualTo, Expect.Argument, "'input' must not have exactly 0 items.");
        yield return Fail(new List<int> { 1, 2 }, (g, m) => g.IfCountNotEqualTo(3, m), ErrorCodes.CountNotEqualTo, Expect.Argument, "'input' must have exactly 3 items.");
        yield return Fail(new List<int> { 1, 2 }, (g, m) => g.IfCountNotEqualTo(1, m), ErrorCodes.CountNotEqualTo, Expect.Argument, "'input' must have exactly 1 item.");
        yield return Fail(new List<int> { 1, 2 }, (g, m) => g.IfCountGreaterThan(1, m), ErrorCodes.CountGreaterThan, Expect.Argument, "'input' must have at most 1 item.");
        yield return Fail(new List<int> { 1 }, (g, m) => g.IfCountGreaterThan(0, m), ErrorCodes.CountGreaterThan, Expect.Argument, "'input' must have at most 0 items.");
        yield return Fail(Enumerable.Range(0, 3), (g, m) => g.IfCountGreaterThan(2, m), ErrorCodes.CountGreaterThan, Expect.Argument, "'input' must have at most 2 items.");
        yield return Fail(new List<int> { 1 }, (g, m) => g.IfCountLessThan(2, m), ErrorCodes.CountLessThan, Expect.Argument, "'input' must have at least 2 items.");
        yield return Fail(new List<int>(), (g, m) => g.IfCountLessThan(1, m), ErrorCodes.CountLessThan, Expect.Argument, "'input' must have at least 1 item.");
        yield return Fail(new List<int> { 1 }, (g, m) => g.IfCountOutOfRange(2, 3, m), ErrorCodes.CountOutOfRange, Expect.Argument, "'input' must have between 2 and 3 items.");
        yield return Fail(new List<int> { 1, 2, 3, 4 }, (g, m) => g.IfCountOutOfRange(2, 3, m), ErrorCodes.CountOutOfRange, Expect.Argument, "'input' must have between 2 and 3 items.");
        yield return Fail(new List<int> { 1, 2, 3 }, (g, m) => g.IfContains(2, message: m), ErrorCodes.Contains, Expect.Argument, "'input' must not contain 2.");
        yield return Fail(new List<string> { "a", "B" }, (g, m) => g.IfContains("b", StringComparer.OrdinalIgnoreCase, m), ErrorCodes.Contains, Expect.Argument, "'input' must not contain the rejected item.");
        yield return Fail(new List<string?> { "a", null }, (g, m) => g.IfContains((string?)null, message: m), ErrorCodes.Contains, Expect.Argument, "'input' must not contain the rejected item.");
        yield return Fail(new List<int> { 1, 2, 3 }, (g, m) => g.IfNotContains(9, message: m), ErrorCodes.NotContains, Expect.Argument, "'input' must contain 9.");
        yield return Fail(new List<string> { "a" }, (g, m) => g.IfNotContains("A", message: m), ErrorCodes.NotContains, Expect.Argument, "'input' must contain the required item.");
        yield return Fail(new List<string?> { "a", null }, (g, m) => g.IfContainsNull(m), ErrorCodes.ContainsNull, Expect.Argument, "'input' must not contain null items.");
        yield return Fail(new List<int?> { 1, null }, (g, m) => g.IfContainsNull(m), ErrorCodes.ContainsNull, Expect.Argument, "'input' must not contain null items.");
        yield return Fail(new List<int> { 1, 2, 1 }, (g, m) => g.IfHasDuplicates(m), ErrorCodes.HasDuplicates, Expect.Argument, "'input' must not contain duplicate items.");
        yield return Fail(new List<string?> { null, "a", null }, (g, m) => g.IfHasDuplicates(m), ErrorCodes.HasDuplicates, Expect.Argument, "'input' must not contain duplicate items.");
        yield return Fail(new List<int> { 1, 2, 3 }, (g, m) => g.IfAny((int x) => x > 2, m), ErrorCodes.Any, Expect.Argument, "'input' must not contain any item that matches the condition.");
        yield return Fail(new[] { "a", "b" }, (g, m) => g.IfNone((string x) => x.Length > 1, m), ErrorCodes.None, Expect.Argument, "'input' must contain at least one item that matches the condition.");
        yield return Fail(new List<int>(), (g, m) => g.IfNone((int x) => true, m), ErrorCodes.None, Expect.Argument, "'input' must contain at least one item that matches the condition.");

        // A null collection fails every collection check with the "Null" code.
        yield return Null<List<int>>((g, m) => g.IfNullOrEmpty(m));
        yield return Null<List<int>>((g, m) => g.IfCountEqualTo(1, m));
        yield return Null<List<int>>((g, m) => g.IfCountNotEqualTo(1, m));
        yield return Null<List<int>>((g, m) => g.IfCountGreaterThan(1, m));
        yield return Null<List<int>>((g, m) => g.IfCountLessThan(1, m));
        yield return Null<List<int>>((g, m) => g.IfCountOutOfRange(1, 2, m));
        yield return Null<List<int>>((g, m) => g.IfContains(1, message: m));
        yield return Null<List<int>>((g, m) => g.IfNotContains(1, message: m));
        yield return Null<List<string>>((g, m) => g.IfContainsNull(m));
        yield return Null<List<int>>((g, m) => g.IfHasDuplicates(m));
        yield return Null<List<int>>((g, m) => g.IfAny((int x) => x > 0, m));
        yield return Null<List<int>>((g, m) => g.IfNone((int x) => x > 0, m));
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(new List<int> { 1 }, (g, m) => g.IfNullOrEmpty(m));
        yield return Pass(Enumerable.Range(0, 1), (g, m) => g.IfNullOrEmpty(m));
        yield return Pass(new List<int> { 1, 2 }, (g, m) => g.IfCountEqualTo(3, m));
        yield return Pass(new List<int> { 1, 2 }, (g, m) => g.IfCountEqualTo(1, m));
        yield return Pass(new List<int> { 1, 2 }, (g, m) => g.IfCountNotEqualTo(2, m));
        yield return Pass(new List<int>(), (g, m) => g.IfCountNotEqualTo(0, m));
        yield return Pass(new List<int> { 1, 2 }, (g, m) => g.IfCountGreaterThan(2, m));
        yield return Pass(new List<int>(), (g, m) => g.IfCountGreaterThan(0, m));
        yield return Pass(new List<int> { 1, 2 }, (g, m) => g.IfCountLessThan(2, m));
        yield return Pass(new List<int>(), (g, m) => g.IfCountLessThan(0, m));
        yield return Pass(new List<int> { 1, 2 }, (g, m) => g.IfCountOutOfRange(2, 3, m));
        yield return Pass(new List<int> { 1, 2, 3 }, (g, m) => g.IfCountOutOfRange(2, 3, m));
        yield return Pass(new List<int>(), (g, m) => g.IfCountOutOfRange(0, 0, m));
        yield return Pass(new List<int> { 1, 2, 3 }, (g, m) => g.IfContains(9, message: m));
        yield return Pass(new List<string> { "a", "B" }, (g, m) => g.IfContains("b", message: m));
        yield return Pass(new List<int> { 1, 2, 3 }, (g, m) => g.IfNotContains(2, message: m));
        yield return Pass(new List<string> { "a" }, (g, m) => g.IfNotContains("A", StringComparer.OrdinalIgnoreCase, m));
        yield return Pass(new List<string> { "a", "b" }, (g, m) => g.IfContainsNull(m));
        yield return Pass(new List<int> { 1, 2 }, (g, m) => g.IfContainsNull(m));
        yield return Pass(new List<int> { 1, 2, 3 }, (g, m) => g.IfHasDuplicates(m));
        yield return Pass(new List<string> { "a", "A" }, (g, m) => g.IfHasDuplicates(m));
        yield return Pass(new List<int>(), (g, m) => g.IfHasDuplicates(m));
        yield return Pass(new List<int> { 1, 2, 3 }, (g, m) => g.IfAny((int x) => x > 3, m));
        yield return Pass(new List<int>(), (g, m) => g.IfAny((int x) => true, m));
        yield return Pass(new[] { "a", "bc" }, (g, m) => g.IfNone((string x) => x.Length > 1, m));
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
    public void IfCountGreaterThan_stops_reading_a_lazy_sequence_early()
    {
        var endless = new CountingSequence<int>(i => i);

        Assert.Throws<ArgumentException>(() => endless.Throw().IfCountGreaterThan(5));

        Assert.Equal(6, endless.ItemsRead);
        Assert.Equal(1, endless.Enumerations);
        Assert.Equal(1, endless.Disposals);
    }

    [Fact]
    public void Count_checks_read_a_lazy_sequence_only_as_far_as_needed()
    {
        var tenItems = new CountingSequence<int>(i => i, count: 10);

        tenItems.Throw().IfNullOrEmpty();
        Assert.Equal(1, tenItems.ItemsRead);

        tenItems.Throw().IfCountLessThan(3);
        Assert.Equal(1 + 3, tenItems.ItemsRead);

        tenItems.Throw().IfCountEqualTo(2);
        Assert.Equal(1 + 3 + 3, tenItems.ItemsRead);

        tenItems.Throw().IfCountOutOfRange(0, 20);
        Assert.Equal(1 + 3 + 3 + 10, tenItems.ItemsRead);

        Assert.Equal(4, tenItems.Disposals);
    }

    [Fact]
    public void Count_checks_do_not_enumerate_a_collection_that_knows_its_count()
    {
        var thousand = new CountOnlyCollection<string>(1000);

        thousand.Throw().IfNullOrEmpty().IfCountLessThan(1000).IfCountGreaterThan(1000).IfCountNotEqualTo(1000).IfCountOutOfRange(1, 1000);
        Assert.Throws<ArgumentException>(() => thousand.Throw().IfCountEqualTo(1000));
    }

    [Fact]
    public void Count_checks_work_at_int_MaxValue()
    {
        var huge = new CountOnlyCollection<string>(int.MaxValue);
        var small = new List<int> { 1, 2, 3 };

        huge.Throw().IfCountGreaterThan(int.MaxValue).IfCountNotEqualTo(int.MaxValue).IfCountOutOfRange(0, int.MaxValue);
        Assert.Throws<ArgumentException>(() => huge.Throw().IfCountEqualTo(int.MaxValue));
        small.Throw().IfCountGreaterThan(int.MaxValue).IfCountEqualTo(int.MaxValue).IfCountOutOfRange(0, int.MaxValue);
        Assert.Throws<ArgumentException>(() => small.Throw().IfCountNotEqualTo(int.MaxValue));
        Assert.Throws<ArgumentException>(() => small.Throw().IfCountLessThan(int.MaxValue));
    }

    [Fact]
    public void Count_checks_work_on_a_set_of_value_types()
    {
        var numbers = new HashSet<int> { 1, 2, 3 };

        numbers.Throw().IfCountGreaterThan(3).IfCountLessThan(3);
        Assert.Throws<ArgumentException>(() => numbers.Throw().IfCountNotEqualTo(2));
    }

    [Fact]
    public void IfContains_uses_the_set_comparer_when_no_comparer_is_given()
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin" };

        Assert.Throws<ArgumentException>(() => roles.Throw().IfContains("ADMIN"));
        roles.Throw().IfNotContains("admin");
    }

    [Fact]
    public void IfContains_uses_the_given_comparer_over_the_set_comparer()
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin" };

        roles.Throw().IfContains("ADMIN", StringComparer.Ordinal);
        Assert.Throws<ArgumentException>(() => roles.Throw().IfNotContains("ADMIN", StringComparer.Ordinal));
    }

    [Fact]
    public void IfContains_on_a_lazy_sequence_stops_at_the_first_match()
    {
        var endless = new CountingSequence<int>(i => i * 2);

        Assert.Throws<ArgumentException>(() => endless.Throw().IfContains(10));

        Assert.Equal(6, endless.ItemsRead);
    }

    [Fact]
    public void IfContainsNull_stops_at_the_first_null()
    {
        var items = new CountingSequence<string?>(i => i == 2 ? null : "item");

        Assert.Throws<ArgumentException>(() => items.Throw().IfContainsNull());

        Assert.Equal(3, items.ItemsRead);
    }

    [Fact]
    public void IfHasDuplicates_stops_at_the_first_duplicate()
    {
        var items = new CountingSequence<int>(i => i < 4 ? i : 0);

        Assert.Throws<ArgumentException>(() => items.Throw().IfHasDuplicates());

        Assert.Equal(5, items.ItemsRead);
    }

    [Fact]
    public void IfHasDuplicates_uses_the_item_Equals()
    {
        var people = new List<Person> { new("Ada Lovelace"), new("ada lovelace"), new("Ada Lovelace") };

        Assert.Throws<ArgumentException>(() => people.Throw().IfHasDuplicates());
    }

    [Fact]
    public void Strings_can_be_checked_as_collections()
    {
        IEnumerable<char> word = "Ada";

        word.Throw().IfCountGreaterThan(3).IfNullOrEmpty();
        Assert.Throws<ArgumentException>(() => "Ada".Throw().IfCountGreaterThan(2));
        Assert.Throws<ArgumentException>(() => word.Throw().IfHasDuplicates().IfContains('A'));
    }

    [Fact]
    public void Non_generic_collections_work()
    {
        IEnumerable list = new ArrayList { 1, null, 1 };

        list.Throw().IfNullOrEmpty().IfCountOutOfRange(3, 3);
        Assert.Throws<ArgumentException>(() => list.Throw().IfContainsNull());
        Assert.Throws<ArgumentException>(() => list.Throw().IfHasDuplicates());
    }

    [Fact]
    public void Negative_counts_throw_even_in_collect_mode()
    {
        var report = new ValidationReport();
        var items = new List<int>();

        Assert.Equal("count", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(items).IfCountEqualTo(-1)).ParamName);
        Assert.Equal("count", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(items).IfCountNotEqualTo(-1)).ParamName);
        Assert.Equal("maxCount", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(items).IfCountGreaterThan(-1)).ParamName);
        Assert.Equal("minCount", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(items).IfCountLessThan(-1)).ParamName);
        Assert.Equal("minCount", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(items).IfCountOutOfRange(-1, 1)).ParamName);
        Assert.Equal("maxCount", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(items).IfCountOutOfRange(2, 1)).ParamName);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Collection_check_names_come_from_the_expression()
    {
        var order = new { Lines = new List<int>() };

        var exception = Assert.Throws<ArgumentException>(() => order.Lines.Throw().IfNullOrEmpty());

        Assert.Equal("order.Lines", exception.ParamName);
    }

    private sealed class Person(string name) : IEquatable<Person>
    {
        public string Name { get; } = name;

        public bool Equals(Person? other) => other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as Person);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
    }
}
