using System;
using System.Collections.Generic;
using System.Text;

namespace DotThrow.Dynamic;

/// <summary>
/// Thrown when a <see cref="RuleSetDefinition"/> cannot be read or built. It lists every
/// problem found, not only the first one, each with its place, for example
/// <c>Rules[2].Checks[0]: unknown check 'LongerThn' (did you mean 'LongerThan'?)</c>.
/// </summary>
public class RuleDefinitionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RuleDefinitionException"/> class.</summary>
    public RuleDefinitionException()
        : this("The rule definition is not valid.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RuleDefinitionException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public RuleDefinitionException(string message)
        : base(message)
    {
        Errors = [message];
    }

    /// <summary>Initializes a new instance of the <see cref="RuleDefinitionException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public RuleDefinitionException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [message];
    }

    /// <summary>Initializes a new instance of the <see cref="RuleDefinitionException"/> class with every error found.</summary>
    /// <param name="errors">The errors.</param>
    public RuleDefinitionException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors ?? [];
    }

    /// <summary>Gets every error, each with its place in the definition.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(IReadOnlyList<string>? errors)
    {
        if (errors is null || errors.Count == 0)
        {
            return "The rule definition is not valid.";
        }

        var text = new StringBuilder("The rule definition has ")
            .Append(errors.Count)
            .Append(errors.Count == 1 ? " error:" : " errors:");
        foreach (var error in errors)
        {
            text.AppendLine().Append(" - ").Append(error);
        }

        return text.ToString();
    }
}
