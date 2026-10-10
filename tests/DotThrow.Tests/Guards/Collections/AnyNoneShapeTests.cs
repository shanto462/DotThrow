using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>Review fix: IfAny and IfNone infer the item type for the common collection shapes.</summary>
public sealed class AnyNoneShapeTests
{
    [Fact]
    public void Untyped_lambdas_work_for_every_shape()
    {
        int[] array = [1, 2, 3];
        Check.Fails<ArgumentException, int[]>(array, g => g.IfAny(x => x > 2), ErrorCodes.Any, "'input' must not contain any item that matches the condition.");
        Check.Passes(array, g => g.IfNone(x => x == 2));
        Check.Fails<ArgumentException, List<int>>([1, 2, 3], g => g.IfAny(x => x > 2), ErrorCodes.Any);
        Check.Fails<ArgumentException, HashSet<int>>([1, 2, 3], g => g.IfNone(x => x > 5), ErrorCodes.None);
        Check.Fails<ArgumentException, Dictionary<string, int>>(new() { ["a"] = 1, ["b"] = 9 }, g => g.IfAny(pair => pair.Value > 5), ErrorCodes.Any);
        Check.Passes(new Dictionary<string, int> { ["a"] = 1 }, g => g.IfNone(pair => pair.Key == "a"));
        Check.Fails<ArgumentException, ICollection<string>>(new List<string> { "a", "long text" }, g => g.IfAny(x => x.Length > 5), ErrorCodes.Any);
        Check.Fails<ArgumentException, IList<string>>(new List<string> { "a" }, g => g.IfNone(x => x.Length > 5), ErrorCodes.None);
        Check.Passes<IReadOnlyCollection<int>>(new ReadOnlyCollection<int>([1, 2]), g => g.IfAny(x => x < 0));
        Check.Fails<ArgumentException, IReadOnlyList<int>>(new[] { 1, 2 }, g => g.IfAny(x => x == 2), ErrorCodes.Any);
        Check.Fails<ArgumentException, IEnumerable<int>>(Enumerable.Range(1, 3).Select(x => x), g => g.IfNone(x => x > 3), ErrorCodes.None);
    }

    [Fact]
    public void Typed_lambdas_and_other_collection_types_still_work()
    {
        Check.Fails<ArgumentException, List<int>>([1, 5], g => g.IfAny((int x) => x > 2), ErrorCodes.Any);
        Check.Fails<ArgumentException, Queue<int>>(new Queue<int>([1, 5]), g => g.IfAny((int x) => x > 2), ErrorCodes.Any);
        Check.Passes(new SortedSet<int> { 1 }, g => g.IfNone((int x) => x == 1));
    }

    [Fact]
    public void The_condition_text_comes_from_the_lambda()
    {
        Assert.Equal("bad: x => x > 2", Check.MessageOf<int[]>([1, 3], g => g.IfAny(x => x > 2, "bad: {Condition}")));
        Check.Fails<ArgumentNullException, List<int>>(null!, g => g.IfAny(x => x > 2), ErrorCodes.Null);
        Assert.Throws<ArgumentNullException>(() => new List<int>().Throw().IfAny((Func<int, bool>)null!));
    }
}
