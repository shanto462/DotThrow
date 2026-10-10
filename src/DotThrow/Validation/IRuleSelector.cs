namespace DotThrow;

/// <summary>
/// Decides whether a rule runs, for <see cref="ValidationOptions.RuleSelector"/>
/// (FluentValidation's <c>IValidatorSelector</c>).
/// </summary>
/// <example>
/// <code>
/// sealed class OnlyWarnings : IRuleSelector
/// {
///     public bool CanRun(RuleDescriptor rule, string propertyPath, ValidationContext context) =&gt;
///         rule.Severity == Severity.Warning;
/// }
/// </code>
/// </example>
public interface IRuleSelector
{
    /// <summary>Returns <see langword="true"/> to run the rule.</summary>
    /// <param name="rule">What the rule checks.</param>
    /// <param name="propertyPath">The full property path of the rule in this run, for example <c>"Lines[2].Quantity"</c>.</param>
    /// <param name="context">The context of the validator that owns the rule.</param>
    /// <returns><see langword="true"/> to run the rule.</returns>
    bool CanRun(RuleDescriptor rule, string propertyPath, ValidationContext context);
}
