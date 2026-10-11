using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace DotThrow.Internal;

internal static class Formats
{
    public static bool Contains(string value, string part, StringComparison comparison)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return value.Contains(part, comparison);
#else
        return value.IndexOf(part, comparison) >= 0;
#endif
    }

    // An RFC 5321 subset, with the non-ASCII letters and digits of RFC 6531 (internationalized
    // addresses such as "用户@例子.广告"):
    // - local part: a dot-atom (letters, digits and !#$%&'*+-/=?^_`{|}~, in dot-separated
    //   non-empty parts) or a quoted string ("a b", printable ASCII and space, \ escapes);
    //   at most 64 UTF-8 bytes;
    // - domain: labels of letters, digits and hyphens (1 to 63 characters, no hyphen at either
    //   end, at most 253 characters in total), or an address literal [1.2.3.4] or [IPv6:...]
    //   that must be a valid address;
    // - at most 254 UTF-8 bytes in total.
    // No allocation: one pass over the string.
    public static bool IsEmailAddress(string value)
    {
        if (value.Length == 0 || Utf8Length(value, 0, value.Length) > 254)
        {
            return false;
        }

        int at;
        if (value[0] == '"')
        {
            at = QuotedLocalPartEnd(value);
            if (at < 0)
            {
                return false;
            }
        }
        else
        {
            at = value.IndexOf('@');
            if (at <= 0 || !IsDotAtom(value, 0, at))
            {
                return false;
            }
        }

        return Utf8Length(value, 0, at) <= 64 && IsMailDomain(value, at + 1);
    }

    // The index of the '@' after a quoted local part, or -1.
    private static int QuotedLocalPartEnd(string value)
    {
        var i = 1;
        while (i < value.Length)
        {
            var c = value[i];
            if (c == '\\')
            {
                if (i + 1 >= value.Length || value[i + 1] is < ' ' or > '~')
                {
                    return -1;
                }

                i += 2;
            }
            else if (c == '"')
            {
                return i + 1 < value.Length && value[i + 1] == '@' ? i + 1 : -1;
            }
            else if (c is >= ' ' and <= '~')
            {
                i++;
            }
            else
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool IsDotAtom(string value, int start, int end)
    {
        var partLength = 0;
        for (var i = start; i < end; i++)
        {
            var c = value[i];
            if (c == '.')
            {
                if (partLength == 0)
                {
                    return false;
                }

                partLength = 0;
                continue;
            }

            if (c < 0x80)
            {
                if (!IsAtomText(c))
                {
                    return false;
                }
            }
            else if (!IsLetterOrDigit(value, ref i))
            {
                return false;
            }

            partLength++;
        }

        return partLength > 0;
    }

    private static bool IsMailDomain(string value, int start)
    {
        var end = value.Length;
        if (start >= end)
        {
            return false;
        }

        if (value[start] == '[')
        {
            if (value[end - 1] != ']')
            {
                return false;
            }

            const string IPv6Tag = "IPv6:";
            var inner = start + 1;
            if (end - 1 - inner > IPv6Tag.Length && string.Compare(value, inner, IPv6Tag, 0, IPv6Tag.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return Text.IsIPv6(value, inner + IPv6Tag.Length, end - 1, allowZone: false);
            }

            return Text.IsIPv4(value, inner, end - 1);
        }

        if (end - start > 253)
        {
            return false;
        }

        var labelStart = start;
        for (var i = start; i <= end; i++)
        {
            if (i == end || value[i] == '.')
            {
                var length = i - labelStart;
                if (length is 0 or > 63 || value[labelStart] == '-' || value[i - 1] == '-')
                {
                    return false;
                }

                labelStart = i + 1;
                continue;
            }

            var c = value[i];
            if (c < 0x80)
            {
                if (!(c == '-' || c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')))
                {
                    return false;
                }
            }
            else if (!IsLetterOrDigit(value, ref i))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAtomText(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
            or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '/' or '=' or '?' or '^' or '_' or '`' or '{' or '|' or '}' or '~';

    // A non-ASCII letter or digit; a surrogate pair is read as one character (and `i` moves past it).
    private static bool IsLetterOrDigit(string value, ref int i)
    {
        if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
        {
            var result = char.IsLetterOrDigit(value, i);
            i++;
            return result;
        }

        return char.IsLetterOrDigit(value[i]);
    }

    private static int Utf8Length(string value, int start, int end)
    {
        var length = 0;
        for (var i = start; i < end; i++)
        {
            var c = value[i];
            if (c < 0x80)
            {
                length++;
            }
            else if (c < 0x800)
            {
                length += 2;
            }
            else if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(value[i + 1]))
            {
                length += 4;
                i++;
            }
            else
            {
                length += 3;
            }
        }

        return length;
    }

    // maxCharacters: 0 for no limit; otherwise a longer text fails at once, and the reader
    // stops at that many characters in the document (XmlReaderSettings.MaxCharactersInDocument).
    public static bool IsXml(string value, long maxCharacters = 0)
    {
        if (maxCharacters > 0 && value.Length > maxCharacters)
        {
            return false;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ConformanceLevel = ConformanceLevel.Document,
            MaxCharactersInDocument = maxCharacters,
        };

        try
        {
            using var stringReader = new StringReader(value);
            using var reader = XmlReader.Create(stringReader, settings);
            while (reader.Read())
            {
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public static bool IsUri(string value, UriKind kind, [NotNullWhen(true)] out Uri? uri)
    {
        // Rules that hold on every operating system: no empty text, no backslash (RFC 3986
        // does not allow it, and Uri reads it differently on Windows and Unix), no Windows
        // drive path such as C:\temp or C:/temp, and only characters a URI may contain as
        // written. Uri.TryCreate would trim white space and escape the rest (spaces, line
        // breaks, quotes, angle brackets), which accepts text that is not a URI.
        if (string.IsNullOrWhiteSpace(value) || Text.ContainsChar(value, '\\') || IsDrivePath(value) || !HasOnlyUriCharacters(value))
        {
            uri = null;
            return false;
        }

        if (!TryCreateUri(value, kind, out uri))
        {
            return false;
        }

        // Uri treats "/etc/hosts" (Unix) as an absolute file URI. A file path is not what
        // callers mean by a URI, so require an explicit scheme.
        if (uri.IsAbsoluteUri && uri.IsFile && !value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            uri = null;
            return false;
        }

        return true;
    }

    // Uri.TryCreate throws IndexOutOfRangeException instead of returning false for some text,
    // for example a file or UNC host made only of bidi control characters ("file://\u202E" on
    // .NET 10). HasOnlyUriCharacters already rejects that text; this catch is defense in depth
    // against other inputs that hit the same runtime bug.
    private static bool TryCreateUri(string value, UriKind kind, [NotNullWhen(true)] out Uri? uri)
    {
        try
        {
            return Uri.TryCreate(value, kind, out uri);
        }
        catch (IndexOutOfRangeException)
        {
            uri = null;
            return false;
        }
    }

    // The text of the URI as written: RFC 3986 unreserved and reserved characters, '%' only
    // as the start of a %XX escape, at most one '#'. Non-ASCII letters, digits, marks and
    // symbols are allowed as in IRIs (RFC 3987, as browsers show addresses); white space,
    // control characters and format characters (such as bidi overrides) are not. A character
    // outside the Basic Multilingual Plane is one surrogate pair, judged as one character (so
    // the invisible tag characters U+E0001 and U+E0020 to U+E007F are format characters too); a
    // surrogate that is not part of a pair is not a character, and Uri would replace it with U+FFFD.
    private static bool HasOnlyUriCharacters(string value)
    {
        var fragments = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c < 0x80)
            {
                if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                    or '-' or '.' or '_' or '~' or ':' or '/' or '?' or '[' or ']' or '@'
                    or '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=')
                {
                    continue;
                }

                if (c == '%' && i + 2 < value.Length && IsHexDigit(value[i + 1]) && IsHexDigit(value[i + 2]))
                {
                    i += 2;
                    continue;
                }

                if (c == '#' && ++fragments == 1)
                {
                    continue;
                }

                return false;
            }

            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(value, i) == UnicodeCategory.Format)
                {
                    return false;
                }

                i++;
                continue;
            }

            if (char.IsSurrogate(c) || char.IsWhiteSpace(c) || char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHexDigit(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    // The outcome of a regular expression check. A match that runs out of time cannot confirm
    // anything about the value, so the checks treat it as a failure (fail closed).
    public enum RegexOutcome : byte
    {
        NoMatch,
        Match,
        TimedOut,
    }

    public static RegexOutcome Match(string input, string pattern, RegexOptions options, TimeSpan? matchTimeout)
    {
        try
        {
            var matched = matchTimeout is { } timeout ? Regex.IsMatch(input, pattern, options, timeout) : Regex.IsMatch(input, pattern, options);
            return matched ? RegexOutcome.Match : RegexOutcome.NoMatch;
        }
        catch (RegexMatchTimeoutException)
        {
            return RegexOutcome.TimedOut;
        }
    }

    public static RegexOutcome Match(string input, Regex regex)
    {
        try
        {
            return regex.IsMatch(input) ? RegexOutcome.Match : RegexOutcome.NoMatch;
        }
        catch (RegexMatchTimeoutException)
        {
            return RegexOutcome.TimedOut;
        }
    }

    public static bool IsHttpUrl(string value) =>
        IsUri(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsDrivePath(string value) =>
        value.Length >= 2 && value[1] == ':' && value[0] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')
        && (value.Length == 2 || value[2] is '/' or '\\');

    // Only cased letters count, so digits, symbols and letters without case (such as
    // Chinese characters) never make a string "all uppercase".
    public static bool AllLettersAre(string value, bool upper)
    {
        var hasUpper = false;
        var hasLower = false;
        foreach (var c in value)
        {
            hasUpper |= char.IsUpper(c);
            hasLower |= char.IsLower(c);
        }

        return upper ? hasUpper && !hasLower : hasLower && !hasUpper;
    }
}
