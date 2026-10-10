using System;
using System.Linq;
using System.Text;
using DotThrow.Internal;
using Xunit;
#if NET8_0_OR_GREATER
using System.Collections.Generic;
using System.Text.Json;
#endif

namespace DotThrow.Tests;

public sealed class JsonTests
{
    public static readonly string[] ValidTexts =
    [
        // Numbers
        "0", "-0", "1", "-1", "123", "1.5", "-1.5", "0.0", "0.5", "1e5", "1E5", "1e+5", "1E+2", "1e-5", "1.5e10",
        "-0.0e-0", "1E400", "-1E-400", "123456789012345678901234567890", "1.000000000000000000001",

        // Literals
        "true", "false", "null",

        // Strings
        "\"\"", "\"abc\"", "\"\\\"\"", "\"\\\\\"", "\"\\/\"", "\"/\"", "\"\\b\\f\\n\\r\\t\"", "\"\\u0000\"",
        "\"\\u00e9\"", "\"\\u00E9\"", "\"\\uD83D\\uDE00\"", "\"\\uDEAD\"", "\"\\uD800\"", "\"é日本\"", "\"\U0001F600\"",
        "\"\u007f\"", "\"\u2028\u2029\"", "\"' single quote\"", "\"a\\\\\\\"b\"",

        // Containers
        "[]", "[ ]", "{}", "{ }", "[1]", "[1,2,3]", "[1, \"a\", true, false, null, {}, []]", "{\"a\":1}",
        "{\"a\":1,\"b\":[1,2]}", "{\"a\":{\"b\":{\"c\":null}}}", "{\"\":0}", "{\"a\":1,\"a\":2}", "[[[[[]]]]]",
        "[{\"a\":[{\"b\":[]}]}]", "[\"]\", \"}\", \",\", \":\"]", "{\"{\":\"}\"}",

        // White space: only space, tab, line feed and carriage return.
        " 1 ", "\t\n\r 1 \t\n\r", "\t\n\r [ \t\n\r 1 \t\n\r , \t\n\r 2 \t\n\r ] \t\n\r ", "{ \"a\" : 1 , \"b\" : 2 }",
        "[\r\n  {\r\n    \"name\": \"Ada Lovelace\",\r\n    \"email\": \"ada@example.com\"\r\n  }\r\n]",
    ];

    public static readonly string[] InvalidTexts =
    [
        // Nothing
        "", " ", "\t\n\r",

        // Numbers
        "01", "-01", "00", "1.", ".5", "-", "+1", "1e", "1e+", "1E-", "1.e5", "0x10", "1_000", "Infinity", "-Infinity",
        "NaN", "1 2", "--1", "1.5.5", "1e5e5", "\u0661", "1,000", "- 1", "1e 5", "0.e1", "-.5", "1.5e+-3",

        // Literals
        "tru", "True", "TRUE", "nul", "NULL", "Null", "falsey", "nulll", "truefalse", "undefined", "t", "f", "n",

        // Strings
        "\"", "\"abc", "'abc'", "\"\\\"", "\"\\x\"", "\"\\u12\"", "\"\\u12G4\"", "\"\\U0041\"", "\"\\u\"", "\"\\\"",
        "\"\t\"", "\"\n\"", "\"\r\"", "\"\u0000\"", "\"\u001f\"", "\"a\"b\"", "\"abc\"\"", "\"\\a\"", "\"\\'\"", "\"\\0\"",

        // Containers
        "[", "]", "[1", "[1,]", "[,1]", "[1,,2]", "[1 2]", "[,]", "{", "}", "{\"a\"}", "{\"a\":}", "{\"a\":1,}",
        "{,\"a\":1}", "{\"a\" 1}", "{a:1}", "{1:1}", "{'a':1}", "{\"a\":1 \"b\":2}", "[}", "{]", "[1]]", "{}}", "[]]",
        "{\"a\":1}}", "[1}", "{\"a\":[1}", "{\"a\":1,,\"b\":2}", "{\"a\"::1}", "{:1}", "{null:1}", "[[]", "[[[]]",
        "{\"a\":{}", "{\"a\",\"b\"}", "[\"a\":1]",

        // Comments and other extensions
        "// c\n1", "/* c */ 1", "1 // c", "[1 /* c */]", "#1", "[1,2]//",

        // White space outside the four allowed characters, and a leading byte order mark
        "\uFEFF1", "\uFEFF{}", "1\u00A0", "\u00A01", "\f1", "\v1", "1\f", "[1,\u00A02]", "\u20281",

        // More than one value
        "[1]x", "{} {}", "1,", ",1", "[][]", "\"a\" \"b\"", "1 1", "null null",
    ];

    public static TheoryData<string> Valid => new(ValidTexts);

    public static TheoryData<string> Invalid => new(InvalidTexts);

    [Theory]
    [MemberData(nameof(Valid))]
    public void Accepts_valid_json(string text)
    {
        Assert.True(Json.IsValid(text));
        text.Throw().IfNotJson();
    }

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Rejects_invalid_json(string text)
    {
        Assert.False(Json.IsValid(text));
        Assert.Throws<ArgumentException>(() => text.Throw().IfNotJson());
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(200_000)]
    public void Deep_arrays_do_not_overflow_the_stack(int depth)
    {
        var valid = new string('[', depth) + new string(']', depth);
        var missingClose = new string('[', depth) + new string(']', depth - 1);
        var extraClose = new string('[', depth) + new string(']', depth + 1);

        Assert.True(Json.IsValid(valid));
        Assert.False(Json.IsValid(missingClose));
        Assert.False(Json.IsValid(extraClose));
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(100_000)]
    public void Deep_objects_do_not_overflow_the_stack(int depth)
    {
        var valid = string.Concat(Enumerable.Repeat("{\"a\":", depth)) + "0" + new string('}', depth);
        var missingValue = string.Concat(Enumerable.Repeat("{\"a\":", depth)) + new string('}', depth);

        Assert.True(Json.IsValid(valid));
        Assert.False(Json.IsValid(missingValue));
    }

    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(1_000)]
    public void Mixed_nesting_remembers_each_container_type_past_the_inline_stack(int depth)
    {
        // Every level adds a value after its child, so a wrong container type at any depth
        // makes the text invalid: an object needs "key": value, an array needs a value.
        var valid = MixedNesting(depth, breakAt: -1);

        Assert.True(Json.IsValid(valid));
        for (var level = 0; level < depth; level += Math.Max(1, depth / 10))
        {
            Assert.False(Json.IsValid(MixedNesting(depth, breakAt: level)), $"A wrong closing bracket at level {level} was accepted.");
        }
    }

    [Fact]
    public void Nesting_can_go_deep_again_after_coming_back_up()
    {
        var deep = new string('[', 100) + new string(']', 100);
        var text = "[" + deep + "," + "{\"a\":" + deep + "}," + deep + "]";

        Assert.True(Json.IsValid(text));
    }

    [Fact]
    public void Large_flat_documents_are_valid()
    {
        var items = string.Join(",", Enumerable.Range(0, 50_000).Select(i => "{\"id\":" + i + ",\"name\":\"Ada Lovelace\",\"ok\":true}"));

        Assert.True(Json.IsValid("[" + items + "]"));
        Assert.False(Json.IsValid("[" + items + ",]"));
    }

    [Fact]
    public void Lone_surrogate_characters_in_a_string_are_accepted()
    {
        // Known difference: the recognizer checks JSON syntax on UTF-16 text and does not
        // validate surrogate pairs. System.Text.Json rejects these because they cannot be
        // converted to UTF-8. The escaped forms (\uD800) are valid JSON in both.
        Assert.True(Json.IsValid("\"\uD800\""));
        Assert.True(Json.IsValid("\"\uDC00\""));
        Assert.True(Json.IsValid("\"a\uDBFFb\""));
        Assert.False(Json.IsValid("\uD800"));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Agrees_with_System_Text_Json_on_the_tables()
    {
        var disagreements = ValidTexts.Concat(InvalidTexts)
            .Where(text => Json.IsValid(text) != ParsesWithSystemTextJson(text))
            .Select(Escape)
            .ToList();

        Assert.Empty(disagreements);
    }

    [Fact]
    public void Agrees_with_System_Text_Json_on_deep_nesting()
    {
        var texts = new[]
        {
            new string('[', 5_000) + new string(']', 5_000),
            new string('[', 5_000) + new string(']', 4_999),
            MixedNesting(300, breakAt: -1),
            MixedNesting(300, breakAt: 150),
        };

        Assert.All(texts, text => Assert.Equal(ParsesWithSystemTextJson(text), Json.IsValid(text)));
    }

    [Fact]
    public void Agrees_with_System_Text_Json_on_random_and_mutated_documents()
    {
        var generator = new JsonGenerator(seed: 20261010);
        var disagreements = new List<string>();
        var checkedCount = 0;
        var validCount = 0;

        for (var i = 0; i < 4_000; i++)
        {
            var document = generator.Document();
            foreach (var text in new[] { document, generator.Mutate(document), generator.Mutate(document) })
            {
                if (HasLoneSurrogate(text))
                {
                    // See Lone_surrogate_characters_in_a_string_are_accepted.
                    continue;
                }

                checkedCount++;
                var ours = Json.IsValid(text);
                validCount += ours ? 1 : 0;
                if (ours != ParsesWithSystemTextJson(text))
                {
                    disagreements.Add($"ours={ours}: {Escape(text)}");
                }
            }
        }

        Assert.Empty(disagreements);
        Assert.True(checkedCount > 11_000, $"Only {checkedCount} texts were checked.");

        // Both sides of the decision must be well represented.
        Assert.InRange(validCount, checkedCount / 5, checkedCount * 4 / 5);
    }

    private static bool ParsesWithSystemTextJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 1_000_000 });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            // Text that cannot be converted to UTF-8, such as a lone surrogate.
            return false;
        }
    }

    private static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }
#endif

    // Builds nesting like {"k":[{"k":[ ... 0 ... ,2]},"z":1} where each level adds a sibling
    // after its child. breakAt swaps the closing bracket of one level for the wrong one.
    private static string MixedNesting(int depth, int breakAt)
    {
        var text = new StringBuilder();
        for (var level = 0; level < depth; level++)
        {
            text.Append(level % 2 == 0 ? "{\"k\":" : "[");
        }

        text.Append('0');
        for (var level = depth - 1; level >= 0; level--)
        {
            var isObject = level % 2 == 0;
            var close = isObject ? '}' : ']';
            if (level == breakAt)
            {
                close = isObject ? ']' : '}';
            }

            text.Append(isObject ? ",\"z\":1" : ",2").Append(close);
        }

        return text.ToString();
    }

#if NET8_0_OR_GREATER
    private static string Escape(string text)
    {
        var shown = text.Length > 200 ? string.Concat(text.AsSpan(0, 200), "...") : text;
        return string.Concat(shown.Select(c => c < ' ' || c > '~' ? "\\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture) : c.ToString()));
    }

    /// <summary>Makes random JSON documents and random edits of them, from a fixed seed.</summary>
    private sealed class JsonGenerator(int seed)
    {
        private const string MutationAlphabet = "{}[]\",:\\ 0123456789-+.eEtrufalsn\t\n\r\u0001\u00A0\uFEFF/x'";
        private static readonly string[] WhiteSpace = ["", "", " ", "\t", "\n", "\r\n", "  "];

        private readonly Random _random = new(seed);

        public string Document() => Space() + Value(0) + Space();

        public string Mutate(string text)
        {
            var result = new StringBuilder(text);
            for (var edits = _random.Next(1, 3); edits > 0; edits--)
            {
                if (result.Length == 0)
                {
                    result.Append(MutationAlphabet[_random.Next(MutationAlphabet.Length)]);
                    continue;
                }

                var index = _random.Next(result.Length);
                switch (_random.Next(5))
                {
                    case 0:
                        result.Remove(index, 1);
                        break;
                    case 1:
                        result.Insert(index, MutationAlphabet[_random.Next(MutationAlphabet.Length)]);
                        break;
                    case 2:
                        result[index] = MutationAlphabet[_random.Next(MutationAlphabet.Length)];
                        break;
                    case 3:
                        result.Insert(index, result[index]);
                        break;
                    default:
                        result.Length = index;
                        break;
                }
            }

            return result.ToString();
        }

        private string Space() => WhiteSpace[_random.Next(WhiteSpace.Length)];

        private string Value(int depth)
        {
            switch (depth > 4 ? _random.Next(4) : _random.Next(6))
            {
                case 0:
                case 3:
                    return Number();
                case 1:
                    return Text();
                case 2:
                    return new[] { "true", "false", "null" }[_random.Next(3)];
                case 4:
                    {
                        var count = _random.Next(4);
                        var items = Enumerable.Range(0, count).Select(_ => Space() + Value(depth + 1) + Space()).ToList();
                        return "[" + (count == 0 ? Space() : string.Join(",", items)) + "]";
                    }

                default:
                    {
                        var count = _random.Next(4);
                        var members = Enumerable.Range(0, count).Select(_ => Space() + Text() + Space() + ":" + Space() + Value(depth + 1) + Space()).ToList();
                        return "{" + (count == 0 ? Space() : string.Join(",", members)) + "}";
                    }
            }
        }

        private string Number()
        {
            var text = new StringBuilder();
            if (_random.Next(3) == 0)
            {
                text.Append('-');
            }

            if (_random.Next(4) == 0)
            {
                text.Append('0');
            }
            else
            {
                text.Append((char)('1' + _random.Next(9)));
                for (var i = _random.Next(5); i > 0; i--)
                {
                    text.Append((char)('0' + _random.Next(10)));
                }
            }

            if (_random.Next(3) == 0)
            {
                text.Append('.');
                for (var i = _random.Next(1, 4); i > 0; i--)
                {
                    text.Append((char)('0' + _random.Next(10)));
                }
            }

            if (_random.Next(3) == 0)
            {
                text.Append(_random.Next(2) == 0 ? 'e' : 'E');
                var sign = _random.Next(3);
                if (sign == 1)
                {
                    text.Append('+');
                }
                else if (sign == 2)
                {
                    text.Append('-');
                }

                for (var i = _random.Next(1, 3); i > 0; i--)
                {
                    text.Append((char)('0' + _random.Next(10)));
                }
            }

            return text.ToString();
        }

        private string Text()
        {
            var text = new StringBuilder("\"");
            for (var i = _random.Next(6); i > 0; i--)
            {
                switch (_random.Next(8))
                {
                    case 0:
                        text.Append('\\').Append("\"\\/bfnrt"[_random.Next(8)]);
                        break;
                    case 1:
                        text.Append("\\u").Append(_random.Next(0x10000).ToString(_random.Next(2) == 0 ? "x4" : "X4", System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case 2:
                        text.Append("é日\u2028\u007f"[_random.Next(4)]);
                        break;
                    case 3:
                        text.Append("\U0001F600");
                        break;
                    default:
                        var c = (char)_random.Next(0x20, 0x7F);
                        text.Append(c is '"' or '\\' ? 'a' : c);
                        break;
                }
            }

            return text.Append('"').ToString();
        }
    }
#endif
}
