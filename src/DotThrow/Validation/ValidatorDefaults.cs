using System;

namespace DotThrow;

/// <summary>
/// Global defaults for every <see cref="Validator{T}"/>. Set them once at startup, before
/// validators are built and used. They play the role of FluentValidation's
/// <c>ValidatorOptions.Global</c>.
/// </summary>
public static class ValidatorDefaults
{
    private static int _maxDepth = DefaultMaxDepth;
    private static string _propertyChainSeparator = ".";

    private const int DefaultMaxDepth = 256;

    /// <summary>
    /// Gets or sets the rule cascade used when neither the rule nor the validator sets one.
    /// The default is <see cref="CascadeMode.Stop"/>: each rule reports at most one failure.
    /// </summary>
    public static CascadeMode RuleCascade { get; set; } = CascadeMode.Stop;

    /// <summary>
    /// Gets or sets the class cascade used when the validator does not set one. The default is
    /// <see cref="CascadeMode.Continue"/>: every rule runs.
    /// </summary>
    public static CascadeMode ClassCascade { get; set; } = CascadeMode.Continue;

    /// <summary>Gets or sets the severity of rule failures that do not set one. The default is <see cref="Severity.Error"/>.</summary>
    public static Severity Severity { get; set; } = Severity.Error;

    /// <summary>
    /// Gets or sets a hook that turns a property path taken from a selector into the name used in
    /// failures, for example to use camelCase names in API responses. It gets the validated
    /// type and the path (for example <c>"Address.City"</c>) and runs once, when the rule is added.
    /// <see langword="null"/> keeps the path.
    /// </summary>
    /// <example><c>ValidatorDefaults.PropertyNameResolver = (type, path) =&gt; JsonNamingPolicy.CamelCase.ConvertName(path);</c></example>
    public static Func<Type, string, string>? PropertyNameResolver { get; set; }

    /// <summary>
    /// Gets or sets a hook that gives the name used in messages (<c>{PropertyName}</c>) for a
    /// property path, for example from a <c>[Display]</c> attribute. It runs once, when the rule
    /// is added. Returning <see langword="null"/>, or a <see langword="null"/> hook, uses the path.
    /// </summary>
    public static Func<Type, string, string?>? DisplayNameResolver { get; set; }

    /// <summary>
    /// Gets or sets the text between the parts of a property path, for example <c>"Address.City"</c>
    /// with the default <c>"."</c>, or <c>"Address_City"</c> with <c>"_"</c> (FluentValidation's
    /// <c>PropertyChainSeparator</c>). It applies to paths taken from selectors when a rule is
    /// added, to child validator prefixes, and to <see cref="ValidationOptions.Properties"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The value is <see langword="null"/> or empty.</exception>
    public static string PropertyChainSeparator
    {
        get => _propertyChainSeparator;
        set => _propertyChainSeparator = string.IsNullOrEmpty(value)
            ? throw new ArgumentException("The separator must not be empty.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or sets a hook that turns the error code of a failed check into the code stored on
    /// rule failures, for rules without <c>WithErrorCode</c> (FluentValidation's
    /// <c>ErrorCodeResolver</c>). It gets the check code, for example <c>"LongerThan"</c>.
    /// <see langword="null"/> keeps the codes.
    /// </summary>
    /// <example><c>ValidatorDefaults.ErrorCodeResolver = code =&gt; "DT_" + code;</c></example>
    public static Func<string, string>? ErrorCodeResolver { get; set; }

    /// <summary>
    /// Gets or sets a hook called for every failure a rule creates, which may return a changed
    /// failure. <see langword="null"/> keeps failures as they are.
    /// </summary>
    public static Func<ValidationFailure, ValidationFailure>? OnFailureCreated { get; set; }

    /// <summary>
    /// Gets or sets how deep child and included validators may nest in one run. A deeper run
    /// throws an <see cref="InvalidOperationException"/>, which usually means the object graph
    /// has a cycle or validators include each other. The default is 256. Running out of stack space
    /// before that also throws an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public static int MaxDepth
    {
        get => _maxDepth;
        set => _maxDepth = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The depth must be at least 1.");
    }

    /// <summary>Restores every default.</summary>
    public static void Reset()
    {
        RuleCascade = CascadeMode.Stop;
        ClassCascade = CascadeMode.Continue;
        Severity = Severity.Error;
        PropertyNameResolver = null;
        DisplayNameResolver = null;
        OnFailureCreated = null;
        ErrorCodeResolver = null;
        _propertyChainSeparator = ".";
        _maxDepth = DefaultMaxDepth;
    }
}
