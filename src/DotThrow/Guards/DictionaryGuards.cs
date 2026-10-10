using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

// Key checks for dictionaries. There are overloads for Dictionary<K, V>,
// IDictionary<K, V> and IReadOnlyDictionary<K, V>, and a fallback for any non-generic
// IDictionary (ConcurrentDictionary, SortedDictionary, ImmutableDictionary, FrozenDictionary,
// Hashtable...). A null dictionary fails with the "Null" code; a null key is a programming error.
// The default message shows the key only for value-type keys (templates "ContainsKey.Value"
// and "NotContainsKey.Value"): a string key may be a secret, such as a session token.
public static partial class GuardExtensions
{
    /// <summary>Fails when the dictionary contains <paramref name="key"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The rejected key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<Dictionary<TKey, TValue>> IfContainsKey<TKey, TValue>(this Guard<Dictionary<TKey, TValue>> guard, TKey key, string? message = null)
        where TKey : notnull
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || value.ContainsKey(key)))
        {
            return KeyFailed(guard, ErrorCodes.ContainsKey, key, message);
        }

        return guard;
    }

    /// <summary>Fails when the dictionary does not contain <paramref name="key"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The required key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<Dictionary<TKey, TValue>> IfNotContainsKey<TKey, TValue>(this Guard<Dictionary<TKey, TValue>> guard, TKey key, string? message = null)
        where TKey : notnull
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || !value.ContainsKey(key)))
        {
            return KeyFailed(guard, ErrorCodes.NotContainsKey, key, message);
        }

        return guard;
    }

    /// <summary>Fails when the dictionary contains <paramref name="key"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The rejected key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<IDictionary<TKey, TValue>> IfContainsKey<TKey, TValue>(this Guard<IDictionary<TKey, TValue>> guard, TKey key, string? message = null)
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || value.ContainsKey(key)))
        {
            return KeyFailed(guard, ErrorCodes.ContainsKey, key, message);
        }

        return guard;
    }

    /// <summary>Fails when the dictionary does not contain <paramref name="key"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The required key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<IDictionary<TKey, TValue>> IfNotContainsKey<TKey, TValue>(this Guard<IDictionary<TKey, TValue>> guard, TKey key, string? message = null)
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || !value.ContainsKey(key)))
        {
            return KeyFailed(guard, ErrorCodes.NotContainsKey, key, message);
        }

        return guard;
    }

    /// <summary>Fails when the dictionary contains <paramref name="key"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The rejected key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<IReadOnlyDictionary<TKey, TValue>> IfContainsKey<TKey, TValue>(this Guard<IReadOnlyDictionary<TKey, TValue>> guard, TKey key, string? message = null)
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || value.ContainsKey(key)))
        {
            return KeyFailed(guard, ErrorCodes.ContainsKey, key, message);
        }

        return guard;
    }

    /// <summary>Fails when the dictionary does not contain <paramref name="key"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The required key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<IReadOnlyDictionary<TKey, TValue>> IfNotContainsKey<TKey, TValue>(this Guard<IReadOnlyDictionary<TKey, TValue>> guard, TKey key, string? message = null)
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || !value.ContainsKey(key)))
        {
            return KeyFailed(guard, ErrorCodes.NotContainsKey, key, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the dictionary contains <paramref name="key"/>. This overload takes any
    /// non-generic <see cref="IDictionary"/>, such as a <c>ConcurrentDictionary</c>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The rejected key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<TDictionary> IfContainsKey<TDictionary>(this Guard<TDictionary> guard, object key, string? message = null)
        where TDictionary : IDictionary
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || value.Contains(key)))
        {
            return KeyFailed(guard, ErrorCodes.ContainsKey, key, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the dictionary does not contain <paramref name="key"/>. This overload takes
    /// any non-generic <see cref="IDictionary"/>, such as a <c>ConcurrentDictionary</c>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="key">The required key.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Key}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<TDictionary> IfNotContainsKey<TDictionary>(this Guard<TDictionary> guard, object key, string? message = null)
        where TDictionary : IDictionary
    {
        Argument.NotNull(key);
        var value = guard.Value;
        if (guard.IsActive && (value is null || !value.Contains(key)))
        {
            return KeyFailed(guard, ErrorCodes.NotContainsKey, key, message);
        }

        return guard;
    }

    // Shows the key in the default message only when its type is a value type. For the
    // object overloads the runtime type decides.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Guard<T> KeyFailed<T, TKey>(Guard<T> guard, string code, TKey key, string? message)
    {
        var showKey = typeof(TKey).IsValueType || key is System.ValueType;
        var variant = code == ErrorCodes.ContainsKey ? TemplateKeys.ContainsKeyValue : TemplateKeys.NotContainsKeyValue;
        return Rejection.Content(guard, FailureKind.Argument, code, message, Placeholder.Key, key, showKey ? variant : null);
    }
}
