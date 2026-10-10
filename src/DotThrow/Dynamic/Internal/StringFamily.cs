using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DotThrow.Dynamic.Internal;

// Every string check. Comparisons come from ComparableFamily<string> (ordinal).
internal sealed class StringFamily() : ComparableFamily<string>(static text => text)
{
    protected override bool TryCreateMore(string name, CheckArgs args, out GuardStep<string>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.EqualTo when args.HasComparison:
                if (args.Text(Arg.Value) is { } equal && args.Comparison(StringComparison.Ordinal) is { } equalComparison)
                {
                    step = g => g.IfEqualTo(equal, equalComparison, message);
                }

                return true;
            case CheckNames.NotEqualTo when args.HasComparison:
                if (args.Text(Arg.Value) is { } notEqual && args.Comparison(StringComparison.Ordinal) is { } notEqualComparison)
                {
                    step = g => g.IfNotEqualTo(notEqual, notEqualComparison, message);
                }

                return true;
            case CheckNames.OneOf when args.HasComparison:
                if (args.TryParseList(Parser, out var rejected) && args.Comparison(StringComparison.Ordinal) is { } rejectedComparison)
                {
                    step = g => g.IfOneOf((IEnumerable<string>)rejected, Comparer(rejectedComparison), message);
                }

                return true;
            case CheckNames.NotOneOf when args.HasComparison:
                if (args.TryParseList(Parser, out var allowed) && args.Comparison(StringComparison.Ordinal) is { } allowedComparison)
                {
                    step = g => g.IfNotOneOf((IEnumerable<string>)allowed, Comparer(allowedComparison), message);
                }

                return true;
            case CheckNames.NullOrEmpty:
                step = g => g.IfNullOrEmpty(message);
                return true;
            case CheckNames.WhiteSpace:
            case CheckNames.NullOrWhiteSpace:
                step = g => g.IfNullOrWhiteSpace(message);
                return true;
            case CheckNames.LongerThan:
                if (Length(args, Arg.Value, out var maxLength))
                {
                    step = g => g.IfLongerThan(maxLength, message);
                }

                return true;
            case CheckNames.ShorterThan:
                if (Length(args, Arg.Value, out var minLength))
                {
                    step = g => g.IfShorterThan(minLength, message);
                }

                return true;
            case CheckNames.LengthNotEqualTo:
                if (Length(args, Arg.Value, out var length))
                {
                    step = g => g.IfLengthNotEqualTo(length, message);
                }

                return true;
            case CheckNames.LengthOutOfRange:
                var hasMin = Length(args, Arg.Min, out var rangeMin);
                var hasMax = Length(args, Arg.Max, out var rangeMax);
                if (hasMin && hasMax && rangeMin > rangeMax)
                {
                    args.Error("'Min' is greater than 'Max'");
                }
                else if (hasMin && hasMax)
                {
                    step = g => g.IfLengthOutOfRange(rangeMin, rangeMax, message);
                }

                return true;
            case CheckNames.Contains:
                return Text(args, (g, text, comparison) => g.IfContains(text, comparison, message), out step);
            case CheckNames.NotContains:
                return Text(args, (g, text, comparison) => g.IfNotContains(text, comparison, message), out step);
            case CheckNames.StartsWith:
                return Text(args, (g, text, comparison) => g.IfStartsWith(text, comparison, message), out step);
            case CheckNames.NotStartsWith:
                return Text(args, (g, text, comparison) => g.IfNotStartsWith(text, comparison, message), out step);
            case CheckNames.EndsWith:
                return Text(args, (g, text, comparison) => g.IfEndsWith(text, comparison, message), out step);
            case CheckNames.NotEndsWith:
                return Text(args, (g, text, comparison) => g.IfNotEndsWith(text, comparison, message), out step);
            case CheckNames.Matches:
                if (Pattern(args) is { } matches)
                {
                    step = g => g.IfMatches(matches, message);
                }

                return true;
            case CheckNames.NotMatches:
                if (Pattern(args) is { } notMatches)
                {
                    step = g => g.IfNotMatches(notMatches, message);
                }

                return true;
            case CheckNames.NotUri:
                if (UriKindOption(args) is { } kind)
                {
                    step = g => g.IfNotUri(kind, message);
                }

                return true;
            case CheckNames.ContainsAny:
                if (args.Text(Arg.Value) is { } any)
                {
                    var characters = any.ToCharArray();
                    step = g => g.IfContainsAny(characters, message);
                }

                return true;
            case CheckNames.ContainsAnyExcept:
                if (args.Text(Arg.Value) is { } except)
                {
                    var characters = except.ToCharArray();
                    step = g => g.IfContainsAnyExcept(characters, message);
                }

                return true;
            case CheckNames.NotEnumName:
                if (args.Flag("IgnoreCase") is { } ignoreCase)
                {
                    step = args.Context.Types.NotEnumName(args, ignoreCase, message);
                }

                return true;
            case CheckNames.NotParsable:
#if NET8_0_OR_GREATER
                step = Parsable.Create(args, message);
#else
                args.Error("needs .NET 8 or later (IParsable)");
#endif
                return true;
            default:
                step = Simple(name, message);
                return step is not null;
        }
    }

    // String checks without arguments.
    private static GuardStep<string>? Simple(string name, string? message) => name switch
    {
        CheckNames.NotEmailAddress => g => g.IfNotEmailAddress(message),
        CheckNames.NotJson => g => g.IfNotJson(message),
        CheckNames.NotXml => g => g.IfNotXml(message),
        CheckNames.NotHttpUrl => g => g.IfNotHttpUrl(message),
        CheckNames.AllUpperCase => g => g.IfAllUpperCase(message),
        CheckNames.AllLowerCase => g => g.IfAllLowerCase(message),
        CheckNames.NotTrimmed => g => g.IfNotTrimmed(message),
        CheckNames.ContainsWhiteSpace => g => g.IfContainsWhiteSpace(message),
        CheckNames.ContainsControlCharacters => g => g.IfContainsControlCharacters(message),
        CheckNames.NotAscii => g => g.IfNotAscii(message),
        CheckNames.NotDigits => g => g.IfNotDigits(message),
        CheckNames.NotLettersOrDigits => g => g.IfNotLettersOrDigits(message),
        CheckNames.NotHex => g => g.IfNotHex(message),
        CheckNames.NotBase64 => g => g.IfNotBase64(message),
        CheckNames.NotGuid => g => g.IfNotGuid(message),
        CheckNames.NotIPAddress => g => g.IfNotIPAddress(message),
        CheckNames.NotSlug => g => g.IfNotSlug(message),
        CheckNames.NotVersion => g => g.IfNotVersion(message),
        CheckNames.NotCreditCardNumber => g => g.IfNotCreditCardNumber(message),
        CheckNames.NotFileName => g => g.IfNotFileName(message),
        _ => null,
    };

    private delegate Guard<string> TextCheck(Guard<string> guard, string text, StringComparison comparison);

    private static bool Text(CheckArgs args, TextCheck check, out GuardStep<string>? step)
    {
        step = null;
        if (args.Text(Arg.Value) is { } text && args.Comparison(StringComparison.Ordinal) is { } comparison)
        {
            step = g => check(g, text, comparison);
        }

        return true;
    }

    private static bool Length(CheckArgs args, Arg arg, out int length)
    {
        if (!args.TryInt(arg, out length))
        {
            return false;
        }

        if (length < 0)
        {
            args.Error($"'{arg}' must not be negative");
            return false;
        }

        return true;
    }

    // Patterns come from data, so a slow match stops after this time (RegexMatchTimeoutException).
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    // The pattern is compiled once, when the rule is built; a bad pattern is a definition error.
    private static Regex? Pattern(CheckArgs args)
    {
        var pattern = args.Text(Arg.Value);
        var options = args.RegexOptions();
        if (pattern is null || options is null)
        {
            return null;
        }

        try
        {
            return new Regex(pattern, options.Value, PatternTimeout);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            args.Error($"'{pattern}' is not a valid regular expression ({exception.Message})");
            return null;
        }
    }

    private static UriKind? UriKindOption(CheckArgs args)
    {
        var text = args.OptionalText(Arg.Options);
        if (text is null)
        {
            return UriKind.Absolute;
        }

        if (EnumText.TryParse<UriKind>(text, out var kind))
        {
            return kind;
        }

        args.Error($"'{text}' is not a UriKind (Absolute, Relative or RelativeOrAbsolute)");
        return null;
    }

    // Read at every run, so the culture comparers use the culture of the run, like code.
    private static StringComparer Comparer(StringComparison comparison) => comparison switch
    {
        StringComparison.CurrentCulture => StringComparer.CurrentCulture,
        StringComparison.CurrentCultureIgnoreCase => StringComparer.CurrentCultureIgnoreCase,
        StringComparison.InvariantCulture => StringComparer.InvariantCulture,
        StringComparison.InvariantCultureIgnoreCase => StringComparer.InvariantCultureIgnoreCase,
        StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase,
        _ => StringComparer.Ordinal,
    };
}
