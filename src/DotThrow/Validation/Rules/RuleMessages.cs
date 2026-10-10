using System;
using System.Collections.Generic;
using System.Text;

namespace DotThrow.Internal;

// Formats the message of a rule (WithMessage) and the default messages of Must rules, with the
// same template syntax and value formatting as the messages of the checks (MessageTemplate and
// ValueFormatter): {PropertyName}, {PropertyPath}, {PropertyValue}, {CollectionIndex}, the
// arguments of the failed check ({MaxLength}, {ComparisonValue}...) and AppendArgument values,
// each with an optional format, for example {PropertyValue:N2}. Unknown placeholders stay as
// they are. AppendArgument text is written as it is (it is usually words, as in
// FluentValidation); its other values use the check formatting.
internal static class RuleMessages
{
    public static string Must(string name) =>
        name.Length == 0
            ? "The specified condition was not met."
            : "The specified condition was not met for '" + name + "'.";

    // A message that is already final (a check's own message, or one given to AddFailure),
    // with the AppendArgument values filled in.
    public static string Format(string message, IReadOnlyDictionary<string, object?>? arguments) =>
        arguments is null || arguments.Count == 0 || message.IndexOf('{') < 0 ? message : FillArguments(message, arguments);

    // The rule message for a failure without check arguments (Must, Custom, Guard.Fail).
    public static string Format(string template, string name, string path, object? value, int index, IReadOnlyDictionary<string, object?>? arguments) =>
        Render(template, name, path, value, index, arguments, default(NoArgs));

    // The rule message with the arguments of the failed check, like FluentValidation, where
    // WithMessage("{PropertyName} must be at most {MaxLength} long") fills {MaxLength}.
    public static string Render<TValue, TArgs>(
        string template, string name, string path, TValue value, int index, IReadOnlyDictionary<string, object?>? arguments, TArgs args)
        where TArgs : struct, IMessageArgs =>
        template.IndexOf('{') < 0
            ? template
            : MessageTemplate.ForCustomMessage(template).Render(name, value, new RuleArgs<TArgs>(path, index, arguments, args));

    private static string FillArguments(string message, IReadOnlyDictionary<string, object?> arguments)
    {
        var builder = new StringBuilder(message.Length + 16);
        var position = 0;
        while (position < message.Length)
        {
            var open = message.IndexOf('{', position);
            var close = open < 0 ? -1 : message.IndexOf('}', open + 1);
            if (open < 0 || close < 0)
            {
                builder.Append(message, position, message.Length - position);
                break;
            }

            builder.Append(message, position, open - position);
            var key = message.Substring(open + 1, close - open - 1);
            var colon = key.IndexOf(':');
            var name = colon < 0 ? key : key.Substring(0, colon);
            if (arguments.TryGetValue(name, out var value)
                && AppendArgument(builder, Placeholders.Find(name), value, colon < 0 ? null : key.Substring(colon + 1)))
            {
                position = close + 1;
            }
            else
            {
                // Not an argument (or a format its value cannot use): keep the brace and look
                // for the next one after it.
                builder.Append('{');
                position = open + 1;
            }
        }

        return builder.ToString();
    }

    // False (nothing written) when the format does not suit the value.
    private static bool AppendArgument(StringBuilder builder, Placeholder id, object? value, string? format)
    {
        if (value is string text)
        {
            builder.Append(text);
            return true;
        }

        return ValueFormatter.Append(builder, id, value, format);
    }

    // Rule values first (AppendArgument, {PropertyPath}, {CollectionIndex}), then the values of
    // the failed check.
    private readonly struct RuleArgs<TArgs> : IMessageArgs
        where TArgs : struct, IMessageArgs
    {
        private readonly string _path;
        private readonly int _index;
        private readonly IReadOnlyDictionary<string, object?>? _arguments;
        private readonly TArgs _check;

        public RuleArgs(string path, int index, IReadOnlyDictionary<string, object?>? arguments, TArgs check)
        {
            _path = path;
            _index = index;
            _arguments = arguments;
            _check = check;
        }

        public bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format)
        {
            if (_arguments is not null && _arguments.TryGetValue(name, out var value))
            {
                return AppendArgument(builder, id, value, format);
            }

            if (string.Equals(name, "PropertyPath", StringComparison.Ordinal))
            {
                builder.Append(_path);
                return true;
            }

            if (_index >= 0 && string.Equals(name, "CollectionIndex", StringComparison.Ordinal))
            {
                return ValueFormatter.Append(builder, id, _index, format);
            }

            return _check.TryAppend(builder, id, name, format);
        }
    }
}
