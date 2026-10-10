using System;
using System.Collections;
using System.Collections.Generic;
#if NET5_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace DotThrow.Internal;

internal static class Collections
{
    // Counts items but stops at `limit`, so checks like "at most 10 items" never walk
    // a long sequence to the end. Collections that know their count are not enumerated.
    public static int CountUpTo(IEnumerable source, int limit)
    {
        if (source is ICollection collection)
        {
            return Math.Min(collection.Count, limit);
        }

        // Covariance makes this match any collection of reference-type items, such as
        // HashSet<string>, which does not implement the non-generic ICollection.
        if (source is IReadOnlyCollection<object?> readOnly)
        {
            return Math.Min(readOnly.Count, limit);
        }

        var count = 0;
        var enumerator = source.GetEnumerator();
        try
        {
            while (count < limit && enumerator.MoveNext())
            {
                count++;
            }
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }

        return count;
    }

    // The same for a generic sequence: ICollection<T> and IReadOnlyCollection<T> give the
    // count directly (also for value-type items, which the non-generic version cannot see),
    // and anything else is enumerated without boxing the items.
    public static int CountUpTo<T>(IEnumerable<T> source, int limit)
    {
        if (source is ICollection<T> collection)
        {
            return Math.Min(collection.Count, limit);
        }

        if (source is IReadOnlyCollection<T> readOnly)
        {
            return Math.Min(readOnly.Count, limit);
        }

        if (source is ICollection nonGeneric)
        {
            return Math.Min(nonGeneric.Count, limit);
        }

        var count = 0;
        using var enumerator = source.GetEnumerator();
        while (count < limit && enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    // Arrays use Array.IndexOf (vectorized for primitive types), any other ICollection<T>
    // uses its own Contains (O(1) for HashSet and FrozenSet, with the set's own comparer).
    // A List<T> with a custom comparer is searched as a span, without an enumerator.
    public static bool Contains<T>(IEnumerable<T> source, T item, IEqualityComparer<T>? comparer)
    {
        if (comparer is null)
        {
            if (source is T[] array)
            {
                return Array.IndexOf(array, item) >= 0;
            }

            if (source is ICollection<T> collection)
            {
                return collection.Contains(item);
            }
        }

        comparer ??= EqualityComparer<T>.Default;
#if NET5_0_OR_GREATER
        if (source is List<T> list)
        {
            foreach (var candidate in CollectionsMarshal.AsSpan(list))
            {
                if (comparer.Equals(candidate, item))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        if (source is IList<T> indexed)
        {
            for (var i = 0; i < indexed.Count; i++)
            {
                if (comparer.Equals(indexed[i], item))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var candidate in source)
        {
            if (comparer.Equals(candidate, item))
            {
                return true;
            }
        }

        return false;
    }

#if NET8_0_OR_GREATER
    public static bool Contains<T>(scoped ReadOnlySpan<T> values, T item, IEqualityComparer<T>? comparer)
    {
        if (comparer is null)
        {
            // EqualityComparer<T>.Default is devirtualized for value types.
            foreach (var candidate in values)
            {
                if (EqualityComparer<T>.Default.Equals(candidate, item))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var candidate in values)
        {
            if (comparer.Equals(candidate, item))
            {
                return true;
            }
        }

        return false;
    }
#endif

    // Index loops for lists and arrays, so the check does not allocate a boxed enumerator.
    public static bool ContainsNull(IEnumerable source)
    {
        if (source is IList list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is null)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var item in source)
        {
            if (item is null)
            {
                return true;
            }
        }

        return false;
    }

    // Typed version: no boxing. Arrays and lists are read by index or as a span.
    public static bool ContainsNull<T>(IEnumerable<T> source)
    {
        if (source is T[] array)
        {
            foreach (var item in array)
            {
                if (item is null)
                {
                    return true;
                }
            }

            return false;
        }

#if NET5_0_OR_GREATER
        if (source is List<T> list)
        {
            foreach (var item in CollectionsMarshal.AsSpan(list))
            {
                if (item is null)
                {
                    return true;
                }
            }

            return false;
        }
#endif

        if (source is IList<T> indexed)
        {
            for (var i = 0; i < indexed.Count; i++)
            {
                if (indexed[i] is null)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var item in source)
        {
            if (item is null)
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasDuplicates(IEnumerable source)
    {
        // Zero or one item cannot have duplicates, and needs no set.
        if (source is ICollection { Count: < 2 })
        {
            return false;
        }

        var seen = new HashSet<object?>();
        foreach (var item in source)
        {
            if (!seen.Add(item))
            {
                return true;
            }
        }

        return false;
    }

    // Finds the first item that matches the predicate. Arrays, lists and IList<T> are
    // walked by index (List<T> as a span on .NET 5 and later), so no enumerator is
    // allocated; any other sequence is enumerated and stops at the first match.
    public static bool Any<TCollection, TItem>(TCollection source, Func<TItem, bool> predicate)
        where TCollection : IEnumerable<TItem>
    {
        if (source is TItem[] array)
        {
            foreach (var item in array)
            {
                if (predicate(item))
                {
                    return true;
                }
            }

            return false;
        }

#if NET5_0_OR_GREATER
        if (source is List<TItem> list)
        {
            foreach (var item in CollectionsMarshal.AsSpan(list))
            {
                if (predicate(item))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        if (source is IList<TItem> indexed)
        {
            for (var i = 0; i < indexed.Count; i++)
            {
                if (predicate(indexed[i]))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var item in source)
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }
}
