using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>One failed check: what failed, why, a stable error code, and optional details.</summary>
public sealed class ValidationFailure
{
    private static readonly IReadOnlyDictionary<string, object?> NoArguments =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    private readonly string? _displayName;
    private readonly int _collectionIndex = -1;

    // The arguments added with AppendArgument (a Dictionary) until MessageArguments is first read,
    // then the full read-only dictionary. One field keeps the failure small.
    private IReadOnlyDictionary<string, object?>? _arguments;

    /// <summary>Initializes a new instance of the <see cref="ValidationFailure"/> class.</summary>
    /// <param name="name">The parameter name, property path or expression that failed. Use an empty string for the whole object.</param>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">A stable code for the failure. See <see cref="ErrorCodes"/>.</param>
    public ValidationFailure(string name, string message, string errorCode)
    {
        Argument.NotNull(name);
        Argument.NotNull(message);
        Argument.NotNull(errorCode);
        Name = name;
        Message = message;
        ErrorCode = errorCode;
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationFailure"/> class with every detail.</summary>
    /// <param name="name">The parameter name, property path or expression that failed. Use an empty string for the whole object.</param>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">A stable code for the failure. See <see cref="ErrorCodes"/>.</param>
    /// <param name="severity">How serious the failure is.</param>
    /// <param name="attemptedValue">The value that failed, or <see langword="null"/>.</param>
    /// <param name="customState">Extra data from the rule, or <see langword="null"/>.</param>
    public ValidationFailure(string name, string message, string errorCode, Severity severity, object? attemptedValue = null, object? customState = null)
        : this(name, message, errorCode)
    {
        Severity = severity;
        AttemptedValue = attemptedValue;
        CustomState = customState;
    }

    // Used by validator rules, whose arguments are never null: skips the argument checks.
    private ValidationFailure(
        string name,
        string message,
        string errorCode,
        Severity severity,
        object? attemptedValue,
        object? customState,
        string? displayName,
        int collectionIndex,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        Name = name;
        Message = message;
        ErrorCode = errorCode;
        Severity = severity;
        AttemptedValue = attemptedValue;
        CustomState = customState;
        _displayName = displayName;
        _collectionIndex = collectionIndex;
        _arguments = arguments;
    }

    /// <summary>
    /// Gets the parameter name, property path or expression that failed, for example
    /// <c>"email"</c>, <c>"Address.City"</c> or <c>"Lines[2].Quantity"</c>. It is empty for
    /// a rule about the whole object.
    /// </summary>
    public string Name { get; }

    /// <summary>Gets the error message.</summary>
    public string Message { get; }

    /// <summary>Gets the stable error code, for example <see cref="ErrorCodes.LongerThan"/>.</summary>
    public string ErrorCode { get; }

    /// <summary>Gets how serious the failure is. The default is <see cref="Severity.Error"/>.</summary>
    public Severity Severity { get; init; }

    /// <summary>
    /// Gets the value that failed the check, or <see langword="null"/> when unknown. It may hold
    /// a string the user typed, such as a password, so do not log it or send it to clients
    /// without care. Messages never include it unless a custom message uses <c>{PropertyValue}</c>.
    /// </summary>
    public object? AttemptedValue { get; init; }

    /// <summary>Gets extra data set by the rule with <c>WithState</c>, or <see langword="null"/>.</summary>
    public object? CustomState { get; init; }

    /// <summary>
    /// Gets the values a rule message can use as placeholders (FluentValidation's
    /// <c>FormattedMessagePlaceholderValues</c>): <c>PropertyName</c>, <c>PropertyPath</c>,
    /// <c>PropertyValue</c>, <c>CollectionIndex</c> for collection items, and the arguments added
    /// with <see cref="ValidationContext.AppendArgument(string, object?)"/>. It is empty for
    /// failures not created by a <see cref="Validator{T}"/> rule. The dictionary is built on first use.
    /// </summary>
    public IReadOnlyDictionary<string, object?> MessageArguments =>
        _arguments is ReadOnlyDictionary<string, object?> built ? built : _arguments = BuildArguments();

    /// <inheritdoc/>
    public override string ToString() => Name.Length == 0 ? Message : $"{Name}: {Message}";

    internal static ValidationFailure Create(string name, string message, string errorCode, Severity severity, object? attemptedValue, object? customState) =>
        new(name, message, errorCode, severity, attemptedValue, customState, null, -1, null);

    internal static ValidationFailure Create(
        string name,
        string message,
        string errorCode,
        Severity severity,
        object? attemptedValue,
        object? customState,
        string displayName,
        int collectionIndex,
        IReadOnlyDictionary<string, object?>? arguments) =>
        new(name, message, errorCode, severity, attemptedValue, customState, displayName, collectionIndex, arguments);

    internal ValidationFailure WithName(string name) =>
        ReferenceEquals(name, Name)
            ? this
            : new ValidationFailure(name, Message, ErrorCode, Severity, AttemptedValue, CustomState, _displayName, _collectionIndex, AppendedArguments());

    // The arguments added with AppendArgument, also after MessageArguments was built (which
    // holds the old path, so a renamed copy must build its own).
    private IReadOnlyDictionary<string, object?>? AppendedArguments()
    {
        if (_arguments is not ReadOnlyDictionary<string, object?> built)
        {
            return _arguments;
        }

        Dictionary<string, object?>? appended = null;
        foreach (var argument in built)
        {
            if (argument.Key is not ("PropertyName" or "PropertyValue" or "PropertyPath" or "CollectionIndex"))
            {
                (appended ??= new Dictionary<string, object?>(StringComparer.Ordinal))[argument.Key] = argument.Value;
            }
        }

        return appended;
    }

    private IReadOnlyDictionary<string, object?> BuildArguments()
    {
        if (_displayName is null && _arguments is null)
        {
            return NoArguments;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_displayName is not null)
        {
            values["PropertyName"] = _displayName;
            values["PropertyValue"] = AttemptedValue;
            values["PropertyPath"] = Name;
        }

        if (_collectionIndex >= 0)
        {
            values["CollectionIndex"] = _collectionIndex;
        }

        if (_arguments is not null)
        {
            foreach (var argument in _arguments)
            {
                values[argument.Key] = argument.Value;
            }
        }

        return new ReadOnlyDictionary<string, object?>(values);
    }

    internal ValidationFailure WithPrefix(string prefix) =>
        prefix.Length == 0 ? this : WithName(PropertyPaths.Join(prefix, Name));
}
