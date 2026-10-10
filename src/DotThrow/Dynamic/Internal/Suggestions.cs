using System;
using System.Collections.Generic;

namespace DotThrow.Dynamic.Internal;

// "Did you mean ...?" for misspelled names (edit distance, ignoring case).
internal static class Suggestions
{
    public static string? Closest(string name, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Distance(name, candidate);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        // Only close matches help: at most a third of the letters differ.
        return best is not null && bestDistance <= Math.Max(1, name.Length / 3) ? best : null;
    }

    // Edit distance where a swap of two neighbor letters counts as one edit ("Txet" is one
    // edit from "Text").
    private static int Distance(string a, string b)
    {
        var before = new int[b.Length + 1];
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = Same(a[i - 1], b[j - 1]) ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && Same(a[i - 1], b[j - 2]) && Same(a[i - 2], b[j - 1]))
                {
                    current[j] = Math.Min(current[j], before[j - 2] + 1);
                }
            }

            var recycled = before;
            before = previous;
            previous = current;
            current = recycled;
        }

        return previous[b.Length];
    }

    private static bool Same(char x, char y) => char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
}

// Type names in definition errors, as C# writes them: int, string?, List<int>, int[].
internal static class TypeText
{
    public static string Of(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Of(underlying) + "?";
        }

        if (type.IsArray)
        {
            return Of(type.GetElementType()!) + "[]";
        }

        if (Keyword(type) is { } keyword)
        {
            return keyword;
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        var arguments = new List<string>();
        foreach (var argument in type.GetGenericArguments())
        {
            arguments.Add(Of(argument));
        }

        return (tick < 0 ? name : name.Substring(0, tick)) + "<" + string.Join(", ", arguments) + ">";
    }

    private static string? Keyword(Type type) => Type.GetTypeCode(type) switch
    {
        _ when type.IsEnum => null,
        TypeCode.Boolean => "bool",
        TypeCode.Char => "char",
        TypeCode.SByte => "sbyte",
        TypeCode.Byte => "byte",
        TypeCode.Int16 => "short",
        TypeCode.UInt16 => "ushort",
        TypeCode.Int32 => "int",
        TypeCode.UInt32 => "uint",
        TypeCode.Int64 => "long",
        TypeCode.UInt64 => "ulong",
        TypeCode.Single => "float",
        TypeCode.Double => "double",
        TypeCode.Decimal => "decimal",
        TypeCode.String => "string",
        _ => type == typeof(object) ? "object" : null,
    };
}
