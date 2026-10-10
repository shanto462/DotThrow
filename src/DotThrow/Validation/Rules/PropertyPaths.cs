using System;
using System.Globalization;
using System.Text;

namespace DotThrow.Internal;

// Builds failure names: "Address" + "City" = "Address.City", "Lines" + [2] = "Lines[2]".
internal static class PropertyPaths
{
    public static string Join(string prefix, string name)
    {
        if (prefix.Length == 0)
        {
            return name;
        }

        if (name.Length == 0)
        {
            return prefix;
        }

        return name[0] == '[' ? prefix + name : prefix + ValidatorDefaults.PropertyChainSeparator + name;
    }

    // A path from selector text uses "."; other separators replace it.
    public static string FromSelector(string path)
    {
        var separator = ValidatorDefaults.PropertyChainSeparator;
        return separator == "." || path.IndexOf('.') < 0 ? path : path.Replace(".", separator);
    }

    public static string Index(int index) => "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    // The text added after a collection name: "[2]" by default, or the custom indexer text
    // as it is (the same as FluentValidation's OverrideIndexer).
    public static string AppendIndex(string path, int index, string? indexText) =>
        indexText is null ? path + Index(index) : path + indexText;

    // "Lines[2].Items[0].Sku" becomes "Lines[].Items[].Sku".
    public static string NormalizeIndexes(string path)
    {
        if (path.IndexOf('[') < 0)
        {
            return path;
        }

        var builder = new StringBuilder(path.Length);
        var inside = false;
        foreach (var c in path)
        {
            if (c == '[')
            {
                inside = true;
                builder.Append('[');
            }
            else if (c == ']')
            {
                inside = false;
                builder.Append(']');
            }
            else if (!inside)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    // "Lines[2].Quantity" becomes "Lines.Quantity" (used by the test helpers).
    public static string RemoveIndexes(string path) =>
        path.IndexOf('[') < 0 ? path : NormalizeIndexes(path).Replace("[]", string.Empty);

    // True for "Lines[].Quantity" (every item of a collection).
    public static bool HasAnyIndex(string path)
    {
        for (var i = 0; i + 1 < path.Length; i++)
        {
            if (path[i] == '[' && path[i + 1] == ']')
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasSeparator(string path) =>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        path.Contains(ValidatorDefaults.PropertyChainSeparator, StringComparison.Ordinal);
#else
        path.IndexOf(ValidatorDefaults.PropertyChainSeparator, StringComparison.Ordinal) >= 0;
#endif

    // True when path is prefix followed by a separator and more.
    public static bool StartsWithMember(string path, string prefix)
    {
        var separator = ValidatorDefaults.PropertyChainSeparator;
        return path.Length > prefix.Length + separator.Length
            && path.StartsWith(prefix, StringComparison.Ordinal)
            && string.CompareOrdinal(path, prefix.Length, separator, 0, separator.Length) == 0;
    }

    // True when path is prefix followed by "[" and more.
    public static bool StartsWithIndex(string path, string prefix) =>
        path.Length > prefix.Length && path[prefix.Length] == '[' && path.StartsWith(prefix, StringComparison.Ordinal);
}
