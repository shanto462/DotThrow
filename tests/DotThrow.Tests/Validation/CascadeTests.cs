using System;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

[CollectionDefinition(nameof(GlobalDefaults), DisableParallelization = true)]
public sealed class GlobalDefaults
{
}

// Rule cascade (one value, many checks), class cascade (many rules) and the global defaults.
[Collection(nameof(GlobalDefaults))]
public sealed class CascadeTests : IDisposable
{
    public void Dispose() => ValidatorDefaults.Reset();

    [Fact]
    public void Default_rule_cascade_stops_at_the_first_failed_check()
    {
        var validator = new Validator<Person>().For(x => x.Name, n => n.IfNullOrEmpty().IfShorterThan(3).IfNotStartsWith("A"));

        Assert.Equal([ErrorCodes.ShorterThan], Names.Codes(validator.Validate(new Person { Name = "x" })));
    }

    [Fact]
    public void Continue_rule_cascade_runs_every_check()
    {
        var validator = new Validator<Person>().For(
            x => x.Name,
            n => n.IfNullOrEmpty().IfShorterThan(3).IfNotStartsWith("A"),
            rule => rule.Cascade(CascadeMode.Continue));

        Assert.Equal([ErrorCodes.ShorterThan, ErrorCodes.NotStartsWith], Names.Codes(validator.Validate(new Person { Name = "x" })));
    }

    [Fact]
    public void Continue_still_reports_null_once()
    {
        var validator = new Validator<Person>().For(
            x => x.Name,
            n => n.IfNullOrEmpty().IfShorterThan(3).IfNotStartsWith("A"),
            rule => rule.Cascade(CascadeMode.Continue));

        Assert.Equal([ErrorCodes.Null], Names.Codes(validator.Validate(new Person())));
    }

    [Fact]
    public void Validator_rule_cascade_applies_to_rules_without_their_own()
    {
        var validator = new Validator<Person> { RuleCascade = CascadeMode.Continue }
            .For(x => x.Name, n => n.IfShorterThan(3).IfNotStartsWith("A"))
            .For(x => x.Email, e => e.IfShorterThan(3).IfNotContains("@"), rule => rule.Cascade(CascadeMode.Stop));

        Assert.Equal(["Name", "Name", "Email"], Names.Of(validator.Validate(new Person { Name = "x", Email = "y" })));
    }

    [Fact]
    public void Continue_on_a_collection_applies_per_item()
    {
        var validator = new Validator<Person>().ForEach(x => x.Tags, t => t.IfShorterThan(3).IfNotStartsWith("A"), rule => rule.Cascade(CascadeMode.Continue));

        Assert.Equal(["Tags[0]", "Tags[0]", "Tags[1]", "Tags[1]"], Names.Of(validator.Validate(new Person { Tags = ["x", "y"] })));
    }

    [Fact]
    public void Class_cascade_stop_ends_at_the_first_failed_rule()
    {
        var validator = new Validator<Person> { ClassCascade = CascadeMode.Stop }
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .For(x => x.Email, e => e.IfNullOrEmpty());

        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person())));
        Assert.Equal(["Email"], Names.Of(validator.Validate(new Person { Name = "Ada" })));
    }

    [Fact]
    public void Run_options_override_the_class_cascade_for_every_validator()
    {
        var address = new Validator<Address>().For(x => x.Street, s => s.IfNullOrEmpty()).For(x => x.City, c => c.IfNullOrEmpty());
        var validator = new Validator<Person>().For(x => x.Address, address).For(x => x.Name, n => n.IfNullOrEmpty());

        var report = validator.Validate(new Person { Address = new Address() }, new ValidationOptions { ClassCascade = CascadeMode.Stop });

        Assert.Equal(["Address.Street"], Names.Of(report));
    }

    [Fact]
    public void Global_defaults_apply_when_the_validator_sets_nothing()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfShorterThan(3).IfNotStartsWith("A"))
            .For(x => x.Email, e => e.IfNullOrEmpty());

        ValidatorDefaults.RuleCascade = CascadeMode.Continue;
        ValidatorDefaults.ClassCascade = CascadeMode.Stop;
        ValidatorDefaults.Severity = Severity.Warning;

        var report = validator.Validate(new Person { Name = "x" });

        Assert.Equal(["Name", "Name"], Names.Of(report));
        Assert.All(report.Failures, f => Assert.Equal(Severity.Warning, f.Severity));
    }

    [Fact]
    public void Name_resolvers_and_failure_hook_apply()
    {
        ValidatorDefaults.PropertyNameResolver = (type, path) => char.ToLowerInvariant(path[0]) + path.Substring(1);
        ValidatorDefaults.DisplayNameResolver = (type, path) => path == "email" ? "E-mail" : null;
        ValidatorDefaults.OnFailureCreated = failure => new ValidationFailure(failure.Name, failure.Message, failure.ErrorCode, failure.Severity, failure.AttemptedValue, "hooked");
        var validator = new Validator<Person>().For(x => x.Email, e => e.IfNullOrEmpty()).For(x => x.Name, n => n.IfNullOrEmpty());

        var report = validator.Validate(new Person());

        Assert.Equal(["email", "name"], Names.Of(report));
        Assert.Equal("'E-mail' must not be null.", report.Failures[0].Message);
        Assert.Equal("'name' must not be null.", report.Failures[1].Message);
        Assert.All(report.Failures, f => Assert.Equal("hooked", f.CustomState));
    }

    [Fact]
    public void Reset_restores_every_default()
    {
        ValidatorDefaults.RuleCascade = CascadeMode.Continue;
        ValidatorDefaults.MaxDepth = 3;
        ValidatorDefaults.Reset();

        Assert.Equal(CascadeMode.Stop, ValidatorDefaults.RuleCascade);
        Assert.Equal(CascadeMode.Continue, ValidatorDefaults.ClassCascade);
        Assert.Equal(256, ValidatorDefaults.MaxDepth);
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidatorDefaults.MaxDepth = 0);
    }
}
