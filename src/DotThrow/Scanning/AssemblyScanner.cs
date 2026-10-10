using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotThrow.Internal;

namespace DotThrow.Scanning;

/// <summary>
/// Finds validator types in assemblies: every concrete, non-generic class that implements
/// <see cref="IValidator{T}"/>, for example a <c>UserValidator : Validator&lt;User&gt;</c>.
/// The DependencyInjection package uses it to register validators; it also works on its own.
/// </summary>
/// <remarks>
/// Scanning uses reflection over all types of an assembly, so trimming may remove validators
/// that are only found this way. In trimmed or native AOT apps, register validators explicitly.
/// </remarks>
/// <example>
/// <code>
/// foreach (var result in AssemblyScanner.FindValidatorsInAssemblyContaining&lt;UserValidator&gt;())
/// {
///     services.AddSingleton(result.InterfaceType, result.ValidatorType);
/// }
/// </code>
/// </example>
[RequiresUnreferencedCode(TrimMessage)]
public sealed class AssemblyScanner : IEnumerable<AssemblyScanResult>
{
    private const string TrimMessage = "Assembly scanning finds validator types with reflection. Trimming may remove them; register validators explicitly in trimmed apps.";

    private readonly IEnumerable<Type> _types;

    /// <summary>Initializes a new instance of the <see cref="AssemblyScanner"/> class over the given types.</summary>
    /// <param name="types">The types to look at.</param>
    public AssemblyScanner(IEnumerable<Type> types)
    {
        Argument.NotNull(types);
        _types = types;
    }

    /// <summary>Finds the validators in an assembly.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="includeInternalTypes"><see langword="true"/> to include internal validator classes; by default only public ones are found.</param>
    /// <returns>The scanner.</returns>
    public static AssemblyScanner FindValidatorsInAssembly(Assembly assembly, bool includeInternalTypes = false)
    {
        Argument.NotNull(assembly);
        return new AssemblyScanner(TypesOf(assembly, includeInternalTypes));
    }

    /// <summary>Finds the validators in several assemblies. A type is reported once.</summary>
    /// <param name="assemblies">The assemblies.</param>
    /// <param name="includeInternalTypes"><see langword="true"/> to include internal validator classes.</param>
    /// <returns>The scanner.</returns>
    public static AssemblyScanner FindValidatorsInAssemblies(IEnumerable<Assembly> assemblies, bool includeInternalTypes = false)
    {
        Argument.NotNull(assemblies);
        var seen = new HashSet<Type>();
        var types = new List<Type>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in TypesOf(assembly, includeInternalTypes))
            {
                if (seen.Add(type))
                {
                    types.Add(type);
                }
            }
        }

        return new AssemblyScanner(types);
    }

    /// <summary>Finds the validators in the assembly that defines <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">A type in the assembly.</typeparam>
    /// <returns>The scanner.</returns>
    public static AssemblyScanner FindValidatorsInAssemblyContaining<T>() => FindValidatorsInAssembly(typeof(T).Assembly);

    /// <summary>Finds the validators in the assembly that defines <paramref name="type"/>.</summary>
    /// <param name="type">A type in the assembly.</param>
    /// <returns>The scanner.</returns>
    public static AssemblyScanner FindValidatorsInAssemblyContaining(Type type)
    {
        Argument.NotNull(type);
        return FindValidatorsInAssembly(type.Assembly);
    }

    /// <summary>Runs <paramref name="action"/> for every validator found.</summary>
    /// <param name="action">The action.</param>
    public void ForEach(Action<AssemblyScanResult> action)
    {
        Argument.NotNull(action);
        foreach (var result in this)
        {
            action(result);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<AssemblyScanResult> GetEnumerator()
    {
        foreach (var type in _types)
        {
            if (type is null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IValidator<>))
                {
                    yield return new AssemblyScanResult(interfaceType, type);
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static Type[] TypesOf(Assembly assembly, bool includeInternalTypes)
    {
        // Dynamic assemblies (for example from AppDomain.GetAssemblies()) cannot list exported types.
        if (assembly.IsDynamic)
        {
            return Type.EmptyTypes;
        }

        if (!includeInternalTypes)
        {
            return assembly.GetExportedTypes();
        }

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // Some types could not be loaded (a missing dependency); scan the ones that could.
            var loaded = new List<Type>();
            foreach (var type in exception.Types)
            {
                if (type is not null)
                {
                    loaded.Add(type);
                }
            }

            return [.. loaded];
        }
    }
}

/// <summary>One validator found by <see cref="AssemblyScanner"/>.</summary>
public sealed class AssemblyScanResult
{
    /// <summary>Initializes a new instance of the <see cref="AssemblyScanResult"/> class.</summary>
    /// <param name="interfaceType">The closed <see cref="IValidator{T}"/> interface, for example <c>IValidator&lt;User&gt;</c>.</param>
    /// <param name="validatorType">The validator class.</param>
    public AssemblyScanResult(Type interfaceType, Type validatorType)
    {
        Argument.NotNull(interfaceType);
        Argument.NotNull(validatorType);
        InterfaceType = interfaceType;
        ValidatorType = validatorType;
    }

    /// <summary>Gets the closed <see cref="IValidator{T}"/> interface, for example <c>IValidator&lt;User&gt;</c>.</summary>
    public Type InterfaceType { get; }

    /// <summary>Gets the validator class.</summary>
    public Type ValidatorType { get; }

    /// <summary>Gets the validated type, for example <c>User</c>.</summary>
    public Type ModelType => InterfaceType.GetGenericArguments()[0];

    /// <inheritdoc/>
    public override string ToString() => $"{ValidatorType.Name} : {TypeNames.Display(InterfaceType)}";
}
