using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace DotThrow.Dynamic.Internal;

// Properties found by reflection, for the reflection constructor of DynamicValidator. Each
// path is turned once into typed getter delegates (no reflection per validation for classes).
[RequiresUnreferencedCode(DynamicMessages.Trimming)]
[RequiresDynamicCode(DynamicMessages.DynamicCode)]
internal sealed class ReflectionSource<T> : PropertySource<T>
{
    private readonly object _gate = new();
    // Exact spelling: "Code" and "code" can be two properties.
    private readonly Dictionary<string, PropertyEntry<T>?> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _problems = new(StringComparer.Ordinal);

    public ReflectionSource()
        : this(new TypeCatalog())
    {
    }

    public ReflectionSource(TypeCatalog types)
        : base(types)
    {
        types.Reflection ??= new ReflectionTypes();
    }

    public override IEnumerable<string> Paths
    {
        get
        {
            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return property.Name;
            }
        }
    }

    public override PropertyEntry<T>? Find(string path)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(path, out var entry))
            {
                try
                {
                    entry = Create(path);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or TypeLoadException or InvalidOperationException or MemberAccessException)
                {
                    // A property type reflection cannot work with; the build reports it.
                    _problems[path] = $"'{path}' cannot be read by the reflection constructor ({exception.Message}); use a PropertyMap";
                    entry = null;
                }

                _entries[path] = entry;
            }

            return entry;
        }
    }

    public override string? Problem(string path)
    {
        lock (_gate)
        {
            return _problems.TryGetValue(path, out var problem) ? problem : null;
        }
    }

    // Walks the path once and composes typed getters. Like "?." in code, a null object on the
    // way gives null, and a value-type property after it becomes nullable (Customer.Age is int?).
    private PropertyEntry<T>? Create(string path)
    {
        var names = new List<string>();
        var current = typeof(T);
        object? getter = null;
        foreach (var segment in path.Split('.'))
        {
            var owner = Nullable.GetUnderlyingType(current) ?? current;
            if (FindProperty(owner, segment.Trim()) is not { } property)
            {
                return null;
            }

            var member = Getters.Member(owner, property);
            if (getter is null)
            {
                getter = member;
                current = property.PropertyType;
            }
            else
            {
                getter = Getters.Compose(typeof(T), current, owner, property.PropertyType, getter, member, out current);
            }

            names.Add(property.Name);
        }

        // Collections of reference type get Each and item paths; a struct collection
        // (ImmutableArray<T>) is a plain value with the collection checks.
        var canonical = string.Join(".", names);
        if (!current.IsValueType && current != typeof(string) && Getters.ItemType(current) is { } item)
        {
            var items = Activator.CreateInstance(typeof(ReflectionSource<>).MakeGenericType(item), Types);
            return (PropertyEntry<T>)Activator.CreateInstance(
                typeof(CollectionEntry<,>).MakeGenericType(typeof(T), item),
                canonical,
                getter,
                items,
                current)!;
        }

        return (PropertyEntry<T>)Activator.CreateInstance(typeof(PropertyEntry<,>).MakeGenericType(typeof(T), current), canonical, getter)!;
    }

    // A readable public instance property by name: the exact spelling first, then ignoring
    // case; a property hidden with "new" loses to the one of the most derived type.
    private static PropertyInfo? FindProperty(Type owner, string name)
    {
        PropertyInfo? best = null;
        var bestScore = int.MinValue;
        foreach (var property in owner.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                || property.GetGetMethod() is null
                || property.GetIndexParameters().Length > 0
                || !Readable(property.PropertyType))
            {
                continue;
            }

            var score = (string.Equals(property.Name, name, StringComparison.Ordinal) ? 1000 : 0) + Depth(property.DeclaringType);
            if (score > bestScore)
            {
                best = property;
                bestScore = score;
            }
        }

        return best;
    }

    // Types a delegate can return: not by-ref, pointer or ref struct types.
    private static bool Readable(Type type)
    {
        if (type.IsByRef || type.IsPointer)
        {
            return false;
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return !type.IsByRefLike;
#else
        foreach (var attribute in type.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute")
            {
                return false;
            }
        }

        return true;
#endif
    }

    private static int Depth(Type? type)
    {
        var depth = 0;
        for (var current = type; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }
}

// Families and types the static switch does not know, made by reflection.
[RequiresUnreferencedCode(DynamicMessages.Trimming)]
[RequiresDynamicCode(DynamicMessages.DynamicCode)]
internal sealed class ReflectionTypes : IReflectionTypes
{
    // Null falls back to the general checks of any type.
    public object? CreateFamily(Type valueType, TypeCatalog catalog)
    {
        if (Families.IsKnown(valueType))
        {
            return null;
        }

        try
        {
            return Create(valueType);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or TypeLoadException or InvalidOperationException or MemberAccessException or AmbiguousMatchException or TargetInvocationException)
        {
            return null;
        }
    }

    private static object? Create(Type valueType)
    {
        var underlying = Nullable.GetUnderlyingType(valueType);
        var type = underlying ?? valueType;
        object? family = null;
        if (type.IsEnum)
        {
            family = Activator.CreateInstance(typeof(EnumFamily<>).MakeGenericType(type));
        }
        else if (Comparable(type))
        {
            var parser = Activator.CreateInstance(typeof(ConverterParser<>).MakeGenericType(type));
            var parse = parser!.GetType().GetProperty("Parse")!.GetValue(parser);
            var familyType = type.IsValueType ? typeof(StructComparableFamily<>) : typeof(ComparableFamily<>);
            family = Activator.CreateInstance(familyType.MakeGenericType(type), parse);
        }
        else if (underlying is null && type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type) && Getters.ItemType(type) is { } item)
        {
            return Activator.CreateInstance(typeof(CollectionFamily<,>).MakeGenericType(type, item), new object?[] { null, type });
        }

        if (family is null || underlying is null)
        {
            return family;
        }

        return Activator.CreateInstance(typeof(NullableFamily<>).MakeGenericType(type), family);
    }

    // By assembly-qualified or full name first, then by simple name across the loaded
    // assemblies (the first match wins, so prefer full names).
    public Type? FindType(string name)
    {
        try
        {
            if (Type.GetType(name, throwOnError: false, ignoreCase: true) is { } direct)
            {
                return direct;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(name, throwOnError: false, ignoreCase: true) is { } full)
                {
                    return full;
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or System.IO.IOException or TypeLoadException or BadImageFormatException)
        {
            // Not a type name the runtime can read: try it as a simple name below.
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            Type?[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            foreach (var type in types)
            {
                if (type is not null && string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return type;
                }
            }
        }

        return null;
    }

    public EnumNameCheck? CreateEnumNameCheck(Type enumType) =>
        (EnumNameCheck?)Activator.CreateInstance(typeof(EnumNameCheck<>).MakeGenericType(enumType));

    // Implements IComparable<itself>.
    private static bool Comparable(Type type)
    {
        foreach (var contract in type.GetInterfaces())
        {
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IComparable<>) && contract.GetGenericArguments()[0] == type)
            {
                return true;
            }
        }

        return false;
    }
}

// Argument text for comparable types the static switch does not know: a static
// Parse(string, IFormatProvider) or Parse(string) method of the type, or its TypeConverter.
[RequiresUnreferencedCode(DynamicMessages.Trimming)]
[RequiresDynamicCode(DynamicMessages.DynamicCode)]
internal sealed class ConverterParser<TValue>
{
    public ConverterParser()
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;
        if (typeof(TValue).GetMethod("Parse", Flags, null, [typeof(string), typeof(IFormatProvider)], null) is { } withProvider
            && withProvider.ReturnType == typeof(TValue))
        {
            var parse = Getters.Create<Func<string, IFormatProvider?, TValue>>(withProvider);
            Parse = text => parse(text, CultureInfo.InvariantCulture);
        }
        else if (typeof(TValue).GetMethod("Parse", Flags, null, [typeof(string)], null) is { } plain && plain.ReturnType == typeof(TValue))
        {
            Parse = Getters.Create<Func<string, TValue>>(plain);
        }
        else
        {
            var converter = TypeDescriptor.GetConverter(typeof(TValue));
            Parse = text => converter.CanConvertFrom(typeof(string))
                ? (TValue)converter.ConvertFromString(null, CultureInfo.InvariantCulture, text)!
                : throw new FormatException($"{typeof(TValue).Name} has no Parse method or TypeConverter for text.");
        }
    }

    public Func<string, TValue> Parse { get; }
}

// Typed getter delegates built from PropertyInfo.
[RequiresUnreferencedCode(DynamicMessages.Trimming)]
[RequiresDynamicCode(DynamicMessages.DynamicCode)]
internal static class Getters
{
    public static object Member(Type owner, PropertyInfo property) =>
        ((IGetter)Activator.CreateInstance(typeof(MemberGetter<,>).MakeGenericType(owner, property.PropertyType), property)!).Delegate;

    // first: Func<TRoot, TCurrent>, where TCurrent is TOwner, or TOwner? for a nullable struct.
    // next: Func<TOwner, TLeaf>. The result reads TLeaf, or TLeaf? when TLeaf is a value type
    // and TCurrent can be null.
    public static object Compose(Type root, Type current, Type owner, Type leaf, object first, object next, out Type result)
    {
        var nullableOwner = current != owner;
        var lift = (nullableOwner || !current.IsValueType) && leaf.IsValueType && Nullable.GetUnderlyingType(leaf) is null;
        Type composer;
        if (lift)
        {
            composer = (nullableOwner ? typeof(LiftedNullableComposer<,,>) : typeof(LiftedComposer<,,>)).MakeGenericType(root, owner, leaf);
            result = typeof(Nullable<>).MakeGenericType(leaf);
        }
        else
        {
            composer = (nullableOwner ? typeof(NullableComposer<,,>) : typeof(Composer<,,>)).MakeGenericType(root, owner, leaf);
            result = leaf;
        }

        return ((IGetter)Activator.CreateInstance(composer, first, next)!).Delegate;
    }

#if NET5_0_OR_GREATER
    public static TDelegate Create<TDelegate>(MethodInfo method)
        where TDelegate : Delegate => method.CreateDelegate<TDelegate>();
#else
    public static TDelegate Create<TDelegate>(MethodInfo method)
        where TDelegate : Delegate => (TDelegate)method.CreateDelegate(typeof(TDelegate));
#endif

    // The T of IEnumerable<T>, or null.
    public static Type? ItemType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        foreach (var contract in type.GetInterfaces())
        {
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return contract.GetGenericArguments()[0];
            }
        }

        return null;
    }
}

internal interface IGetter
{
    object Delegate { get; }
}

[RequiresUnreferencedCode(DynamicMessages.Trimming)]
[RequiresDynamicCode(DynamicMessages.DynamicCode)]
internal sealed class MemberGetter<TOwner, TValue> : IGetter
{
    public MemberGetter(PropertyInfo property)
    {
        // A struct getter takes its owner by reference, so it becomes an open delegate with a
        // ref parameter, wrapped without boxing.
        if (typeof(TOwner).IsValueType)
        {
            var getter = Getters.Create<StructGetter>(property.GetMethod!);
            Delegate = new Func<TOwner, TValue>(owner => getter(ref owner));
        }
        else
        {
            Delegate = Getters.Create<Func<TOwner, TValue>>(property.GetMethod!);
        }
    }

    private delegate TValue StructGetter(ref TOwner owner);

    public object Delegate { get; }
}

internal sealed class Composer<TRoot, TOwner, TValue>(Func<TRoot, TOwner> first, Func<TOwner, TValue> next) : IGetter
{
    public object Delegate { get; } = new Func<TRoot, TValue>(root => first(root) is { } owner ? next(owner) : default!);
}

internal sealed class NullableComposer<TRoot, TOwner, TValue>(Func<TRoot, TOwner?> first, Func<TOwner, TValue> next) : IGetter
    where TOwner : struct
{
    public object Delegate { get; } = new Func<TRoot, TValue>(root => first(root) is { } owner ? next(owner) : default!);
}

internal sealed class LiftedComposer<TRoot, TOwner, TValue>(Func<TRoot, TOwner> first, Func<TOwner, TValue> next) : IGetter
    where TValue : struct
{
    public object Delegate { get; } = new Func<TRoot, TValue?>(root => first(root) is { } owner ? next(owner) : null);
}

internal sealed class LiftedNullableComposer<TRoot, TOwner, TValue>(Func<TRoot, TOwner?> first, Func<TOwner, TValue> next) : IGetter
    where TOwner : struct
    where TValue : struct
{
    public object Delegate { get; } = new Func<TRoot, TValue?>(root => first(root) is { } owner ? next(owner) : null);
}

internal static class DynamicMessages
{
    public const string Trimming =
        "The reflection constructor finds properties by name, which trimming may remove. Use the constructor that takes a PropertyMap<T> in trimmed or Native AOT apps.";

    public const string DynamicCode =
        "The reflection constructor makes generic types for property types at run time. Use the constructor that takes a PropertyMap<T> in Native AOT apps.";
}
