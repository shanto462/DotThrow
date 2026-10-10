using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotThrow.AspNetCore.Internal;

// The MVC action filter. The global filter (AddDotThrowValidation) and every
// [ValidateWithDotThrow] attribute create one; only the closest one runs (action, then
// controller, then global), so an attribute replaces the global settings for its actions.
internal sealed class DotThrowValidationFilter : IAsyncActionFilter, IOrderedFilter
{
    private static readonly ConcurrentDictionary<Type, Type?> ValidatorTypes = new();

    private readonly IReadOnlyList<string>? _groups;
    private readonly bool _skip;

    internal DotThrowValidationFilter(IReadOnlyList<string>? groups, bool skip, int order)
    {
        _groups = groups;
        _skip = skip;
        Order = order;
    }

    public int Order { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_skip || !ReferenceEquals(context.FindEffectivePolicy<DotThrowValidationFilter>(), this))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var settings = services.GetService<IOptions<MvcValidationOptions>>()?.Value;
        ValidationReport? failed = null;
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (!context.ActionArguments.TryGetValue(parameter.Name, out var value) || value is null || IsFromServices(parameter.BindingInfo))
            {
                continue;
            }

            if (ValidatorType(parameter.ParameterType) is not { } validatorType || services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var report = await validator.ValidateAsync(value, CreateOptions(context, settings), context.HttpContext.RequestAborted).ConfigureAwait(false);
            if (report.IsValid)
            {
                continue;
            }

            // Failure names become model state keys, after the parameter's binding prefix if it has one.
            (failed ??= new ValidationReport()).AddRange(report, parameter.BindingInfo?.BinderModelName);
        }

        if (failed is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        foreach (var failure in failed.Failures)
        {
            context.ModelState.AddModelError(failure.Name, failure.Message);
        }

        if (ReturnsProblem(context))
        {
            context.Result = CreateResult(context, services, failed);
            return;
        }

        await next().ConfigureAwait(false);
    }

    // The closed IValidator<T> for a parameter type, cached. MakeGenericType of an interface
    // creates no code; a type that cannot be made (possible only in Native AOT, which MVC does not
    // support) has no registration either, so it counts as "no validator".
    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "Only a service type for a DI lookup is made; failures mean no validator is registered.")]
    internal static Type? ValidatorType(Type modelType) =>
        ValidatorTypes.GetOrAdd(modelType, static type =>
        {
            try
            {
                return typeof(IValidator<>).MakeGenericType(type);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        });

    private static bool IsFromServices(BindingInfo? binding) =>
        binding?.BindingSource is { } source && (source == BindingSource.Services || source == BindingSource.Special);

    // Answer with a validation problem where MVC itself would answer invalid model state with one:
    // [ApiController] actions, unless the app turned that off (SuppressModelStateInvalidFilter).
    // Other actions continue and read ModelState.IsValid themselves.
    private static bool ReturnsProblem(ActionExecutingContext context)
    {
        foreach (var filter in context.Filters)
        {
            if (filter is ModelStateInvalidFilter)
            {
                return true;
            }
        }

        return false;
    }

    private static IActionResult CreateResult(ActionExecutingContext context, IServiceProvider services, ValidationReport failed)
    {
        var result = services.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory(context);
        if (result is ObjectResult { Value: ProblemDetails details } &&
            services.GetService<IOptions<ValidationProblemOptions>>()?.Value is { } problemOptions)
        {
            ProblemExtensions.Add(details.Extensions, failed.Failures, problemOptions, asJson: false);
        }

        return result;
    }

    private ValidationOptions? CreateOptions(ActionExecutingContext context, MvcValidationOptions? settings)
    {
        var groups = _groups ?? settings?.Groups;
        var configure = settings?.ConfigureValidation;
        if (groups is null && configure is null)
        {
            return null;
        }

        var options = new ValidationOptions { Groups = groups };
        configure?.Invoke(options, context);
        return options;
    }
}
