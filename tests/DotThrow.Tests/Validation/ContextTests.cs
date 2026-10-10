using System.Collections.Generic;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// ValidationContext: items (root context data), paths, PreValidate, failures added by hand.
public sealed class ContextTests
{
    [Fact]
    public void Items_flow_to_child_validators_and_a_shared_object_carries_data_back()
    {
        var address = new Validator<Address>().Custom((a, context) =>
        {
            if (context.Items.TryGetValue("country", out var country) && (string?)country == "NL")
            {
                context.Check(a.PostalCode, "PostalCode").IfNotMatches("^[0-9]{4}[A-Z]{2}$");
            }

            context.Items["seen"] = context.PropertyPath;
            ((List<string>)context.Items["log"]!).Add(context.PropertyPath);
        });
        var validator = new Validator<Person>().For(x => x.Address, address);
        var log = new List<string>();
        var options = new ValidationOptions();
        options.Items["country"] = "NL";
        options.Items["log"] = log;

        var report = validator.Validate(new Person { Address = Address.Valid() }, options);

        Assert.Equal(["Address.PostalCode"], Names.Of(report));

        // Each run copies Items, so keys added by rules stay in the run; a shared object carries data out.
        Assert.False(options.Items.ContainsKey("seen"));
        Assert.Equal(["Address"], log);
    }

    [Fact]
    public void Context_knows_the_path_parent_groups_and_mode()
    {
        var seen = new List<string>();
        var line = new Validator<Line>().Custom((l, context) =>
            seen.Add($"{context.PropertyPath}|{context.Parent is not null}|{string.Join(",", context.Groups)}|{context.IsAsync}"));
        var validator = new Validator<Order>().ForEach(x => x.Lines, line, rule => rule.InGroup("g").WithChildGroups(ValidationGroups.Default, "g"));

        validator.Validate(new Order { Lines = [new Line(), new Line()] }, "g");

        Assert.Equal(["Lines[0]|True|default,g|False", "Lines[1]|True|default,g|False"], seen);
    }

    [Fact]
    public void PreValidate_can_handle_a_null_object_and_stop_the_run()
    {
        var validator = new NullTolerantValidator();

        var report = validator.Validate(null!);

        Assert.Equal(["Please send a person."], Names.Messages(report));
        Assert.Equal([string.Empty], Names.Of(report));
    }

    [Fact]
    public void PreValidate_runs_for_child_validators_and_its_failures_get_the_path()
    {
        var validator = new Validator<Order>().For(x => x.Customer, new CustomerPreValidator());

        var report = validator.Validate(new Order { Customer = new Customer() });

        Assert.Equal(["Customer.Name"], Names.Of(report));
    }

    [Fact]
    public void AddFailure_with_a_full_failure_keeps_it_as_is()
    {
        var failure = new ValidationFailure("Exact", "As is.", "Code", Severity.Info, 1, "state");
        var validator = new Validator<Order>().For(x => x.Customer, new Validator<Customer>().Custom((_, context) => context.AddFailure(failure)));

        var report = validator.Validate(new Order { Customer = new Customer() });

        Assert.Same(failure, Assert.Single(report.Failures));
    }

    [Fact]
    public void Custom_rule_options_apply_to_failures_added_through_the_context()
    {
        var validator = new Validator<Person>().Custom(
            (x, context) =>
            {
                context.Check(x.Name, "Name").IfNullOrEmpty();
                context.AddFailure("Email", "Email is missing.");
            },
            rule => rule.WithSeverity(Severity.Warning).WithErrorCode("Profile"));

        var report = validator.Validate(new Person());

        Assert.All(report.Failures, f => Assert.Equal(Severity.Warning, f.Severity));
        Assert.All(report.Failures, f => Assert.Equal("Profile", f.ErrorCode));
    }

    private sealed class NullTolerantValidator : Validator<Person>
    {
        public NullTolerantValidator() => For(x => x.Name, n => n.IfNullOrEmpty());

        protected override bool PreValidate(ValidationContext<Person> context)
        {
            if (context.Instance is null)
            {
                context.AddFailure("Please send a person.");
                return false;
            }

            return true;
        }
    }

    private sealed class CustomerPreValidator : Validator<Customer>
    {
        public CustomerPreValidator() => For(x => x.Address, a => a.IfNull());

        protected override bool PreValidate(ValidationContext<Customer> context)
        {
            context.Check(context.Instance.Name, "Name").IfNullOrEmpty();
            return false;
        }
    }
}
