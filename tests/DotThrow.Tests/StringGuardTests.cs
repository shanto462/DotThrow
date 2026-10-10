using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System.Globalization;
#endif

namespace DotThrow.Tests;

public sealed class StringGuardTests
{
    private static readonly Regex Lowercase = new("^[a-z]+$", RegexOptions.CultureInvariant);

    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail("", (g, m) => g.IfNullOrEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail("", (g, m) => g.IfNullOrWhiteSpace(m), ErrorCodes.WhiteSpace, Expect.Argument, "'input' must not be empty or white space.");
        yield return Fail(" \t\r\n\u00A0\u2003", (g, m) => g.IfNullOrWhiteSpace(m), ErrorCodes.WhiteSpace, Expect.Argument, "'input' must not be empty or white space.");
        yield return Fail("abcd", (g, m) => g.IfLongerThan(3, m), ErrorCodes.LongerThan, Expect.Argument, "'input' must be at most 3 characters long.");
        yield return Fail("a", (g, m) => g.IfLongerThan(0, m), ErrorCodes.LongerThan, Expect.Argument, "'input' must be at most 0 characters long.");
        yield return Fail("ab", (g, m) => g.IfShorterThan(3, m), ErrorCodes.ShorterThan, Expect.Argument, "'input' must be at least 3 characters long.");
        yield return Fail("", (g, m) => g.IfShorterThan(1, m), ErrorCodes.ShorterThan, Expect.Argument, "'input' must be at least 1 characters long.");
        yield return Fail("ab", (g, m) => g.IfLengthNotEqualTo(3, m), ErrorCodes.LengthNotEqualTo, Expect.Argument, "'input' must be exactly 3 characters long.");
        yield return Fail("abcd", (g, m) => g.IfLengthNotEqualTo(3, m), ErrorCodes.LengthNotEqualTo, Expect.Argument, "'input' must be exactly 3 characters long.");
        yield return Fail("a", (g, m) => g.IfLengthOutOfRange(2, 3, m), ErrorCodes.LengthOutOfRange, Expect.Argument, "'input' must be between 2 and 3 characters long.");
        yield return Fail("abcd", (g, m) => g.IfLengthOutOfRange(2, 3, m), ErrorCodes.LengthOutOfRange, Expect.Argument, "'input' must be between 2 and 3 characters long.");
        yield return Fail("ADA", (g, m) => g.IfEqualTo("ada", StringComparison.OrdinalIgnoreCase, m), ErrorCodes.EqualTo, Expect.Argument, "'input' must not be equal to 'ada'.");
        yield return Fail<string>(null!, (g, m) => g.IfEqualTo(null, StringComparison.Ordinal, m), ErrorCodes.EqualTo, Expect.Argument, "'input' must not be equal to null.");
        yield return Fail("ada", (g, m) => g.IfNotEqualTo("ADA", StringComparison.Ordinal, m), ErrorCodes.NotEqualTo, Expect.Argument, "'input' must be equal to 'ADA'.");
        yield return Fail<string>(null!, (g, m) => g.IfNotEqualTo("ada", StringComparison.Ordinal, m), ErrorCodes.NotEqualTo, Expect.Argument, "'input' must be equal to 'ada'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfContains("Love", message: m), ErrorCodes.Contains, Expect.Argument, "'input' must not contain 'Love'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfContains("LOVE", StringComparison.OrdinalIgnoreCase, m), ErrorCodes.Contains, Expect.Argument, "'input' must not contain 'LOVE'.");
        yield return Fail("Ada", (g, m) => g.IfContains("", message: m), ErrorCodes.Contains, Expect.Argument, "'input' must not contain ''.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfNotContains("LOVE", message: m), ErrorCodes.NotContains, Expect.Argument, "'input' must contain 'LOVE'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfStartsWith("Ada", message: m), ErrorCodes.StartsWith, Expect.Argument, "'input' must not start with 'Ada'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfStartsWith("ada", StringComparison.OrdinalIgnoreCase, m), ErrorCodes.StartsWith, Expect.Argument, "'input' must not start with 'ada'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfNotStartsWith("ada", message: m), ErrorCodes.NotStartsWith, Expect.Argument, "'input' must start with 'ada'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfEndsWith("lace", message: m), ErrorCodes.EndsWith, Expect.Argument, "'input' must not end with 'lace'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfEndsWith("LACE", StringComparison.OrdinalIgnoreCase, m), ErrorCodes.EndsWith, Expect.Argument, "'input' must not end with 'LACE'.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfNotEndsWith("LACE", message: m), ErrorCodes.NotEndsWith, Expect.Argument, "'input' must end with 'LACE'.");
        yield return Fail("abc", (g, m) => g.IfMatches("^[a-z]+$", message: m), ErrorCodes.Matches, Expect.Argument, "'input' must not match the pattern '^[a-z]+$'.");
        yield return Fail("ABC", (g, m) => g.IfMatches("^[a-z]+$", RegexOptions.IgnoreCase, m), ErrorCodes.Matches, Expect.Argument, "'input' must not match the pattern '^[a-z]+$'.");
        yield return Fail("abc", (g, m) => g.IfMatches(Lowercase, m), ErrorCodes.Matches, Expect.Argument, "'input' must not match the pattern '^[a-z]+$'.");
        yield return Fail("abc", (g, m) => g.IfNotMatches("^[0-9]+$", message: m), ErrorCodes.NotMatches, Expect.Argument, "'input' must match the pattern '^[0-9]+$'.");
        yield return Fail("ABC", (g, m) => g.IfNotMatches(Lowercase, m), ErrorCodes.NotMatches, Expect.Argument, "'input' must match the pattern '^[a-z]+$'.");
        yield return Fail("ada.example.com", (g, m) => g.IfNotEmailAddress(m), ErrorCodes.NotEmailAddress, Expect.Argument, "'input' must be a valid email address.");
        yield return Fail("{\"a\":1,}", (g, m) => g.IfNotJson(m), ErrorCodes.NotJson, Expect.Argument, "'input' must be valid JSON.");
        yield return Fail("", (g, m) => g.IfNotJson(m), ErrorCodes.NotJson, Expect.Argument, "'input' must be valid JSON.");
        yield return Fail("<a><b></a>", (g, m) => g.IfNotXml(m), ErrorCodes.NotXml, Expect.Argument, "'input' must be well-formed XML.");
        yield return Fail("not a uri", (g, m) => g.IfNotUri(message: m), ErrorCodes.NotUri, Expect.Argument, "'input' must be a valid absolute URI.");
        yield return Fail("/etc/hosts", (g, m) => g.IfNotUri(UriKind.Absolute, m), ErrorCodes.NotUri, Expect.Argument, "'input' must be a valid absolute URI.");
        yield return Fail("https://example.com", (g, m) => g.IfNotUri(UriKind.Relative, m), ErrorCodes.NotUri, Expect.Argument, "'input' must be a valid relative URI.");
        yield return Fail("C:\\temp", (g, m) => g.IfNotUri(UriKind.RelativeOrAbsolute, m), ErrorCodes.NotUri, Expect.Argument, "'input' must be a valid URI.");
        yield return Fail("ftp://example.com/file.txt", (g, m) => g.IfNotHttpUrl(m), ErrorCodes.NotHttpUrl, Expect.Argument, "'input' must be an absolute HTTP or HTTPS URL.");
        yield return Fail("/relative/path", (g, m) => g.IfNotHttpUrl(m), ErrorCodes.NotHttpUrl, Expect.Argument, "'input' must be an absolute HTTP or HTTPS URL.");
        yield return Fail("ABC-123", (g, m) => g.IfAllUpperCase(m), ErrorCodes.AllUpperCase, Expect.Argument, "'input' must not be all uppercase.");
        yield return Fail("abc-123", (g, m) => g.IfAllLowerCase(m), ErrorCodes.AllLowerCase, Expect.Argument, "'input' must not be all lowercase.");

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        yield return Fail("12a", (g, m) => g.IfNotParsable<int>(message: m), ErrorCodes.NotParsable, Expect.Argument, "'input' must be a valid Int32.");
        yield return Fail("not-a-guid", (g, m) => g.IfNotParsable<Guid>(message: m), ErrorCodes.NotParsable, Expect.Argument, "'input' must be a valid Guid.");
        yield return Fail("1.5", (g, m) => g.IfNotParsable<decimal>(CultureInfo.GetCultureInfo("fr-FR"), m), ErrorCodes.NotParsable, Expect.Argument, "'input' must be a valid Decimal.");
        yield return Fail("2026-02-30", (g, m) => g.IfNotParsable<DateOnly>(message: m), ErrorCodes.NotParsable, Expect.Argument, "'input' must be a valid DateOnly.");
        yield return Null<string>((g, m) => g.IfNotParsable<int>(message: m));
#endif

        // A null string fails every content check with the "Null" code.
        yield return Null<string>((g, m) => g.IfNullOrEmpty(m));
        yield return Null<string>((g, m) => g.IfNullOrWhiteSpace(m));
        yield return Null<string>((g, m) => g.IfLongerThan(3, m));
        yield return Null<string>((g, m) => g.IfShorterThan(3, m));
        yield return Null<string>((g, m) => g.IfLengthNotEqualTo(3, m));
        yield return Null<string>((g, m) => g.IfLengthOutOfRange(1, 3, m));
        yield return Null<string>((g, m) => g.IfContains("a", message: m));
        yield return Null<string>((g, m) => g.IfNotContains("a", message: m));
        yield return Null<string>((g, m) => g.IfStartsWith("a", message: m));
        yield return Null<string>((g, m) => g.IfNotStartsWith("a", message: m));
        yield return Null<string>((g, m) => g.IfEndsWith("a", message: m));
        yield return Null<string>((g, m) => g.IfNotEndsWith("a", message: m));
        yield return Null<string>((g, m) => g.IfMatches("a", message: m));
        yield return Null<string>((g, m) => g.IfMatches(Lowercase, m));
        yield return Null<string>((g, m) => g.IfNotMatches("a", message: m));
        yield return Null<string>((g, m) => g.IfNotMatches(Lowercase, m));
        yield return Null<string>((g, m) => g.IfNotEmailAddress(m));
        yield return Null<string>((g, m) => g.IfNotJson(m));
        yield return Null<string>((g, m) => g.IfNotXml(m));
        yield return Null<string>((g, m) => g.IfNotUri(message: m));
        yield return Null<string>((g, m) => g.IfNotHttpUrl(m));
        yield return Null<string>((g, m) => g.IfAllUpperCase(m));
        yield return Null<string>((g, m) => g.IfAllLowerCase(m));
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass("a", (g, m) => g.IfNullOrEmpty(m));
        yield return Pass(" ", (g, m) => g.IfNullOrEmpty(m));
        yield return Pass(" a ", (g, m) => g.IfNullOrWhiteSpace(m));
        yield return Pass("abc", (g, m) => g.IfLongerThan(3, m));
        yield return Pass("", (g, m) => g.IfLongerThan(0, m));
        yield return Pass("abc", (g, m) => g.IfShorterThan(3, m));
        yield return Pass("", (g, m) => g.IfShorterThan(0, m));
        yield return Pass("abc", (g, m) => g.IfLengthNotEqualTo(3, m));
        yield return Pass("", (g, m) => g.IfLengthNotEqualTo(0, m));
        yield return Pass("ab", (g, m) => g.IfLengthOutOfRange(2, 3, m));
        yield return Pass("abc", (g, m) => g.IfLengthOutOfRange(2, 3, m));
        yield return Pass("", (g, m) => g.IfLengthOutOfRange(0, 0, m));
        yield return Pass("ADA", (g, m) => g.IfEqualTo("ada", StringComparison.Ordinal, m));
        yield return Pass<string>(null!, (g, m) => g.IfEqualTo("ada", StringComparison.Ordinal, m));
        yield return Pass("ADA", (g, m) => g.IfNotEqualTo("ada", StringComparison.OrdinalIgnoreCase, m));
        yield return Pass<string>(null!, (g, m) => g.IfNotEqualTo(null, StringComparison.Ordinal, m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfContains("LOVE", message: m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfNotContains("LOVE", StringComparison.OrdinalIgnoreCase, m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfNotContains("", message: m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfStartsWith("ada", message: m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfNotStartsWith("ADA", StringComparison.OrdinalIgnoreCase, m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfEndsWith("LACE", message: m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfNotEndsWith("LACE", StringComparison.OrdinalIgnoreCase, m));
        yield return Pass("ABC", (g, m) => g.IfMatches("^[a-z]+$", message: m));
        yield return Pass("ABC", (g, m) => g.IfMatches(Lowercase, m));
        yield return Pass("ABC", (g, m) => g.IfNotMatches("^[a-z]+$", RegexOptions.IgnoreCase, m));
        yield return Pass("abc", (g, m) => g.IfNotMatches(Lowercase, m));
        yield return Pass("ada@example.com", (g, m) => g.IfNotEmailAddress(m));
        yield return Pass("{\"name\":\"Ada Lovelace\",\"tags\":[1,2.5,true,null]}", (g, m) => g.IfNotJson(m));
        yield return Pass("42", (g, m) => g.IfNotJson(m));
        yield return Pass("<user name=\"Ada Lovelace\"><email>ada@example.com</email></user>", (g, m) => g.IfNotXml(m));
        yield return Pass("https://example.com/path?q=1#top", (g, m) => g.IfNotUri(message: m));
        yield return Pass("file:///etc/hosts", (g, m) => g.IfNotUri(UriKind.Absolute, m));
        yield return Pass("mailto:ada@example.com", (g, m) => g.IfNotUri(UriKind.Absolute, m));
        yield return Pass("/etc/hosts", (g, m) => g.IfNotUri(UriKind.Relative, m));
        yield return Pass("images/logo.png", (g, m) => g.IfNotUri(UriKind.Relative, m));
        yield return Pass("/etc/hosts", (g, m) => g.IfNotUri(UriKind.RelativeOrAbsolute, m));
        yield return Pass("https://example.com", (g, m) => g.IfNotUri(UriKind.RelativeOrAbsolute, m));
        yield return Pass("http://example.com", (g, m) => g.IfNotHttpUrl(m));
        yield return Pass("HTTPS://EXAMPLE.COM/a", (g, m) => g.IfNotHttpUrl(m));
        yield return Pass("https://localhost:8443/api", (g, m) => g.IfNotHttpUrl(m));
        yield return Pass("Abc", (g, m) => g.IfAllUpperCase(m));
        yield return Pass("123", (g, m) => g.IfAllUpperCase(m));
        yield return Pass("日本", (g, m) => g.IfAllUpperCase(m));
        yield return Pass("", (g, m) => g.IfAllUpperCase(m));
        yield return Pass("aBC", (g, m) => g.IfAllLowerCase(m));
        yield return Pass("123", (g, m) => g.IfAllLowerCase(m));
        yield return Pass("日本", (g, m) => g.IfAllLowerCase(m));

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        yield return Pass("-42", (g, m) => g.IfNotParsable<int>(message: m));
        yield return Pass("6f9619ff-8b86-d011-b42d-00cf4fc964ff", (g, m) => g.IfNotParsable<Guid>(message: m));
        yield return Pass("1.5", (g, m) => g.IfNotParsable<decimal>(message: m));
        yield return Pass("1,5", (g, m) => g.IfNotParsable<decimal>(CultureInfo.GetCultureInfo("fr-FR"), m));
        yield return Pass("2026-02-28", (g, m) => g.IfNotParsable<DateOnly>(message: m));
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

    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("a@b")]
    [InlineData("first.last@example.co.uk")]
    [InlineData("ada+tag@example.com")]
    [InlineData("o'brien@example.com")]
    [InlineData("\"quoted\"@example.com")]
    [InlineData("ada@[127.0.0.1]")]
    [InlineData("用户@例子.广告")]
    public void IfNotEmailAddress_accepts_plausible_addresses(string email) => email.Throw().IfNotEmailAddress();

    [Theory]
    [InlineData("")]
    [InlineData("ada")]
    [InlineData("@example.com")]
    [InlineData("ada@")]
    [InlineData("@")]
    [InlineData(".a@b")]
    [InlineData("a.@b")]
    [InlineData("a..b@c")]
    [InlineData("a@.b")]
    [InlineData("a@b.")]
    [InlineData("a@b..c")]
    [InlineData("a b@c")]
    [InlineData("a@b c")]
    [InlineData(" a@b")]
    [InlineData("a@b ")]
    [InlineData("a\t@b")]
    [InlineData("a@b\n")]
    [InlineData("a\u0001@b")]
    [InlineData("a@b\u00A0")]
    [InlineData("a@b@c")]
    public void IfNotEmailAddress_rejects_bad_addresses(string email)
    {
        var exception = Assert.Throws<ArgumentException>(() => email.Throw().IfNotEmailAddress());

        Assert.Equal("email", exception.ParamName);
    }

    [Theory]
    [InlineData("https://example.com", UriKind.Absolute)]
    [InlineData("file:///etc/hosts", UriKind.Absolute)]
    [InlineData("FILE:///C:/temp", UriKind.Absolute)]
    [InlineData("urn:isbn:0451450523", UriKind.Absolute)]
    [InlineData("relative/path", UriKind.Relative)]
    [InlineData("../up", UriKind.Relative)]
    [InlineData("?q=1", UriKind.Relative)]
    [InlineData("relative/path", UriKind.RelativeOrAbsolute)]
    [InlineData("https://example.com", UriKind.RelativeOrAbsolute)]
    public void IfNotUri_accepts_uris_of_the_requested_kind(string uri, UriKind kind) => uri.Throw().IfNotUri(kind);

    [Theory]
    [InlineData("/etc/hosts", UriKind.Absolute)]
    [InlineData("C:\\temp", UriKind.Absolute)]
    [InlineData("C:\\temp", UriKind.RelativeOrAbsolute)]
    [InlineData("\\\\server\\share", UriKind.Absolute)]
    [InlineData("relative/path", UriKind.Absolute)]
    [InlineData("http://", UriKind.Absolute)]
    [InlineData("https://example.com", UriKind.Relative)]
    [InlineData("", UriKind.Absolute)]
    public void IfNotUri_rejects_bare_file_paths_and_wrong_kinds(string uri, UriKind kind) =>
        Assert.Throws<ArgumentException>(() => uri.Throw().IfNotUri(kind));

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/a/b?c=d")]
    [InlineData("https://user@example.com:8080")]
    public void IfNotHttpUrl_accepts_http_and_https(string url) => url.Throw().IfNotHttpUrl();

    [Theory]
    [InlineData("example.com")]
    [InlineData("//example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/hosts")]
    [InlineData("/etc/hosts")]
    [InlineData("C:\\temp")]
    [InlineData("mailto:ada@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https//example.com")]
    public void IfNotHttpUrl_rejects_other_schemes_and_paths(string url) =>
        Assert.Throws<ArgumentException>(() => url.Throw().IfNotHttpUrl());

    [Fact]
    public void IfNotXml_rejects_a_doctype_and_never_resolves_entities()
    {
        var xxe = "<?xml version=\"1.0\"?><!DOCTYPE foo [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><foo>&xxe;</foo>";
        var laughs = "<?xml version=\"1.0\"?><!DOCTYPE lolz [<!ENTITY lol \"lol\"><!ENTITY lol2 \"&lol;&lol;&lol;&lol;\">]><lolz>&lol2;</lolz>";
        var plainDoctype = "<!DOCTYPE html><html></html>";

        Assert.Throws<ArgumentException>(() => xxe.Throw().IfNotXml());
        Assert.Throws<ArgumentException>(() => laughs.Throw().IfNotXml());
        Assert.Throws<ArgumentException>(() => plainDoctype.Throw().IfNotXml());
    }

    [Theory]
    [InlineData("<a/>")]
    [InlineData("<?xml version=\"1.0\" encoding=\"utf-8\"?><a b=\"c\">text<!-- note --><![CDATA[<raw>]]></a>")]
    [InlineData("  <a>&lt;&amp;&gt;&#65;</a>  ")]
    [InlineData("<ns:a xmlns:ns=\"urn:example\"><ns:b/></ns:a>")]
    public void IfNotXml_accepts_well_formed_documents(string xml) => xml.Throw().IfNotXml();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("text")]
    [InlineData("<a>")]
    [InlineData("<a></b>")]
    [InlineData("<a/><b/>")]
    [InlineData("<a b=c/>")]
    [InlineData("<a>&undefined;</a>")]
    [InlineData(" <?xml version=\"1.0\"?><a/>")]
    public void IfNotXml_rejects_malformed_documents(string xml) =>
        Assert.Throws<ArgumentException>(() => xml.Throw().IfNotXml());

    [Theory]
    [InlineData("ABC", true, false)]
    [InlineData("ABC 123 !?", true, false)]
    [InlineData("ÀÉÎ", true, false)]
    [InlineData("abc", false, true)]
    [InlineData("abc 123", false, true)]
    [InlineData("éß", false, true)]
    [InlineData("Abc", false, false)]
    [InlineData("123", false, false)]
    [InlineData("日本", false, false)]
    [InlineData("日本A", true, false)]
    [InlineData("", false, false)]
    public void Case_checks_look_only_at_cased_letters(string value, bool allUpper, bool allLower)
    {
        Assert.Equal(allUpper, Record.Exception(() => value.Throw().IfAllUpperCase()) is ArgumentException);
        Assert.Equal(allLower, Record.Exception(() => value.Throw().IfAllLowerCase()) is ArgumentException);
    }

    [Fact]
    public void Messages_never_contain_the_string_value()
    {
        var password = "hunter2-secret";
        var checks = new List<Exception?>
        {
            Record.Exception(() => password.Throw().IfLongerThan(3)),
            Record.Exception(() => password.Throw().IfLengthNotEqualTo(3)),
            Record.Exception(() => password.Throw().IfMatches("hunter")),
            Record.Exception(() => password.Throw().IfNotEmailAddress()),
            Record.Exception(() => password.Throw().IfNotJson()),
            Record.Exception(() => password.Throw().IfNotXml()),
            Record.Exception(() => password.Throw().IfNotUri()),
            Record.Exception(() => password.Throw().IfNotHttpUrl()),
            Record.Exception(() => password.Throw().IfAllLowerCase()),
            Record.Exception(() => password.Throw().IfOneOf("hunter2-secret")),
        };

        Assert.All(checks, exception =>
        {
            Assert.NotNull(exception);
            Assert.DoesNotContain("hunter2", exception.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Negative_lengths_throw_even_in_collect_mode()
    {
        var report = new ValidationReport();

        Assert.Equal("maxLength", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("a", "s").IfLongerThan(-1)).ParamName);
        Assert.Equal("minLength", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("a", "s").IfShorterThan(-1)).ParamName);
        Assert.Equal("length", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("a", "s").IfLengthNotEqualTo(-1)).ParamName);
        Assert.Equal("minLength", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("a", "s").IfLengthOutOfRange(-1, 3)).ParamName);
        Assert.Equal("maxLength", Assert.Throws<ArgumentOutOfRangeException>(() => report.Check("a", "s").IfLengthOutOfRange(3, 2)).ParamName);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Null_arguments_of_string_checks_throw_ArgumentNullException()
    {
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfContains(null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfNotContains(null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfStartsWith(null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfNotStartsWith(null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfEndsWith(null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfNotEndsWith(null!)).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfMatches((string)null!)).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfNotMatches((string)null!)).ParamName);
        Assert.Equal("regex", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfMatches((Regex)null!)).ParamName);
        Assert.Equal("regex", Assert.Throws<ArgumentNullException>(() => "a".Throw().IfNotMatches((Regex)null!)).ParamName);
    }

    [Fact]
    public void Invalid_pattern_throws_the_regex_error()
    {
        Assert.ThrowsAny<ArgumentException>(() => "a".Throw().IfMatches("("));
    }

    [Fact]
    public void Contains_with_a_char_uses_the_collection_check()
    {
        Assert.Throws<ArgumentException>(() => "abc".Throw().IfContains('b'));
        "abc".Throw().IfNotContains('b');
    }

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    [Fact]
    public void IfNotParsable_uses_the_invariant_culture_by_default()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            "1.5".Throw().IfNotParsable<double>();
            Assert.Throws<ArgumentException>(() => "1,5e".Throw().IfNotParsable<double>());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
#endif
}
