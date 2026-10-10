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
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.True, message);
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
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.False, message);
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
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.Empty, message, TemplateKeys.EmptyGuid);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is not a defined value of <typeparamref name="TEnum"/>, for example
    /// <c>(Color)42</c>. For a <c>[Flags]</c> enum any combination of defined flags passes,
    /// including <c>0</c> (no flags), like FluentValidation's <c>IsInEnum</c>.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Type}</c> (the enum name).</param>
    /// <returns>The guard.</returns>
    /// <remarks>The defined values are read once per enum type and cached. The check is AOT safe.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TEnum> IfNotDefined<TEnum>(this Guard<TEnum> guard, string? message = null)
        where TEnum : struct, Enum
    {
        if (guard.IsActive && !Enums.IsDefined(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotDefined, message, Placeholder.Type, typeof(TEnum).Name);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value has every bit of <paramref name="flag"/> set
    /// (<see cref="Enum.HasFlag(Enum)"/>). A <paramref name="flag"/> of <c>0</c> is in every
    /// value, so it always fails.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="flag">The rejected flag or combination of flags.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Flag}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TEnum> IfHasFlag<TEnum>(this Guard<TEnum> guard, TEnum flag, string? message = null)
        where TEnum : struct, Enum
    {
        if (guard.IsActive && HasFlag(guard.Value, flag))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.HasFlag, message, Placeholder.Flag, flag);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value does not have every bit of <paramref name="flag"/> set
    /// (<see cref="Enum.HasFlag(Enum)"/>).
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="flag">The required flag or combination of flags.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Flag}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<TEnum> IfNotHasFlag<TEnum>(this Guard<TEnum> guard, TEnum flag, string? message = null)
        where TEnum : struct, Enum
    {
        if (guard.IsActive && !HasFlag(guard.Value, flag))
        {
            return Rejection.Fail(guard, FailureKind.OutOfRange, ErrorCodes.NotHasFlag, message, Placeholder.Flag, flag);
        }

        return guard;
    }

    // Bit test without boxing on .NET 8 and later (Enum.HasFlag boxes on .NET Framework).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasFlag<TEnum>(TEnum value, TEnum flag)
        where TEnum : struct, Enum
    {
        var bits = Enums.ToBits(flag);
        return (Enums.ToBits(value) & bits) == bits;
    }
}
