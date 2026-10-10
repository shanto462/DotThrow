using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// Picks a validator by the runtime type of the object (FluentValidation's
/// <c>SetInheritanceValidator</c>). Use it as a child validator for a property whose type is a
/// base class or interface, or on its own. An object whose exact type has no validator passes.
/// </summary>
/// <typeparam name="T">The base type.</typeparam>
/// <example>
/// <code>
/// var payments = new PolymorphicValidator&lt;Payment&gt;()
///     .Add(new CardPaymentValidator())
///     .Add(new BankPaymentValidator());
/// orderValidator.For(x =&gt; x.Payment, payments);
/// </code>
/// </example>
public sealed class PolymorphicValidator<T> : IValidator<T>, IChildValidator<T>, IAsyncRuleInfo
{
    private readonly object _gate = new();
    private Dictionary<Type, Entry> _entries = [];

    /// <summary>
    /// Validates objects of exactly <typeparamref name="TDerived"/> with <paramref name="validator"/>.
    /// Adding a type again replaces its validator.
    /// </summary>
    /// <typeparam name="TDerived">The derived type.</typeparam>
    /// <param name="validator">The validator for that type.</param>
    /// <param name="groups">The groups the validator runs with, instead of the groups, property filter and custom selector of the run. Leave empty to keep them.</param>
    /// <returns>This validator.</returns>
    public PolymorphicValidator<T> Add<TDerived>(IValidator<TDerived> validator, params string[] groups)
        where TDerived : T
    {
        Argument.NotNull(validator);
        return Register(typeof(TDerived), new Entry<TDerived>(validator, null, CheckGroups(groups)));
    }

    /// <summary>
    /// Validates objects of exactly <typeparamref name="TDerived"/> with a validator picked per
    /// object. Returning <see langword="null"/> skips the object.
    /// </summary>
    /// <typeparam name="TDerived">The derived type.</typeparam>
    /// <param name="validatorFactory">Picks the validator for the object.</param>
    /// <param name="groups">The groups the validator runs with, instead of the groups, property filter and custom selector of the run.</param>
    /// <returns>This validator.</returns>
    public PolymorphicValidator<T> Add<TDerived>(Func<TDerived, IValidator<TDerived>?> validatorFactory, params string[] groups)
        where TDerived : T
    {
        Argument.NotNull(validatorFactory);
        return Register(typeof(TDerived), new Entry<TDerived>(null, (_, value) => validatorFactory(value), CheckGroups(groups)));
    }

    /// <summary>
    /// Validates objects of exactly <typeparamref name="TDerived"/> with a validator picked from
    /// the object that has the property (<typeparamref name="TRoot"/>) and the value
    /// (FluentValidation's <c>Add&lt;TDerived&gt;((x, value) =&gt; validator)</c>). When this
    /// validator runs on its own, or the parent is not a <typeparamref name="TRoot"/>, the factory gets
    /// <see langword="default"/> for the parent.
    /// </summary>
    /// <typeparam name="TDerived">The derived type.</typeparam>
    /// <typeparam name="TRoot">The type of the object that has the property.</typeparam>
    /// <param name="validatorFactory">Picks the validator from the parent object and the value.</param>
    /// <param name="groups">The groups the validator runs with, instead of the groups, property filter and custom selector of the run.</param>
    /// <returns>This validator.</returns>
    /// <example><c>payments.Add&lt;CardPayment, Order&gt;((order, card) =&gt; order.IsTrusted ? relaxed : strict)</c></example>
    public PolymorphicValidator<T> Add<TDerived, TRoot>(Func<TRoot, TDerived, IValidator<TDerived>?> validatorFactory, params string[] groups)
        where TDerived : T
    {
        Argument.NotNull(validatorFactory);
        return Register(
            typeof(TDerived),
            new Entry<TDerived>(null, (parent, value) => validatorFactory(parent is ValidationContext<TRoot> typed ? typed.Instance : default!, value), CheckGroups(groups)));
    }

    /// <summary>
    /// Validates objects of exactly <paramref name="derivedType"/> with a validator known only by
    /// its non-generic interface (FluentValidation's <c>Add(Type, IValidator)</c>).
    /// </summary>
    /// <param name="derivedType">The derived type.</param>
    /// <param name="validator">The validator. It must accept <paramref name="derivedType"/>.</param>
    /// <param name="groups">The groups the validator runs with, instead of the groups, property filter and custom selector of the run.</param>
    /// <returns>This validator.</returns>
    /// <exception cref="ArgumentException"><paramref name="derivedType"/> does not derive from <typeparamref name="T"/>, or the validator cannot validate it.</exception>
    public PolymorphicValidator<T> Add(Type derivedType, IValidator validator, params string[] groups)
    {
        Argument.NotNull(derivedType);
        Argument.NotNull(validator);
        if (!typeof(T).IsAssignableFrom(derivedType) || !validator.CanValidate(derivedType))
        {
            throw new ArgumentException(
                $"{TypeNames.Display(validator.GetType())} cannot validate {TypeNames.Display(derivedType)} as a {TypeNames.Display(typeof(T))}.",
                nameof(validator));
        }

        return Register(derivedType, new UntypedEntry(validator, CheckGroups(groups)));
    }

    /// <inheritdoc/>
    public bool CanValidate(Type type)
    {
        Argument.NotNull(type);
        return typeof(T).IsAssignableFrom(type);
    }

    /// <inheritdoc/>
    public ValidationReport Validate(T value, ValidationOptions? options)
    {
        Argument.NotNull(value);
        return Find(value) is { } entry ? entry.Validate(value, options) : new ValidationReport();
    }

    /// <inheritdoc/>
    public Task<ValidationReport> ValidateAsync(T value, ValidationOptions? options, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            return TaskHelpers.FromException<ValidationReport>(new ArgumentNullException(nameof(value)));
        }

        return Find(value) is { } entry ? entry.ValidateAsync(value, options, cancellationToken) : Task.FromResult(new ValidationReport());
    }

    ValidationReport IValidator.Validate(object instance, ValidationOptions? options) => Validate(Cast(instance), options);

    Task<ValidationReport> IValidator.ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken) =>
        ValidateAsync(Cast(instance), options, cancellationToken);

    bool IAsyncRuleInfo.HasAsyncRules(HashSet<object> visited)
    {
        if (!visited.Add(this))
        {
            return false;
        }

        foreach (var entry in Volatile.Read(ref _entries).Values)
        {
            if (entry.NeedsAsync(visited))
            {
                return true;
            }
        }

        return false;
    }

    void IChildValidator<T>.RunAsChild(T instance, in ChildScope scope) => Find(instance)?.Run(instance, scope);

    Task IChildValidator<T>.RunAsChildAsync(T instance, ChildScope scope) => Find(instance)?.RunAsync(instance, scope) ?? Task.CompletedTask;

    private static T Cast(object? instance) => instance switch
    {
        null when default(T) is not null => throw new ArgumentNullException(nameof(instance)),
        null => default!,
        T typed => typed,
        _ => throw new ArgumentException(
            $"This validator validates {TypeNames.Display(typeof(T))}, not {TypeNames.Display(instance.GetType())}.",
            nameof(instance)),
    };

    private static string[]? CheckGroups(string[] groups)
    {
        Argument.NotNull(groups);
        return groups.Length == 0 ? null : Validator<T>.CheckGroups(groups);
    }

    private PolymorphicValidator<T> Register(Type type, Entry entry)
    {
        lock (_gate)
        {
            var next = new Dictionary<Type, Entry>(_entries) { [type] = entry };
            Volatile.Write(ref _entries, next);
        }

        return this;
    }

    private Entry? Find(T instance) =>
        instance is not null && Volatile.Read(ref _entries).TryGetValue(instance.GetType(), out var entry) ? entry : null;

    private static ValidationOptions? WithGroups(ValidationOptions? options, string[]? groups)
    {
        if (groups is null)
        {
            return options;
        }

        // Registration groups replace the whole selection of the run (groups, properties and
        // custom selector), like FluentValidation's rule sets on a child validator.
        var copy = new ValidationOptions
        {
            Groups = groups,
            ThrowOnFailures = options?.ThrowOnFailures ?? false,
            ClassCascade = options?.ClassCascade,
            Depth = options?.Depth ?? 0,
        };
        if (options is not null)
        {
            copy.UseItemsOf(options);
        }

        return copy;
    }

    private abstract class Entry
    {
        public abstract void Run(T instance, in ChildScope scope);

        public abstract Task RunAsync(T instance, ChildScope scope);

        public abstract ValidationReport Validate(T instance, ValidationOptions? options);

        public abstract Task<ValidationReport> ValidateAsync(T instance, ValidationOptions? options, CancellationToken cancellationToken);

        public abstract bool NeedsAsync(HashSet<object> visited);
    }

    private sealed class Entry<TDerived>(
        IValidator<TDerived>? validator,
        Func<ValidationContext?, TDerived, IValidator<TDerived>?>? factory,
        string[]? groups) : Entry
        where TDerived : T
    {
        public override void Run(T instance, in ChildScope scope)
        {
            var value = (TDerived)instance!;
            if (Pick(scope.Parent, value) is { } picked)
            {
                ChildRunner.Run(picked, value, scope.WithGroups(groups));
            }
        }

        public override Task RunAsync(T instance, ChildScope scope)
        {
            var value = (TDerived)instance!;
            return Pick(scope.Parent, value) is { } picked
                ? ChildRunner.RunAsync(picked, value, scope.WithGroups(groups))
                : Task.CompletedTask;
        }

        public override ValidationReport Validate(T instance, ValidationOptions? options)
        {
            var value = (TDerived)instance!;
            return Pick(null, value) is { } picked ? picked.Validate(value, WithGroups(options, groups)) : new ValidationReport();
        }

        public override Task<ValidationReport> ValidateAsync(T instance, ValidationOptions? options, CancellationToken cancellationToken)
        {
            var value = (TDerived)instance!;
            return Pick(null, value) is { } picked
                ? picked.ValidateAsync(value, WithGroups(options, groups), cancellationToken)
                : Task.FromResult(new ValidationReport());
        }

        public override bool NeedsAsync(HashSet<object> visited) => AsyncRules.OfChild(validator, factory is not null, visited);

        private IValidator<TDerived>? Pick(ValidationContext? parent, TDerived value) => validator ?? factory!(parent, value);
    }

    private sealed class UntypedEntry(IValidator validator, string[]? groups) : Entry
    {
        public override void Run(T instance, in ChildScope scope) =>
            ChildRunner.RunUntyped(validator, instance!, scope.WithGroups(groups));

        public override Task RunAsync(T instance, ChildScope scope) =>
            ChildRunner.RunUntypedAsync(validator, instance!, scope.WithGroups(groups));

        public override ValidationReport Validate(T instance, ValidationOptions? options) =>
            validator.Validate(instance!, WithGroups(options, groups));

        public override Task<ValidationReport> ValidateAsync(T instance, ValidationOptions? options, CancellationToken cancellationToken) =>
            validator.ValidateAsync(instance!, WithGroups(options, groups), cancellationToken);

        public override bool NeedsAsync(HashSet<object> visited) => AsyncRules.OfChild(validator, picked: false, visited);
    }
}
