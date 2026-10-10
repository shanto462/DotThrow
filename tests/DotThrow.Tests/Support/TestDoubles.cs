using System;
using System.Collections;
using System.Collections.Generic;

namespace DotThrow.Tests.Support;

/// <summary>
/// A lazy sequence that counts how far it was enumerated and whether its enumerator was
/// disposed. With no count it never ends.
/// </summary>
public sealed class CountingSequence<T> : IEnumerable<T>
{
    private readonly Func<int, T> _item;
    private readonly int? _count;

    public CountingSequence(Func<int, T> item, int? count = null)
    {
        _item = item;
        _count = count;
    }

    /// <summary>Gets the number of items handed out so far, over all enumerations.</summary>
    public int ItemsRead { get; private set; }

    public int Enumerations { get; private set; }

    public int Disposals { get; private set; }

    public IEnumerator<T> GetEnumerator()
    {
        Enumerations++;
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Enumerator(CountingSequence<T> owner) : IEnumerator<T>
    {
        private int _index = -1;

        public T Current { get; private set; } = default!;

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (owner._count is { } count && _index + 1 >= count)
            {
                return false;
            }

            _index++;
            owner.ItemsRead++;
            Current = owner._item(_index);
            return true;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() => owner.Disposals++;
    }
}

/// <summary>
/// A read-only collection that knows its count and fails the test if anything enumerates it.
/// It implements only <see cref="IReadOnlyCollection{T}"/>, not the non-generic ICollection.
/// </summary>
public sealed class CountOnlyCollection<T>(int count) : IReadOnlyCollection<T>
{
    public int Count { get; } = count;

    public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("The collection must not be enumerated.");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

#if NET8_0_OR_GREATER
/// <summary>A clock with a fixed time and a chosen local time zone.</summary>
public sealed class FakeTimeProvider(DateTimeOffset utcNow, TimeZoneInfo? localTimeZone = null) : TimeProvider
{
    /// <summary>A time zone five hours ahead of UTC with no daylight saving time.</summary>
    public static readonly TimeZoneInfo PlusFive =
        TimeZoneInfo.CreateCustomTimeZone("Test+05", TimeSpan.FromHours(5), "Test+05", "Test+05");

    public override TimeZoneInfo LocalTimeZone => localTimeZone ?? TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => utcNow;
}
#endif
