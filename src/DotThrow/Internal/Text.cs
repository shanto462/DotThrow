using System;
using System.IO;
#if NET8_0_OR_GREATER
using System.Buffers;
using System.Text;
#endif

namespace DotThrow.Internal;

// Character-class and format checks for strings. On .NET 8 and later they use
// SearchValues, IndexOfAnyExcept and Ascii, which are vectorized; on older frameworks
// they are simple loops that never allocate. Every check gives the same answer on every
// framework (the tests compare them).
internal static class Text
{
    // Every char for which char.IsWhiteSpace is true (the tests check this list).
    public const string WhiteSpaceChars =
        "\t\n\v\f\r \u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000";

    private const string Base64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    private static readonly char[] InvalidFileNameCharArray = Path.GetInvalidFileNameChars();

#if NET8_0_OR_GREATER
    private static readonly SearchValues<char> WhiteSpace = SearchValues.Create(WhiteSpaceChars);
    private static readonly SearchValues<char> Controls = SearchValues.Create(ControlChars());
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789ABCDEFabcdef");
    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");
    private static readonly SearchValues<char> Base64Digits = SearchValues.Create(Base64Alphabet);
    private static readonly SearchValues<char> SlugChars = SearchValues.Create("-0123456789abcdefghijklmnopqrstuvwxyz");
    private static readonly SearchValues<char> AsciiLettersOrDigits =
        SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
    private static readonly SearchValues<char> InvalidFileNameChars = SearchValues.Create(InvalidFileNameCharArray);

    private static string ControlChars()
    {
        var chars = new char[0x20 + 0x21];
        for (var i = 0; i < 0x20; i++)
        {
            chars[i] = (char)i;
        }

        for (var i = 0; i <= 0x20; i++)
        {
            chars[0x20 + i] = (char)(0x7F + i);
        }

        return new string(chars);
    }
#endif

    public static bool ContainsChar(string value, char c)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return value.Contains(c);
#else
        return value.IndexOf(c) >= 0;
#endif
    }

    public static bool IsTrimmed(string value) =>
        value.Length == 0 || (!char.IsWhiteSpace(value[0]) && !char.IsWhiteSpace(value[value.Length - 1]));

    public static bool ContainsWhiteSpace(string value)
    {
#if NET8_0_OR_GREATER
        return value.AsSpan().ContainsAny(WhiteSpace);
#else
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                return true;
            }
        }

        return false;
#endif
    }

    public static bool ContainsControlCharacters(string value)
    {
#if NET8_0_OR_GREATER
        return value.AsSpan().ContainsAny(Controls);
#else
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
#endif
    }

    // Ad-hoc character sets (IfContainsAny and IfContainsAnyExcept with a list or an array).
    // The set is turned into a 128-bit ASCII bitmap on the stack; a non-ASCII character is
    // looked up in the set with a plain loop. No generic BCL span search is used, because on
    // some CPUs their generic helpers boxed values on the happy path (IfNotDigits did on Linux
    // x64). For a large set used often, the SearchValues overloads are the vectorized path.
    public static bool ContainsAny(string value, char[] characters)
    {
        var set = new CharSet(characters);
        foreach (var c in value)
        {
            if (set.Contains(c, characters))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsAnyExcept(string value, char[] characters)
    {
        var set = new CharSet(characters);
        foreach (var c in value)
        {
            if (!set.Contains(c, characters))
            {
                return true;
            }
        }

        return false;
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    public static bool ContainsAny(ReadOnlySpan<char> value, ReadOnlySpan<char> characters)
    {
        var set = new CharSet(characters);
        foreach (var c in value)
        {
            if (set.Contains(c, characters))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsAnyExcept(ReadOnlySpan<char> value, ReadOnlySpan<char> characters)
    {
        var set = new CharSet(characters);
        foreach (var c in value)
        {
            if (!set.Contains(c, characters))
            {
                return true;
            }
        }

        return false;
    }
#endif

    // The ASCII characters of a set as two 64-bit masks, plus a flag for non-ASCII members.
    private readonly struct CharSet
    {
        private readonly ulong _low;
        private readonly ulong _high;
        private readonly bool _hasNonAscii;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        public CharSet(ReadOnlySpan<char> characters)
#else
        public CharSet(char[] characters)
#endif
        {
            _low = 0;
            _high = 0;
            _hasNonAscii = false;
            foreach (var c in characters)
            {
                if (c < 64)
                {
                    _low |= 1UL << c;
                }
                else if (c < 128)
                {
                    _high |= 1UL << (c - 64);
                }
                else
                {
                    _hasNonAscii = true;
                }
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        public bool Contains(char c, ReadOnlySpan<char> characters)
#else
        public bool Contains(char c, char[] characters)
#endif
        {
            if (c < 64)
            {
                return (_low & (1UL << c)) != 0;
            }

            if (c < 128)
            {
                return (_high & (1UL << (c - 64))) != 0;
            }

            if (_hasNonAscii)
            {
                foreach (var candidate in characters)
                {
                    if (candidate == c)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public static bool IsAscii(string value)
    {
#if NET8_0_OR_GREATER
        return Ascii.IsValid(value);
#else
        foreach (var c in value)
        {
            if (c > '\x7F')
            {
                return false;
            }
        }

        return true;
#endif
    }

    // ASCII digits only: Unicode digits such as Arabic-Indic digits do not count.
    public static bool IsDigits(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

#if NET8_0_OR_GREATER
        // SearchValues, not ContainsAnyExceptInRange: on GitHub's Linux x64 runners the generic
        // range search boxed its char limits (4 boxes, 96 B per call); SearchValues never does.
        return !value.AsSpan().ContainsAnyExcept(Digits);
#else
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
#endif
    }

    // Unicode letters and digits (char.IsLetterOrDigit), with letters outside the Basic
    // Multilingual Plane read as one surrogate pair.
    public static bool IsLettersOrDigits(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var start = 0;
#if NET8_0_OR_GREATER
        // Most values are plain ASCII: check them in one vectorized pass.
        start = value.AsSpan().IndexOfAnyExcept(AsciiLettersOrDigits);
        if (start < 0)
        {
            return true;
        }
#endif

        for (var i = start; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                if (!char.IsLetterOrDigit(value, i))
                {
                    return false;
                }

                i++;
            }
            else if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsHex(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

#if NET8_0_OR_GREATER
        return !value.AsSpan().ContainsAnyExcept(HexDigits);
#else
        foreach (var c in value)
        {
            if (!IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
#endif
    }

    // Standard Base64 (RFC 4648 section 4) as Convert.FromBase64String reads it: white space
    // (space, tab, CR, LF) is ignored, the other characters must be a multiple of 4, and up
    // to two '=' may end the text. An empty string fails.
    public static bool IsBase64(string value)
    {
#if NET8_0_OR_GREATER
        // Fast path: no padding and no white space.
        if (!value.AsSpan().ContainsAnyExcept(Base64Digits))
        {
            return value.Length > 0 && value.Length % 4 == 0;
        }
#endif

        var significant = 0;
        var padding = 0;
        foreach (var c in value)
        {
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                continue;
            }

            if (c == '=')
            {
                if (++padding > 2)
                {
                    return false;
                }
            }
            else if (padding > 0 || !IsBase64Digit(c))
            {
                return false;
            }

            significant++;
        }

        return significant > 0 && significant % 4 == 0;
    }

    public static bool IsGuid(string value)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return Guid.TryParse(value.AsSpan(), out _);
#else
        // .NET Framework's Guid.TryParse throws instead of returning false for some text in the
        // hexadecimal format, such as "{0xa,0xa,0xa,{0xz,": it reads the last eight numbers with
        // Convert.ToInt32(text, 16), and a FormatException or OverflowException from it escapes
        // (it turns an ArgumentException into false itself; the filter keeps that type to be
        // safe). Such text is not a GUID.
        try
        {
            return Guid.TryParse(value, out _);
        }
#pragma warning disable CA1031 // Do not catch general exception types: only the parse errors above.
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
#pragma warning restore CA1031
        {
            return false;
        }
#endif
    }

    // Strict IP address text, the same on every framework (IPAddress.TryParse differs between
    // runtimes and accepts legacy forms). IPv4: four decimal numbers from 0 to 255 without
    // leading zeros, so "1", "1.2" and the octal "010.0.0.1" fail. IPv6 (RFC 4291): up to
    // eight groups of 1 to 4 hex digits, at most one "::", an optional IPv4 tail, and an
    // optional zone ("%eth0"); brackets and ports fail.
    public static bool IsIPAddress(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        return ContainsChar(value, ':') ? IsIPv6(value, 0, value.Length, allowZone: true) : IsIPv4(value, 0, value.Length);
    }

    // Dotted-decimal IPv4 text in value[start..end): four numbers from 0 to 255, no leading zeros.
    internal static bool IsIPv4(string value, int start, int end)
    {
        var parts = 0;
        var i = start;
        while (true)
        {
            var first = i;
            var number = 0;
            while (i < end && value[i] is >= '0' and <= '9' && i - first < 3)
            {
                number = (number * 10) + (value[i] - '0');
                i++;
            }

            var digits = i - first;
            if (digits == 0 || number > 255 || (digits > 1 && value[first] == '0'))
            {
                return false;
            }

            parts++;
            if (i == end)
            {
                return parts == 4;
            }

            if (value[i] != '.' || parts == 4)
            {
                return false;
            }

            i++;
        }
    }

    // IPv6 text in value[start..end): RFC 4291 groups, optionally followed by a zone when
    // allowZone is true (a zone runs to the end of the string).
    internal static bool IsIPv6(string value, int start, int end, bool allowZone)
    {
        var zone = value.IndexOf('%', start, end - start);
        if (zone >= 0)
        {
            if (!allowZone || end != value.Length || !IsZone(value, zone + 1))
            {
                return false;
            }

            end = zone;
        }

        if (end - start < 2)
        {
            return false;
        }

        var groups = 0;
        var compressed = false;
        var i = start;
        if (value[start] == ':')
        {
            if (value[start + 1] != ':')
            {
                return false;
            }

            compressed = true;
            i = start + 2;
        }

        while (i < end)
        {
            var first = i;
            while (i < end && IsHexDigit(value[i]))
            {
                i++;
            }

            if (i < end && value[i] == '.')
            {
                // An IPv4 tail takes the place of the last two groups.
                if (groups > 6 || !IsIPv4(value, first, end))
                {
                    return false;
                }

                groups += 2;
                break;
            }

            var digits = i - first;
            if (digits is 0 or > 4)
            {
                return false;
            }

            groups++;
            if (i == end)
            {
                break;
            }

            if (value[i] != ':' || ++i == end)
            {
                return false;
            }

            if (value[i] == ':')
            {
                if (compressed)
                {
                    return false;
                }

                compressed = true;
                i++;
            }
        }

        return compressed ? groups <= 7 : groups == 8;
    }

    private static bool IsZone(string value, int start)
    {
        if (start >= value.Length)
        {
            return false;
        }

        for (var i = start; i < value.Length; i++)
        {
            if (!IsAsciiLetterOrDigit(value[i]) && value[i] is not ('_' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    // A URL slug: lowercase ASCII letters and digits in groups joined by single hyphens,
    // for example "hello-world-2". No hyphen at the start or end.
    public static bool IsSlug(string value)
    {
        if (value.Length == 0 || value[0] == '-' || value[value.Length - 1] == '-')
        {
            return false;
        }

#if NET8_0_OR_GREATER
        var span = value.AsSpan();
        return !span.ContainsAnyExcept(SlugChars) && span.IndexOf("--".AsSpan(), StringComparison.Ordinal) < 0;
#else
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '-')
            {
                if (value[i - 1] == '-')
                {
                    return false;
                }
            }
            else if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
#endif
    }

    // A System.Version string: two to four parts of ASCII digits joined by dots, each part at
    // most int.MaxValue. Stricter than Version.TryParse, which also accepts white space and
    // signs inside the parts.
    public static bool IsVersion(string value)
    {
        var parts = 0;
        var i = 0;
        while (true)
        {
            var start = i;
            long number = 0;
            while (i < value.Length && value[i] is >= '0' and <= '9')
            {
                number = (number * 10) + (value[i] - '0');
                if (number > int.MaxValue)
                {
                    return false;
                }

                i++;
            }

            if (i == start)
            {
                return false;
            }

            parts++;
            if (i == value.Length)
            {
                return parts >= 2;
            }

            if (value[i] != '.' || parts == 4)
            {
                return false;
            }

            i++;
        }
    }

    // FluentValidation CreditCard: spaces and hyphens are ignored, every other character
    // must be a digit, and the digits must pass the Luhn checksum. Unlike FluentValidation,
    // only ASCII digits count and a value without digits fails.
    public static bool IsCreditCardNumber(string value)
    {
        var sum = 0;
        var digits = 0;
        var doubled = false;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            var c = value[i];
            if (c is ' ' or '-')
            {
                continue;
            }

            if (c is < '0' or > '9')
            {
                return false;
            }

            var digit = c - '0';
            if (doubled)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            digits++;
            doubled = !doubled;
        }

        return digits > 0 && sum % 10 == 0;
    }

    // A file name (not a path) for the current operating system: not empty, not "." or "..",
    // and none of Path.GetInvalidFileNameChars (on Windows this includes \ / : * ? " &lt; &gt; |
    // and control characters; on Linux and macOS only '\0' and '/').
    public static bool IsFileName(string value)
    {
        if (value.Length == 0 || value == "." || value == "..")
        {
            return false;
        }

#if NET8_0_OR_GREATER
        return !value.AsSpan().ContainsAny(InvalidFileNameChars);
#else
        return value.IndexOfAny(InvalidFileNameCharArray) < 0;
#endif
    }

    private static bool IsHexDigit(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private static bool IsAsciiLetterOrDigit(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsBase64Digit(char c) => IsAsciiLetterOrDigit(c) || c is '+' or '/';
}
