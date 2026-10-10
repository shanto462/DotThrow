using System;
using System.Collections.Generic;
using DotThrow.AspNetCore.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MvcValidationProblemDetails = Microsoft.AspNetCore.Mvc.ValidationProblemDetails;

namespace DotThrow.AspNetCore;

/// <summary>
/// Turns a <see cref="ValidationReport"/> into an RFC 7807 validation problem: <c>errors</c> maps
/// each failure name (a property path such as <c>"Address.City"</c>) to its messages, the same as
/// <see cref="ValidationReport.ToDictionary"/>. Attempted values are never included (decision D10).
/// </summary>
/// <example>
/// <code>
/// app.MapPost("/sign-up", async (SignUp request, IValidator&lt;SignUp&gt; validator) =&gt;
/// {
///     var report = await validator.ValidateAsync(request);
///     return report.IsValid ? Results.Ok() : report.ToValidationProblem();
/// });
/// </code>
/// </example>
public static class ValidationReportProblemExtensions
{
    /// <summary>
    /// Creates a 400 <c>ValidationProblem</c> result for minimal APIs, the same result as
    /// <c>TypedResults.ValidationProblem(report.ToDictionary())</c> plus the extensions that
    /// <paramref name="options"/> asks for. Extension values are <see cref="System.Text.Json.JsonElement"/>,
    /// so Native AOT apps need no extra JSON metadata beyond what <c>AddProblemDetails()</c> adds for
    /// every <c>ValidationProblem</c>.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="options">Which extensions to add, or <see langword="null"/> for none.</param>
    /// <returns>The result.</returns>
    public static ValidationProblem ToValidationProblem(this ValidationReport report, ValidationProblemOptions? options = null)
    {
        report.Throw().IfNull();
        Dictionary<string, object?>? extensions = null;
        if (options is { IncludeErrorCodes: true } or { IncludeSeverities: true })
        {
            extensions = new Dictionary<string, object?>(StringComparer.Ordinal);
            ProblemExtensions.Add(extensions, report.Failures, options, asJson: true);
        }

        return TypedResults.ValidationProblem(report.ToDictionary(), extensions: extensions);
    }

    /// <summary>
    /// Creates an <see cref="HttpValidationProblemDetails"/> with status 400, for minimal APIs and
    /// <c>IProblemDetailsService</c>. Extension values are <see cref="System.Text.Json.JsonElement"/>,
    /// so it is safe for Native AOT.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="options">Which extensions to add, or <see langword="null"/> for none.</param>
    /// <returns>The problem details.</returns>
    public static HttpValidationProblemDetails ToProblemDetails(this ValidationReport report, ValidationProblemOptions? options = null)
    {
        report.Throw().IfNull();
        var details = new HttpValidationProblemDetails(report.ToDictionary()) { Status = StatusCodes.Status400BadRequest };
        ProblemExtensions.Add(details.Extensions, report.Failures, options, asJson: true);
        return details;
    }

    /// <summary>
    /// Creates an MVC <see cref="MvcValidationProblemDetails"/> with status 400, for controllers
    /// (<c>return BadRequest(report.ToValidationProblemDetails());</c>). Extension values are
    /// <c>Dictionary&lt;string, string[]&gt;</c>, which every MVC output formatter can write.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="options">Which extensions to add, or <see langword="null"/> for none.</param>
    /// <returns>The problem details.</returns>
    public static MvcValidationProblemDetails ToValidationProblemDetails(this ValidationReport report, ValidationProblemOptions? options = null)
    {
        report.Throw().IfNull();
        var details = new MvcValidationProblemDetails(report.ToDictionary()) { Status = StatusCodes.Status400BadRequest };
        ProblemExtensions.Add(details.Extensions, report.Failures, options, asJson: false);
        return details;
    }
}
