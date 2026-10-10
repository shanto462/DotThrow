using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>The findings of the tests agent and the docs agent that were in the core area.</summary>
public sealed class ReviewFixTests
{
    // 1. Secrets: covered in EnglishTextTests.Reference_type_comparands_are_left_out_of_the_default_message.

    // 2. Strings compare by ordinal in every ordering check.
    [Fact]
    public void String_ordering_is_ordinal_not_culture_sensitive()
    {
        // Culture-sensitive comparison puts "a" before "B"; ordinal puts "B" (66) before "a" (97).
        Check.Passes("a", g => g.IfLessThan("B"));
        Check.Fails<ArgumentOutOfRangeException, string>("B", g => g.IfGreaterThanOrEqualTo("a").IfLessThan("a"), ErrorCodes.LessThan);
        Check.Passes("B", g => g.IfOutOfRange("A", "Z"));
        Check.Fails<ArgumentOutOfRangeException, string>("a", g => g.IfOutOfRange("A", "Z"), ErrorCodes.OutOfRange);
        Check.Passes("\u00E9", g => g.IfLessThan("e"));
    }

    // 3. A NaN limit is a programming error.
    [Theory]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void A_NaN_limit_throws_even_in_collect_mode(double value)
    {
        var report = new ValidationReport();
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfGreaterThan(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfLessThan(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfGreaterThanOrEqualTo(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfLessThanOrEqualTo(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfOutOfRange(double.NaN, 2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfOutOfRange(0.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfOutOfRangeExclusive(0.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(value).IfInRange(double.NaN, 2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check((float)value).IfLessThan(float.NaN));
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void A_reversed_range_throws_even_in_collect_mode()
    {
        var report = new ValidationReport();
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(5).IfOutOfRange(10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(5).IfInRange(10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(5).IfOutOfRangeExclusive(10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("abc").IfLengthOutOfRange(5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("abc").IfLongerThan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("abc").IfShorterThan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(new List<int>()).IfCountGreaterThan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(new List<int>()).IfCountLessThan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.Check(new List<int>()).IfCountOutOfRange(3, 1));
        Assert.Throws<ArgumentNullException>(() => report.Check("b").IfGreaterThan(null!));
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void A_NaN_value_still_fails_every_ordering_check()
    {
        Check.Fails<ArgumentOutOfRangeException, double>(double.NaN, g => g.IfGreaterThan(1.0), ErrorCodes.GreaterThan);
        Check.Fails<ArgumentOutOfRangeException, double>(double.NaN, g => g.IfLessThan(1.0), ErrorCodes.LessThan);
        Check.Fails<ArgumentOutOfRangeException, double>(double.NaN, g => g.IfNegative(), ErrorCodes.Negative);
        Check.Fails<ArgumentOutOfRangeException, double>(double.NaN, g => g.IfPositive(), ErrorCodes.Positive);
        Check.Fails<ArgumentOutOfRangeException, double>(double.NaN, g => g.IfInRange(5.0, 6.0), ErrorCodes.InRange);
        Check.Fails<ArgumentOutOfRangeException, float>(float.NaN, g => g.IfOutOfRange(0f, 1f), ErrorCodes.OutOfRange);
        Check.Passes(double.NaN, g => g.IfZero());
        Check.Passes(-0.0, g => g.IfNegative());
    }

    // 4. DateTime kinds.
    [Fact]
    public void Utc_and_local_DateTime_values_compare_as_UTC()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();
        Check.Passes(local, g => g.IfBefore(utc).IfAfter(utc));
        Check.Passes(utc, g => g.IfBefore(local).IfAfter(local).IfGreaterThan(local).IfLessThan(local));
        Check.Fails<ArgumentOutOfRangeException, DateTime>(local.AddTicks(1), g => g.IfAfter(utc), ErrorCodes.After);

        // Unspecified compares as is.
        var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        Check.Passes(unspecified, g => g.IfBefore(utc).IfAfter(utc));
    }

    // 5. URIs: same answer on every OS.
    [Theory]
    [InlineData("", UriKind.Relative)]
    [InlineData("   ", UriKind.RelativeOrAbsolute)]
    [InlineData("C:\\temp", UriKind.Relative)]
    [InlineData("C:/temp", UriKind.Relative)]
    [InlineData("C:/temp", UriKind.Absolute)]
    [InlineData("C:", UriKind.RelativeOrAbsolute)]
    [InlineData("folder\\file.txt", UriKind.Relative)]
    [InlineData("http://example.com\\path", UriKind.Absolute)]
    [InlineData("\\\\server\\share", UriKind.RelativeOrAbsolute)]
    public void Empty_text_backslashes_and_drive_paths_are_not_uris(string text, UriKind kind) =>
        Check.Fails<ArgumentException, string>(text, g => g.IfNotUri(kind), ErrorCodes.NotUri);

    [Theory]
    [InlineData("FILE:///C:/temp", UriKind.Absolute)]
    [InlineData("a/b", UriKind.Relative)]
    [InlineData("?q=1", UriKind.Relative)]
    [InlineData("https://example.com/a%5Cb", UriKind.Absolute)]
    public void Normal_uris_still_pass(string text, UriKind kind) => Check.Passes(text, g => g.IfNotUri(kind));

    // 6. IfContains(null) on a collection: IfContainsNull is the check for it.
    [Fact]
    public void IfContainsNull_rejects_null_items_and_a_typed_null_works_with_IfContains()
    {
        var items = new List<string?> { "a", null };
        Check.Fails<ArgumentException, List<string?>>(items, g => g.IfContainsNull(), ErrorCodes.ContainsNull);
        Check.Fails<ArgumentException, List<string?>>(items, g => g.IfContains((string?)null), ErrorCodes.Contains);
    }

    // Docs agent: a null value fails IfDefault and IfNotType with ArgumentNullException.
    [Fact]
    public void Null_fails_IfDefault_and_IfNotType_with_ArgumentNullException_and_the_same_code()
    {
        Check.Fails<ArgumentNullException, string?>(null, g => g.IfDefault(), ErrorCodes.Default, "'input' must not be the default value.");
        Check.Fails<ArgumentNullException, object?>(null, g => g.IfNotType(typeof(object)), ErrorCodes.NotType, "'input' must be of type Object.");
        Check.Fails<ArgumentException, int>(0, g => g.IfDefault(), ErrorCodes.Default);
    }

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    [Fact]
    public void Params_span_overloads_take_a_message_through_a_collection_expression()
    {
        Assert.Equal("bad", Check.MessageOf(3, g => g.IfOneOf([1, 2, 3], message: "bad")));
        Assert.Equal("bad", Check.MessageOf(4, g => g.IfNotOneOf([1, 2, 3], message: "bad")));
        Check.Passes(4, g => g.IfOneOf(1, 2, 3));
        Check.Passes(2, g => g.IfNotOneOf(1, 2, 3));
        Check.Passes("a", g => g.IfOneOf(["b", "c"], StringComparer.Ordinal));
        Check.Fails<ArgumentException, string>("B", g => g.IfOneOf(["b", "c"], StringComparer.OrdinalIgnoreCase), ErrorCodes.OneOf);
    }
#endif
}
