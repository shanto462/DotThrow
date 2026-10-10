using System;
using System.Text;

namespace DotThrow.Internal;

// Turns the source text of a selector lambda into a property path, without reflection
// or expression trees: "x => x.Address.City" becomes "Address.City".
// Null-forgiving and null-conditional operators are dropped, so "x => x.Address!.City"
// and "x => x.Address?.City" both become "Address.City".
internal static class SelectorNames
{
    // A selector that returns its parameter ("x => x") selects the object itself; its name
    // is identityName ("value" for guards, "" for validator rules, which means the whole object).
    public static string FromExpression(string? expression, string identityName = ThrowExtensions.DefaultName)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return ThrowExtensions.DefaultName;
        }

        var text = expression!.Trim();
        var arrow = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrow < 0)
        {
            return CleanPath(text);
        }

        var parameter = ParameterName(text.Substring(0, arrow));
        var body = CleanPath(text.Substring(arrow + 2).Trim());
        if (parameter.Length == 0)
        {
            return body;
        }

        if (body == parameter)
        {
            return identityName;
        }

        return body.StartsWith(parameter + ".", StringComparison.Ordinal)
            ? body.Substring(parameter.Length + 1)
            : body;
    }

    // Drops "!" before ".", "[" or the end, turns "?." into "." and "?[" into "[", and
    // removes white space around dots (selectors split over several lines). Text that
    // contains quotes is left alone, because the operators may be inside a literal.
    private static string CleanPath(string body)
    {
        if (body.IndexOfAny(['!', '?', ' ', '\t', '\r', '\n']) < 0 || body.IndexOfAny(['"', '\'']) >= 0)
        {
            return body;
        }

        var builder = new StringBuilder(body.Length);
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            var next = i + 1 < body.Length ? body[i + 1] : '\0';
            if (c == '!' && (next == '.' || next == '[' || next == '\0' || char.IsWhiteSpace(next)))
            {
                continue;
            }

            if (c == '?' && (next == '.' || next == '['))
            {
                continue;
            }

            if (char.IsWhiteSpace(c) && (StartsMemberAccess(body, i) || PreviousNonSpace(builder) == '.'))
            {
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    // True when the next non-space text is ".", "?." or "!.".
    private static bool StartsMemberAccess(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index >= text.Length)
        {
            return false;
        }

        return text[index] == '.'
            || ((text[index] == '?' || text[index] == '!') && index + 1 < text.Length && text[index + 1] == '.');
    }

    private static char PreviousNonSpace(StringBuilder builder) => builder.Length == 0 ? '\0' : builder[builder.Length - 1];

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
