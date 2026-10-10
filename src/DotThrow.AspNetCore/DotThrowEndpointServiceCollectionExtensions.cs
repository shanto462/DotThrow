using DotThrow;
using DotThrow.AspNetCore.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Startup checks for DotThrow minimal API validation.</summary>
public static class DotThrowEndpointServiceCollectionExtensions
{
    /// <summary>
    /// Builds every endpoint when the host starts, so a <c>WithValidation&lt;T&gt;()</c> that is set up
    /// wrong (the handler has no <c>T</c> parameter from the request, no <see cref="IValidator{T}"/> is
    /// registered, or the JSON options cannot write a validation problem) stops the app at startup with
    /// an <see cref="System.InvalidOperationException"/> that lists every problem. Other endpoint build
    /// errors surface at startup too.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// Without it, a wrong setup never breaks other endpoints: the endpoints are built at the first
    /// request, the problem is logged as an error, and only that endpoint answers 500.
    /// </remarks>
    /// <example><c>builder.Services.ValidateEndpointsOnStart();</c></example>
    public static IServiceCollection ValidateEndpointsOnStart(this IServiceCollection services)
    {
        services.Throw().IfNull();
        services.TryAddSingleton<EndpointValidationDiagnostics>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, EndpointValidationStartupFilter>());
        return services;
    }
}
