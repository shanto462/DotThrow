using System;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>Review fix: IfNotEmailAddress follows an RFC 5321 subset (with RFC 6531 letters).</summary>
public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    [InlineData("o'brien@example.com")]
    [InlineData("!#$%&'*+-/=?^_`{|}~@example.com")]
    [InlineData("\"a b\"@c.com")]
    [InlineData("\"a\\\"b@c\"@example.com")]
    [InlineData("\"\"@example.com")]
    [InlineData("ada@[127.0.0.1]")]
    [InlineData("ada@[IPv6:2001:db8::1]")]
    [InlineData("ada@[ipv6:::1]")]
    [InlineData("ada@a-b.example")]
    [InlineData("ada@xn--bcher-kva.example")]
    [InlineData("用户@例子.广告")]
    [InlineData("a@b")]
    public void Valid_addresses_pass(string address) => Check.Passes(address, g => g.IfNotEmailAddress());

    [Theory]
    [InlineData("a@b;c.com")]
    [InlineData("<a@b.com>")]
    [InlineData("a@[1.2.3.4")]
    [InlineData("a@[1.2.3.400]")]
    [InlineData("a@[IPv6:12345::]")]
    [InlineData("a@[IPv6:fe80::1%eth0]")]
    [InlineData("a@-b.com")]
    [InlineData("a@b-.com")]
    [InlineData("a@b:80")]
    [InlineData("a@b_c.com")]
    [InlineData("a@b.com.")]
    [InlineData("a b@c.com")]
    [InlineData("\"a\tb\"@c.com")]
    [InlineData("\"a\"b@c.com")]
    [InlineData("\"unclosed@c.com")]
    [InlineData("a(comment)@c.com")]
    [InlineData("a@b@c")]
    [InlineData("a@b ")]
    public void Invalid_addresses_fail(string address) =>
        Check.Fails<ArgumentException, string>(address, g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress);

    [Fact]
    public void Length_limits_follow_RFC_5321()
    {
        var local64 = new string('a', 64);
        Check.Passes(local64 + "@example.com", g => g.IfNotEmailAddress());
        Check.Fails<ArgumentException, string>(new string('a', 65) + "@example.com", g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress);

        // A label is at most 63 characters, a domain at most 253, an address at most 254.
        Check.Passes("a@" + new string('b', 63) + ".com", g => g.IfNotEmailAddress());
        Check.Fails<ArgumentException, string>("a@" + new string('b', 64) + ".com", g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress);
        var host300 = string.Join(".", new string('h', 60), new string('h', 60), new string('h', 60), new string('h', 60), new string('h', 57));
        Check.Fails<ArgumentException, string>("a@" + host300, g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress);
        var domain249 = string.Join(".", new string('d', 62), new string('d', 62), new string('d', 62), new string('d', 60));
        Check.Passes("abc@" + domain249, g => g.IfNotEmailAddress());
        Check.Fails<ArgumentException, string>(new string('a', 10) + "@" + domain249, g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress);

        // Non-ASCII characters count as UTF-8 bytes: 22 three-byte characters are 66 bytes.
        Check.Fails<ArgumentException, string>(new string('用', 22) + "@example.com", g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress);
    }
}
