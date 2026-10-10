using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotThrow.Internal;

// A message with named placeholders such as "'{PropertyName}' must be at most {MaxLength}
// characters long.", parsed once into literal parts and placeholder ids.
//
// Syntax: {Name} or {Name:format}. A name is letters, digits and underscores. A format is
// passed to IFormattable.ToString with the invariant culture, for example {From:N2}. A
// placeholder that has no value stays in the message as written, so text such as "{x}"
// or "{" in a custom message is left alone.
internal sealed class MessageTemplate
{
    // Custom messages are almost always string literals, which are interned, so the cache
    // parses each one once. It holds its keys weakly, so a message built at run time is
    // collected with its cache entry.
    private static readonly ConditionalWeakTable<string, MessageTemplate> CustomMessages = new();

    private readonly Segment[]? _segments;

    private MessageTemplate(string text, Segment[]? segments)
    {
        Text = text;
        _segments = segments;
    }

    public string Text { get; }

    public static MessageTemplate ForCustomMessage(string message) =>
        CustomMessages.GetValue(message, static text => Parse(text));

    public static MessageTemplate Parse(string text)
    {
        List<Segment>? segments = null;
        var literalStart = 0;
        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf('{', position);
            if (open < 0)
            {
                break;
            }

            var close = text.IndexOf('}', open + 1);
            if (close < 0)
            {
                break;
            }

            var nameStart = open + 1;
            var colon = text.IndexOf(':', nameStart, close - nameStart);
            var nameEnd = colon < 0 ? close : colon;
            if (!IsName(text, nameStart, nameEnd) || (colon >= 0 && text.IndexOf('{', colon, close - colon) >= 0))
            {
                position = open + 1;
                continue;
            }

            segments ??= [];
            if (open > literalStart)
            {
                segments.Add(new Segment(literalStart, open - literalStart, Placeholder.None, null, null));
            }

            var name = text.Substring(nameStart, nameEnd - nameStart);
            var format = colon < 0 || close == colon + 1 ? null : text.Substring(colon + 1, close - colon - 1);
            segments.Add(new Segment(open, close + 1 - open, Placeholders.Find(name), name, format));
            literalStart = position = close + 1;
        }

        if (segments is null)
        {
            return new MessageTemplate(text, null);
        }

        if (literalStart < text.Length)
        {
            segments.Add(new Segment(literalStart, text.Length - literalStart, Placeholder.None, null, null));
        }

        return new MessageTemplate(text, [.. segments]);
    }

    // Renders the message for a guard. {PropertyName} is the guard name, {PropertyValue} the
    // checked value; every other placeholder comes from the check's arguments.
    public string Render<TValue, TArgs>(string name, TValue value, TArgs args)
        where TArgs : struct, IMessageArgs
    {
        if (_segments is null)
        {
            return Text;
        }

        var builder = StringBuilderPool.Rent();
        foreach (var segment in _segments)
        {
            switch (segment.Id)
            {
                case Placeholder.None:
                    builder.Append(Text, segment.Start, segment.Length);
                    break;
                case Placeholder.PropertyName:
                    builder.Append(name);
                    break;
                case Placeholder.PropertyValue when typeof(TValue) != typeof(Unset):
                    if (!args.TryAppend(builder, segment.Id, segment.Name!, segment.Format))
                    {
                        ValueFormatter.Append(builder, segment.Id, value, segment.Format);
                    }

                    break;
                default:
                    if (!args.TryAppend(builder, segment.Id, segment.Name!, segment.Format))
                    {
                        builder.Append(Text, segment.Start, segment.Length);
                    }

                    break;
            }
        }

        return StringBuilderPool.ToStringAndReturn(builder);
    }

    private static bool IsName(string text, int start, int end)
    {
        if (end <= start || !(char.IsLetter(text[start]) || text[start] == '_'))
        {
            return false;
        }

        for (var i = start + 1; i < end; i++)
        {
            if (!(char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private readonly struct Segment
    {
        public Segment(int start, int length, Placeholder id, string? name, string? format)
        {
            Start = start;
            Length = length;
            Id = id;
            Name = name;
            Format = format;
        }

        // The literal text, or the whole "{...}" of a placeholder.
        public int Start { get; }

        public int Length { get; }

        public Placeholder Id { get; }

        public string? Name { get; }

        public string? Format { get; }
    }
}

// The "no value" marker for MessageTemplates.Format, which has a property name but no
// checked value. {PropertyValue} then comes from the dictionary or stays as written.
internal readonly struct Unset
{
}
