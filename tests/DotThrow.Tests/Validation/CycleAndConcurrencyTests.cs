using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

public sealed class CycleAndConcurrencyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Include_cycles_are_rejected_when_they_are_built()
    {
        var a = new Validator<Person>();
        var b = new Validator<Person>();
        var c = new Validator<Person>();
        a.Include(b);
        b.Include(c);

        Assert.Throws<ArgumentException>(() => a.Include(a));
        Assert.Throws<ArgumentException>(() => c.Include(a));
        Assert.Throws<ArgumentException>(() => c.For(x => x.Name, n => n.IfNull(), rule => rule.DependentRules(v => v.Include(a))));
    }

    [Fact]
    public void Recursive_validators_follow_the_data()
    {
        var node = new Validator<TreeNode>();
        node.For(x => x.Name, n => n.IfNullOrEmpty()).ForEach(x => x.Children, node);

        var tree = new TreeNode { Name = "root", Children = [new TreeNode { Name = "a", Children = [new TreeNode()] }] };

        Assert.Equal(["Children[0].Children[0].Name"], Names.Of(node.Validate(tree)));
    }

    [Fact]
    public void Object_graph_cycle_stops_with_a_clear_error()
    {
        var node = new Validator<TreeNode>();
        node.For(x => x.Parent, node);
        var first = new TreeNode { Name = "a" };
        var second = new TreeNode { Name = "b", Parent = first };
        first.Parent = second;

        var exception = Assert.Throws<InvalidOperationException>(() => node.Validate(first));

        Assert.Contains("cycle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Object_graph_cycle_stops_in_async_runs_too()
    {
        var node = new Validator<TreeNode>();
        node.For(x => x.Parent, node);
        var first = new TreeNode { Name = "a" };
        first.Parent = first;

        await Assert.ThrowsAsync<InvalidOperationException>(() => node.ValidateAsync(first, Token));
    }

    [Fact]
    public async Task Validating_from_many_threads_while_adding_rules_is_safe()
    {
        var validator = new Validator<Person>().For(x => x.Name, n => n.IfNullOrEmpty());
        var errors = new List<Exception>();
        using var stop = new CancellationTokenSource();
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    var report = validator.Validate(new Person());
                    if (report.Failures.Count < 1 || report.Failures[0].Name != "Name")
                    {
                        throw new InvalidOperationException("Unexpected report.");
                    }
                }
                catch (Exception exception)
                {
                    lock (errors)
                    {
                        errors.Add(exception);
                    }
                }
            }
        })).ToArray();

        for (var i = 0; i < 200; i++)
        {
            validator.For(x => x.Email, e => e.IfNull(), rule => rule.When(x => x.Age > 1000));
        }

        stop.Cancel();
        await Task.WhenAll(readers);
        Assert.Empty(errors);
        Assert.Equal(201, validator.Describe().Rules.Count);
    }

    [Fact]
    public void Nested_runs_of_the_same_type_on_one_thread_keep_their_own_state()
    {
        var inner = new Validator<Person>().For(x => x.Email, e => e.IfNull());
        var outer = new Validator<Person>().Custom((x, context) =>
        {
            var nested = inner.Validate(new Person(), "other", ValidationGroups.Default);
            context.AddFailure("Nested", $"{nested.Failures.Count}");
            context.Check(x.Name, "Name").IfNull();
        });

        var report = outer.Validate(new Person());

        Assert.Equal(["Nested", "Name"], Names.Of(report));
        Assert.Equal("1", report.Failures[0].Message);
        Assert.Equal([ValidationGroups.Default], report.GroupsExecuted);
    }
}
