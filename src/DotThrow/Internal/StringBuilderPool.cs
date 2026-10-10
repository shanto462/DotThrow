using System;
using System.Text;

namespace DotThrow.Internal;

// One cached StringBuilder per thread for building messages, so a failed check allocates
// only the final message string. A nested use (a value whose ToString fails a check) finds
// the cache empty and gets a new builder, so it is safe.
internal static class StringBuilderPool
{
    private const int MaxCachedCapacity = 1024;

    [ThreadStatic]
    private static StringBuilder? _cached;

    public static StringBuilder Rent()
    {
        var builder = _cached;
        if (builder is null)
        {
            return new StringBuilder(128);
        }

        _cached = null;
        builder.Clear();
        return builder;
    }

    public static string ToStringAndReturn(StringBuilder builder)
    {
        var text = builder.ToString();
        if (builder.Capacity <= MaxCachedCapacity)
        {
            _cached = builder;
        }

        return text;
    }
}
