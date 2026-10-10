using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// The built-in checks. Each check returns the guard so checks can be chained, and
/// each one takes an optional <c>message</c> that replaces the default message.
/// </summary>
public static partial class GuardExtensions
{
    /// <summary>Fails when the value is <see langword="null"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNull<T>(this Guard<T> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value is null)
        {
            return guard.FailNull(message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the nullable value has no value. Otherwise the chain continues with the
    /// unwrapped value, so <c>int count = maybeCount.Throw().IfNull();</c> compiles.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>A guard for the unwrapped value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNull<T>(this Guard<T?> guard, string? message = null)
        where T : struct
    {
        if (guard.IsActive && !guard.Value.HasValue)
        {
            return guard.FailNull(message).Map(default(T));
        }

        return guard.Map(guard.Value.GetValueOrDefault());
    }

    /// <summary>Fails when the value is not <see langword="null"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNotNull<T>(this Guard<T> guard, string? message = null)
    {
        if (guard.IsActive && guard.Value is not null)
        {
            return guard.Fail(ErrorCodes.NotNull, message ?? Messages.NotNull(guard.Name));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is the default value of its type: <see langword="null"/>, zero,
    /// <see cref="Guid.Empty"/>, <see cref="DateTime.MinValue"/> and so on.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfDefault<T>(this Guard<T> guard, string? message = null)
    {
        if (guard.IsActive && EqualityComparer<T>.Default.Equals(guard.Value, default!))
        {
            return guard.Fail(ErrorCodes.Default, message ?? Messages.Default(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value equals <paramref name="other"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The rejected value.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfEqualTo<T>(this Guard<T> guard, T other, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        if (guard.IsActive && (comparer ?? EqualityComparer<T>.Default).Equals(guard.Value, other))
        {
            return guard.Fail(ErrorCodes.EqualTo, message ?? Messages.EqualTo(guard.Name, other));
        }

        return guard;
    }

    /// <summary>Fails when the value does not equal <paramref name="other"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The required value.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfNotEqualTo<T>(this Guard<T> guard, T other, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        if (guard.IsActive && !(comparer ?? EqualityComparer<T>.Default).Equals(guard.Value, other))
        {
            return guard.Fail(ErrorCodes.NotEqualTo, message ?? Messages.NotEqualTo(guard.Name, other));
        }

        return guard;
    }

    /// <summary>Fails when the value is one of <paramref name="values"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The rejected values.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfOneOf<T>(this Guard<T> guard, params T[] values) =>
        guard.IfOneOf((IEnumerable<T>)values);

    /// <summary>Fails when the value is one of <paramref name="values"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The rejected values.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfOneOf<T>(this Guard<T> guard, IEnumerable<T> values, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        Argument.NotNull(values);
        if (guard.IsActive && Collections.Contains(values, guard.Value, comparer))
        {
            return guard.Fail(ErrorCodes.OneOf, message ?? Messages.OneOf(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the value is not one of <paramref name="values"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The allowed values.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfNotOneOf<T>(this Guard<T> guard, params T[] values) =>
        guard.IfNotOneOf((IEnumerable<T>)values);

    /// <summary>Fails when the value is not one of <paramref name="values"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The allowed values.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfNotOneOf<T>(this Guard<T> guard, IEnumerable<T> values, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        Argument.NotNull(values);
        if (guard.IsActive && !Collections.Contains(values, guard.Value, comparer))
        {
            return guard.Fail(ErrorCodes.NotOneOf, message ?? Messages.NotOneOf(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when <paramref name="predicate"/> returns <see langword="true"/> for the value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="predicate">The condition that rejects the value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <param name="predicateExpression">The predicate source text, filled in by the compiler for the default message.</param>
    /// <returns>The guard.</returns>
    /// <example><c>quantity.Throw().If(q => q % 2 != 0, "Quantity must be even.");</c></example>
    public static Guard<T> If<T>(
        this Guard<T> guard,
        Func<T, bool> predicate,
        string? message = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        Argument.NotNull(predicate);
        if (guard.IsActive && predicate(guard.Value))
        {
            return guard.Fail(ErrorCodes.Condition, message ?? Messages.Condition(guard.Name, predicateExpression));
        }

        return guard;
    }

    /// <summary>Fails when <paramref name="predicate"/> returns <see langword="false"/> for the value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="predicate">The condition that the value must meet.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <param name="predicateExpression">The predicate source text, filled in by the compiler for the default message.</param>
    /// <returns>The guard.</returns>
    /// <example><c>sku.Throw().IfNot(s => s.StartsWith("SKU-"), "Unknown SKU format.");</c></example>
    public static Guard<T> IfNot<T>(
        this Guard<T> guard,
        Func<T, bool> predicate,
        string? message = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        Argument.NotNull(predicate);
        if (guard.IsActive && !predicate(guard.Value))
        {
            return guard.Fail(ErrorCodes.Condition, message ?? Messages.NotCondition(guard.Name, predicateExpression));
        }

        return guard;
    }

    /// <summary>Fails when the value is an instance of <paramref name="type"/> or a type derived from it.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="type">The rejected type.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <example><c>stream.Throw().IfType(typeof(BufferedStream));</c></example>
    public static Guard<T> IfType<T>(this Guard<T> guard, Type type, string? message = null)
    {
        Argument.NotNull(type);
        if (guard.IsActive && type.IsInstanceOfType(guard.Value))
        {
            return guard.Fail(ErrorCodes.Type, message ?? Messages.Type(guard.Name, type));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is not an instance of <paramref name="type"/> or a type derived
    /// from it. A <see langword="null"/> value fails.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="type">The required type.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <example><c>handler.Throw().IfNotType(typeof(IDisposable));</c></example>
    public static Guard<T> IfNotType<T>(this Guard<T> guard, Type type, string? message = null)
    {
        Argument.NotNull(type);
        if (guard.IsActive && !type.IsInstanceOfType(guard.Value))
        {
            return guard.Fail(ErrorCodes.NotType, message ?? Messages.NotType(guard.Name, type));
        }

        return guard;
    }
}
