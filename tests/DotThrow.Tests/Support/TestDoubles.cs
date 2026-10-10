using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

/// <summary>A stream with chosen capabilities and length. It holds no data.</summary>
public sealed class CapabilityStream(bool canRead = true, bool canWrite = true, bool canSeek = true, long length = 0) : System.IO.Stream
{
    public override bool CanRead { get; } = canRead;

    public override bool CanWrite { get; } = canWrite;

    public override bool CanSeek { get; } = canSeek;

    public override long Length => CanSeek ? length : throw new NotSupportedException();

    public override long Position { get; set; }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => 0;

    public override long Seek(long offset, System.IO.SeekOrigin origin) => 0;

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
    }

    public override string ToString() => $"stream(read={CanRead}, write={CanWrite}, seek={CanSeek})";
}

/// <summary>Cancels a token source the best way the runtime offers.</summary>
public static class CancellationExtensions
{
    public static Task CancelNowAsync(this CancellationTokenSource source)
    {
#if NET8_0_OR_GREATER
        return source.CancelAsync();
#else
        source.Cancel();
        return Task.CompletedTask;
#endif
    }
}

/// <summary>String helpers with the same meaning on .NET Framework and .NET.</summary>
public static class TextHelpers
{
    public static bool HasChar(this string text, char value)
    {
#if NETCOREAPP2_1_OR_GREATER
        return text.Contains(value);
#else
        return text.IndexOf(value) >= 0;
#endif
    }
}

/// <summary>Lazy sequences: they are not collections, so checks must enumerate them.</summary>
public static class Sequences
{
    public static IEnumerable<T> Of<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
}
