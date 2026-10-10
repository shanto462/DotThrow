using System;
using System.Collections.Generic;
using DotThrow.Internal;

namespace DotThrow.Dynamic.Internal;

// Where the properties of T come from: a PropertyMap (explicit getters, AOT safe) or
// reflection (the reflection constructor).
internal abstract class PropertySource<T>
{
    protected PropertySource(TypeCatalog types)
    {
        Types = types;
        Root = new PropertyEntry<T, T>(string.Empty, static x => x);
    }

    public TypeCatalog Types { get; }

    // The object itself (an empty property path).
    public PropertyEntry<T> Root { get; }

    public abstract PropertyEntry<T>? Find(string path);

    public abstract IEnumerable<string> Paths { get; }

    // Why a path cannot be used, when it is not simply unknown.
    public virtual string? Problem(string path) => null;
}

// A map can get new properties while a validator that uses it builds, so every access locks.
internal sealed class MapSource<T>(TypeCatalog types) : PropertySource<T>(types)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PropertyEntry<T>> _entries = new(StringComparer.OrdinalIgnoreCase);

    public override IEnumerable<string> Paths
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries.Keys];
            }
        }
    }

    // Adds the entry unless its path is taken; returns false when it is.
    public bool TryAdd(PropertyEntry<T> entry)
    {
        lock (_gate)
        {
            if (_entries.ContainsKey(entry.Path))
            {
                return false;
            }

            _entries.Add(entry.Path, entry);
            return true;
        }
    }

    public bool Contains(string path)
    {
        lock (_gate)
        {
            return _entries.ContainsKey(path);
        }
    }

    public override PropertyEntry<T>? Find(string path)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(path, out var entry) ? entry : null;
        }
    }
}

// One property path with a typed getter. The generic code below is where the check family,
// the rules and the conditions get their value type, so nothing else needs reflection.
internal abstract class PropertyEntry<T>(string path)
{
    public string Path { get; } = path;

    public abstract Type ValueType { get; }

    public abstract Rule<T>? CreateRule(Validator<T> owner, string path, RuleDefinition rule, BuildContext context, string location);

    public virtual Rule<T>? CreateEachRule(Validator<T> owner, string path, RuleDefinition rule, BuildContext context, string location)
    {
        context.Error(location, NotCollection("Each"));
        return null;
    }

    // "Lines[].Sku": a ForEach rule over this collection with an inline validator for the item rule.
    public virtual void AddItemRule(Validator<T> owner, RuleDefinition itemRule, RuleScope<T> scope, BuildContext context, string location) =>
        context.Error(location, NotCollection("[]"));

    private string NotCollection(string feature) =>
        ValueType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(ValueType)
            ? $"'{feature}' needs the item type of '{Path}'; add it with PropertyMap.AddCollection instead of Add"
            : $"'{feature}' needs a collection, but '{Path}' is {TypeText.Of(ValueType)}";

    public abstract Func<T, bool>? CreateCondition(ConditionDefinition condition, BuildContext context, string location);
}

internal class PropertyEntry<T, TValue>(string path, Func<T, TValue?> getter) : PropertyEntry<T>(path)
{
    public Func<T, TValue?> Getter { get; } = getter;

    public override Type ValueType => typeof(TValue);

    public override Rule<T>? CreateRule(Validator<T> owner, string path, RuleDefinition rule, BuildContext context, string location)
    {
        var checks = CheckCompiler.Compile(Family(context), rule, path, context, location);
        return checks is null ? null : new DynamicValueRule<T, TValue>(owner, path, Getter, checks, rule.AllowNull);
    }

    public override Func<T, bool>? CreateCondition(ConditionDefinition condition, BuildContext context, string location)
    {
        var hasEqual = condition.EqualTo is not null;
        var hasNotEqual = condition.NotEqualTo is not null;
        var isNull = condition.IsNull;
        if (!hasEqual && !hasNotEqual && isNull is null)
        {
            context.Error(location, "a condition needs 'EqualTo', 'NotEqualTo' or 'IsNull'");
            return null;
        }

        var parser = ValueText.Parser<TValue>();
        if ((hasEqual || hasNotEqual) && parser is null)
        {
            context.Error(location, $"'{Path}' is {TypeText.Of(typeof(TValue))}, which has no text form to compare with");
            return null;
        }

        var ok = true;
        var equal = hasEqual ? Parse(parser!, condition.EqualTo!, "EqualTo", context, location, ref ok) : default;
        var notEqual = hasNotEqual ? Parse(parser!, condition.NotEqualTo!, "NotEqualTo", context, location, ref ok) : default;
        if (!ok)
        {
            return null;
        }

        var getter = Getter;
        var comparer = EqualityComparer<TValue?>.Default;
        return instance =>
        {
            var value = getter(instance);
            return (isNull is not { } mustBeNull || (value is null) == mustBeNull)
                && (!hasEqual || comparer.Equals(value, equal))
                && (!hasNotEqual || !comparer.Equals(value, notEqual));
        };
    }

    protected virtual CheckFamily<TValue> Family(BuildContext context) => Families.For<TValue>(context.Types);

    private static TValue? Parse(Func<string, TValue> parser, string text, string name, BuildContext context, string location, ref bool ok)
    {
        try
        {
            return parser(text);
        }
#pragma warning disable CA1031 // Do not catch general exception types: parse errors become definition errors.
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
#pragma warning restore CA1031
        {
            context.Error(location, $"'{name}' value '{text}' is not a valid {TypeText.Of(typeof(TValue))}{TextFormatException.DetailOf(exception)}");
            ok = false;
            return default;
        }
    }
}

// A collection property with a known item type: count and Contains checks, Each rules and
// "[]" item rules.
internal sealed class CollectionEntry<T, TItem>(string path, Func<T, IEnumerable<TItem?>?> getter, PropertySource<TItem>? items, Type? declaredType)
    : PropertyEntry<T, IEnumerable<TItem?>>(path, getter)
{
    public PropertySource<TItem>? Items { get; } = items;

    public override Rule<T>? CreateEachRule(Validator<T> owner, string path, RuleDefinition rule, BuildContext context, string location)
    {
        var checks = CheckCompiler.Compile(Families.For<TItem>(context.Types), rule, path, context, location);
        return checks is null ? null : new DynamicEachRule<T, TItem>(owner, path, Getter, checks, rule.AllowNull);
    }

    public override void AddItemRule(Validator<T> owner, RuleDefinition itemRule, RuleScope<T> scope, BuildContext context, string location)
    {
        if (Items is null)
        {
            context.Error(location, $"the items of '{Path}' have no properties to check; add them with PropertyMap.AddCollection(..., items => ...)");
            return;
        }

        var inline = owner.CreateInline<TItem>();
        RuleCompiler.AddRule(inline, itemRule, Items, context, location, scope.Groups);
        owner.ForEach(Getter, inline, rule => scope.Apply(rule), Path);
    }

    protected override CheckFamily<IEnumerable<TItem?>> Family(BuildContext context) =>
        new CollectionFamily<IEnumerable<TItem?>, TItem>(null, declaredType);
}

// The options of a rule that the outer ForEach of a "[]" rule takes: groups and conditions.
internal sealed class RuleScope<T>(string[]? groups, Func<T, bool>? when, Func<T, bool>? unless)
{
    public string[]? Groups { get; } = groups;

    public void Apply<TItem>(EachRuleOptions<T, TItem> rule)
    {
        if (Groups is not null)
        {
            rule.InGroup(Groups);
        }

        if (when is not null)
        {
            rule.When(when);
        }

        if (unless is not null)
        {
            rule.Unless(unless);
        }
    }
}
