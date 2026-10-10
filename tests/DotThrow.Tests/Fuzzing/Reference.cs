using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Reference versions of documented rules, written again with other means (regular expressions
/// and splitting instead of one scan over the text), so a slip in either version shows up as a
/// difference in the fuzz tests.
/// </summary>
internal static class Reference
{
    private const string Octet = "(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])";

    private static readonly Regex IPv4Text = new("^" + Octet + "(?:\\." + Octet + "){3}\\z", RegexOptions.CultureInvariant);

    private static readonly Regex HexGroup = new("^[0-9A-Fa-f]{1,4}\\z", RegexOptions.CultureInvariant);

    private static readonly Regex Zone = new("^[A-Za-z0-9_.-]+\\z", RegexOptions.CultureInvariant);

    // A quoted local part and the '@' after it: printable ASCII except '"' and '\', or a
    // backslash and any printable ASCII character.
    private static readonly Regex QuotedLocalPart = new("^\"(?:[\\x20\\x21\\x23-\\x5B\\x5D-\\x7E]|\\\\[\\x20-\\x7E])*\"@", RegexOptions.CultureInvariant);

    private static readonly char[] AtomSymbols = "!#$%&'*+-/=?^_`{|}~".ToCharArray();

    /// <summary>IfNotIPAddress: strict dotted-decimal IPv4, or RFC 4291 IPv6 with an optional zone.</summary>
    public static bool IsIPAddress(string text) =>
        text.Any(c => c == ':') ? IsIPv6(text, allowZone: true) : IsIPv4(text);

    public static bool IsIPv4(string text) => IPv4Text.IsMatch(text);

    /// <summary>RFC 4291 text: eight groups, or fewer around one "::" that stands for at least one group, with an IPv4 tail as the last two groups.</summary>
    public static bool IsIPv6(string text, bool allowZone)
    {
        var percent = text.IndexOf('%');
        if (percent >= 0)
        {
            if (!allowZone || !Zone.IsMatch(text.Remove(0, percent + 1)))
            {
                return false;
            }

            text = text.Substring(0, percent);
        }

        var compressed = text.IndexOf("::", StringComparison.Ordinal);
        if (compressed < 0)
        {
            return CountGroups(text.Split(':'), allowTail: true) == 8;
        }

        if (text.IndexOf("::", compressed + 1, StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        var left = text.Substring(0, compressed);
        var right = text.Substring(compressed + 2);
        var leftGroups = left.Length == 0 ? 0 : CountGroups(left.Split(':'), allowTail: false);
        var rightGroups = right.Length == 0 ? 0 : CountGroups(right.Split(':'), allowTail: true);
        return leftGroups >= 0 && rightGroups >= 0 && leftGroups + rightGroups <= 7;
    }

    // The 16-bit groups the parts stand for (an IPv4 tail counts two), or -1 for a bad part.
    private static int CountGroups(string[] parts, bool allowTail)
    {
        var count = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            if (HexGroup.IsMatch(parts[i]))
            {
                count++;
            }
            else if (allowTail && i == parts.Length - 1 && IsIPv4(parts[i]))
            {
                count += 2;
            }
            else
            {
                return -1;
            }
        }

        return count;
    }

    /// <summary>
    /// IfNotEmailAddress: a dot-atom or quoted local part of at most 64 UTF-8 bytes, an '@', and a
    /// domain of labels (or an address literal); at most 254 UTF-8 bytes in total.
    /// </summary>
    public static bool IsEmailAddress(string text)
    {
        if (text.Length == 0 || Encoding.UTF8.GetByteCount(text) > 254)
        {
            return false;
        }

        string local;
        string domain;
        if (text[0] == '"')
        {
            var quoted = QuotedLocalPart.Match(text);
            if (!quoted.Success)
            {
                return false;
            }

            local = text.Substring(0, quoted.Length - 1);
            domain = text.Substring(quoted.Length);
        }
        else
        {
            var at = text.IndexOf('@');
            if (at < 0)
            {
                return false;
            }

            local = text.Substring(0, at);
            domain = text.Substring(at + 1);
            if (!local.Split('.').All(IsAtom))
            {
                return false;
            }
        }

        return Encoding.UTF8.GetByteCount(local) <= 64 && IsMailDomain(domain);
    }

    private static bool IsAtom(string part) =>
        part.Length > 0 && CodePointsAll(part, (text, i) => text[i] < 0x80 ? char.IsLetterOrDigit(text[i]) || Array.IndexOf(AtomSymbols, text[i]) >= 0 : char.IsLetterOrDigit(text, i));

    private static bool IsMailDomain(string domain)
    {
        if (domain.Length > 0 && domain[0] == '[')
        {
            if (domain.Length < 2 || domain[domain.Length - 1] != ']')
            {
                return false;
            }

            var inner = domain.Substring(1, domain.Length - 2);
            return inner.Length > 5 && inner.StartsWith("IPv6:", StringComparison.OrdinalIgnoreCase)
                ? IsIPv6(inner.Substring(5), allowZone: false)
                : IsIPv4(inner);
        }

        return domain.Length is > 0 and <= 253 && domain.Split('.').All(label =>
            label.Length is >= 1 and <= 63
            && label[0] != '-'
            && label[label.Length - 1] != '-'
            && CodePointsAll(label, (text, i) => text[i] < 0x80 ? char.IsLetterOrDigit(text[i]) || text[i] == '-' : char.IsLetterOrDigit(text, i)));
    }

    // Applies the test to every code point (a surrogate pair is one).
    private static bool CodePointsAll(string text, Func<string, int, bool> test)
    {
        for (var i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            if (!test(text, i))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>IfLoopback on an address: 127.0.0.0/8, ::1, or an IPv4-mapped 127.x.x.x; the zone does not count.</summary>
    public static bool IsLoopback(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4
            ? bytes[0] == 127
            : IsMapped(bytes) ? bytes[12] == 127 : bytes.Take(15).All(b => b == 0) && bytes[15] == 1;
    }

    /// <summary>IfPrivate: the ranges its documentation lists.</summary>
    public static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 4 || IsMapped(bytes))
        {
            var (a, b) = bytes.Length == 4 ? (bytes[0], bytes[1]) : (bytes[12], bytes[13]);
            return a == 10
                || (a == 172 && b is >= 16 and <= 31)
                || (a == 192 && b == 168)
                || (a == 100 && b is >= 64 and <= 127)
                || (a == 169 && b == 254);
        }

        // fc00::/7 and fe80::/10.
        return bytes[0] is 0xFC or 0xFD || (bytes[0] == 0xFE && bytes[1] is >= 0x80 and <= 0xBF);
    }

    public static bool IsIPv4(IPAddress address) => address.AddressFamily == AddressFamily.InterNetwork;

    private static bool IsMapped(byte[] bytes) =>
        bytes.Length == 16 && bytes.Take(10).All(b => b == 0) && bytes[10] == 0xFF && bytes[11] == 0xFF;
}
