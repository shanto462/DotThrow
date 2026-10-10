using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// The built-in checks. Each check returns the guard so checks can be chained, and
/// each one takes an optional <c>message</c> that replaces the default message.
/// </summary>
/// <remarks>
/// <para>
/// A default message comes from the template for the error code (see
/// <see cref="MessageTemplates"/>). A custom message can use the same placeholders, for
/// example <c>"{PropertyName} must be at most {MaxLength} characters."</c>. Each check
/// lists the placeholders it fills in; <c>{PropertyName}</c> and <c>{PropertyValue}</c>
/// work everywhere.
/// </para>
/// <para>
/// A wrong argument to a check itself, such as a <see langword="null"/> predicate or a NaN
/// limit, is a programming error: it throws an <see cref="ArgumentException"/> even in
/// collect mode. Some of these are detected only when the check runs on an active guard
/// (for example a maximum below the minimum makes every value fail, and is reported then).
/// </para>
/// </remarks>
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
            return Rejection.Fail(guard, FailureKind.Null, ErrorCodes.Null, message);
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
            return Rejection.Fail(guard, FailureKind.Null, ErrorCodes.Null, message).Map(default(T));
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
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.NotNull, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is the default value of its type: <see langword="null"/>, zero,
    /// <see cref="Guid.Empty"/>, <see cref="DateTime.MinValue"/> and so on. A
    /// <see langword="null"/> value throws an <see cref="ArgumentNullException"/> (still with
    /// the <see cref="ErrorCodes.Default"/> code).
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
            return Rejection.Fail(guard, guard.Value is null ? FailureKind.Null : FailureKind.Argument, ErrorCodes.Default, message);
        }

        return guard;
    }

    /// <summary>Fails when the value equals <paramref name="other"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The rejected value.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// The default message shows <paramref name="other"/> only for value types (template
    /// <c>"EqualTo.Value"</c>). A string or other reference type may be a secret, such as an
    /// old password, so the default message leaves it out.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfEqualTo<T>(this Guard<T> guard, T other, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        if (guard.IsActive && (comparer is null ? EqualityComparer<T>.Default.Equals(guard.Value, other) : comparer.Equals(guard.Value, other)))
        {
            return Rejection.Fail(
                guard, FailureKind.Argument, ErrorCodes.EqualTo, message, Placeholder.ComparisonValue, other, typeof(T).IsValueType ? TemplateKeys.EqualToValue : null);
        }

        return guard;
    }

    /// <summary>Fails when the value does not equal <paramref name="other"/>.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The required value.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// The default message shows <paramref name="other"/> only for value types (template
    /// <c>"NotEqualTo.Value"</c>). A string or other reference type may be a secret, such as
    /// a password to confirm, so the default message leaves it out.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> IfNotEqualTo<T>(this Guard<T> guard, T other, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        if (guard.IsActive && !(comparer is null ? EqualityComparer<T>.Default.Equals(guard.Value, other) : comparer.Equals(guard.Value, other)))
        {
            return Rejection.Fail(
                guard, FailureKind.Argument, ErrorCodes.NotEqualTo, message, Placeholder.ComparisonValue, other, typeof(T).IsValueType ? TemplateKeys.NotEqualToValue : null);
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
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.OneOf, message);
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
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.NotOneOf, message);
        }

        return guard;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Fails when the value is one of <paramref name="values"/>. With C# 13 or later the
    /// compiler picks this overload for a list of values, for example <c>IfOneOf(1, 2, 3)</c>,
    /// and passes them on the stack, so the check does not allocate. Available on .NET 8 and later.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The rejected values.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfOneOf<T>(this Guard<T> guard, params ReadOnlySpan<T> values) =>
        guard.IfOneOf(values, comparer: null, message: null);

    /// <summary>
    /// Fails when the value is one of <paramref name="values"/>. A collection expression such
    /// as <c>IfOneOf([1, 2, 3], message: "...")</c> binds here and does not allocate.
    /// Available on .NET 8 and later.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The rejected values.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfOneOf<T>(this Guard<T> guard, scoped ReadOnlySpan<T> values, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        if (guard.IsActive && Collections.Contains(values, guard.Value, comparer))
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.OneOf, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is not one of <paramref name="values"/>. With C# 13 or later the
    /// compiler picks this overload for a list of values, for example <c>IfNotOneOf(1, 2, 3)</c>,
    /// and passes them on the stack, so the check does not allocate. Available on .NET 8 and later.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The allowed values.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfNotOneOf<T>(this Guard<T> guard, params ReadOnlySpan<T> values) =>
        guard.IfNotOneOf(values, comparer: null, message: null);

    /// <summary>
    /// Fails when the value is not one of <paramref name="values"/>. A collection expression
    /// such as <c>IfNotOneOf([1, 2, 3], message: "...")</c> binds here and does not allocate.
    /// Available on .NET 8 and later.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="values">The allowed values.</param>
    /// <param name="comparer">The comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<T> IfNotOneOf<T>(this Guard<T> guard, scoped ReadOnlySpan<T> values, IEqualityComparer<T>? comparer = null, string? message = null)
    {
        if (guard.IsActive && !Collections.Contains(values, guard.Value, comparer))
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.NotOneOf, message);
        }

        return guard;
    }
#endif

    /// <summary>Fails when <paramref name="predicate"/> returns <see langword="true"/> for the value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="predicate">The condition that rejects the value.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Condition}</c> (the predicate source text).</param>
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
            return Rejection.Fail(
                guard, FailureKind.Argument, ErrorCodes.Condition, message, Placeholder.Condition, predicateExpression, ConditionKey(predicateExpression, TemplateKeys.ConditionIf));
        }

        return guard;
    }

    /// <summary>Fails when <paramref name="predicate"/> returns <see langword="false"/> for the value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="predicate">The condition that the value must meet.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Condition}</c> (the predicate source text).</param>
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
            return Rejection.Fail(
                guard, FailureKind.Argument, ErrorCodes.Condition, message, Placeholder.Condition, predicateExpression, ConditionKey(predicateExpression, TemplateKeys.ConditionIfNot));
        }

        return guard;
    }

    /// <summary>Fails when the value is an instance of <paramref name="type"/> or a type derived from it.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="type">The rejected type.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Type}</c>.</param>
    /// <returns>The guard.</returns>
    /// <example><c>stream.Throw().IfType(typeof(BufferedStream));</c></example>
    public static Guard<T> IfType<T>(this Guard<T> guard, Type type, string? message = null)
    {
        Argument.NotNull(type);
        if (guard.IsActive && type.IsInstanceOfType(guard.Value))
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.Type, message, Placeholder.Type, type.Name);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the value is not an instance of <paramref name="type"/> or a type derived
    /// from it. A <see langword="null"/> value fails with an <see cref="ArgumentNullException"/>
    /// (still with the <see cref="ErrorCodes.NotType"/> code).
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="type">The required type.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Type}</c>.</param>
    /// <returns>The guard.</returns>
    /// <example><c>handler.Throw().IfNotType(typeof(IDisposable));</c></example>
    public static Guard<T> IfNotType<T>(this Guard<T> guard, Type type, string? message = null)
    {
        Argument.NotNull(type);
        if (guard.IsActive && !type.IsInstanceOfType(guard.Value))
        {
            return Rejection.Fail(
                guard, guard.Value is null ? FailureKind.Null : FailureKind.Argument, ErrorCodes.NotType, message, Placeholder.Type, type.Name);
        }

        return guard;
    }

    private static string? ConditionKey(string? expression, string key) => string.IsNullOrEmpty(expression) ? null : key;
}
