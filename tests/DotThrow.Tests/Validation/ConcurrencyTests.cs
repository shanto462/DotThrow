using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Support;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// The validator reuses a root context per thread and the checks build messages with a
// per-thread pooled builder. These tests run many interleaved sync and async runs and check
// that every report and message belongs to its own input.
public sealed class ConcurrencyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Validator<Person> Shared = new Validator<Person>()
        .For(x => x.Name, n => n.IfNullOrWhiteSpace().IfLongerThan(10))
        .MustAsync(x => x.Email, async (email, ct) => { await Task.Yield(); return email is not null; }, "Email is required.")
        .For(x => x.Age, a => a.IfOutOfRange(0, 150))
        .ForEach(x => x.Tags, t => t.IfNullOrEmpty())
        .For(x => x.Address, new AddressValidator())
        .When(x => x.IsCompany, v => v.For(x => x.VatNumber, n => n.IfNullOrEmpty()));

    [Fact]
    public async Task Interleaved_async_runs_keep_their_own_failures()
    {
        var people = Enumerable.Range(0, 400).Select(Make).ToList();

        var reports = await Task.WhenAll(people.Select(p => Task.Run(() => Shared.ValidateAsync(p, Token), Token)));

        for (var i = 0; i < people.Count; i++)
        {
            Assert.Equal(Expected(i), reports[i].Failures.Select(f => f.Name));
        }
    }

    [Fact]
    public void Parallel_sync_runs_keep_their_own_failures()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrWhiteSpace().IfLongerThan(10))
            .For(x => x.Age, a => a.IfOutOfRange(0, 150))
            .ForEach(x => x.Tags, t => t.IfNullOrEmpty())
            .For(x => x.Address, new AddressValidator())
            .When(x => x.IsCompany, v => v.For(x => x.VatNumber, n => n.IfNullOrEmpty()));
        var people = Enumerable.Range(0, 2000).Select(Make).ToList();
        var results = new string[people.Count][];

        Parallel.For(0, people.Count, new ParallelOptions { CancellationToken = Token }, i =>
            results[i] = [.. validator.Validate(people[i]).Failures.Select(f => f.Name)]);

        for (var i = 0; i < people.Count; i++)
        {
            Assert.Equal(Expected(i).Where(n => n != "Email"), results[i]);
        }
    }

    [Fact]
    public void Nested_runs_on_one_thread_do_not_share_state()
    {
        // A rule that runs another validator inside the same thread, in the middle of a run.
        var inner = new Validator<Address>().For(x => x.City, c => c.IfNullOrEmpty());
        var outer = new Validator<Person>()
            .For(x => x.Name, n => n.IfNull())
            .Custom((x, context) =>
            {
                var nested = inner.Validate(new Address());
                context.AddFailure("Nested", nested.Failures.Count.ToString(CultureInfo.InvariantCulture));
            })
            .For(x => x.Email, e => e.IfNull());

        var report = outer.Validate(new Person());

        Assert.Equal(["Name", "Nested", "Email"], report.Failures.Select(f => f.Name));
        Assert.Equal("1", report.Failures[1].Message);
    }

    [Fact]
    public void Failure_messages_built_in_parallel_are_exact()
    {
        var messages = new string[4000];

        Parallel.For(0, messages.Length, new ParallelOptions { CancellationToken = Token }, i =>
        {
            var report = new ValidationReport();
            report.Check(i, "count").IfLessThan(i + 1);
            report.Check("x", "name").IfShorterThan(i % 7 + 2);
            report.Check(new List<int> { 1, 2, 3 }, "items").IfCountGreaterThan(i % 3);
            messages[i] = string.Join("|", report.Failures.Select(f => f.Message));
        });

        for (var i = 0; i < messages.Length; i++)
        {
            var expected = string.Join(
                "|",
                $"'count' must be greater than or equal to {(i + 1).ToString(CultureInfo.InvariantCulture)}.",
                $"'name' must be at least {(i % 7 + 2).ToString(CultureInfo.InvariantCulture)} characters long.",
                i % 3 == 1 ? "'items' must have at most 1 item." : $"'items' must have at most {(i % 3).ToString(CultureInfo.InvariantCulture)} items.");
            Assert.Equal(expected, messages[i]);
        }
    }

    [Fact]
    public async Task Validating_while_rules_are_added_from_another_thread()
    {
        var validator = new Validator<Person>().For(x => x.Name, n => n.IfNull());
        using var stop = new CancellationTokenSource();
        var errors = 0;
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(
            async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    var report = await validator.ValidateAsync(new Person(), Token);

                    // Each run sees a prefix of the rules: Name first, then R0, R1, ... in order.
                    var names = report.Failures.Select(f => f.Name).ToList();
                    var inOrder = names.Count > 0 && names[0] == "Name"
                        && names.Skip(1).Select((n, i) => n == $"R{i.ToString(CultureInfo.InvariantCulture)}").All(ok => ok);
                    if (!inOrder)
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            },
            Token)).ToArray();

        for (var i = 0; i < 100; i++)
        {
            var name = $"R{i.ToString(CultureInfo.InvariantCulture)}";
            validator.MustAsync(async (_, _) => { await Task.Yield(); return false; }, "Fails.", rule => rule.WithName(name));
            await Task.Yield();
        }

        await stop.CancelNowAsync();
        await Task.WhenAll(readers);

        Assert.Equal(0, errors);
        Assert.Equal(101, (await validator.ValidateAsync(new Person(), Token)).Failures.Count);
    }

    private static Person Make(int i) => new()
    {
        Name = (i % 4) switch
        {
            0 => null,
            1 => "Ada Lovelace the First",
            _ => "Ada",
        },
        Email = i % 3 == 0 ? null : "ada@example.com",
        Age = i % 5 == 0 ? -1 : 36,
        Tags = i % 6 == 0 ? ["math", ""] : ["math"],
        Address = i % 7 == 0 ? new Address() : Address.Valid(),
        IsCompany = i % 2 == 1,
        VatNumber = i % 4 == 3 ? null : "GB123",
    };

    private static IEnumerable<string> Expected(int i)
    {
        if (i % 4 is 0 or 1)
        {
            yield return "Name";
        }

        if (i % 3 == 0)
        {
            yield return "Email";
        }

        if (i % 5 == 0)
        {
            yield return "Age";
        }

        if (i % 6 == 0)
        {
            yield return "Tags[1]";
        }

        if (i % 7 == 0)
        {
            yield return "Address.Street";
            yield return "Address.City";
            yield return "Address.PostalCode";
        }

        if (i % 2 == 1 && i % 4 == 3)
        {
            yield return "VatNumber";
        }
    }
}
