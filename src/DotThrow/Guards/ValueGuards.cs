using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// Checks for booleans, GUIDs and enums.
public static partial class GuardExtensions
{
    /// <summary>Fails when the value is <see langword="true"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<bool> IfTrue(this Guard<bool> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value)
        {
            return guard.Fail(ErrorCodes.True, message ?? Messages.True(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is <see langword="false"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<bool> IfFalse(this Guard<bool> guard, string? message = null)
    {
        if (guard.IsActive && !guard.Value)
        {
            return guard.Fail(ErrorCodes.False, message ?? Messages.False(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is <see cref="Guid.Empty"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<Guid> IfEmpty(this Guard<Guid> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value == Guid.Empty)
        {
            return guard.Fail(ErrorCodes.Empty, message ?? Messages.EmptyGuid(guard.Name));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is not a named member of <typeparamref name="TEnum"/>, for example
    /// <c>(Color)42</c>. A combination of <c>[Flags]</c> values fails unless it has a name.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<TEnum> IfNotDefined<TEnum>(this Guard<TEnum> guard, string? message = null)
        where TEnum : struct, Enum
    {
        if (guard.IsActive && !IsDefined(guard.Value))
        {
            return guard.FailOutOfRange(ErrorCodes.NotDefined, message ?? Messages.NotDefined(guard.Name, typeof(TEnum)));
        }

        return guard;
    }

#if NET8_0_OR_GREATER
    private static bool IsDefined<TEnum>(TEnum value)
        where TEnum : struct, Enum => Enum.IsDefined(value);
#else
    private static bool IsDefined<TEnum>(TEnum value)
        where TEnum : struct, Enum => Enum.IsDefined(typeof(TEnum), value);
#endif
}
