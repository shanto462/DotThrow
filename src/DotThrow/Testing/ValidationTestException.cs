using System;
using System.Collections.Generic;

namespace DotThrow.Testing;

/// <summary>
/// Thrown by the <see cref="TestValidationResult{T}"/> assertions when the report does not match.
/// Every test framework reports an unhandled exception as a failed test, so the helpers work
/// with xUnit, NUnit, MSTest and others.
/// </summary>
public class ValidationTestException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ValidationTestException"/> class.</summary>
    public ValidationTestException()
        : this("The validation result did not match.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationTestException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public ValidationTestException(string message)
        : base(message)
    {
        Failures = Array.Empty<ValidationFailure>();
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationTestException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ValidationTestException(string message, Exception innerException)
        : base(message, innerException)
    {
        Failures = Array.Empty<ValidationFailure>();
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationTestException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="failures">The failures that made the assertion fail.</param>
    public ValidationTestException(string message, IReadOnlyList<ValidationFailure> failures)
        : base(message)
    {
        Failures = failures ?? Array.Empty<ValidationFailure>();
    }

    /// <summary>Gets the failures that made the assertion fail, when there are any.</summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }
}
