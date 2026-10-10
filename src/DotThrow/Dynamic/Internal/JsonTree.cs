using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DotThrow.Dynamic.Internal;

internal enum JsonKind
{
    Object,
    Array,
    String,
    Number,
    True,
    False,
    Null,
}

// A parsed JSON value. Numbers keep their text, so "5" and 5 read the same as arguments.
internal sealed class JsonNode
{
    private JsonNode(JsonKind kind, int position)
    {
        Kind = kind;
        Position = position;
    }

    public JsonKind Kind { get; }

    // Index of the first character, for error messages.
    public int Position { get; }

    // String text or number text.
    public string? Text { get; private set; }

    public List<KeyValuePair<string, JsonNode>>? Members { get; private set; }

    public List<JsonNode>? Items { get; private set; }

    public static JsonNode Scalar(JsonKind kind, int position, string? text) => new(kind, position) { Text = text };

    public static JsonNode Object(int position, List<KeyValuePair<string, JsonNode>> members) => new(JsonKind.Object, position) { Members = members };

    public static JsonNode Array(int position, List<JsonNode> items) => new(JsonKind.Array, position) { Items = items };
}

// A strict RFC 8259 reader that builds a small tree. Like the Json recognizer used by
// IfNotJson, it allows no comments, no trailing commas and no other extensions. Nesting is
// limited (definitions are shallow), so deep input cannot overflow the stack.
internal sealed class JsonTreeReader
{
    private const int MaxDepth = 64;

    private readonly string _text;
    private int _position;

    private JsonTreeReader(string text)
    {
        _text = text;
    }

    public static JsonNode Parse(string text)
    {
        var reader = new JsonTreeReader(text);

        // A byte order mark left by a text reader is not part of the JSON.
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            reader._position = 1;
        }

        reader.SkipWhiteSpace();
        var value = reader.ReadValue(0);
        reader.SkipWhiteSpace();
        if (reader._position < text.Length)
        {
            throw reader.Error("unexpected text after the end of the JSON value");
        }

        return value;
    }

    // "line 3, column 7" for a character index.
    public static string Place(string text, int position)
    {
        var line = 1;
        var column = 1;
        for (var i = 0; i < position && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return string.Format(CultureInfo.InvariantCulture, "line {0}, column {1}", line, column);
    }

    private JsonNode ReadValue(int depth)
    {
        if (depth > MaxDepth)
        {
            throw Error("the JSON is nested too deeply");
        }

        if (_position >= _text.Length)
        {
            throw Error("unexpected end of the JSON text");
        }

        var start = _position;
        switch (_text[_position])
        {
            case '{':
                return ReadObject(depth);
            case '[':
                return ReadArray(depth);
            case '"':
                return JsonNode.Scalar(JsonKind.String, start, ReadString());
            case 't':
                ReadLiteral("true");
                return JsonNode.Scalar(JsonKind.True, start, "true");
            case 'f':
                ReadLiteral("false");
                return JsonNode.Scalar(JsonKind.False, start, "false");
            case 'n':
                ReadLiteral("null");
                return JsonNode.Scalar(JsonKind.Null, start, null);
            default:
                return JsonNode.Scalar(JsonKind.Number, start, ReadNumber());
        }
    }

    private JsonNode ReadObject(int depth)
    {
        var start = _position++;
        var members = new List<KeyValuePair<string, JsonNode>>();
        SkipWhiteSpace();
        if (Peek() == '}')
        {
            _position++;
            return JsonNode.Object(start, members);
        }

        while (true)
        {
            SkipWhiteSpace();
            if (Peek() != '"')
            {
                throw Error("expected a property name in double quotes");
            }

            var key = ReadString();
            SkipWhiteSpace();
            Expect(':');
            SkipWhiteSpace();
            members.Add(new KeyValuePair<string, JsonNode>(key, ReadValue(depth + 1)));
            SkipWhiteSpace();
            if (Peek() == ',')
            {
                _position++;
                continue;
            }

            Expect('}');
            return JsonNode.Object(start, members);
        }
    }

    private JsonNode ReadArray(int depth)
    {
        var start = _position++;
        var items = new List<JsonNode>();
        SkipWhiteSpace();
        if (Peek() == ']')
        {
            _position++;
            return JsonNode.Array(start, items);
        }

        while (true)
        {
            SkipWhiteSpace();
            items.Add(ReadValue(depth + 1));
            SkipWhiteSpace();
            if (Peek() == ',')
            {
                _position++;
                continue;
            }

            Expect(']');
            return JsonNode.Array(start, items);
        }
    }

    private string ReadString()
    {
        _position++; // opening quote
        var builder = new StringBuilder();
        while (true)
        {
            if (_position >= _text.Length)
            {
                throw Error("a string is not closed");
            }

            var c = _text[_position++];
            if (c == '"')
            {
                return builder.ToString();
            }

            if (c < ' ')
            {
                _position--;
                throw Error("a control character must be escaped in a string");
            }

            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            if (_position >= _text.Length)
            {
                throw Error("a string is not closed");
            }

            var escape = _text[_position++];
            switch (escape)
            {
                case '"':
                    builder.Append('"');
                    break;
                case '\\':
                    builder.Append('\\');
                    break;
                case '/':
                    builder.Append('/');
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'u':
                    builder.Append(ReadHex4());
                    break;
                default:
                    _position--;
                    throw Error("unknown escape sequence");
            }
        }
    }

    private char ReadHex4()
    {
        if (_position + 4 > _text.Length)
        {
            throw Error("a \\u escape needs four hex digits");
        }

        var value = 0;
        for (var i = 0; i < 4; i++)
        {
            var c = _text[_position++];
            var digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => -1,
            };
            if (digit < 0)
            {
                _position--;
                throw Error("a \\u escape needs four hex digits");
            }

            value = (value * 16) + digit;
        }

        return (char)value;
    }

    // -?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?
    private string ReadNumber()
    {
        var start = _position;
        if (Peek() == '-')
        {
            _position++;
        }

        if (Peek() == '0')
        {
            _position++;
        }
        else if (IsDigit(Peek()))
        {
            while (IsDigit(Peek()))
            {
                _position++;
            }
        }
        else
        {
            throw Error("expected a value");
        }

        if (Peek() == '.')
        {
            _position++;
            RequireDigits();
        }

        if (Peek() is 'e' or 'E')
        {
            _position++;
            if (Peek() is '+' or '-')
            {
                _position++;
            }

            RequireDigits();
        }

        return _text.Substring(start, _position - start);
    }

    private void RequireDigits()
    {
        if (!IsDigit(Peek()))
        {
            throw Error("expected a digit");
        }

        while (IsDigit(Peek()))
        {
            _position++;
        }
    }

    private void ReadLiteral(string literal)
    {
        if (string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
        {
            throw Error("expected a value");
        }

        _position += literal.Length;
    }

    private void Expect(char c)
    {
        if (Peek() != c)
        {
            throw Error("expected '" + c + "'");
        }

        _position++;
    }

    private char Peek() => _position < _text.Length ? _text[_position] : '\0';

    private void SkipWhiteSpace()
    {
        while (_position < _text.Length && _text[_position] is ' ' or '\t' or '\r' or '\n')
        {
            _position++;
        }
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private RuleDefinitionException Error(string problem) =>
        new("JSON (" + Place(_text, _position) + "): " + problem + ".");
}
