using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using DotThrow.Dynamic;
using DotThrow.Dynamic.Internal;
using FsCheck;
using FsCheck.Fluent;
using Xunit;
#if NET
using System.Text.Json;
#endif

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Fuzz tests for the checks that parse a whole document or address: IfNotUri, IfNotHttpUrl,
/// IfNotJson (and the JSON reader of dynamic rules) and IfNotXml (with and without its limit).
/// </summary>
[Trait(Fuzz.Trait, Fuzz.Category)]
public sealed class FormatFuzzTests
{
    private static readonly Arbitrary<string?> UriText = Arb.From(
        Gen.Frequency((4, Generators.AsNullable(Generators.UriLike)), (1, Generators.AnyTextGen)),
        Generators.Shrink);

    private static readonly Arbitrary<string?> JsonText = Arb.From(
        Gen.Frequency((4, Generators.AsNullable(Generators.JsonLike)), (1, Generators.AnyTextGen)),
        Generators.Shrink);

    private static readonly Arbitrary<string?> XmlText = Arb.From(
        Gen.Frequency((4, Generators.AsNullable(Generators.XmlLike)), (1, Generators.AnyTextGen)),
        Generators.Shrink);

    // The text as written must be a URI (docs/guards.md): no white space around it, no control or
    // format characters, only RFC 3986 characters and non-ASCII letters as in IRIs, '%' only in
    // valid escapes, at most one '#', no backslash and no Windows drive path. A passing text is a
    // URI of that kind for System.Uri too, and a file URI only when it says "file:".
    [Fact]
    public void NotUri_and_NotHttpUrl_follow_the_documented_rules() =>
        Fuzz.Run(UriText, text =>
        {
            var absolute = !Contract.Fails(text, g => g.IfNotUri(), ErrorCodes.NotUri);
            var relative = !Contract.Fails(text, g => g.IfNotUri(UriKind.Relative), ErrorCodes.NotUri);
            var any = !Contract.Fails(text, g => g.IfNotUri(UriKind.RelativeOrAbsolute), ErrorCodes.NotUri);
            var http = !Contract.Fails(text, g => g.IfNotHttpUrl(), ErrorCodes.NotHttpUrl);
            if (text is null)
            {
                return;
            }

            if (NotAUriAsWritten(text) is { } reason)
            {
                Assert.False(absolute || relative || any || http, $"A text with {reason} passed: {Contract.Show(text)}.");
            }

            if (absolute)
            {
                Assert.True(System.Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri, Contract.Show(text));
                Assert.True(!uri.IsFile || text.StartsWith("file:", StringComparison.OrdinalIgnoreCase), $"A file path passed as an absolute URI: {Contract.Show(text)}.");
            }

            if (relative)
            {
                Assert.True(System.Uri.TryCreate(text, UriKind.Relative, out _), Contract.Show(text));
            }

#if NET
            // RelativeOrAbsolute accepts what one of the other kinds accepts. This rule is not
            // documented, so it is checked only where it was seen to hold: .NET on Linux and macOS.
            // On Windows, Uri may read "//server/share" as a UNC file path, which IfNotUri rejects
            // for RelativeOrAbsolute but not for Relative; that is not confirmed yet.
            Assert.True(OperatingSystem.IsWindows() || any == (absolute || relative), $"RelativeOrAbsolute {(any ? "passed" : "failed")}, Absolute {(absolute ? "passed" : "failed")}, Relative {(relative ? "passed" : "failed")} for {Contract.Show(text)}.");
#endif
            var scheme = absolute ? new Uri(text, UriKind.Absolute).Scheme : null;
            Assert.True(http == (scheme is "http" or "https"), $"IfNotHttpUrl {(http ? "passed" : "failed")} with the scheme {scheme ?? "(none)"} for {Contract.Show(text)}.");
        });

    // Why the text is not a URI as written, or null.
    private static string? NotAUriAsWritten(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "only white space";
        }

        if (text.Any(c => c == '\\'))
        {
            return "a backslash";
        }

        if (text.Length >= 2 && text[1] == ':' && char.IsLetter(text[0]) && text[0] < 0x80 && (text.Length == 2 || text[2] is '/' or '\\'))
        {
            return "a drive path";
        }

        if (text.Count(c => c == '#') > 1)
        {
            return "two '#'";
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '%' && !(i + 2 < text.Length && Uri.IsHexDigit(text[i + 1]) && Uri.IsHexDigit(text[i + 2])))
            {
                return "a '%' that is not an escape";
            }

            if (c < 0x80 && !char.IsLetterOrDigit(c) && "-._~:/?#[]@!$&'()*+,;=%".IndexOf(c) < 0)
            {
                return "the character U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
            }

            if (c >= 0x80 && (char.IsWhiteSpace(c) || char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format))
            {
                return "the white space, control or format character U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
            }

            // A surrogate pair is one character; a surrogate that is not part of a pair is not a
            // character at all.
            if (char.IsSurrogatePair(text, i))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(text, i) == UnicodeCategory.Format)
                {
                    return "the format character U+" + char.ConvertToUtf32(text, i).ToString("X", CultureInfo.InvariantCulture);
                }

                i++;
            }
            else if (char.IsSurrogate(c))
            {
                return "a lone surrogate U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

#if NET
    // IfNotJson is a strict RFC 8259 checker, like System.Text.Json with its defaults (no comments,
    // no trailing commas). Text with a lone surrogate is left out: System.Text.Json cannot turn it
    // into UTF-8 at all (ArgumentException), while IfNotJson accepts one inside a string. Which
    // answer RFC 8259 wants for such text is an open question.
    [Fact]
    public void NotJson_agrees_with_System_Text_Json() =>
        Fuzz.Run(JsonText, text =>
        {
            var fails = Contract.Fails(text, g => g.IfNotJson(), ErrorCodes.NotJson);
            if (text is null || Contract.HasLoneSurrogate(text))
            {
                return;
            }

            bool parses;
            try
            {
                using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 10_000 });
                parses = true;
            }
            catch (JsonException)
            {
                parses = false;
            }

            Assert.True(parses != fails, $"System.Text.Json {(parses ? "reads" : "rejects")} {Contract.Show(text)}.");
        });
#endif

    // The reader of RuleSetDefinition.FromJson accepts the same JSON as IfNotJson (it also skips a
    // byte order mark and stops at 64 levels), and it throws only RuleDefinitionException.
    [Fact]
    public void The_definition_reader_accepts_the_same_json_as_NotJson() =>
        Fuzz.Run(JsonText, text =>
        {
            if (text is null)
            {
                return;
            }

            string? error = null;
            try
            {
                _ = JsonTreeReader.Parse(text);
            }
            catch (RuleDefinitionException exception)
            {
                error = exception.Message;
                Assert.False(string.IsNullOrEmpty(exception.Message));
            }

            if ((text.Length > 0 && text[0] == '\uFEFF') || error?.IndexOf("nested too deeply", StringComparison.Ordinal) >= 0)
            {
                return;
            }

            var valid = !Contract.Fails(text, g => g.IfNotJson(), ErrorCodes.NotJson);
            Assert.True(valid == (error is null), $"IfNotJson says {(valid ? "valid" : "not valid")}, the reader says {error ?? "valid"}, for {Contract.Show(text)}.");
        });

    // IfNotXml: a well-formed document without a DTD passes. The reference reads the text with
    // DTDs allowed (no resolver, entity expansion capped) and says whether it found a DTD.
    [Fact]
    public void NotXml_passes_well_formed_documents_and_rejects_every_DTD() =>
        Fuzz.Run(XmlText, text =>
        {
            var fails = Contract.Fails(text, g => g.IfNotXml(), ErrorCodes.NotXml);
            if (text is null)
            {
                return;
            }

            var (wellFormed, hasDtd) = ReadXml(text);
            Assert.True(fails == (!wellFormed || hasDtd), $"The reference says {(wellFormed ? "well-formed" : "not well-formed")}{(hasDtd ? " with a DTD" : string.Empty)} for {Contract.Show(text)}.");
        });

    // IfNotXml(limit) is IfNotXml plus "at most limit characters"; 0 means no limit.
    [Fact]
    public void NotXml_with_a_limit_is_NotXml_and_a_length_check() =>
        Fuzz.Run(Arb.From(Gen.Zip(XmlText.Generator, Gen.Frequency((1, Gen.Constant(0)), (4, Gen.Choose(1, 400))))), input =>
        {
            var (text, limit) = input;
            var fails = Contract.Fails(text, g => g.IfNotXml(), ErrorCodes.NotXml);
            var limited = Contract.Fails(text, g => g.IfNotXml(limit), ErrorCodes.NotXml);
            var tooLong = text is not null && limit > 0 && text.Length > limit;
            Assert.True(limited == (fails || tooLong), $"IfNotXml({limit}) {(limited ? "failed" : "passed")}, IfNotXml() {(fails ? "failed" : "passed")} for {Contract.Show(text)}.");
        });

    // Documents written by XmlWriter are well-formed, so they pass, also with a limit of exactly
    // their length; one character less fails.
    [Fact]
    public void NotXml_passes_documents_written_by_XmlWriter() =>
        Fuzz.Run(Arb.From(WrittenXml), text =>
        {
            if (text is null)
            {
                return;
            }

            Assert.False(Contract.Fails(text, g => g.IfNotXml(), ErrorCodes.NotXml), Contract.Show(text));
            Assert.False(Contract.Fails(text, g => g.IfNotXml(text.Length), ErrorCodes.NotXml), Contract.Show(text));
            Assert.True(Contract.Fails(text, g => g.IfNotXml(text.Length - 1), ErrorCodes.NotXml), Contract.Show(text));
        });

    private static (bool WellFormed, bool HasDtd) ReadXml(string text)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = 10_000,
            ConformanceLevel = ConformanceLevel.Document,
        };

        var hasDtd = false;
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), settings);
            while (reader.Read())
            {
                hasDtd |= reader.NodeType == XmlNodeType.DocumentType;
            }

            return (true, hasDtd);
        }
        catch (XmlException)
        {
            return (false, hasDtd);
        }
    }

    // A random element tree, written by XmlWriter. Text the writer refuses (characters XML does
    // not allow) gives null.
    private static readonly Gen<string?> WrittenXml =
        from tree in XmlTree(3)
        from declaration in Gen.Elements(true, false)
        from indent in Gen.Elements(true, false)
        select Write(tree, declaration, indent);

    private static Gen<XmlNode> XmlTree(int depth) =>
        from name in Gen.Elements("a", "b1", "_c", "d.e", "f-g", "\u00E9l\u00E9ment", "\u4F8B", "p:h")
        from count in Gen.Choose(0, 2)
        from attributes in Gen.Zip(Gen.Elements("x", "y", "z_1"), Generators.TextOf(Generators.AnyUnit, 8)).ArrayOf(count)
        from text in Generators.TextOf(Generators.AnyUnit, 12)
        from cdata in Gen.Elements(false, false, true)
        from children in depth > 0 ? Gen.Choose(0, 3).SelectMany(count => XmlTree(depth - 1).ArrayOf(count)) : Gen.Constant(Array.Empty<XmlNode>())
        select new XmlNode(name, attributes.GroupBy(a => a.Item1).Select(g => g.First()).ToArray(), text, cdata, children);

    private static string? Write(XmlNode root, bool declaration, bool indent)
    {
        var builder = new StringBuilder();
        try
        {
            using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = !declaration, Indent = indent }))
            {
                WriteNode(writer, root);
            }

            return builder.ToString();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void WriteNode(XmlWriter writer, XmlNode node)
    {
        if (node.Name.StartsWith("p:", StringComparison.Ordinal))
        {
            writer.WriteStartElement("p", node.Name.Substring(2), "urn:p");
        }
        else
        {
            writer.WriteStartElement(node.Name);
        }

        foreach (var (name, value) in node.Attributes)
        {
            writer.WriteAttributeString(name, value);
        }

        if (node.CData && node.Text.IndexOf("]]>", StringComparison.Ordinal) < 0)
        {
            writer.WriteCData(node.Text);
        }
        else
        {
            writer.WriteString(node.Text);
        }

        foreach (var child in node.Children)
        {
            WriteNode(writer, child);
        }

        writer.WriteEndElement();
    }

    private sealed record XmlNode(string Name, (string, string)[] Attributes, string Text, bool CData, XmlNode[] Children);
}
