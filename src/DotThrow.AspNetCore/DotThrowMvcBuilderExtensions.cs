using System;
using DotThrow;
using DotThrow.AspNetCore;
using DotThrow.AspNetCore.Internal;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds DotThrow validation to every MVC action: arguments whose type has an
/// <see cref="IValidator{T}"/> registered are validated before the action runs. See
/// <see cref="ValidateWithDotThrowAttribute"/> for the behavior and for per-controller settings.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddValidatorsFromAssemblyContaining&lt;Program&gt;();
/// builder.Services.AddControllers().AddDotThrowValidation();
/// builder.Services.Configure&lt;ValidationProblemOptions&gt;(o =&gt; o.IncludeErrorCodes = true);
/// </code>
/// </example>
public static class DotThrowMvcBuilderExtensions
{
    /// <summary>Validates the arguments of every action with DotThrow validators.</summary>
    /// <param name="builder">The MVC builder.</param>
    /// <param name="configure">Sets the default groups and the per-run options, or <see langword="null"/>.</param>
    /// <returns>The MVC builder.</returns>
    public static IMvcBuilder AddDotThrowValidation(this IMvcBuilder builder, Action<MvcValidationOptions>? configure = null)
    {
        builder.Throw().IfNull();
        AddCore(builder.Services, configure);
        return builder;
    }

    /// <summary>Validates the arguments of every action with DotThrow validators.</summary>
    /// <param name="builder">The MVC core builder.</param>
    /// <param name="configure">Sets the default groups and the per-run options, or <see langword="null"/>.</param>
    /// <returns>The MVC core builder.</returns>
    public static IMvcCoreBuilder AddDotThrowValidation(this IMvcCoreBuilder builder, Action<MvcValidationOptions>? configure = null)
    {
        builder.Throw().IfNull();
        AddCore(builder.Services, configure);
        return builder;
    }

    private static void AddCore(IServiceCollection services, Action<MvcValidationOptions>? configure)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.Configure<MvcOptions>(static options => options.Filters.Add(new DotThrowValidationFilter(null, skip: false, order: 0)));
    }
}
