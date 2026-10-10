using System;
using System.Net;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

public sealed class UriAndAddressTests
{
    [Fact]
    public void Uri_checks()
    {
        var https = new Uri("https://example.com/a");
        var http = new Uri("http://example.com/a");
        var relative = new Uri("/a", UriKind.Relative);

        Check.Passes(https, g => g.IfRelative().IfNotHttps().IfNotHttpOrHttps().IfLoopback().IfNotScheme("https", "wss"));
        Check.Fails<ArgumentException, Uri>(relative, g => g.IfRelative(), ErrorCodes.Relative, "'input' must be an absolute URI.");
        Check.Fails<ArgumentException, Uri>(http, g => g.IfNotHttps(), ErrorCodes.NotHttps, "'input' must be an absolute HTTPS URL.");
        Check.Fails<ArgumentException, Uri>(relative, g => g.IfNotHttps(), ErrorCodes.NotHttps);
        Check.Passes(http, g => g.IfNotHttpOrHttps());
        Check.Fails<ArgumentException, Uri>(new Uri("ftp://example.com"), g => g.IfNotHttpOrHttps(), ErrorCodes.NotHttpOrHttps, "'input' must be an absolute HTTP or HTTPS URL.");
        Check.Fails<ArgumentException, Uri>(http, g => g.IfNotScheme("https", "wss"), ErrorCodes.NotScheme, "'input' must use one of these URI schemes: https, wss.");
        Check.Passes(new Uri("WSS://example.com"), g => g.IfNotScheme("https", "WSS"));
        Check.Fails<ArgumentException, Uri>(relative, g => g.IfNotScheme("https"), ErrorCodes.NotScheme);
        Check.Fails<ArgumentNullException, Uri>(null!, g => g.IfRelative(), ErrorCodes.Null);
        Assert.Throws<ArgumentNullException>(() => https.Throw().IfNotScheme((string[])null!, null));
    }

    [Theory]
    [InlineData("http://localhost/", true)]
    [InlineData("http://api.localhost/", true)]
    [InlineData("http://127.0.0.1/", true)]
    [InlineData("http://127.1.2.3/", true)]
    [InlineData("http://[::1]/", true)]
    [InlineData("http://example.com/", false)]
    [InlineData("http://localhost.example.com/", false)]
    [InlineData("http://10.0.0.1/", false)]
    public void Uri_IfLoopback(string text, bool loopback)
    {
        if (loopback)
        {
            Check.Fails<ArgumentException, Uri>(new Uri(text), g => g.IfLoopback(), ErrorCodes.Loopback, "'input' must not be a loopback address.");
        }
        else
        {
            Check.Passes(new Uri(text), g => g.IfLoopback());
        }
    }

    [Theory]
    [InlineData("http://LOCALHOST:8080/")]
    [InlineData("http://LocalHost/")]
    public void Uri_IfLoopback_ignores_the_case_of_localhost(string text)
    {
        // .NET 8 Uri.IsLoopback is false for mixed-case "localhost"; the check must still fail
        // on every runtime.
        Check.Fails<ArgumentException, Uri>(new Uri(text), g => g.IfLoopback(), ErrorCodes.Loopback, "'input' must not be a loopback address.");
    }

    [Fact]
    public void A_relative_uri_is_not_a_loopback_address() => Check.Passes(new Uri("/a", UriKind.Relative), g => g.IfLoopback());

    [Theory]
    [InlineData("127.0.0.1", true, false)]
    [InlineData("127.255.0.9", true, false)]
    [InlineData("::1", true, false)]
    [InlineData("::ffff:127.0.0.1", true, false)]
    [InlineData("10.1.2.3", false, true)]
    [InlineData("172.16.0.1", false, true)]
    [InlineData("172.31.255.255", false, true)]
    [InlineData("172.32.0.1", false, false)]
    [InlineData("172.15.0.1", false, false)]
    [InlineData("192.168.1.1", false, true)]
    [InlineData("192.169.1.1", false, false)]
    [InlineData("169.254.0.5", false, true)]
    [InlineData("8.8.8.8", false, false)]
    [InlineData("fc00::1", false, true)]
    [InlineData("fdff::1", false, true)]
    [InlineData("fe80::1", false, true)]
    [InlineData("febf::1", false, true)]
    [InlineData("fec0::1", false, false)]
    [InlineData("2001:db8::1", false, false)]
    [InlineData("::ffff:10.0.0.1", false, true)]
    [InlineData("::ffff:8.8.8.8", false, false)]
    public void IPAddress_ranges(string text, bool loopback, bool isPrivate)
    {
        var address = IPAddress.Parse(text);
        if (loopback)
        {
            Check.Fails<ArgumentException, IPAddress>(address, g => g.IfLoopback(), ErrorCodes.Loopback);
        }
        else
        {
            Check.Passes(address, g => g.IfLoopback());
        }

        if (isPrivate)
        {
            Check.Fails<ArgumentException, IPAddress>(address, g => g.IfPrivate(), ErrorCodes.Private, "'input' must not be a private or link-local address.");
        }
        else
        {
            Check.Passes(address, g => g.IfPrivate());
        }
    }

    [Fact]
    public void IPv4_and_IPv6_family_checks()
    {
        Check.Passes(IPAddress.Parse("1.2.3.4"), g => g.IfNotIPv4());
        Check.Fails<ArgumentException, IPAddress>(IPAddress.Parse("::ffff:1.2.3.4"), g => g.IfNotIPv4(), ErrorCodes.NotIPv4, "'input' must be an IPv4 address.");
        Check.Passes(IPAddress.IPv6Loopback, g => g.IfNotIPv6());
        Check.Fails<ArgumentException, IPAddress>(IPAddress.Loopback, g => g.IfNotIPv6(), ErrorCodes.NotIPv6, "'input' must be an IPv6 address.");
        Check.Fails<ArgumentNullException, IPAddress>(null!, g => g.IfPrivate(), ErrorCodes.Null);
    }
}
