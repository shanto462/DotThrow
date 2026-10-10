using System.Collections.Generic;

namespace DotThrow.Testing;

/// <summary>
/// The failures an assertion matched, so more conditions can follow:
/// <c>result.ShouldHaveValidationErrorFor(x =&gt; x.Email).WithErrorCode("NotEmailAddress")</c>.
/// Enumerating it gives the matched failures.
/// </summary>
public interface ITestValidationContinuation : IEnumerable<ValidationFailure>
{
    /// <summary>Gets the failures that match every condition so far.</summary>
    IEnumerable<ValidationFailure> MatchedFailures { get; }

    /// <summary>Gets the failures that fail at least one condition so far.</summary>
    IEnumerable<ValidationFailure> UnmatchedFailures { get; }
}

/// <summary>A continuation after a <c>With...</c> condition. <see cref="TestValidationExtensions.Only(ITestValidationWith)"/> applies to it.</summary>
public interface ITestValidationWith : ITestValidationContinuation
{
}
