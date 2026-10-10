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
/// filter that finds the handler parameter of type <c>T</c> (once, when the endpoint
/// is built), validates its value with <see cref="IValidator{T}"/> and returns a 400
/// <c>ValidationProblem</c> (RFC 7807) when it is invalid. The handler runs only for valid input.
/// </summary>
/// <remarks>
/// <para>
/// The validator comes from the request services unless one is given; register it with
/// <c>AddValidator&lt;TModel, TValidator&gt;()</c> or assembly scanning (DotThrow.DependencyInjection).
/// A missing registration fails when the endpoint is built, not on the first bad request.
/// </para>
/// <para>
/// Native AOT: the filter uses no reflection at request time and works with the request delegate
/// generator. Call <c>builder.Services.AddProblemDetails()</c> so the app's JSON options can write
/// <c>ValidationProblem</c> results; without that metadata the endpoint fails when it is built. The
/// response extensions (error codes, severities) are JSON elements, so no extra
/// <c>JsonSerializerContext</c> entries are needed. A parameter matches when its type can be
/// assigned to <c>T</c>; a nullable struct parameter (<c>T?</c>) does not match.
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
    /// <exception cref="InvalidOperationException">When the endpoint is built: the handler has no parameter of type <typeparamref name="T"/>, no <see cref="IValidator{T}"/> is registered, or the JSON options cannot write a validation problem.</exception>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder, params string[] groups) =>
        Add(builder, Options<T>(groups), required: true);

    /// <summary>Validates the argument of type <typeparamref name="T"/> with the given validator.</summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <param name="validator">The validator. It is shared by all requests, so it must be thread-safe (every <see cref="Validator{T}"/> is).</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="InvalidOperationException">When the endpoint is built: the handler has no parameter of type <typeparamref name="T"/>.</exception>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder, IValidator<T> validator) =>
        Add(builder, new EndpointValidationOptions<T> { Validator = validator.Throw().IfNull().Value }, required: true);

    /// <summary>Validates the argument of type <typeparamref name="T"/> with the given settings.</summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <param name="configure">Sets the validator, groups, per-request options and the failure response.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="InvalidOperationException">When the endpoint is built: the handler has no parameter of type <typeparamref name="T"/>, or no validator is given or registered.</exception>
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
    /// <exception cref="InvalidOperationException">When an endpoint with a <typeparamref name="T"/> parameter is built and no <see cref="IValidator{T}"/> is registered.</exception>
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
    /// <exception cref="InvalidOperationException">When an endpoint with a <typeparamref name="T"/> parameter is built and no validator is given or registered.</exception>
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
            if (HandlerOf(endpoint) is { } method && ValidationEndpointFilter<T>.Accepts(method))
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
