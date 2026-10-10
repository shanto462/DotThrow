using System;
using System.Collections.Generic;
using DotThrow.Dynamic;
using Xunit;

namespace DotThrow.Tests.Dynamic;

// Building a validator collects every definition error and throws them together.
public sealed class DefinitionErrorTests
{
    private static IReadOnlyList<string> ErrorsOf(string json, bool reflection = false)
    {
        var definition = RuleSetDefinition.FromJson(json);
        var exception = Assert.Throws<RuleDefinitionException>(() => reflection
            ? new DynamicValidator<Sample>(definition)
            : new DynamicValidator<Sample>(definition, Maps.Sample()));
        return exception.Errors;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Collects_all_errors_with_places_and_suggestions(bool reflection)
    {
        var errors = ErrorsOf(
            """
            {
              "rules": [
                { "property": "Txet", "checks": [ "NullOrEmpty" ] },
                { "property": "Text", "checks": [ "LongerThn", { "check": "LongerThan" }, { "check": "LongerThan", "value": "ten" } ] },
                { "property": "Number", "checks": [ "NotEmailAddress", { "check": "OutOfRange", "min": 10, "max": 1 }, { "check": "GreaterThan", "value": 1, "min": 2 } ] },
                { "property": "Text", "checks": [ "Must", { "check": "Matches", "value": "(" } ] },
                { "property": "Text", "severity": "Fatal", "cascade": "Sometimes", "errorCode": " ", "checks": [ "Null" ] },
                { "property": "Text", "checks": [ ] },
                { "property": "Number", "when": { "property": "Flag" }, "unless": { "property": "Nope", "isNull": true }, "checks": [ "Zero" ] },
                { "property": "Number", "groups": [ "" ], "checks": [ "Zero" ] }
              ],
              "ruleCascade": "Never"
            }
            """,
            reflection);

        Assert.Equal(
            [
                "RuleCascade: 'Never' is not a cascade mode (use Stop or Continue)",
                "Rules[0].Property: 'Txet' is not a property of Sample (did you mean 'Text'?)",
                "Rules[1].Checks[0]: check 'LongerThn' on 'Text': is not a known check (did you mean 'LongerThan'?)",
                "Rules[1].Checks[1]: check 'LongerThan' on 'Text': needs the 'Value' argument",
                "Rules[1].Checks[2]: check 'LongerThan' on 'Text': 'Value' value 'ten' is not a valid int",
                "Rules[2].Checks[0]: check 'NotEmailAddress' on 'Number': does not apply to int; it applies to string",
                "Rules[2].Checks[1]: check 'OutOfRange' on 'Number': 'Min' is greater than 'Max'",
                "Rules[2].Checks[2]: check 'GreaterThan' on 'Number': does not use the 'Min' argument",
                "Rules[3].Checks[0]: check 'Must' on 'Text': needs code (a lambda), so it cannot come from a definition; register a custom check in a CheckRegistry",
                "Rules[3].Checks[1]: check 'Matches' on 'Text': '(' is not a valid regular expression",
                "Rules[4].Severity: 'Fatal' is not a severity (use Error, Warning or Info)",
                "Rules[4].Cascade: 'Sometimes' is not a cascade mode (use Stop or Continue)",
                "Rules[4].ErrorCode: the error code is empty",
                "Rules[5]: the rule has no checks",
                "Rules[6].When: a condition needs 'EqualTo', 'NotEqualTo' or 'IsNull'",
                "Rules[6].Unless.Property: 'Nope' is not a property of Sample",
                "Rules[7].Groups: a group name is empty",
            ],
            errors);
    }

    [Fact]
    public void Wrong_argument_types_and_unknown_names_are_errors()
    {
        var errors = ErrorsOf("""
            {
              "rules": [
                { "property": "When", "checks": [ { "check": "Before", "value": "yesterday" } ] },
                { "property": "Status", "checks": [ { "check": "EqualTo", "value": "Open" }, { "check": "OneOf", "values": [ "Draft", "Nope" ] } ] },
                { "property": "Text", "checks": [ { "check": "Contains", "value": "a", "comparison": "Loose" }, { "check": "NotUri", "options": "Sideways" } ] },
                { "property": "Text", "checks": [ { "check": "NotEnumName", "value": "Missing" }, { "check": "NotParsable", "value": "Money" } ] },
                { "property": "Kind", "checks": [ { "check": "NotAssignableTo", "value": "NoSuchType" } ] },
                { "property": "Tags", "checks": [ { "check": "CountGreaterThan", "value": -1 }, { "check": "ContainsKey", "value": "a" } ] },
                { "property": "Number", "each": true, "checks": [ "Zero" ] },
                { "property": "Text[].Length", "checks": [ "Zero" ] },
                { "property": "Number", "checks": [ { "check": "NotMultipleOf", "value": 0 } ] },
                { "property": "Price", "checks": [ { "check": "PrecisionScaleExceeded", "values": [ 2, 5 ] } ] }
              ]
            }
            """);

        Assert.Collection(
            errors,
            e => Assert.StartsWith("Rules[0].Checks[0]: check 'Before' on 'When': 'Value' value 'yesterday' is not a valid DateTime", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[1].Checks[0]: check 'EqualTo' on 'Status': 'Value' value 'Open' is not a valid Status", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[1].Checks[1]: check 'OneOf' on 'Status': 'Values[1]' value 'Nope' is not a valid Status", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[2].Checks[0]: check 'Contains' on 'Text': 'Loose' is not a StringComparison", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[2].Checks[1]: check 'NotUri' on 'Text': 'Sideways' is not a UriKind", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[3].Checks[0]: check 'NotEnumName' on 'Text': the type 'Missing' is not known", e, StringComparison.Ordinal),
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
            e => Assert.StartsWith("Rules[3].Checks[1]: check 'NotParsable' on 'Text': 'Money' is not a parsable type DotThrow knows", e, StringComparison.Ordinal),
#else
            e => Assert.Equal("Rules[3].Checks[1]: check 'NotParsable' on 'Text': needs .NET 8 or later (IParsable)", e),
#endif
            e => Assert.StartsWith("Rules[4].Checks[0]: check 'NotAssignableTo' on 'Kind': the type 'NoSuchType' is not known", e, StringComparison.Ordinal),
            e => Assert.Equal("Rules[5].Checks[0]: check 'CountGreaterThan' on 'Tags': 'Value' must not be negative", e),
            e => Assert.StartsWith("Rules[5].Checks[1]: check 'ContainsKey' on 'Tags': applies to dictionaries", e, StringComparison.Ordinal),
            e => Assert.Equal("Rules[6]: 'Each' needs a collection, but 'Number' is int", e),
            e => Assert.Equal("Rules[7]: '[]' needs a collection, but 'Text' is string", e),
            e => Assert.StartsWith("Rules[8].Checks[0]: check 'NotMultipleOf' on 'Number':", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[9].Checks[0]: check 'PrecisionScaleExceeded' on 'Price':", e, StringComparison.Ordinal));
    }

    [Fact]
    public void A_null_definition_throws_and_a_null_rule_list_is_an_error()
    {
        Assert.Throws<ArgumentNullException>(() => new DynamicValidator<Sample>(null!, Maps.Sample()));
        var validator = new DynamicValidator<Sample>(Rules.One("Text", "\"Null\""), Maps.Sample());
        Assert.Throws<ArgumentNullException>(() => validator.Update(null!));
        Assert.Throws<ArgumentNullException>(() => validator.TryUpdate(null!, out _));
        Assert.Throws<ArgumentNullException>(() => validator.FindErrors(null!));
        Assert.Equal(["Rules: the rules are null"], Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(new RuleSetDefinition { Rules = null! }, Maps.Sample())).Errors);
    }

    [Fact]
    public void Item_paths_need_item_properties_in_the_map()
    {
        var definition = Rules.One("Tags[].Length", "\"Zero\"");
        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(definition, Maps.Sample())).Errors;
        Assert.Equal(["Rules[0]: the items of 'Tags' have no properties to check; add them with PropertyMap.AddCollection(..., items => ...)"], errors);

        var unknown = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(Rules.One("Lines[].Skus", "\"Null\""), Maps.Sample())).Errors;
        Assert.Equal(["Rules[0].Property: 'Skus' is not a property of Line (did you mean 'Sku'?)"], unknown);
    }

    [Fact]
    public void The_map_does_not_know_types_it_was_not_given()
    {
        var map = new PropertyMap<Sample>().Add(x => x.Text).Add(x => x.Status).Add(x => x.Kind);
        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                { "rules": [
                  { "property": "Status", "checks": [ "NotDefined" ] },
                  { "property": "Text", "checks": [ { "check": "NotEnumName", "value": "Status" } ] },
                  { "property": "Kind", "checks": [ { "check": "NotAssignableTo", "value": "Money" } ] } ] }
                """),
            map)).Errors;
        Assert.Collection(
            errors,
            e => Assert.Equal("Rules[0].Checks[0]: check 'NotDefined' on 'Status': does not apply to Status; it applies to enums", e),
            e => Assert.StartsWith("Rules[1].Checks[0]: check 'NotEnumName' on 'Text': the type 'Status' is not known; register it with PropertyMap.AddType", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[2].Checks[0]: check 'NotAssignableTo' on 'Kind': the type 'Money' is not known", e, StringComparison.Ordinal));
    }

    // Fuzzing fix: Uri threw IndexOutOfRangeException instead of UriFormatException for a file or
    // UNC host made only of bidi control characters ("file://\u202E", "\\\\\u200F" on .NET 10),
    // and the exception escaped the build and CheckArguments.Parse. Such text is now a normal
    // definition error. (.NET Framework's Uri may read the text differently, so there the test
    // only checks that no other exception escapes.)
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_uri_argument_that_Uri_cannot_read_is_a_definition_error(bool reflection)
    {
        var definition = RuleSetDefinition.FromJson("""
            { "rules": [
              { "property": "Link", "checks": [ { "check": "EqualTo", "value": "file://\u202E" } ] },
              { "property": "Text", "when": { "property": "Link", "equalTo": "\\\\\u200F" }, "checks": [ "Null" ] } ] }
            """);
        var exception = Record.Exception(() => reflection ? new DynamicValidator<Sample>(definition) : new DynamicValidator<Sample>(definition, Maps.Sample()));
        Assert.True(exception is null or RuleDefinitionException, exception?.ToString());
        var parse = Record.Exception(() => CheckArguments.Parse<Uri>("file://\u202E"));
        Assert.True(parse is null or FormatException, parse?.ToString());
#if NET
        Assert.Collection(
            Assert.IsType<RuleDefinitionException>(exception).Errors,
            e => Assert.Equal("Rules[0].Checks[0]: check 'EqualTo' on 'Link': 'Value' value 'file://\u202E' is not a valid Uri", e),
            e => Assert.Equal("Rules[1].When: 'EqualTo' value '\\\\\u200F' is not a valid Uri", e));
        Assert.Equal("'file://\u202E' is not a valid Uri.", Assert.IsType<FormatException>(parse).Message);
#endif
    }
}
