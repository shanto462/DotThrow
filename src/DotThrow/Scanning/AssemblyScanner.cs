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
/// <para>
/// Results come in a fixed order: a validator class comes after its base classes (fewer base
/// classes first), then by full type name. Dependency injection registers them in this order, so
/// when several validators exist for one model, a single <see cref="IValidator{T}"/> resolves to the
/// last one: a derived validator wins over its base. All of them stay available as a list.
/// </para>
/// <para>
/// Types that cannot be loaded (for example because an assembly they depend on is missing) are
/// skipped, so scanning <c>AppDomain.CurrentDomain.GetAssemblies()</c> works.
/// </para>
/// <para>
/// Scanning uses reflection over all types of an assembly, so trimming may remove validators
/// that are only found this way. In trimmed or native AOT apps, register validators explicitly.
/// </para>
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
        var found = new List<Found>();
        foreach (var type in _types)
        {
            if (type is not null)
            {
                Inspect(type, found);
            }
        }

        // Base classes before derived classes, then by name: the order does not depend on metadata order.
        found.Sort(static (a, b) =>
        {
            var order = a.Depth.CompareTo(b.Depth);
            if (order == 0)
            {
                order = string.CompareOrdinal(a.Name, b.Name);
            }

            return order == 0 ? a.Index.CompareTo(b.Index) : order;
        });

        foreach (var item in found)
        {
            yield return item.Result;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Adds the validator interfaces of one type. A type that cannot be loaded (a base type or an
    // interface from a missing assembly) is skipped.
    private static void Inspect(Type type, List<Found> found)
    {
        try
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
            {
                return;
            }

            var depth = 0;
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                depth++;
            }

            var name = type.FullName ?? type.Name;
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IValidator<>))
                {
                    found.Add(new Found(depth, name, found.Count, new AssemblyScanResult(interfaceType, type)));
                }
            }
        }
        catch (Exception exception) when (IsLoadFailure(exception))
        {
            // Skip the type.
        }
    }

    private static Type[] TypesOf(Assembly assembly, bool includeInternalTypes)
    {
        // Dynamic assemblies (for example from AppDomain.GetAssemblies()) cannot list exported types.
        if (assembly.IsDynamic)
        {
            return Type.EmptyTypes;
        }

        if (!includeInternalTypes)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (Exception exception) when (IsLoadFailure(exception))
            {
                // A public type depends on a missing assembly: keep the public types that load.
                var visible = new List<Type>();
                foreach (var type in LoadableTypes(assembly))
                {
                    if (IsVisible(type))
                    {
                        visible.Add(type);
                    }
                }

                return [.. visible];
            }
        }

        return LoadableTypes(assembly);
    }

    private static Type[] LoadableTypes(Assembly assembly)
    {
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
        catch (Exception exception) when (IsLoadFailure(exception))
        {
            return Type.EmptyTypes;
        }
    }

    private static bool IsVisible(Type type)
    {
        try
        {
            return type.IsVisible;
        }
        catch (Exception exception) when (IsLoadFailure(exception))
        {
            return false;
        }
    }

    private static bool IsLoadFailure(Exception exception) =>
        exception is ReflectionTypeLoadException or TypeLoadException or System.IO.FileNotFoundException
            or System.IO.FileLoadException or BadImageFormatException;

    private readonly struct Found(int depth, string name, int index, AssemblyScanResult result)
    {
        public int Depth { get; } = depth;

        public string Name { get; } = name;

        public int Index { get; } = index;

        public AssemblyScanResult Result { get; } = result;
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
