using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DotThrow.Internal;

// Built-in translations of the default templates, loaded lazily the first time a culture
// needs them and kept for the life of the process.
//
// Adding a language (spec H):
// 1. Add Internal/Translations/<Language>.cs with
//        internal static class <Language>
//        {
//            public static Dictionary<string, string> Create() => new(StringComparer.Ordinal)
//            {
//                [ErrorCodes.Null] = "...",
//                [TemplateKeys.CountEqualToOne] = "...",
//            };
//        }
//    using the same keys as DefaultTemplates.All and the same placeholders as English.
// 2. Add a line to Load below: "de" => German.Create(),
//    Use the neutral culture name ("de"); add a specific one ("pt-BR") only when it differs.
internal static class Translations
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>?> Loaded =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string>? Find(string cultureName) =>
        cultureName.Length == 0 ? null : Loaded.GetOrAdd(cultureName, static name => Load(name));

    // The culture names that have built-in translations, for tests and docs. Keep it in
    // sync with Load.
    public static IReadOnlyList<string> Cultures { get; } = [];

    // Culture names come from CultureInfo.Name, so they have the standard casing.
    private static Dictionary<string, string>? Load(string cultureName) => cultureName switch
    {
        // Phase 4 adds one line per language, for example: "de" => German.Create(),
        _ => null,
    };
}
