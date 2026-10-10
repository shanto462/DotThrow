using System;
using System.Collections.Generic;
using System.Net;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests.Guards;

// Case tables for the Uri and IPAddress checks added in v2. UriAndAddressTests holds the edge cases.
public sealed class NetworkGuardTests
{
    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(new Uri("/orders/1", UriKind.Relative), (g, m) => g.IfRelative(m), ErrorCodes.Relative, Expect.Argument, "'input' must be an absolute URI.");
        yield return Fail(new Uri("http://example.com"), (g, m) => g.IfNotHttps(m), ErrorCodes.NotHttps, Expect.Argument, "'input' must be an absolute HTTPS URL.");
        yield return Fail(new Uri("/a", UriKind.Relative), (g, m) => g.IfNotHttps(m), ErrorCodes.NotHttps, Expect.Argument, "'input' must be an absolute HTTPS URL.");
        yield return Fail(new Uri("ftp://example.com"), (g, m) => g.IfNotHttpOrHttps(m), ErrorCodes.NotHttpOrHttps, Expect.Argument, "'input' must be an absolute HTTP or HTTPS URL.");
        yield return Fail(new Uri("http://localhost:8080"), (g, m) => g.IfLoopback(m), ErrorCodes.Loopback, Expect.Argument, "'input' must not be a loopback address.");
        yield return Fail(new Uri("https://127.0.0.1"), (g, m) => g.IfLoopback(m), ErrorCodes.Loopback, Expect.Argument, "'input' must not be a loopback address.");
        yield return Fail(new Uri("ftp://example.com"), (g, m) => g.IfNotScheme(["https", "sftp"], m), ErrorCodes.NotScheme, Expect.Argument, "'input' must use one of these URI schemes: https, sftp.");
        yield return Fail(new Uri("ftp://example.com"), (g, m) => g.IfNotScheme(new[] { "https", "sftp" }, m), ErrorCodes.NotScheme, Expect.Argument, "'input' must use one of these URI schemes: https, sftp.");
        yield return Fail(new Uri("/a", UriKind.Relative), (g, m) => g.IfNotScheme(new[] { "https" }, m), ErrorCodes.NotScheme, Expect.Argument, "'input' must use one of these URI schemes: https.");
        yield return Fail(new Uri("/a", UriKind.Relative), (g, m) => g.IfNotScheme(["https"], m), ErrorCodes.NotScheme, Expect.Argument, "'input' must use one of these URI schemes: https.");
        yield return Null<Uri>((g, m) => g.IfNotScheme(new[] { "https" }, m));
        yield return Fail(IPAddress.Loopback, (g, m) => g.IfLoopback(m), ErrorCodes.Loopback, Expect.Argument, "'input' must not be a loopback address.");
        yield return Fail(IPAddress.IPv6Loopback, (g, m) => g.IfLoopback(m), ErrorCodes.Loopback, Expect.Argument, "'input' must not be a loopback address.");
        yield return Fail(IPAddress.Parse("10.1.2.3"), (g, m) => g.IfPrivate(m), ErrorCodes.Private, Expect.Argument, "'input' must not be a private or link-local address.");
        yield return Fail(IPAddress.Parse("fe80::1"), (g, m) => g.IfPrivate(m), ErrorCodes.Private, Expect.Argument, "'input' must not be a private or link-local address.");
        yield return Fail(IPAddress.IPv6Loopback, (g, m) => g.IfNotIPv4(m), ErrorCodes.NotIPv4, Expect.Argument, "'input' must be an IPv4 address.");
        yield return Fail(IPAddress.Loopback, (g, m) => g.IfNotIPv6(m), ErrorCodes.NotIPv6, Expect.Argument, "'input' must be an IPv6 address.");

        yield return Null<Uri>((g, m) => g.IfRelative(m));
        yield return Null<Uri>((g, m) => g.IfNotHttps(m));
        yield return Null<Uri>((g, m) => g.IfNotHttpOrHttps(m));
        yield return Null<Uri>((g, m) => g.IfLoopback(m));
        yield return Null<Uri>((g, m) => g.IfNotScheme(["https"], m));
        yield return Null<IPAddress>((g, m) => g.IfLoopback(m));
        yield return Null<IPAddress>((g, m) => g.IfPrivate(m));
        yield return Null<IPAddress>((g, m) => g.IfNotIPv4(m));
        yield return Null<IPAddress>((g, m) => g.IfNotIPv6(m));
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(new Uri("https://example.com"), (g, m) => g.IfRelative(m));
        yield return Pass(new Uri("https://example.com"), (g, m) => g.IfNotHttps(m));
        yield return Pass(new Uri("http://example.com"), (g, m) => g.IfNotHttpOrHttps(m));
        yield return Pass(new Uri("https://example.com"), (g, m) => g.IfLoopback(m));
        yield return Pass(new Uri("/a", UriKind.Relative), (g, m) => g.IfLoopback(m));
        yield return Pass(new Uri("SFTP://example.com"), (g, m) => g.IfNotScheme(["https", "sftp"], m));
        yield return Pass(new Uri("Https://example.com"), (g, m) => g.IfNotScheme(new[] { "sftp", "https" }, m));
        yield return Pass(IPAddress.Parse("8.8.8.8"), (g, m) => g.IfLoopback(m));
        yield return Pass(IPAddress.Parse("8.8.8.8"), (g, m) => g.IfPrivate(m));
        yield return Pass(IPAddress.Parse("8.8.8.8"), (g, m) => g.IfNotIPv4(m));
        yield return Pass(IPAddress.Parse("2001:db8::1"), (g, m) => g.IfNotIPv6(m));
    }

    [Fact]
    public void Params_array_form_without_a_message()
    {
        Assert.Throws<ArgumentException>(() => new Uri("ftp://example.com").Throw().IfNotScheme(new[] { "https" }));
        new Uri("https://example.com").Throw().IfNotScheme(new[] { "https" });
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
}
