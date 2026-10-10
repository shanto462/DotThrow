using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow;

/// <summary>
/// A validator for objects whose type is known only at run time, for example in a framework
/// filter. <see cref="IValidator{T}"/> extends it.
/// </summary>
public interface IValidator
{
    /// <summary>Returns <see langword="true"/> when this validator can validate objects of <paramref name="type"/>.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when objects of <paramref name="type"/> can be validated.</returns>
    bool CanValidate(Type type);

    /// <summary>Validates an object of a type this validator accepts.</summary>
    /// <param name="instance">The object to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentException"><paramref name="instance"/> has a type this validator cannot validate.</exception>
    ValidationReport Validate(object instance, ValidationOptions? options);

    /// <summary>Validates an object of a type this validator accepts, including async rules.</summary>
    /// <param name="instance">The object to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentException"><paramref name="instance"/> has a type this validator cannot validate.</exception>
    Task<ValidationReport> ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates objects of type <typeparamref name="T"/>. Register it in dependency injection, or
/// mock it in tests: the two members below are all an implementation needs, and
/// <see cref="ValidatorExtensions"/> adds the short forms (groups, no options, and throwing).
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
public interface IValidator<in T> : IValidator
{
    /// <summary>Runs the rules and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <returns>The report.</returns>
    ValidationReport Validate(T value, ValidationOptions? options);

    /// <summary>Runs the rules, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="options">The options, or <see langword="null"/> for a default run.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The report.</returns>
    Task<ValidationReport> ValidateAsync(T value, ValidationOptions? options, CancellationToken cancellationToken = default);
}
