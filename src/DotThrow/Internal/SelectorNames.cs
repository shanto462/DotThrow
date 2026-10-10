using System;

namespace DotThrow.Internal;

// Turns the source text of a selector lambda into a property path, without reflection
// or expression trees: "x => x.Address.City" becomes "Address.City".
internal static class SelectorNames
{
    public static string FromExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return ThrowExtensions.DefaultName;
        }

        var text = expression!.Trim();
        var arrow = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrow < 0)
        {
            return text;
        }

        var parameter = ParameterName(text.Substring(0, arrow));
        var body = text.Substring(arrow + 2).Trim();
        if (parameter.Length == 0)
        {
            return body;
        }

        if (body == parameter)
        {
            return ThrowExtensions.DefaultName;
        }

        return body.StartsWith(parameter + ".", StringComparison.Ordinal)
            ? body.Substring(parameter.Length + 1)
            : body;
    }

    // Handles "x", "(x)", "static x", "(User x)" and "static (User x)".
    private static string ParameterName(string text)
    {
        var name = text.Trim().TrimStart('(').TrimEnd(')').Trim();
        if (name.StartsWith("static ", StringComparison.Ordinal))
        {
            name = name.Substring("static ".Length).Trim().TrimStart('(').Trim();
        }

        var space = name.LastIndexOf(' ');
        if (space >= 0)
        {
            name = name.Substring(space + 1);
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '@')
            {
                return string.Empty;
            }
        }

        return name;
    }
}
