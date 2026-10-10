using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DotThrow.Internal;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Fuzz tests for message templates: <see cref="MessageTemplates.Format"/>, custom messages of
/// checks, rule messages of <see cref="Validator{T}"/>, and <see cref="ValidationReport"/> output.
/// Rendering never throws, and the result follows the documented syntax, checked against a
/// reference renderer written with a regular expression.
/// </summary>
[Trait(Fuzz.Trait, Fuzz.Category)]
public sealed class MessageFuzzTests
{
    // {Name} or {Name:format}: a name is a letter or '_' and then letters, digits and '_'; the
    // format runs to the first '}' and has no '{'.
    private static readonly Regex PlaceholderPattern = new("\\{([\\p{L}_][\\p{L}\\p{Nd}_]*)(?::([^{}]*))?\\}", RegexOptions.CultureInvariant);

    private static readonly string[] TextPlaceholders = ["PropertyName", "Type", "Condition", "Schemes", "Characters"];

    private static readonly Gen<Dictionary<string, object?>> Values =
        from names in Gen.SubListOf(Generators.KnownPlaceholders.Concat(Generators.CustomPlaceholders).ToArray())
        from values in Generators.PlaceholderValue.ArrayOf(names.Count)
        select names.Zip(values, (name, value) => (name, value)).ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal);

    [Fact]
    public void Format_follows_the_documented_template_syntax() =>
        Fuzz.Run(Arb.From(Gen.Zip(Generators.Template, Values)), input =>
        {
            var (template, values) = input;
            var expected = Render(template, "Ada.Name", values);
            var actual = MessageTemplates.Format(template, "Ada.Name", values);
            Assert.True(expected == actual, $"Template {Contract.Show(template)} with {Describe(values)}: expected {Contract.Show(expected)}, got {Contract.Show(actual)}.");
            Assert.Equal(Render(template, "Ada.Name", new Dictionary<string, object?>()), MessageTemplates.Format(template, "Ada.Name", null));
            _ = MessageTemplate.TryValidate(template, out _);
        });

    // A custom message of a failed check is the template with {PropertyName}, {PropertyValue} and
    // the check's own placeholders filled in, the same in both modes.
    [Fact]
    public void Custom_messages_of_checks_fill_in_the_check_values() =>
        Fuzz.Run(Arb.From(Gen.Zip(Generators.Template, Generators.AnyTextGen)), input =>
        {
            var (template, text) = input;
            if (!Contract.Fails(text, g => g.IfLongerThan(3, template), ErrorCodes.LongerThan) || text is null)
            {
                return;
            }

            var report = new ValidationReport();
            _ = report.Check(text, "Ada.Name").IfLongerThan(3, template);
            var values = new Dictionary<string, object?> { ["PropertyValue"] = text, ["MaxLength"] = 3, ["TotalLength"] = text.Length };
            var expected = Render(template, "Ada.Name", values);
            Assert.True(expected == report.Failures[0].Message, $"Template {Contract.Show(template)}: expected {Contract.Show(expected)}, got {Contract.Show(report.Failures[0].Message)}.");
        });

    // A rule message (WithMessage) gets the rule values ({PropertyPath}) and the check's values,
    // and building the message never throws, whatever the template.
    [Fact]
    public void Rule_messages_fill_in_the_rule_and_check_values() =>
        Fuzz.Run(Arb.From(Gen.Zip(Generators.Template, Generators.AnyTextGen)), input =>
        {
            var (template, text) = input;
            var validator = new Validator<Account>().For(x => x.Name, n => n.IfLongerThan(3), rule => rule.WithMessage(template));
            var report = validator.Validate(new Account { Name = text });
            if (text is null || text.Length <= 3)
            {
                Assert.True(text is null ? report.Failures.Count == 1 : report.IsValid, report.ToString());
                return;
            }

            var failure = Assert.Single(report.Failures);
            var values = new Dictionary<string, object?> { ["PropertyValue"] = text, ["PropertyPath"] = new Verbatim("Name"), ["MaxLength"] = 3, ["TotalLength"] = text.Length };
            var expected = Render(template, "Name", values);
            Assert.True(expected == failure.Message, $"Template {Contract.Show(template)}: expected {Contract.Show(expected)}, got {Contract.Show(failure.Message)}.");
            Assert.Equal(ErrorCodes.LongerThan, failure.ErrorCode);
        });

    // MessageTemplates.Set refuses a template only for a format that no value can use, and the
    // template check never throws.
    [Fact]
    public void Template_validation_never_throws() =>
        Fuzz.Run(Generators.Of(Generators.Template), template =>
        {
            if (!MessageTemplate.TryValidate(template!, out var error))
            {
                Assert.False(string.IsNullOrEmpty(error));
                Assert.Contains(PlaceholderPattern.Matches(template!).Cast<Match>(), match => match.Groups[2].Length > 0);
            }
        });

    // The report's text forms hold every failure, whatever the names and messages.
    [Fact]
    public void Report_text_holds_every_failure() =>
        Fuzz.Run(Arb.From(Gen.Choose(1, 7).SelectMany(count => Gen.Zip(Gen.Zip(Generators.AnyTextGen, Generators.AnyTextGen), Generators.AnyTextGen, (pair, code) => (Name: pair.Item1, Message: pair.Item2, Code: code)).ArrayOf(count))), failures =>
        {
            var report = new ValidationReport();
            foreach (var (name, message, code) in failures)
            {
                report.Add(name ?? string.Empty, message ?? string.Empty, code);
            }

            var copy = new ValidationReport().AddRange(report, "Ada").AddRange(report);
            var dictionary = report.ToDictionary();
            Assert.Equal(failures.Length, dictionary.Values.Sum(messages => messages.Length));
            Assert.Equal(failures.Length * 2, copy.Failures.Count);
            var all = report.ToString(" | ");
            Assert.All(failures, failure => Assert.Contains(failure.Message ?? string.Empty, all, StringComparison.Ordinal));
            Assert.StartsWith("Validation failed with ", report.ToString(), StringComparison.Ordinal);
        });

    /// <summary>The reference renderer, from the documentation of <see cref="MessageTemplates"/> and <c>ValueFormatter</c>.</summary>
    private static string Render(string template, string propertyName, Dictionary<string, object?> values) =>
        PlaceholderPattern.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            var format = match.Groups[2].Length > 0 ? match.Groups[2].Value : null;
            if (name == "PropertyName")
            {
                return propertyName;
            }

            return values.TryGetValue(name, out var value) ? FormatValue(name, value, format) ?? match.Value : match.Value;
        });

    // Strings in single quotes (names and descriptions as they are), null as "null", dates for
    // people, double and float round-trip; a format that does not suit the value gives null.
    private static string? FormatValue(string name, object? value, string? format)
    {
        switch (value)
        {
            case null:
                return "null";
            case Verbatim verbatim:
                return verbatim.Text;
            case string text:
                return TextPlaceholders.Contains(name) ? text : "'" + text + "'";
            case DateTime date when format is null:
                return date.TimeOfDay.Ticks == 0 && date.Kind != DateTimeKind.Utc
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + Fraction(date.Ticks) + (date.Kind == DateTimeKind.Utc ? "Z" : string.Empty);
            case DateTimeOffset date when format is null:
                var minutes = (int)Math.Abs(date.Offset.TotalMinutes);
                return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + Fraction(date.Ticks) + " "
                    + (date.Offset < TimeSpan.Zero ? "-" : "+") + (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
            case double or float when format is null:
                return ((IFormattable)value).ToString("R", CultureInfo.InvariantCulture);
            case IFormattable formattable:
                try
                {
                    return formattable.ToString(format, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    return null;
                }

            default:
                return value.ToString();
        }
    }

    // ".5" for half a second: up to seven digits, without trailing zeros.
    private static string Fraction(long ticks)
    {
        var fraction = ticks % TimeSpan.TicksPerSecond;
        return fraction == 0 ? string.Empty : "." + fraction.ToString("0000000", CultureInfo.InvariantCulture).TrimEnd('0');
    }

    private static string Describe(Dictionary<string, object?> values) =>
        "{" + string.Join(", ", values.Select(pair => pair.Key + " = " + Contract.Show(pair.Value) + " (" + (pair.Value?.GetType().Name ?? "null") + ")")) + "}";

    // A value written as it is, like {PropertyPath}.
    private sealed record Verbatim(string Text);

    private sealed class Account
    {
        public string? Name { get; set; }
    }
}
