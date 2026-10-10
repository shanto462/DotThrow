using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// IfEmpty and IfNotEmpty on any type, with FluentValidation's NotEmpty and Empty meaning
// of "empty".
public static partial class GuardExtensions
{
    /// <summary>
    /// Fails when the value is empty, like FluentValidation's <c>NotEmpty</c>: an empty or
    /// white-space string, a collection without items, a seekable stream of length 0, or the
    /// default value of a value type (<c>0</c>, <see cref="System.Guid.Empty"/>, ...).
    /// A <see langword="null"/> value fails with the <see cref="ErrorCodes.Null"/> code.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A nullable value type with a value is not empty, even when the value is <c>0</c>
    /// (as in FluentValidation). A collection that knows its count is not enumerated; any
    /// other sequence is enumerated up to its first item.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfEmpty<T>(this Guard<T> guard, string? message = null)
    {
        var value = guard.Value;
        if (guard.IsActive && (value is null || Emptiness.IsEmpty(value)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Empty, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is not empty, like FluentValidation's <c>Empty</c>. A
    /// <see langword="null"/> value, an empty or white-space string, an empty collection or
    /// stream, and the default value of a value type all pass.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNotEmpty<T>(this Guard<T> guard, string? message = null)
    {
        var value = guard.Value;
        if (guard.IsActive && value is not null && !Emptiness.IsEmpty(value))
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.NotEmpty, message);
        }

        return guard;
    }
}
