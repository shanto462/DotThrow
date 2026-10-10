using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotThrow.AspNetCore.Internal;

// The endpoint filter behind WithValidation<T>(). The parameters to validate are found once, when
// the endpoint is built, from the handler's MethodInfo; each request reads the arguments with
// GetArgument<T>(index), which does not box with the request delegate generator (Native AOT).
internal sealed class ValidationEndpointFilter<T>
{
    private static readonly Action<ILogger, string, Exception?> LogMisconfigured = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(1, "EndpointValidationMisconfigured"), "{Problem} The endpoint answers 500 until this is fixed.");

    private readonly Parameter[] _parameters;
    private readonly IValidator<T>? _validator;
    private readonly IReadOnlyList<string>? _groups;
    private readonly Action<ValidationOptions<T>, HttpContext>? _configure;
    private readonly Func<ValidationReport, HttpContext, IResult>? _resultFactory;
    private readonly ValidationProblemOptions? _problemOptions;
    private readonly JsonNamingPolicy? _keyPolicy;

    private ValidationEndpointFilter(Parameter[] parameters, EndpointValidationOptions<T> options, IServiceProvider services)
    {
        _parameters = parameters;
        _validator = options.Validator;
        _groups = options.Groups;
        _configure = options.ConfigureValidation;
        _resultFactory = options.ResultFactory;
        _problemOptions = services.GetService<IOptions<ValidationProblemOptions>>()?.Value;
        _keyPolicy = services.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions.DictionaryKeyPolicy;
    }

    // Creates the filter factory. "required" is true for a single endpoint (a handler without a
    // T parameter is a mistake) and false for a route group (endpoints without one are skipped).
    // A mistake never throws here, because that would break every endpoint of the app: the
    // endpoint answers 500 with the problem, the problem is logged, and ValidateEndpointsOnStart()
    // turns it into a startup failure.
    internal static Func<EndpointFilterFactoryContext, EndpointFilterDelegate, EndpointFilterDelegate> Factory(EndpointValidationOptions<T> options, bool required) =>
        (factoryContext, next) =>
        {
            var services = factoryContext.ApplicationServices;
            var parameters = Parameters(factoryContext.MethodInfo, services);
            if (parameters.Length == 0)
            {
                return required
                    ? Misconfigured(
                        services,
                        $"WithValidation<{typeof(T).Name}>() is used on the endpoint handler '{factoryContext.MethodInfo.Name}', which has no " +
                        $"parameter of type {typeof(T).Name} bound from the request (parameters from services are not validated).")
                    : next;
            }

            if (options.Validator is null && services.GetService<IServiceProviderIsService>() is { } isService && !isService.IsService(typeof(IValidator<T>)))
            {
                return Misconfigured(
                    services,
                    $"WithValidation<{typeof(T).Name}>() on the endpoint handler '{factoryContext.MethodInfo.Name}' needs an IValidator<{typeof(T).Name}> service. " +
                    $"Register one, for example with services.AddValidator<{typeof(T).Name}, {typeof(T).Name}Validator>(), or pass a validator to WithValidation.");
            }

            if (options.ResultFactory is null && !CanWriteValidationProblems(services))
            {
                return Misconfigured(
                    services,
                    $"WithValidation<{typeof(T).Name}>() on the endpoint handler '{factoryContext.MethodInfo.Name}' answers invalid requests with a validation " +
                    "problem, but the app's JSON options have no metadata for HttpValidationProblemDetails (Native AOT or source-generated JSON only). " +
                    "Call builder.Services.AddProblemDetails(), or set a ResultFactory.");
            }

            var filter = new ValidationEndpointFilter<T>(parameters, options, services);
            return context => filter.InvokeAsync(context, next);
        };

    // ValidationProblem writes HttpValidationProblemDetails with the app's JSON options. With
    // source-generated metadata only (Native AOT), that works after AddProblemDetails().
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
    internal static bool Accepts(MethodInfo method, IServiceProvider services) => Parameters(method, services).Length > 0;

    // The parameters of type T (or a derived type) that are bound from the request. Parameters
    // from services ([FromServices], [FromKeyedServices], or a registered service type without a
    // binding attribute, the same rule the request delegate uses) are not validated.
    private static Parameter[] Parameters(MethodInfo method, IServiceProvider services)
    {
        var parameters = method.GetParameters();
        var isService = services.GetService<IServiceProviderIsService>();
        List<Parameter>? found = null;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (typeof(T).IsAssignableFrom(parameter.ParameterType) && !FromServices(parameter, isService))
            {
                (found ??= []).Add(new Parameter(i, parameter.Name ?? string.Empty));
            }
        }

        return found is null ? [] : [.. found];
    }

    private static bool FromServices(ParameterInfo parameter, IServiceProviderIsService? isService)
    {
        foreach (var attribute in parameter.GetCustomAttributes(inherit: true))
        {
            switch (attribute)
            {
                case IFromServiceMetadata or FromKeyedServicesAttribute:
                    return true;
                case IFromBodyMetadata or IFromQueryMetadata or IFromRouteMetadata or IFromHeaderMetadata or IFromFormMetadata or AsParametersAttribute:
                    return false;
            }
        }

        return isService?.IsService(parameter.ParameterType) == true;
    }

    private static EndpointFilterDelegate Misconfigured(IServiceProvider services, string problem)
    {
        services.GetService<EndpointValidationDiagnostics>()?.Add(problem);
        if (services.GetService<ILoggerFactory>() is { } loggers)
        {
            LogMisconfigured(loggers.CreateLogger("DotThrow.AspNetCore.EndpointValidation"), problem, null);
        }

        return _ => ValueTask.FromException<object?>(new InvalidOperationException(problem));
    }

    private async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        ValidationReport? failed = null;
        foreach (var parameter in _parameters)
        {
            var value = context.GetArgument<T>(parameter.Index);
            if (value is null)
            {
                // An optional argument that the request did not send: nothing to validate.
                continue;
            }

            var validator = _validator ?? http.RequestServices.GetRequiredService<IValidator<T>>();
            var report = await validator.ValidateAsync(value, CreateOptions(http), http.RequestAborted).ConfigureAwait(false);
            if (report.IsValid)
            {
                continue;
            }

            if (_parameters.Length == 1)
            {
                failed = report;
                break;
            }

            // Several validated arguments: one report, failures named after their parameter.
            (failed ??= new ValidationReport()).AddRange(report, parameter.Name);
        }

        if (failed is not null)
        {
            return _resultFactory is null ? failed.ToValidationProblem(_problemOptions, _keyPolicy) : _resultFactory(failed, http);
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

    private readonly struct Parameter(int index, string name)
    {
        public int Index { get; } = index;

        public string Name { get; } = name;
    }
}
