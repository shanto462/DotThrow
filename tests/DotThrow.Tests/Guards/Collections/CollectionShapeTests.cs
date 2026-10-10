using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>Every count check gives the same answer for every collection shape.</summary>
public sealed class CollectionShapeTests
{
    public static TheoryData<int> Sizes => [0, 1, 2, 3, 5];

    // Each shape method has a concrete collection type, so the compiler picks the specific
    // overload (array, List, HashSet, ...) and the generic fallback for the others.
    [Theory]
    [MemberData(nameof(Sizes))]
    public void Every_shape_counts_the_same(int size)
    {
        var items = Enumerable.Range(1, size).ToArray();
        ShapeArray(items, size);
        ShapeList(items.ToList(), size);
        ShapeHashSet(new HashSet<int>(items), size);
        ShapeDictionary(items.ToDictionary(i => i, i => i), size);
        ShapeICollection(items.ToList(), size);
        ShapeIList(items.ToList(), size);
        ShapeIReadOnlyCollection(new ReadOnlyCollection<int>(items), size);
        ShapeIReadOnlyList(items, size);
        ShapeIEnumerableLazy(items.Select(i => i), size);
        ShapeIEnumerableSet(new HashSet<int>(items), size);
        ShapeNonGeneric(new ArrayList(items), size);
        ShapeQueue(new Queue<int>(items), size);
        ShapeSortedSet(new SortedSet<int>(items), size);
    }

    [Fact]
    public void Shapes_report_null_with_the_Null_code()
    {
        Check.Fails<ArgumentNullException, int[]>(null!, g => g.IfCountGreaterThan(1), ErrorCodes.Null);
        Check.Fails<ArgumentNullException, List<int>>(null!, g => g.IfNullOrEmpty(), ErrorCodes.Null);
        Check.Fails<ArgumentNullException, HashSet<int>>(null!, g => g.IfCountOutOfRange(1, 2), ErrorCodes.Null);
        Check.Fails<ArgumentNullException, IReadOnlyList<int>>(null!, g => g.IfCountLessThan(1), ErrorCodes.Null);
        Check.Fails<ArgumentNullException, IEnumerable<int>>(null!, g => g.IfCountEqualTo(1), ErrorCodes.Null);
    }

    [Fact]
    public void A_lazy_sequence_is_read_only_as_far_as_needed()
    {
        var reads = 0;
        IEnumerable<int> Endless()
        {
            while (true)
            {
                reads++;
                yield return reads;
            }
        }

        Check.Fails<ArgumentException, IEnumerable<int>>(Endless(), g => g.IfCountGreaterThan(3), ErrorCodes.CountGreaterThan);
        Assert.True(reads <= 8, reads.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void IfAny_and_IfNone_with_a_typed_lambda()
    {
        var prices = new List<decimal> { 1m, 2m, 3m };
        Check.Passes(prices, g => g.IfAny((decimal p) => p < 0));
        Check.Fails<ArgumentException, List<decimal>>(prices, g => g.IfAny((decimal p) => p > 2), ErrorCodes.Any, "'input' must not contain any item that matches the condition.");
        Check.Passes(prices, g => g.IfNone((decimal p) => p == 2));
        Check.Fails<ArgumentException, List<decimal>>(prices, g => g.IfNone((decimal p) => p > 5), ErrorCodes.None, "'input' must contain at least one item that matches the condition.");
        Check.Fails<ArgumentException, decimal[]>([], g => g.IfNone((decimal p) => true), ErrorCodes.None);
        Assert.Equal("bad: (decimal p) => p > 2", Check.MessageOf(prices, g => g.IfAny((decimal p) => p > 2, "bad: {Condition}")));

        var stops = 0;
        Check.Fails<ArgumentException, IEnumerable<int>>(Enumerable.Range(1, 1000).Select(i => { stops++; return i; }), g => g.IfAny((int i) => i == 3), ErrorCodes.Any);
        Assert.Equal(6, stops); // 3 reads in throw mode and 3 in collect mode: it stops at the first match.
        Check.Fails<ArgumentException, IList<int>>(new Collection<int> { 1, 5 }, g => g.IfAny((int i) => i == 5), ErrorCodes.Any);
        Assert.Throws<ArgumentNullException>(() => prices.Throw().IfAny((Func<decimal, bool>)null!));
    }

    private static void ShapeArray(int[] collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeList(List<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeHashSet(HashSet<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeDictionary(Dictionary<int, int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeICollection(ICollection<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeIList(IList<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeIReadOnlyCollection(IReadOnlyCollection<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeIReadOnlyList(IReadOnlyList<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeIEnumerableLazy(IEnumerable<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeIEnumerableSet(IEnumerable<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeNonGeneric(ArrayList collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeQueue(Queue<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static void ShapeSortedSet(SortedSet<int> collection, int size)
    {
        Assert.Equal(size == 0, Fails(collection, g => g.IfNullOrEmpty()));
        for (var n = 0; n <= 6; n++)
        {
            Assert.Equal(size == n, Fails(collection, g => g.IfCountEqualTo(n)));
            Assert.Equal(size != n, Fails(collection, g => g.IfCountNotEqualTo(n)));
            Assert.Equal(size > n, Fails(collection, g => g.IfCountGreaterThan(n)));
            Assert.Equal(size < n, Fails(collection, g => g.IfCountLessThan(n)));
            Assert.Equal(size < n || size > n + 1, Fails(collection, g => g.IfCountOutOfRange(n, n + 1)));
        }
    }

    private static bool Fails<T>(T collection, GuardChain<T> chain)
        where T : IEnumerable
    {
        var report = new ValidationReport();
        chain(report.Check(collection));
        return !report.IsValid;
    }
}
