using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// Entry points for guard clauses. Call <c>Throw()</c> on any value, then chain checks.
/// The first failed check throws.
/// </summary>
/// <example>
/// <code>
/// public User(string name, int age)
/// {
///     Name = name.Throw().IfNullOrWhiteSpace().IfLongerThan(50);
///     Age = age.Throw().IfNegative().IfGreaterThan(150);
/// }
/// </code>
/// </example>
public static class ThrowExtensions
{
    /// <summary>
    /// Starts a chain of checks that throws on the first failure. The default exceptions are
    /// <see cref="ArgumentNullException"/>, <see cref="ArgumentOutOfRangeException"/> and
    /// <see cref="ArgumentException"/>, with <see cref="ArgumentException.ParamName"/> set.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">
    /// The name used in error messages. The compiler fills it in with the expression you
    /// call <c>Throw()</c> on, for example <c>"name"</c> or <c>"request.Email"</c>.
    /// </param>
    /// <returns>A guard for the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Throw<T>(this T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        new(value!, paramName ?? DefaultName, sink: null);

    /// <summary>
    /// Starts a chain of checks that throws a custom exception on the first failure.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="exceptionFactory">
    /// Creates the exception to throw from the failure (name, message and error code).
    /// </param>
    /// <param name="paramName">The name used in error messages. The compiler fills it in.</param>
    /// <returns>A guard for the value.</returns>
    /// <example><c>order.Total.Throw(f => new DomainException(f.Message)).IfNegative();</c></example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<T> Throw<T>(
        this T? value,
        Func<ValidationFailure, Exception> exceptionFactory,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        Argument.NotNull(exceptionFactory);
        return new Guard<T>(value!, paramName ?? DefaultName, exceptionFactory);
    }

    internal const string DefaultName = "value";
}
