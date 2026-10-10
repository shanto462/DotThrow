using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Validation.Users;
using Xunit;

namespace DotThrow.Tests.Validation;

public sealed class ValidatorAsyncTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MustAsync_rule_reports_a_failure()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ada@example.com" };
        var validator = new Validator<User>()
            .For(x => x.Name, name => name.IfNullOrWhiteSpace())
            .MustAsync(
                async (user, ct) =>
                {
                    await Task.Yield();
                    return !taken.Contains(user.Email ?? "");
                },
                "The email is already registered.",
                configure: r => r.OverridePropertyName("Email").WithErrorCode("EmailTaken"));

        var report = await validator.ValidateAsync(User.Valid(), Token);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(("Email", "The email is already registered.", "EmailTaken"), (failure.Name, failure.Message, failure.ErrorCode));
    }

    [Fact]
    public async Task MustAsync_rule_uses_the_default_name_and_code()
    {
        var validator = new Validator<User>().MustAsync((_, _) => Task.FromResult(false), "Not allowed.");

        var report = await validator.ValidateAsync(new User(), Token);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(("", ErrorCodes.Must), (failure.Name, failure.ErrorCode));
    }

    [Fact]
    public async Task CustomAsync_rule_can_add_failures()
    {
        var validator = new Validator<Order>().CustomAsync(async (order, report, ct) =>
        {
            await Task.Delay(1, ct);
            report.Check(order.Total, "Total").IfNegative();
            report.AddFailure("", "Checked remotely.", "Remote");
        });

        var report = await validator.ValidateAsync(new Order { Total = -1 }, Token);

        Assert.Equal(new[] { ErrorCodes.Negative, "Remote" }, report.Failures.Select(f => f.ErrorCode));
    }

    [Fact]
    public async Task ValidateAsync_runs_sync_and_async_rules_in_order()
    {
        var validator = new Validator<Order>()
            .Must(_ => false, "first")
            .MustAsync((_, _) => Task.FromResult(false), "second")
            .For(x => x.Total, total => total.IfNegative())
            .CustomAsync((_, r, _) =>
            {
                r.AddFailure("", "fourth");
                return Task.CompletedTask;
            })
            .ForEach(x => x.Lines, new LineValidator())
            .Custom((_, r) => r.AddFailure("", "sixth"));
        var order = new Order { Total = -1, Lines = [new Line { Sku = "", Quantity = 1 }] };

        var report = await validator.ValidateAsync(order, Token);

        Assert.Equal(
            new[] { "first", "second", "'Total' must not be negative.", "fourth", "'Sku' must not be empty or white space.", "sixth" },
            report.Failures.Select(f => f.Message));
        Assert.Equal("Lines[0].Sku", report.Failures[4].Name);
    }

    [Fact]
    public void Sync_Validate_throws_when_a_selected_rule_is_async()
    {
        var mustAsync = new Validator<User>().MustAsync((_, _) => Task.FromResult(true), "m");
        var customAsync = new Validator<User>().CustomAsync((_, _, _) => Task.CompletedTask);

        var first = Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => mustAsync.Validate(new User()));
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => customAsync.Validate(new User()));
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => mustAsync.ValidateAndThrow(new User()));

        Assert.IsAssignableFrom<InvalidOperationException>(first);
        Assert.Equal(typeof(Validator<User>), first.ValidatorType);
        Assert.Contains("ValidateAsync", first.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sync_Validate_works_when_async_rules_are_not_selected()
    {
        var validator = new Validator<User>()
            .For(x => x.Name, name => name.IfNull())
            .MustAsync((_, _) => Task.FromResult(false), "m", configure: r => r.InGroup("remote"))
            .CustomAsync((_, _, _) => Task.CompletedTask, configure: r => r.When(_ => false));

        var report = validator.Validate(new User());

        Assert.Equal("Name", Assert.Single(report.Failures).Name);
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => validator.Validate(new User(), "remote"));
    }

    [Fact]
    public void Sync_Validate_throws_when_a_child_validator_is_async()
    {
        var address = new Validator<Address>().MustAsync((_, _) => Task.FromResult(true), "m");
        var line = new Validator<Line>().MustAsync((_, _) => Task.FromResult(true), "m");
        var child = new Validator<User>().For(x => x.Address, address);
        var included = new Validator<User>().Include(new Validator<User>().MustAsync((_, _) => Task.FromResult(true), "m"));
        var each = new Validator<Order>().ForEach(x => x.Lines, line);

        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => child.Validate(new User { Address = new Address() }));
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => included.Validate(new User()));
        Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => each.Validate(new Order { Lines = [new Line()] }));
    }

    [Fact]
    public async Task ValidateAsync_runs_async_rules_of_child_validators()
    {
        var address = new Validator<Address>().MustAsync((a, _) => Task.FromResult(a.City is not null), "No city.", configure: r => r.OverridePropertyName("City"));
        var line = new Validator<Line>().MustAsync((l, _) => Task.FromResult(l.Quantity > 0), "No quantity.", configure: r => r.OverridePropertyName("Quantity"));
        var user = new Validator<User>()
            .For(x => x.Address, address)
            .Include(new Validator<User>().MustAsync((_, _) => Task.FromResult(false), "Included."));
        var order = new Validator<Order>().ForEach(x => x.Lines, line);

        var userReport = await user.ValidateAsync(new User { Address = new Address() }, Token);
        var orderReport = await order.ValidateAsync(new Order { Lines = [new Line { Quantity = 1 }, null, new Line()] }, Token);
        var nullLines = await order.ValidateAsync(new Order(), Token);
        var nullAddress = await user.ValidateAsync(new User(), Token);

        Assert.Equal(new[] { "Address.City", "" }, userReport.Failures.Select(f => f.Name));
        Assert.Equal("Lines[2].Quantity", Assert.Single(orderReport.Failures).Name);
        Assert.True(nullLines.IsValid);
        Assert.Equal("", Assert.Single(nullAddress.Failures).Name);
    }

    [Fact]
    public async Task ValidateAsync_uses_groups()
    {
        var validator = new Validator<User>()
            .For(x => x.Name, name => name.IfNull())
            .MustAsync((_, _) => Task.FromResult(false), "remote", configure: r => r.InGroup("remote"))
            .MustAsync((_, _) => Task.FromResult(false), "other", configure: r => r.InGroup("other"));

        var defaults = await validator.ValidateAsync(new User(), Token);
        var remote = await validator.ValidateAsync(new User(), "remote", Token);
        var several = await validator.ValidateAsync(new User(), ["other", ValidationGroups.Default], Token);
        var all = await validator.ValidateAsync(new User(), [ValidationGroups.All], Token);
        var empty = await validator.ValidateAsync(new User(), Array.Empty<string>(), Token);
        var none = await validator.ValidateAsync(new User(), groups: null, Token);

        Assert.Equal(new[] { "'Name' must not be null." }, defaults.Failures.Select(f => f.Message));
        Assert.Equal(new[] { "remote" }, remote.Failures.Select(f => f.Message));
        Assert.Equal(new[] { "'Name' must not be null.", "other" }, several.Failures.Select(f => f.Message));
        Assert.Equal(3, all.Failures.Count);
        Assert.Single(empty.Failures);
        Assert.Single(none.Failures);
    }

    [Fact]
    public async Task ValidateAsync_works_through_the_interface()
    {
        var report = await ValidateThroughInterface(new UserValidator(), new User());
        var stub = await ValidateThroughInterface(new StubValidator<User>(), new User());

        Assert.False(report.IsValid);
        Assert.Equal("Stub", Assert.Single(stub.Failures).Name);
    }

    [Fact]
    public async Task ValidateAsync_null_throws_ArgumentNullException()
    {
        var validator = new UserValidator();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => validator.ValidateAsync(null!, Token));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public async Task ValidateAsync_honors_a_canceled_token()
    {
        var calls = 0;
        var validator = new Validator<User>().MustAsync(
            (_, _) =>
            {
                calls++;
                return Task.FromResult(true);
            },
            "m");
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validator.ValidateAsync(new User(), source.Token));

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ValidateAsync_stops_before_the_next_rule_after_cancel()
    {
        using var source = new CancellationTokenSource();
        var secondRan = false;
        var validator = new Validator<User>()
            .MustAsync(
                (_, _) =>
                {
                    source.Cancel();
                    return Task.FromResult(true);
                },
                "first")
            .Must(
                _ =>
                {
                    secondRan = true;
                    return true;
                },
                "second");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validator.ValidateAsync(new User(), source.Token));

        Assert.False(secondRan);
    }

    [Fact]
    public async Task ValidateAsync_passes_the_token_to_async_rules()
    {
        using var source = new CancellationTokenSource();
        var tokens = new List<CancellationToken>();
        var child = new Validator<Address>().MustAsync(
            (_, ct) =>
            {
                tokens.Add(ct);
                return Task.FromResult(true);
            },
            "m");
        var validator = new Validator<User>()
            .MustAsync(
                (_, ct) =>
                {
                    tokens.Add(ct);
                    return Task.FromResult(true);
                },
                "m")
            .CustomAsync((_, _, ct) =>
            {
                tokens.Add(ct);
                return Task.CompletedTask;
            })
            .For(x => x.Address, child);

        await validator.ValidateAsync(new User { Address = new Address() }, source.Token);

        Assert.Equal(3, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(source.Token, t));
    }

    [Fact]
    public async Task Rule_that_observes_the_token_can_cancel_a_long_lookup()
    {
        using var source = new CancellationTokenSource();
        var validator = new Validator<User>().MustAsync(
            async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return true;
            },
            "m");

        var run = validator.ValidateAsync(new User(), source.Token);
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task ValidateAndThrowAsync_throws_with_every_failure()
    {
        var validator = new Validator<User>()
            .For(x => x.Name, name => name.IfNull())
            .MustAsync((_, _) => Task.FromResult(false), "Remote check failed.", configure: r => r.OverridePropertyName("Email"));

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => validator.ValidateAndThrowAsync(new User(), Token));

        Assert.Equal(new[] { "Name", "Email" }, exception.Failures.Select(f => f.Name));
    }

    [Fact]
    public async Task ValidateAndThrowAsync_with_groups()
    {
        var validator = new Validator<User>()
            .For(x => x.Name, name => name.IfNull())
            .MustAsync((_, _) => Task.FromResult(false), "remote", configure: r => r.InGroup("remote"));

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => validator.ValidateAndThrowAsync(new User(), ["remote"], Token));
        await validator.ValidateAndThrowAsync(User.Valid(), Token);

        Assert.Equal("remote", Assert.Single(exception.Failures).Message);
    }

    [Fact]
    public async Task Async_rules_see_a_consistent_report_under_concurrency()
    {
        var validator = new Validator<Order>()
            .MustAsync(
                async (order, ct) =>
                {
                    await Task.Delay(1, ct);
                    return order.Total >= 0;
                },
                "Negative total.",
                configure: r => r.OverridePropertyName("Total"))
            .ForEach(x => x.Lines, new LineValidator());

        var runs = Enumerable.Range(0, 50).Select(i => validator.ValidateAsync(
            new Order { Total = i % 2 == 0 ? -1 : 1, Lines = [new Line { Sku = "", Quantity = 1 }] },
            Token));
        var reports = await Task.WhenAll(runs);

        for (var i = 0; i < reports.Length; i++)
        {
            Assert.Equal(i % 2 == 0 ? 2 : 1, reports[i].Failures.Count);
        }
    }

    private static Task<ValidationReport> ValidateThroughInterface(IValidator<User> validator, User user) =>
        validator.ValidateAsync(user, groups: null, Token);
}
