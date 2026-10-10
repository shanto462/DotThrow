using System;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Support;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// ValidateAsync: the sync fast path, async rules, conditions and filters, and cancellation.
public sealed class AsyncTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValidateAsync_with_only_sync_rules_returns_a_completed_task()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .For(x => x.Address, new AddressValidator())
            .ForEach(x => x.Addresses, new AddressValidator())
            .MustAsync((_, _) => Task.FromResult(true), configure: rule => rule.InGroup("never"));

        var task = validator.ValidateAsync(new Person { Address = new Address(), Addresses = [new Address()] }, Token);

        Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        Assert.Equal(7, (await task).Failures.Count);
    }

    [Fact]
    public async Task Async_rules_that_complete_synchronously_also_keep_the_task_completed()
    {
        var validator = new Validator<Person>()
            .MustAsync(x => x.Email, (email, _) => Task.FromResult(email is not null), "Email is required.")
            .CustomAsync((x, context, _) => { context.AddFailure("Custom"); return Task.CompletedTask; }, rule => rule.WhenAsync((_, _) => Task.FromResult(true)));

        var task = validator.ValidateAsync(new Person(), Token);

        Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        var names = Names.Of(await task);
        Assert.Equal(["Email", string.Empty], names);
    }

    [Fact]
    public async Task Async_rules_run_in_order_with_sync_rules()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .MustAsync(x => x.Email, async (email, ct) => { await Task.Yield(); return email is not null; }, "Email is required.")
            .For(x => x.Age, a => a.IfNegative())
            .CustomAsync(async (x, context, ct) => { await Task.Delay(1, ct); context.Check(x.VatNumber, "VatNumber").IfNullOrEmpty(); })
            .Must(x => x.Age > 100, "Old.");

        var report = await validator.ValidateAsync(new Person { Age = -1 }, Token);

        Assert.Equal(["Name", "Email", "Age", "VatNumber", string.Empty], Names.Of(report));
    }

    [Fact]
    public async Task Async_child_validators_and_async_collection_items()
    {
        var line = new Validator<Line>().MustAsync(x => x.Sku, async (sku, _) => { await Task.Yield(); return sku is not null; }, "Sku is required.");
        var validator = new Validator<Order>().ForEach(x => x.Lines, line).For(x => x.Number, n => n.IfNullOrEmpty());

        var report = await validator.ValidateAsync(new Order { Lines = [new Line(), new Line { Sku = "a" }, new Line()] }, Token);

        Assert.Equal(["Lines[0].Sku", "Lines[2].Sku", "Number"], Names.Of(report));
    }

    [Fact]
    public async Task Async_conditions_and_async_filters()
    {
        var validator = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrEmpty(), rule => rule.WhenAsync(async (x, _) => { await Task.Yield(); return x.Total > 0; }))
            .ForEach(x => x.Lines, new LineValidator(), rule => rule.WhereAsync(async (l, _) => { await Task.Yield(); return l!.Active; }))
            .WhenAsync(async (x, _) => { await Task.Yield(); return x.Total > 10; }, v => v.Must(x => x.Payment is not null, "Payment is required."));

        var report = await validator.ValidateAsync(new Order { Total = 20, Lines = [new Line { Active = false }, new Line()] }, Token);

        Assert.Equal(["Number", "Lines[1].Sku", "Lines[1].Quantity", string.Empty], Names.Of(report));
    }

    [Fact]
    public void Sync_run_throws_when_it_reaches_an_async_rule_condition_or_filter()
    {
        var rule = new Validator<Person>().MustAsync((_, _) => Task.FromResult(true));
        var condition = new Validator<Person>().For(x => x.Name, n => n.IfNull(), r => r.WhenAsync((_, _) => Task.FromResult(true)));
        var filter = new Validator<Person>().ForEach(x => x.Tags, t => t.IfNull(), r => r.WhereAsync((_, _) => Task.FromResult(true)));
        var child = new Validator<Person>().For(x => x.Address, new Validator<Address>().CustomAsync((_, _, _) => Task.CompletedTask));

        var exception = Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => rule.Validate(new Person()));
        Assert.Equal(typeof(Validator<Person>), exception.ValidatorType);
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => condition.Validate(new Person()));
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => filter.Validate(new Person { Tags = ["a"] }));
        Assert.Equal(typeof(Validator<Address>), Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => child.Validate(new Person { Address = new Address() })).ValidatorType);
        Assert.IsAssignableFrom<InvalidOperationException>(exception);
    }

    [Fact]
    public void Sync_run_does_not_throw_for_async_parts_that_do_not_run()
    {
        var validator = new Validator<Person>()
            .MustAsync((_, _) => Task.FromResult(true), configure: r => r.InGroup("async"))
            .For(x => x.Name, n => n.IfNull(), r => r.When(_ => false).WhenAsync((_, _) => Task.FromResult(true)))
            .ForEach(x => x.Tags, t => t.IfNull(), r => r.WhereAsync((_, _) => Task.FromResult(true)));

        Assert.True(validator.Validate(new Person { Tags = [] }).IsValid);
    }

    [Fact]
    public async Task Canceled_token_cancels_the_run()
    {
        var validator = new Validator<Person>().For(x => x.Name, n => n.IfNull());
        using var source = new CancellationTokenSource();
        await source.CancelNowAsync();

        var task = validator.ValidateAsync(new Person(), source.Token);

        Assert.True(task.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Token_reaches_async_rules_and_stops_before_the_next_rule()
    {
        using var source = new CancellationTokenSource();
        var reached = false;
        var validator = new Validator<Person>()
            .CustomAsync(async (_, _, ct) => { await source.CancelNowAsync(); await Task.Yield(); Assert.True(ct.IsCancellationRequested); })
            .Custom((_, _) => reached = true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validator.ValidateAsync(new Person(), source.Token));
        Assert.False(reached);
    }

    [Fact]
    public async Task Null_value_gives_a_faulted_task()
    {
        var validator = new Validator<Person>();

        var task = validator.ValidateAsync(null!, Token);

        Assert.True(task.IsFaulted);
        Assert.Equal("value", (await Assert.ThrowsAsync<ArgumentNullException>(() => task)).ParamName);
    }

    [Fact]
    public async Task Rule_exception_after_an_await_faults_the_task()
    {
        var validator = new Validator<Person>()
            .MustAsync(async (_, _) => { await Task.Yield(); return true; })
            .Custom((_, _) => throw new InvalidOperationException("Rule failed."));

        Assert.Equal("Rule failed.", (await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateAsync(new Person(), Token))).Message);
    }

    [Fact]
    public async Task ValidateAndThrowAsync_throws_with_every_failure()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .MustAsync(async (_, _) => { await Task.Yield(); return false; }, "Async failed.");

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => validator.ValidateAndThrowAsync(new Person(), Token));

        Assert.Equal(["Name", string.Empty], Names.Of(exception.Failures));
        await validator.ValidateAndThrowAsync(new Person { Name = "Ada" }, ["other"], Token);
    }

    [Fact]
    public async Task Many_concurrent_async_runs_keep_their_own_reports()
    {
        var validator = new Validator<Person>()
            .MustAsync(x => x.Name, async (name, _) => { await Task.Yield(); return name!.Length > 3; }, "Short.")
            .ForEach(x => x.Tags, t => t.IfNullOrEmpty());

        var tasks = new Task<ValidationReport>[200];
        for (var i = 0; i < tasks.Length; i++)
        {
            var person = new Person { Name = i % 2 == 0 ? "Ada Lovelace" : "Ada", Tags = i % 3 == 0 ? [""] : ["x"] };
            tasks[i] = Task.Run(() => validator.ValidateAsync(person));
        }

        var reports = await Task.WhenAll(tasks);
        for (var i = 0; i < reports.Length; i++)
        {
            var expected = (i % 2 == 0 ? 0 : 1) + (i % 3 == 0 ? 1 : 0);
            Assert.Equal(expected, reports[i].Failures.Count);
        }
    }
}
