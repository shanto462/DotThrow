using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using DotThrow.Internal;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>
/// The text helpers against the BCL, on many inputs. Run these on the netstandard builds
/// too (-p:DotThrowAsset=netstandard2.0), where the helpers are plain loops instead of
/// SearchValues, so both versions are checked against the same reference.
/// </summary>
public sealed class TextDifferentialTests
{
    [Fact]
    public void The_white_space_list_matches_char_IsWhiteSpace()
    {
        for (var c = 0; c <= char.MaxValue; c++)
        {
            Assert.Equal(char.IsWhiteSpace((char)c), Text.WhiteSpaceChars.HasChar((char)c));
            var text = "a" + (char)c + "b";
            Assert.Equal(char.IsWhiteSpace((char)c), Text.ContainsWhiteSpace(text));
            Assert.Equal(char.IsControl((char)c), Text.ContainsControlCharacters(text));
            Assert.Equal(c <= 0x7F, Text.IsAscii(text));
            Assert.Equal(char.IsLetterOrDigit((char)c) && !char.IsSurrogate((char)c), Text.IsLettersOrDigits("a" + (char)c));
            Assert.Equal(c is >= '0' and <= '9', Text.IsDigits("1" + (char)c));
            Assert.Equal(Uri.IsHexDigit((char)c), Text.IsHex("a" + (char)c));
        }
    }

    [Fact]
    public void Base64_matches_Convert_on_random_input()
    {
        var random = new Random(20261010);
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/= \t\r\n-_*";
        var buffer = new byte[64];
        for (var i = 0; i < 20000; i++)
        {
            string text;
            if (i % 3 == 0)
            {
                // Valid Base64, sometimes with white space added.
                random.NextBytes(buffer);
                text = Convert.ToBase64String(buffer, 0, random.Next(0, 40));
                if (i % 2 == 0 && text.Length > 2)
                {
                    text = text.Insert(random.Next(text.Length), " \r\n".Substring(random.Next(3), 1));
                }
            }
            else
            {
                var chars = new char[random.Next(0, 12)];
                for (var j = 0; j < chars.Length; j++)
                {
                    chars[j] = Alphabet[random.Next(Alphabet.Length)];
                }

                text = new string(chars);
            }

            var expected = text.Trim().Length > 0 && IsConvertible(text);
            Assert.True(expected == Text.IsBase64(text), $"'{text}' expected {expected}");
        }
    }

    [Fact]
    public void Version_matches_Version_TryParse_for_digits_and_dots()
    {
        var random = new Random(7);
        const string Alphabet = "0123456789.";
        for (var i = 0; i < 20000; i++)
        {
            var chars = new char[random.Next(0, 14)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = Alphabet[random.Next(Alphabet.Length)];
            }

            var text = new string(chars);
            Assert.True(Version.TryParse(text, out _) == Text.IsVersion(text), text);
        }
    }

    [Fact]
    public void IPv4_matches_IPAddress_on_canonical_dotted_quads()
    {
        var random = new Random(11);
        for (var i = 0; i < 5000; i++)
        {
            var parts = Enumerable.Range(0, 4).Select(_ => random.Next(0, 300)).ToArray();
            var text = string.Join(".", parts);
            var expected = parts.All(p => p <= 255);
            Assert.Equal(expected, Text.IsIPAddress(text));
            if (expected)
            {
                Assert.Equal(text, IPAddress.Parse(text).ToString());
            }
        }
    }

    [Fact]
    public void IPv6_accepts_every_address_IPAddress_writes()
    {
        var random = new Random(5);
        var bytes = new byte[16];
        for (var i = 0; i < 5000; i++)
        {
            random.NextBytes(bytes);
            for (var j = 0; j < 16; j++)
            {
                // Runs of zeros, so "::" appears often.
                if (random.Next(3) == 0)
                {
                    bytes[j] = 0;
                }
            }

            var text = new IPAddress(bytes).ToString();
            Assert.True(Text.IsIPAddress(text), text);
            Assert.True(Text.IsIPAddress(text.ToUpperInvariant()), text);
        }
    }

    [Fact]
    public void IPv6_never_accepts_what_IPAddress_rejects()
    {
        var random = new Random(9);
        const string Alphabet = "0123456789abcdef:.";
        for (var i = 0; i < 50000; i++)
        {
            var chars = new char[random.Next(2, 24)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = Alphabet[random.Next(Alphabet.Length)];
            }

            var text = new string(chars);
            if (text.HasChar(':') && Text.IsIPAddress(text))
            {
                Assert.True(IPAddress.TryParse(text, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6, text);
            }
        }

        string[] samples =
        [
            "::", "::1", "1::", "1:2:3:4:5:6:7:8", "::ffff:1.2.3.4", "fe80::1%eth0", "abcd:ef01:2345:6789:abcd:ef01:2345:6789",
            "1:2:3:4:5:6:1.2.3.4", "1:2:3:4:5:6:7::", "::1.2.3.4",
        ];
        foreach (var text in samples)
        {
            Assert.True(Text.IsIPAddress(text), text);
            Assert.True(IPAddress.TryParse(text, out _), text);
        }

        foreach (var text in new[] { "1:2:3:4:5:6:7:8:9", "1::2::3", "::ffff:1.2.3", "12345::", ":1", "1:", ":::", "1:2:3:4:5:6:7:1.2.3.4", "::01.2.3.4", "1:2:3:4:5:6:7:8::" })
        {
            Assert.False(Text.IsIPAddress(text), text);
        }
    }

    [Fact]
    public void Credit_card_matches_a_simple_Luhn()
    {
        var random = new Random(3);
        for (var i = 0; i < 5000; i++)
        {
            var digits = new string(Enumerable.Range(0, random.Next(1, 20)).Select(_ => (char)('0' + random.Next(10))).ToArray());
            var sum = digits.Reverse().Select((c, index) =>
            {
                var d = c - '0';
                return index % 2 == 1 ? (d * 2 > 9 ? (d * 2) - 9 : d * 2) : d;
            }).Sum();
            Assert.Equal(sum % 10 == 0, Text.IsCreditCardNumber(digits));
        }
    }

    private static bool IsConvertible(string text)
    {
        try
        {
            _ = Convert.FromBase64String(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
