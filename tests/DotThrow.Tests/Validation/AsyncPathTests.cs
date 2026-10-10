using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Support;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// Child validators, collections and conditions on the async paths: hand-written validators
// that finish at once or later, providers, lazy collections, async item filters, async
// conditions and dependent rules.
public sealed class AsyncPathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hand_written_child_that_finishes_at_once_or_later_is_awaited()
    {
        var validator = new Validator<Person>()
            .For(x => x.Address, new SlowStubValidator<Address>("Now", delay: false))
            .For(x => x.Address, new SlowStubValidator<Address>("Later", delay: true));

        var report = await validator.ValidateAsync(new Person { Address = new Address() }, Token);

        Assert.Equal(["Address.Now", "Address.Later"], Names.Of(report));
    }

    [Fact]
    public async Task Hand_written_collection_children_are_awaited_in_order()
    {
        var validator = new Validator<Person>().ForEach(x => x.Addresses, new SlowStubValidator<Address>("City", delay: true));

        var report = await validator.ValidateAsync(new Person { Addresses = [new Address(), null, new Address()] }, Token);

        Assert.Equal(["Addresses[0].City", "Addresses[2].City"], Names.Of(report));
    }

    [Fact]
    public async Task Child_provider_runs_in_async_runs_and_null_skips()
    {
        var validator = new Validator<Person>().For(x => x.Address, x => x.IsCompany ? new AddressValidator() : null);

        var company = await validator.ValidateAsync(new Person { IsCompany = true, Address = new Address() }, Token);
        var person = await validator.ValidateAsync(new Person { Address = new Address() }, Token);

        Assert.Equal(["Address.Street", "Address.City", "Address.PostalCode"], Names.Of(company));
        Assert.True(person.IsValid);
    }

    [Fact]
    public void Child_validators_over_a_lazy_collection_keep_the_indexes()
    {
        var validator = new Validator<Holder>().ForEach(x => x.Lazy, new LineValidator());

        var report = validator.Validate(new Holder { Lazy = Sequences.Of<Line?>(new Line { Sku = "A", Quantity = 1 }, null, new Line()) });

        Assert.Equal(["Lazy[2].Sku", "Lazy[2].Quantity"], Names.Of(report));
    }

    [Fact]
    public async Task Async_child_validators_over_a_lazy_collection_keep_the_indexes()
    {
        var slow = new Validator<Line>().MustAsync(x => x.Sku, async (sku, ct) => { await Task.Yield(); return sku is not null; }, "Sku is required.");
        var validator = new Validator<Holder>().ForEach(x => x.Lazy, slow);
        var quick = new Validator<Holder>().ForEach(x => x.Lazy, new LineValidator());
        var items = Sequences.Of<Line?>(new Line { Sku = "A" }, new Line(), null, new Line());

        var report = await validator.ValidateAsync(new Holder { Lazy = items }, Token);
        var quickReport = await quick.ValidateAsync(new Holder { Lazy = Sequences.Of<Line?>(new Line()) }, Token);

        Assert.Equal(["Lazy[1].Sku", "Lazy[3].Sku"], Names.Of(report));
        Assert.Equal(["Lazy[0].Sku", "Lazy[0].Quantity"], Names.Of(quickReport));
    }

    [Fact]
    public async Task Async_item_filter_on_a_chain_rule()
    {
        var validator = new Validator<Person>().ForEach(
            x => x.Tags,
            tag => tag.IfShorterThan(3),
            rule => rule.Where(t => t != "skip").WhereAsync(async (t, _) => { await Task.Yield(); return t != "also-skip"; }));
        var none = new Validator<Holder2>().ForEach(x => x.Tags, tag => tag.IfNull(), rule => rule.WhereAsync((_, _) => Task.FromResult(true)));

        var report = await validator.ValidateAsync(new Person { Tags = ["ab", "skip", "also-skip", "c", "long"] }, Token);
        var nullTags = await none.ValidateAsync(new Holder2(), Token);

        Assert.Equal(["Tags[0]", "Tags[3]"], Names.Of(report));
        Assert.True(nullTags.IsValid);
    }

    [Fact]
    public void Async_item_filters_need_an_async_run()
    {
        var chain = new Validator<Person>().ForEach(x => x.Tags, tag => tag.IfNull(), rule => rule.WhereAsync((_, _) => Task.FromResult(true)));
        var child = new Validator<Person>().ForEach(x => x.Addresses, new AddressValidator(), rule => rule.WhereAsync((_, _) => Task.FromResult(true)));

        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => chain.Validate(new Person { Tags = ["a"] }));
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => child.Validate(new Person { Addresses = [new Address()] }));
    }

    [Fact]
    public async Task Async_condition_that_is_false_skips_the_rule()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNull(), rule => rule.WhenAsync(async (x, _) => { await Task.Yield(); return x.IsCompany; }))
            .For(x => x.Email, e => e.IfNull(), rule => rule.UnlessAsync(async (x, _) => { await Task.Yield(); return x.IsCompany; }))
            .For(x => x.VatNumber, v => v.IfNull(), rule => rule.UnlessAsync((x, context, _) => Task.FromResult(context.Items.ContainsKey("skip"))))
            .For(x => x.Address, a => a.IfNull(), rule => rule.WhenAsync((x, context, _) => Task.FromResult(!context.Items.ContainsKey("skip"))));

        var person = await validator.ValidateAsync(new Person(), Token);
        var skipped = await validator.ValidateAsync(new Person(), o => o.Items["skip"] = true, Token);

        Assert.Equal(["Email", "VatNumber", "Address"], Names.Of(person));
        Assert.Equal(["Email"], Names.Of(skipped));
    }

    [Fact]
    public async Task Dependent_rules_run_after_an_async_rule_passes()
    {
        var validator = new Validator<Person>().MustAsync(
            x => x.Email,
            async (email, _) => { await Task.Yield(); return email is not null; },
            "Email is required.",
            rule => rule.DependentRules(v => v.For(x => x.Email, e => e.IfNotEmailAddress())));
        var quick = new Validator<Person>().For(
            x => x.Name,
            n => n.IfNull(),
            rule => rule.DependentRules(v => v.MustAsync((_, _) => Task.FromResult(false), "Dependent.")));

        var missing = await validator.ValidateAsync(new Person(), Token);
        var bad = await validator.ValidateAsync(new Person { Email = "ada" }, Token);
        var good = await validator.ValidateAsync(new Person { Email = "ada@example.com" }, Token);
        var quickPassed = await quick.ValidateAsync(new Person { Name = "Ada" }, Token);
        var quickFailed = await quick.ValidateAsync(new Person(), Token);

        Assert.Equal(["Email"], Names.Of(missing));
        Assert.Equal([ErrorCodes.NotEmailAddress], Names.Codes(bad));
        Assert.True(good.IsValid);
        Assert.Equal([string.Empty], Names.Of(quickPassed));
        Assert.Equal(["Name"], Names.Of(quickFailed));
    }

    [Fact]
    public async Task Class_cascade_stop_ends_the_run_at_the_first_failed_rule()
    {
        var validator = new Validator<Person>
        {
            ClassCascade = CascadeMode.Stop,
        };
        validator.For(x => x.Name, n => n.IfNull())
            .MustAsync(async (_, _) => { await Task.Yield(); return false; }, "Async.")
            .For(x => x.Email, e => e.IfNull());
        var asyncFirst = new Validator<Person> { ClassCascade = CascadeMode.Stop }
            .MustAsync(async (_, _) => { await Task.Yield(); return false; }, "Async.")
            .For(x => x.Email, e => e.IfNull());

        var report = await validator.ValidateAsync(new Person(), Token);
        var second = await validator.ValidateAsync(new Person { Name = "Ada" }, Token);
        var third = await asyncFirst.ValidateAsync(new Person(), Token);

        Assert.Equal(["Name"], Names.Of(report));
        Assert.Equal([string.Empty], Names.Of(second));
        Assert.Equal([string.Empty], Names.Of(third));
    }

    [Fact]
    public async Task Null_reference_in_a_selector_becomes_a_clear_error_in_both_modes()
    {
        var validator = new Validator<Person>().For(x => x.Address!.City, c => c.IfNull());

        var sync = Assert.Throws<InvalidOperationException>(() => validator.Validate(new Person()));
        var async = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateAsync(new Person(), Token));

        Assert.IsType<NullReferenceException>(sync.InnerException);
        Assert.IsType<NullReferenceException>(async.InnerException);
        Assert.Contains("x.Address!.City", sync.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Null_reference_from_rule_code_without_a_selector_is_rethrown_as_it_is()
    {
        var validator = new Validator<Person>().Custom((x, context) => context.AppendArgument("Length", x.Address!.City!.Length));

        Assert.Throws<NullReferenceException>(() => validator.Validate(new Person()));
        await Assert.ThrowsAsync<NullReferenceException>(() => validator.ValidateAsync(new Person(), Token));
    }

    [Fact]
    public async Task Exception_before_the_first_await_faults_the_task()
    {
        var validator = new Validator<Person>().For(x => Explode(x), v => v.IfNull());

        var task = validator.ValidateAsync(new Person(), Token);

        Assert.True(task.IsFaulted);
        await Assert.ThrowsAsync<FormatException>(() => task);
    }

    [Fact]
    public async Task Block_conditions_are_shared_by_every_rule_of_the_block()
    {
        var calls = 0;
        var validator = new Validator<Person>()
            .When(
                x =>
                {
                    calls++;
                    return x.IsCompany;
                },
                v => v.For(x => x.VatNumber, n => n.IfNull()).For(x => x.Name, n => n.IfNull()),
                otherwise: v => v.For(x => x.Email, e => e.IfNull()))
            .WhenAsync(async (x, _) => { await Task.Yield(); return x.IsCompany; }, v => v.For(x => x.Address, a => a.IfNull()).Must(x => x.Age > 0, "Age."))
            .UnlessAsync((x, _) => Task.FromResult(x.IsCompany), v => v.Must(x => x.Age < 100, "Young.").Must(x => x.Age > 1, "Born."))
            .Unless((x, context) => context.Items.ContainsKey("skip"), v => v.For(x => x.Tags, t => t.IfNullOrEmpty()))
            .WhenAsync((x, context, _) => Task.FromResult(x.IsCompany && !context.Items.ContainsKey("skip")), v => v.Must(x => x.Age > 18, "Adult."))
            .UnlessAsync((x, context, _) => Task.FromResult(context.Items.ContainsKey("skip")), v => v.Must(x => x.Age != 7, "Not seven."));

        var company = await validator.ValidateAsync(new Person { IsCompany = true, Age = 7 }, Token);
        var person = await validator.ValidateAsync(new Person { Age = 7 }, o => o.Items["skip"] = true, Token);

        Assert.Equal(["VatNumber", "Name", "Address", "Tags", string.Empty, string.Empty], Names.Of(company));
        Assert.Equal(["Email"], Names.Of(person));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Property_MustAsync_with_the_object_and_the_value()
    {
        var validator = new Validator<Person>().MustAsync(
            x => x.Age,
            async (x, age, _) => { await Task.Yield(); return age >= 18 || !x.IsCompany; },
            "Company owners must be adults.");

        var report = await validator.ValidateAsync(new Person { IsCompany = true, Age = 10 }, Token);
        var valid = await validator.ValidateAsync(new Person { Age = 10 }, Token);

        Assert.Equal(["Age"], Names.Of(report));
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task PreValidate_can_stop_an_async_run()
    {
        var validator = new StoppingValidator();

        var report = await validator.ValidateAsync(new Person(), Token);

        Assert.Equal(["Gate"], Names.Of(report));
        Assert.Equal("ClosedGate", Assert.Single(report.Failures).ErrorCode);
    }

    private static string Explode(Person person) => throw new FormatException($"Cannot read {person.Name}.");

    private sealed class StoppingValidator : Validator<Person>
    {
        public StoppingValidator() => For(x => x.Name, n => n.IfNull());

        protected override bool PreValidate(ValidationContext<Person> context)
        {
            context.AddFailure("Gate", "The gate is closed.", "ClosedGate");
            return false;
        }
    }

    public sealed class Holder
    {
        public IEnumerable<Line?>? Lazy { get; set; }
    }

    public sealed class Holder2
    {
        public List<string?>? Tags { get; set; }
    }

    // A hand-written child validator; with delay it completes after an await.
    private sealed class SlowStubValidator<T>(string name, bool delay) : IValidator<T>
    {
        public bool CanValidate(Type type) => typeof(T).IsAssignableFrom(type);

        public ValidationReport Validate(T value, ValidationOptions? options) => new ValidationReport().Add(name, "Stub failure.", "Stub");

        public async Task<ValidationReport> ValidateAsync(T value, ValidationOptions? options, CancellationToken cancellationToken = default)
        {
            if (delay)
            {
                await Task.Yield();
            }

            return Validate(value, options);
        }

        public ValidationReport Validate(object instance, ValidationOptions? options) => Validate((T)instance, options);

        public Task<ValidationReport> ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken = default) =>
            ValidateAsync((T)instance, options, cancellationToken);
    }
}
