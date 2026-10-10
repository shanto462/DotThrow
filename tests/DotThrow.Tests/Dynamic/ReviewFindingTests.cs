using System;
using System.Linq;
using DotThrow.Dynamic;
using Xunit;

namespace DotThrow.Tests.Dynamic;

// Regression tests for the findings of the blind review of the dynamic rules.
public sealed class ReviewFindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_trailing_brackets_path_keeps_its_groups_and_conditions(bool reflection)
    {
        var definition = RuleSetDefinition.FromJson("""
            { "rules": [ { "property": "Tags[]", "group": "g", "when": { "property": "Flag", "equalTo": true }, "checks": [ { "check": "LongerThan", "value": 3 } ] } ] }
            """);
        var validator = reflection ? new DynamicValidator<Sample>(definition) : new DynamicValidator<Sample>(definition, Maps.Sample());
        var sample = new Sample { Tags = ["math"] };
        Assert.True(validator.Validate(sample, "g").IsValid);
        sample.Flag = true;
        Assert.True(validator.Validate(sample).IsValid);
        Assert.Equal(["Tags[0]"], validator.Validate(sample, "g").Failures.Select(f => f.Name));
        Assert.Contains("use either 'Each' or '[]'", Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(Rules.One("Tags[]", "\"Null\"", "\"each\": true"), Maps.Sample())).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Item_rules_take_the_cascade_of_the_definition_like_code()
    {
        var definition = RuleSetDefinition.FromJson("""
            { "ruleCascade": "Continue", "rules": [ { "property": "Lines[].Sku", "checks": [ { "check": "ShorterThan", "value": 5 }, "NotDigits" ] } ] }
            """);
        var dynamic = new DynamicValidator<Sample>(definition, Maps.Sample());
        var code = new Validator<Sample> { RuleCascade = CascadeMode.Continue }
            .ForEach(x => x.Lines, line => line.For(l => l.Sku, sku => sku.IfShorterThan(5).IfNotDigits()));
        var sample = new Sample { Lines = [new Line { Sku = "ab" }] };
        var expected = code.Validate(sample).Failures.Select(f => f.Name + ":" + f.ErrorCode).ToList();
        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, dynamic.Validate(sample).Failures.Select(f => f.Name + ":" + f.ErrorCode));
    }

    [Fact]
    public void NaN_limits_and_numeric_regex_options_are_definition_errors()
    {
        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                { "rules": [
                  { "property": "Ratio", "checks": [ { "check": "GreaterThan", "value": "NaN" }, { "check": "OutOfRange", "min": "NaN", "max": 5 } ] },
                  { "property": "Text", "checks": [ { "check": "Matches", "value": "a", "options": "99999" } ] } ] }
                """),
            Maps.Sample())).Errors;
        Assert.Equal(
            [
                "Rules[0].Checks[0]: check 'GreaterThan' on 'Ratio': 'Value' must not be NaN",
                "Rules[0].Checks[1]: check 'OutOfRange' on 'Ratio': 'Min' must not be NaN",
                "Rules[1].Checks[0]: check 'Matches' on 'Text': '99999' is not a list of RegexOptions",
            ],
            errors);

        // NaN is still a value for EqualTo.
        var equal = new DynamicValidator<Sample>(Rules.One("Ratio", """{ "check": "EqualTo", "value": "NaN" }"""), Maps.Sample());
        Assert.False(equal.Validate(new Sample { Ratio = double.NaN }).IsValid);
    }

    [Fact]
    public void A_custom_factory_that_returns_null_is_an_error()
    {
        var registry = new CheckRegistry().Add<string>("Nothing", _ => null!);
        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(Rules.One("Text", "\"Nothing\""), Maps.Sample(), registry)).Errors;
        Assert.Equal(["Rules[0].Checks[0]: check 'Nothing' on 'Text': the check factory returned no check"], errors);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Reflection_handles_struct_collections_private_getters_ref_structs_and_bad_type_names()
    {
        var validator = new DynamicValidator<Edge>(Rules.One("Fixed", """{ "check": "CountGreaterThan", "value": 2 }"""));
        Assert.Equal("CountGreaterThan", Assert.Single(validator.Validate(new Edge()).Failures).ErrorCode);

        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Edge>(RuleSetDefinition.FromJson("""
            { "rules": [
              { "property": "Secret", "checks": [ "Null" ] },
              { "property": "Window", "checks": [ "Null" ] },
              { "property": "Fixed", "each": true, "checks": [ "Zero" ] },
              { "property": "Shown", "checks": [ { "check": "Type", "value": "Foo[[, Bar=" } ] } ] }
            """))).Errors;
        Assert.Collection(
            errors,
            e => Assert.StartsWith("Rules[0].Property: 'Secret' is not a property of Edge", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[1].Property: 'Window' is not a property of Edge", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[2]: 'Each' needs the item type of 'Fixed'", e, StringComparison.Ordinal),
            e => Assert.StartsWith("Rules[3].Checks[0]: check 'Type' on 'Shown': the type 'Foo[[, Bar=' is not known", e, StringComparison.Ordinal));
    }
#endif

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nullable_custom_comparables_unwrap(bool reflection)
    {
        var definition = Rules.One("MaybeCost", """{ "check": "GreaterThan", "value": "10" }""");
        var validator = reflection
            ? new DynamicValidator<Edge>(definition)
            : new DynamicValidator<Edge>(definition, new PropertyMap<Edge>().AddComparable(x => x.MaybeCost, Money.Parse));
        var code = new Validator<Edge>().For(x => x.MaybeCost, g => g.IfNull().IfGreaterThan(new Money(10)));
        foreach (var edge in new[] { new Edge(), new Edge { MaybeCost = new Money(11) }, new Edge { MaybeCost = new Money(1) } })
        {
            Assert.Equal(code.Validate(edge).Failures.Select(f => f.ErrorCode + f.Message), validator.Validate(edge).Failures.Select(f => f.ErrorCode + f.Message));
        }
    }

    [Fact]
    public void Property_maps_check_paths_and_getters_before_registering()
    {
        Assert.Throws<ArgumentException>(() => new PropertyMap<Sample>().Add(x => x.Text!.Trim()));
        Assert.Throws<ArgumentException>(() => new PropertyMap<Sample>().Add("Tags[]", x => x.Tags));
        Assert.Throws<ArgumentNullException>(() => new PropertyMap<Sample>().AddEnum<Status>("Status", (Func<Sample, Status>)null!));
        Assert.Throws<ArgumentNullException>(() => new PropertyMap<Sample>().AddComparable<Money>(null!, Money.Parse));

        var calls = 0;
        var map = new PropertyMap<Sample>().AddCollection(x => x.Lines);
        Assert.Throws<ArgumentException>(() => map.AddCollection(x => x.Lines, _ => calls++));
        Assert.Equal(0, calls);
        Assert.Equal(["Lines"], map.Paths);

        // Each on a collection added with Add points to AddCollection.
        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(Rules.One("Counts", "\"Null\"", "\"each\": true"), Maps.Sample())).Errors;
        Assert.Equal(["Rules[0]: 'Each' needs the item type of 'Counts'; add it with PropertyMap.AddCollection instead of Add"], errors);
    }

    [Fact]
    public void Definition_and_rules_change_together_and_a_byte_order_mark_is_ignored()
    {
        var first = Rules.One("Text", "\"Null\"");
        var second = RuleSetDefinition.FromJson("﻿{ \"rules\": [ { \"property\": \"Text\", \"checks\": [ \"NotNull\" ] } ] }");
        var validator = new DynamicValidator<Sample>(first, Maps.Sample());
        validator.Update(second);
        Assert.Same(second, validator.Definition);
        Assert.Equal(["NotNull"], validator.Describe().Rules[0].Checks);
    }
}
