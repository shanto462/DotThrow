using System.Collections.Generic;
using DotThrow.Dynamic.Internal;

namespace DotThrow.Dynamic;

/// <summary>
/// A set of rules described as data, for <see cref="DynamicValidator{T}"/>. It binds from JSON
/// (<see cref="FromJson(string)"/>), from <c>IConfiguration</c> (<c>section.Get&lt;RuleSetDefinition&gt;()</c>),
/// or from code. Every argument is a string, the way configuration gives it, and is converted
/// with the invariant culture when the validator is built.
/// </summary>
/// <example>
/// <code>
/// {
///   "rules": [
///     { "property": "Email", "checks": [ { "check": "WhiteSpace" }, { "check": "NotEmailAddress" } ] },
///     { "property": "Age", "severity": "Warning", "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] },
///     { "property": "Lines[].Quantity", "checks": [ { "check": "OutOfRange", "min": 1, "max": 100 } ] }
///   ]
/// }
/// </code>
/// </example>
public sealed class RuleSetDefinition
{
    /// <summary>Gets or sets the rules, in the order they run.</summary>
    public List<RuleDefinition> Rules { get; set; } = [];

    /// <summary>Gets or sets the rule cascade of the validator, <c>"Stop"</c> (default) or <c>"Continue"</c>.</summary>
    public string? RuleCascade { get; set; }

    /// <summary>Gets or sets the class cascade of the validator, <c>"Continue"</c> (default) or <c>"Stop"</c>.</summary>
    public string? ClassCascade { get; set; }

    /// <summary>
    /// Reads a definition from JSON with DotThrow's own strict reader (RFC 8259, no comments or
    /// trailing commas). Keys ignore case; numbers are kept as their text, so <c>"value": 5</c>
    /// and <c>"value": "5"</c> are the same. Unknown keys and wrong value kinds are errors.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The definition. Check names and arguments are checked later, when a validator is built.</returns>
    /// <exception cref="RuleDefinitionException">The text is not valid JSON or does not have the shape of a definition. Every shape error is listed.</exception>
    public static RuleSetDefinition FromJson(string json)
    {
        DotThrow.Internal.Argument.NotNull(json);
        return DefinitionReader.Read(json);
    }
}

/// <summary>One rule: a property, the checks to run on it, and rule options.</summary>
public sealed class RuleDefinition
{
    /// <summary>
    /// Gets or sets the property path, for example <c>"Email"</c> or <c>"Address.City"</c> (a
    /// <see langword="null"/> part on the way reads as <see langword="null"/>). <c>"Lines[].Sku"</c>
    /// checks a property of every item of a collection (failures are named <c>"Lines[2].Sku"</c>).
    /// Empty or <see langword="null"/> means the object itself. Names ignore case.
    /// </summary>
    public string? Property { get; set; }

    /// <summary>Gets or sets a value indicating whether the checks run on every item of the collection (failures are named <c>"Tags[2]"</c>).</summary>
    public bool Each { get; set; }

    /// <summary>Gets or sets a value indicating whether a <see langword="null"/> value skips the checks (<c>AllowNull()</c>).</summary>
    public bool AllowNull { get; set; }

    /// <summary>Gets or sets the group of the rule (FluentValidation rule set).</summary>
    public string? Group { get; set; }

    /// <summary>Gets or sets several groups of the rule. They add to <see cref="Group"/>.</summary>
    public List<string>? Groups { get; set; }

    /// <summary>Gets or sets a condition that must be true for the rule to run.</summary>
    public ConditionDefinition? When { get; set; }

    /// <summary>Gets or sets a condition that must be false for the rule to run.</summary>
    public ConditionDefinition? Unless { get; set; }

    /// <summary>Gets or sets the severity of the failures: <c>"Error"</c>, <c>"Warning"</c> or <c>"Info"</c>.</summary>
    public string? Severity { get; set; }

    /// <summary>Gets or sets the error code of every failure of the rule (a check's own code wins).</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Gets or sets the message of every failure of the rule, with placeholders such as <c>{PropertyName}</c> (a check's own message wins).</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets the name used in messages (<c>WithName</c>). The failure name stays the property path.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the rule cascade: <c>"Stop"</c> or <c>"Continue"</c>.</summary>
    public string? Cascade { get; set; }

    /// <summary>Gets or sets the checks, in the order they run.</summary>
    public List<CheckDefinition> Checks { get; set; } = [];
}

/// <summary>
/// One check: its name (the error code of the built-in check, for example <c>"LongerThan"</c>,
/// or the name of a check in a <see cref="CheckRegistry"/>) and its arguments as strings.
/// See <see cref="CheckRegistry.BuiltInChecks"/> for every name.
/// </summary>
public sealed class CheckDefinition
{
    /// <summary>Gets or sets the check name, for example <c>"LongerThan"</c>. Names ignore case.</summary>
    public string? Check { get; set; }

    /// <summary>Gets or sets the single argument of the check, for example the maximum length.</summary>
    public string? Value { get; set; }

    /// <summary>Gets or sets the lower bound of a range check.</summary>
    public string? Min { get; set; }

    /// <summary>Gets or sets the upper bound of a range check.</summary>
    public string? Max { get; set; }

    /// <summary>Gets or sets the list argument, for example the allowed values of <c>NotOneOf</c>.</summary>
    public List<string>? Values { get; set; }

    /// <summary>Gets or sets the <see cref="System.StringComparison"/> name for string checks, for example <c>"OrdinalIgnoreCase"</c>.</summary>
    public string? Comparison { get; set; }

    /// <summary>
    /// Gets or sets extra options: <c>RegexOptions</c> names for <c>Matches</c>/<c>NotMatches</c>
    /// (<c>"IgnoreCase, Multiline"</c>), a <c>UriKind</c> for <c>NotUri</c>, <c>"IgnoreCase"</c> for
    /// <c>NotEnumName</c>, <c>"IgnoreTrailingZeros"</c> for <c>PrecisionScaleExceeded</c>.
    /// </summary>
    public string? Options { get; set; }

    /// <summary>Gets or sets the message of this check (the <c>message</c> argument of the check method).</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets the error code of failures of this check.</summary>
    public string? ErrorCode { get; set; }
}

/// <summary>
/// A condition on a property of the validated object, for <see cref="RuleDefinition.When"/> and
/// <see cref="RuleDefinition.Unless"/>. Every part that is set must hold.
/// </summary>
public sealed class ConditionDefinition
{
    /// <summary>Gets or sets the property path, relative to the validated object.</summary>
    public string? Property { get; set; }

    /// <summary>Gets or sets a value the property must equal (converted to the property type).</summary>
    public string? EqualTo { get; set; }

    /// <summary>Gets or sets a value the property must not equal (converted to the property type).</summary>
    public string? NotEqualTo { get; set; }

    /// <summary>Gets or sets whether the property must be <see langword="null"/> (<see langword="true"/>) or not (<see langword="false"/>).</summary>
    public bool? IsNull { get; set; }
}
