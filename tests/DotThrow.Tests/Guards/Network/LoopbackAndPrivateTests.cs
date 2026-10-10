using System;
using System.Net;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>Review fix: loopback hosts with a trailing dot or IPv4-mapped, and the RFC 6598 range.</summary>
public sealed class LoopbackAndPrivateTests
{
    [Theory]
    [InlineData("http://localhost./")]
    [InlineData("http://LOCALHOST./x")]
    [InlineData("http://api.localhost./")]
    [InlineData("http://127.0.0.1./")]
    [InlineData("http://127.1.2.3./")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://2130706433/")]
    public void Loopback_hosts_in_every_spelling_fail(string text) =>
        Check.Fails<ArgumentException, Uri>(new Uri(text), g => g.IfLoopback(), ErrorCodes.Loopback);

    [Theory]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://[::]/")]
    [InlineData("http://127.example.com/")]
    [InlineData("http://localhost.example.com./")]
    [InlineData("http://127.0.0.1.example/")]
    public void Unspecified_addresses_and_look_alike_names_are_not_loopback(string text) =>
        Check.Passes(new Uri(text), g => g.IfLoopback());

    [Theory]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.127.255.255", true)]
    [InlineData("::ffff:100.64.0.1", true)]
    [InlineData("100.63.255.255", false)]
    [InlineData("100.128.0.0", false)]
    public void Shared_address_space_is_private(string text, bool isPrivate)
    {
        var address = IPAddress.Parse(text);
        if (isPrivate)
        {
            Check.Fails<ArgumentException, IPAddress>(address, g => g.IfPrivate(), ErrorCodes.Private);
        }
        else
        {
            Check.Passes(address, g => g.IfPrivate());
        }
    }
}
