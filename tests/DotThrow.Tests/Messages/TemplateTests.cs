using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DotThrow.Internal;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Messages;

/// <summary>Templates, placeholders, cultures and the public MessageTemplates API.</summary>
[Collection(nameof(MessageTemplates))]
public sealed class TemplateTests : IDisposable
{
    public TemplateTests() => MessageTemplates.Reset();

    public void Dispose() => MessageTemplates.Reset();

    [Fact]
    public void Every_error_code_has_an_English_template()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!);
        foreach (var code in codes)
        {
            Assert.True(DefaultTemplates.All.ContainsKey(code), code);
        }
    }

    [Fact]
    public void Every_variant_key_belongs_to_an_error_code_with_a_template()
    {
        foreach (var key in DefaultTemplates.All.Keys.Where(k => k.HasChar('.')))
        {
            Assert.True(DefaultTemplates.All.ContainsKey(TemplateKeys.CodeOf(key)), key);
        }
    }

    [Fact]
    public void No_default_template_shows_the_checked_value_and_every_placeholder_is_known()
    {
        foreach (var pair in DefaultTemplates.All)
        {
            var key = pair.Key;
            var text = pair.Value;
            Assert.DoesNotContain("{PropertyValue", text, StringComparison.Ordinal);
            Assert.StartsWith("'{PropertyName}' ", text, StringComparison.Ordinal);
            foreach (var name in PlaceholderNames(text))
            {
                Assert.NotEqual(Placeholder.Custom, Placeholders.Find(name));
            }

            Assert.DoesNotContain("\u2014", text, StringComparison.Ordinal);
            _ = key;
        }
    }

    [Fact]
    public void A_custom_message_fills_in_placeholders_with_formats()
    {
        var message = Check.MessageOf("abcdef", g => g.IfLongerThan(3, "{PropertyName}: max {MaxLength}, got {TotalLength}"));
        Assert.Equal("input: max 3, got 6", message);

        Assert.Equal("between 1.50 and 2.00", Check.MessageOf(5.0, g => g.IfOutOfRange(1.5, 2.0, "between {From:N2} and {To:N2}")));
        Assert.Equal("value 5 is bad", Check.MessageOf(5, g => g.IfEqualTo(5, message: "value {PropertyValue} is bad")));
        Assert.Equal("text 'secret' is bad", Check.MessageOf("secret", g => g.IfContains("e", message: "text {PropertyValue} is bad")));
    }

    [Fact]
    public void Unknown_placeholders_and_stray_braces_stay_as_written()
    {
        const string Text = "{Nope} {PropertyName {Count} } {{x}} {a:b{c}} {";
        Assert.Equal("{Nope} {PropertyName {Count} } {{x}} {a:b{c}} {", Check.MessageOf("abc", g => g.IfLongerThan(1, Text)));
        Assert.Equal("{ not a placeholder }", Check.MessageOf("abc", g => g.IfLongerThan(1, "{ not a placeholder }")));
    }

    [Fact]
    public void A_message_without_braces_is_returned_as_the_same_instance()
    {
        const string Text = "Plain text.";
        Assert.Same(Text, Check.MessageOf("abc", g => g.IfLongerThan(1, Text)));
    }

    [Fact]
    public void Set_replaces_the_template_for_every_culture_and_its_variants()
    {
        MessageTemplates.Set(ErrorCodes.CountGreaterThan, "{PropertyName} has more than {MaxCount}.");
        Assert.Equal("input has more than 1.", Check.MessageOf(new[] { 1, 2 }, g => g.IfCountGreaterThan(1)));
        Assert.Equal("input has more than 0.", Check.MessageOf(new[] { 1, 2 }, g => g.IfCountGreaterThan(0)));
        Assert.Equal("{PropertyName} has more than {MaxCount}.", MessageTemplates.Get(ErrorCodes.CountGreaterThan));
        Assert.Equal("{PropertyName} has more than {MaxCount}.", MessageTemplates.Get(TemplateKeys.CountGreaterThanOne));
    }

    [Fact]
    public void Culture_fallback_goes_from_region_to_language_to_every_culture_to_the_defaults()
    {
        var de = CultureInfo.GetCultureInfo("de");
        var deAt = CultureInfo.GetCultureInfo("de-AT");
        MessageTemplates.Set(ErrorCodes.Null, "'{PropertyName}' darf nicht null sein.", de);
        MessageTemplates.Set(ErrorCodes.Null, "every culture", null);

        MessageTemplates.Culture = deAt;
        Assert.Equal("'input' darf nicht null sein.", Check.MessageOf<string?>(null, g => g.IfNull()));

        MessageTemplates.Set(ErrorCodes.Null, "Austria", deAt);
        Assert.Equal("Austria", Check.MessageOf<string?>(null, g => g.IfNull()));

        // A template set for every culture beats the built-in French translation.
        MessageTemplates.Culture = CultureInfo.GetCultureInfo("fr-FR");
        Assert.Equal("every culture", Check.MessageOf<string?>(null, g => g.IfNull()));

        // Swahili has no built-in translation, so after the removal it gets the English default.
        MessageTemplates.Culture = CultureInfo.GetCultureInfo("sw-KE");
        Assert.Equal("every culture", Check.MessageOf<string?>(null, g => g.IfNull()));
        Assert.True(MessageTemplates.Remove(ErrorCodes.Null));
        Assert.False(MessageTemplates.Remove(ErrorCodes.Null));
        Assert.Equal("'input' must not be null.", Check.MessageOf<string?>(null, g => g.IfNull()));
        Assert.Equal("Austria", MessageTemplates.Get(ErrorCodes.Null, deAt));
        Assert.Equal("'{PropertyName}' darf nicht null sein.", MessageTemplates.Get(ErrorCodes.Null, CultureInfo.GetCultureInfo("de-CH")));
    }

    [Fact]
    public void Culture_null_means_the_current_UI_culture()
    {
        MessageTemplates.Set(ErrorCodes.Zero, "nl: {PropertyName}", CultureInfo.GetCultureInfo("nl"));
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-BE");
            Assert.Equal("nl: input", Check.MessageOf(0, g => g.IfZero()));
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Assert.Equal("'input' must not be zero.", Check.MessageOf(0, g => g.IfZero()));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Reset_clears_templates_and_the_culture()
    {
        MessageTemplates.Set(ErrorCodes.Zero, "x");
        MessageTemplates.Culture = CultureInfo.GetCultureInfo("de");
        MessageTemplates.Reset();
        Assert.Null(MessageTemplates.Culture);
        Assert.Equal("'{PropertyName}' must not be zero.", MessageTemplates.Get(ErrorCodes.Zero));
        Assert.Null(MessageTemplates.Get("NoSuchCode"));
    }

    [Fact]
    public void Custom_codes_can_have_templates_and_Format_fills_them_in()
    {
        MessageTemplates.Set("NotSku", "'{PropertyName}' must be a SKU like {Example}.");
        var template = MessageTemplates.Get("NotSku")!;
        var values = new Dictionary<string, object?> { ["Example"] = "AB-12", ["Count"] = 3 };
        Assert.Equal("'code' must be a SKU like 'AB-12'.", MessageTemplates.Format(template, "code", values));
        Assert.Equal("3 {PropertyValue} {Missing}", MessageTemplates.Format("{Count} {PropertyValue} {Missing}", "code", values));
    }

    [Fact]
    public void Arguments_are_validated()
    {
        Assert.Throws<ArgumentNullException>(() => MessageTemplates.Set(null!, "x"));
        Assert.Throws<ArgumentException>(() => MessageTemplates.Set("", "x"));
        Assert.Throws<ArgumentNullException>(() => MessageTemplates.Set("x", null!));
        Assert.Throws<ArgumentNullException>(() => MessageTemplates.Get(null!));
        Assert.Throws<ArgumentNullException>(() => MessageTemplates.Format(null!, "x"));
    }

    [Fact]
    public void Templates_can_change_while_other_threads_fail_checks()
    {
        Parallel.For(0, 2000, i =>
        {
            if (i % 50 == 0)
            {
                MessageTemplates.Set(ErrorCodes.Negative, "{PropertyName} < 0 (" + i + ")");
            }

            var message = Check.MessageOf(-1, g => g.IfNegative());
            Assert.True(message == "'input' must not be negative." || message.StartsWith("input < 0 (", StringComparison.Ordinal), message);
        });
    }

    [Fact]
    public void Values_use_the_invariant_culture_whatever_the_current_culture_is()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("'input' must be less than or equal to 1.5.", Check.MessageOf(2.5, g => g.IfGreaterThan(1.5)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Exceptions_carry_the_rendered_message_in_throw_mode()
    {
        MessageTemplates.Set(ErrorCodes.LongerThan, "{PropertyName} max {MaxLength}");
        var exception = Assert.Throws<ArgumentException>(() => "abc".Throw("name").IfLongerThan(1));
        Assert.StartsWith("name max 1", exception.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<string> PlaceholderNames(string text)
    {
        var start = text.IndexOf('{');
        while (start >= 0)
        {
            var end = text.IndexOf('}', start);
            var name = text.Substring(start + 1, end - start - 1);
            var colon = name.IndexOf(':');
            yield return colon < 0 ? name : name.Substring(0, colon);
            start = text.IndexOf('{', end);
        }
    }
}
