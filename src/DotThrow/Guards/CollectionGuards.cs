using System.Collections;
using System.Collections.Generic;
using DotThrow.Internal;

namespace DotThrow;

// Collection checks work on any IEnumerable: arrays, lists, sets, dictionaries, and
// strings. A null collection fails with the "Null" code. Count checks stop enumerating
// as soon as the answer is known, and never enumerate a collection that has a Count.
public static partial class GuardExtensions
{
    /// <summary>Fails when the collection is <see langword="null"/> or has no items.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfNullOrEmpty<TCollection>(this Guard<TCollection> guard, string? message = null)
        where TCollection : IEnumerable
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.CountUpTo(guard.Value, 1) == 0)
        {
            return guard.Fail(ErrorCodes.Empty, message ?? Messages.Empty(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the collection has exactly <paramref name="count"/> items.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="count">The rejected number of items.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfCountEqualTo<TCollection>(this Guard<TCollection> guard, int count, string? message = null)
        where TCollection : IEnumerable
    {
        Argument.NotNegative(count);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.CountUpTo(guard.Value, Above(count)) == count)
        {
            return guard.Fail(ErrorCodes.CountEqualTo, message ?? Messages.CountEqualTo(guard.Name, count));
        }

        return guard;
    }

    /// <summary>Fails when the collection does not have exactly <paramref name="count"/> items.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="count">The required number of items.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfCountNotEqualTo<TCollection>(this Guard<TCollection> guard, int count, string? message = null)
        where TCollection : IEnumerable
    {
        Argument.NotNegative(count);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.CountUpTo(guard.Value, Above(count)) != count)
        {
            return guard.Fail(ErrorCodes.CountNotEqualTo, message ?? Messages.CountNotEqualTo(guard.Name, count));
        }

        return guard;
    }

    /// <summary>Fails when the collection has more than <paramref name="maxCount"/> items.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="maxCount">The largest allowed number of items.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfCountGreaterThan<TCollection>(this Guard<TCollection> guard, int maxCount, string? message = null)
        where TCollection : IEnumerable
    {
        Argument.NotNegative(maxCount);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.CountUpTo(guard.Value, Above(maxCount)) > maxCount)
        {
            return guard.Fail(ErrorCodes.CountGreaterThan, message ?? Messages.CountGreaterThan(guard.Name, maxCount));
        }

        return guard;
    }

    /// <summary>Fails when the collection has fewer than <paramref name="minCount"/> items.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="minCount">The smallest allowed number of items.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfCountLessThan<TCollection>(this Guard<TCollection> guard, int minCount, string? message = null)
        where TCollection : IEnumerable
    {
        Argument.NotNegative(minCount);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.CountUpTo(guard.Value, minCount) < minCount)
        {
            return guard.Fail(ErrorCodes.CountLessThan, message ?? Messages.CountLessThan(guard.Name, minCount));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the number of items is outside the range from <paramref name="minCount"/>
    /// to <paramref name="maxCount"/>. Both ends are allowed.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="minCount">The smallest allowed number of items.</param>
    /// <param name="maxCount">The largest allowed number of items.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfCountOutOfRange<TCollection>(this Guard<TCollection> guard, int minCount, int maxCount, string? message = null)
        where TCollection : IEnumerable
    {
        Argument.NotNegative(minCount);
        Argument.Range(minCount, maxCount);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        var count = Collections.CountUpTo(guard.Value, Above(maxCount));
        if (count < minCount || count > maxCount)
        {
            return guard.Fail(ErrorCodes.CountOutOfRange, message ?? Messages.CountOutOfRange(guard.Name, minCount, maxCount));
        }

        return guard;
    }

    /// <summary>Fails when the collection contains <paramref name="item"/>.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="item">The rejected item.</param>
    /// <param name="comparer">
    /// The comparer. When it is <see langword="null"/>, a collection that implements
    /// <see cref="ICollection{T}"/> uses its own <c>Contains</c> (and its own comparer).
    /// </param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfContains<TCollection, TItem>(this Guard<TCollection> guard, TItem item, IEqualityComparer<TItem>? comparer = null, string? message = null)
        where TCollection : IEnumerable<TItem>
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.Contains(guard.Value, item, comparer))
        {
            return guard.Fail(ErrorCodes.Contains, message ?? Messages.ContainsItem(guard.Name, item));
        }

        return guard;
    }

    /// <summary>Fails when the collection does not contain <paramref name="item"/>.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="item">The required item.</param>
    /// <param name="comparer">
    /// The comparer. When it is <see langword="null"/>, a collection that implements
    /// <see cref="ICollection{T}"/> uses its own <c>Contains</c> (and its own comparer).
    /// </param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfNotContains<TCollection, TItem>(this Guard<TCollection> guard, TItem item, IEqualityComparer<TItem>? comparer = null, string? message = null)
        where TCollection : IEnumerable<TItem>
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Collections.Contains(guard.Value, item, comparer))
        {
            return guard.Fail(ErrorCodes.NotContains, message ?? Messages.NotContainsItem(guard.Name, item));
        }

        return guard;
    }

    /// <summary>Fails when any item in the collection is <see langword="null"/>.</summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfContainsNull<TCollection>(this Guard<TCollection> guard, string? message = null)
        where TCollection : IEnumerable
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.ContainsNull(guard.Value))
        {
            return guard.Fail(ErrorCodes.ContainsNull, message ?? Messages.ContainsNull(guard.Name));
        }

        return guard;
    }

    /// <summary>
    /// Fails when two items in the collection are equal, by their own <c>Equals</c>.
    /// This check allocates a <see cref="HashSet{T}"/>.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TCollection> IfHasDuplicates<TCollection>(this Guard<TCollection> guard, string? message = null)
        where TCollection : IEnumerable
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Collections.HasDuplicates(guard.Value))
        {
            return guard.Fail(ErrorCodes.HasDuplicates, message ?? Messages.Duplicates(guard.Name));
        }

        return guard;
    }

    // One more than `count`, without overflow, so CountUpTo can tell "exactly count"
    // from "more than count".
    private static int Above(int count) => count == int.MaxValue ? count : count + 1;
}
