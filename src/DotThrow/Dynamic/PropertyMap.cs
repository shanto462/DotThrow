using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DotThrow.Dynamic.Internal;
using DotThrow.Internal;

namespace DotThrow.Dynamic;

/// <summary>
/// The properties a <see cref="DynamicValidator{T}"/> may check, each with an explicit getter.
/// With a map there is no reflection, so it works with trimming and Native AOT. Paths come from
/// the getter source text (<c>x =&gt; x.Customer?.Email</c> is <c>"Customer.Email"</c>) or are
/// given, and are matched ignoring case.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <example>
/// <code>
/// var properties = new PropertyMap&lt;Order&gt;()
///     .Add(x =&gt; x.Number)
///     .Add(x =&gt; x.Customer?.Email)
///     .AddEnum(x =&gt; x.Status)
///     .AddCollection(x =&gt; x.Lines, line =&gt; line.Add(l =&gt; l.Sku).Add(l =&gt; l.Quantity));
/// var validator = new DynamicValidator&lt;Order&gt;(definition, properties);
/// </code>
/// </example>
public sealed class PropertyMap<T>
{
    private readonly MapSource<T> _source;

    /// <summary>Initializes a new instance of the <see cref="PropertyMap{T}"/> class with no properties.</summary>
    public PropertyMap()
        : this(new TypeCatalog())
    {
    }

    private PropertyMap(TypeCatalog types)
    {
        _source = new MapSource<T>(types);
    }

    /// <summary>Gets the registered property paths.</summary>
    public IReadOnlyCollection<string> Paths => [.. _source.Paths];

    internal PropertySource<T> Source => _source;

    /// <summary>Adds a property. Its path is the getter source text without the parameter, for example <c>"Customer.Email"</c>.</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="getter">Reads the value. Use <c>?.</c> for parts that can be <see langword="null"/>.</param>
    /// <param name="getterExpression">The getter source text, filled in by the compiler.</param>
    /// <returns>This map.</returns>
    /// <exception cref="ArgumentException">The getter does not read a property path, or the path is already in the map.</exception>
    public PropertyMap<T> Add<TProperty>(Func<T, TProperty?> getter, [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
    {
        Argument.NotNull(getter);
        return Add(PathOf(getterExpression), getter);
    }

    /// <summary>Adds a property with an explicit path.</summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="path">The path used in definitions and failure names, for example <c>"Customer.Email"</c>.</param>
    /// <param name="getter">Reads the value.</param>
    /// <returns>This map.</returns>
    /// <exception cref="ArgumentException">The path is empty, has <c>[</c> or <c>]</c>, or is already in the map.</exception>
    public PropertyMap<T> Add<TProperty>(string path, Func<T, TProperty?> getter)
    {
        Argument.NotNull(getter);
        return Register(new PropertyEntry<T, TProperty>(CheckPath(path), getter));
    }

    /// <summary>
    /// Adds a collection property. Its items can be checked with <c>Each</c>, and the properties of
    /// its items with <c>"Lines[].Sku"</c> paths when <paramref name="items"/> adds them.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="getter">Reads the collection.</param>
    /// <param name="items">Adds the item properties, or <see langword="null"/>.</param>
    /// <param name="getterExpression">The getter source text, filled in by the compiler.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddCollection<TItem>(
        Func<T, IEnumerable<TItem?>?> getter,
        Action<PropertyMap<TItem>>? items = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null) =>
        AddCollection(PathOf(getterExpression), getter, items);

    /// <summary>Adds a collection property with an explicit path.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="path">The path.</param>
    /// <param name="getter">Reads the collection.</param>
    /// <param name="items">Adds the item properties, or <see langword="null"/>.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddCollection<TItem>(string path, Func<T, IEnumerable<TItem?>?> getter, Action<PropertyMap<TItem>>? items = null)
    {
        Argument.NotNull(getter);
        var checkedPath = CheckPath(path);
        PropertySource<TItem>? itemSource = null;
        if (items is not null)
        {
            var itemMap = new PropertyMap<TItem>(_source.Types);
            items(itemMap);
            itemSource = itemMap.Source;
        }

        return Register(new CollectionEntry<T, TItem>(checkedPath, getter, itemSource, null));
    }

    /// <summary>Adds an enum property and enables the enum checks (<c>NotDefined</c>, <c>HasFlag</c>, <c>NotHasFlag</c>) for it.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="getter">Reads the value.</param>
    /// <param name="getterExpression">The getter source text, filled in by the compiler.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddEnum<TEnum>(Func<T, TEnum> getter, [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TEnum : struct, Enum =>
        AddEnum(PathOf(getterExpression), getter);

    /// <summary>Adds a nullable enum property and enables the enum checks for it.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="getter">Reads the value.</param>
    /// <param name="getterExpression">The getter source text, filled in by the compiler.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddEnum<TEnum>(Func<T, TEnum?> getter, [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TEnum : struct, Enum =>
        AddEnum(PathOf(getterExpression), getter);

    /// <summary>Adds an enum property with an explicit path.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="path">The path.</param>
    /// <param name="getter">Reads the value.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddEnum<TEnum>(string path, Func<T, TEnum> getter)
        where TEnum : struct, Enum
    {
        Argument.NotNull(getter);
        var checkedPath = CheckPath(path);
        AddEnumType<TEnum>();
        return Register(new PropertyEntry<T, TEnum>(checkedPath, x => getter(x)));
    }

    /// <summary>Adds a nullable enum property with an explicit path.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="path">The path.</param>
    /// <param name="getter">Reads the value.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddEnum<TEnum>(string path, Func<T, TEnum?> getter)
        where TEnum : struct, Enum
    {
        Argument.NotNull(getter);
        var checkedPath = CheckPath(path);
        AddEnumType<TEnum>();
        return Register(new PropertyEntry<T, TEnum?>(checkedPath, getter));
    }

    /// <summary>
    /// Adds a property of a comparable type DotThrow does not know (for example a money type), so the
    /// comparison checks (<c>GreaterThan</c>, <c>OutOfRange</c>...) work on it.
    /// </summary>
    /// <typeparam name="TComparable">The property type.</typeparam>
    /// <param name="getter">Reads the value.</param>
    /// <param name="parse">Converts argument text to a value (use the invariant culture).</param>
    /// <param name="getterExpression">The getter source text, filled in by the compiler.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddComparable<TComparable>(
        Func<T, TComparable?> getter,
        Func<string, TComparable> parse,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TComparable : IComparable<TComparable>
    {
        Argument.NotNull(getter);
        Argument.NotNull(parse);
        var checkedPath = CheckPath(PathOf(getterExpression));
        _source.Types.AddComparable(parse);
        return Register(new PropertyEntry<T, TComparable>(checkedPath, getter));
    }

    /// <summary>
    /// Adds a nullable property of a comparable value type DotThrow does not know. The comparison
    /// checks unwrap it, and a <see langword="null"/> fails them with <c>Null</c>.
    /// </summary>
    /// <typeparam name="TComparable">The value type.</typeparam>
    /// <param name="getter">Reads the value.</param>
    /// <param name="parse">Converts argument text to a value (use the invariant culture).</param>
    /// <param name="getterExpression">The getter source text, filled in by the compiler.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddComparable<TComparable>(
        Func<T, TComparable?> getter,
        Func<string, TComparable> parse,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TComparable : struct, IComparable<TComparable>
    {
        Argument.NotNull(getter);
        Argument.NotNull(parse);
        var checkedPath = CheckPath(PathOf(getterExpression));
        _source.Types.AddStructComparable(parse);
        return Register(new PropertyEntry<T, TComparable?>(checkedPath, getter));
    }

    /// <summary>
    /// Makes an enum known by name, for the <c>NotEnumName</c> check on strings and for collection
    /// items of that enum type.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddEnumType<TEnum>()
        where TEnum : struct, Enum
    {
        _source.Types.AddEnum<TEnum>();
        return this;
    }

    /// <summary>Makes a type known by name (its name or full name, for example <c>"Money"</c> or <c>"Shop.Money"</c>), for the <c>Type</c>, <c>NotType</c> and <c>NotAssignableTo</c> checks.</summary>
    /// <param name="type">The type.</param>
    /// <returns>This map.</returns>
    public PropertyMap<T> AddType(Type type)
    {
        Argument.NotNull(type);
        _source.Types.AddType(type);
        return this;
    }

    // The path of a getter like x => x.Customer?.Email: names separated by dots, nothing else.
    private static string PathOf(string? getterExpression)
    {
        var path = SelectorNames.FromExpression(getterExpression, identityName: string.Empty);
        foreach (var part in path.Split('.'))
        {
            if (!IsName(part))
            {
                throw new ArgumentException(
                    $"The getter '{getterExpression}' does not read a property path such as x => x.Customer.Email. Use the overload that takes a path for other getters.",
                    nameof(getterExpression));
            }
        }

        return path;
    }

    private static bool IsName(string part)
    {
        if (part.Length == 0 || !(char.IsLetter(part[0]) || part[0] == '_'))
        {
            return false;
        }

        foreach (var c in part)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private string CheckPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A property path must not be empty.", nameof(path));
        }

        var trimmed = path.Trim();
        if (trimmed.IndexOfAny(['[', ']']) >= 0)
        {
            throw new ArgumentException($"The path '{trimmed}' must not have '[' or ']'; definitions use 'Lines[].Sku' for the items of a collection added with AddCollection.", nameof(path));
        }

        if (_source.Contains(trimmed))
        {
            throw Duplicate(trimmed);
        }

        return trimmed;
    }

    private PropertyMap<T> Register(PropertyEntry<T> entry) => _source.TryAdd(entry) ? this : throw Duplicate(entry.Path);

    private static ArgumentException Duplicate(string path) => new($"The property '{path}' is already in the map.", nameof(path));
}
