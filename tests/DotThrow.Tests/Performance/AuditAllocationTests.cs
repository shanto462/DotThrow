#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Performance;

/// <summary>
/// Review fix: paths that used generic BCL span helpers (which boxed on some CPUs) now use
/// their own loops; they must stay allocation-free on the happy path.
/// </summary>
public sealed class AuditAllocationTests
{
    private const int Iterations = 2000;

    private static readonly char[] Markup = ['<', '>', '&', '"', '\'', '%', ';', '(', ')'];
    private static readonly int[] Numbers = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly List<int> NumberList = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly HashSet<int> NumberSet = [1, 2, 3];
    private static readonly Dictionary<string, int> Stock = new() { ["apple"] = 1 };

    [Fact]
    public void Ad_hoc_character_sets_do_not_allocate() => AssertNoAllocations(static () =>
    {
        string a = "hello world".Throw().IfContainsAny(Markup).IfContainsAnyExcept("abcdefghijklmnopqrstuvwxyz éü");
        string b = "abc".Throw().IfContainsAny('x', 'y').IfContainsAnyExcept('a', 'b', 'c');
        return a.Length + b.Length;
    });

    [Fact]
    public void Value_searches_and_enums_do_not_allocate() => AssertNoAllocations(static () =>
    {
        int a = 9.Throw().IfOneOf(1, 2, 3, 4, 5, 6).IfNotOneOf(9, 10, 11, 12, 13);
        var b = Numbers.Throw().IfContains(42).Value.Length;
        var c = NumberList.Throw().IfContains(42).IfNotContains(8).Value.Count;
        var d = DayOfWeek.Friday.Throw().IfNotDefined().Value;
        return a + b + c + (int)d;
    });

    [Fact]
    public void Any_and_None_on_sets_and_dictionaries_do_not_allocate() => AssertNoAllocations(static () =>
    {
        var a = NumberSet.Throw().IfAny(static x => x > 10).IfNone(static x => x == 2).Value.Count;
        var b = Stock.Throw().IfAny(static pair => pair.Value < 0).IfNone(static pair => pair.Key == "apple").Value.Count;
        var c = Numbers.Throw().IfAny(static x => x > 10).Value.Length;
        return a + b + c;
    });

    [Fact]
    public void Email_check_does_not_allocate() => AssertNoAllocations(static () =>
    {
        string a = "first.last+tag@sub.example.co.uk".Throw().IfNotEmailAddress();
        string b = "\"a b\"@[IPv6:2001:db8::1]".Throw().IfNotEmailAddress();
        return a.Length + b.Length;
    });

    private static void AssertNoAllocations(Func<int> chain)
    {
        Allocations.SkipUnlessOptimized();
        var sum = 0;
        Allocations.AssertAtMost(() => sum += chain(), Iterations);
        Assert.True(sum != 0);
    }
}
#endif
