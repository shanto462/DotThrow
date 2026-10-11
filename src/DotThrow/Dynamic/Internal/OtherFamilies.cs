using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace DotThrow.Dynamic.Internal;

internal sealed class DateTimeFamily() : StructComparableFamily<DateTime>(ValueText.Parser<DateTime>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<DateTime>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.Before:
                if (args.TryParse(Arg.Value, Parser, out var before))
                {
                    step = g => g.IfBefore(before, message);
                }

                return true;
            case CheckNames.After:
                if (args.TryParse(Arg.Value, Parser, out var after))
                {
                    step = g => g.IfAfter(after, message);
                }

                return true;
            case CheckNames.OlderThan:
                if (Age.Read(args, out var maxAge))
                {
                    step = g => g.IfOlderThan(maxAge, message);
                }

                return true;
            case CheckNames.NewerThan:
                if (Age.Read(args, out var minAge))
                {
                    step = g => g.IfNewerThan(minAge, message);
                }

                return true;
            default:
                step = name switch
                {
                    CheckNames.InPast => g => g.IfInPast(message),
                    CheckNames.InFuture => g => g.IfInFuture(message),
                    CheckNames.NotUtc => g => g.IfNotUtc(message),
                    CheckNames.Weekend => g => g.IfWeekend(message),
                    CheckNames.Weekday => g => g.IfWeekday(message),
                    CheckNames.HasTimeOfDay => g => g.IfHasTimeOfDay(message),
                    _ => null,
                };
                return step is not null;
        }
    }
}

internal sealed class DateTimeOffsetFamily() : StructComparableFamily<DateTimeOffset>(ValueText.Parser<DateTimeOffset>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<DateTimeOffset>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.Before:
                if (args.TryParse(Arg.Value, Parser, out var before))
                {
                    step = g => g.IfBefore(before, message);
                }

                return true;
            case CheckNames.After:
                if (args.TryParse(Arg.Value, Parser, out var after))
                {
                    step = g => g.IfAfter(after, message);
                }

                return true;
            case CheckNames.OlderThan:
                if (Age.Read(args, out var maxAge))
                {
                    step = g => g.IfOlderThan(maxAge, message);
                }

                return true;
            case CheckNames.NewerThan:
                if (Age.Read(args, out var minAge))
                {
                    step = g => g.IfNewerThan(minAge, message);
                }

                return true;
            default:
                step = name switch
                {
                    CheckNames.InPast => g => g.IfInPast(message),
                    CheckNames.InFuture => g => g.IfInFuture(message),
                    CheckNames.NotUtc => g => g.IfNotUtc(message),
                    CheckNames.Weekend => g => g.IfWeekend(message),
                    CheckNames.Weekday => g => g.IfWeekday(message),
                    CheckNames.HasTimeOfDay => g => g.IfHasTimeOfDay(message),
                    _ => null,
                };
                return step is not null;
        }
    }
}

#if NET8_0_OR_GREATER
internal sealed class DateOnlyFamily() : StructComparableFamily<DateOnly>(ValueText.Parser<DateOnly>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<DateOnly>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.Before:
                if (args.TryParse(Arg.Value, Parser, out var before))
                {
                    step = g => g.IfBefore(before, message);
                }

                return true;
            case CheckNames.After:
                if (args.TryParse(Arg.Value, Parser, out var after))
                {
                    step = g => g.IfAfter(after, message);
                }

                return true;
            default:
                step = name switch
                {
                    CheckNames.InPast => g => g.IfInPast(message),
                    CheckNames.InFuture => g => g.IfInFuture(message),
                    CheckNames.Weekend => g => g.IfWeekend(message),
                    CheckNames.Weekday => g => g.IfWeekday(message),
                    _ => null,
                };
                return step is not null;
        }
    }
}

internal sealed class TimeOnlyFamily() : StructComparableFamily<TimeOnly>(ValueText.Parser<TimeOnly>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<TimeOnly>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.Before:
                if (args.TryParse(Arg.Value, Parser, out var before))
                {
                    step = g => g.IfBefore(before, message);
                }

                return true;
            case CheckNames.After:
                if (args.TryParse(Arg.Value, Parser, out var after))
                {
                    step = g => g.IfAfter(after, message);
                }

                return true;
            default:
                return false;
        }
    }
}
#endif

internal sealed class BoolFamily() : StructComparableFamily<bool>(ValueText.Parser<bool>())
{
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<bool>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.True => g => g.IfTrue(message),
            CheckNames.False => g => g.IfFalse(message),
            _ => null,
        };
        return step is not null;
    }
}

internal sealed class GuidFamily() : StructComparableFamily<Guid>(ValueText.Parser<Guid>())
{
    // Guid has its own IfEmpty (code: guard.IfEmpty() picks it).
    protected override bool TryCreateType(string name, CheckArgs args, out GuardStep<Guid>? step)
    {
        var message = args.Message;
        step = name == CheckNames.Empty ? g => g.IfEmpty(message) : null;
        return step is not null;
    }
}

internal sealed class UriFamily() : ComparableFamilyless<Uri>(ValueText.Parser<Uri>())
{
    protected override bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<Uri>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.NotScheme:
                if (args.TryParseList(static text => text, out var schemes))
                {
                    step = g => g.IfNotScheme(schemes, message);
                }

                return true;
            default:
                step = name switch
                {
                    CheckNames.Relative => g => g.IfRelative(message),
                    CheckNames.NotHttps => g => g.IfNotHttps(message),
                    CheckNames.NotHttpOrHttps => g => g.IfNotHttpOrHttps(message),
                    CheckNames.Loopback => g => g.IfLoopback(message),
                    _ => null,
                };
                return step is not null;
        }
    }
}

internal sealed class IPAddressFamily() : ComparableFamilyless<IPAddress>(ValueText.Parser<IPAddress>())
{
    protected override bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<IPAddress>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.Loopback => g => g.IfLoopback(message),
            CheckNames.Private => g => g.IfPrivate(message),
            CheckNames.NotIPv4 => g => g.IfNotIPv4(message),
            CheckNames.NotIPv6 => g => g.IfNotIPv6(message),
            _ => null,
        };
        return step is not null;
    }
}

internal sealed class TypeFamily() : ComparableFamilyless<Type>(null)
{
    protected override bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<Type>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.Abstract:
                step = g => g.IfAbstract(message);
                return true;
            case CheckNames.Interface:
                step = g => g.IfInterface(message);
                return true;
            case CheckNames.NotAssignableTo:
                if (args.Context.Types.ResolveType(args, Arg.Value) is { } target)
                {
                    step = g => g.IfNotAssignableTo(target, message);
                }

                return true;
            default:
                return false;
        }
    }
}

// Any Stream type: the checks run on the value as a Stream.
internal sealed class StreamFamily<TValue>() : ComparableFamilyless<TValue>(null)
{
    protected override bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = name switch
        {
            CheckNames.NotReadable => g => g.Map((Stream)(object)g.Value!).IfNotReadable(message).Map(g.Value),
            CheckNames.NotWritable => g => g.Map((Stream)(object)g.Value!).IfNotWritable(message).Map(g.Value),
            CheckNames.NotSeekable => g => g.Map((Stream)(object)g.Value!).IfNotSeekable(message).Map(g.Value),
            _ => null,
        };
        return step is not null;
    }
}

// Any collection (IEnumerable, not string): the count checks run on the value as an
// IEnumerable; Contains and NotContains need the item type (CollectionFamily with items).
// declaredType: the property type when TValue is IEnumerable<TItem> (reflection collections),
// so dictionary checks know the dictionary type.
internal class CollectionFamily<TValue>(Func<string, TValue>? parser, Type? declaredType) : ComparableFamilyless<TValue>(parser)
{
    protected override bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.NullOrEmpty:
                step = g => g.Map(AsEnumerable(g.Value)).IfNullOrEmpty(message).Map(g.Value);
                return true;
            case CheckNames.ContainsNull:
                step = g => g.Map(AsEnumerable(g.Value)).IfContainsNull(message).Map(g.Value);
                return true;
            case CheckNames.HasDuplicates:
                step = g => g.Map(AsEnumerable(g.Value)).IfHasDuplicates(message).Map(g.Value);
                return true;
            case CheckNames.CountEqualTo:
                if (Count(args, Arg.Value, out var count))
                {
                    step = g => g.Map(AsEnumerable(g.Value)).IfCountEqualTo(count, message).Map(g.Value);
                }

                return true;
            case CheckNames.CountNotEqualTo:
                if (Count(args, Arg.Value, out var exact))
                {
                    step = g => g.Map(AsEnumerable(g.Value)).IfCountNotEqualTo(exact, message).Map(g.Value);
                }

                return true;
            case CheckNames.CountGreaterThan:
                if (Count(args, Arg.Value, out var maxCount))
                {
                    step = g => g.Map(AsEnumerable(g.Value)).IfCountGreaterThan(maxCount, message).Map(g.Value);
                }

                return true;
            case CheckNames.CountLessThan:
                if (Count(args, Arg.Value, out var minCount))
                {
                    step = g => g.Map(AsEnumerable(g.Value)).IfCountLessThan(minCount, message).Map(g.Value);
                }

                return true;
            case CheckNames.CountOutOfRange:
                var hasMin = Count(args, Arg.Min, out var rangeMin);
                var hasMax = Count(args, Arg.Max, out var rangeMax);
                if (hasMin && hasMax && rangeMin > rangeMax)
                {
                    args.Error("'Min' is greater than 'Max'");
                }
                else if (hasMin && hasMax)
                {
                    step = g => g.Map(AsEnumerable(g.Value)).IfCountOutOfRange(rangeMin, rangeMax, message).Map(g.Value);
                }

                return true;
            case CheckNames.ContainsKey:
            case CheckNames.NotContainsKey:
                step = DictionaryKey(name, args, message, declaredType ?? typeof(TValue));
                return true;
            default:
                return TryCreateItems(name, args, out step);
        }
    }

    protected virtual bool TryCreateItems(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        step = null;
        if (name is CheckNames.Contains or CheckNames.NotContains)
        {
            args.Error($"needs the item type of {TypeText.Of(typeof(TValue))}; add the property with PropertyMap.AddCollection, or use the reflection constructor");
            return true;
        }

        return false;
    }

    protected static IEnumerable AsEnumerable(TValue value) => (IEnumerable)(object)value!;

    private static bool Count(CheckArgs args, Arg arg, out int count)
    {
        if (!args.TryInt(arg, out count))
        {
            return false;
        }

        if (count < 0)
        {
            args.Error($"'{arg}' must not be negative");
            return false;
        }

        return true;
    }

    // Dictionaries that implement the non-generic IDictionary (Dictionary<K,V>, SortedDictionary,
    // ConcurrentDictionary, Hashtable...). The key text is converted to the key type.
    private static GuardStep<TValue>? DictionaryKey(string name, CheckArgs args, string? message, Type type)
    {
        if (!typeof(IDictionary).IsAssignableFrom(type))
        {
            args.Error($"applies to dictionaries that implement IDictionary, not {TypeText.Of(type)}");
            return null;
        }

        var keyType = KeyType(type);
        if (!args.TryParse(Arg.Value, text => ValueText.ParseObject(keyType, text)!, out var key, keyType))
        {
            return null;
        }

        return name == CheckNames.ContainsKey
            ? g => g.Map((IDictionary)(object)g.Value!).IfContainsKey(key, message).Map(g.Value)
            : g => g.Map((IDictionary)(object)g.Value!).IfNotContainsKey(key, message).Map(g.Value);
    }

    // The key type of Dictionary<K,V> and similar generic dictionaries, or string.
    private static Type KeyType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericArguments().Length == 2)
            {
                return current.GetGenericArguments()[0];
            }
        }

        return typeof(string);
    }
}

internal sealed class CollectionFamily<TValue, TItem>(Func<string, TValue>? parser, Type? declaredType) : CollectionFamily<TValue>(parser, declaredType)
{
    protected override bool TryCreateItems(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = null;
        if (name is not (CheckNames.Contains or CheckNames.NotContains))
        {
            return false;
        }

        if (args.TryParse(Arg.Value, ValueText.Parser<TItem>(), out var item))
        {
            step = name == CheckNames.Contains
                ? g => g.Map((IEnumerable<TItem>)(object)g.Value!).IfContains(item, null, message).Map(g.Value)
                : g => g.Map((IEnumerable<TItem>)(object)g.Value!).IfNotContains(item, null, message).Map(g.Value);
        }

        return true;
    }
}

internal sealed class EnumFamily<TEnum>() : ComparableFamilyless<TEnum>(ValueText.Parser<TEnum>())
    where TEnum : struct, Enum
{
    protected override bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<TEnum>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.NotDefined:
                step = g => g.IfNotDefined(message);
                return true;
            case CheckNames.HasFlag:
                if (args.TryParse(Arg.Value, Parser, out var flag))
                {
                    step = g => g.IfHasFlag(flag, message);
                }

                return true;
            case CheckNames.NotHasFlag:
                if (args.TryParse(Arg.Value, Parser, out var required))
                {
                    step = g => g.IfNotHasFlag(required, message);
                }

                return true;
            default:
                return false;
        }
    }
}

// A family with only its own checks and the general ones (no comparisons).
internal abstract class ComparableFamilyless<TValue>(Func<string, TValue>? parser) : ValueFamily<TValue>(parser)
{
}

internal static class Age
{
    public static bool Read(CheckArgs args, out TimeSpan age)
    {
        if (!args.TryParse(Arg.Value, ValueText.Parser<TimeSpan>(), out age))
        {
            return false;
        }

        if (age < TimeSpan.Zero)
        {
            args.Error("'Value' must not be negative");
            return false;
        }

        return true;
    }
}
