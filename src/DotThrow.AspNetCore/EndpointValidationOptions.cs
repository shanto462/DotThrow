using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace DotThrow.AspNetCore;

/// <summary>
/// Settings of one <c>WithValidation&lt;T&gt;()</c> endpoint filter (minimal APIs).
/// </summary>
/// <typeparam name="T">The type of the validated argument.</typeparam>
/// <example>
/// <code>
/// app.MapPost("/orders", (Order order) =&gt; TypedResults.Created())
///     .WithValidation&lt;Order&gt;(o =&gt;
///     {
///         o.Groups = ["checkout"];
///         o.ConfigureValidation = (options, http) =&gt; options.Items["user"] = http.User.Identity?.Name;
///     });
/// </code>
/// </example>
public sealed class EndpointValidationOptions<T>
{
    /// <summary>
    /// Gets or sets the validator. When it is <see langword="null"/> (the default), the filter resolves
    /// <see cref="IValidator{T}"/> from the request services; when none is registered, the endpoint
    /// answers 500 and the mistake is logged (or the host does not start, with
    /// <c>ValidateEndpointsOnStart()</c>).
    /// </summary>
    public IValidator<T>? Validator { get; set; }

    /// <summary>
    /// Gets or sets the groups to run, or <see langword="null"/> for the rules in
    /// <see cref="ValidationGroups.Default"/>.
    /// </summary>
    public IReadOnlyList<string>? Groups { get; set; }

    /// <summary>
    /// Gets or sets a callback that adjusts the options of each run, for example to pass data from
    /// the request to the rules through <see cref="ValidationOptions.Items"/>. It gets new options
    /// (with <see cref="Groups"/> already set) for every request.
    /// </summary>
    public Action<ValidationOptions<T>, HttpContext>? ConfigureValidation { get; set; }

    /// <summary>
    /// Gets or sets a callback that creates the response for an invalid argument. The default is a
    /// 400 <c>ValidationProblem</c> built with <see cref="ValidationReportProblemExtensions.ToValidationProblem"/>
    /// and the app's <see cref="ValidationProblemOptions"/>.
    /// </summary>
    public Func<ValidationReport, HttpContext, IResult>? ResultFactory { get; set; }
}
