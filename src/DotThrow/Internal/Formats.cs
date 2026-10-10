using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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

    public static bool IsEmailAddress(string value)
    {
        var at = value.IndexOf('@');
        if (at <= 0 || at == value.Length - 1 || value.IndexOf('@', at + 1) >= 0)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }

        return IsDotSafe(value, 0, at) && IsDotSafe(value, at + 1, value.Length);
    }

    // No dot at the start or end, and no two dots in a row.
    private static bool IsDotSafe(string value, int start, int end)
    {
        if (value[start] == '.' || value[end - 1] == '.')
        {
            return false;
        }

        for (var i = start + 1; i < end; i++)
        {
            if (value[i] == '.' && value[i - 1] == '.')
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsXml(string value)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ConformanceLevel = ConformanceLevel.Document,
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
        // does not allow it, and Uri reads it differently on Windows and Unix), and no
        // Windows drive path such as C:\temp or C:/temp.
        if (string.IsNullOrWhiteSpace(value) || Text.ContainsChar(value, '\\') || IsDrivePath(value))
        {
            uri = null;
            return false;
        }

        if (!Uri.TryCreate(value, kind, out uri))
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
