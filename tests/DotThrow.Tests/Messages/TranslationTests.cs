using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DotThrow.Internal;
using Xunit;

namespace DotThrow.Tests.Messages;

// The built-in translations (58 languages): complete, same placeholders as English, never the
// checked value, and found through aliases and parent cultures.
public sealed class TranslationTests
{
    private static readonly Regex Placeholder = new(@"\{([A-Za-z]+)(:[^}]*)?\}", RegexOptions.CultureInvariant);

    public static TheoryData<string> Cultures => new(Translations.Cultures);

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Translation_has_every_english_key_and_no_other(string culture)
    {
        var translation = Translations.Find(culture);

        Assert.NotNull(translation);
        Assert.Empty(DefaultTemplates.All.Keys.Except(translation.Keys));
        Assert.Empty(translation.Keys.Except(DefaultTemplates.All.Keys));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Translation_keeps_the_english_placeholders(string culture)
    {
        var translation = Translations.Find(culture)!;

        var mismatches = DefaultTemplates.All
            .Where(pair => translation.TryGetValue(pair.Key, out var text) && !Names(text).SetEquals(Names(pair.Value)))
            .Select(pair => pair.Key)
            .ToList();

        Assert.Empty(mismatches);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Translation_never_shows_the_checked_value_and_has_text(string culture)
    {
        var translation = Translations.Find(culture)!;

        Assert.All(translation, pair =>
        {
            Assert.DoesNotContain("{PropertyValue", pair.Value, System.StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(pair.Value), pair.Key);
            Assert.DoesNotContain("\u2014", pair.Value, System.StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("de-AT", "de")]
    [InlineData("pt-PT", "pt")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("sr-Latn-RS", "sr-Latn")]
    [InlineData("zh-Hant-TW", "zh-Hant")]
    [InlineData("no", "nb")]
    [InlineData("zh", "zh-Hans")]
    public void Specific_cultures_and_aliases_find_their_translation(string culture, string file)
    {
        var expected = Translations.Find(file)![ErrorCodes.Null];

        Assert.Equal(expected, MessageTemplates.Get(ErrorCodes.Null, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void Cultures_without_a_translation_use_english()
    {
        Assert.Null(Translations.Find("sw"));
        Assert.Null(Translations.Find(string.Empty));
        Assert.Equal("'{PropertyName}' must not be null.", MessageTemplates.Get(ErrorCodes.Null, CultureInfo.GetCultureInfo("sw-KE")));
        Assert.Equal("'{PropertyName}' must not be null.", MessageTemplates.Get(ErrorCodes.Null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Every_listed_culture_loads_once_and_is_shared()
    {
        Assert.All(Translations.Cultures, culture => Assert.Same(Translations.Find(culture), Translations.Find(culture.ToUpperInvariant())));
    }

    private static HashSet<string> Names(string text) =>
        [.. Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value)];
}
