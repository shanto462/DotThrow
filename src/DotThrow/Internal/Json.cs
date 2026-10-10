using System;

namespace DotThrow.Internal;

// A strict RFC 8259 JSON syntax checker. It is iterative, so deep nesting cannot overflow
// the stack, and it allocates nothing until nesting goes deeper than 64 levels.
// It is shared by every target framework, so the result never depends on the runtime.
internal static class Json
{
    private enum Step
    {
        ExpectValue,
        Valid,
        Invalid,
    }

    public static bool IsValid(string text)
    {
        var containers = default(BitStack); // true = object, false = array
        var i = 0;
        SkipWhiteSpace(text, ref i);

        while (true)
        {
            if (i >= text.Length)
            {
                return false;
            }

            var c = text[i];
            if (c is '{' or '[')
            {
                var isObject = c == '{';
                i++;
                SkipWhiteSpace(text, ref i);
                if (i < text.Length && text[i] == (isObject ? '}' : ']'))
                {
                    // An empty container is a complete value.
                    i++;
                }
                else
                {
                    containers.Push(isObject);
                    if (isObject && !ReadKey(text, ref i))
                    {
                        return false;
                    }

                    continue;
                }
            }
            else if (!ReadScalar(text, ref i))
            {
                return false;
            }

            var step = AfterValue(text, ref i, ref containers);
            if (step != Step.ExpectValue)
            {
                return step == Step.Valid;
            }
        }
    }

    // After a complete value: close finished containers, then either reach the end of the
    // text or find the comma that starts the next value.
    private static Step AfterValue(string text, ref int i, ref BitStack containers)
    {
        while (true)
        {
            SkipWhiteSpace(text, ref i);
            if (containers.Count == 0)
            {
                return i == text.Length ? Step.Valid : Step.Invalid;
            }

            if (i >= text.Length)
            {
                return Step.Invalid;
            }

            var isObject = containers.Peek();
            var c = text[i];
            if (c == ',')
            {
                i++;
                SkipWhiteSpace(text, ref i);
                if (isObject && !ReadKey(text, ref i))
                {
                    return Step.Invalid;
                }

                return Step.ExpectValue;
            }

            if (c != (isObject ? '}' : ']'))
            {
                return Step.Invalid;
            }

            i++;
            containers.Pop();
        }
    }

    // Reads `"key" :` and the white space after the colon.
    private static bool ReadKey(string text, ref int i)
    {
        if (i >= text.Length || text[i] != '"' || !ReadString(text, ref i))
        {
            return false;
        }

        SkipWhiteSpace(text, ref i);
        if (i >= text.Length || text[i] != ':')
        {
            return false;
        }

        i++;
        SkipWhiteSpace(text, ref i);
        return true;
    }

    private static bool ReadScalar(string text, ref int i)
    {
        var c = text[i];
        return c switch
        {
            '"' => ReadString(text, ref i),
            't' => ReadLiteral(text, ref i, "true"),
            'f' => ReadLiteral(text, ref i, "false"),
            'n' => ReadLiteral(text, ref i, "null"),
            _ => c == '-' || IsDigit(c) ? ReadNumber(text, ref i) : false,
        };
    }

    private static bool ReadLiteral(string text, ref int i, string literal)
    {
        if (string.CompareOrdinal(text, i, literal, 0, literal.Length) != 0)
        {
            return false;
        }

        i += literal.Length;
        return true;
    }

    private static bool ReadString(string text, ref int i)
    {
        i++; // opening quote
        while (i < text.Length)
        {
            var c = text[i++];
            if (c == '"')
            {
                return true;
            }

            if (c < ' ')
            {
                return false;
            }

            if (c != '\\')
            {
                continue;
            }

            if (i >= text.Length)
            {
                return false;
            }

            var escape = text[i++];
            if (escape == 'u')
            {
                if (i + 4 > text.Length)
                {
                    return false;
                }

                for (var end = i + 4; i < end; i++)
                {
                    if (!IsHexDigit(text[i]))
                    {
                        return false;
                    }
                }
            }
            else if (escape is not ('"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't'))
            {
                return false;
            }
        }

        return false;
    }

    // number = [ "-" ] ( "0" / digit1-9 *DIGIT ) [ "." 1*DIGIT ] [ ( "e" / "E" ) [ "-" / "+" ] 1*DIGIT ]
    private static bool ReadNumber(string text, ref int i)
    {
        if (text[i] == '-')
        {
            i++;
        }

        if (i >= text.Length || !IsDigit(text[i]))
        {
            return false;
        }

        if (text[i] == '0')
        {
            i++;
        }
        else
        {
            SkipDigits(text, ref i);
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            if (i >= text.Length || !IsDigit(text[i]))
            {
                return false;
            }

            SkipDigits(text, ref i);
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            i++;
            if (i < text.Length && text[i] is '+' or '-')
            {
                i++;
            }

            if (i >= text.Length || !IsDigit(text[i]))
            {
                return false;
            }

            SkipDigits(text, ref i);
        }

        return true;
    }

    private static void SkipDigits(string text, ref int i)
    {
        while (i < text.Length && IsDigit(text[i]))
        {
            i++;
        }
    }

    private static void SkipWhiteSpace(string text, ref int i)
    {
        while (i < text.Length && text[i] is ' ' or '\t' or '\n' or '\r')
        {
            i++;
        }
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsHexDigit(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    // A stack of bits that lives in one ulong for the first 64 levels.
    private struct BitStack
    {
        private ulong _bits;
        private bool[]? _overflow;

        public int Count { get; private set; }

        public void Push(bool value)
        {
            if (Count < 64)
            {
                _bits = value ? _bits | (1UL << Count) : _bits & ~(1UL << Count);
            }
            else
            {
                var index = Count - 64;
                if (_overflow is null || index == _overflow.Length)
                {
                    Array.Resize(ref _overflow, Math.Max(64, index * 2));
                }

                _overflow[index] = value;
            }

            Count++;
        }

        public readonly bool Peek() => Count <= 64 ? (_bits & (1UL << (Count - 1))) != 0 : _overflow![Count - 65];

        public void Pop() => Count--;
    }
}
