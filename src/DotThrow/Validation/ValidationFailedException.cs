using System;
using System.Collections.Generic;

namespace DotThrow;

/// <summary>
/// Thrown by <see cref="ValidationReport.ThrowIfInvalid"/>, by <c>ValidateAndThrow</c>, and by
/// runs with <see cref="ValidationOptions.ThrowOnFailures"/>. It carries every failure.
/// It derives from <see cref="ArgumentException"/>, so code that already catches bad
/// arguments catches it too.
/// </summary>
public class ValidationFailedException : ArgumentException
{
    /// <summary>Initializes a new instance of the <see cref="ValidationFailedException"/> class.</summary>
    public ValidationFailedException()
        : this("Validation failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationFailedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public ValidationFailedException(string message)
        : base(message)
    {
        Report = new ValidationReport();
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationFailedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ValidationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Report = new ValidationReport();
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationFailedException"/> class.</summary>
    /// <param name="report">The report with the failures.</param>
    public ValidationFailedException(ValidationReport report)
        : base(report?.ToString())
    {
        Report = report ?? new ValidationReport();
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationFailedException"/> class with a custom message.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="report">The report with the failures.</param>
    public ValidationFailedException(string message, ValidationReport report)
        : base(message)
    {
        Report = report ?? new ValidationReport();
    }

    /// <summary>Gets the report with every failure.</summary>
    public ValidationReport Report { get; }

    /// <summary>Gets the failures.</summary>
    public IReadOnlyList<ValidationFailure> Failures => Report.Failures;
}
