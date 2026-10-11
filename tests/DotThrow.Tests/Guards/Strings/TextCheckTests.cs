using System;
using DotThrow.Tests.Support;
using Xunit;
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System.Buffers;
#endif

namespace DotThrow.Tests.Guards;

public sealed class TextCheckTests
{
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    private static readonly SearchValues<char> Brackets = SearchValues.Create("<>");
#endif

    [Theory]
    [InlineData("abc", true)]
    [InlineData("", true)]
    [InlineData("a b", true)]
    [InlineData(" abc", false)]
    [InlineData("abc\n", false)]
    [InlineData(" abc", false)]
    public void IfNotTrimmed(string text, bool trimmed)
    {
        if (trimmed)
        {
            Check.Passes(text, g => g.IfNotTrimmed());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotTrimmed(), ErrorCodes.NotTrimmed, "'input' must not start or end with white space.");
        }
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData("a b", true)]
    [InlineData("a\tb", true)]
    [InlineData("a　b", true)]
    [InlineData("a​b", false)]
    public void IfContainsWhiteSpace(string text, bool hasWhiteSpace)
    {
        if (hasWhiteSpace)
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfContainsWhiteSpace(), ErrorCodes.ContainsWhiteSpace, "'input' must not contain white space.");
        }
        else
        {
            Check.Passes(text, g => g.IfContainsWhiteSpace());
        }
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("a\0b", true)]
    [InlineData("a\tb", true)]
    [InlineData("a\u007Fb", true)]
    [InlineData("a\u009Fb", true)]
    [InlineData("a b", false)]
    public void IfContainsControlCharacters(string text, bool hasControl)
    {
        if (hasControl)
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfContainsControlCharacters(), ErrorCodes.ContainsControlCharacters, "'input' must not contain control characters.");
        }
        else
        {
            Check.Passes(text, g => g.IfContainsControlCharacters());
        }
    }

    [Fact]
    public void IfContainsAny_and_IfContainsAnyExcept()
    {
        Check.Passes("abc", g => g.IfContainsAny('<', '>'));
        Check.Fails<ArgumentException, string>("a<b", g => g.IfContainsAny('<', '>'), ErrorCodes.ContainsAny, "'input' must not contain any of the rejected characters.");
        Assert.Equal("no <>", Check.MessageOf("a<b", g => g.IfContainsAny(new[] { '<', '>' }, "no {Characters}")));
        Check.Passes("ABBA", g => g.IfContainsAnyExcept('A', 'B'));
        Check.Passes("", g => g.IfContainsAnyExcept('A'));
        Check.Fails<ArgumentException, string>("ABC", g => g.IfContainsAnyExcept('A', 'B'), ErrorCodes.ContainsAnyExcept, "'input' must contain only the allowed characters.");
        Check.Fails<ArgumentException, string>("A", g => g.IfContainsAnyExcept(), ErrorCodes.ContainsAnyExcept);
        Check.Passes("anything", g => g.IfContainsAny());
        Check.Fails<ArgumentNullException, string>(null!, g => g.IfContainsAny('a'), ErrorCodes.Null);
        Assert.Throws<ArgumentNullException>(() => "a".Throw().IfContainsAny((char[])null!, null));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Check.Fails<ArgumentException, string>("a>b", g => g.IfContainsAny(Brackets), ErrorCodes.ContainsAny);
        Check.Passes("ab", g => g.IfContainsAny(Brackets));
        Check.Fails<ArgumentException, string>("a", g => g.IfContainsAnyExcept(Brackets), ErrorCodes.ContainsAnyExcept);
        Assert.Equal("no &<", Check.MessageOf("a<b", g => g.IfContainsAny(['&', '<'], "no {Characters}")));
        Check.Passes("abc", g => g.IfContainsAnyExcept("abc"));
#endif
    }

    [Theory]
    [InlineData("hello", true)]
    [InlineData("", true)]
    [InlineData("\u007F", true)]
    [InlineData("café", false)]
    [InlineData("😀", false)]
    public void IfNotAscii(string text, bool ascii)
    {
        if (ascii)
        {
            Check.Passes(text, g => g.IfNotAscii());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotAscii(), ErrorCodes.NotAscii, "'input' must contain only ASCII characters.");
        }
    }

    [Theory]
    [InlineData("0123456789", true)]
    [InlineData("", false)]
    [InlineData("-1", false)]
    [InlineData("1 2", false)]
    [InlineData("٣", false)]
    [InlineData("12a", false)]
    public void IfNotDigits(string text, bool digits)
    {
        if (digits)
        {
            Check.Passes(text, g => g.IfNotDigits());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotDigits(), ErrorCodes.NotDigits, "'input' must contain only the digits 0 to 9.");
        }
    }

    [Theory]
    [InlineData("abc123", true)]
    [InlineData("Ångström", true)]
    [InlineData("Жук", true)]
    [InlineData("𝒜", true)]
    [InlineData("", false)]
    [InlineData("a b", false)]
    [InlineData("a_b", false)]
    [InlineData("a\uD835", false)]
    public void IfNotLettersOrDigits(string text, bool valid)
    {
        if (valid)
        {
            Check.Passes(text, g => g.IfNotLettersOrDigits());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotLettersOrDigits(), ErrorCodes.NotLettersOrDigits, "'input' must contain only letters or digits.");
        }
    }

    [Theory]
    [InlineData("0123456789abcdefABCDEF", true)]
    [InlineData("f", true)]
    [InlineData("", false)]
    [InlineData("0x1F", false)]
    [InlineData("12g", false)]
    public void IfNotHex(string text, bool hex)
    {
        if (hex)
        {
            Check.Passes(text, g => g.IfNotHex());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotHex(), ErrorCodes.NotHex, "'input' must be a hexadecimal string.");
        }
    }

    [Theory]
    [InlineData("3fa85f64-5717-4562-b3fc-2c963f66afa6", true)]
    [InlineData("3fa85f6457174562b3fc2c963f66afa6", true)]
    [InlineData("{3fa85f64-5717-4562-b3fc-2c963f66afa6}", true)]
    [InlineData("3fa85f64-5717-4562-b3fc", false)]
    [InlineData("", false)]
    public void IfNotGuid(string text, bool valid)
    {
        if (valid)
        {
            Check.Passes(text, g => g.IfNotGuid());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotGuid(), ErrorCodes.NotGuid, "'input' must be a valid GUID.");
        }
    }

    // Fuzzing fix: .NET Framework's Guid.TryParse throws FormatException or OverflowException
    // instead of returning false for some text in the hexadecimal format, so IfNotGuid threw
    // there. "0xz", "0x1ffffffff" and "0xg" take that path on .NET Framework; "0x-1" makes an
    // ArgumentException, which Guid.TryParse turns into false itself, and stays as a neighbor
    // case. On every framework they must fail the check with its code and an ArgumentException.
    [Theory]
    [InlineData("{0xa,0xa,0xa,{0xz,")]
    [InlineData("{0xa,0xa,0xa,{0x1ffffffff,0xa,0xa,0xa,0xa,0xa,0xa,0xa}}")]
    [InlineData("{0xa,0xa,0xa,{0x-1,0xa,0xa,0xa,0xa,0xa,0xa,0xa}}")]
    [InlineData("{0xa,0xa,0xa,{0xa,0xa,0xa,0xa,0xa,0xa,0xa,0xg}}")]
    public void IfNotGuid_fails_for_bad_hexadecimal_text(string text) =>
        Check.Fails<ArgumentException, string>(text, g => g.IfNotGuid(), ErrorCodes.NotGuid, "'input' must be a valid GUID.");

    [Theory]
    [InlineData("192.168.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("::1", true)]
    [InlineData("2001:db8::8a2e:370:7334", true)]
    [InlineData("::ffff:192.168.0.1", true)]
    [InlineData("fe80::1%eth0", true)]
    [InlineData("fe80::1%12", true)]
    [InlineData("1", false)]
    [InlineData("1.2.3", false)]
    [InlineData("01.2.3.4", false)]
    [InlineData("256.1.1.1", false)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("1.2.3.", false)]
    [InlineData("0x7f.0.0.1", false)]
    [InlineData("[::1]", false)]
    [InlineData("[::1]:80", false)]
    [InlineData("fe80::1%", false)]
    [InlineData("::g", false)]
    [InlineData("", false)]
    [InlineData(" 1.2.3.4", false)]
    public void IfNotIPAddress(string text, bool valid)
    {
        if (valid)
        {
            Check.Passes(text, g => g.IfNotIPAddress());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotIPAddress(), ErrorCodes.NotIPAddress, "'input' must be a valid IP address.");
        }
    }

    [Theory]
    [InlineData("hello-world-2", true)]
    [InlineData("a", true)]
    [InlineData("2026", true)]
    [InlineData("", false)]
    [InlineData("-a", false)]
    [InlineData("a-", false)]
    [InlineData("a--b", false)]
    [InlineData("Hello", false)]
    [InlineData("a_b", false)]
    [InlineData("café", false)]
    public void IfNotSlug(string text, bool slug)
    {
        if (slug)
        {
            Check.Passes(text, g => g.IfNotSlug());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotSlug(), ErrorCodes.NotSlug, "'input' must be a slug of lowercase letters, digits and single hyphens.");
        }
    }

    [Theory]
    [InlineData("1.2", true)]
    [InlineData("1.2.3.4", true)]
    [InlineData("0.0", true)]
    [InlineData("2147483647.0", true)]
    [InlineData("1", false)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("1..2", false)]
    [InlineData("1.2.", false)]
    [InlineData("2147483648.0", false)]
    [InlineData("1.-2", false)]
    [InlineData(" 1.2", false)]
    [InlineData("1.2.3-beta", false)]
    [InlineData("", false)]
    public void IfNotVersion(string text, bool version)
    {
        if (version)
        {
            Check.Passes(text, g => g.IfNotVersion());
            Assert.True(Version.TryParse(text, out _));
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotVersion(), ErrorCodes.NotVersion, "'input' must be a valid version number.");
        }
    }

    // Test numbers published by card networks for testing.
    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111-1111-1111-1111", true)]
    [InlineData("378282246310005", true)]
    [InlineData("5555555555554444", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("4111x1111", false)]
    [InlineData("", false)]
    [InlineData(" - ", false)]
    [InlineData("٤١١١", false)]
    public void IfNotCreditCardNumber(string text, bool valid)
    {
        if (valid)
        {
            Check.Passes(text, g => g.IfNotCreditCardNumber());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotCreditCardNumber(), ErrorCodes.NotCreditCardNumber, "'input' must be a valid credit card number.");
        }
    }

    [Fact]
    public void IfNotEnumName_accepts_member_names_only()
    {
        Check.Passes("Monday", g => g.IfNotEnumName<DayOfWeek>());
        Check.Passes("monday", g => g.IfNotEnumName<DayOfWeek>(ignoreCase: true));
        Check.Fails<ArgumentException, string>("monday", g => g.IfNotEnumName<DayOfWeek>(), ErrorCodes.NotEnumName, "'input' must be a defined DayOfWeek name.");
        Check.Fails<ArgumentException, string>("1", g => g.IfNotEnumName<DayOfWeek>(), ErrorCodes.NotEnumName);
        Check.Fails<ArgumentException, string>("Read, Write", g => g.IfNotEnumName<System.IO.FileAccess>(), ErrorCodes.NotEnumName);
        Check.Passes("ReadWrite", g => g.IfNotEnumName<System.IO.FileAccess>());
    }

    [Theory]
    [InlineData("report.pdf", true)]
    [InlineData(".gitignore", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    [InlineData("a\0b", false)]
    public void IfNotFileName(string text, bool valid)
    {
        if (valid)
        {
            Check.Passes(text, g => g.IfNotFileName());
        }
        else
        {
            Check.Fails<ArgumentException, string>(text, g => g.IfNotFileName(), ErrorCodes.NotFileName, "'input' must be a valid file name.");
        }
    }

    [Fact]
    public void Every_new_string_check_fails_null_with_the_Null_code()
    {
        Chain<string>[] chains =
        [
            g => g.IfNotTrimmed(), g => g.IfContainsWhiteSpace(), g => g.IfContainsControlCharacters(), g => g.IfNotAscii(),
            g => g.IfNotDigits(), g => g.IfNotLettersOrDigits(), g => g.IfNotHex(), g => g.IfNotBase64(), g => g.IfNotGuid(),
            g => g.IfNotIPAddress(), g => g.IfNotSlug(), g => g.IfNotVersion(), g => g.IfNotCreditCardNumber(),
            g => g.IfNotEnumName<DayOfWeek>(), g => g.IfNotFileName(), g => g.IfContainsAnyExcept('a'),
        ];
        foreach (var chain in chains)
        {
            Check.Fails<ArgumentNullException, string>(null!, chain, ErrorCodes.Null, "'input' must not be null.");
            Check.Passes<string>(null!, g => chain(g.AllowNull()));
        }
    }
}
