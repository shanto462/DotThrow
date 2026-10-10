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
