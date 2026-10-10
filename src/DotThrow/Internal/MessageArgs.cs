using System.Collections.Generic;
using System.Text;

namespace DotThrow.Internal;

// The values a failed check passes to its message. The values keep their own types, so a
// number is formatted straight into the message without boxing, and a value is formatted
// only when the template uses it.
internal interface IMessageArgs
{
    bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format);
}

internal readonly struct NoArgs : IMessageArgs
{
    public bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format) => false;
}

internal readonly struct MessageArgs<TA> : IMessageArgs
{
    private readonly Placeholder _idA;
    private readonly TA _a;

    public MessageArgs(Placeholder idA, TA a)
    {
        _idA = idA;
        _a = a;
    }

    public bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format)
    {
        if (id == _idA)
        {
            return ValueFormatter.Append(builder, id, _a, format);
        }

        return false;
    }
}

internal readonly struct MessageArgs<TA, TB> : IMessageArgs
{
    private readonly Placeholder _idA;
    private readonly TA _a;
    private readonly Placeholder _idB;
    private readonly TB _b;

    public MessageArgs(Placeholder idA, TA a, Placeholder idB, TB b)
    {
        _idA = idA;
        _a = a;
        _idB = idB;
        _b = b;
    }

    public bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format)
    {
        if (id == _idA)
        {
            return ValueFormatter.Append(builder, id, _a, format);
        }

        if (id == _idB)
        {
            return ValueFormatter.Append(builder, id, _b, format);
        }

        return false;
    }
}

internal readonly struct MessageArgs<TA, TB, TC> : IMessageArgs
{
    private readonly MessageArgs<TA, TB> _first;
    private readonly Placeholder _idC;
    private readonly TC _c;

    public MessageArgs(Placeholder idA, TA a, Placeholder idB, TB b, Placeholder idC, TC c)
    {
        _first = new MessageArgs<TA, TB>(idA, a, idB, b);
        _idC = idC;
        _c = c;
    }

    public bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format)
    {
        if (_first.TryAppend(builder, id, name, format))
        {
            return true;
        }

        if (id == _idC)
        {
            return ValueFormatter.Append(builder, id, _c, format);
        }

        return false;
    }
}

internal readonly struct MessageArgs<TA, TB, TC, TD> : IMessageArgs
{
    private readonly MessageArgs<TA, TB> _first;
    private readonly MessageArgs<TC, TD> _second;

    public MessageArgs(Placeholder idA, TA a, Placeholder idB, TB b, Placeholder idC, TC c, Placeholder idD, TD d)
    {
        _first = new MessageArgs<TA, TB>(idA, a, idB, b);
        _second = new MessageArgs<TC, TD>(idC, c, idD, d);
    }

    public bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format) =>
        _first.TryAppend(builder, id, name, format) || _second.TryAppend(builder, id, name, format);
}

// Values by placeholder name, for MessageTemplates.Format.
internal readonly struct DictionaryArgs : IMessageArgs
{
    private readonly IReadOnlyDictionary<string, object?>? _values;

    public DictionaryArgs(IReadOnlyDictionary<string, object?>? values)
    {
        _values = values;
    }

    public bool TryAppend(StringBuilder builder, Placeholder id, string name, string? format)
    {
        if (_values is not null && _values.TryGetValue(name, out var value))
        {
            return ValueFormatter.Append(builder, id, value, format);
        }

        return false;
    }
}
