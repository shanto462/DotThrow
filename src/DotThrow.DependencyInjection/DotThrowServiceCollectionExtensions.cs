using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotThrow;
using DotThrow.Scanning;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers DotThrow validators in an <see cref="IServiceCollection"/>. The scanning methods have
/// the same shape as FluentValidation's <c>AddValidatorsFromAssembly</c> family, so moving from
/// FluentValidation is a change of the <c>using</c> directive.
/// </summary>
/// <remarks>
/// <para>
/// Each validator class is registered twice, the same way FluentValidation does it: as every
/// <see cref="IValidator{T}"/> it implements (with <c>TryAddEnumerable</c>, so calling a method
/// twice adds nothing new) and as the class itself (with <c>TryAdd</c>).
/// </para>
/// <para>
/// When several classes validate the same model, all are registered (<c>IEnumerable&lt;IValidator&lt;T&gt;&gt;</c>)
/// and a single <see cref="IValidator{T}"/> resolves to the last one registered. Scanning registers a
/// class after its base classes, then by full type name, so a derived validator wins over its base;
/// between unrelated validators only the names decide. Keep one validator per model, or leave the
/// others out with the <c>filter</c>.
/// Types that cannot be loaded (an assembly they need is missing) are skipped.
/// </para>
/// <para>
/// Assembly scanning uses reflection, so trimming may remove validator types that only scanning
/// finds. In trimmed and Native AOT apps use <see cref="AddValidator{TModel, TValidator}(IServiceCollection, ServiceLifetime)"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.Services.AddValidatorsFromAssemblyContaining&lt;Program&gt;();
///
/// // Native AOT and trimming:
/// builder.Services.AddValidator&lt;SignUp, SignUpValidator&gt;();
/// </code>
/// </example>
public static partial class DotThrowServiceCollectionExtensions
{
    private const string ScanMessage = "Assembly scanning finds validator types with reflection. Trimming may remove them; use AddValidator<TModel, TValidator>() in trimmed and Native AOT apps.";

    /// <summary>Registers every public validator class in <paramref name="assembly"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">The lifetime of the validators. The default is <see cref="ServiceLifetime.Scoped"/> (one per request in web apps).</param>
    /// <param name="filter">Returns <see langword="false"/> for a validator that should not be registered, or <see langword="null"/> to register all.</param>
    /// <param name="includeInternalTypes"><see langword="true"/> to register internal validator classes too.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode(ScanMessage)]
    public static IServiceCollection AddValidatorsFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        Func<AssemblyScanResult, bool>? filter = null,
        bool includeInternalTypes = false)
    {
        services.Throw().IfNull();
        assembly.Throw().IfNull();
        foreach (var result in AssemblyScanner.FindValidatorsInAssembly(assembly, includeInternalTypes))
        {
            AddScanResult(services, result, lifetime, filter);
        }

        return services;
    }

    /// <summary>Registers every public validator class in <paramref name="assemblies"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <param name="lifetime">The lifetime of the validators. The default is <see cref="ServiceLifetime.Scoped"/> (one per request in web apps).</param>
    /// <param name="filter">Returns <see langword="false"/> for a validator that should not be registered, or <see langword="null"/> to register all.</param>
    /// <param name="includeInternalTypes"><see langword="true"/> to register internal validator classes too.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode(ScanMessage)]
    public static IServiceCollection AddValidatorsFromAssemblies(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        Func<AssemblyScanResult, bool>? filter = null,
        bool includeInternalTypes = false)
    {
        services.Throw().IfNull();
        assemblies.Throw().IfNull();
        foreach (var result in AssemblyScanner.FindValidatorsInAssemblies(assemblies, includeInternalTypes))
        {
            AddScanResult(services, result, lifetime, filter);
        }

        return services;
    }

    /// <summary>Registers every public validator class in the assembly that defines <paramref name="type"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="type">A type in the assembly to scan.</param>
    /// <param name="lifetime">The lifetime of the validators. The default is <see cref="ServiceLifetime.Scoped"/> (one per request in web apps).</param>
    /// <param name="filter">Returns <see langword="false"/> for a validator that should not be registered, or <see langword="null"/> to register all.</param>
    /// <param name="includeInternalTypes"><see langword="true"/> to register internal validator classes too.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode(ScanMessage)]
    public static IServiceCollection AddValidatorsFromAssemblyContaining(
        this IServiceCollection services,
        Type type,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        Func<AssemblyScanResult, bool>? filter = null,
        bool includeInternalTypes = false)
    {
        type.Throw().IfNull();
        return services.AddValidatorsFromAssembly(type.Assembly, lifetime, filter, includeInternalTypes);
    }

    /// <summary>Registers every public validator class in the assembly that defines <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">A type in the assembly to scan, for example <c>Program</c>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The lifetime of the validators. The default is <see cref="ServiceLifetime.Scoped"/> (one per request in web apps).</param>
    /// <param name="filter">Returns <see langword="false"/> for a validator that should not be registered, or <see langword="null"/> to register all.</param>
    /// <param name="includeInternalTypes"><see langword="true"/> to register internal validator classes too.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode(ScanMessage)]
    public static IServiceCollection AddValidatorsFromAssemblyContaining<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        Func<AssemblyScanResult, bool>? filter = null,
        bool includeInternalTypes = false) =>
        services.AddValidatorsFromAssembly(typeof(T).Assembly, lifetime, filter, includeInternalTypes);

    /// <summary>
    /// Registers one validator class as <see cref="IValidator{T}"/> of <typeparamref name="TModel"/>
    /// and as itself. No reflection scanning, so it is safe for trimming and Native AOT.
    /// </summary>
    /// <typeparam name="TModel">The validated type.</typeparam>
    /// <typeparam name="TValidator">The validator class.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The lifetime of the validator. The default is <see cref="ServiceLifetime.Scoped"/> (one per request in web apps).</param>
    /// <returns>The service collection.</returns>
    /// <example><c>services.AddValidator&lt;SignUp, SignUpValidator&gt;(ServiceLifetime.Singleton);</c></example>
    public static IServiceCollection AddValidator<TModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TValidator>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TValidator : class, IValidator<TModel>
    {
        services.Throw().IfNull();
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IValidator<TModel>), typeof(TValidator), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(TValidator), typeof(TValidator), lifetime));
        return services;
    }

    /// <summary>
    /// Registers a validator object as a singleton <see cref="IValidator{T}"/>, for example an
    /// inline <see cref="Validator{T}"/>. Safe for trimming and Native AOT.
    /// </summary>
    /// <typeparam name="TModel">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="validator">The validator. It is shared, so it must be thread-safe (every <see cref="Validator{T}"/> is).</param>
    /// <returns>The service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddValidator(new Validator&lt;SignUp&gt;()
    ///     .For(x =&gt; x.Email, email =&gt; email.IfNullOrWhiteSpace().IfNotEmailAddress()));
    /// </code>
    /// </example>
    public static IServiceCollection AddValidator<TModel>(this IServiceCollection services, IValidator<TModel> validator)
    {
        services.Throw().IfNull();
        validator.Throw().IfNull();
        services.Add(ServiceDescriptor.Singleton(validator));
        return services;
    }

    /// <summary>
    /// Registers a factory that creates the <see cref="IValidator{T}"/> of <typeparamref name="TModel"/>,
    /// for validators that need other services. Safe for trimming and Native AOT.
    /// </summary>
    /// <typeparam name="TModel">The validated type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">Creates the validator.</param>
    /// <param name="lifetime">The lifetime of the validator. The default is <see cref="ServiceLifetime.Scoped"/> (one per request in web apps).</param>
    /// <returns>The service collection.</returns>
    /// <example><c>services.AddValidator(sp =&gt; new SignUpValidator(sp.GetRequiredService&lt;IUserStore&gt;()));</c></example>
    public static IServiceCollection AddValidator<TModel>(
        this IServiceCollection services,
        Func<IServiceProvider, IValidator<TModel>> factory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        services.Throw().IfNull();
        factory.Throw().IfNull();
        services.Add(new ServiceDescriptor(typeof(IValidator<TModel>), factory, lifetime));
        return services;
    }

    [RequiresUnreferencedCode(ScanMessage)]
    private static void AddScanResult(IServiceCollection services, AssemblyScanResult result, ServiceLifetime lifetime, Func<AssemblyScanResult, bool>? filter)
    {
        if (filter is not null && !filter(result))
        {
            return;
        }

        // As FluentValidation: the interface with TryAddEnumerable, the class with TryAdd.
        services.TryAddEnumerable(new ServiceDescriptor(result.InterfaceType, result.ValidatorType, lifetime));
        services.TryAdd(new ServiceDescriptor(result.ValidatorType, result.ValidatorType, lifetime));
    }
}
