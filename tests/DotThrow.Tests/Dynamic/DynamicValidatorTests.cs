using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using DotThrow.Dynamic;
using Xunit;

namespace DotThrow.Tests.Dynamic;

public sealed class DynamicValidatorTests
{
    private static readonly RuleSetDefinition LongerThan5 = Rules.One("Text", """{ "check": "LongerThan", "value": 5 }""");
    private static readonly RuleSetDefinition LongerThan50 = Rules.One("Text", """{ "check": "LongerThan", "value": 50 }""");
    private static readonly RuleSetDefinition Broken = Rules.One("Text", """{ "check": "LongerThan", "value": "many" }""");

    [Fact]
    public void Update_swaps_the_rules_and_keeps_the_old_ones_when_the_new_ones_are_invalid()
    {
        var validator = new DynamicValidator<Sample>(LongerThan5, Maps.Sample());
        var sample = new Sample();
        Assert.Equal("LongerThan", Assert.Single(validator.Validate(sample).Failures).ErrorCode);

        var exception = Assert.Throws<RuleDefinitionException>(() => validator.Update(Broken));
        Assert.Single(exception.Errors);
        Assert.Same(LongerThan5, validator.Definition);
        Assert.Single(validator.Validate(sample).Failures);

        Assert.False(validator.TryUpdate(Broken, out var errors));
        Assert.Single(errors);
        Assert.Single(validator.FindErrors(Broken));
        Assert.Empty(validator.FindErrors(LongerThan50));
        Assert.Single(validator.Validate(sample).Failures);

        validator.Update(LongerThan50);
        Assert.Same(LongerThan50, validator.Definition);
        Assert.True(validator.Validate(sample).IsValid);

        Assert.True(validator.TryUpdate(LongerThan5, out var none));
        Assert.Empty(none);
        Assert.False(validator.Validate(sample).IsValid);
    }

    [Fact]
    public void Changing_the_definition_object_later_has_no_effect_until_update()
    {
        var definition = Rules.One("Text", """{ "check": "LongerThan", "value": 5 }""");
        var validator = new DynamicValidator<Sample>(definition, Maps.Sample());
        definition.Rules[0].Checks[0].Value = "50";
        Assert.False(validator.Validate(new Sample()).IsValid);
        validator.Update(definition);
        Assert.True(validator.Validate(new Sample()).IsValid);
    }

    [Fact]
    public async Task Runs_stay_consistent_while_another_thread_updates()
    {
        var validator = new DynamicValidator<Sample>(LongerThan5, Maps.Sample());
        var token = TestContext.Current.CancellationToken;
        var outcomes = new ConcurrentBag<int>();
        var updater = Task.Run(
            () =>
            {
                for (var i = 0; i < 2_000; i++)
                {
                    validator.Update(i % 2 == 0 ? LongerThan50 : LongerThan5);
                }
            },
            token);
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(
            () =>
            {
                while (!updater.IsCompleted)
                {
                    outcomes.Add(validator.Validate(new Sample()).Failures.Count);
                }
            },
            token)).ToArray();
        await Task.WhenAll([updater, .. readers]);
        Assert.All(outcomes, count => Assert.InRange(count, 0, 1));
    }

    [Fact]
    public void Can_be_included_in_a_code_validator_and_used_as_a_child_validator()
    {
        var dynamic = new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                { "rules": [
                  { "property": "Text", "checks": [ { "check": "LongerThan", "value": 5 } ] },
                  { "property": "Number", "group": "strict", "checks": [ { "check": "GreaterThan", "value": 1 } ] } ] }
                """),
            Maps.Sample());
        var code = new Validator<Sample>()
            .For(x => x.Price, p => p.IfNotInteger())
            .Include(dynamic);

        var report = code.Validate(new Sample());
        Assert.Equal(["Price", "Text"], report.Failures.Select(f => f.Name));
        Assert.Equal(["Price", "Text", "Number"], code.Validate(new Sample(), ValidationGroups.All).Failures.Select(f => f.Name));
        Assert.Equal(["Number"], code.Validate(new Sample(), "strict").Failures.Select(f => f.Name));

        var address = new DynamicValidator<Address>(Rules.One("City", """{ "check": "ShorterThan", "value": 10 }"""), new PropertyMap<Address>().Add(x => x.City));
        var parent = new Validator<Sample>().For(x => x.Address, address);
        var failure = Assert.Single(parent.Validate(new Sample()).Failures);
        Assert.Equal("Address.City", failure.Name);
        Assert.Equal(new Validator<Address>().For(x => x.City, c => c.IfShorterThan(10)).Validate(new Address { City = "London" }).Failures[0].Message, failure.Message);
    }

    [Fact]
    public void Groups_conditions_and_class_cascade_apply()
    {
        var validator = new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                {
                  "classCascade": "Stop",
                  "rules": [
                    { "property": "Text", "when": { "property": "Flag", "equalTo": true }, "unless": { "property": "Status", "equalTo": "Closed" }, "checks": [ { "check": "LongerThan", "value": 3 } ] },
                    { "property": "Number", "groups": [ "a", "b" ], "when": { "property": "MaybeNumber", "isNull": true }, "checks": [ "Positive" ] },
                    { "property": "Price", "group": "a", "unless": { "property": "Text", "notEqualTo": "Ada Lovelace" }, "checks": [ "Positive" ] }
                  ]
                }
                """),
            Maps.Sample());

        Assert.True(validator.Validate(new Sample()).IsValid);
        Assert.Equal(["Text"], validator.Validate(new Sample { Flag = true }).Failures.Select(f => f.Name));
        Assert.True(validator.Validate(new Sample { Flag = true, Status = Status.Closed }).IsValid);
        Assert.Equal(["Price"], validator.Validate(new Sample(), "a").Failures.Select(f => f.Name));
        Assert.Equal(["Number"], validator.Validate(new Sample { MaybeNumber = null }, "b").Failures.Select(f => f.Name));

        // Class cascade Stop: the first failing rule ends the run.
        Assert.Equal(["Number"], validator.Validate(new Sample { MaybeNumber = null }, "a").Failures.Select(f => f.Name));
    }

    [Fact]
    public void Each_and_item_paths_name_failures_with_the_index()
    {
        var validator = new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                { "rules": [
                  { "property": "Tags", "each": true, "allowNull": true, "checks": [ { "check": "LongerThan", "value": 3 } ] },
                  { "property": "lines[].sku", "group": "items", "checks": [ "NullOrEmpty" ] },
                  { "property": "Lines[].Status", "group": "items", "errorCode": "BadStatus", "checks": [ "NotDefined" ] },
                  { "property": "Lines[].Quantity", "group": "items", "each": false, "severity": "Info", "checks": [ { "check": "OutOfRange", "min": 1, "max": 10 } ] } ] }
                """),
            Maps.Sample());

        var sample = new Sample
        {
            Tags = ["math", "ok", null, "history"],
            Lines = [new Line { Sku = "A", Quantity = 1 }, new Line { Quantity = 11, Status = (Status)9 }, null],
        };
        Assert.Equal(["Tags[0]", "Tags[3]"], validator.Validate(sample).Failures.Select(f => f.Name));
        var items = validator.Validate(sample, "items").Failures;
        Assert.Equal(["Lines[1].Sku", "Lines[1].Status", "Lines[1].Quantity"], items.Select(f => f.Name));
        Assert.Equal("BadStatus", items[1].ErrorCode);
        Assert.Equal(Severity.Info, items[2].Severity);
    }

    [Fact]
    public void The_reflection_constructor_reads_nested_paths_ignoring_case()
    {
        var validator = new DynamicValidator<Sample>(RuleSetDefinition.FromJson("""
            { "rules": [
              { "property": "address.city", "checks": [ { "check": "LongerThan", "value": 3 } ] },
              { "property": "ADDRESS.point.latitude", "checks": [ { "check": "OutOfRange", "min": -90, "max": 90 } ] },
              { "property": "Lines[].sku", "checks": [ "NullOrEmpty" ] },
              { "property": "Counts", "checks": [ { "check": "NotContainsKey", "value": "b" } ] } ] }
            """));

        var report = validator.Validate(new Sample { Lines = [new Line()] });
        Assert.Equal(["Address.City", "Address.Point.Latitude", "Lines[0].Sku", "Counts"], report.Failures.Select(f => f.Name));
        Assert.Equal(["LongerThan", "Null", "Null", "NotContainsKey"], report.Failures.Select(f => f.ErrorCode));

        // A null object on the way reads as null.
        var noAddress = validator.Validate(new Sample { Address = null, Lines = null, Counts = new() { ["b"] = 1 } });
        Assert.Equal(["Address.City", "Address.Point.Latitude"], noAddress.Failures.Select(f => f.Name));
        Assert.All(noAddress.Failures, f => Assert.Null(f.AttemptedValue));
    }

    [Fact]
    public void Works_on_the_object_itself_and_on_structs()
    {
        var text = new DynamicValidator<string>(Rules.One(string.Empty, """{ "check": "LongerThan", "value": 3 }"""), new PropertyMap<string>());
        var code = new Validator<string>().For(x => x, g => g.IfLongerThan(3));
        var expected = Assert.Single(code.Validate("Ada Lovelace").Failures);
        var actual = Assert.Single(text.Validate("Ada Lovelace").Failures);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Message, actual.Message);

        var point = new DynamicValidator<GeoPoint>(Rules.One("Latitude", """{ "check": "OutOfRange", "min": -90, "max": 90 }"""));
        Assert.Equal("Latitude", Assert.Single(point.Validate(new GeoPoint { Latitude = 100 }).Failures).Name);
        Assert.True(point.Validate(new GeoPoint { Latitude = 10 }).IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Item_paths_can_nest(bool reflection)
    {
        var definition = RuleSetDefinition.FromJson("""{ "rules": [ { "property": "Lines[].Notes[]", "checks": [ { "check": "LongerThan", "value": 2 } ] } ] }""");
        var validator = reflection ? new DynamicValidator<Sample>(definition) : new DynamicValidator<Sample>(definition, Maps.Sample());
        var sample = new Sample { Lines = [new Line { Notes = ["ok"] }, new Line { Notes = ["fine", "no", "long"] }] };
        Assert.Equal(["Lines[1].Notes[0]", "Lines[1].Notes[2]"], validator.Validate(sample).Failures.Select(f => f.Name));
    }

    [Fact]
    public void Reflection_prefers_the_exact_name_and_the_most_derived_property()
    {
        var validator = new DynamicValidator<DerivedItem>(RuleSetDefinition.FromJson("""
            { "rules": [
              { "property": "value", "checks": [ { "check": "LongerThan", "value": 3 } ] },
              { "property": "Code", "checks": [ "Null" ] },
              { "property": "code", "checks": [ "NotNull" ] } ] }
            """));
        var report = validator.Validate(new DerivedItem { Value = "long text", Code = null, code = "x" });
        Assert.Equal(["Value", "Code", "code"], report.Failures.Select(f => f.Name));
        Assert.Equal(["LongerThan", "Null", "NotNull"], report.Failures.Select(f => f.ErrorCode));
    }

    [Fact]
    public async Task Implements_the_validator_interfaces()
    {
        var validator = new DynamicValidator<Sample>(LongerThan5, Maps.Sample());
        IValidator untyped = validator;
        Assert.True(untyped.CanValidate(typeof(Sample)));
        Assert.False(untyped.CanValidate(typeof(string)));
        Assert.Single(untyped.Validate(new Sample(), null).Failures);
        Assert.Single((await untyped.ValidateAsync(new Sample(), null, TestContext.Current.CancellationToken)).Failures);
        Assert.Throws<ArgumentException>(() => untyped.Validate("text", null));
        Assert.Single((await validator.ValidateAsync(new Sample(), TestContext.Current.CancellationToken)).Failures);
        Assert.Throws<ValidationFailedException>(() => validator.ValidateAndThrow(new Sample()));
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));

        var descriptor = validator.Describe();
        Assert.Equal(typeof(DynamicValidator<Sample>), descriptor.ValidatorType);
        var rule = Assert.Single(descriptor.Rules);
        Assert.Equal("Text", rule.Path);
        Assert.Equal(["LongerThan"], rule.Checks);
    }

    [Fact]
    public void Property_maps_reject_bad_paths()
    {
        Assert.Throws<ArgumentException>(() => new PropertyMap<Sample>().Add(x => x));
        Assert.Throws<ArgumentException>(() => new PropertyMap<Sample>().Add(x => x.Text).Add("text", x => x.Text));
        Assert.Throws<ArgumentException>(() => new PropertyMap<Sample>().Add(" ", x => x.Text));
        Assert.Throws<ArgumentNullException>(() => new PropertyMap<Sample>().Add<string>("Text", null!));
        Assert.Throws<ArgumentNullException>(() => new DynamicValidator<Sample>(LongerThan5, (PropertyMap<Sample>)null!));
        Assert.Equal(["Text", "Address.City"], new PropertyMap<Sample>().Add(x => x.Text).Add(x => x.Address!.City).Paths);
    }
}
