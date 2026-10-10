namespace DotThrow.AspNetCore;

/// <summary>
/// What a validation problem response (RFC 7807 <c>ValidationProblemDetails</c>) contains besides
/// the messages. The minimal API filter and the MVC filter read it from dependency injection, so
/// one setting covers the whole app:
/// <c>builder.Services.Configure&lt;ValidationProblemOptions&gt;(o =&gt; o.IncludeErrorCodes = true);</c>
/// The <c>ToProblemDetails</c> helpers take it as an argument.
/// </summary>
/// <remarks>
/// The attempted value of a failure is never written to a response: it may be a password or a
/// token (DotThrow decision D10).
/// </remarks>
public class ValidationProblemOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the response has an <c>errorCodes</c> extension with
    /// the error code of every message, keyed and ordered like <c>errors</c>, for example
    /// <c>"errorCodes": { "Email": ["NotEmailAddress"] }</c>. The default is <see langword="false"/>.
    /// </summary>
    public bool IncludeErrorCodes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the response has a <c>severities</c> extension with
    /// the <see cref="Severity"/> of every message, keyed and ordered like <c>errors</c>, for example
    /// <c>"severities": { "Email": ["Error"] }</c>. The default is <see langword="false"/>.
    /// </summary>
    public bool IncludeSeverities { get; set; }
}
