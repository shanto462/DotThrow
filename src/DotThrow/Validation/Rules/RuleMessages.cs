using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DotThrow.Internal;

// Formats the message of a rule (WithMessage) and the default messages of Must rules.
// Placeholders: {PropertyName}, {PropertyPath}, {PropertyValue}, {CollectionIndex}, with an
// optional format, for example {PropertyValue:N2}. Unknown placeholders stay as they are.
internal static class RuleMessages
{
    public static string Must(string name) =>
        name.Length == 0
            ? "The specified condition was not met."
            : "The specified condition was not met for '" + name + "'.";

    // Formats a plain message with the arguments added by AppendArgument only.
    public static string Format(string message, IReadOnlyDictionary<string, object?>? arguments) =>
        arguments is null || arguments.Count == 0 ? message : Format(message, null, null, null, -1, arguments);

    public static string Format(string template, string? propertyName, string? propertyPath, object? value, int index, IReadOnlyDictionary<string, object?>? arguments)
    {
        if (template.IndexOf('{') < 0)
        {
            return template;
        }

        var builder = new StringBuilder(template.Length + 16);
        var position = 0;
        while (position < template.Length)
        {
            var open = template.IndexOf('{', position);
            var close = open < 0 ? -1 : template.IndexOf('}', open + 1);
            if (open < 0 || close < 0)
            {
                builder.Append(template, position, template.Length - position);
                break;
            }

            builder.Append(template, position, open - position);
            var key = template.Substring(open + 1, close - open - 1);
            var colon = key.IndexOf(':');
            var format = colon < 0 ? null : key.Substring(colon + 1);
            var name = colon < 0 ? key : key.Substring(0, colon);
            if (arguments is not null && arguments.TryGetValue(name, out var argument))
            {
                builder.Append(Text(argument, format));
            }
            else if (propertyName is not null && name == "PropertyName")
            {
                builder.Append(propertyName);
            }
            else if (propertyPath is not null && name == "PropertyPath")
            {
                builder.Append(propertyPath);
            }
            else if (propertyName is not null && name == "PropertyValue")
            {
                builder.Append(Text(value, format));
            }
            else if (index >= 0 && name == "CollectionIndex")
            {
                builder.Append(Text(index, format));
            }
            else
            {
                builder.Append(template, open, close - open + 1);
            }

            position = close + 1;
        }

        return builder.ToString();
    }

    private static string Text(object? value, string? format) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(format, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
