using System;
using System.Linq;
using DotThrow.Dynamic;
using Xunit;

namespace DotThrow.Tests.Dynamic;

// RuleSetDefinition.FromJson: the strict reader and the binding to the definition classes.
public sealed class DefinitionJsonTests
{
    [Fact]
    public void Reads_every_field_with_keys_in_any_case_and_numbers_as_text()
    {
        var definition = RuleSetDefinition.FromJson("""
            {
              "RULES": [
                {
                  "Property": "Price", "each": "true", "AllowNull": false, "group": "a", "groups": [ "b", "c" ],
                  "when": { "property": "Flag", "equalTo": true }, "unless": { "PROPERTY": "Text", "isNull": true },
                  "severity": "Warning", "errorCode": "E1", "message": "m", "name": "Cost", "cascade": "Continue",
                  "checks": [
                    "Positive",
                    { "CHECK": "OutOfRange", "min": 1.50, "max": 1e3, "message": "x", "errorCode": "E2" },
                    { "check": "OneOf", "values": [ 1, "2", -3.25 ], "comparison": "Ordinal", "options": "IgnoreCase", "value": "v" }
                  ]
                }
              ],
              "ruleCascade": "Continue",
              "classCascade": "Stop"
            }
            """);

        Assert.Equal("Continue", definition.RuleCascade);
        Assert.Equal("Stop", definition.ClassCascade);
        var rule = Assert.Single(definition.Rules);
        Assert.Equal("Price", rule.Property);
        Assert.True(rule.Each);
        Assert.False(rule.AllowNull);
        Assert.Equal("a", rule.Group);
        Assert.Equal(["b", "c"], rule.Groups!);
        Assert.Equal("Flag", rule.When!.Property);
        Assert.Equal("true", rule.When.EqualTo);
        Assert.True(rule.Unless!.IsNull);
        Assert.Equal("Warning", rule.Severity);
        Assert.Equal("E1", rule.ErrorCode);
        Assert.Equal("m", rule.Message);
        Assert.Equal("Cost", rule.Name);
        Assert.Equal("Continue", rule.Cascade);
        Assert.Equal(3, rule.Checks.Count);
        Assert.Equal("Positive", rule.Checks[0].Check);
        Assert.Equal("1.50", rule.Checks[1].Min);
        Assert.Equal("1e3", rule.Checks[1].Max);
        Assert.Equal("E2", rule.Checks[1].ErrorCode);
        Assert.Equal(["1", "2", "-3.25"], rule.Checks[2].Values!);
        Assert.Equal("Ordinal", rule.Checks[2].Comparison);
        Assert.Equal("IgnoreCase", rule.Checks[2].Options);
    }

    [Fact]
    public void Reads_escapes_and_unicode()
    {
        var definition = RuleSetDefinition.FromJson("""{ "rules": [ { "property": "Text", "checks": [ { "check": "Contains", "value": "a\"b\\c\/é\n😀" } ] } ] }""");
        Assert.Equal("a\"b\\c/é\n😀", definition.Rules[0].Checks[0].Value);
    }

    [Theory]
    [InlineData("""{ "rules": [], }""", "line 1, column 16")]
    [InlineData("""{ "rules": [] } // comment""", "unexpected text after the end")]
    [InlineData("""{ 'rules': [] }""", "expected a property name in double quotes")]
    [InlineData("""{ "rules": [ 01 ] }""", "line 1, column 15")]
    [InlineData("""{ "rules": [ +1 ] }""", "expected a value")]
    [InlineData("""{ "rules": [ 1. ] }""", "expected a digit")]
    [InlineData("""{ "rules": "a""", "a string is not closed")]
    [InlineData("""{ "rules": "\x" }""", "unknown escape sequence")]
    [InlineData("""{ "rules": "\u12" }""", "four hex digits")]
    [InlineData("""{ "rules": tru }""", "expected a value")]
    [InlineData("""{ "rules": [ NaN ] }""", "expected a value")]
    [InlineData("", "unexpected end of the JSON text")]
    [InlineData("{", "expected a property name")]
    public void Rejects_json_that_is_not_strict(string json, string expected)
    {
        var exception = Assert.Throws<RuleDefinitionException>(() => RuleSetDefinition.FromJson(json));
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
        Assert.StartsWith("JSON (line", Assert.Single(exception.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_control_characters_in_strings_and_too_deep_nesting()
    {
        Assert.Contains("control character", Assert.Throws<RuleDefinitionException>(() => RuleSetDefinition.FromJson("{ \"rules\": \"a\tb\" }")).Message, StringComparison.Ordinal);
        var deep = new string('[', 100) + new string(']', 100);
        Assert.Contains("nested too deeply", Assert.Throws<RuleDefinitionException>(() => RuleSetDefinition.FromJson(deep)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Collects_every_shape_error_with_its_place_and_a_suggestion()
    {
        var exception = Assert.Throws<RuleDefinitionException>(() => RuleSetDefinition.FromJson("""
            {
              "rules": [
                { "property": "Text", "chekcs": [ "Null" ] },
                { "property": [ 1 ], "each": "maybe", "checks": [ { "check": "Null", "value": { } } ] },
                { "property": "Text", "property": "Name", "checks": "Null" }
              ],
              "extra": 1
            }
            """));

        Assert.Equal(
            [
                "Rules[0].Chekcs (line 3, column 37): unknown key (did you mean 'checks'?).",
                "Rules[1].Property (line 4, column 19): expected a string or a number.",
                "Rules[1].Each (line 4, column 34): expected true or false.",
                "Rules[1].Checks[0].Value (line 4, column 83): expected a string or a number.",
                "Rules[2].Property (line 5, column 39): the key appears more than once.",
                "Rules[2].Checks (line 5, column 57): expected an array.",
                "Extra (line 7, column 12): unknown key.",
            ],
            exception.Errors);
        Assert.StartsWith("The rule definition has 7 errors:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Null_values_mean_not_set()
    {
        var definition = RuleSetDefinition.FromJson("""{ "rules": [ { "property": null, "group": null, "groups": null, "when": null, "checks": [ { "check": "Null", "value": null, "values": null } ] } ], "ruleCascade": null }""");
        var rule = definition.Rules.Single();
        Assert.Null(rule.Property);
        Assert.Null(rule.Groups);
        Assert.Null(rule.When);
        Assert.Null(rule.Checks[0].Value);
        Assert.Null(definition.RuleCascade);
    }

    [Fact]
    public void Null_json_text_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => RuleSetDefinition.FromJson(null!));
    }
}
