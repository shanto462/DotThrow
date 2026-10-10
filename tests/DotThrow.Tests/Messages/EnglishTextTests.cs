using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Messages;

/// <summary>
/// The default English messages of the checks that existed before templates must keep their
/// exact text. The expected strings are the old Messages.cs output for the same arguments.
/// Only EqualTo/NotEqualTo and collection Contains/NotContains with a reference-type value
/// changed on purpose (they no longer show a value that may be a secret).
/// </summary>
public sealed class EnglishTextTests
{
    [Fact]
    public void General_messages_keep_their_text()
    {
        Assert.Equal("'input' must not be null.", Check.MessageOf<string?>(null, g => g.IfNull()));
        Assert.Equal("'input' must be null.", Check.MessageOf("a", g => g.IfNotNull()));
        Assert.Equal("'input' must not be the default value.", Check.MessageOf(0, g => g.IfDefault()));
        Assert.Equal("'input' must not be equal to 5.", Check.MessageOf(5, g => g.IfEqualTo(5)));
        Assert.Equal("'input' must be equal to 6.", Check.MessageOf(5, g => g.IfNotEqualTo(6)));
        Assert.Equal("'input' must not be one of the rejected values.", Check.MessageOf(5, g => g.IfOneOf(4, 5)));
        Assert.Equal("'input' must be one of the allowed values.", Check.MessageOf(5, g => g.IfNotOneOf(4, 6)));
        Assert.Equal("'input' must not satisfy the condition: x => x > 1.", Check.MessageOf(5, g => g.If(x => x > 1)));
        Assert.Equal("'input' must satisfy the condition: x => x > 9.", Check.MessageOf(5, g => g.IfNot(x => x > 9)));
        Assert.Equal("'input' is not valid.", Check.MessageOf(5, g => g.If(x => x > 1, null, predicateExpression: null)));
        Assert.Equal("'input' must not be of type Int32.", Check.MessageOf<object>(5, g => g.IfType(typeof(int))));
        Assert.Equal("'input' must be of type String.", Check.MessageOf<object>(5, g => g.IfNotType(typeof(string))));
        Assert.Equal("'input' must be false.", Check.MessageOf(true, g => g.IfTrue()));
        Assert.Equal("'input' must be true.", Check.MessageOf(false, g => g.IfFalse()));
        Assert.Equal("'input' must not be an empty GUID.", Check.MessageOf(Guid.Empty, g => g.IfEmpty()));
    }

    [Fact]
    public void Comparison_messages_keep_their_text()
    {
        Assert.Equal("'input' must be less than or equal to 3.", Check.MessageOf(5, g => g.IfGreaterThan(3)));
        Assert.Equal("'input' must be less than 5.", Check.MessageOf(5, g => g.IfGreaterThanOrEqualTo(5)));
        Assert.Equal("'input' must be greater than or equal to 9.", Check.MessageOf(5, g => g.IfLessThan(9)));
        Assert.Equal("'input' must be greater than 5.", Check.MessageOf(5, g => g.IfLessThanOrEqualTo(5)));
        Assert.Equal("'input' must be between 6 and 9.", Check.MessageOf(5, g => g.IfOutOfRange(6, 9)));
        Assert.Equal("'input' must not be between 1 and 9.", Check.MessageOf(5, g => g.IfInRange(1, 9)));
        Assert.Equal("'input' must be less than or equal to 1.5.", Check.MessageOf(2.5, g => g.IfGreaterThan(1.5)));
        Assert.Equal("'input' must be less than or equal to 'a'.", Check.MessageOf("b", g => g.IfGreaterThan("a")));
        Assert.Equal("'input' must not be negative.", Check.MessageOf(-1, g => g.IfNegative()));
        Assert.Equal("'input' must be greater than zero.", Check.MessageOf(0, g => g.IfNegativeOrZero()));
        Assert.Equal("'input' must not be greater than zero.", Check.MessageOf(1, g => g.IfPositive()));
        Assert.Equal("'input' must be less than zero.", Check.MessageOf(0, g => g.IfPositiveOrZero()));
        Assert.Equal("'input' must not be zero.", Check.MessageOf(0, g => g.IfZero()));
        Assert.Equal("'input' must be a number, not NaN.", Check.MessageOf(double.NaN, g => g.IfNaN()));
        Assert.Equal("'input' must be a finite number.", Check.MessageOf(double.PositiveInfinity, g => g.IfInfinity()));
    }

    [Fact]
    public void String_messages_keep_their_text()
    {
        Assert.Equal("'input' must not be empty.", Check.MessageOf("", g => g.IfNullOrEmpty()));
        Assert.Equal("'input' must not be empty or white space.", Check.MessageOf(" ", g => g.IfNullOrWhiteSpace()));
        Assert.Equal("'input' must be at most 2 characters long.", Check.MessageOf("abc", g => g.IfLongerThan(2)));
        Assert.Equal("'input' must be at least 5 characters long.", Check.MessageOf("abc", g => g.IfShorterThan(5)));
        Assert.Equal("'input' must be exactly 2 characters long.", Check.MessageOf("abc", g => g.IfLengthNotEqualTo(2)));
        Assert.Equal("'input' must be between 4 and 5 characters long.", Check.MessageOf("abc", g => g.IfLengthOutOfRange(4, 5)));
        Assert.Equal("'input' must not contain 'b'.", Check.MessageOf("abc", g => g.IfContains("b")));
        Assert.Equal("'input' must contain 'x'.", Check.MessageOf("abc", g => g.IfNotContains("x")));
        Assert.Equal("'input' must not start with 'a'.", Check.MessageOf("abc", g => g.IfStartsWith("a")));
        Assert.Equal("'input' must start with 'x'.", Check.MessageOf("abc", g => g.IfNotStartsWith("x")));
        Assert.Equal("'input' must not end with 'c'.", Check.MessageOf("abc", g => g.IfEndsWith("c")));
        Assert.Equal("'input' must end with 'x'.", Check.MessageOf("abc", g => g.IfNotEndsWith("x")));
        Assert.Equal("'input' must not match the pattern '^a'.", Check.MessageOf("abc", g => g.IfMatches("^a")));
        Assert.Equal("'input' must match the pattern '^x'.", Check.MessageOf("abc", g => g.IfNotMatches(new Regex("^x"))));
        Assert.Equal("'input' must be a valid email address.", Check.MessageOf("abc", g => g.IfNotEmailAddress()));
        Assert.Equal("'input' must be valid JSON.", Check.MessageOf("{", g => g.IfNotJson()));
        Assert.Equal("'input' must be well-formed XML.", Check.MessageOf("<a>", g => g.IfNotXml()));
        Assert.Equal("'input' must be a valid absolute URI.", Check.MessageOf("x", g => g.IfNotUri()));
        Assert.Equal("'input' must be a valid relative URI.", Check.MessageOf("https://a.b", g => g.IfNotUri(UriKind.Relative)));
        Assert.Equal("'input' must be a valid URI.", Check.MessageOf("C:\\temp", g => g.IfNotUri(UriKind.RelativeOrAbsolute)));
        Assert.Equal("'input' must be an absolute HTTP or HTTPS URL.", Check.MessageOf("ftp://a.b", g => g.IfNotHttpUrl()));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Assert.Equal("'input' must be a valid Int32.", Check.MessageOf("x", g => g.IfNotParsable<int>()));
#endif
        Assert.Equal("'input' must not be all uppercase.", Check.MessageOf("ABC", g => g.IfAllUpperCase()));
        Assert.Equal("'input' must not be all lowercase.", Check.MessageOf("abc", g => g.IfAllLowerCase()));
    }

    [Fact]
    public void Collection_messages_keep_their_text_and_the_one_item_wording()
    {
        var three = new List<int> { 1, 2, 3 };
        Assert.Equal("'input' must not be empty.", Check.MessageOf(new List<int>(), g => g.IfNullOrEmpty()));
        Assert.Equal("'input' must not have exactly 3 items.", Check.MessageOf(three, g => g.IfCountEqualTo(3)));
        Assert.Equal("'input' must not have exactly 1 item.", Check.MessageOf(new List<int> { 1 }, g => g.IfCountEqualTo(1)));
        Assert.Equal("'input' must have exactly 2 items.", Check.MessageOf(three, g => g.IfCountNotEqualTo(2)));
        Assert.Equal("'input' must have exactly 1 item.", Check.MessageOf(three, g => g.IfCountNotEqualTo(1)));
        Assert.Equal("'input' must have at most 2 items.", Check.MessageOf(three, g => g.IfCountGreaterThan(2)));
        Assert.Equal("'input' must have at most 1 item.", Check.MessageOf(three, g => g.IfCountGreaterThan(1)));
        Assert.Equal("'input' must have at least 4 items.", Check.MessageOf(three, g => g.IfCountLessThan(4)));
        Assert.Equal("'input' must have at least 1 item.", Check.MessageOf(new List<int>(), g => g.IfCountLessThan(1)));
        Assert.Equal("'input' must have between 4 and 5 items.", Check.MessageOf(three, g => g.IfCountOutOfRange(4, 5)));
        Assert.Equal("'input' must not contain 2.", Check.MessageOf(three, g => g.IfContains(2)));
        Assert.Equal("'input' must contain 9.", Check.MessageOf(three, g => g.IfNotContains(9)));
        Assert.Equal("'input' must not contain null items.", Check.MessageOf(new List<string?> { null }, g => g.IfContainsNull()));
        Assert.Equal("'input' must not contain duplicate items.", Check.MessageOf(new List<int> { 1, 1 }, g => g.IfHasDuplicates()));
    }

    [Fact]
    public void Enum_and_date_messages_keep_their_text()
    {
        var limit = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        Assert.Equal("'input' must be a defined DayOfWeek value.", Check.MessageOf((DayOfWeek)42, g => g.IfNotDefined()));
        Assert.Equal("'input' must not be before 2026-01-02 03:04:05Z.", Check.MessageOf(limit.AddDays(-1), g => g.IfBefore(limit)));
        Assert.Equal("'input' must not be after 2026-01-02 03:04:05Z.", Check.MessageOf(limit.AddDays(1), g => g.IfAfter(limit)));
        Assert.Equal("'input' must not be in the past.", Check.MessageOf(DateTime.UtcNow.AddDays(-1), g => g.IfInPast()));
        Assert.Equal("'input' must not be in the future.", Check.MessageOf(DateTime.UtcNow.AddDays(1), g => g.IfInFuture()));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Assert.Equal("'input' must not be before 2026-01-02.", Check.MessageOf(new DateOnly(2026, 1, 1), g => g.IfBefore(new DateOnly(2026, 1, 2))));
#endif
    }

    [Fact]
    public void Reference_type_comparands_are_left_out_of_the_default_message()
    {
        Assert.Equal("'input' must not be equal to the rejected value.", Check.MessageOf("hunter2", g => g.IfEqualTo("hunter2")));
        Assert.Equal("'input' must be equal to the required value.", Check.MessageOf("a", g => g.IfNotEqualTo("hunter2")));
        Assert.Equal("'input' must not be equal to the rejected value.", Check.MessageOf("A", g => g.IfEqualTo("a", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("'input' must be equal to the required value.", Check.MessageOf("a", g => g.IfNotEqualTo("b", StringComparison.Ordinal)));
        Assert.Equal("'input' must not contain the rejected item.", Check.MessageOf(new List<string> { "token" }, g => g.IfContains("token")));
        Assert.Equal("'input' must contain the required item.", Check.MessageOf(new List<string> { "a" }, g => g.IfNotContains("token")));

        // A custom message can still show the value.
        Assert.Equal("was 'hunter2'", Check.MessageOf("hunter2", g => g.IfEqualTo("hunter2", message: "was {ComparisonValue}")));
    }
}
