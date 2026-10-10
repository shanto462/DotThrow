using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// Options for one validation run: which groups and properties to validate, data for the
/// rules, and whether to throw on failure. Pass it to
/// <see cref="Validator{T}.Validate(T, ValidationOptions?)"/> or
/// <see cref="Validator{T}.ValidateAsync(T, ValidationOptions?, System.Threading.CancellationToken)"/>,
/// or set it in a callback with <see cref="Validator{T}.Validate(T, Action{ValidationOptions{T}})"/>.
/// This is DotThrow's version of FluentValidation's <c>ValidationStrategy</c>.
/// </summary>
/// <remarks>
/// Options are read at the start of a run. <see cref="Items"/> is shared with the rules and
/// may change during the run, so do not share one options object between runs on several threads.
/// </remarks>
/// <example>
/// <code>
/// var report = validator.Validate(order, new ValidationOptions
/// {
///     Groups = ["checkout"],
///     Properties = ["Email", "Lines[].Quantity"],
/// });
/// </code>
/// </example>
public class ValidationOptions
{
    private IDictionary<string, object?>? _items;

    /// <summary>
    /// Gets or sets the groups to run. <see langword="null"/> or empty runs the rules in
    /// <see cref="ValidationGroups.Default"/>. Use <see cref="ValidationGroups.All"/> to run
    /// every rule. Group names ignore case. Child validators get the same groups.
    /// </summary>
    public IReadOnlyList<string>? Groups { get; set; }

    /// <summary>
    /// Gets or sets the property paths to validate, or <see langword="null"/> for all.
    /// A path selects its own rules, the rules of its children (<c>"Address"</c> runs
    /// <c>"Address.City"</c>) and the rules on the way to it (<c>"Address.City"</c> runs the
    /// <c>"Address"</c> child rule). Use <c>[]</c> for every item of a collection, for example
    /// <c>"Lines[].Quantity"</c>, or an index such as <c>"Lines[2].Quantity"</c>.
    /// Rules about the whole object do not run when this is set; rules added with
    /// <c>Include</c> are filtered rule by rule.
    /// </summary>
    public IReadOnlyList<string>? Properties { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the run throws when any rule fails. The
    /// exception comes from <see cref="Validator{T}.CreateValidationException(ValidationReport)"/>,
    /// a <see cref="ValidationFailedException"/> by default.
    /// </summary>
    public bool ThrowOnFailures { get; set; }

    /// <summary>
    /// Gets or sets the class cascade for this run, or <see langword="null"/> to use
    /// <see cref="Validator{T}.ClassCascade"/>. It applies to every validator in the run,
    /// child validators included.
    /// </summary>
    public CascadeMode? ClassCascade { get; set; }

    /// <summary>
    /// Gets or sets a custom rule filter (FluentValidation's <c>UseCustomSelector</c>). It runs
    /// for every rule, child validators included, together with <see cref="Groups"/> and
    /// <see cref="Properties"/>. When it is set and no groups are, rules in named groups are not
    /// excluded by default: the selector decides, as in FluentValidation.
    /// </summary>
    public IRuleSelector? RuleSelector { get; set; }

    /// <summary>
    /// Gets data shared with every rule of the run, child validators included, through
    /// <see cref="ValidationContext.Items"/>. Rules may also add items, and the caller can
    /// read them after the run.
    /// </summary>
    public IDictionary<string, object?> Items => _items ??= new Dictionary<string, object?>(StringComparer.Ordinal);

    internal IDictionary<string, object?>? ItemsOrNull => _items;

    /// <summary>Adds groups to run (FluentValidation's <c>IncludeRuleSets</c>).</summary>
    /// <param name="groups">The group names.</param>
    /// <returns>These options.</returns>
    public ValidationOptions IncludeGroups(params string[] groups)
    {
        Argument.NotNull(groups);
        Groups = Append(Groups, groups);
        return this;
    }

    /// <summary>Runs every rule, whatever its group (FluentValidation's <c>IncludeAllRuleSets</c>).</summary>
    /// <returns>These options.</returns>
    public ValidationOptions IncludeAllGroups() => IncludeGroups(ValidationGroups.All);

    /// <summary>Runs the rules without a group too (FluentValidation's <c>IncludeRulesNotInRuleSet</c>).</summary>
    /// <returns>These options.</returns>
    public ValidationOptions IncludeDefaultGroup() => IncludeGroups(ValidationGroups.Default);

    /// <summary>Adds property paths to validate (FluentValidation's <c>IncludeProperties</c>).</summary>
    /// <param name="paths">The property paths, for example <c>"Address.City"</c> or <c>"Lines[].Quantity"</c>.</param>
    /// <returns>These options.</returns>
    public ValidationOptions IncludeProperties(params string[] paths)
    {
        Argument.NotNull(paths);
        Properties = Append(Properties, paths);
        return this;
    }

    /// <summary>Sets <see cref="RuleSelector"/> (FluentValidation's <c>UseCustomSelector</c>).</summary>
    /// <param name="selector">The rule filter.</param>
    /// <returns>These options.</returns>
    public ValidationOptions UseRuleSelector(IRuleSelector selector)
    {
        Argument.NotNull(selector);
        RuleSelector = selector;
        return this;
    }

    internal void ShareItems(IDictionary<string, object?> items) => _items = items;

    private protected static string[] Append(IReadOnlyList<string>? current, string[] more)
    {
        var count = current?.Count ?? 0;
        var result = new string[count + more.Length];
        for (var i = 0; i < count; i++)
        {
            result[i] = current![i];
        }

        Array.Copy(more, 0, result, count, more.Length);
        return result;
    }
}

/// <summary>
/// <see cref="ValidationOptions"/> for a validator of <typeparamref name="T"/>, which can name
/// properties with selectors instead of strings. Every method returns these options.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <example><c>validator.Validate(order, o =&gt; o.IncludeProperties(x =&gt; x.Customer!.Email).IncludeGroups("checkout"));</c></example>
public sealed class ValidationOptions<T> : ValidationOptions
{
    /// <summary>
    /// Adds a property to validate, named from the selector source text the same way as rule names
    /// (FluentValidation's <c>IncludeProperties(x =&gt; x.Name)</c>; no expression trees, AOT safe).
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="property">Selects the property, for example <c>x =&gt; x.Address.City</c>. It is not called.</param>
    /// <param name="propertyExpression">The selector source text, filled in by the compiler.</param>
    /// <returns>These options.</returns>
    public ValidationOptions<T> IncludeProperties<TProperty>(
        Func<T, TProperty> property,
        [CallerArgumentExpression(nameof(property))] string? propertyExpression = null)
    {
        Argument.NotNull(property);
        base.IncludeProperties(RuleNames.Path(typeof(T), propertyExpression));
        return this;
    }

    /// <inheritdoc cref="ValidationOptions.IncludeGroups(string[])"/>
    public new ValidationOptions<T> IncludeGroups(params string[] groups)
    {
        base.IncludeGroups(groups);
        return this;
    }

    /// <inheritdoc cref="ValidationOptions.IncludeAllGroups"/>
    public new ValidationOptions<T> IncludeAllGroups()
    {
        base.IncludeAllGroups();
        return this;
    }

    /// <inheritdoc cref="ValidationOptions.IncludeDefaultGroup"/>
    public new ValidationOptions<T> IncludeDefaultGroup()
    {
        base.IncludeDefaultGroup();
        return this;
    }

    /// <inheritdoc cref="ValidationOptions.IncludeProperties(string[])"/>
    public new ValidationOptions<T> IncludeProperties(params string[] paths)
    {
        base.IncludeProperties(paths);
        return this;
    }

    /// <inheritdoc cref="ValidationOptions.UseRuleSelector(IRuleSelector)"/>
    public new ValidationOptions<T> UseRuleSelector(IRuleSelector selector)
    {
        base.UseRuleSelector(selector);
        return this;
    }

    /// <summary>Sets <see cref="ValidationOptions.ThrowOnFailures"/> (FluentValidation's <c>ThrowOnFailures()</c>).</summary>
    /// <returns>These options.</returns>
    public ValidationOptions<T> ThrowOnFailure()
    {
        ThrowOnFailures = true;
        return this;
    }

    internal static ValidationOptions<T> Create(Action<ValidationOptions<T>> configure)
    {
        Argument.NotNull(configure);
        var options = new ValidationOptions<T>();
        configure(options);
        return options;
    }
}
