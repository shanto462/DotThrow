using System;
using System.Collections;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Globalization;
#endif
using System.IO;
using System.Net;
using System.Numerics;

namespace DotThrow.Dynamic.Internal;

// The static type switch: the check family of a value type, without reflection. Types it
// does not know (enums, custom comparable types, collections with a known item type) come
// from the type catalog (PropertyMap registrations, or the reflection constructor).
internal static class Families
{
    public static CheckFamily<TValue> For<TValue>(TypeCatalog catalog)
    {
        if (catalog.Family<TValue>() is { } registered)
        {
            return registered;
        }

        var type = typeof(TValue);
        if (Known(type) is CheckFamily<TValue> known)
        {
            return known;
        }

        if (typeof(Stream).IsAssignableFrom(type))
        {
            return new StreamFamily<TValue>();
        }

        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            return new CollectionFamily<TValue>(ValueText.Parser<TValue>(), null);
        }

        return new ValueFamily<TValue>(ValueText.Parser<TValue>());
    }

    // Types the switch above handles without the catalog.
    public static bool IsKnown(Type type) => KnownFamilies.ContainsKey(type) || typeof(Stream).IsAssignableFrom(type);

    private static object? Known(Type type) => KnownFamilies.TryGetValue(type, out var create) ? create() : null;

    // The families of the types DotThrow knows, and of their Nullable<T>.
    private static readonly Dictionary<Type, Func<object>> KnownFamilies = new()
    {
        [typeof(string)] = static () => new StringFamily(),
        [typeof(int)] = static () => new Int32Family(),
        [typeof(long)] = static () => new Int64Family(),
        [typeof(decimal)] = static () => new DecimalFamily(),
        [typeof(double)] = static () => new DoubleFamily(),
        [typeof(float)] = static () => new SingleFamily(),
        [typeof(bool)] = static () => new BoolFamily(),
        [typeof(Guid)] = static () => new GuidFamily(),
        [typeof(DateTime)] = static () => new DateTimeFamily(),
        [typeof(DateTimeOffset)] = static () => new DateTimeOffsetFamily(),
        [typeof(TimeSpan)] = static () => new StructComparableFamily<TimeSpan>(ValueText.Parser<TimeSpan>()),
        [typeof(char)] = static () => new StructComparableFamily<char>(ValueText.Parser<char>()),
        [typeof(Version)] = static () => new ComparableFamily<Version>(ValueText.Parser<Version>()),
        [typeof(Uri)] = static () => new UriFamily(),
        [typeof(IPAddress)] = static () => new IPAddressFamily(),
        [typeof(Type)] = static () => new TypeFamily(),
#if NET8_0_OR_GREATER
        [typeof(byte)] = static () => new BinaryIntegerFamily<byte>(),
        [typeof(sbyte)] = static () => new BinaryIntegerFamily<sbyte>(),
        [typeof(short)] = static () => new BinaryIntegerFamily<short>(),
        [typeof(ushort)] = static () => new BinaryIntegerFamily<ushort>(),
        [typeof(uint)] = static () => new BinaryIntegerFamily<uint>(),
        [typeof(ulong)] = static () => new BinaryIntegerFamily<ulong>(),
        [typeof(Int128)] = static () => new BinaryIntegerFamily<Int128>(),
        [typeof(UInt128)] = static () => new BinaryIntegerFamily<UInt128>(),
        [typeof(BigInteger)] = static () => new BinaryIntegerFamily<BigInteger>(),
        [typeof(Half)] = static () => new FloatingPointFamily<Half>(),
        [typeof(DateOnly)] = static () => new DateOnlyFamily(),
        [typeof(TimeOnly)] = static () => new TimeOnlyFamily(),
#else
        [typeof(byte)] = static () => new StructComparableFamily<byte>(ValueText.Parser<byte>()),
        [typeof(sbyte)] = static () => new StructComparableFamily<sbyte>(ValueText.Parser<sbyte>()),
        [typeof(short)] = static () => new StructComparableFamily<short>(ValueText.Parser<short>()),
        [typeof(ushort)] = static () => new StructComparableFamily<ushort>(ValueText.Parser<ushort>()),
        [typeof(uint)] = static () => new StructComparableFamily<uint>(ValueText.Parser<uint>()),
        [typeof(ulong)] = static () => new StructComparableFamily<ulong>(ValueText.Parser<ulong>()),
        [typeof(BigInteger)] = static () => new StructComparableFamily<BigInteger>(ValueText.Parser<BigInteger>()),
#endif
        [typeof(int?)] = static () => Nullable(new Int32Family()),
        [typeof(long?)] = static () => Nullable(new Int64Family()),
        [typeof(decimal?)] = static () => Nullable(new DecimalFamily()),
        [typeof(double?)] = static () => Nullable(new DoubleFamily()),
        [typeof(float?)] = static () => Nullable(new SingleFamily()),
        [typeof(bool?)] = static () => Nullable(new BoolFamily()),
        [typeof(Guid?)] = static () => Nullable(new GuidFamily()),
        [typeof(DateTime?)] = static () => Nullable(new DateTimeFamily()),
        [typeof(DateTimeOffset?)] = static () => Nullable(new DateTimeOffsetFamily()),
        [typeof(TimeSpan?)] = static () => Nullable(new StructComparableFamily<TimeSpan>(ValueText.Parser<TimeSpan>())),
        [typeof(char?)] = static () => Nullable(new StructComparableFamily<char>(ValueText.Parser<char>())),
#if NET8_0_OR_GREATER
        [typeof(byte?)] = static () => Nullable(new BinaryIntegerFamily<byte>()),
        [typeof(sbyte?)] = static () => Nullable(new BinaryIntegerFamily<sbyte>()),
        [typeof(short?)] = static () => Nullable(new BinaryIntegerFamily<short>()),
        [typeof(ushort?)] = static () => Nullable(new BinaryIntegerFamily<ushort>()),
        [typeof(uint?)] = static () => Nullable(new BinaryIntegerFamily<uint>()),
        [typeof(ulong?)] = static () => Nullable(new BinaryIntegerFamily<ulong>()),
        [typeof(Int128?)] = static () => Nullable(new BinaryIntegerFamily<Int128>()),
        [typeof(UInt128?)] = static () => Nullable(new BinaryIntegerFamily<UInt128>()),
        [typeof(BigInteger?)] = static () => Nullable(new BinaryIntegerFamily<BigInteger>()),
        [typeof(Half?)] = static () => Nullable(new FloatingPointFamily<Half>()),
        [typeof(DateOnly?)] = static () => Nullable(new DateOnlyFamily()),
        [typeof(TimeOnly?)] = static () => Nullable(new TimeOnlyFamily()),
#else
        [typeof(byte?)] = static () => Nullable(new StructComparableFamily<byte>(ValueText.Parser<byte>())),
        [typeof(sbyte?)] = static () => Nullable(new StructComparableFamily<sbyte>(ValueText.Parser<sbyte>())),
        [typeof(short?)] = static () => Nullable(new StructComparableFamily<short>(ValueText.Parser<short>())),
        [typeof(ushort?)] = static () => Nullable(new StructComparableFamily<ushort>(ValueText.Parser<ushort>())),
        [typeof(uint?)] = static () => Nullable(new StructComparableFamily<uint>(ValueText.Parser<uint>())),
        [typeof(ulong?)] = static () => Nullable(new StructComparableFamily<ulong>(ValueText.Parser<ulong>())),
        [typeof(BigInteger?)] = static () => Nullable(new StructComparableFamily<BigInteger>(ValueText.Parser<BigInteger>())),
#endif
    };

    private static NullableFamily<TValue> Nullable<TValue>(CheckFamily<TValue> inner)
        where TValue : struct => new(inner);
}

#if NET8_0_OR_GREATER
// NotParsable: "Value" names the type (Int32, int, System.Int32, DateTime, Guid...), "Options"
// an optional culture name (default: the invariant culture).
internal static class Parsable
{
    public static GuardStep<string>? Create(CheckArgs args, string? message)
    {
        var typeName = args.Text(Arg.Value);
        var cultureName = args.OptionalText(Arg.Options);
        if (typeName is null)
        {
            return null;
        }

        IFormatProvider provider;
        try
        {
            provider = cultureName is null ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            args.Error($"'{cultureName}' is not a culture name");
            return null;
        }

        GuardStep<string>? step = Normalize(typeName) switch
        {
            "BYTE" => g => g.IfNotParsable<byte>(provider, message),
            "SBYTE" => g => g.IfNotParsable<sbyte>(provider, message),
            "SHORT" or "INT16" => g => g.IfNotParsable<short>(provider, message),
            "USHORT" or "UINT16" => g => g.IfNotParsable<ushort>(provider, message),
            "INT" or "INT32" => g => g.IfNotParsable<int>(provider, message),
            "UINT" or "UINT32" => g => g.IfNotParsable<uint>(provider, message),
            "LONG" or "INT64" => g => g.IfNotParsable<long>(provider, message),
            "ULONG" or "UINT64" => g => g.IfNotParsable<ulong>(provider, message),
            "INT128" => g => g.IfNotParsable<Int128>(provider, message),
            "UINT128" => g => g.IfNotParsable<UInt128>(provider, message),
            "BIGINTEGER" => g => g.IfNotParsable<BigInteger>(provider, message),
            "HALF" => g => g.IfNotParsable<Half>(provider, message),
            "FLOAT" or "SINGLE" => g => g.IfNotParsable<float>(provider, message),
            "DOUBLE" => g => g.IfNotParsable<double>(provider, message),
            "DECIMAL" => g => g.IfNotParsable<decimal>(provider, message),
            "BOOL" or "BOOLEAN" => g => g.IfNotParsable<bool>(provider, message),
            "CHAR" => g => g.IfNotParsable<char>(provider, message),
            "GUID" => g => g.IfNotParsable<Guid>(provider, message),
            "DATETIME" => g => g.IfNotParsable<DateTime>(provider, message),
            "DATETIMEOFFSET" => g => g.IfNotParsable<DateTimeOffset>(provider, message),
            "DATEONLY" => g => g.IfNotParsable<DateOnly>(provider, message),
            "TIMEONLY" => g => g.IfNotParsable<TimeOnly>(provider, message),
            "TIMESPAN" => g => g.IfNotParsable<TimeSpan>(provider, message),
            _ => null,
        };

        if (step is null)
        {
            args.Error($"'{typeName}' is not a parsable type DotThrow knows (use a number type, bool, char, Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly or TimeSpan)");
        }

        return step;
    }

    // "System.Int32", "Int32" and "int" all become "INT32" or "INT".
    private static string Normalize(string name)
    {
        var trimmed = name.Trim();
        foreach (var prefix in new[] { "System.Numerics.", "System." })
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(prefix.Length);
                break;
            }
        }

        return trimmed.ToUpperInvariant();
    }
}
#endif

// Types known by name (for Type, NotType, NotAssignableTo and NotEnumName) and the check
// families of types the static switch does not know. Filled by PropertyMap registrations, or
// by reflection in the reflection constructor.
internal sealed class TypeCatalog
{
    // A map can be shared by validators that build at the same time, so every access locks.
    private readonly object _gate = new();
    private readonly Dictionary<Type, object> _families = [];
    private readonly Dictionary<string, Type> _types = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, EnumNameCheck> _enumNames = [];

    // Creates families and types by reflection when set (reflection constructor only).
    public IReflectionTypes? Reflection { get; set; }

    public void AddFamily<TValue>(CheckFamily<TValue> family)
    {
        lock (_gate)
        {
            _families[typeof(TValue)] = family;
        }
    }

    public void AddType(Type type)
    {
        lock (_gate)
        {
            _types[type.Name] = type;
            if (type.FullName is { } fullName)
            {
                _types[fullName] = type;
            }
        }
    }

    public void AddEnum<TEnum>()
        where TEnum : struct, Enum
    {
        var family = new EnumFamily<TEnum>();
        lock (_gate)
        {
            AddFamily(family);
            AddFamily(new NullableFamily<TEnum>(family));
            _enumNames[typeof(TEnum)] = new EnumNameCheck<TEnum>();
            AddType(typeof(TEnum));
        }
    }

    public void AddComparable<TValue>(Func<string, TValue> parser)
        where TValue : IComparable<TValue>
    {
        lock (_gate)
        {
            AddFamily(new ComparableFamily<TValue>(parser));
            AddType(typeof(TValue));
        }
    }

    public void AddStructComparable<TValue>(Func<string, TValue> parser)
        where TValue : struct, IComparable<TValue>
    {
        var family = new StructComparableFamily<TValue>(parser);
        lock (_gate)
        {
            AddFamily(family);
            AddFamily(new NullableFamily<TValue>(family));
            AddType(typeof(TValue));
        }
    }

    public CheckFamily<TValue>? Family<TValue>()
    {
        lock (_gate)
        {
            if (_families.TryGetValue(typeof(TValue), out var family))
            {
                return (CheckFamily<TValue>)family;
            }

            if (Reflection?.CreateFamily(typeof(TValue), this) is { } created)
            {
                _families[typeof(TValue)] = created;
                return (CheckFamily<TValue>)created;
            }

            return null;
        }
    }

    public Type? ResolveType(CheckArgs args, Arg arg)
    {
        var name = args.Text(arg)?.Trim();
        if (name is null)
        {
            return null;
        }

        lock (_gate)
        {
            if (_types.TryGetValue(name, out var type))
            {
                return type;
            }

            if (BuiltIn(name) is { } builtIn)
            {
                return builtIn;
            }

            if (Reflection?.FindType(name) is { } found)
            {
                AddType(found);
                return found;
            }
        }

        args.Error($"the type '{name}' is not known; register it with PropertyMap.AddType, or use the reflection constructor");
        return null;
    }

    public GuardStep<string>? NotEnumName(CheckArgs args, bool ignoreCase, string? message)
    {
        if (ResolveType(args, Arg.Value) is not { } type)
        {
            return null;
        }

        if (!type.IsEnum)
        {
            args.Error($"'{TypeText.Of(type)}' is not an enum");
            return null;
        }

        EnumNameCheck? check;
        lock (_gate)
        {
            if (!_enumNames.TryGetValue(type, out check) && Reflection?.CreateEnumNameCheck(type) is { } created)
            {
                _enumNames[type] = check = created;
            }
        }

        if (check is null)
        {
            args.Error($"the enum '{TypeText.Of(type)}' is not registered; add it with PropertyMap.AddEnum, or use the reflection constructor");
            return null;
        }

        return check.Create(ignoreCase, message);
    }

    private static Type? BuiltIn(string name) => name.ToUpperInvariant() switch
    {
        "STRING" or "SYSTEM.STRING" => typeof(string),
        "INT" or "INT32" or "SYSTEM.INT32" => typeof(int),
        "LONG" or "INT64" or "SYSTEM.INT64" => typeof(long),
        "DECIMAL" or "SYSTEM.DECIMAL" => typeof(decimal),
        "DOUBLE" or "SYSTEM.DOUBLE" => typeof(double),
        "BOOL" or "BOOLEAN" or "SYSTEM.BOOLEAN" => typeof(bool),
        "OBJECT" or "SYSTEM.OBJECT" => typeof(object),
        "GUID" or "SYSTEM.GUID" => typeof(Guid),
        "DATETIME" or "SYSTEM.DATETIME" => typeof(DateTime),
        "URI" or "SYSTEM.URI" => typeof(Uri),
        "STREAM" or "SYSTEM.IO.STREAM" => typeof(Stream),
        "IDISPOSABLE" or "SYSTEM.IDISPOSABLE" => typeof(IDisposable),
        "IENUMERABLE" or "SYSTEM.COLLECTIONS.IENUMERABLE" => typeof(IEnumerable),
        _ => null,
    };
}

internal abstract class EnumNameCheck
{
    public abstract GuardStep<string> Create(bool ignoreCase, string? message);
}

internal sealed class EnumNameCheck<TEnum> : EnumNameCheck
    where TEnum : struct, Enum
{
    public override GuardStep<string> Create(bool ignoreCase, string? message) => g => g.IfNotEnumName<TEnum>(ignoreCase, message);
}

// Reflection services of the reflection constructor (not used with a PropertyMap).
internal interface IReflectionTypes
{
    object? CreateFamily(Type valueType, TypeCatalog catalog);

    Type? FindType(string name);

    EnumNameCheck? CreateEnumNameCheck(Type enumType);
}
