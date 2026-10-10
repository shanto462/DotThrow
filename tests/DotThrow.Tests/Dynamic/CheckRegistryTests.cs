using System;
using System.Linq;
using DotThrow.Dynamic;
using Xunit;

namespace DotThrow.Tests.Dynamic;

public sealed class CheckRegistryTests
{
    [Fact]
    public void The_default_registry_is_read_only_and_lists_the_built_in_checks()
    {
        Assert.True(CheckRegistry.Default.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => CheckRegistry.Default.Add<string>("Sku", (g, _) => g));
        Assert.Empty(CheckRegistry.Default.Names);
        Assert.Contains("LongerThan", CheckRegistry.BuiltInChecks);
        Assert.Contains("NotEmailAddress", CheckRegistry.BuiltInChecks);
        Assert.DoesNotContain("Must", CheckRegistry.BuiltInChecks);
        Assert.False(new CheckRegistry().IsReadOnly);
    }

    [Fact]
    public void Names_must_be_new_and_unique_per_type()
    {
        var registry = new CheckRegistry();
        Assert.Throws<ArgumentException>(() => registry.Add<string>("longerthan", (g, _) => g));
        Assert.Throws<ArgumentException>(() => registry.Add<string>(" ", (g, _) => g));
        Assert.Throws<ArgumentNullException>(() => registry.Add<string>("Sku", (CustomCheck<string>)null!));
        registry.Add<string>("Sku", (g, _) => g).Add<int>("Sku", (g, _) => g);
        Assert.Throws<ArgumentException>(() => registry.Add<string>("SKU", (g, _) => g));
        Assert.Equal(["Sku"], registry.Names);
    }

    [Fact]
    public void Custom_checks_run_with_their_arguments_message_and_error_code()
    {
        var registry = new CheckRegistry()
            .Add<string>("NotSku", (g, a) => g.IfNotStartsWith(a.Value ?? "SKU-", message: a.Message))
            .Add<int>("Between", a =>
            {
                var min = CheckArguments.Parse<int>(a.Min!);
                var max = CheckArguments.Parse<int>(a.Max!);
                return (g, _) => g.IfOutOfRange(min, max, a.Message);
            })
            .Add<object>("Required", (g, a) => g.IfNull(a.Message));

        var validator = new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                { "rules": [
                  { "property": "Text", "checks": [ { "check": "notsku", "value": "SKU-", "errorCode": "Sku1", "message": "{PropertyName} needs a SKU" } ] },
                  { "property": "Number", "checks": [ { "check": "Between", "min": 1, "max": 5 } ] },
                  { "property": "Version", "message": "Give a version", "checks": [ "Required" ] } ] }
                """),
            Maps.Sample(),
            registry);

        var report = validator.Validate(new Sample { Version = null });
        Assert.Equal(["Text", "Number", "Version"], report.Failures.Select(f => f.Name));
        Assert.Equal("Sku1", report.Failures[0].ErrorCode);
        Assert.Equal("Text needs a SKU", report.Failures[0].Message);
        Assert.Equal(new Validator<Sample>().For(x => x.Number, g => g.IfOutOfRange(1, 5)).Validate(new Sample()).Failures[0].Message, report.Failures[1].Message);
        Assert.Equal("Null", report.Failures[2].ErrorCode);
        Assert.Equal("Give a version", report.Failures[2].Message);
        Assert.Equal(["notsku"], validator.Describe().Rules[0].Checks.Select(c => c.ToLowerInvariant()));
    }

    [Fact]
    public void Argument_and_type_errors_of_custom_checks_are_definition_errors()
    {
        var registry = new CheckRegistry()
            .Add<int>("Between", a => (g, _) => g.IfOutOfRange(CheckArguments.Parse<int>(a.Min!), 5))
            .Add<int>("Strict", a =>
            {
                var limit = CheckArguments.Parse<int>(a.Value ?? throw new ArgumentException("'Value' is required."));
                return (g, _) => g.IfGreaterThan(limit);
            });

        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                { "rules": [
                  { "property": "Text", "checks": [ "Between" ] },
                  { "property": "Number", "checks": [ { "check": "Strict" }, { "check": "Strict", "value": "x" } ] } ] }
                """),
            Maps.Sample(),
            registry)).Errors;

        Assert.Collection(
            errors,
            e => Assert.Equal("Rules[0].Checks[0]: check 'Between' on 'Text': is a custom check for int, not for string", e),
            e => Assert.Equal("Rules[1].Checks[0]: check 'Strict' on 'Number': 'Value' is required.", e),
            e => Assert.Equal("Rules[1].Checks[1]: check 'Strict' on 'Number': 'x' is not a valid int.", e));
    }

    [Fact]
    public void Checks_added_later_apply_after_an_update()
    {
        var registry = new CheckRegistry();
        var definition = Rules.One("Text", "\"Shouting\"");
        Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(definition, Maps.Sample(), registry));

        var validator = new DynamicValidator<Sample>(Rules.One("Text", "\"Null\""), Maps.Sample(), registry);
        registry.Add<string>("Shouting", (g, a) => g.IfAllUpperCase(a.Message));
        validator.Update(definition);
        Assert.False(validator.Validate(new Sample { Text = "ADA" }).IsValid);
    }

    [Fact]
    public void Parse_converts_with_the_invariant_culture()
    {
        Assert.Equal(1.5m, CheckArguments.Parse<decimal>("1.5"));
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), CheckArguments.Parse<DateTime>("2026-01-02T03:04:05Z"));
        Assert.Equal(TimeSpan.FromHours(36), CheckArguments.Parse<TimeSpan>("1.12:00:00"));
        Assert.Equal(Status.Closed, CheckArguments.Parse<Status>("closed"));
        Assert.Equal(5, CheckArguments.Parse<int?>("5"));
        Assert.Throws<FormatException>(() => CheckArguments.Parse<int>("five"));
        Assert.Throws<FormatException>(() => CheckArguments.Parse<object>("x"));
    }
}
