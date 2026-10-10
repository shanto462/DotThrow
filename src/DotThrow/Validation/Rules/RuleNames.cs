using System;

namespace DotThrow.Internal;

// Names of rules: the property path from the selector text, after the global hooks.
internal static class RuleNames
{
    public static string Path(Type type, string? selectorExpression)
    {
        var path = PropertyPaths.FromSelector(SelectorNames.FromExpression(selectorExpression, identityName: string.Empty));
        return path.Length > 0 && ValidatorDefaults.PropertyNameResolver is { } resolve ? resolve(type, path) ?? path : path;
    }

    public static string DisplayName(Type type, string path) =>
        ValidatorDefaults.DisplayNameResolver is { } resolve ? resolve(type, path) ?? path : path;
}
