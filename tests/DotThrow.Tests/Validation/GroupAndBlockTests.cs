using System;
using System.Linq;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// Groups (FluentValidation rule sets), validator-level When/Unless blocks and Group blocks.
public sealed class GroupAndBlockTests
{
    private static Validator<Person> Grouped() => new Validator<Person>()
        .For(x => x.Name, n => n.IfNullOrEmpty())
        .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.InGroup("contact"))
        .For(x => x.VatNumber, v => v.IfNullOrEmpty(), rule => rule.InGroup("billing", ValidationGroups.Default))
        .Group("admin", v => v.For(x => x.Age, a => a.IfNegativeOrZero()));

    [Fact]
    public void Default_run_runs_rules_without_groups_and_rules_in_the_default_group()
    {
        var report = Grouped().Validate(new Person());

        Assert.Equal(["Name", "VatNumber"], Names.Of(report));
        Assert.Equal([ValidationGroups.Default], report.GroupsExecuted);
    }

    [Fact]
    public void Named_groups_run_only_their_rules_and_names_ignore_case()
    {
        var report = Grouped().Validate(new Person(), "CONTACT", "Admin");

        Assert.Equal(["Email", "Age"], Names.Of(report));
        Assert.Equal(["contact", "admin"], report.GroupsExecuted);
    }

    [Fact]
    public void Default_group_can_be_asked_for_with_other_groups()
    {
        var report = Grouped().Validate(new Person(), ValidationGroups.Default, "billing");

        Assert.Equal(["Name", "VatNumber"], Names.Of(report));
        Assert.Contains(ValidationGroups.Default, report.GroupsExecuted);
    }

    [Fact]
    public void All_group_runs_every_rule()
    {
        var report = Grouped().Validate(new Person(), ValidationGroups.All);

        Assert.Equal(["Name", "Email", "VatNumber", "Age"], Names.Of(report));
        Assert.Equal(4, report.GroupsExecuted.Count);
    }

    [Fact]
    public void Unknown_group_runs_nothing_and_reports_no_executed_group()
    {
        var report = Grouped().Validate(new Person(), "nothing");

        Assert.True(report.IsValid);
        Assert.Empty(report.GroupsExecuted);
    }

    [Fact]
    public void Child_validator_runs_with_the_groups_of_the_run_like_FluentValidation()
    {
        var address = new Validator<Address>()
            .For(x => x.City, c => c.IfNullOrEmpty())
            .For(x => x.Street, s => s.IfNullOrEmpty(), rule => rule.InGroup("shipping"));
        var validator = new Validator<Person>().For(x => x.Address, address, rule => rule.InGroup("shipping"));

        Assert.True(validator.Validate(new Person { Address = new Address() }).IsValid);
        Assert.Equal(["Address.Street"], Names.Of(validator.Validate(new Person { Address = new Address() }, "shipping")));
    }

    [Fact]
    public void WithChildGroups_runs_other_groups_of_the_child()
    {
        var address = new Validator<Address>()
            .For(x => x.City, c => c.IfNullOrEmpty())
            .For(x => x.Street, s => s.IfNullOrEmpty(), rule => rule.InGroup("shipping"));
        var validator = new Validator<Person>()
            .For(x => x.Address, address, rule => rule.InGroup("shipping").WithChildGroups(ValidationGroups.Default, "shipping"));

        Assert.Equal(["Address.City", "Address.Street"], Names.Of(validator.Validate(new Person { Address = new Address() }, "shipping")));
    }

    [Fact]
    public void Inline_child_rules_and_dependent_rules_take_the_groups_of_their_parent()
    {
        var validator = new Validator<Person>()
            .For(x => x.Address, a => a.For(x => x.City, c => c.IfNullOrEmpty()), rule => rule.InGroup("shipping"))
            .For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.InGroup("profile").DependentRules(v => v.For(x => x.Email, e => e.IfNullOrEmpty())));

        Assert.True(validator.Validate(new Person { Address = new Address(), Name = "Ada" }).IsValid);
        Assert.Equal(["Address.City"], Names.Of(validator.Validate(new Person { Address = new Address(), Name = "Ada" }, "shipping")));
        Assert.Equal(["Email"], Names.Of(validator.Validate(new Person { Name = "Ada" }, "profile")));
    }

    [Fact]
    public void Include_without_group_runs_in_every_run_and_filters_its_own_rules()
    {
        var shared = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.InGroup("contact"));
        var validator = new Validator<Person>().Include(shared);

        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person())));
        Assert.Equal(["Email"], Names.Of(validator.Validate(new Person(), "contact")));
    }

    [Fact]
    public void Include_in_a_group_runs_only_for_that_group()
    {
        var shared = new Validator<Person>().For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.InGroup("extra"));
        var validator = new Validator<Person>().Include(shared, rule => rule.InGroup("extra"));

        Assert.True(validator.Validate(new Person()).IsValid);
        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person(), "extra")));
    }

    [Fact]
    public void Group_block_applies_to_every_rule_added_inside_unless_a_rule_sets_its_own()
    {
        var validator = new Validator<Person>().Group(["a", "b"], v => v
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.InGroup("c")));

        Assert.True(validator.Validate(new Person()).IsValid);
        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person(), "b")));
        Assert.Equal(["Email"], Names.Of(validator.Validate(new Person(), "c")));
    }

    [Fact]
    public void When_block_with_otherwise_splits_the_rules()
    {
        var calls = 0;
        var validator = new Validator<Person>().When(
            x => { calls++; return x.IsCompany; },
            v => v.For(x => x.VatNumber, n => n.IfNullOrEmpty()).For(x => x.Name, n => n.IfNullOrEmpty()),
            otherwise: v => v.For(x => x.Email, e => e.IfNullOrEmpty()));

        Assert.Equal(["VatNumber", "Name"], Names.Of(validator.Validate(new Person { IsCompany = true })));
        Assert.Equal(1, calls);
        Assert.Equal(["Email"], Names.Of(validator.Validate(new Person())));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Unless_block_and_nested_blocks_combine()
    {
        var validator = new Validator<Person>().Unless(
            x => x.Age > 100,
            v => v.When(x => x.IsCompany, inner => inner.For(x => x.VatNumber, n => n.IfNullOrEmpty())));

        Assert.False(validator.Validate(new Person { IsCompany = true }).IsValid);
        Assert.True(validator.Validate(new Person { IsCompany = true, Age = 101 }).IsValid);
        Assert.True(validator.Validate(new Person()).IsValid);
    }

    [Fact]
    public void Block_conditions_can_read_the_context()
    {
        var validator = new Validator<Person>().When((x, context) => context.Groups.Contains("strict"), v => v.For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.InGroup("strict")));

        Assert.False(validator.Validate(new Person(), "strict").IsValid);
    }

    [Fact]
    public void Rules_added_from_other_threads_do_not_join_an_open_block()
    {
        var validator = new Validator<Person>();
        validator.When(x => x.IsCompany, v =>
        {
            var thread = new System.Threading.Thread(() => validator.For(x => x.Email, e => e.IfNullOrEmpty()));
            thread.Start();
            Assert.False(thread.Join(200));
            v.For(x => x.VatNumber, n => n.IfNullOrEmpty());
        });

        SpinUntil(() => validator.Describe().Rules.Count == 2);
        Assert.Equal(["Email"], Names.Of(validator.Validate(new Person())));
    }

    private static void SpinUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            System.Threading.Thread.Sleep(5);
        }

        Assert.True(condition());
    }
}
