using System;
using System.Reflection;
using DotThrow;
using DotThrow.AspNetCore;
using DotThrow.AspNetCore.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Validates minimal API arguments with DotThrow. <c>WithValidation&lt;T&gt;()</c> adds an endpoint
/// filter that finds the handler parameters of type <c>T</c> that come from the request (once, when
/// the endpoint is built), validates their values with <see cref="IValidator{T}"/> and returns one 400
/// <c>ValidationProblem</c> (RFC 7807) with the failures of all of them. The handler runs only for valid input.
/// </summary>
/// <remarks>
/// <para>
/// A parameter matches when its type can be assigned to <c>T</c> and it is bound from the request.
/// Parameters from services (<c>[FromServices]</c>, <c>[FromKeyedServices]</c>, or a registered service
/// type without a binding attribute) are never validated. With several matching parameters, failure
/// names start with the parameter name (<c>"first.Value"</c>). A nullable struct parameter
/// (<c>T?</c>) does not match.
/// </para>
/// <para>
/// The validator comes from the request services unless one is given; register it with
/// <c>AddValidator&lt;TModel, TValidator&gt;()</c> or assembly scanning (DotThrow.DependencyInjection).
/// </para>
/// <para>
/// Setup mistakes (no matching parameter on a single endpoint, no validator, JSON options that cannot
/// write a validation problem) never break other endpoints: endpoints are built at the first request,
/// the mistake is logged as an error, and only that endpoint answers 500. Call
/// <c>builder.Services.ValidateEndpointsOnStart()</c> to build the endpoints when the host starts and
/// stop the app with every mistake listed.
/// </para>
/// <para>
/// Native AOT: the filter uses no reflection at request time and works with the request delegate
/// generator. Call <c>builder.Services.AddProblemDetails()</c> so the app's JSON options can write
/// <c>ValidationProblem</c> results. The response extensions (error codes, severities) are JSON
/// elements whose keys follow the JSON options' <c>DictionaryKeyPolicy</c>, like <c>errors</c>, so no
/// extra <c>JsonSerializerContext</c> entries are needed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.Services.AddValidator&lt;SignUp, SignUpValidator&gt;();
///
/// app.MapPost("/sign-up", (SignUp request) =&gt; TypedResults.Ok())
///     .WithValidation&lt;SignUp&gt;();
///
/// var admin = app.MapGroup("/admin").WithValidation&lt;CreateUser&gt;("admin");
/// </code>
/// </example>
public static class DotThrowEndpointValidationExtensions
{
    /// <summary>Validates the argument of type <typeparamref name="T"/> with the registered <see cref="IValidator{T}"/>.</summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <returns>The endpoint.</returns>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder, params string[] groups) =>
        Add(builder, Options<T>(groups), required: true);

    /// <summary>Validates the argument of type <typeparamref name="T"/> with the given validator.</summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <param name="validator">The validator. It is shared by all requests, so it must be thread-safe (every <see cref="Validator{T}"/> is).</param>
    /// <returns>The endpoint.</returns>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder, IValidator<T> validator) =>
        Add(builder, new EndpointValidationOptions<T> { Validator = validator.Throw().IfNull().Value }, required: true);

    /// <summary>Validates the argument of type <typeparamref name="T"/> with the given settings.</summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <param name="configure">Sets the validator, groups, per-request options and the failure response.</param>
    /// <returns>The endpoint.</returns>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder, Action<EndpointValidationOptions<T>> configure) =>
        Add(builder, Options(configure), required: true);

    /// <summary>
    /// Validates the argument of type <typeparamref name="T"/> of every endpoint in the group with the
    /// registered <see cref="IValidator{T}"/>. Endpoints without such a parameter are not changed.
    /// </summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The route group.</param>
    /// <param name="groups">The groups to run. With no groups, the rules in <see cref="ValidationGroups.Default"/> run.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder WithValidation<T>(this RouteGroupBuilder builder, params string[] groups) =>
        Add(builder, Options<T>(groups), required: false);

    /// <summary>
    /// Validates the argument of type <typeparamref name="T"/> of every endpoint in the group with the
    /// given validator. Endpoints without such a parameter are not changed.
    /// </summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The route group.</param>
    /// <param name="validator">The validator. It is shared by all requests, so it must be thread-safe.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder WithValidation<T>(this RouteGroupBuilder builder, IValidator<T> validator) =>
        Add(builder, new EndpointValidationOptions<T> { Validator = validator.Throw().IfNull().Value }, required: false);

    /// <summary>
    /// Validates the argument of type <typeparamref name="T"/> of every endpoint in the group with the
    /// given settings. Endpoints without such a parameter are not changed.
    /// </summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The route group.</param>
    /// <param name="configure">Sets the validator, groups, per-request options and the failure response.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder WithValidation<T>(this RouteGroupBuilder builder, Action<EndpointValidationOptions<T>> configure) =>
        Add(builder, Options(configure), required: false);

    private static EndpointValidationOptions<T> Options<T>(string[] groups)
    {
        groups.Throw().IfNull();
        return new EndpointValidationOptions<T> { Groups = groups.Length == 0 ? null : groups };
    }

    private static EndpointValidationOptions<T> Options<T>(Action<EndpointValidationOptions<T>> configure)
    {
        configure.Throw().IfNull();
        var options = new EndpointValidationOptions<T>();
        configure(options);
        return options;
    }

    private static TBuilder Add<TBuilder, T>(TBuilder builder, EndpointValidationOptions<T> options, bool required)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Throw().IfNull();

        // OpenAPI: endpoints that validate can answer 400 with a validation problem.
        builder.Add(endpoint =>
        {
            if (HandlerOf(endpoint) is { } method && ValidationEndpointFilter<T>.Accepts(method, endpoint.ApplicationServices))
            {
                endpoint.Metadata.Add(new ProducesResponseTypeMetadata(
                    StatusCodes.Status400BadRequest,
                    typeof(HttpValidationProblemDetails),
                    ["application/problem+json"]));
            }
        });

        builder.AddEndpointFilterFactory(ValidationEndpointFilter<T>.Factory(options, required));
        return builder;
    }

    // Minimal API endpoints carry their handler's MethodInfo as the first metadata item.
    private static MethodInfo? HandlerOf(EndpointBuilder endpoint)
    {
        foreach (var item in endpoint.Metadata)
        {
            if (item is MethodInfo method)
            {
                return method;
            }
        }

        return null;
    }
}
