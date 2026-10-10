using System;
using System.Globalization;
using DotThrow.Internal;

namespace DotThrow;

/// <summary>
/// Thrown when <c>Validate</c> reaches an async rule, async condition or async filter. Call
/// <c>ValidateAsync</c> instead. Rules that are not selected (other groups, false conditions)
/// never throw.
/// </summary>
/// <remarks>It derives from <see cref="InvalidOperationException"/>.</remarks>
public class AsyncRuleInvokedSynchronouslyException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="AsyncRuleInvokedSynchronouslyException"/> class.</summary>
    public AsyncRuleInvokedSynchronouslyException()
        : this("A validator with async rules was run synchronously. Call ValidateAsync instead of Validate.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AsyncRuleInvokedSynchronouslyException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public AsyncRuleInvokedSynchronouslyException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AsyncRuleInvokedSynchronouslyException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public AsyncRuleInvokedSynchronouslyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AsyncRuleInvokedSynchronouslyException"/> class.</summary>
    /// <param name="validatorType">The type of the validator that owns the async rule.</param>
    public AsyncRuleInvokedSynchronouslyException(Type validatorType)
        : base(BuildMessage(validatorType))
    {
        ValidatorType = validatorType;
    }

    /// <summary>Gets the type of the validator that owns the async rule, when known.</summary>
    public Type? ValidatorType { get; }

    private static string BuildMessage(Type? validatorType) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Validator '{0}' has async rules but was run synchronously. Call ValidateAsync instead of Validate.",
            validatorType is null ? "(unknown)" : TypeNames.Display(validatorType));
}
