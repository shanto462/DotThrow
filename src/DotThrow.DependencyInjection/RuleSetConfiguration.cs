using System;
using System.Collections.Generic;
using System.Globalization;
using DotThrow.Dynamic;
using Microsoft.Extensions.Configuration;

namespace DotThrow.DependencyInjection;

/// <summary>
/// Reads a <see cref="RuleSetDefinition"/> from an <see cref="IConfiguration"/> section, for example
/// a part of <c>appsettings.json</c>. It accepts the same shape as <see cref="RuleSetDefinition.FromJson(string)"/>
/// (keys ignore case, a check can be a plain name) and uses no reflection, so it works with trimming
/// and Native AOT without the configuration binder.
/// </summary>
/// <remarks>
/// Configuration has only strings: numbers and booleans are read as their text, and a JSON
/// <see langword="null"/> or an empty string reads as a value that is not set. An empty list
/// item stays an empty string.
/// </remarks>
/// <example>
/// <code>
/// // appsettings.json
/// // "Validation": { "SignUp": { "rules": [
/// //     { "property": "Email", "checks": [ "WhiteSpace", "NotEmailAddress" ] },
/// //     { "property": "Age", "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] } ] } }
/// RuleSetDefinition definition = RuleSetConfiguration.Read(configuration.GetSection("Validation:SignUp"));
/// </code>
/// </example>
public static class RuleSetConfiguration
{
    /// <summary>Reads a rule definition from a configuration section.</summary>
    /// <param name="section">The section that holds <c>rules</c>, <c>ruleCascade</c> and <c>classCascade</c>.</param>
    /// <returns>The definition. Check names and arguments are checked later, when a validator is built.</returns>
    /// <exception cref="RuleDefinitionException">
    /// The section is missing, or has unknown keys or values of the wrong kind. Every error is listed
    /// with its place, for example <c>Rules[0].Checks[1].Chek (Validation:SignUp:rules:0:checks:1:chek): unknown key (did you mean 'check'?).</c>
    /// </exception>
    public static RuleSetDefinition Read(IConfiguration section)
    {
        section.Throw().IfNull();
        var reader = new Reader();
        var definition = reader.ReadRuleSet(section);
        if (reader.Errors.Count > 0)
        {
            throw new RuleDefinitionException(reader.Errors);
        }

        return definition;
    }

    // Mirrors the JSON reader of DotThrow.Dynamic: the same keys, the same error texts.
    private sealed class Reader
    {
        private static readonly string[] RuleSetKeys = ["rules", "ruleCascade", "classCascade"];

        private static readonly string[] RuleKeys =
            ["property", "each", "allowNull", "group", "groups", "when", "unless", "severity", "errorCode", "message", "name", "cascade", "checks"];

        private static readonly string[] CheckKeys = ["check", "value", "min", "max", "values", "comparison", "options", "message", "errorCode"];

        private static readonly string[] ConditionKeys = ["property", "equalTo", "notEqualTo", "isNull"];

        public List<string> Errors { get; } = [];

        public RuleSetDefinition ReadRuleSet(IConfiguration node)
        {
            var definition = new RuleSetDefinition();
            var children = Children(node);
            if (children.Count == 0)
            {
                Fail(string.Empty, node, Value(node) is { Length: > 0 } ? "expected an object" : "the section is missing or empty");
                return definition;
            }

            foreach (var (key, value, path) in Members(children, string.Empty, RuleSetKeys))
            {
                switch (key)
                {
                    case "rules":
                        foreach (var (item, itemPath) in Items(value, path))
                        {
                            definition.Rules.Add(ReadRule(item, itemPath));
                        }

                        break;
                    case "ruleCascade":
                        definition.RuleCascade = Text(value, path);
                        break;
                    default:
                        definition.ClassCascade = Text(value, path);
                        break;
                }
            }

            return definition;
        }

        private RuleDefinition ReadRule(IConfigurationSection node, string location)
        {
            var rule = new RuleDefinition();
            foreach (var (key, value, path) in Members(ObjectChildren(node, location), location, RuleKeys))
            {
                switch (key)
                {
                    case "property":
                        rule.Property = Text(value, path);
                        break;
                    case "each":
                        rule.Each = Flag(value, path) ?? false;
                        break;
                    case "allowNull":
                        rule.AllowNull = Flag(value, path) ?? false;
                        break;
                    case "group":
                        rule.Group = Text(value, path);
                        break;
                    case "groups":
                        rule.Groups = TextList(value, path);
                        break;
                    case "when":
                        rule.When = ReadCondition(value, path);
                        break;
                    case "unless":
                        rule.Unless = ReadCondition(value, path);
                        break;
                    case "severity":
                        rule.Severity = Text(value, path);
                        break;
                    case "errorCode":
                        rule.ErrorCode = Text(value, path);
                        break;
                    case "message":
                        rule.Message = Text(value, path);
                        break;
                    case "name":
                        rule.Name = Text(value, path);
                        break;
                    case "cascade":
                        rule.Cascade = Text(value, path);
                        break;
                    default:
                        foreach (var (item, itemPath) in Items(value, path))
                        {
                            rule.Checks.Add(ReadCheck(item, itemPath));
                        }

                        break;
                }
            }

            return rule;
        }

        private CheckDefinition ReadCheck(IConfigurationSection node, string location)
        {
            var check = new CheckDefinition();
            var children = Children(node);

            // A plain string is a check without arguments: "checks": ["NotEmailAddress"].
            if (children.Count == 0 && node.Value is { Length: > 0 } name)
            {
                check.Check = name;
                return check;
            }

            foreach (var (key, value, path) in Members(ObjectChildren(node, location), location, CheckKeys))
            {
                switch (key)
                {
                    case "check":
                        check.Check = Text(value, path);
                        break;
                    case "value":
                        check.Value = Text(value, path);
                        break;
                    case "min":
                        check.Min = Text(value, path);
                        break;
                    case "max":
                        check.Max = Text(value, path);
                        break;
                    case "values":
                        check.Values = TextList(value, path);
                        break;
                    case "comparison":
                        check.Comparison = Text(value, path);
                        break;
                    case "options":
                        check.Options = Text(value, path);
                        break;
                    case "message":
                        check.Message = Text(value, path);
                        break;
                    default:
                        check.ErrorCode = Text(value, path);
                        break;
                }
            }

            return check;
        }

        private ConditionDefinition? ReadCondition(IConfigurationSection node, string location)
        {
            var children = Children(node);
            if (children.Count == 0 && string.IsNullOrEmpty(node.Value))
            {
                // "when": null
                return null;
            }

            var condition = new ConditionDefinition();
            foreach (var (key, value, path) in Members(ObjectChildren(node, location), location, ConditionKeys))
            {
                switch (key)
                {
                    case "property":
                        condition.Property = Text(value, path);
                        break;
                    case "equalTo":
                        condition.EqualTo = Text(value, path);
                        break;
                    case "notEqualTo":
                        condition.NotEqualTo = Text(value, path);
                        break;
                    default:
                        condition.IsNull = Flag(value, path);
                        break;
                }
            }

            return condition;
        }

        // The children of a section that must be an object; an error for a value.
        private List<IConfigurationSection> ObjectChildren(IConfigurationSection node, string location)
        {
            var children = Children(node);
            if (children.Count == 0)
            {
                Fail(location, node, "expected an object");
            }

            return children;
        }

        private IEnumerable<Member> Members(List<IConfigurationSection> children, string location, string[] known)
        {
            foreach (var child in children)
            {
                var key = Find(known, child.Key);
                if (key is null)
                {
                    var hint = Closest(child.Key, known);
                    Fail(Join(location, child.Key), child, hint is null ? "unknown key" : $"unknown key (did you mean '{hint}'?)");
                    continue;
                }

                yield return new Member(key, child, Join(location, key));
            }
        }

        // The items of an array: children with the keys 0, 1, 2... A missing, empty or null value is an empty array.
        private IEnumerable<Item> Items(IConfigurationSection node, string location)
        {
            var children = Children(node);
            if (children.Count == 0)
            {
                if (!string.IsNullOrEmpty(node.Value))
                {
                    Fail(location, node, "expected an array");
                }

                yield break;
            }

            var items = new List<(int Index, IConfigurationSection Section)>(children.Count);
            foreach (var child in children)
            {
                if (!int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    Fail(location, node, "expected an array");
                    yield break;
                }

                items.Add((index, child));
            }

            items.Sort(static (a, b) => a.Index.CompareTo(b.Index));
            foreach (var (index, section) in items)
            {
                yield return new Item(section, location + "[" + index.ToString(CultureInfo.InvariantCulture) + "]");
            }
        }

        // Configuration stores a JSON null as an empty string, so an empty value reads as not set.
        private string? Text(IConfigurationSection node, string location)
        {
            if (Children(node).Count > 0)
            {
                Fail(location, node, "expected a string or a number");
                return null;
            }

            return string.IsNullOrEmpty(node.Value) ? null : node.Value;
        }

        private bool? Flag(IConfigurationSection node, string location)
        {
            if (Children(node).Count == 0)
            {
                if (string.IsNullOrEmpty(node.Value))
                {
                    return null;
                }

                if (bool.TryParse(node.Value, out var flag))
                {
                    return flag;
                }
            }

            Fail(location, node, "expected true or false");
            return null;
        }

        private List<string>? TextList(IConfigurationSection node, string location)
        {
            if (Children(node).Count == 0 && string.IsNullOrEmpty(node.Value))
            {
                return null;
            }

            // List items keep an empty string: "values": ["", "none"] may allow an empty value.
            var list = new List<string>();
            foreach (var (item, path) in Items(node, location))
            {
                if (Children(item).Count > 0)
                {
                    Fail(path, item, "expected a string or a number");
                }
                else
                {
                    list.Add(item.Value ?? string.Empty);
                }
            }

            return list;
        }

        private void Fail(string location, IConfiguration node, string problem)
        {
            var place = node is IConfigurationSection section ? section.Path : "configuration root";
            Errors.Add($"{(location.Length == 0 ? "(root)" : location)} ({place}): {problem}.");
        }

        private static List<IConfigurationSection> Children(IConfiguration node) => [.. node.GetChildren()];

        private static string? Value(IConfiguration node) => (node as IConfigurationSection)?.Value;

        private static string? Find(string[] known, string key)
        {
            foreach (var name in known)
            {
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            return null;
        }

        // The known key closest to a typo, by edit distance (at most 2, or a third of the length).
        private static string? Closest(string key, string[] known)
        {
            string? best = null;
            var bestDistance = Math.Max(2, key.Length / 3) + 1;
            foreach (var name in known)
            {
                var distance = Distance(key.ToUpperInvariant(), name.ToUpperInvariant());
                if (distance < bestDistance)
                {
                    best = name;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }

        // Paths use the C# property names, like the errors found when a validator is built.
        private static string Join(string location, string key)
        {
            var name = key.Length == 0 ? key : char.ToUpperInvariant(key[0]) + key.Substring(1);
            return location.Length == 0 ? name : location + "." + name;
        }

        private readonly struct Member(string key, IConfigurationSection value, string path)
        {
            public void Deconstruct(out string memberKey, out IConfigurationSection memberValue, out string memberPath)
            {
                memberKey = key;
                memberValue = value;
                memberPath = path;
            }
        }

        private readonly struct Item(IConfigurationSection value, string path)
        {
            public void Deconstruct(out IConfigurationSection itemValue, out string itemPath)
            {
                itemValue = value;
                itemPath = path;
            }
        }
    }
}
