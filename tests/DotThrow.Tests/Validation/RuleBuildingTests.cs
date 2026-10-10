using System;
using System.Collections.Generic;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// For, ForEach, Must, Custom and Include: names, messages and basic behavior.
public sealed class RuleBuildingTests
{
    [Fact]
    public void For_rule_names_the_failure_after_the_property_path_and_messages_use_it()
    {
        var validator = new Validator<Person>().For(x => x.Address!.City, city => city.IfNullOrWhiteSpace());

        var report = validator.Validate(new Person { Address = new Address() });

        var failure = Assert.Single(report.Failures);
        Assert.Equal("Address.City", failure.Name);
        Assert.Equal("'Address.City' must not be null.", failure.Message);
        Assert.Equal(ErrorCodes.Null, failure.ErrorCode);
        Assert.Equal(Severity.Error, failure.Severity);
    }

    [Fact]
    public void Null_forgiving_and_null_conditional_operators_are_dropped_from_names()
    {
        var validator = new Validator<Person>()
            .For(x => x.Address!.City, city => city.IfNullOrEmpty())
            .For(x => x.Address?.Street, street => street.IfNullOrEmpty())
            .For(
                x => x.Address!
                    .PostalCode,
                code => code.IfNullOrEmpty());

        Assert.Equal(["Address.City", "Address.Street", "Address.PostalCode"], Names.Of(validator.Validate(new Person { Address = new Address() })));
    }

    [Fact]
    public void Chain_may_change_the_guard_type_so_nullable_values_can_be_unwrapped()
    {
        var validator = new Validator<Person>().For(x => x.Height, height => height.IfNull().IfNegative());

        Assert.Equal([ErrorCodes.Null], Names.Codes(validator.Validate(new Person())));
        Assert.Equal([ErrorCodes.Negative], Names.Codes(validator.Validate(new Person { Height = -1 })));
        Assert.True(validator.Validate(new Person { Height = 180 }).IsValid);
    }

    [Fact]
    public void Optional_nullable_value_is_checked_only_when_present()
    {
        var validator = new Validator<Person>().For(x => x.Height, height => height.AllowNull().IfNull().IfNegative());

        Assert.True(validator.Validate(new Person()).IsValid);
        Assert.Equal([ErrorCodes.Negative], Names.Codes(validator.Validate(new Person { Height = -1 })));
    }

    [Fact]
    public void Root_aware_chain_compares_with_another_property()
    {
        var validator = new Validator<Person>().For(x => x.End, (end, x) => end.IfBefore(x.Start));

        var report = validator.Validate(new Person { Start = new DateTime(2026, 5, 2), End = new DateTime(2026, 5, 1) });

        var failure = Assert.Single(report.Failures);
        Assert.Equal("End", failure.Name);
        Assert.Equal(ErrorCodes.Before, failure.ErrorCode);
        Assert.Equal(new DateTime(2026, 5, 1), failure.AttemptedValue);
    }

    [Fact]
    public void ForEach_names_items_by_index_and_messages_use_the_collection_name()
    {
        var validator = new Validator<Person>().ForEach(x => x.Tags, tag => tag.IfNullOrWhiteSpace().IfLongerThan(3));

        var report = validator.Validate(new Person { Tags = ["ok", null, "toolong"] });

        Assert.Equal(["Tags[1]", "Tags[2]"], Names.Of(report));
        Assert.Equal(["'Tags' must not be null.", "'Tags' must be at most 3 characters long."], Names.Messages(report));
    }

    [Fact]
    public void ForEach_over_a_lazy_sequence_enumerates_once_and_keeps_indexes()
    {
        var reads = 0;
        IEnumerable<int> Numbers()
        {
            foreach (var n in new[] { 1, -2, 3, -4 })
            {
                reads++;
                yield return n;
            }
        }

        var validator = new Validator<Person>().ForEach(_ => Numbers(), n => n.IfNegative(), rule => rule.OverridePropertyName("Numbers"));

        Assert.Equal(["Numbers[1]", "Numbers[3]"], Names.Of(validator.Validate(new Person())));
        Assert.Equal(4, reads);
    }

    [Fact]
    public void ForEach_root_aware_chain_reads_the_object()
    {
        var validator = new Validator<Order>().ForEach(x => x.Lines, (line, order) => line.If(l => l!.Quantity > order.Total, "Too many."));

        var report = validator.Validate(new Order { Total = 2, Lines = [new Line { Quantity = 1 }, new Line { Quantity = 5 }] });

        Assert.Equal(["Lines[1]"], Names.Of(report));
    }

    [Fact]
    public void Child_validator_prefixes_names_and_keeps_the_child_name_in_messages()
    {
        var validator = new Validator<Person>().For(x => x.Address, new AddressValidator());

        var report = validator.Validate(new Person { Address = new Address { Street = "x", PostalCode = "12345" } });

        var failure = Assert.Single(report.Failures);
        Assert.Equal("Address.City", failure.Name);
        Assert.Equal("'City' must not be null.", failure.Message);
    }

    [Fact]
    public void Null_child_and_null_collection_items_are_skipped()
    {
        var validator = new Validator<Person>()
            .For(x => x.Address, new AddressValidator())
            .ForEach(x => x.Addresses, new AddressValidator());

        Assert.True(validator.Validate(new Person { Address = null, Addresses = [null, Address.Valid()] }).IsValid);
    }

    [Fact]
    public void ForEach_child_validator_names_items_by_index()
    {
        var validator = new Validator<Person>().ForEach(x => x.Addresses, new AddressValidator());

        var report = validator.Validate(new Person { Addresses = [Address.Valid(), new Address { Street = "s", City = "c", PostalCode = "x" }] });

        Assert.Equal(["Addresses[1].PostalCode"], Names.Of(report));
    }

    [Fact]
    public void Nested_child_validators_build_the_full_path()
    {
        var customer = new Validator<Customer>().For(x => x.Address, new AddressValidator());
        var order = new Validator<Order>().For(x => x.Customer, customer);

        var report = order.Validate(new Order { Customer = new Customer { Address = new Address { Street = "s", PostalCode = "12345" } } });

        Assert.Equal(["Customer.Address.City"], Names.Of(report));
    }

    [Fact]
    public void Inline_child_rules_work_like_a_child_validator()
    {
        var validator = new Validator<Person>()
            .For(x => x.Address, address => address.For(a => a.City, city => city.IfNullOrEmpty()))
            .ForEach(x => x.Addresses, item => item.For(a => a.Street, street => street.IfNullOrEmpty()));

        var report = validator.Validate(new Person { Address = new Address(), Addresses = [Address.Valid(), new Address()] });

        Assert.Equal(["Address.City", "Addresses[1].Street"], Names.Of(report));
    }

    [Fact]
    public void Nested_ForEach_through_inline_rules_builds_double_indexes()
    {
        var validator = new Validator<List<List<int>>>()
            .ForEach(x => x, row => row.ForEach(r => r, cell => cell.IfNegative()), rule => rule.OverridePropertyName("Matrix"));

        var report = validator.Validate([[1, 2], [3, -4]]);

        Assert.Equal(["Matrix[1][1]"], Names.Of(report));
    }

    [Fact]
    public void Must_on_the_object_has_an_empty_name_and_a_default_message()
    {
        var validator = new Validator<Person>().Must(x => x.Age > 40);

        var failure = Assert.Single(validator.Validate(Person.Valid()).Failures);

        Assert.Equal(string.Empty, failure.Name);
        Assert.Equal(ErrorCodes.Must, failure.ErrorCode);
        Assert.Equal("The specified condition was not met.", failure.Message);
    }

    [Fact]
    public void Must_on_the_object_takes_its_name_from_WithName()
    {
        var validator = new Validator<Person>().Must(x => x.End > x.Start, configure: rule => rule.WithName("End"));

        var failure = Assert.Single(validator.Validate(new Person()).Failures);

        Assert.Equal("End", failure.Name);
        Assert.Equal("The specified condition was not met for 'End'.", failure.Message);
    }

    [Fact]
    public void Must_on_a_property_gets_the_value_or_the_object_and_the_value()
    {
        var validator = new Validator<Person>()
            .Must(x => x.Name, name => name.Length > 0 && name[0] == 'A', "Must start with A.")
            .Must(x => x.Age, (x, age) => age < x.Height, "Must be smaller than the height.");

        var report = validator.Validate(new Person { Name = "Grace", Age = 50, Height = 40 });

        Assert.Equal(["Name", "Age"], Names.Of(report));
        Assert.Equal("Grace", report.Failures[0].AttemptedValue);
        Assert.Equal(50, report.Failures[1].AttemptedValue);
    }

    [Fact]
    public void Custom_rule_checks_through_the_context_and_names_failures_after_the_path()
    {
        var address = new Validator<Address>().Custom((a, context) =>
        {
            context.Check(a.City, "City").IfNullOrEmpty();
            context.Check(a, x => x.Street).IfNullOrEmpty();
            context.AddFailure("PostalCode", "Bad code.", "BadCode");
            context.AddFailure("Whole address is odd.");
        });
        var validator = new Validator<Person>().For(x => x.Address, address);

        var report = validator.Validate(new Person { Address = new Address() });

        Assert.Equal(["Address.City", "Address.Street", "Address.PostalCode", "Address"], Names.Of(report));
        Assert.Equal(["Null", "Null", "BadCode", ErrorCodes.Invalid], Names.Codes(report));
    }

    [Fact]
    public void Include_adds_the_rules_of_another_validator()
    {
        var shared = new Validator<Person>().For(x => x.Name, name => name.IfNullOrEmpty());
        var validator = new Validator<Person>().Include(shared).For(x => x.Email, email => email.IfNullOrEmpty());

        Assert.Equal(["Name", "Email"], Names.Of(validator.Validate(new Person())));
    }

    [Fact]
    public void Include_accepts_a_validator_of_a_base_type()
    {
        var any = new Validator<object>().Must(_ => false, "Never valid.");
        var validator = new Validator<Person>().Include(any);

        Assert.Equal(["Never valid."], Names.Messages(validator.Validate(new Person())));
    }

    [Fact]
    public void Builders_return_the_same_validator_and_reject_null_arguments()
    {
        var validator = new Validator<Person>();

        Assert.Same(validator, validator.For(x => x.Name, n => n.IfNull()));
        Assert.Same(validator, validator.ForEach(x => x.Tags, t => t.IfNull()));
        Assert.Same(validator, validator.Must(_ => true));
        Assert.Same(validator, validator.Custom((_, _) => { }));
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => validator.For((Func<Person, string?>)null!, n => n.IfNull())).ParamName);
        Assert.Equal("rules", Assert.Throws<ArgumentNullException>(() => validator.For(x => x.Name, (GuardChain<string>)null!)).ParamName);
        Assert.Equal("validator", Assert.Throws<ArgumentNullException>(() => validator.For(x => x.Address, (IValidator<Address>)null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => validator.Must(null!)).ParamName);
    }

    [Fact]
    public void Selector_that_throws_NullReferenceException_gets_a_clear_message()
    {
        var validator = new Validator<Person>().For(x => x.Address!.City!.Length, length => length.IfNegative());

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(new Person()));

        Assert.Contains("x => x.Address!.City!.Length", exception.Message, StringComparison.Ordinal);
        Assert.IsType<NullReferenceException>(exception.InnerException);
    }

    [Fact]
    public void Value_type_objects_can_be_validated()
    {
        var validator = new Validator<int>().For(x => x, x => x.IfNegative()).Must(x => x % 2 == 0, "Even.");

        Assert.True(validator.Validate(4).IsValid);
        var report = validator.Validate(-3);
        Assert.Equal([string.Empty, string.Empty], Names.Of(report));
        Assert.Equal("'value' must not be negative.", report.Failures[0].Message);
    }

    [Fact]
    public void Validating_null_throws_ArgumentNullException()
    {
        var validator = new Validator<Person>();

        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => validator.Validate(null!)).ParamName);
    }

    [Fact]
    public void Validator_is_contravariant()
    {
        IValidator<object> any = new Validator<object>().Must(_ => false, "No.");
        IValidator<Person> person = any;

        Assert.False(person.Validate(new Person()).IsValid);
        Assert.Equal(["Address"], Names.Of(new Validator<Person>().For(x => x.Address, any).Validate(new Person { Address = new Address() })));
    }
}
