using System;
using System.Collections.Generic;
using System.Globalization;

namespace DotThrow.Dynamic.Internal;

// Binds a JSON tree to the definition classes. Keys ignore case; unknown keys, duplicate keys
// and wrong value kinds are errors, all collected before throwing.
internal sealed class DefinitionReader
{
    private static readonly string[] RuleSetKeys = ["rules", "ruleCascade", "classCascade"];

    private static readonly string[] RuleKeys =
        ["property", "each", "allowNull", "group", "groups", "when", "unless", "severity", "errorCode", "message", "name", "cascade", "checks"];

    private static readonly string[] CheckKeys = ["check", "value", "min", "max", "values", "comparison", "options", "message", "errorCode"];

    private static readonly string[] ConditionKeys = ["property", "equalTo", "notEqualTo", "isNull"];

    private readonly string _json;
    private readonly List<string> _errors = [];

    private DefinitionReader(string json)
    {
        _json = json;
    }

    public static RuleSetDefinition Read(string json)
    {
        var reader = new DefinitionReader(json);
        var root = JsonTreeReader.Parse(json);
        var definition = reader.ReadRuleSet(root);
        if (reader._errors.Count > 0)
        {
            throw new RuleDefinitionException(reader._errors);
        }

        return definition;
    }

    private RuleSetDefinition ReadRuleSet(JsonNode node)
    {
        var definition = new RuleSetDefinition();
        foreach (var (key, value, path) in Members(node, string.Empty, RuleSetKeys))
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
                case "classCascade":
                    definition.ClassCascade = Text(value, path);
                    break;
            }
        }

        return definition;
    }

    private RuleDefinition ReadRule(JsonNode node, string location)
    {
        var rule = new RuleDefinition();
        foreach (var (key, value, path) in Members(node, location, RuleKeys))
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
                    rule.When = value.Kind == JsonKind.Null ? null : ReadCondition(value, path);
                    break;
                case "unless":
                    rule.Unless = value.Kind == JsonKind.Null ? null : ReadCondition(value, path);
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
                case "checks":
                    foreach (var (item, itemPath) in Items(value, path))
                    {
                        rule.Checks.Add(ReadCheck(item, itemPath));
                    }

                    break;
            }
        }

        return rule;
    }

    private CheckDefinition ReadCheck(JsonNode node, string location)
    {
        var check = new CheckDefinition();

        // A plain string is a check without arguments: "checks": ["NotEmailAddress"].
        if (node.Kind == JsonKind.String)
        {
            check.Check = node.Text;
            return check;
        }

        foreach (var (key, value, path) in Members(node, location, CheckKeys))
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
                case "errorCode":
                    check.ErrorCode = Text(value, path);
                    break;
            }
        }

        return check;
    }

    private ConditionDefinition ReadCondition(JsonNode node, string location)
    {
        var condition = new ConditionDefinition();
        foreach (var (key, value, path) in Members(node, location, ConditionKeys))
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
                case "isNull":
                    condition.IsNull = Flag(value, path);
                    break;
            }
        }

        return condition;
    }

    // The members of an object with their canonical key (from known), or nothing on errors.
    private IEnumerable<Member> Members(JsonNode node, string location, string[] known)
    {
        if (node.Kind != JsonKind.Object)
        {
            Fail(location, node, "expected an object");
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in node.Members!)
        {
            var path = Join(location, member.Key);
            if (!seen.Add(member.Key))
            {
                Fail(path, member.Value, "the key appears more than once");
                continue;
            }

            var key = Find(known, member.Key);
            if (key is null)
            {
                var hint = Suggestions.Closest(member.Key, known);
                Fail(path, member.Value, hint is null ? "unknown key" : $"unknown key (did you mean '{hint}'?)");
                continue;
            }

            yield return new Member(key, member.Value, Join(location, key));
        }
    }

    private IEnumerable<Item> Items(JsonNode node, string location)
    {
        if (node.Kind == JsonKind.Null)
        {
            yield break;
        }

        if (node.Kind != JsonKind.Array)
        {
            Fail(location, node, "expected an array");
            yield break;
        }

        for (var i = 0; i < node.Items!.Count; i++)
        {
            yield return new Item(node.Items[i], location + "[" + i.ToString(CultureInfo.InvariantCulture) + "]");
        }
    }

    // Strings, numbers (as written) and booleans are text; null is null.
    private string? Text(JsonNode node, string location)
    {
        switch (node.Kind)
        {
            case JsonKind.String:
            case JsonKind.Number:
            case JsonKind.True:
            case JsonKind.False:
                return node.Text;
            case JsonKind.Null:
                return null;
            default:
                Fail(location, node, "expected a string or a number");
                return null;
        }
    }

    private bool? Flag(JsonNode node, string location)
    {
        switch (node.Kind)
        {
            case JsonKind.True:
                return true;
            case JsonKind.False:
                return false;
            case JsonKind.Null:
                return null;
            case JsonKind.String when bool.TryParse(node.Text, out var parsed):
                return parsed;
            default:
                Fail(location, node, "expected true or false");
                return null;
        }
    }

    private List<string>? TextList(JsonNode node, string location)
    {
        if (node.Kind == JsonKind.Null)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var (item, path) in Items(node, location))
        {
            if (Text(item, path) is { } text)
            {
                list.Add(text);
            }
            else if (item.Kind == JsonKind.Null)
            {
                Fail(path, item, "expected a string or a number");
            }
        }

        return list;
    }

    private void Fail(string location, JsonNode node, string problem) =>
        _errors.Add($"{(location.Length == 0 ? "(root)" : location)} ({JsonTreeReader.Place(_json, node.Position)}): {problem}.");

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

    // Paths use the C# property names, like the errors found when a validator is built.
    private static string Join(string location, string key)
    {
        var name = key.Length == 0 ? key : char.ToUpperInvariant(key[0]) + key.Substring(1);
        return location.Length == 0 ? name : location + "." + name;
    }

    private readonly struct Member(string key, JsonNode value, string path)
    {
        public void Deconstruct(out string memberKey, out JsonNode memberValue, out string memberPath)
        {
            memberKey = key;
            memberValue = value;
            memberPath = path;
        }
    }

    private readonly struct Item(JsonNode value, string path)
    {
        public void Deconstruct(out JsonNode itemValue, out string itemPath)
        {
            itemValue = value;
            itemPath = path;
        }
    }
}
