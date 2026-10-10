using System;
using System.Diagnostics.CodeAnalysis;
using DotThrow;
using DotThrow.DependencyInjection;
using DotThrow.DependencyInjection.Internal;
using DotThrow.Dynamic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

// Dynamic validators: rules from configuration (with reload) or from a definition object.
public static partial class DotThrowServiceCollectionExtensions
{
    private const string DynamicTrimMessage = "The rules find properties by name with reflection. Use the overload with a PropertyMap<T> in trimmed and Native AOT apps.";
    private const string DynamicCodeMessage = "The rules may create generic types at run time. Use the overload with a PropertyMap<T> in Native AOT apps.";

    /// <summary>
    /// Registers a <see cref="DynamicValidator{T}"/> whose rules come from a configuration section,
    /// for example a part of <c>appsettings.json</c>, as a singleton <see cref="IValidator{T}"/> and
    /// <see cref="DynamicValidator{T}"/>. When the configuration changes (<c>reloadOnChange</c>), the
    /// rules are read again and swapped in one step; changed rules with errors are reported through
    /// <see cref="DynamicValidatorOptions{T}.OnReloadError"/> and the old rules stay.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="section">The section with the rules, in the shape of <see cref="RuleSetDefinition"/>.</param>
    /// <param name="configure">Sets custom checks, code rules and the reload error handler, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// Properties are found with reflection. One dynamic validator is registered per type: calling this
    /// again for the same <typeparamref name="T"/> replaces the earlier registration. The validator is
    /// built when it is first resolved, or when the host starts (<see cref="DynamicValidatorOptions{T}.ValidateOnStart"/>);
    /// a section with errors throws a <see cref="RuleDefinitionException"/> that lists all of them.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddDynamicValidator&lt;SignUp&gt;(builder.Configuration.GetSection("Validation:SignUp"));
    /// </code>
    /// </example>
    [RequiresUnreferencedCode(DynamicTrimMessage)]
    [RequiresDynamicCode(DynamicCodeMessage)]
    public static IServiceCollection AddDynamicValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IServiceCollection services,
        IConfiguration section,
        Action<DynamicValidatorOptions<T>>? configure = null)
    {
        section.Throw().IfNull();
        return AddDynamic(services, static (definition, checks) => new DynamicValidator<T>(definition, checks), null, _ => section, configure);
    }

    /// <summary>
    /// Registers a <see cref="DynamicValidator{T}"/> whose rules come from a configuration section and
    /// may check only the properties in <paramref name="properties"/>. No reflection: safe for trimming
    /// and Native AOT. Reloads the same way as <see cref="AddDynamicValidator{T}(IServiceCollection, IConfiguration, Action{DynamicValidatorOptions{T}}?)"/>.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="section">The section with the rules, in the shape of <see cref="RuleSetDefinition"/>.</param>
    /// <param name="properties">The properties the rules may check.</param>
    /// <param name="configure">Sets custom checks, code rules and the reload error handler, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddDynamicValidator(
    ///     builder.Configuration.GetSection("Validation:SignUp"),
    ///     new PropertyMap&lt;SignUp&gt;().Add(x =&gt; x.Email).Add(x =&gt; x.Age));
    /// </code>
    /// </example>
    public static IServiceCollection AddDynamicValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IServiceCollection services,
        IConfiguration section,
        PropertyMap<T> properties,
        Action<DynamicValidatorOptions<T>>? configure = null)
    {
        section.Throw().IfNull();
        properties.Throw().IfNull();
        return AddDynamic(services, (definition, checks) => new DynamicValidator<T>(definition, properties, checks), null, _ => section, configure);
    }

    /// <summary>
    /// Registers a <see cref="DynamicValidator{T}"/> whose rules come from the section at
    /// <paramref name="sectionPath"/> of the app's <see cref="IConfiguration"/> (resolved from the
    /// service provider), with reload. Properties are found with reflection.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="sectionPath">The section path, for example <c>"Validation:SignUp"</c>.</param>
    /// <param name="configure">Sets custom checks, code rules and the reload error handler, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="InvalidOperationException">When the validator is built: no <see cref="IConfiguration"/> is registered.</exception>
    [RequiresUnreferencedCode(DynamicTrimMessage)]
    [RequiresDynamicCode(DynamicCodeMessage)]
    public static IServiceCollection AddDynamicValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IServiceCollection services,
        string sectionPath,
        Action<DynamicValidatorOptions<T>>? configure = null)
    {
        sectionPath.Throw().IfNull();
        return AddDynamic(services, static (definition, checks) => new DynamicValidator<T>(definition, checks), null, provider => SectionOf(provider, sectionPath), configure);
    }

    /// <summary>
    /// Registers a <see cref="DynamicValidator{T}"/> whose rules come from the section at
    /// <paramref name="sectionPath"/> of the app's <see cref="IConfiguration"/>, with reload, checking
    /// only the properties in <paramref name="properties"/> (safe for trimming and Native AOT).
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="sectionPath">The section path, for example <c>"Validation:SignUp"</c>.</param>
    /// <param name="properties">The properties the rules may check.</param>
    /// <param name="configure">Sets custom checks, code rules and the reload error handler, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="InvalidOperationException">When the validator is built: no <see cref="IConfiguration"/> is registered.</exception>
    public static IServiceCollection AddDynamicValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IServiceCollection services,
        string sectionPath,
        PropertyMap<T> properties,
        Action<DynamicValidatorOptions<T>>? configure = null)
    {
        sectionPath.Throw().IfNull();
        properties.Throw().IfNull();
        return AddDynamic(services, (definition, checks) => new DynamicValidator<T>(definition, properties, checks), null, provider => SectionOf(provider, sectionPath), configure);
    }

    /// <summary>
    /// Registers a <see cref="DynamicValidator{T}"/> built from a definition object (code, a database,
    /// <see cref="RuleSetDefinition.FromJson(string)"/>). There is no reload; call
    /// <see cref="DynamicValidator{T}.Update(RuleSetDefinition)"/> on the resolved
    /// <see cref="DynamicValidator{T}"/> to change the rules. Properties are found with reflection.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="definition">The rules.</param>
    /// <param name="configure">Sets custom checks and code rules, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode(DynamicTrimMessage)]
    [RequiresDynamicCode(DynamicCodeMessage)]
    public static IServiceCollection AddDynamicValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IServiceCollection services,
        RuleSetDefinition definition,
        Action<DynamicValidatorOptions<T>>? configure = null)
    {
        definition.Throw().IfNull();
        return AddDynamic(services, static (rules, checks) => new DynamicValidator<T>(rules, checks), definition, null, configure);
    }

    /// <summary>
    /// Registers a <see cref="DynamicValidator{T}"/> built from a definition object, checking only the
    /// properties in <paramref name="properties"/> (safe for trimming and Native AOT). There is no reload.
    /// </summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="definition">The rules.</param>
    /// <param name="properties">The properties the rules may check.</param>
    /// <param name="configure">Sets custom checks and code rules, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDynamicValidator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IServiceCollection services,
        RuleSetDefinition definition,
        PropertyMap<T> properties,
        Action<DynamicValidatorOptions<T>>? configure = null)
    {
        definition.Throw().IfNull();
        properties.Throw().IfNull();
        return AddDynamic(services, (rules, checks) => new DynamicValidator<T>(rules, properties, checks), definition, null, configure);
    }

    private static IServiceCollection AddDynamic<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        IServiceCollection services,
        Func<RuleSetDefinition, CheckRegistry?, DynamicValidator<T>> create,
        RuleSetDefinition? definition,
        Func<IServiceProvider, IConfiguration>? section,
        Action<DynamicValidatorOptions<T>>? configure)
    {
        services.Throw().IfNull();
        var options = new DynamicValidatorOptions<T>();
        configure?.Invoke(options);

        // One dynamic validator per type: a second call replaces the registration, and the
        // forwarding services added by the first call resolve the new one.
        var replaces = false;
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(DynamicValidatorRegistration<T>))
            {
                replaces = true;
                break;
            }
        }

        services.RemoveAll<DynamicValidatorRegistration<T>>();
        services.AddSingleton(provider => new DynamicValidatorRegistration<T>(create, definition, section?.Invoke(provider), options, provider));
        if (!replaces)
        {
            services.AddSingleton(static provider => provider.GetRequiredService<DynamicValidatorRegistration<T>>().Dynamic);
            services.AddSingleton(static provider => provider.GetRequiredService<DynamicValidatorRegistration<T>>().Validator);
        }

        services.RemoveAll<IValidateOptions<DynamicValidatorStartup<T>>>();
        if (options.ValidateOnStart)
        {
            services.AddSingleton<IValidateOptions<DynamicValidatorStartup<T>>>(static provider => new DynamicValidatorStartupCheck<T>(provider));
            services.AddOptions<DynamicValidatorStartup<T>>().ValidateOnStart();
        }

        return services;
    }

    private static IConfiguration SectionOf(IServiceProvider provider, string sectionPath) =>
        provider.GetService<IConfiguration>()?.GetSection(sectionPath)
        ?? throw new InvalidOperationException(
            $"AddDynamicValidator(\"{sectionPath}\") needs an IConfiguration service. Hosts register one; otherwise pass the section itself.");
}
