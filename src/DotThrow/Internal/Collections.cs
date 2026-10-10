using System;
using System.Collections;
using System.Collections.Generic;

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

    public static bool Contains<T>(IEnumerable<T> source, T item, IEqualityComparer<T>? comparer)
    {
        // A collection's own Contains uses its own comparer, for example a
        // case-insensitive HashSet<string>. Use it only when no comparer is given.
        if (comparer is null && source is ICollection<T> collection)
        {
            return collection.Contains(item);
        }

        comparer ??= EqualityComparer<T>.Default;
        foreach (var candidate in source)
        {
            if (comparer.Equals(candidate, item))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsNull(IEnumerable source)
    {
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
}
