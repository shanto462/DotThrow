using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DotThrow.Dynamic.Internal;
using FsCheck;
using FsCheck.Fluent;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Input generators for the fuzz tests. <see cref="AnyText"/> is random text with the
/// characters that break parsers (control characters, nulls, lone surrogates, bidi and
/// zero-width characters). The grammar generators build text from the pieces of one format
/// (email addresses, URIs, JSON, XML, IP addresses, message templates), mostly valid, and
/// <see cref="Mutated"/> changes a few characters of it, so the inputs reach deep into the
/// parsers instead of failing on the first character.
/// </summary>
internal static class Generators
{
    // Characters that are special somewhere: Unicode white space, zero-width and bidi format
    // characters, the byte order mark, noncharacters, letters whose case maps to ASCII (Kelvin
    // sign, dotted and dotless I), digits of other scripts, a combining mark.
    private const string SpecialChars =
        "\u0085\u00A0\u00AD\u1680\u180E\u2000\u200B\u200C\u200D\u200E\u200F\u2028\u2029\u202A\u202E\u2060\u2066\u3000"
        + "\uFEFF\uFFFD\uFFFE\uFFFF\u212A\u0130\u0131\u017F\u00DF\u00E9\u0661\u0966\uFF10\u0301\u2160";

    // Letters and other characters outside the Basic Multilingual Plane (surrogate pairs).
    private static readonly string[] Supplementary = ["\U0001F600", "\U0001D400", "\U00020000", "\U0001F1FA\U0001F1F8", "\U00010400", "\U0001D7CE", "\U000E0001", "\U0010FFFF"];

    /// <summary>Any character, weighted toward printable ASCII.</summary>
    public static readonly Gen<char> AnyChar = Gen.Frequency(
        (40, Gen.Choose(' ', '~').Select(c => (char)c)),
        (8, Gen.Frequency((4, Gen.Choose(0, 0x1F)), (1, Gen.Constant(0x7F)), (1, Gen.Choose(0x80, 0x9F))).Select(c => (char)c)),
        (6, Gen.Choose(0xA0, 0xD7FF).Select(c => (char)c)),
        (2, Gen.Choose(0xE000, 0xFFFF).Select(c => (char)c)),
        (6, Gen.Elements(SpecialChars.ToCharArray())),
        (3, Gen.Choose(0xD800, 0xDFFF).Select(c => (char)c)));

    /// <summary>One text element: a character, or a surrogate pair now and then.</summary>
    public static readonly Gen<string> AnyUnit = Gen.Frequency(
        (12, AnyChar.Select(c => c.ToString())),
        (1, Gen.Elements(Supplementary)));

    /// <summary>Random text of up to about 300 characters, and null now and then.</summary>
    public static readonly Gen<string?> AnyTextGen = Gen.Frequency(
        (1, Gen.Constant<string?>(null)),
        (16, AsNullable(TextOf(AnyUnit, 60))),
        (3, AsNullable(TextOf(AnyUnit, 300))),
        (3, AsNullable(TextOf(Gen.Elements("a", "0", " ", ".", "-", "@", ":", "/", "\\", "{", "}", "\"", "<", "&", "%", "=", "+"), 40))));

    public static readonly Arbitrary<string?> AnyText = Arb.From(AnyTextGen, Shrink);

    public static Arbitrary<string?> Of(Gen<string> gen) => Arb.From(AsNullable(gen), Shrink);

    public static Gen<string?> AsNullable(Gen<string> gen) => gen.Select(text => (string?)text);

    /// <summary>Text of up to <paramref name="max"/> pieces (fewer while the FsCheck size is small).</summary>
    public static Gen<string> TextOf(Gen<string> piece, int max) =>
        Gen.Sized(size => Gen.Choose(0, Math.Max(1, Math.Min(max, size * max / 50))))
            .SelectMany(count => piece.ArrayOf(count))
            .Select(pieces => string.Concat(pieces));

    /// <summary>Text from pieces: between <paramref name="min"/> and <paramref name="max"/> pieces.</summary>
    public static Gen<string> Join(Gen<string> piece, int min, int max, string separator = "") =>
        Gen.Choose(min, max).SelectMany(count => piece.ArrayOf(count)).Select(pieces => string.Join(separator, pieces));

    public static Gen<string> Concat(params Gen<string>[] parts) =>
        parts.Aggregate(Gen.Constant(string.Empty), (all, part) => all.SelectMany(left => part.Select(right => left + right)));

    public static Gen<string> Optional(Gen<string> part, int weight = 1) => Gen.Frequency((weight, part), (2, Gen.Constant(string.Empty)));

    /// <summary>
    /// Valid inputs as they are (three in five) and with one to three random edits: a character or
    /// a piece of the grammar inserted, characters deleted or replaced, a part repeated, the text
    /// cut short.
    /// </summary>
    public static Gen<string> Mutated(Gen<string> valid, string grammarChars)
    {
        var piece = Gen.Frequency((3, Gen.Elements(grammarChars.Select(c => c.ToString()).ToArray())), (2, AnyUnit));
        var edit = Gen.Frequency(
            (4, Gen.Zip(Gen.Choose(0, 10_000), piece).Select(e => (Func<string, string>)(text => text.Insert(e.Item1 % (text.Length + 1), e.Item2)))),
            (3, Gen.Zip(Gen.Choose(0, 10_000), Gen.Choose(1, 4)).Select(e => (Func<string, string>)(text => Delete(text, e.Item1, e.Item2)))),
            (3, Gen.Zip(Gen.Choose(0, 10_000), piece).Select(e => (Func<string, string>)(text => Replace(text, e.Item1, e.Item2)))),
            (1, Gen.Zip(Gen.Choose(0, 10_000), Gen.Choose(1, 12)).Select(e => (Func<string, string>)(text => Repeat(text, e.Item1, e.Item2)))),
            (1, Gen.Choose(0, 10_000).Select(at => (Func<string, string>)(text => text.Length == 0 ? text : text.Substring(0, at % text.Length)))));
        var mutated = valid.SelectMany(text => Gen.Choose(1, 3).SelectMany(count => edit.ArrayOf(count))
            .Select(edits => edits.Aggregate(text, (current, apply) => apply(current))));
        return Gen.Frequency((3, valid), (2, mutated));
    }

    /// <summary>
    /// Smaller texts for FsCheck to try after a failure: halves, then shorter by blocks and by one
    /// character, then simpler characters. FsCheck keeps the first smaller text that still fails.
    /// </summary>
    public static IEnumerable<string?> Shrink(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        for (var block = text!.Length / 2; block >= 1; block /= 2)
        {
            for (var start = 0; start + block <= text.Length; start += block)
            {
                yield return text.Remove(start, block);
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != 'a' && !char.IsSurrogate(text[i]))
            {
                yield return text.Remove(i, 1).Insert(i, "a");
            }
        }
    }

    private static string Delete(string text, int at, int count)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var start = at % text.Length;
        return text.Remove(start, Math.Min(count, text.Length - start));
    }

    private static string Replace(string text, int at, string piece)
    {
        if (text.Length == 0)
        {
            return piece;
        }

        var start = at % text.Length;
        return text.Remove(start, 1).Insert(start, piece);
    }

    private static string Repeat(string text, int at, int count)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var start = at % text.Length;
        var part = text.Substring(start, Math.Min(count, text.Length - start));
        return text.Insert(start, part);
    }

    private static Gen<string> Chars(string alphabet) => Gen.Elements(alphabet.Select(c => c.ToString()).ToArray());

    private static Gen<string> Number(int min, int max) => Gen.Choose(min, max).Select(n => n.ToString(CultureInfo.InvariantCulture));

    // ---------------------------------------------------------------- IP addresses

    /// <summary>Dotted-decimal IPv4 text, valid or almost valid (leading zeros, 256, three or five parts).</summary>
    public static readonly Gen<string> IPv4Text = Join(
        Gen.Frequency(
            (12, Number(0, 255)),
            (1, Gen.Elements("256", "999", "1000", "01", "00", "000", "0255", "", "-1", "+1", "1a", "0x1", " 1", "\u0661")),
            (1, Number(0, 9).Select(n => "0" + n))),
        3,
        5,
        ".");

    /// <summary>
    /// A valid IPv6 address with its 16 bytes: random groups, a run of zero groups that may be
    /// written as "::", groups in either case with leading zeros, and an IPv4 tail sometimes.
    /// </summary>
    public static readonly Gen<(string Text, byte[] Bytes)> ValidIPv6 =
        from groups in Gen.Frequency((3, Gen.Choose(0, 0xFFFF)), (2, Gen.Constant(0)), (1, Gen.Choose(0, 0xF))).ArrayOf(8)
        from zeroStart in Gen.Choose(0, 8)
        from zeroLength in Gen.Choose(0, 8)
        from compress in Gen.Elements(true, true, false)
        from tail in Gen.Elements(false, false, false, true)
        from upper in Gen.Elements(false, false, true)
        from padded in Gen.Elements(false, false, true)
        select RenderIPv6(groups, zeroStart, zeroLength, compress, tail, upper, padded);

    /// <summary>IPv6 text: valid addresses, with a zone, brackets or a port sometimes, and broken forms.</summary>
    public static readonly Gen<string> IPv6Text = Gen.Frequency(
        (8, ValidIPv6.Select(address => address.Text)),
        (2, from address in ValidIPv6
            from zone in Gen.Elements("%eth0", "%1", "%en0.1", "%a_b-c", "%", "%a b", "%25", "%\u00E9", "%%1")
            select address.Text + zone),
        (1, ValidIPv6.Select(address => "[" + address.Text + "]")),
        (1, ValidIPv6.Select(address => "[" + address.Text + "]:443")),
        (2, Gen.Elements(
            ":", "::", ":::", "1::2::3", "1:2:3:4:5:6:7:8:9", "1:2:3:4:5:6:7", "12345::", "::ffff:1.2.3.256", "::1.2.3",
            "1:2:3:4:5:6:7:1.2.3.4", "1.2.3.4::", "::1.2.3.4:1", "fe80::1%", "g::", ":1::", "1::2:", "::ffff:01.2.3.4",
            "0:0:0:0:0:0:0:0:", "::1 ", " ::1", "\u0661::1")));

    private static (string Text, byte[] Bytes) RenderIPv6(int[] groups, int zeroStart, int zeroLength, bool compress, bool tail, bool upper, bool padded)
    {
        var values = (int[])groups.Clone();
        zeroLength = Math.Min(zeroLength, 8 - zeroStart);
        for (var i = zeroStart; i < zeroStart + zeroLength; i++)
        {
            values[i] = 0;
        }

        var bytes = new byte[16];
        for (var i = 0; i < 8; i++)
        {
            bytes[2 * i] = (byte)(values[i] >> 8);
            bytes[(2 * i) + 1] = (byte)values[i];
        }

        // With a tail the last two groups are written as an IPv4 address.
        var count = tail ? 6 : 8;
        var parts = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var hex = values[i].ToString(upper ? "X" : "x", CultureInfo.InvariantCulture);
            parts.Add(padded ? hex.PadLeft(4, '0') : hex);
        }

        var ipv4 = tail ? string.Join(".", bytes.Skip(12).Select(b => b.ToString(CultureInfo.InvariantCulture))) : null;
        var runEnd = Math.Min(zeroStart + zeroLength, count);
        string text;
        if (compress && zeroLength > 0 && zeroStart < count && runEnd > zeroStart)
        {
            var left = string.Join(":", parts.Take(zeroStart));
            var right = string.Join(":", parts.Skip(runEnd));
            if (ipv4 is not null)
            {
                right = right.Length == 0 ? ipv4 : right + ":" + ipv4;
            }

            text = left + "::" + right;
        }
        else
        {
            text = string.Join(":", parts) + (ipv4 is null ? string.Empty : ":" + ipv4);
        }

        return (text, bytes);
    }

    // ---------------------------------------------------------------- Email addresses

    private const string AtomChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&'*+-/=?^_`{|}~";

    private static readonly Gen<string> Atom = Join(
        Gen.Frequency(
            (30, Chars(AtomChars)),
            (3, Gen.Elements("\u00E9", "\u00DF", "\u7528", "\u6237", "\u0416", "\u0627", "\u0661", "\U0001D400", "\U00020000")),
            (1, Gen.Elements("\U0001F600", "\u0301", "\u200B", " ", "\"", "(", ")", ",", ":", ";", "<", ">", "@", "[", "\\", "]", "\t", "\0", "\uD800"))),
        1,
        10);

    private static readonly Gen<string> DotAtom = Gen.Frequency(
        (10, Join(Atom, 1, 4, ".")),
        (1, Join(Atom, 1, 3, ".").Select(text => "." + text)),
        (1, Join(Atom, 1, 3, ".").Select(text => text + ".")),
        (1, Join(Atom, 2, 3, "..")));

    private static readonly Gen<string> QuotedLocal = Join(
        Gen.Frequency(
            (20, Chars(" !#$%&'()*+,-./0123456789:;<=>?@ABCXYZ[]^_`abcxyz{|}~")),
            (4, Chars("\"\\ az~").Select(c => "\\" + c)),
            (1, Gen.Elements("\t", "\u00E9", "\0", "\\", "\"", "\\\t", "\\\u00E9"))),
        0,
        12).Select(text => "\"" + text + "\"");

    private static readonly Gen<string> Label = Gen.Frequency(
        (20, Join(Chars("abcdefghijklmnopqrstuvwxyzABCXYZ0123456789-"), 1, 12)),
        (2, Gen.Elements("\u4F8B\u5B50", "b\u00FCcher", "xn--bcher-kva", "\u0645\u062B\u0627\u0644", "\U0001D400a")),
        (1, Gen.Elements("-a", "a-", "-", "a_b", "a b", "")),
        (1, Gen.Choose(62, 64).Select(length => new string('a', length))));

    private static readonly Gen<string> MailDomain = Gen.Frequency(
        (14, Join(Label, 1, 4, ".")),
        (1, Join(Label, 1, 3, ".").Select(text => text + ".")),
        (1, Gen.Choose(240, 256).Select(length => string.Join(".", Enumerable.Repeat(new string('d', 9), (length / 10) + 1)).Substring(0, length).TrimEnd('.'))),
        (2, IPv4Text.Select(address => "[" + address + "]")),
        (2, from tag in Gen.Elements("IPv6:", "ipv6:", "IPV6:", "IPv6", "IPv4:", "")
            from address in IPv6Text
            select "[" + tag + address + "]"),
        (1, Gen.Elements("[]", "[", "]", "[1.2.3.4", "1.2.3.4]", "[IPv6:]", "[IPv6:::1", "[1.2.3.4]x")));

    private static readonly Gen<string> ValidishEmail = Gen.Frequency(
        (12, Concat(Gen.Frequency((6, DotAtom), (1, QuotedLocal)), Gen.Constant("@"), MailDomain)),
        (1, Concat(Gen.Choose(60, 70).Select(length => new string('l', length)), Gen.Constant("@example.com"))),
        (1, Concat(Gen.Choose(30, 34).Select(length => string.Concat(Enumerable.Repeat("\u00E9", length))), Gen.Constant("@example.com"))),
        (1, Concat(DotAtom, Gen.Constant("@"), DotAtom, Gen.Constant("@"), MailDomain)),
        (1, Gen.Elements("@", "a@", "@b", "\"@b", "\"a\"b@c", "a@b@c", "\"\"@a", "\"\\\"@a")));

    /// <summary>Email-like text: dot-atom and quoted local parts, Unicode letters, address literals, long parts, and edits of them.</summary>
    public static readonly Gen<string> EmailLike = Mutated(ValidishEmail, "@.\"\\[]:-_ +");

    // ---------------------------------------------------------------- URIs

    private static readonly Gen<string> Scheme = Gen.Frequency(
        (8, Gen.Elements("http", "https")),
        (3, Gen.Elements("HTTP", "HtTpS", "ftp", "file", "mailto", "urn", "ws", "wss", "data", "javascript", "git+ssh", "a.b-c+d", "news", "ldap", "tel")),
        (1, Gen.Elements("1http", "ht tp", "", "-a", "h_t", "\u00E9", "http\0")));

    private static readonly Gen<string> UriHost = Gen.Frequency(
        (8, Join(Join(Chars("abcxyz0129-"), 1, 8), 1, 3, ".")),
        (4, Gen.Elements(
            "localhost", "LOCALHOST", "localhost.", "a.localhost", "a.LOCALHOST.", "localhost.example", "127.0.0.1", "127.0.0.1.", "127.1",
            "0x7f.0.0.1", "2130706433", "0177.0.0.1", "0.0.0.0", "[::1]", "[::ffff:127.0.0.1]", "[0:0:0:0:0:0:0:1]", "[::]",
            "[fe80::1%25eth0]", "[::ffff:7f00:1]", "b\u00FCcher.example", "xn--bcher-kva.example", "\u4F8B\u5B50.\u5E7F\u544A",
            "[1::2::3]", "[v1.x]", "a..b", ".a", "a_b.example", "", "[::1", "::1]", "a%41b", "%7F")),
        (2, IPv4Text),
        (2, IPv6Text.Select(address => "[" + address + "]")),
        // Bidi control characters, which Uri removes from the text before it reads the host.
        (1, Join(Chars("\u200E\u200F\u202A\u202B\u202C\u202D\u202E"), 1, 3)));

    private static readonly Gen<string> UriPiece = Join(
        Gen.Frequency(
            (80, Chars("abcxyzABC0129-._~!$&'()*+,;=:@")),
            (8, Gen.Elements("%20", "%41", "%2F", "%7e", "%C3%A9", "%00", "%FF")),
            (2, Gen.Elements("%zz", "%4", "%", "%%")),
            (6, Gen.Elements("stra\u00DFe", "\U0001F600", "\u00E9", "\u4F8B")),
            (2, Gen.Elements(" ", "\\", "|", "{", "}", "<", ">", "\"", "^", "`", "\u202E", "\u200B", "\0", "\t", "\n", "\u00A0", "#", "?", "[", "]", "\uD800"))),
        0,
        8);

    private static readonly Gen<string> UriPath = Join(UriPiece, 0, 4, "/").Select(path => path.Length == 0 ? path : "/" + path);

    private static readonly Gen<string> Authority = Concat(
        Optional(Gen.Elements("user@", "user:pw@", "@", "a@b@", "us%20er@", "\u00E9@")),
        UriHost,
        Optional(Gen.Elements(":80", ":443", ":", ":0", ":65535", ":65536", ":99999999999", ":-1", ":a", ":8080", ": 80")));

    private static readonly Gen<string> ValidishUri = Gen.Frequency(
        (10, Concat(Scheme, Gen.Frequency((12, Gen.Constant("://")), (1, Gen.Elements(":/", ":", ":///", "//"))), Authority, UriPath, Optional(UriPiece.Select(q => "?" + q)), Optional(UriPiece.Select(f => "#" + f)))),
        (2, Concat(Gen.Elements("mailto:", "urn:", "tel:", "data:", "javascript:", "news:", "x:"), UriPiece)),
        (3, Gen.Frequency(
            (3, UriPath),
            (1, UriPiece),
            (1, Concat(Gen.Constant("//"), Authority, UriPath)),
            (1, UriPiece.Select(q => "?" + q)),
            (1, UriPiece.Select(f => "#" + f)),
            (1, Concat(Gen.Elements("./", "../", "../../"), UriPiece)))),
        (1, Gen.Elements(
            "C:\\temp", "C:/temp", "c:", "C:", "/etc/hosts", "\\\\server\\share", "file:///etc/hosts", "file://server/share",
            "file:///C:/x", "file:C:/x", "file:", "//", "///", "/", "?", "#", " ", "\t", "file://\u202E", "\\\\\u200F\\a")));

    /// <summary>
    /// The URI for the text (absolute or relative), or null. Uri itself can throw
    /// IndexOutOfRangeException instead of failing, for a file or UNC host made only of bidi
    /// control characters, for example "file://\u202E" (seen on .NET 10).
    /// </summary>
    public static System.Uri? ToUri(string? text)
    {
        try
        {
            return text is not null && System.Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri) ? uri : null;
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>URI-like text: absolute, opaque and relative forms, IP and IDN hosts, ports, escapes, file paths, and edits of them.</summary>
    public static readonly Gen<string> UriLike = Mutated(ValidishUri, ":/?#[]@!$&'()*+,;=%\\. ");

    // ---------------------------------------------------------------- JSON

    private static readonly Gen<string> JsonWhiteSpace = Gen.Frequency(
        (12, Gen.Constant(string.Empty)),
        (6, Gen.Elements(" ", "\n", "\r\n", "\t", "  ")),
        (1, Gen.Elements("\u00A0", "\v", "\f", "\u2028", "\uFEFF", "\u3000", "\0")));

    private static readonly Gen<string> JsonNumber = Gen.Frequency(
        (10, Concat(
            Optional(Gen.Constant("-")),
            Gen.Frequency((1, Gen.Constant("0")), (4, Concat(Chars("123456789"), Join(Chars("0123456789"), 0, 6)))),
            Optional(Join(Chars("0123456789"), 1, 4).Select(digits => "." + digits)),
            Optional(Concat(Gen.Elements("e", "E"), Gen.Elements("", "+", "-"), Join(Chars("0123456789"), 1, 3))))),
        (2, Gen.Elements(
            "01", "-01", "1.", ".5", "+1", "1e", "1e+", "-", "--1", "0x1F", "NaN", "Infinity", "-Infinity", "1_000", "\u0661", "1.0.0", "1e1.5",
            "00", "-0", "0e0", "1E400", "-1e-400", "123456789012345678901234567890", "0.0000000000000000000000001")));

    private static readonly Gen<string> JsonStringContent = Join(
        Gen.Frequency(
            (24, Chars("abcXYZ019 !#$%&'()*+,-./:;<=>?@[]^_`{|}~")),
            (6, Gen.Elements("\\\"", "\\\\", "\\/", "\\b", "\\f", "\\n", "\\r", "\\t", "\\u0041", "\\u00e9", "\\uFFFF", "\\u0000")),
            (2, Gen.Elements("\\uD83D\\uDE00", "\\uD800", "\\uDC00", "\\uDE00\\uD83D")),
            (2, Gen.Elements("\u00E9", "\u4F8B", "\U0001F600", "\u2028", "\u007F", "\u0080")),
            (1, Gen.Elements("\\x", "\\u12", "\\U0041", "\\'", "\\", "\\a", "\\u12G4", "\n", "\t", "\0", "\u001F", "\"", "\uD800", "\uDC00"))),
        0,
        12);

    private static readonly Gen<string> JsonString = JsonStringContent.Select(content => "\"" + content + "\"");

    private static readonly Gen<string> JsonScalar = Gen.Frequency(
        (4, JsonNumber),
        (5, JsonString),
        (2, Gen.Elements("true", "false", "null")),
        (1, Gen.Elements("True", "nul", "nulll", "tru", "falsey", "undefined", "'a'", "\"a", "a\"", "")));

    private static Gen<string> JsonValue(int depth) => depth <= 0
        ? JsonScalar
        : Gen.Frequency(
            (4, JsonScalar),
            (2, JsonArray(depth - 1)),
            (2, JsonObject(depth - 1)));

    private static Gen<string> JsonArray(int depth) =>
        from items in Gen.Choose(0, 4).SelectMany(count => Concat(JsonWhiteSpace, JsonValue(depth), JsonWhiteSpace).ArrayOf(count))
        from separator in Gen.Frequency((12, Gen.Constant(",")), (1, Gen.Elements(";", ",,", " ", string.Empty)))
        from trailing in Gen.Frequency((12, Gen.Constant(string.Empty)), (1, Gen.Constant(",")))
        from open in Gen.Frequency((20, Gen.Constant("[")), (1, Gen.Elements("{", "(")))
        from close in Gen.Frequency((20, Gen.Constant("]")), (1, Gen.Elements("}", ")", string.Empty)))
        select open + string.Join(separator, items) + (items.Length > 0 ? trailing : string.Empty) + close;

    private static Gen<string> JsonObject(int depth) =>
        from members in Gen.Choose(0, 4).SelectMany(count => JsonMember(depth).ArrayOf(count))
        from separator in Gen.Frequency((12, Gen.Constant(",")), (1, Gen.Elements(";", ",,", string.Empty)))
        from trailing in Gen.Frequency((12, Gen.Constant(string.Empty)), (1, Gen.Constant(",")))
        from close in Gen.Frequency((20, Gen.Constant("}")), (1, Gen.Elements("]", string.Empty)))
        select "{" + string.Join(separator, members) + (members.Length > 0 ? trailing : string.Empty) + close;

    private static Gen<string> JsonMember(int depth) =>
        from leading in JsonWhiteSpace
        from key in Gen.Frequency((12, JsonString), (1, Gen.Elements("a", "'a'", "1", "true", "null", "\"a")))
        from beforeColon in JsonWhiteSpace
        from colon in Gen.Frequency((20, Gen.Constant(":")), (1, Gen.Elements("=", "::", string.Empty)))
        from afterColon in JsonWhiteSpace
        from value in JsonValue(depth)
        from trailing in JsonWhiteSpace
        select leading + key + beforeColon + colon + afterColon + value + trailing;

    /// <summary>Nesting deeper than 64 levels (where the JSON checkers change their storage), with broken closers sometimes.</summary>
    private static readonly Gen<string> DeepJson =
        from depth in Gen.Frequency((3, Gen.Choose(55, 75)), (2, Gen.Choose(0, 200)), (1, Gen.Choose(120, 140)))
        from objects in Gen.Elements(true, false).ArrayOf(depth)
        from inner in JsonScalar
        from broken in Gen.Frequency((4, Gen.Constant(-1)), (1, Gen.Choose(0, Math.Max(0, depth - 1))))
        select BuildDeep(objects, inner, broken);

    private static string BuildDeep(bool[] objects, string inner, int broken)
    {
        var open = string.Concat(objects.Select(isObject => isObject ? "{\"k\":" : "["));
        var close = string.Concat(Enumerable.Reverse(objects).Select((isObject, index) =>
            index == broken ? (isObject ? "]" : "}") : (isObject ? "}" : "]")));
        return open + inner + close;
    }

    private static readonly Gen<string> ValidishJson = Gen.Frequency(
        (10, Concat(JsonWhiteSpace, JsonValue(3), JsonWhiteSpace)),
        (2, DeepJson),
        (1, Concat(Gen.Constant("\uFEFF"), JsonValue(2))),
        (1, Concat(JsonValue(1), JsonWhiteSpace, JsonValue(1))),
        (1, Concat(JsonValue(2), Gen.Elements("//c", "/*c*/", "#"))));

    /// <summary>JSON-like text: nested values, escapes, number edge cases, trailing commas, bad white space, deep nesting, and edits of them.</summary>
    public static readonly Gen<string> JsonLike = Mutated(ValidishJson, "{}[],:\"\\0123456789.-+eEtfn ");

    // ---------------------------------------------------------------- XML

    private static readonly Gen<string> XmlName = Gen.Frequency(
        (60, Concat(Chars("abcxyzABC_"), Join(Chars("abcxyz0129._-"), 0, 6))),
        (2, Gen.Elements("p:a", "x:b1", "xml:lang", "xmlns:p", "\u00E9l\u00E9ment", "\u4F8B")),
        (1, Gen.Elements("1a", "-a", ".a", "a b", "", "a:b:c", ":a", "a:", "\U0001F600", "a\0", "xmlns")));

    private static readonly Gen<string> XmlReference = Gen.Frequency(
        (6, Gen.Elements("&amp;", "&lt;", "&gt;", "&quot;", "&apos;", "&#65;", "&#x41;", "&#x1F600;", "&#xE9;")),
        (1, Gen.Elements("&#0;", "&#x0;", "&#xD800;", "&#xFFFE;", "&#1114112;", "&foo;", "&", "&#;", "&#x;", "&amp", "&#65", "&lt")));

    private static readonly Gen<string> XmlText = XmlTextOf("abcXYZ019 .,;:!?()[]{}'\"=/-_\n\t");

    // Attribute values: no quotes, so the value does not end the attribute.
    private static readonly Gen<string> XmlAttributeText = XmlTextOf("abcXYZ019 .,;:!?()[]{}=/-_\n\t");

    private static Gen<string> XmlTextOf(string alphabet) => Join(
        Gen.Frequency(
            (60, Chars(alphabet)),
            (8, XmlReference),
            (6, Gen.Elements("\u00E9", "\u4F8B", "\U0001F600", "\u0085", "\u2028")),
            (1, Gen.Elements("<", ">", "]]>", "\0", "\u0001", "\u001F", "\uFFFE", "\uFFFF", "\uD800", "\uDC00", "\r", "\r\n"))),
        0,
        10);

    private static readonly Gen<string> XmlAttribute =
        from name in XmlName
        from quote in Gen.Frequency((60, Gen.Elements("\"", "'")), (1, Gen.Elements(string.Empty, "`")))
        from value in XmlAttributeText
        from assign in Gen.Frequency((20, Gen.Constant("=")), (1, Gen.Elements(" = ", string.Empty, "==")))
        select " " + name + assign + quote + value + quote;

    // Comment text has no '-', so only the broken comments below break the "--" rule.
    private static readonly Gen<string> XmlMisc = Gen.Frequency(
        (6, Gen.Elements(" ", "\n", "\r\n", "\t")),
        (5, XmlTextOf("abcXYZ019 .,;:!?()[]{}'\"=/_\n\t").Select(text => "<!--" + text + "-->")),
        (1, Gen.Elements("<!-- a -- b -->", "<!--->", "<!-- a --->", "<!--", "<!---->")),
        (4, XmlText.Select(text => "<?pi " + text.Replace("?>", string.Empty) + "?>")),
        (1, Gen.Elements("<?xml version=\"1.0\"?>", "<??>", "<?xml-stylesheet href=\"a\"?>", "<?XML x?>")));

    private static Gen<string> XmlContent(int depth)
    {
        var pieces = new List<(int, Gen<string>)>
        {
            (15, XmlText),
            (6, XmlText.Select(text => "<![CDATA[" + text + "]]>")),
            (6, XmlMisc),
            (1, Gen.Elements("<![CDATA[", "]]>", "<![cdata[a]]>", "</>", "<>", "< a/>", "&#x10FFFF;")),
        };
        if (depth > 0)
        {
            pieces.Add((12, XmlElement(depth - 1)));
        }

        return Join(Gen.Frequency(pieces.ToArray()), 0, 4);
    }

    private static Gen<string> XmlElement(int depth) =>
        from name in XmlName
        from attributes in Join(Gen.Frequency((16, XmlAttribute), (1, Gen.Elements(" xmlns:p=\"urn:p\"", " xmlns=\"\"", " xmlns:p=\"\"", " a=\"1\" a=\"2\"", " xml:space=\"preserve\""))), 0, 3)
        from selfClosing in Gen.Elements(false, false, true)
        from content in XmlContent(depth)
        from closeName in Gen.Frequency((30, Gen.Constant(name)), (1, XmlName))
        from space in Gen.Elements(string.Empty, string.Empty, " ")
        select selfClosing
            ? "<" + name + attributes + space + "/>"
            : "<" + name + attributes + space + ">" + content + "</" + closeName + space + ">";

    private static readonly Gen<string> XmlDoctype = Gen.Elements(
        "<!DOCTYPE a>",
        "<!DOCTYPE a [<!ENTITY e \"x\">]>",
        "<!DOCTYPE a SYSTEM \"file:///etc/passwd\">",
        "<!DOCTYPE a [<!ENTITY x SYSTEM \"http://example.com/x\">]>",
        "<!DOCTYPE a [<!ENTITY a \"aaaa\"><!ENTITY b \"&a;&a;&a;&a;\"><!ENTITY c \"&b;&b;&b;&b;\">]>",
        "<!DOCTYPE a PUBLIC \"-//A//B\" \"a.dtd\">",
        "<!doctype a>",
        "<!DOCTYPE>");

    private static readonly Gen<string> ValidishXml = Gen.Frequency(
        (10, Concat(
            Optional(Gen.Frequency(
                (8, Gen.Elements("<?xml version=\"1.0\"?>", "<?xml version=\"1.0\" encoding=\"utf-8\"?>", "<?xml version='1.0' standalone='yes'?>")),
                (1, Gen.Elements("<?xml version=\"2.0\"?>", "<?xml?>", "<?xml encoding=\"utf-8\"?>")))),
            Join(XmlMisc, 0, 2),
            XmlElement(3),
            Join(XmlMisc, 0, 2))),
        (1, Concat(Optional(Gen.Constant("<?xml version=\"1.0\"?>")), XmlDoctype, XmlElement(1), Optional(Gen.Elements("&b;", "<c>&c;</c>")))),
        (1, Concat(XmlElement(1), XmlElement(1))),
        (1, Concat(Gen.Elements("\uFEFF", " ", "a", "<?xml version=\"1.0\"?>", " <?xml version=\"1.0\"?>"), XmlElement(2))),
        (1, Concat(XmlElement(1), Gen.Elements("a", "<?xml version=\"1.0\"?>", "]]>", "&amp;"))));

    /// <summary>XML-like text: elements, attributes, entities, CDATA, comments, processing instructions, DOCTYPEs (with entities), namespaces, and edits of them.</summary>
    public static readonly Gen<string> XmlLike = Mutated(ValidishXml, "<>/=\"'&;![]-?:");

    // ---------------------------------------------------------------- Message templates

    /// <summary>The placeholder names DotThrow knows.</summary>
    public static readonly string[] KnownPlaceholders =
    [
        "PropertyName", "PropertyValue", "ComparisonValue", "From", "To", "MinLength", "MaxLength", "TotalLength", "Length",
        "ExpectedPrecision", "ExpectedScale", "Digits", "ActualScale", "Count", "MinCount", "MaxCount", "Type", "Pattern", "Value",
        "Condition", "Divisor", "MaxAge", "MinAge", "Key", "Flag", "Schemes", "Characters",
    ];

    /// <summary>Names of placeholders a test gives values to, besides the known ones.</summary>
    public static readonly string[] CustomPlaceholders = ["A", "b_1", "_x", "\u00DCn\u00EFcode", "Limit", "x2", "\u0661a"];

    public static readonly Gen<string> PlaceholderName = Gen.Frequency(
        (6, Gen.Elements(KnownPlaceholders)),
        (4, Gen.Elements(CustomPlaceholders)),
        (2, Gen.Elements("", "1a", "a b", "a-b", "{", "a}", " ", "a.b", "PropertyName ", "propertyname", "\U0001D400", "\u0301a")));

    public static readonly Gen<string> PlaceholderFormat = Gen.Frequency(
        (5, Gen.Elements(
            "N2", "N0", "D5", "X", "x8", "F3", "E2", "G", "R", "P1", "C", "0.00", "#,##0.0", "00000", "0;(0);zero", "yyyy-MM-dd", "HH:mm",
            "O", "o", "s", "u", "c", "g", "dd/MM", "%d", "000-00", "D", "B", "P", "N", "'x'0", "\\#0", "G17", "E+0", "hh\\:mm", "d")),
        (3, Gen.Elements("Q", "%", "\\", "'", "\"", "yyyy-MM-dd'", ":", "D-1", "D1000000000", "\0", "fffffffff", " ", "{", "Z9", "%%")),
        (1, TextOf(AnyUnit, 8).Select(text => text.Replace("}", string.Empty))));

    private static readonly Gen<string> Placeholder =
        from name in PlaceholderName
        from format in Gen.Frequency((3, Gen.Constant<string?>(null)), (2, PlaceholderFormat.Select(f => (string?)f)))
        select "{" + name + (format is null ? string.Empty : ":" + format) + "}";

    /// <summary>Message templates: text with placeholders (known, custom and invalid names, with valid and invalid formats) and stray braces.</summary>
    public static readonly Gen<string> Template = Join(
        Gen.Frequency(
            (6, TextOf(Gen.Frequency((20, Chars("abc XYZ019.,'-")), (2, AnyUnit)), 12)),
            (6, Placeholder),
            (2, Gen.Elements("{", "}", "{{", "}}", "{}", "{:}", "{a{b}", "{a:{b}}", "{{PropertyName}}", "{PropertyName", "PropertyName}", "{ PropertyName }"))),
        0,
        8);

    /// <summary>Values for placeholders: strings, numbers, dates, GUIDs, enums, booleans and null.</summary>
    public static readonly Gen<object?> PlaceholderValue = Gen.Frequency(
        (4, AnyTextGen.Select(text => (object?)text)),
        (3, Gen.Choose(-1000, 1000).Select(n => (object?)n)),
        (1, Gen.Elements<object?>(int.MinValue, int.MaxValue, long.MaxValue, 0L, (byte)7, (short)-3, 3000000000u)),
        (2, Gen.Elements<object?>(0.5, -1.25, double.NaN, double.PositiveInfinity, double.Epsilon, 1e300, 0.1f, float.MaxValue, 1.5m, decimal.MaxValue, -0.0001m)),
        (2, Gen.Elements<object?>(
            new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 5, 2, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 2, 14, 30, 0, 500, DateTimeKind.Local),
            new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(637_000_000_000_000_123, DateTimeKind.Utc),
            DateTime.MinValue,
            DateTime.MaxValue,
            new DateTimeOffset(2026, 5, 2, 14, 30, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 5, 2, 14, 30, 0, TimeSpan.FromMinutes(-330)),
            DateTimeOffset.MinValue,
            TimeSpan.FromMinutes(90),
            TimeSpan.MinValue)),
        (1, Gen.Elements<object?>(Guid.Empty, new Guid("6f9619ff-8b86-d011-b42d-00cf4fc964ff"), true, false, DayOfWeek.Friday, (DayOfWeek)42, StringComparison.Ordinal)),
        (1, Gen.Constant<object?>(null)));

    // ---------------------------------------------------------------- Regular expressions

    /// <summary>Patterns that run in about linear time, with backreferences, lookarounds, Unicode classes and surrogates.</summary>
    public static readonly string[] Patterns =
    [
        "^[a-z]+$", "a|b", "^\\d{3}-\\d{4}$", "[\\p{L}]+", "(?i)^abc", ".*", "^$", "\\s", "(?<x>a)\\k<x>", "^.{0,5}$",
        "[^\\u0000-\\u007F]", "\\bword\\b", "(?=a)a", "(?<!b)a", "^\\uD83D", "[\\uD800-\\uDFFF]", "\\p{Cs}", "^[\\w.+-]+@[\\w-]+\\.[\\w.]+$",
    ];

    /// <summary>
    /// Patterns that backtrack for an exponential time on a long run of matching characters with
    /// a bad character at the end (ReDoS).
    /// </summary>
    public static readonly string[] SlowPatterns = ["^(a+)+$", "^(a|aa)+$", "^(\\w+\\s?)+$", "^(([a-z])+.)+[A-Z]([a-z])+$"];

    /// <summary>A long run of 'a' (a match for every slow pattern so far) and an end that may break the match.</summary>
    public static readonly Gen<string> SlowInput =
        from length in Gen.Choose(24, 40)
        from end in Gen.Elements("!", "\0", "a", " ", "\u00E9", "\uD800", "A", "Ab")
        select new string('a', length) + end;

    // ---------------------------------------------------------------- Dynamic rule definitions

    /// <summary>Property paths of the dynamic test model (<c>Sample</c>), with item paths, and paths that do not exist.</summary>
    private static readonly Gen<string> PropertyPath = Gen.Frequency(
        (12, Gen.Elements(
            "Text", "Number", "MaybeNumber", "Big", "Short", "Price", "Ratio", "Single", "Flag", "MaybeFlag", "When", "At", "Day", "Time",
            "Duration", "Id", "MaybeId", "Status", "MaybeStatus", "Access", "Link", "Ip", "Version", "Letter", "Tags", "Scores", "Counts",
            "Cost", "Kind", "Body", "Address", "Address.City", "Address.Zip", "Address.Floor", "Address.Point.Latitude", "Lines",
            "Lines[].Sku", "Lines[].Quantity", "Lines[].Status", "Lines[].Notes", "Lines[].Notes[]", "Tags[]", "Scores[]", "")),
        (2, Gen.Elements(
            "text", "ADDRESS.CITY", " Text ", "Txet", "Address..City", "Lines[]", "[]", "Lines[].", "Lines[0].Sku", "Text.Length",
            "Counts[a]", "Lines[]Sku", "Tags[][]", ".Text", "Address.", "Lines[].Nope", "Nope[].Sku")),
        (1, TextOf(AnyUnit, 12)));

    /// <summary>Check names: every built-in name, the ones that need code, and misspelled or random names.</summary>
    private static readonly Gen<string> CheckName = Gen.Frequency(
        (12, Gen.Elements(CheckNames.All.ToArray())),
        (1, Gen.Elements("Condition", "Must", "Any", "None", "Invalid", "longerthan", " LongerThan ", "LongerThn", "NotEmail", "")),
        (1, Gen.Elements(CheckNames.All.ToArray()).SelectMany(name => Mutated(Gen.Constant(name), "aeiouns")).Select(name => name)),
        (1, TextOf(AnyUnit, 10)));

    /// <summary>Argument text: numbers, dates, times, GUIDs, enum and type names, patterns, URIs, addresses, cultures and noise.</summary>
    private static readonly Gen<string> ArgumentText = Gen.Frequency(
        (6, Gen.Elements(
            "0", "1", "5", "-1", "10", "130", "1.5", "-3.25", "1e3", "2147483647", "2147483648", "-2147483649", "18446744073709551616", "NaN",
            "Infinity", "-0", "0x10", "1,000", " 7 ", "\u0661", "79228162514264337593543950336", "1e400", "0.1")),
        (3, Gen.Elements(
            "2026-05-02", "2026-05-02T10:30:00Z", "2026-05-02T10:30:00+05:30", "10:30", "1.02:03:04", "00:00:00.5", "9999-12-31",
            "0001-01-01", "2026-02-30", "24:00", "-1.00:00:00", "10675199.02:48:05.4775808", "P1D")),
        (3, Gen.Elements(
            "Active", "active", "Read, Write", "Read,Write", "Admin", "42", "-1", "Draft, Closed", "Nope", "System.Int32", "int", "Status",
            "DotThrow.Tests.Dynamic.Status", "System.IO.Stream", "Stream", "IDisposable", "System.String, mscorlib", "List`1")),
        (3, Gen.Elements(
            "^[a-z]+$", "(", "[a-", "(?<x>", "a{2,1}", "\\", "(?i)ada", "^(a|aa)+$", ".*", "\\p{Lu}", "(?<=a)b", "\\k<x>", "[\\uD800]")),
        (3, Gen.Elements(
            "https://example.com", "/relative", "http://[::1]", "not a uri", "203.0.113.5", "::1", "999.1.1.1", "1.2", "1.2.3.4.5",
            "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "{6f9619ff-8b86-d011-b42d-00cf4fc964ff}", "x", "ab", "\u00E9", "true", "false", "True")),
        (2, Gen.Elements("de-DE", "en-US", "xx", "", " ", "x-private", "en-US-x-twain", "zh-Hant-TW", "\u0000", "iv", "tlh")),
        (2, UriLike),
        (3, TextOf(AnyUnit, 16)));

    private static readonly Gen<string> RegexOptionText = Gen.Elements(
        "IgnoreCase", "IgnoreCase, Multiline", "ignorecase,  singleline", "NonBacktracking", "NonBacktracking, RightToLeft", "ECMAScript, Singleline",
        "ECMAScript, IgnoreCase", "Compiled", "None", "123", "-1", "+1", "IgnoreCase|Multiline", "Absolute", "Relative", "RelativeOrAbsolute",
        "IgnoreTrailingZeros", "de-DE", "", "NotAnOption");

    /// <summary>A JSON string with the text, escaped as JSON needs (control characters as \u escapes).</summary>
    public static string Quote(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length + 2).Append('"');
        foreach (var c in text)
        {
            if (c is '"' or '\\')
            {
                builder.Append('\\').Append(c);
            }
            else if (c < ' ')
            {
                builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.Append('"').ToString();
    }

    // A JSON value for an argument: text, a number as written, or true, false and null.
    private static readonly Gen<string> ArgumentJson = Gen.Frequency(
        (8, ArgumentText.Select(Quote)),
        (3, Gen.Elements("0", "5", "-1", "1.5", "1e3", "2147483648", "-0", "130")),
        (1, Gen.Elements("true", "false", "null")));

    // A value of the wrong kind for the key, now and then.
    private static Gen<string> OrWrongKind(Gen<string> value) => Gen.Frequency(
        (14, value),
        (1, Gen.Elements("{}", "[]", "[1]", "{\"a\":1}", "1", "true", "null", "\"\"")));

    // An object with some of the members, keys sometimes in another case, misspelled, repeated or unknown.
    private static Gen<string> JsonObjectOf(params (string Key, Gen<string> Value)[] members)
    {
        var parts = members.Select(member =>
            Gen.Frequency(
                (2, Gen.Constant(string.Empty)),
                (5, from key in Gen.Frequency(
                        (16, Gen.Constant(member.Key)),
                        (1, Gen.Constant(member.Key.ToUpperInvariant())),
                        (1, Gen.Elements(member.Key + "s", member.Key.Substring(1), "extra", member.Key + " ")))
                    from value in OrWrongKind(member.Value)
                    from twice in Gen.Frequency((30, Gen.Constant(false)), (1, Gen.Constant(true)))
                    select (twice ? Quote(key) + ": " + value + ", " : string.Empty) + Quote(key) + ": " + value)))
            .ToArray();
        return Gen.CollectToArray(parts).Select(values => "{ " + string.Join(", ", values.Where(part => part.Length > 0)) + " }");
    }

    private static Gen<string> JsonArrayOf(Gen<string> item, int max, int min = 0) =>
        Gen.Choose(min, max).SelectMany(count => item.ArrayOf(count)).Select(items => "[ " + string.Join(", ", items) + " ]");

    private static readonly Gen<string> CheckJson = Gen.Frequency(
        (2, CheckName.Select(Quote)),
        (6, JsonObjectOf(
            ("check", CheckName.Select(Quote)),
            ("value", ArgumentJson),
            ("min", ArgumentJson),
            ("max", ArgumentJson),
            ("values", JsonArrayOf(ArgumentJson, 4)),
            ("comparison", Gen.Elements("Ordinal", "OrdinalIgnoreCase", "InvariantCulture", "CurrentCultureIgnoreCase", "ordinal", "Bogus", "1", "", "Ordinal, OrdinalIgnoreCase").Select(Quote)),
            ("options", RegexOptionText.Select(Quote)),
            ("message", Template.Select(Quote)),
            ("errorCode", Gen.Elements("E1", "", " ", "Custom.Code").Select(Quote)))));

    private static readonly Gen<string> ConditionJson = JsonObjectOf(
        ("property", PropertyPath.Select(Quote)),
        ("equalTo", ArgumentJson),
        ("notEqualTo", ArgumentJson),
        ("isNull", Gen.Elements("true", "false", "\"true\"", "\"False\"", "\"yes\"", "null", "1")));

    private static readonly Gen<string> RuleJson = JsonObjectOf(
        ("property", PropertyPath.Select(Quote)),
        ("each", Gen.Elements("true", "false", "\"true\"", "\"no\"", "null")),
        ("allowNull", Gen.Elements("true", "false", "\"TRUE\"", "0")),
        ("group", Gen.Elements("a", "b", "", " ").Select(Quote)),
        ("groups", JsonArrayOf(Gen.Elements("a", "b", "", "default").Select(Quote), 3)),
        ("when", ConditionJson),
        ("unless", ConditionJson),
        ("severity", Gen.Elements("Error", "Warning", "Info", "warning", "Fatal", "1", "").Select(Quote)),
        ("errorCode", Gen.Elements("E1", "", " ", "Rule.Code").Select(Quote)),
        ("message", Template.Select(Quote)),
        ("name", Gen.Elements("Display", "", " ", "\u00C9l\u00E8ve").Select(Quote)),
        ("cascade", Gen.Elements("Stop", "Continue", "stop", "Break", "0", "").Select(Quote)),
        ("checks", JsonArrayOf(CheckJson, 4)));

    private static readonly Gen<string> RuleSetJson = JsonObjectOf(
        ("rules", JsonArrayOf(RuleJson, 4)),
        ("ruleCascade", Gen.Elements("Stop", "Continue", "Never", "").Select(Quote)),
        ("classCascade", Gen.Elements("Stop", "Continue", "continue", "1").Select(Quote)));

    // Checks that suit a property type, with arguments that are mostly right, so many definitions
    // build and their validators run.
    private static Gen<string> Check(string name, params (string Key, Gen<string> Value)[] arguments) =>
        Gen.CollectToArray(arguments.Select(argument => argument.Value.Select(value => ", " + Quote(argument.Key) + ": " + value)))
            .SelectMany(parts => Optional(Template.Select(message => ", \"message\": " + Quote(message)), weight: 1)
                .Select(message => "{ \"check\": " + Quote(name) + string.Concat(parts) + message + " }"));

    private static Gen<string> Simple(params string[] names) => Gen.Elements(names).Select(Quote);

    private static readonly Gen<string> SmallNumber = Gen.Frequency((8, Gen.Choose(0, 20).Select(n => n.ToString(CultureInfo.InvariantCulture))), (1, ArgumentJson));

    private static readonly Gen<string> AnyNumber = Gen.Frequency((8, Gen.Choose(-100, 100).Select(n => n.ToString(CultureInfo.InvariantCulture))), (1, ArgumentJson));

    private static readonly Gen<string> TextArgument = Gen.Frequency((4, Gen.Elements("a", "Ada", "@", "\u00E9", " ", "x-y").Select(Quote)), (1, ArgumentJson));

    private static readonly Gen<string> Comparison = Gen.Elements("Ordinal", "OrdinalIgnoreCase", "InvariantCulture", "CurrentCultureIgnoreCase").Select(Quote);

    private static readonly Gen<string> StringCheck = Gen.Frequency(
        (6, Simple(
            "NullOrEmpty", "WhiteSpace", "NullOrWhiteSpace", "NotEmailAddress", "NotJson", "NotXml", "NotHttpUrl", "AllUpperCase", "AllLowerCase",
            "NotTrimmed", "ContainsWhiteSpace", "ContainsControlCharacters", "NotAscii", "NotDigits", "NotLettersOrDigits", "NotHex", "NotBase64",
            "NotGuid", "NotIPAddress", "NotSlug", "NotVersion", "NotCreditCardNumber", "NotFileName", "Null", "NotNull", "Empty", "NotEmpty")),
        (1, Check("LongerThan", ("value", SmallNumber))),
        (1, Check("ShorterThan", ("value", SmallNumber))),
        (1, Check("LengthNotEqualTo", ("value", SmallNumber))),
        (1, Check("LengthOutOfRange", ("min", SmallNumber), ("max", SmallNumber))),
        (2, Gen.Elements("Contains", "NotContains", "StartsWith", "NotStartsWith", "EndsWith", "NotEndsWith", "EqualTo", "NotEqualTo", "GreaterThan", "LessThan")
            .SelectMany(name => Gen.Frequency((2, Check(name, ("value", TextArgument))), (1, Check(name, ("value", TextArgument), ("comparison", Comparison)))))),
        (1, Gen.Elements("OneOf", "NotOneOf").SelectMany(name => Check(name, ("values", JsonArrayOf(TextArgument, 3))))),
        (1, Gen.Elements("Matches", "NotMatches").SelectMany(name => Check(name, ("value", Gen.Elements(Patterns.Concat(SlowPatterns).ToArray()).Select(Quote)), ("options", Gen.Elements("None", "IgnoreCase", "IgnoreCase, CultureInvariant", "NonBacktracking", "Multiline").Select(Quote))))),
        (1, Check("NotUri", ("options", Gen.Elements("Absolute", "Relative", "RelativeOrAbsolute").Select(Quote)))),
        (1, Gen.Elements("ContainsAny", "ContainsAnyExcept").SelectMany(name => Check(name, ("value", Gen.Elements("<>", "abc", "\u00E9\uD800", "0123456789", "").Select(Quote))))),
        (1, Check("NotEnumName", ("value", Gen.Elements("Status", "DotThrow.Tests.Dynamic.Access").Select(Quote)), ("options", Gen.Elements("IgnoreCase").Select(Quote)))),
        (1, Check("NotParsable", ("value", Gen.Elements("int", "Guid", "decimal", "DateTime", "TimeSpan", "Half", "BigInteger", "DateOnly").Select(Quote)), ("options", Gen.Elements("de-DE", "en-US", "ar-SA", "ja-JP").Select(Quote)))));

    private static readonly Gen<string> NumberCheck = Gen.Frequency(
        (4, Simple("Negative", "NegativeOrZero", "Positive", "PositiveOrZero", "Zero", "Odd", "Even", "NotPowerOfTwo", "Default", "Null")),
        (3, Gen.Elements("GreaterThan", "GreaterThanOrEqualTo", "LessThan", "LessThanOrEqualTo", "EqualTo", "NotEqualTo", "NotMultipleOf").SelectMany(name => Check(name, ("value", AnyNumber)))),
        (2, Gen.Elements("OutOfRange", "OutOfRangeExclusive", "InRange").SelectMany(name => Check(name, ("min", AnyNumber), ("max", AnyNumber)))),
        (1, Gen.Elements("OneOf", "NotOneOf").SelectMany(name => Check(name, ("values", JsonArrayOf(AnyNumber, 3))))));

    private static readonly Gen<string> DecimalCheck = Gen.Frequency(
        (3, NumberCheck),
        (1, Simple("NotInteger", "NaN")),
        (1, Check("PrecisionScaleExceeded", ("values", Gen.Zip(Gen.Choose(1, 10), Gen.Choose(0, 6)).Select(pair => "[" + pair.Item1.ToString(CultureInfo.InvariantCulture) + ", " + pair.Item2.ToString(CultureInfo.InvariantCulture) + "]")), ("options", Gen.Elements("IgnoreTrailingZeros").Select(Quote)))));

    private static readonly Gen<string> DoubleCheck = Gen.Frequency((3, NumberCheck), (2, Simple("NaN", "Infinity", "NotFinite", "NotInteger")));

    private static readonly Gen<string> DateCheck = Gen.Frequency(
        (3, Simple("InPast", "InFuture", "NotUtc", "Weekend", "Weekday", "HasTimeOfDay", "Default")),
        (2, Gen.Elements("Before", "After", "GreaterThan", "LessThanOrEqualTo", "EqualTo").SelectMany(name => Check(name, ("value", Gen.Elements("2026-05-02", "2026-05-02T10:30:00Z", "0001-01-01", "9999-12-31T23:59:59").Select(Quote))))),
        (1, Gen.Elements("OlderThan", "NewerThan").SelectMany(name => Check(name, ("value", Gen.Elements("1.00:00:00", "00:00:01", "10675199.02:48:05.4775807", "-1.00:00:00").Select(Quote))))),
        (1, Check("OutOfRange", ("min", Gen.Constant(Quote("2020-01-01"))), ("max", Gen.Constant(Quote("2030-01-01"))))));

    private static readonly Gen<string> UriCheck = Gen.Frequency(
        (4, Simple("Relative", "NotHttps", "NotHttpOrHttps", "Loopback", "Null", "NotNull")),
        (1, Check("NotScheme", ("values", JsonArrayOf(Gen.Elements("https", "WSS", "mailto", "").Select(Quote), 3)))),
        (1, Gen.Elements("EqualTo", "NotEqualTo").SelectMany(name => Check(name, ("value", Gen.Elements("https://example.com/a", "/a", "mailto:ada@example.com").Select(Quote))))));

    private static readonly Gen<string> CollectionCheck = Gen.Frequency(
        (3, Simple("NullOrEmpty", "ContainsNull", "HasDuplicates", "Empty", "NotEmpty")),
        (2, Gen.Elements("CountEqualTo", "CountNotEqualTo", "CountGreaterThan", "CountLessThan").SelectMany(name => Check(name, ("value", SmallNumber)))),
        (1, Check("CountOutOfRange", ("min", SmallNumber), ("max", SmallNumber))),
        (1, Gen.Elements("Contains", "NotContains").SelectMany(name => Check(name, ("value", TextArgument)))));

    // A property and checks that suit its type.
    private static readonly Gen<(string Property, Gen<string> Checks)> TypedProperty = Gen.Elements<(string, Gen<string>)>(
        ("Text", StringCheck),
        ("Address.City", StringCheck),
        ("Lines[].Sku", StringCheck),
        ("Tags[]", StringCheck),
        ("Lines[].Notes[]", StringCheck),
        ("Number", NumberCheck),
        ("MaybeNumber", NumberCheck),
        ("Lines[].Quantity", NumberCheck),
        ("Address.Floor", NumberCheck),
        ("Scores[]", NumberCheck),
        ("Short", NumberCheck),
        ("Big", NumberCheck),
        ("Price", DecimalCheck),
        ("Ratio", DoubleCheck),
        ("Single", DoubleCheck),
        ("When", DateCheck),
        ("At", DateCheck),
        ("Link", UriCheck),
        ("Ip", Simple("Loopback", "Private", "NotIPv4", "NotIPv6", "Null")),
        ("Tags", CollectionCheck),
        ("Scores", CollectionCheck),
        ("Lines", CollectionCheck),
        ("Counts", Gen.Elements("ContainsKey", "NotContainsKey").SelectMany(name => Check(name, ("value", Gen.Elements("a", "b", "").Select(Quote))))),
        ("Status", Gen.Frequency((2, Simple("NotDefined", "Default")), (1, Check("EqualTo", ("value", Gen.Elements("Active", "closed", "42").Select(Quote)))))),
        ("MaybeStatus", Simple("NotDefined", "Null", "NotNull")),
        ("Access", Gen.Elements("HasFlag", "NotHasFlag").SelectMany(name => Check(name, ("value", Gen.Elements("Read", "Read, Write", "Admin").Select(Quote))))),
        ("Flag", Simple("True", "False")),
        ("Kind", Gen.Frequency((2, Simple("Abstract", "Interface", "Null")), (1, Check("NotAssignableTo", ("value", Gen.Elements("System.IO.Stream", "IDisposable", "object").Select(Quote)))))),
        ("Version", Check("GreaterThan", ("value", Gen.Elements("1.0", "2.0.1").Select(Quote)))),
        ("Letter", Check("OutOfRange", ("min", Gen.Constant(Quote("a"))), ("max", Gen.Constant(Quote("z"))))),
        ("Duration", Check("LessThan", ("value", Gen.Elements("00:10:00", "1.00:00:00").Select(Quote)))),
        ("Id", Simple("Default", "Empty")));

    private static readonly Gen<string> TypedRuleJson =
        from property in TypedProperty
        from checks in JsonArrayOf(property.Checks, 3, min: 1)
        from options in Optional(Gen.Elements(
            ", \"allowNull\": true", ", \"severity\": \"Warning\"", ", \"cascade\": \"Continue\"", ", \"group\": \"a\"",
            ", \"when\": { \"property\": \"Flag\", \"equalTo\": \"false\" }", ", \"unless\": { \"property\": \"Address\", \"isNull\": true }",
            ", \"errorCode\": \"E1\"", ", \"name\": \"Shown\""))
        from message in Optional(Template.Select(text => ", \"message\": " + Quote(text)))
        select "{ \"property\": " + Quote(property.Property) + options + message + ", \"checks\": " + checks + " }";

    private static readonly Gen<string> TypedRuleSetJson =
        from rules in JsonArrayOf(TypedRuleJson, 4, min: 1)
        from cascade in Optional(Gen.Elements(", \"ruleCascade\": \"Continue\"", ", \"classCascade\": \"Stop\""))
        select "{ \"rules\": " + rules + cascade + " }";

    /// <summary>
    /// Rule definitions as JSON: definitions whose checks suit the property types (most of them
    /// build), definitions of random parts (known and unknown properties, checks and arguments,
    /// keys in any case, values of the wrong kind), and edits of the text.
    /// </summary>
    public static readonly Gen<string> RuleDefinitionJson = Gen.Frequency(
        (5, TypedRuleSetJson),
        (4, RuleSetJson),
        (3, Mutated(Gen.Frequency((1, RuleSetJson), (1, TypedRuleSetJson)), "{}[],:\"\\ ")));
}
