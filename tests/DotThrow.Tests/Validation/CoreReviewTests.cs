using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Dynamic;
using DotThrow.Tests.Validation.Models;
using Xunit;
#if NET8_0_OR_GREATER
using DotThrow.Tests.Support;
using FluentValidation;
#endif

namespace DotThrow.Tests.Validation;

// Findings of the blind review of the core library (selector name cache, rule messages, async
// descriptors, shared options, depth), one or more tests each.
[Collection(nameof(GlobalDefaults))]
public sealed class CoreReviewTests : IDisposable
{
    public void Dispose() => ValidatorDefaults.Reset();

    private sealed class Form
    {
        public string? Name { get; set; }

        public string? Email { get; set; }
    }

    // 1. report.Check(root, selector) parses the selector text once.
    [Fact]
    public void Report_check_with_a_selector_names_the_value_and_keeps_the_name_hook()
    {
        var form = new Form { Name = "", Email = "x" };
        for (var i = 0; i < 3; i++)
        {
            var report = new ValidationReport();
            report.Check(form, f => f.Name).IfNullOrEmpty();
            report.Check(form, f => f.Email).IfShorterThan(3);
            Assert.Equal(["Name", "Email"], Names.Of(report));
        }

        ValidatorDefaults.PropertyNameResolver = (_, path) => path.ToUpperInvariant();
        var resolved = new ValidationReport();
        resolved.Check(form, f => f.Name).IfNullOrEmpty();
        Assert.Equal(["NAME"], Names.Of(resolved));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Report_check_with_a_selector_allocates_nothing_after_the_first_call()
    {
        Allocations.SkipUnlessOptimized();
        var form = new Form { Name = "Ada Lovelace" };
        var report = new ValidationReport();
        Allocations.AssertAtMost(() => report.Check(form, f => f.Name).IfNullOrEmpty(), iterations: 1000);
        Assert.True(report.IsValid);
    }
#endif

    // 2. A rule message (WithMessage) gets the check's arguments and the check formatting.
    [Fact]
    public void Rule_messages_fill_check_placeholders_with_the_check_formatting()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfLongerThan(5), rule => rule.WithMessage("{PropertyName} has {TotalLength} characters, at most {MaxLength}: {PropertyValue}."))
            .For(x => x.Age, a => a.IfGreaterThan(130), rule => rule.WithMessage("{PropertyName} {PropertyValue} is over {ComparisonValue}."))
            .For(x => x.Start, s => s.IfAfter(new DateTime(2026, 1, 1)), rule => rule.WithMessage("{PropertyName} {PropertyValue} is after {ComparisonValue}."))
            .For(x => x.Email, e => e.IfNull(), rule => rule.WithMessage("{PropertyName} is {PropertyValue}."))
            .ForEach(x => x.Tags, t => t.IfLongerThan(2), rule => rule.WithMessage("Tag {CollectionIndex} is over {MaxLength}."));

        var person = new Person { Name = "Ada Lovelace", Age = 140, Start = new DateTime(2026, 5, 2, 14, 30, 0), Tags = ["ok", "long"] };

        Assert.Equal(
            [
                "Name has 12 characters, at most 5: 'Ada Lovelace'.",
                "Age 140 is over 130.",
                "Start 2026-05-02 14:30:00 is after 2026-01-01.",
                "Email is null.",
                "Tag 1 is over 2.",
            ],
            Names.Messages(validator.Validate(person)));
    }

    [Fact]
    public void Rule_messages_without_check_arguments_still_work_for_must_custom_and_fail()
    {
        var validator = new Validator<Person>()
            .Must(x => x.Age, age => age > 0, "{PropertyName} {PropertyValue} must be positive.")
            .For(x => x.Name, n => n.Fail("Custom", "x"), rule => rule.WithMessage("{PropertyName} failed: {MaxLength}."));

        Assert.Equal(["Age -1 must be positive.", "Name failed: {MaxLength}."], Names.Messages(validator.Validate(new Person { Name = "Ada", Age = -1 })));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Rule_message_check_placeholders_match_FluentValidation()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Name).MaximumLength(5).WithMessage("{PropertyName} must be at most {MaxLength} long.");
        fv.RuleFor(x => x.Age).LessThanOrEqualTo(130).WithMessage("{PropertyName} must be at most {ComparisonValue}.");
        fv.RuleFor(x => x.Height).InclusiveBetween(50, 250).WithMessage("{PropertyName} must be from {From} to {To}.");
        var dt = new Validator<Person>()
            .For(x => x.Name, n => n.IfLongerThan(5), rule => rule.WithMessage("{PropertyName} must be at most {MaxLength} long."))
            .For(x => x.Age, a => a.IfGreaterThan(130), rule => rule.WithMessage("{PropertyName} must be at most {ComparisonValue}."))
            .For(x => x.Height, h => h.AllowNull().IfNull().IfOutOfRange(50, 250), rule => rule.WithMessage("{PropertyName} must be from {From} to {To}."));
        var person = new Person { Name = "Ada Lovelace", Age = 140, Height = 300 };

        Assert.Equal(fv.Validate(person).Errors.Select(e => e.ErrorMessage), Names.Messages(dt.Validate(person)));
    }
#endif

    // 5. HasAsyncRules looks into child, included and polymorphic validators.
    [Fact]
    public void Has_async_rules_looks_into_children_includes_and_providers()
    {
        var asyncAddress = new Validator<Address>().MustAsync((_, _) => Task.FromResult(true));
        var syncAddress = new AddressValidator();

        Assert.True(new Validator<Person>().For(x => x.Address, asyncAddress).Describe().HasAsyncRules);
        Assert.False(new Validator<Person>().For(x => x.Address, syncAddress).Describe().HasAsyncRules);
        Assert.True(new Validator<Person>().ForEach(x => x.Addresses, asyncAddress).Describe().HasAsyncRules);
        Assert.True(new Validator<Person>().Include(new Validator<Person>().MustAsync((_, _) => Task.FromResult(true))).Describe().HasAsyncRules);
        Assert.True(new Validator<Person>().Include(_ => syncAddressPerson()).Describe().HasAsyncRules);
        Assert.True(new Validator<Order>().For(x => x.Payment, new PolymorphicValidator<Payment>().Add(new Validator<CardPayment>().MustAsync((_, _) => Task.FromResult(true)))).Describe().HasAsyncRules);
        Assert.False(new Validator<Order>().For(x => x.Payment, new PolymorphicValidator<Payment>().Add(new Validator<CardPayment>())).Describe().HasAsyncRules);

        var dynamic = new DynamicValidator<Person>(RuleSetDefinition.FromJson("""{ "rules": [ { "property": "Name", "checks": [ "Null" ] } ] }"""), new PropertyMap<Person>().Add(x => x.Name));
        Assert.False(new Validator<Person>().Include(dynamic).Describe().HasAsyncRules);
        Assert.False(dynamic.Describe().HasAsyncRules);

        // A cycle ends.
        var node = new Validator<TreeNode>();
        node.For(x => x.Parent, node).ForEach(x => x.Children, node);
        Assert.False(node.Describe().HasAsyncRules);
        node.MustAsync((_, _) => Task.FromResult(true));
        Assert.True(new Validator<TreeNode>().For(x => x.Parent, node).Describe().HasAsyncRules);

        static Validator<Person> syncAddressPerson() => new();
    }

    [Fact]
    public void A_parent_with_an_async_child_reports_it_and_runs_with_ValidateAsync()
    {
        var parent = new Validator<Person>().For(x => x.Address, new Validator<Address>().MustAsync((a, _) => Task.FromResult(a.City is not null)));

        Assert.True(parent.Describe().HasAsyncRules);
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => parent.Validate(new Person { Address = new Address() }));
    }

    // 6. One options object can be shared by runs on several threads.
    [Fact]
    public async Task Shared_options_keep_items_per_run_on_many_threads()
    {
        var mixed = 0;
        var validator = new Validator<Person>().Custom((person, context) =>
        {
            context.Items["name"] = person.Name;
            Thread.Yield();
            if (!Equals(context.Items["name"], person.Name) || !Equals(context.Items["shared"], "yes"))
            {
                Interlocked.Increment(ref mixed);
            }
        });
        var options = new ValidationOptions();
        options.Items["shared"] = "yes";

        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(
            () =>
            {
                for (var i = 0; i < 500; i++)
                {
                    validator.Validate(new Person { Name = t + "-" + i }, options);
                }
            },
            TestContext.Current.CancellationToken)));

        Assert.Equal(0, mixed);
        Assert.Equal(["shared"], options.Items.Keys);
    }

    [Fact]
    public void Report_failures_are_read_only()
    {
        var report = new ValidationReport().Add("Name", "Wrong.");

        Assert.IsNotType<List<ValidationFailure>>(report.Failures, exactMatch: false);
        Assert.Throws<NotSupportedException>(() => ((IList<ValidationFailure>)report.Failures).Add(new ValidationFailure("x", "y", "z")));
        Assert.Throws<NotSupportedException>(() => ((IList<ValidationFailure>)report.Failures).Clear());
        Assert.Single(report.Failures);
        report.Add("Email", "Wrong too.");
        Assert.Equal(["Name", "Email"], Names.Of(report));
    }

    // 7. Deep but valid data runs; the stack check, not the depth limit, protects against overflow.
    [Fact]
    public void A_long_recursive_list_validates_with_the_defaults()
    {
        var node = new Validator<TreeNode>();
        node.For(x => x.Name, n => n.IfNullOrEmpty()).For(x => x.Parent, node);

        Assert.Equal(10_000, ValidatorDefaults.MaxDepth);
        Assert.True(node.Validate(Chain(300, "n")).IsValid);

        ValidationReport? deep = null;
        var thread = new Thread(() => deep = node.Validate(Chain(3_000, string.Empty)), maxStackSize: 64 * 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.Equal(3_000, deep!.Failures.Count);
    }

    [Fact]
    public void A_cycle_still_stops_with_a_clear_exception()
    {
        var node = new Validator<TreeNode>();
        node.For(x => x.Parent, node);
        var loop = new TreeNode { Name = "a" };
        loop.Parent = loop;

        var exception = Assert.Throws<InvalidOperationException>(() => node.Validate(loop));
        Assert.True(
            exception.Message.Contains("deep", StringComparison.Ordinal) || exception.Message.Contains("stack space", StringComparison.Ordinal),
            exception.Message);
    }

    private static TreeNode Chain(int length, string name)
    {
        var first = new TreeNode { Name = name };
        var current = first;
        for (var i = 1; i < length; i++)
        {
            current.Parent = new TreeNode { Name = name };
            current = current.Parent;
        }

        return first;
    }
}
