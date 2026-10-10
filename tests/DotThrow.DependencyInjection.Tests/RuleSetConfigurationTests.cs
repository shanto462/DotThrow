using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotThrow.Dynamic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DotThrow.DependencyInjection.Tests;

public sealed class RuleSetConfigurationTests
{
    // The section "Validation:Contact" of an appsettings-style file, also read with FromJson.
    internal const string ContactRules = """
        {
          "ruleCascade": "Stop",
          "classCascade": "Continue",
          "rules": [
            { "property": "Name", "message": null, "allowNull": null, "checks": [ "WhiteSpace", { "check": "LongerThan", "value": 20 } ] },
            { "property": "Email", "allowNull": true, "checks": [ { "check": "NotEmailAddress", "message": "Bad email.", "errorCode": "Email" } ] },
            { "property": "Age", "severity": "Warning", "errorCode": "AgeRange", "name": "age", "cascade": "Continue", "when": { "property": "Country", "notEqualTo": "XX" },
              "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] },
            { "property": "Tags", "each": true, "checks": [ { "check": "OneOf", "values": [ "spam", "test" ], "comparison": "OrdinalIgnoreCase" } ] },
            { "property": "Address.City", "group": "shipping", "groups": [ "export" ], "when": { "property": "Country", "equalTo": "NL" },
              "unless": { "property": "Address", "isNull": true }, "checks": [ "NullOrEmpty", { "check": "Matches", "value": "^[A-Z]", "options": "IgnoreCase" } ] },
            { "property": "Country", "when": null, "checks": [ { "check": "NotOneOf", "values": [ "NL", "BE", "DE" ] } ] }
          ]
        }
        """;

    internal static string AppSettings(string rules) => "{ \"Logging\": { \"LogLevel\": { \"Default\": \"Information\" } }, \"Validation\": { \"Contact\": " + rules + " } }";

    internal static IConfigurationRoot Json(string json) =>
        new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();

    [Fact]
    public void Appsettings_section_reads_the_same_as_FromJson()
    {
        var fromConfiguration = RuleSetConfiguration.Read(Json(AppSettings(ContactRules)).GetSection("Validation:Contact"));
        var fromJson = RuleSetDefinition.FromJson(ContactRules);

        Assert.Equal(Describe(fromJson), Describe(fromConfiguration));
        Assert.Equal(6, fromConfiguration.Rules.Count);
        Assert.Equal("WhiteSpace", fromConfiguration.Rules[0].Checks[0].Check);
        Assert.Equal("20", fromConfiguration.Rules[0].Checks[1].Value);
        Assert.True(fromConfiguration.Rules[1].AllowNull);
        Assert.True(fromConfiguration.Rules[4].Unless!.IsNull);
        Assert.Null(fromConfiguration.Rules[5].When);
    }

    [Fact]
    public void Flat_keys_ignore_case_and_order_items_by_number()
    {
        var values = new Dictionary<string, string?>
        {
            ["Rules:0:PROPERTY"] = "Name",
            ["rules:0:Checks:0"] = "WhiteSpace",
        };
        for (var i = 0; i < 12; i++)
        {
            values[$"rules:1:checks:{i}:check"] = "LongerThan";
            values[$"rules:1:checks:{i}:value"] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        values["rules:1:property"] = "Email";
        values["rules:1:each"] = "False";
        values["rules:1:allowNull"] = "TRUE";
        var definition = RuleSetConfiguration.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        Assert.Equal("Name", definition.Rules[0].Property);
        Assert.Equal(Enumerable.Range(0, 12).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), definition.Rules[1].Checks.Select(c => c.Value));
        Assert.True(definition.Rules[1].AllowNull);
        Assert.False(definition.Rules[1].Each);
    }

    [Fact]
    public void Empty_values_read_as_not_set_and_empty_list_items_stay()
    {
        var definition = RuleSetConfiguration.Read(Json("""
            { "rules": [ { "property": "Country", "message": "", "groups": [], "checks": [ { "check": "OneOf", "values": [ "", "NL" ] } ] }, { "property": "Name", "checks": [] } ] }
            """));

        Assert.Null(definition.Rules[0].Message);
        Assert.Null(definition.Rules[0].Groups);
        Assert.Equal(["", "NL"], definition.Rules[0].Checks[0].Values!);
        Assert.Empty(definition.Rules[1].Checks);
    }

    [Fact]
    public void Every_error_is_reported_with_its_place()
    {
        var exception = Assert.Throws<RuleDefinitionException>(() => RuleSetConfiguration.Read(Json("""
            { "Validation": { "Contact": {
              "rulez": [],
              "ruleCascade": { "a": "b" },
              "rules": [
                "not an object",
                { "property": "Name", "each": "maybe", "chekcs": [], "when": "always", "groups": "a",
                  "checks": [ { "chek": "WhiteSpace" }, { "check": "OneOf", "values": [ { "a": "b" } ] }, { "check": "x", "values": { "a": "b" } } ] },
                { "property": "Age", "checks": "WhiteSpace", "unless": { "isNull": "maybe" } }
              ] } } }
            """).GetSection("Validation:Contact")));

        string[] expected =
        [
            "Rules[0] (Validation:Contact:rules:0): expected an object.",
            "Rules[1].Checks[0].Chek (Validation:Contact:rules:1:checks:0:chek): unknown key (did you mean 'check'?).",
            "Rules[1].Checks[1].Values[0] (Validation:Contact:rules:1:checks:1:values:0): expected a string or a number.",
            "Rules[1].Checks[2].Values (Validation:Contact:rules:1:checks:2:values): expected an array.",
            "Rules[1].Chekcs (Validation:Contact:rules:1:chekcs): unknown key (did you mean 'checks'?).",
            "Rules[1].Each (Validation:Contact:rules:1:each): expected true or false.",
            "Rules[1].Groups (Validation:Contact:rules:1:groups): expected an array.",
            "Rules[1].When (Validation:Contact:rules:1:when): expected an object.",
            "Rules[2].Checks (Validation:Contact:rules:2:checks): expected an array.",
            "Rules[2].Unless.IsNull (Validation:Contact:rules:2:unless:isNull): expected true or false.",
            "RuleCascade (Validation:Contact:ruleCascade): expected a string or a number.",
            "Rulez (Validation:Contact:rulez): unknown key (did you mean 'rules'?).",
        ];
        Assert.Equal(expected.OrderBy(e => e, StringComparer.Ordinal), exception.Errors.OrderBy(e => e, StringComparer.Ordinal));
    }

    [Fact]
    public void Missing_or_scalar_sections_are_errors()
    {
        var configuration = Json("""{ "Validation": { "Contact": "rules" }, "Other": { "x": 1 } }""");

        Assert.Equal(
            ["(root) (Validation:Missing): the section is missing or empty."],
            Assert.Throws<RuleDefinitionException>(() => RuleSetConfiguration.Read(configuration.GetSection("Validation:Missing"))).Errors);
        Assert.Equal(
            ["(root) (Validation:Contact): expected an object."],
            Assert.Throws<RuleDefinitionException>(() => RuleSetConfiguration.Read(configuration.GetSection("Validation:Contact"))).Errors);
        Assert.Equal(
            ["Other (Other): unknown key.", "Validation (Validation): unknown key."],
            Assert.Throws<RuleDefinitionException>(() => RuleSetConfiguration.Read(configuration)).Errors.OrderBy(e => e, StringComparer.Ordinal));
        Assert.Throws<ArgumentNullException>("section", () => RuleSetConfiguration.Read(null!));
    }

    [Fact]
    public void A_section_whose_keys_are_not_numbers_is_not_an_array()
    {
        var exception = Assert.Throws<RuleDefinitionException>(() => RuleSetConfiguration.Read(Json("""{ "rules": { "first": { "property": "Name" } } }""")));

        Assert.Equal(["Rules (rules): expected an array."], exception.Errors);
    }

    internal static string Describe(RuleSetDefinition definition)
    {
        var text = new StringBuilder().Append(definition.RuleCascade).Append('|').Append(definition.ClassCascade);
        foreach (var rule in definition.Rules)
        {
            text.AppendLine().Append(string.Join("|", rule.Property, rule.Each, rule.AllowNull, rule.Group, List(rule.Groups), Condition(rule.When), Condition(rule.Unless),
                rule.Severity, rule.ErrorCode, rule.Message, rule.Name, rule.Cascade));
            foreach (var check in rule.Checks)
            {
                text.AppendLine().Append("  ").Append(string.Join("|", check.Check, check.Value, check.Min, check.Max, List(check.Values), check.Comparison, check.Options, check.Message, check.ErrorCode));
            }
        }

        return text.ToString();
    }

    private static string List(List<string>? items) => items is null ? "null" : "[" + string.Join(",", items) + "]";

    private static string Condition(ConditionDefinition? condition) =>
        condition is null ? "null" : string.Join("/", condition.Property, condition.EqualTo, condition.NotEqualTo, condition.IsNull);
}
