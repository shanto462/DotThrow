using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace DotThrow.Internal;

// Built-in translations of the default templates, for the languages FluentValidation ships.
// The translations are machine-assisted; native review is welcome.
//
// Source: src/DotThrow/Translations/<culture>.txt, one "Key<TAB>Template" line per template
// (see Translations.targets). The build compresses each file with Deflate and embeds it as the
// manifest resource "DotThrow.Translations.<culture>". A culture is read, decompressed and
// parsed the first time a message needs it (once, even when many threads ask at the same
// time) and kept for the life of the process.
//
// Adding a language: add <culture>.txt with the same keys and placeholders as
// DefaultTemplates.All, and add the culture name to Cultures. Use the neutral culture name
// ("de"); add a specific one ("pt-BR") only when it differs. Lookup walks CultureInfo.Parent,
// so "de" also serves "de-AT", "sr" serves "sr-Cyrl-RS", "sr-Latn" serves "sr-Latn-RS" and
// "uz-Cyrl" serves "uz-Cyrl-UZ".
internal static class Translations
{
    private const string ResourcePrefix = "DotThrow.Translations.";

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>?>> Loaded =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string>? Find(string cultureName) =>
        cultureName.Length == 0
            ? null
            : Loaded.GetOrAdd(cultureName, static name => new Lazy<IReadOnlyDictionary<string, string>?>(() => Load(name), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    // The culture names that have a translation file, in the standard casing of CultureInfo.Name.
    public static IReadOnlyList<string> Cultures { get; } =
    [
        "ar", "az", "be", "bg", "bn", "bs", "ca", "cs", "cy", "da", "de", "el", "es", "et", "fa",
        "fi", "fr", "he", "hi", "hr", "hu", "id", "is", "it", "ja", "ka", "kk", "km", "ko", "lt",
        "lv", "mk", "nb", "nl", "nn", "pl", "pt", "pt-BR", "rm", "ro", "ru", "sk", "sl", "sq",
        "sr", "sr-Latn", "sv", "ta", "te", "tg", "th", "tr", "uk", "uz", "uz-Cyrl", "vi",
        "zh-Hans", "zh-Hant",
    ];

    // Neutral cultures that are not in FluentValidation's list and share another file.
    public static IReadOnlyDictionary<string, string> Aliases { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["no"] = "nb",
            ["zh"] = "zh-Hans",
        };

    // The name of the translation file for a culture name, or null.
    public static string? FileOf(string cultureName)
    {
        if (Aliases.TryGetValue(cultureName, out var alias))
        {
            return alias;
        }

        foreach (var name in Cultures)
        {
            if (string.Equals(name, cultureName, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private static Dictionary<string, string>? Load(string cultureName)
    {
        var file = FileOf(cultureName);
        if (file is null)
        {
            return null;
        }

        using var stream = typeof(Translations).Assembly.GetManifestResourceStream(ResourcePrefix + file);
        if (stream is null)
        {
            return null;
        }

        using var deflate = new DeflateStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(deflate, Encoding.UTF8);
        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var tab = line.IndexOf('\t');
            if (tab > 0 && line[0] != '#')
            {
                templates[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
        }

        return templates;
    }
}
