using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Dynamic.Internal;
using DotThrow.Internal;

namespace DotThrow.Dynamic;

/// <summary>
/// A validator whose rules come from data (JSON, configuration, a database) instead of code.
/// The definition is compiled once into the same rules and guard methods a
/// <see cref="Validator{T}"/> uses, so failures are the same as from code rules. Use it like
/// any <see cref="IValidator{T}"/>: on its own, in dependency injection, or included in a code
/// validator with <see cref="Validator{T}.Include(IValidator{T}, Action{RuleOptions{T, T}}?)"/>.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <remarks>
/// <para>
/// Every problem in a definition (unknown properties or checks, bad arguments, checks that do
/// not apply to the property type) is collected, and a <see cref="RuleDefinitionException"/>
/// lists all of them. <see cref="Update"/> swaps the rules in one step and keeps the old rules
/// when the new definition has errors. Runs that already started finish with the rules they
/// started with.
/// </para>
/// <para>
/// The definition is read when the validator is built or updated; changing the definition
/// object later has no effect until <see cref="Update"/> is called with it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var definition = RuleSetDefinition.FromJson("""
///     { "rules": [
///         { "property": "Email", "checks": [ "NullOrWhiteSpace", { "check": "NotEmailAddress" } ] },
///         { "property": "Age", "checks": [ { "check": "OutOfRange", "min": "18", "max": "120" } ] } ] }
///     """);
/// var properties = new PropertyMap&lt;Person&gt;().Add(x =&gt; x.Email).Add(x =&gt; x.Age);
/// var validator = new DynamicValidator&lt;Person&gt;(definition, properties);
/// var report = validator.Validate(new Person { Email = "ada@example.com", Age = 36 });
/// </code>
/// </example>
public sealed class DynamicValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : IValidator<T>, IChildValidator<T>, IAsyncRuleInfo
{
    private readonly object _gate = new();
    private readonly PropertySource<T> _source;
    private readonly CheckRegistry _registry;
    private State _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicValidator{T}"/> class that finds
    /// properties by name with reflection (public instance properties, nested paths like
    /// <c>"Customer.Email"</c>, and collections). Property getters are compiled into delegates
    /// once. Not for trimmed or Native AOT apps: use the constructor with a <see cref="PropertyMap{T}"/> there.
    /// </summary>
    /// <param name="definition">The rules.</param>
    /// <param name="registry">Custom checks, or <see langword="null"/> for only the built-in checks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="RuleDefinitionException">The definition has errors; <see cref="RuleDefinitionException.Errors"/> lists all of them.</exception>
    [RequiresUnreferencedCode(DynamicMessages.Trimming)]
    [RequiresDynamicCode(DynamicMessages.DynamicCode)]
    public DynamicValidator(RuleSetDefinition definition, CheckRegistry? registry = null)
        : this(new ReflectionSource<T>(), definition, registry)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicValidator{T}"/> class that checks only
    /// the properties in <paramref name="properties"/>. It uses no reflection, so it works with
    /// trimming and Native AOT.
    /// </summary>
    /// <param name="definition">The rules.</param>
    /// <param name="properties">The properties definitions may check.</param>
    /// <param name="registry">Custom checks, or <see langword="null"/> for only the built-in checks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="properties"/> is <see langword="null"/>.</exception>
    /// <exception cref="RuleDefinitionException">The definition has errors; <see cref="RuleDefinitionException.Errors"/> lists all of them.</exception>
    public DynamicValidator(RuleSetDefinition definition, PropertyMap<T> properties, CheckRegistry? registry = null)
        : this(SourceOf(properties), definition, registry)
    {
    }

    private DynamicValidator(PropertySource<T> source, RuleSetDefinition definition, CheckRegistry? registry)
    {
        Argument.NotNull(definition);
        _source = source;
        _registry = registry ?? CheckRegistry.Default;
        var validator = Build(definition, out var errors);
        if (errors.Count > 0)
        {
            throw new RuleDefinitionException(errors);
        }

        _state = new State(validator, definition);
    }

    /// <summary>
    /// Gets the definition of the rules that run now: the object given to the constructor or to
    /// the last successful <see cref="Update"/>. Changing it has no effect until it is passed to
    /// <see cref="Update"/> again.
    /// </summary>
    public RuleSetDefinition Definition => Volatile.Read(ref _state).Definition;

    // The rules and their definition change together, in one write.
    private Validator<T> Current => Volatile.Read(ref _state).Validator;

    /// <summary>
    /// Replaces the rules with the rules of <paramref name="definition"/>, in one step. When the
    /// definition has errors, the old rules stay and nothing changes.
    /// </summary>
    /// <param name="definition">The new rules.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="RuleDefinitionException">The definition has errors; <see cref="RuleDefinitionException.Errors"/> lists all of them.</exception>
    public void Update(RuleSetDefinition definition)
    {
        if (!TryUpdate(definition, out var errors))
        {
            throw new RuleDefinitionException(errors);
        }
    }

    /// <summary>
    /// Replaces the rules with the rules of <paramref name="definition"/> when it has no errors.
    /// </summary>
    /// <param name="definition">The new rules.</param>
    /// <param name="errors">Every error of the definition, or an empty list.</param>
    /// <returns><see langword="true"/> when the rules were replaced; <see langword="false"/> when the definition has errors and the old rules stay.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public bool TryUpdate(RuleSetDefinition definition, out IReadOnlyList<string> errors)
    {
        Argument.NotNull(definition);
        lock (_gate)
        {
            var validator = Build(definition, out errors);
            if (errors.Count > 0)
            {
                return false;
            }

            Volatile.Write(ref _state, new State(validator, definition));
            return true;
        }
    }

    /// <summary>
    /// Checks a definition without using it: returns every error it would have with this
    /// validator's properties and checks.
    /// </summary>
    /// <param name="definition">The definition to check.</param>
    /// <returns>Every error, or an empty list when the definition is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<string> FindErrors(RuleSetDefinition definition)
    {
        Argument.NotNull(definition);
        lock (_gate)
        {
            Build(definition, out var errors);
            return errors;
        }
    }

    /// <summary>Describes the rules that run now, for tools, documentation and tests.</summary>
    /// <returns>A snapshot of the rules.</returns>
    public ValidatorDescriptor Describe()
    {
        var current = Current.Describe();
        return new(GetType(), typeof(T), current.Rules, current.HasAsyncRules);
    }

    bool IAsyncRuleInfo.HasAsyncRules(HashSet<object> visited) => visited.Add(this) && ((IAsyncRuleInfo)Current).HasAsyncRules(visited);

    /// <inheritdoc/>
    public bool CanValidate(Type type)
    {
        Argument.NotNull(type);
        return typeof(T).IsAssignableFrom(type);
    }

    /// <summary>Runs the rules of the given groups and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public ValidationReport Validate(T value, params string[] groups) => Current.Validate(value, groups);

    /// <inheritdoc/>
    [OverloadResolutionPriority(1)]
    public ValidationReport Validate(T value, ValidationOptions? options) => Current.Validate(value, options);

    /// <summary>Runs the rules with options set in a callback.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="configure">Sets the options.</param>
    /// <returns>The report.</returns>
    public ValidationReport Validate(T value, Action<ValidationOptions<T>> configure) => Current.Validate(value, configure);

    /// <summary>Runs the default rules, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public Task<ValidationReport> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
        Current.ValidateAsync(value, cancellationToken);

    /// <summary>Runs the rules of the given groups, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With <see langword="null"/> or no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    public Task<ValidationReport> ValidateAsync(T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default) =>
        Current.ValidateAsync(value, groups, cancellationToken);

    /// <inheritdoc/>
    [OverloadResolutionPriority(1)]
    public Task<ValidationReport> ValidateAsync(T value, ValidationOptions? options, CancellationToken cancellationToken = default) =>
        Current.ValidateAsync(value, options, cancellationToken);

    /// <summary>Runs the rules of the given groups and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public void ValidateAndThrow(T value, params string[] groups) => Current.ValidateAndThrow(value, groups);

    /// <summary>Runs the default rules, including async rules, and throws when any rule fails.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>A task that completes when validation passes.</returns>
    /// <exception cref="ValidationFailedException">At least one rule failed.</exception>
    public Task ValidateAndThrowAsync(T value, CancellationToken cancellationToken = default) =>
        Current.ValidateAndThrowAsync(value, cancellationToken);

    ValidationReport IValidator.Validate(object instance, ValidationOptions? options) =>
        ((IValidator)Current).Validate(instance, options);

    Task<ValidationReport> IValidator.ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken) =>
        ((IValidator)Current).ValidateAsync(instance, options, cancellationToken);

    void IChildValidator<T>.RunAsChild(T instance, in ChildScope scope) =>
        ((IChildValidator<T>)Current).RunAsChild(instance, scope);

    Task IChildValidator<T>.RunAsChildAsync(T instance, ChildScope scope) =>
        ((IChildValidator<T>)Current).RunAsChildAsync(instance, scope);

    private sealed class State(Validator<T> validator, RuleSetDefinition definition)
    {
        public Validator<T> Validator { get; } = validator;

        public RuleSetDefinition Definition { get; } = definition;
    }

    private static PropertySource<T> SourceOf(PropertyMap<T> properties)
    {
        Argument.NotNull(properties);
        return properties.Source;
    }

    private Validator<T> Build(RuleSetDefinition? definition, out IReadOnlyList<string> errors)
    {
        lock (_gate)
        {
            return RuleCompiler.Build(definition, _source, _registry, out errors);
        }
    }
}
