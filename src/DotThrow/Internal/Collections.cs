using System;
using System.Collections;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif
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
    // The values of IfOneOf(1, 2, 3) are usually a few constants. Up to four values are
    // compared without a loop, so after inlining the JIT turns the check into plain
    // comparisons with constants (like a pattern match). Longer lists of common primitive
    // types use the vectorized MemoryExtensions.Contains; anything else a simple loop.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains<T>(scoped ReadOnlySpan<T> values, T item, IEqualityComparer<T>? comparer)
    {
        if (comparer is not null)
        {
            return ContainsWithComparer(values, item, comparer);
        }

        switch (values.Length)
        {
            case 0:
                return false;
            case 1:
                return EqualityComparer<T>.Default.Equals(values[0], item);
            case 2:
                return EqualityComparer<T>.Default.Equals(values[0], item) || EqualityComparer<T>.Default.Equals(values[1], item);
            case 3:
                return EqualityComparer<T>.Default.Equals(values[0], item) || EqualityComparer<T>.Default.Equals(values[1], item)
                    || EqualityComparer<T>.Default.Equals(values[2], item);
            case 4:
                return EqualityComparer<T>.Default.Equals(values[0], item) || EqualityComparer<T>.Default.Equals(values[1], item)
                    || EqualityComparer<T>.Default.Equals(values[2], item) || EqualityComparer<T>.Default.Equals(values[3], item);
            default:
                return ContainsMany(values, item);
        }
    }

    private static bool ContainsMany<T>(scoped ReadOnlySpan<T> values, T item)
    {
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, int>(ref MemoryMarshal.GetReference(values)), values.Length).Contains(Unsafe.As<T, int>(ref item));
        }

        if (typeof(T) == typeof(long) || typeof(T) == typeof(ulong))
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, long>(ref MemoryMarshal.GetReference(values)), values.Length).Contains(Unsafe.As<T, long>(ref item));
        }

        if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, short>(ref MemoryMarshal.GetReference(values)), values.Length).Contains(Unsafe.As<T, short>(ref item));
        }

        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(values)), values.Length).Contains(Unsafe.As<T, byte>(ref item));
        }

        foreach (var candidate in values)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsWithComparer<T>(scoped ReadOnlySpan<T> values, T item, IEqualityComparer<T> comparer)
    {
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
