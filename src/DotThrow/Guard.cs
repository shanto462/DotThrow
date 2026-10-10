using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// A value under check, with the name used in error messages. Create one with
/// <see cref="ThrowExtensions.Throw{T}(T, string?)"/> to throw on the first failed check,
/// or with <see cref="ValidationReport.Check{T}(T, string?)"/> to collect failures instead.
/// </summary>
/// <typeparam name="T">The type of the checked value.</typeparam>
/// <remarks>
/// <para>
/// Every check returns the guard, so checks chain, and the guard converts implicitly
/// to <typeparamref name="T"/>, so a chain can be assigned directly:
/// <c>Name = name.Throw().IfNullOrWhiteSpace();</c>
/// </para>
/// <para>
/// The guard is a <see langword="ref"/> struct. It never allocates and cannot be stored
/// in a field or captured by a lambda.
/// </para>
/// </remarks>
public readonly ref struct Guard<T>
{
    // null: throw the default exception.
    // Func<ValidationFailure, Exception>: throw a custom exception.
    // ValidationReport: record the failure and continue.
    private readonly object? _sink;
    private readonly GuardState _state;

    internal Guard(T value, string name, object? sink)
        : this(value, name, sink, GuardState.Active)
    {
    }

    private Guard(T value, string name, object? sink, GuardState state)
    {
        Value = value;
        Name = name;
        _sink = sink;
        _state = state;
    }

    /// <summary>Gets the checked value.</summary>
    public T Value { get; }

    /// <summary>
    /// Gets the name used in error messages: the parameter name, a property path, or the
    /// expression that produced the value.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether later checks still run. It is <see langword="false"/>
    /// after a check failed in collect mode (so one value reports one failure), or after
    /// <see cref="AllowNull"/> found a <see langword="null"/> value.
    /// </summary>
    /// <remarks>Custom checks should return the guard unchanged when this is <see langword="false"/>.</remarks>
    public bool IsActive => _state == GuardState.Active;

    /// <summary>Gets a value indicating whether a check on this value failed in collect mode.</summary>
    public bool HasFailed => _state == GuardState.Failed;

    /// <summary>Returns the checked value.</summary>
    /// <param name="guard">The guard.</param>
    public static implicit operator T(Guard<T> guard) => guard.Value;

    /// <summary>
    /// Lets a <see langword="null"/> value pass every later check in the chain. A non-null
    /// value is still checked as usual.
    /// </summary>
    /// <returns>The guard, stopped when the value is <see langword="null"/>.</returns>
    /// <example><c>middleName.Throw().AllowNull().IfLongerThan(50);</c></example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Guard<T> AllowNull() =>
        IsActive && Value is null ? new Guard<T>(Value, Name, _sink, GuardState.Skipped) : this;

    /// <summary>
    /// Reports a failed check. In throw mode this throws an <see cref="ArgumentException"/>
    /// (or the custom exception of the chain). In collect mode it records the failure and
    /// returns a guard that skips later checks.
    /// </summary>
    /// <param name="errorCode">A stable code for the failure, for example <c>"NotSlug"</c>.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The guard, marked as failed (collect mode only).</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    public Guard<T> Fail(string errorCode, string message) => Report(FailureKind.Argument, errorCode, message);

    /// <summary>
    /// Reports a failed check like <see cref="Fail"/>, but the default exception is an
    /// <see cref="ArgumentOutOfRangeException"/> that carries the actual value.
    /// </summary>
    /// <param name="errorCode">A stable code for the failure, for example <c>"TooLarge"</c>.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The guard, marked as failed (collect mode only).</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    public Guard<T> FailOutOfRange(string errorCode, string message) => Report(FailureKind.OutOfRange, errorCode, message);

    /// <summary>
    /// Reports a <see langword="null"/> value. The default exception is an
    /// <see cref="ArgumentNullException"/> and the error code is <c>"Null"</c>.
    /// </summary>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard, marked as failed (collect mode only).</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    public Guard<T> FailNull(string? message = null) =>
        Report(FailureKind.Null, ErrorCodes.Null, message ?? Messages.Null(Name));

    // Moves the chain to another value of a different type, keeping the name, the mode
    // and the state. Used to unwrap Nullable<T>.
    internal Guard<TOther> Map<TOther>(TOther value) => new(value, Name, _sink, _state);

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string ToString() => Value?.ToString() ?? string.Empty;

    [StackTraceHidden]
    private Guard<T> Report(FailureKind kind, string errorCode, string message)
    {
        if (_sink is ValidationReport report)
        {
            report.Add(new ValidationFailure(Name, message, errorCode));
            return new Guard<T>(Value, Name, _sink, GuardState.Failed);
        }

        throw CreateException(kind, errorCode, message);
    }

    private Exception CreateException(FailureKind kind, string errorCode, string message)
    {
        if (_sink is Func<ValidationFailure, Exception> factory
            && factory(new ValidationFailure(Name, message, errorCode)) is { } custom)
        {
            return custom;
        }

        return kind switch
        {
            FailureKind.Null => new ArgumentNullException(Name, message),
            FailureKind.OutOfRange => new ArgumentOutOfRangeException(Name, ActualValue(), message),
            _ => new ArgumentException(message, Name),
        };
    }

    // Value types are safe to show. Reference types (strings above all) may hold secrets,
    // so they are left out of exception messages.
    private object? ActualValue() => typeof(T).IsValueType ? Value : null;
}

/// <summary>
/// A chain of checks for one value, used by <see cref="Validator{T}"/> rules.
/// </summary>
/// <typeparam name="T">The type of the checked value.</typeparam>
/// <param name="guard">The guard to run the checks on.</param>
/// <returns>The guard returned by the last check.</returns>
/// <example><c>email => email.IfNullOrWhiteSpace().IfNotEmailAddress()</c></example>
public delegate Guard<T> GuardChain<T>(Guard<T> guard);

internal enum GuardState : byte
{
    Active,
    Failed,
    Skipped,
}

internal enum FailureKind : byte
{
    Argument,
    Null,
    OutOfRange,
}
