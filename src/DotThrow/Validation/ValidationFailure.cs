using DotThrow.Internal;

namespace DotThrow;

/// <summary>One failed check: what failed, why, and a stable error code.</summary>
public sealed class ValidationFailure
{
    /// <summary>Initializes a new instance of the <see cref="ValidationFailure"/> class.</summary>
    /// <param name="name">The parameter name, property path or expression that failed. Use an empty string for the whole object.</param>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">A stable code for the failure. See <see cref="ErrorCodes"/>.</param>
    public ValidationFailure(string name, string message, string errorCode)
    {
        Argument.NotNull(name);
        Argument.NotNull(message);
        Argument.NotNull(errorCode);
        Name = name;
        Message = message;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the parameter name, property path or expression that failed, for example
    /// <c>"email"</c>, <c>"Address.City"</c> or <c>"Lines[2].Quantity"</c>. It is empty for
    /// a rule about the whole object.
    /// </summary>
    public string Name { get; }

    /// <summary>Gets the error message.</summary>
    public string Message { get; }

    /// <summary>Gets the stable error code, for example <see cref="ErrorCodes.LongerThan"/>.</summary>
    public string ErrorCode { get; }

    /// <inheritdoc/>
    public override string ToString() => Name.Length == 0 ? Message : $"{Name}: {Message}";

    internal ValidationFailure WithPrefix(string prefix) =>
        prefix.Length == 0 ? this : new ValidationFailure(Name.Length == 0 ? prefix : $"{prefix}.{Name}", Message, ErrorCode);
}
