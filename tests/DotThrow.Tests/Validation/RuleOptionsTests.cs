using System;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// The configure callback: names, messages, codes, severity, state, conditions, collections.
public sealed class RuleOptionsTests
{
    [Fact]
    public void WithName_changes_the_message_name_but_not_the_failure_name()
    {
        var validator = new Validator<Person>().For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.WithName("E-mail"));

        var failure = Assert.Single(validator.Validate(new Person()).Failures);

        Assert.Equal("Email", failure.Name);
        Assert.Equal("'E-mail' must not be null.", failure.Message);
    }

    [Fact]
    public void WithName_from_the_object_runs_per_object()
    {
        var validator = new Validator<Person>().For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.WithName(x => $"Email of {x.Name}"));

        Assert.Equal("'Email of Ada Lovelace' must not be null.", Assert.Single(validator.Validate(new Person { Name = "Ada Lovelace" }).Failures).Message);
    }

    [Fact]
    public void OverridePropertyName_changes_both_names_unless_WithName_is_set()
    {
        var validator = new Validator<Person>()
            .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.OverridePropertyName("mail"))
            .For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.WithName("Full name").OverridePropertyName("fullName"))
            .For(x => x.Address, new AddressValidator(), rule => rule.OverridePropertyName("home"));

        var report = validator.Validate(new Person { Address = new Address { Street = "s", PostalCode = "12345" } });

        Assert.Equal(["mail", "fullName", "home.City"], Names.Of(report));
        Assert.Equal("'mail' must not be null.", report.Failures[0].Message);
        Assert.Equal("'Full name' must not be null.", report.Failures[1].Message);
    }

    [Fact]
    public void WithMessage_replaces_every_message_and_fills_placeholders()
    {
        var validator = new Validator<Person>()
            .For(x => x.Age, a => a.IfNegative(), rule => rule.WithName("Years").WithMessage("{PropertyName} at {PropertyPath} was {PropertyValue:N1}, {Unknown} stays."))
            .ForEach(x => x.Tags, t => t.IfNullOrEmpty(), rule => rule.WithMessage("Tag {CollectionIndex} is required."));

        var report = validator.Validate(new Person { Age = -2, Tags = ["a", ""] });

        Assert.Equal("Years at Age was -2.0, {Unknown} stays.", report.Failures[0].Message);
        Assert.Equal("Tag 1 is required.", report.Failures[1].Message);
    }

    [Fact]
    public void WithMessage_from_the_object_and_the_value()
    {
        var validator = new Validator<Person>()
            .For(x => x.Age, a => a.IfNegative(), rule => rule.WithMessage((x, age) => $"{x.Name} cannot be {age}."))
            .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.WithMessage(x => $"{x.Name} needs an email."));

        var report = validator.Validate(new Person { Name = "Ada", Age = -1 });

        Assert.Equal(["Ada cannot be -1.", "Ada needs an email."], Names.Messages(report));
    }

    [Fact]
    public void WithErrorCode_WithSeverity_and_WithState_apply_to_failures()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.WithErrorCode("NameRequired").WithSeverity(Severity.Warning).WithState(x => x.Age))
            .For(x => x.Age, a => a.IfNegative(), rule => rule.WithSeverity((x, age) => age < -10 ? Severity.Error : Severity.Info).WithState((x, age) => age * 2));

        var report = validator.Validate(new Person { Age = -1 });

        Assert.Equal("NameRequired", report.Failures[0].ErrorCode);
        Assert.Equal(Severity.Warning, report.Failures[0].Severity);
        Assert.Equal(-1, report.Failures[0].CustomState);
        Assert.Equal(Severity.Info, report.Failures[1].Severity);
        Assert.Equal(-2, report.Failures[1].CustomState);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void When_and_Unless_run_the_rule_only_when_the_condition_allows()
    {
        var calls = 0;
        var validator = new Validator<Person>()
            .For(x => x.VatNumber, v => v.IfNullOrEmpty(), rule => rule.When(x => x.IsCompany))
            .For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.Unless(x => x.IsCompany))
            .For(x => x.Email, e => { calls++; e.IfNullOrEmpty(); }, rule => rule.When((x, context) => context.Items.ContainsKey("checkEmail")));

        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person())));
        Assert.Equal(["VatNumber"], Names.Of(validator.Validate(new Person { IsCompany = true })));
        Assert.Equal(0, calls);

        var options = new ValidationOptions();
        options.Items["checkEmail"] = true;
        Assert.Equal(["Name", "Email"], Names.Of(validator.Validate(new Person(), options)));
    }

    [Fact]
    public void Several_conditions_must_all_pass()
    {
        var validator = new Validator<Person>().For(x => x.VatNumber, v => v.IfNullOrEmpty(), rule => rule.When(x => x.IsCompany).Unless(x => x.Age > 100));

        Assert.False(validator.Validate(new Person { IsCompany = true }).IsValid);
        Assert.True(validator.Validate(new Person { IsCompany = true, Age = 101 }).IsValid);
    }

    [Fact]
    public void Conditions_skip_the_selector_so_null_paths_are_safe()
    {
        var validator = new Validator<Person>().For(x => x.Address!.City, c => c.IfNullOrEmpty(), rule => rule.When(x => x.Address is not null));

        Assert.True(validator.Validate(new Person()).IsValid);
    }

    [Fact]
    public void Where_filters_items_and_keeps_original_indexes()
    {
        var validator = new Validator<Order>()
            .ForEach(x => x.Lines, line => line.If(l => l!.Quantity < 1, "Quantity must be positive."), rule => rule.Where(l => l!.Active))
            .ForEach(x => x.Lines, new LineValidator(), rule => rule.Where(l => l!.Active).OverridePropertyName("Items"));

        var order = new Order
        {
            Lines = [new Line { Sku = "a", Quantity = 1 }, new Line { Sku = "b", Quantity = 0, Active = false }, new Line { Sku = "c", Quantity = 0 }],
        };

        Assert.Equal(["Lines[2]", "Items[2].Quantity"], Names.Of(validator.Validate(order)));
    }

    [Fact]
    public void WithIndexer_replaces_the_index_text()
    {
        var validator = new Validator<Order>()
            .ForEach(x => x.Lines, new LineValidator(), rule => rule.WithIndexer((line, i) => $"[{line!.Sku}]"))
            .ForEach(x => x.Lines, line => line.If(l => l!.Quantity == 0, "Zero."), rule => rule.WithIndexer((line, i) => $"#{i}"));

        var report = validator.Validate(new Order { Lines = [new Line { Sku = "SKU-1", Quantity = 0 }] });

        Assert.Equal(["Lines[SKU-1].Quantity", "Lines#0"], Names.Of(report));
    }

    [Fact]
    public void DependentRules_run_only_when_the_parent_rule_passes()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.DependentRules(v => v
                .For(x => x.Email, e => e.IfNullOrEmpty())
                .Must(x => x.Age > 0, "Age must be positive.")));

        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person())));
        Assert.Equal(["Email", string.Empty], Names.Of(validator.Validate(new Person { Name = "Ada" })));
        Assert.Single(validator.Describe().Rules);
    }

    [Fact]
    public void Configure_runs_before_the_rule_is_published()
    {
        var validator = new Validator<Person>();
        validator.For(x => x.Name, n => n.IfNullOrEmpty(), rule =>
        {
            Assert.Empty(validator.Describe().Rules);
            rule.WithErrorCode("Late");
        });

        Assert.Equal("Late", Assert.Single(validator.Validate(new Person()).Failures).ErrorCode);
    }

    [Fact]
    public void Options_reject_bad_arguments()
    {
        var validator = new Validator<Person>();

        Assert.Throws<ArgumentNullException>(() => validator.For(x => x.Name, n => n.IfNull(), rule => rule.WithName((string)null!)));
        Assert.Throws<ArgumentException>(() => validator.For(x => x.Name, n => n.IfNull(), rule => rule.InGroup()));
        Assert.Throws<ArgumentException>(() => validator.For(x => x.Name, n => n.IfNull(), rule => rule.InGroup(" ")));
        Assert.DoesNotContain(validator.Describe().Rules, r => r.Path == "Name");
    }
}
