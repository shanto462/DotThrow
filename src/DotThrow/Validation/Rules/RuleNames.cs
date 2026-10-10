using System;
using System.Collections.Concurrent;

namespace DotThrow.Internal;

// Names of rules: the property path from the selector text, after the global hooks.
internal static class RuleNames
{
    // Selector texts come from CallerArgumentExpression, so a program has a fixed set of them.
    // The parsed path is kept per text, so report.Check(form, f => f.Name) parses once. The
    // cache stops growing at MaxCached entries, in case texts are made at run time.
    private const int MaxCached = 2048;

    private static readonly ConcurrentDictionary<string, string> Parsed = new(StringComparer.Ordinal);

    public static string Path(Type type, string? selectorExpression)
    {
        var path = PropertyPaths.FromSelector(Selector(selectorExpression));
        return path.Length > 0 && ValidatorDefaults.PropertyNameResolver is { } resolve ? resolve(type, path) ?? path : path;
    }

    public static string DisplayName(Type type, string path) =>
        ValidatorDefaults.DisplayNameResolver is { } resolve ? resolve(type, path) ?? path : path;

    private static string Selector(string? expression)
    {
        if (expression is null)
        {
            return SelectorNames.FromExpression(null, identityName: string.Empty);
        }

        if (Parsed.TryGetValue(expression, out var path))
        {
            return path;
        }

        path = SelectorNames.FromExpression(expression, identityName: string.Empty);
        if (Parsed.Count < MaxCached)
        {
            Parsed.TryAdd(expression, path);
        }

        return path;
    }
}
