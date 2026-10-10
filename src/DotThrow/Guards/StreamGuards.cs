using System.IO;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// Checks for streams. They work on Stream and on every derived type (FileStream,
// MemoryStream...). A null stream fails with the "Null" code. A closed stream cannot read,
// write or seek. To reject an empty stream, use IfEmpty(): it fails a seekable stream of
// length 0 (a stream that cannot seek has no known length, so it passes).
public static partial class GuardExtensions
{
    /// <summary>Fails when the stream cannot be read (<see cref="Stream.CanRead"/> is <see langword="false"/>).</summary>
    /// <typeparam name="TStream">The stream type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TStream> IfNotReadable<TStream>(this Guard<TStream> guard, string? message = null)
        where TStream : Stream
    {
        var stream = guard.Value;
        if (guard.IsActive && (stream is null || !stream.CanRead))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotReadable, message);
        }

        return guard;
    }

    /// <summary>Fails when the stream cannot be written (<see cref="Stream.CanWrite"/> is <see langword="false"/>).</summary>
    /// <typeparam name="TStream">The stream type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TStream> IfNotWritable<TStream>(this Guard<TStream> guard, string? message = null)
        where TStream : Stream
    {
        var stream = guard.Value;
        if (guard.IsActive && (stream is null || !stream.CanWrite))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotWritable, message);
        }

        return guard;
    }

    /// <summary>Fails when the stream cannot seek (<see cref="Stream.CanSeek"/> is <see langword="false"/>).</summary>
    /// <typeparam name="TStream">The stream type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TStream> IfNotSeekable<TStream>(this Guard<TStream> guard, string? message = null)
        where TStream : Stream
    {
        var stream = guard.Value;
        if (guard.IsActive && (stream is null || !stream.CanSeek))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotSeekable, message);
        }

        return guard;
    }
}
