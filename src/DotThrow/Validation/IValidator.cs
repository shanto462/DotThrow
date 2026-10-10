using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow;

/// <summary>Validates objects of type <typeparamref name="T"/>. Register it in dependency injection, or mock it in tests.</summary>
/// <typeparam name="T">The type to validate.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Runs the rules of the given groups and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <returns>The report.</returns>
    ValidationReport Validate(T value, params string[] groups);

    /// <summary>Runs the rules of the given groups, including async rules, and returns every failure.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="groups">The groups to run. With <see langword="null"/> or no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <param name="cancellationToken">A token to cancel the async rules.</param>
    /// <returns>The report.</returns>
    Task<ValidationReport> ValidateAsync(T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default);
}
