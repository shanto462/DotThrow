using System;
using System.Linq;
using System.Net;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Fuzz tests for email addresses, IP addresses (as text and as <see cref="IPAddress"/>) and
/// <see cref="System.Uri"/> values: the contract, and agreement with the documented rules
/// (<see cref="Reference"/>) and with the BCL where the documentation promises the same result.
/// </summary>
[Trait(Fuzz.Trait, Fuzz.Category)]
public sealed class AddressFuzzTests
{
    private static readonly Arbitrary<string?> EmailText = Generators.Of(Generators.EmailLike);

    private static readonly Arbitrary<string?> IPText = Generators.Of(Generators.Mutated(
        Gen.Frequency((1, Generators.IPv4Text), (2, Generators.IPv6Text)),
        "0123456789abcdefABCDEF:.%[]"));

    [Fact]
    public void NotEmailAddress_follows_the_documented_rules() =>
        Fuzz.Run(EmailText, text =>
        {
            var expected = text is not null && Reference.IsEmailAddress(text);
            var fails = Contract.Fails(text, g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress);
            Assert.True(expected != fails, $"The documented rules say {(expected ? "valid" : "not valid")} for {Contract.Show(text)}.");
        });

    [Fact]
    public void NotIPAddress_follows_the_documented_rules() =>
        Fuzz.Run(IPText, text =>
        {
            var expected = text is not null && Reference.IsIPAddress(text);
            var fails = Contract.Fails(text, g => g.IfNotIPAddress(), ErrorCodes.NotIPAddress);
            Assert.True(expected != fails, $"The documented rules say {(expected ? "valid" : "not valid")} for {Contract.Show(text)}.");
        });

#if NET
    // Only on .NET: .NET Framework's IPAddress.TryParse reads some forms differently (D30).
    // IPv4 text made of digits and dots is valid exactly when the BCL reads it as IPv4 and writes
    // it back the same (no short, octal or zero-padded forms).
    [Fact]
    public void NotIPAddress_agrees_with_IPAddress_on_dotted_decimal_text() =>
        Fuzz.Run(Generators.Of(Generators.Mutated(Generators.IPv4Text, "0123456789.")), text =>
        {
            if (text is null || text.Any(c => c is not ('.' or (>= '0' and <= '9'))))
            {
                return;
            }

            var canonical = IPAddress.TryParse(text, out var address) && Reference.IsIPv4(address) && address.ToString() == text;
            Assert.True(canonical != Contract.Fails(text, g => g.IfNotIPAddress(), ErrorCodes.NotIPAddress), $"IPAddress says {canonical} for {Contract.Show(text)}.");
        });

    // A valid IPv6 address passes and means the same 16 bytes to the BCL; any IPv6 text that
    // passes (without a zone) is one the BCL reads too.
    [Fact]
    public void NotIPAddress_agrees_with_IPAddress_on_IPv6_text()
    {
        Fuzz.Run(Arb.From(Generators.ValidIPv6), address =>
        {
            Assert.False(Contract.Fails(address.Text, g => g.IfNotIPAddress(), ErrorCodes.NotIPAddress), Contract.Show(address.Text));
            Assert.Equal(address.Bytes, IPAddress.Parse(address.Text).GetAddressBytes());
        });

        Fuzz.Run(IPText, text =>
        {
            if (text is not null && text.IndexOf('%') < 0 && !Contract.Fails(text, g => g.IfNotIPAddress(), ErrorCodes.NotIPAddress))
            {
                Assert.True(IPAddress.TryParse(text, out _), $"IPAddress cannot read {Contract.Show(text)}.");
            }
        });
    }
#endif

    private static readonly Arbitrary<IPAddress?> Addresses = Arb.From(Gen.Frequency(
        (1, Gen.Constant<IPAddress?>(null)),
        (4, Gen.Choose(0, 255).Select(b => (byte)b).ArrayOf(4).Select(bytes => (IPAddress?)new IPAddress(bytes))),
        (2, Gen.Elements<byte>(0, 10, 100, 127, 169, 172, 192, 255).SelectMany(first => Gen.Choose(0, 255).Select(second => (IPAddress?)new IPAddress([first, (byte)second, 0, 1])))),
        (3, Gen.Zip(Gen.Choose(0, 255).Select(b => (byte)b).ArrayOf(16), Gen.Elements(0L, 0L, 1L, 4294967295L)).Select(pair => (IPAddress?)new IPAddress(pair.Item1, pair.Item2))),
        (2, Gen.Zip(Gen.Elements<byte>(0xFC, 0xFD, 0xFE, 0xFF, 0x00), Gen.Choose(0, 255)).Select(pair =>
        {
            var bytes = new byte[16];
            bytes[0] = pair.Item1;
            bytes[1] = (byte)pair.Item2;
            return (IPAddress?)new IPAddress(bytes);
        })),
        (2, Gen.Choose(0, 255).Select(b => (byte)b).ArrayOf(4).Select(bytes => (IPAddress?)new IPAddress(bytes).MapToIPv6())),
        (1, Gen.Elements<IPAddress?>(IPAddress.IPv6Loopback, IPAddress.IPv6Any, IPAddress.IPv6None, IPAddress.Loopback, IPAddress.Any, IPAddress.Broadcast, IPAddress.Parse("::1%7"), IPAddress.Parse("::127.0.0.1")))));

    [Fact]
    public void IPAddress_checks_follow_the_documented_ranges() =>
        Fuzz.Run(Addresses, address =>
        {
            Assert.Equal(address is null || Reference.IsLoopback(address), Contract.Fails(address, g => g.IfLoopback(), ErrorCodes.Loopback));
            Assert.Equal(address is null || Reference.IsPrivate(address), Contract.Fails(address, g => g.IfPrivate(), ErrorCodes.Private));
            Assert.Equal(address is null || !Reference.IsIPv4(address), Contract.Fails(address, g => g.IfNotIPv4(), ErrorCodes.NotIPv4));
            Assert.Equal(address is null || Reference.IsIPv4(address), Contract.Fails(address, g => g.IfNotIPv6(), ErrorCodes.NotIPv6));
        });

    private static readonly Arbitrary<Uri?> Uris = Arb.From(Gen.Frequency(
        (1, Gen.Constant<Uri?>(null)),
        (12, Generators.UriLike.Select(Generators.ToUri))));

    [Fact]
    public void Uri_checks_follow_the_documented_rules() =>
        Fuzz.Run(Uris, uri =>
        {
            var absolute = uri is not null && uri.IsAbsoluteUri;
            var scheme = absolute ? uri!.Scheme : null;
            Assert.Equal(!absolute, Contract.Fails(uri, g => g.IfRelative(), ErrorCodes.Relative));
            Assert.Equal(scheme != "https", Contract.Fails(uri, g => g.IfNotHttps(), ErrorCodes.NotHttps));
            Assert.Equal(scheme is not ("http" or "https"), Contract.Fails(uri, g => g.IfNotHttpOrHttps(), ErrorCodes.NotHttpOrHttps));
            Assert.Equal(
                scheme is null || !string.Equals(scheme, "WSS", StringComparison.OrdinalIgnoreCase) && scheme != "https",
                Contract.Fails(uri, g => g.IfNotScheme("HTTPS", "wss"), ErrorCodes.NotScheme));

            // IfLoopback fails for every documented local host; anything else it rejects must be
            // a host the BCL itself calls loopback.
            var loopback = Contract.Fails(uri, g => g.IfLoopback(), ErrorCodes.Loopback);
            var documented = uri is null || IsDocumentedLoopback(uri);
            Assert.True(!documented || loopback, $"IfLoopback passed {uri}, a local host.");
            Assert.True(!loopback || documented || uri!.IsLoopback, $"IfLoopback rejected {uri}, which is not a local host.");
        });

    // localhost, *.localhost (with a trailing dot too), and loopback IP addresses, also written
    // with a trailing dot (127.0.0.1.). A relative URI has no host.
    private static bool IsDocumentedLoopback(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        switch (uri.HostNameType)
        {
            case UriHostNameType.IPv4:
            case UriHostNameType.IPv6:
                return IPAddress.TryParse(uri.Host, out var address) && Reference.IsLoopback(address);
            case UriHostNameType.Dns:
                var host = uri.Host.Length > 0 && uri.Host[uri.Host.Length - 1] == '.' ? uri.Host.Substring(0, uri.Host.Length - 1) : uri.Host;
                return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                    || (host.Length > ".localhost".Length && host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
                    || (Reference.IsIPv4(host) && host.StartsWith("127.", StringComparison.Ordinal));
            default:
                return false;
        }
    }
}
