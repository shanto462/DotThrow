using System;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>
/// Review fix: IfNotUri and IfNotHttpUrl judge the text as written. Uri.TryCreate trims white
/// space and escapes characters a URI may not contain, so the checks reject such text first.
/// </summary>
public sealed class UriTextTests
{
    [Theory]
    [InlineData(" http://a.com ")]
    [InlineData("http://a.com ")]
    [InlineData("\thttp://a.com")]
    [InlineData("http://a.com/\r\nSet-Cookie: x=1")]
    [InlineData("http://a.com/a b")]
    [InlineData("http://a.com/a\0b")]
    [InlineData("http://a.com/<script>")]
    [InlineData("http://a.com/a>b")]
    [InlineData("http://a.com/\"x\"")]
    [InlineData("http://a.com/a{b}")]
    [InlineData("http://a.com/a|b")]
    [InlineData("http://a.com/a^b")]
    [InlineData("http://a.com/a`b")]
    [InlineData("http://a.com/a\u007Fb")]
    [InlineData("http://a.com/%zz")]
    [InlineData("http://a.com/%4")]
    [InlineData("http://a.com/a#b#c")]
    [InlineData("http://a.com/‮evil")]
    [InlineData("http://a.com/a​b")]
    [InlineData("http://a.com/a b")]
    [InlineData("http://a.com/a\u0085b")]
    public void Text_that_is_not_a_uri_as_written_fails(string text)
    {
        Check.Fails<ArgumentException, string>(text, g => g.IfNotUri(), ErrorCodes.NotUri);
        Check.Fails<ArgumentException, string>(text, g => g.IfNotHttpUrl(), ErrorCodes.NotHttpUrl);
        Check.Fails<ArgumentException, string>(text, g => g.IfNotUri(UriKind.RelativeOrAbsolute), ErrorCodes.NotUri);
    }

    // Fuzzing fix: a surrogate that is not part of a pair is not a character (Uri replaced it
    // with U+FFFD), and a format character outside the Basic Multilingual Plane, such as the
    // invisible tag characters, is a format character too. Facts, not theory data: test case
    // names with a lone surrogate are not valid UTF-16.
    [Fact]
    public void Lone_surrogates_and_supplementary_format_characters_fail()
    {
        foreach (var text in new[] { "http://a.com/\uD800", "http://a.com/\uDC00x", "http://a.com/?q=\uDBFF", "http://a.com/#\uDFFF", "http://a.com/\U000E0001", "http://a.com/a\U000E0041b", "http://a.com/a\U0001D173b", "http://a.com/\U0001F3F4\U000E0067\U000E0062\U000E0073\U000E0063\U000E0074\U000E007F" })
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotUri(), ErrorCodes.NotUri);
            Check.Fails<ArgumentException, string>(text, g => g.IfNotHttpUrl(), ErrorCodes.NotHttpUrl);
            Check.Fails<ArgumentException, string>(text, g => g.IfNotUri(UriKind.RelativeOrAbsolute), ErrorCodes.NotUri);
        }

        foreach (var text in new[] { "\uD800", "a/\uDC00", "a\U000E0001" })
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotUri(UriKind.Relative), ErrorCodes.NotUri);
        }

        Check.Passes("https://example.com/\U0001D400\U00020000", g => g.IfNotUri());
        Check.Passes("a/\U0001F600", g => g.IfNotUri(UriKind.Relative));
    }

    [Theory]
    [InlineData("http://a.com/a%20b")]
    [InlineData("https://example.com/path?q=1&r=2#top")]
    [InlineData("https://xn--bcher-kva.example/")]
    [InlineData("https://bücher.example/straße")]
    [InlineData("https://[::1]:8080/")]
    [InlineData("https://user:pw@example.com/")]
    [InlineData("https://example.com/a;b=c,d!$'()*+")]
    [InlineData("https://example.com/~user/_x-y.z")]
    [InlineData("https://example.com/😀")]
    public void Valid_urls_and_iris_pass(string text)
    {
        Check.Passes(text, g => g.IfNotUri());
        Check.Passes(text, g => g.IfNotHttpUrl());
    }

    [Theory]
    [InlineData("mailto:ada@example.com", UriKind.Absolute)]
    [InlineData("urn:isbn:0451450523", UriKind.Absolute)]
    [InlineData("a/b%2Fc", UriKind.Relative)]
    [InlineData("?q=a+b", UriKind.Relative)]
    public void Other_valid_uris_pass(string text, UriKind kind) =>
        Check.Passes(text, g => g.IfNotUri(kind));
}
