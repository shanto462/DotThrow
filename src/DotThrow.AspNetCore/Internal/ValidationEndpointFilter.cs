using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotThrow.AspNetCore.Internal;

// The endpoint filter behind WithValidation<T>(). The parameter positions are found once, when
// the endpoint is built, from the handler's MethodInfo; each request reads the arguments with
// GetArgument<T>(index), which does not box with the request delegate generator (Native AOT).
internal sealed class ValidationEndpointFilter<T>
{
    private readonly int[] _indexes;
    private readonly IValidator<T>? _validator;
    private readonly IReadOnlyList<string>? _groups;
    private readonly Action<ValidationOptions<T>, HttpContext>? _configure;
    private readonly Func<ValidationReport, HttpContext, IResult>? _resultFactory;
    private readonly ValidationProblemOptions? _problemOptions;

    private ValidationEndpointFilter(int[] indexes, EndpointValidationOptions<T> options, ValidationProblemOptions? problemOptions)
    {
        _indexes = indexes;
        _validator = options.Validator;
        _groups = options.Groups;
        _configure = options.ConfigureValidation;
        _resultFactory = options.ResultFactory;
        _problemOptions = problemOptions;
    }

    // Creates the filter factory. "required" is true for a single endpoint (a handler without a
    // T parameter is a mistake) and false for a route group (endpoints without one are skipped).
    internal static Func<EndpointFilterFactoryContext, EndpointFilterDelegate, EndpointFilterDelegate> Factory(EndpointValidationOptions<T> options, bool required) =>
        (factoryContext, next) =>
        {
            var indexes = Indexes(factoryContext.MethodInfo);
            if (indexes.Length == 0)
            {
                return required
                    ? throw new InvalidOperationException(
                        $"WithValidation<{typeof(T).Name}>() is used on the endpoint handler '{factoryContext.MethodInfo.Name}', " +
                        $"which has no parameter of type {typeof(T).Name}.")
                    : next;
            }

            var services = factoryContext.ApplicationServices;
            if (options.Validator is null && services.GetService<IServiceProviderIsService>() is { } isService && !isService.IsService(typeof(IValidator<T>)))
            {
                throw new InvalidOperationException(
                    $"WithValidation<{typeof(T).Name}>() needs an IValidator<{typeof(T).Name}> service. Register one, for example with " +
                    $"services.AddValidator<{typeof(T).Name}, {typeof(T).Name}Validator>(), or pass a validator to WithValidation.");
            }

            if (options.ResultFactory is null && !CanWriteValidationProblems(services))
            {
                throw new InvalidOperationException(
                    $"WithValidation<{typeof(T).Name}>() answers invalid requests with a validation problem, but the app's JSON options " +
                    "have no metadata for HttpValidationProblemDetails (Native AOT or source-generated JSON only). " +
                    "Call builder.Services.AddProblemDetails(), or set a ResultFactory.");
            }

            var filter = new ValidationEndpointFilter<T>(indexes, options, services.GetService<IOptions<ValidationProblemOptions>>()?.Value);
            return context => filter.InvokeAsync(context, next);
        };

    // ValidationProblem writes HttpValidationProblemDetails with the app's JSON options. With
    // source-generated metadata only (Native AOT), that works after AddProblemDetails(); without it
    // every invalid request would answer 500, so the endpoint fails when it is built instead.
    internal static bool CanWriteValidationProblems(IServiceProvider services)
    {
        if (services.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions is not { } json)
        {
            return true;
        }

        try
        {
            return json.TryGetTypeInfo(typeof(HttpValidationProblemDetails), out _);
        }
        catch (InvalidOperationException)
        {
            // No type info resolver at all.
            return false;
        }
    }

    // True when the handler has a parameter that WithValidation<T>() validates.
    internal static bool Accepts(MethodInfo method) => Indexes(method).Length > 0;

    private static int[] Indexes(MethodInfo method)
    {
        var parameters = method.GetParameters();
        List<int>? indexes = null;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (typeof(T).IsAssignableFrom(parameters[i].ParameterType))
            {
                (indexes ??= []).Add(i);
            }
        }

        return indexes is null ? [] : [.. indexes];
    }

    private async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        foreach (var index in _indexes)
        {
            var value = context.GetArgument<T>(index);
            if (value is null)
            {
                // An optional argument that the request did not send: nothing to validate.
                continue;
            }

            var validator = _validator ?? http.RequestServices.GetRequiredService<IValidator<T>>();
            var report = await validator.ValidateAsync(value, CreateOptions(http), http.RequestAborted).ConfigureAwait(false);
            if (!report.IsValid)
            {
                return _resultFactory is null ? report.ToValidationProblem(_problemOptions) : _resultFactory(report, http);
            }
        }

        return await next(context).ConfigureAwait(false);
    }

    private ValidationOptions<T>? CreateOptions(HttpContext http)
    {
        if (_groups is null && _configure is null)
        {
            return null;
        }

        var options = new ValidationOptions<T> { Groups = _groups };
        _configure?.Invoke(options, http);
        return options;
    }
}
