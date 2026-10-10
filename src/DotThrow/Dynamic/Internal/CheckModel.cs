using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DotThrow.Dynamic.Internal;

// One built check: it runs a typed guard method on the guard of the rule.
internal delegate Guard<TValue> GuardStep<TValue>(Guard<TValue> guard);

internal sealed class CompiledCheck<TValue>(string name, GuardStep<TValue> step, string? errorCode)
{
    public string Name { get; } = name;

    public GuardStep<TValue> Step { get; } = step;

    public string? ErrorCode { get; } = errorCode;
}

internal enum Arg
{
    Value,
    Min,
    Max,
    Values,
    Comparison,
    Options,
}

// Reads and converts the arguments of one check, adding an error for every problem, and
// reports arguments that were given but not used by the check.
internal sealed class CheckArgs
{
    private readonly CheckDefinition _definition;
    private readonly BuildContext _context;
    private readonly string _location;
    private readonly bool[] _used = new bool[6];
    private bool _failed;

    public CheckArgs(CheckDefinition definition, BuildContext context, string location, string checkName, string propertyName, Type valueType)
    {
        _definition = definition;
        _context = context;
        _location = location;
        CheckName = checkName;
        PropertyName = propertyName;
        ValueType = valueType;
        Arguments = new CheckArguments(definition);
    }

    public string CheckName { get; }

    public string PropertyName { get; }

    public Type ValueType { get; }

    public string? Message => _definition.Message;

    public CheckArguments Arguments { get; }

    public BuildContext Context => _context;

    public bool Failed => _failed;

    public void Error(string problem)
    {
        _failed = true;
        _context.Error(_location, $"check '{CheckName}' on '{PropertyName}': {problem}");
    }

    // The text of a required argument.
    public string? Text(Arg arg)
    {
        _used[(int)arg] = true;
        var text = Raw(arg);
        if (text is null)
        {
            Error($"needs the '{arg}' argument");
        }

        return text;
    }

    public string? OptionalText(Arg arg)
    {
        _used[(int)arg] = true;
        return Raw(arg);
    }

    public bool TryParse<T>(Arg arg, Func<string, T>? parser, out T value)
    {
        value = default!;
        var text = Text(arg);
        return text is not null && TryConvert(arg.ToString(), text, parser, out value);
    }

    public bool TryParseList<T>(Func<string, T>? parser, out T[] values)
    {
        _used[(int)Arg.Values] = true;
        values = [];
        var list = _definition.Values;
        if (list is null || list.Count == 0)
        {
            Error("needs the 'Values' argument (a list)");
            return false;
        }

        var result = new T[list.Count];
        var ok = true;
        for (var i = 0; i < list.Count; i++)
        {
            ok &= TryConvert("Values[" + i.ToString(CultureInfo.InvariantCulture) + "]", list[i], parser, out result[i]);
        }

        values = result;
        return ok;
    }

    public bool TryInt(Arg arg, out int value) => TryParse(arg, static text => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture), out value);

    public StringComparison? Comparison(StringComparison fallback)
    {
        var text = OptionalText(Arg.Comparison);
        if (text is null)
        {
            return fallback;
        }

        if (EnumText.TryParse<StringComparison>(text, out var comparison))
        {
            return comparison;
        }

        Error($"'{text}' is not a StringComparison (use Ordinal, OrdinalIgnoreCase, InvariantCulture, InvariantCultureIgnoreCase, CurrentCulture or CurrentCultureIgnoreCase)");
        return null;
    }

    public bool HasComparison => _definition.Comparison is not null;

    private static readonly RegexOptions AllRegexOptions = AllOptions();

    private static RegexOptions AllOptions()
    {
        var all = System.Text.RegularExpressions.RegexOptions.None;
#if NET5_0_OR_GREATER
        foreach (var option in Enum.GetValues<RegexOptions>())
#else
        foreach (RegexOptions option in Enum.GetValues(typeof(RegexOptions)))
#endif
        {
            all |= option;
        }

        return all;
    }

    public RegexOptions? RegexOptions()
    {
        var text = OptionalText(Arg.Options);
        if (text is null)
        {
            return System.Text.RegularExpressions.RegexOptions.None;
        }

        // Names only ("IgnoreCase, Multiline"); a number could carry bits Regex rejects.
        var names = text.Trim();
        if (names.Length > 0 && !char.IsDigit(names[0]) && names[0] is not ('-' or '+')
            && Enum.TryParse<RegexOptions>(names, ignoreCase: true, out var options)
            && (options & ~AllRegexOptions) == 0)
        {
            return options;
        }

        Error($"'{text}' is not a list of RegexOptions");
        return null;
    }

    // Options that are a single flag word, for example "IgnoreCase".
    public bool? Flag(string word)
    {
        var text = OptionalText(Arg.Options);
        if (text is null)
        {
            return false;
        }

        if (string.Equals(text.Trim(), word, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Error($"the only option is '{word}'");
        return null;
    }

    // Errors for arguments the check does not read.
    public void ReportUnused()
    {
        for (var i = 0; i < _used.Length; i++)
        {
            if (!_used[i] && Given((Arg)i))
            {
                Error($"does not use the '{(Arg)i}' argument");
            }
        }
    }

    private bool TryConvert<T>(string name, string text, Func<string, T>? parser, out T value)
    {
        value = default!;
        if (parser is null)
        {
            Error($"'{name}' cannot be converted to {TypeText.Of(typeof(T))}");
            return false;
        }

        try
        {
            value = parser(text);
            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types: any parse error becomes a definition error.
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
#pragma warning restore CA1031
        {
            Error($"'{name}' value '{text}' is not a valid {TypeText.Of(typeof(T))}{TextFormatException.DetailOf(exception)}");
            return false;
        }
    }

    private string? Raw(Arg arg) => arg switch
    {
        Arg.Value => _definition.Value,
        Arg.Min => _definition.Min,
        Arg.Max => _definition.Max,
        Arg.Comparison => _definition.Comparison,
        Arg.Options => _definition.Options,
        _ => null,
    };

    private bool Given(Arg arg) => arg == Arg.Values ? _definition.Values is { Count: > 0 } : Raw(arg) is not null;
}

// What a build needs besides the definition: the errors found so far, the known types and the
// custom checks.
internal sealed class BuildContext(CheckRegistry registry, TypeCatalog types)
{
    private readonly List<string> _errors = [];

    public CheckRegistry Registry { get; } = registry;

    public TypeCatalog Types { get; } = types;

    public IReadOnlyList<string> Errors => _errors;

    public void Error(string location, string problem) => _errors.Add(location.Length == 0 ? problem : location + ": " + problem);
}

// A family of checks for values of one type (all strings, all ints...). TryCreate returns
// false when the name is not a check of this family, and true with a null step when it is
// but its arguments have errors.
internal abstract class CheckFamily<TValue>
{
    public abstract bool TryCreate(string name, CheckArgs args, out GuardStep<TValue>? step);
}

// Enum option names in definitions: one defined name, ignoring case. Numbers are not accepted.
internal static class EnumText
{
    public static bool TryParse<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || char.IsDigit(trimmed![0]) || trimmed[0] is '-' or '+' || trimmed.Split(',').Length > 1)
        {
            return false;
        }

#if NET5_0_OR_GREATER
        return Enum.TryParse(trimmed, ignoreCase: true, out value) && Enum.IsDefined(value);
#else
        return Enum.TryParse(trimmed, ignoreCase: true, out value) && Enum.IsDefined(typeof(TEnum), value);
#endif
    }
}
