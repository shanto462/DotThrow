using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DotThrow.Internal;

namespace DotThrow;

// String checks. A null string fails every check here with the "Null" code. Call
// AllowNull() first to let null pass. Messages never include the string itself, so
// passwords and tokens do not leak into logs. Substrings and patterns that a check is
// given (IfContains("@"), IfMatches(pattern)) do appear in the default message, because
// they are part of the rule; pass a custom message if one of them is a secret.
public static partial class GuardExtensions
{
    /// <summary>Fails when the string is <see langword="null"/> or empty.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfNullOrEmpty(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.Length == 0))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Empty, message);
        }

        return guard;
    }

    /// <summary>Fails when the string is <see langword="null"/>, empty, or only white-space characters.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfNullOrWhiteSpace(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || string.IsNullOrWhiteSpace(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.WhiteSpace, message);
        }

        return guard;
    }

    /// <summary>Fails when the string has more than <paramref name="maxLength"/> characters.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="maxLength">The largest allowed length.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholders: <c>{MaxLength}</c>, <c>{TotalLength}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfLongerThan(this Guard<string> guard, int maxLength, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.Length > maxLength))
        {
            // A negative maximum fails every string, so it is caught here.
            Argument.NotNegative(maxLength);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.LongerThan, message, Placeholder.MaxLength, maxLength, Placeholder.TotalLength, text?.Length ?? 0);
        }

        return guard;
    }

    /// <summary>Fails when the string has fewer than <paramref name="minLength"/> characters.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="minLength">The smallest allowed length.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholders: <c>{MinLength}</c>, <c>{TotalLength}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfShorterThan(this Guard<string> guard, int minLength, string? message = null)
    {
        Argument.NotNegative(minLength);
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.Length < minLength))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ShorterThan, message, Placeholder.MinLength, minLength, Placeholder.TotalLength, text?.Length ?? 0);
        }

        return guard;
    }

    /// <summary>Fails when the string does not have exactly <paramref name="length"/> characters.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="length">The required length.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholders: <c>{Length}</c> (also as <c>{MinLength}</c> and <c>{MaxLength}</c>), <c>{TotalLength}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfLengthNotEqualTo(this Guard<string> guard, int length, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.Length != length))
        {
            // A negative length fails every string, so it is caught here.
            Argument.NotNegative(length);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.LengthNotEqualTo, message, Placeholder.Length, length, Placeholder.MinLength, length, Placeholder.MaxLength, length, Placeholder.TotalLength, text?.Length ?? 0);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string length is outside the range from <paramref name="minLength"/>
    /// to <paramref name="maxLength"/>. Both ends are allowed.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="minLength">The smallest allowed length.</param>
    /// <param name="maxLength">The largest allowed length.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholders: <c>{MinLength}</c>, <c>{MaxLength}</c>, <c>{TotalLength}</c>.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfLengthOutOfRange(this Guard<string> guard, int minLength, int maxLength, string? message = null)
    {
        Argument.NotNegative(minLength);
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.Length < minLength || text.Length > maxLength))
        {
            // A maximum below the minimum fails every string, so it is caught here.
            Argument.Range(minLength, maxLength);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.LengthOutOfRange, message, Placeholder.MinLength, minLength, Placeholder.MaxLength, maxLength, Placeholder.TotalLength, text?.Length ?? 0);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string equals <paramref name="other"/> under <paramref name="comparison"/>.
    /// The default message does not show <paramref name="other"/>, which may be a secret;
    /// a custom message can use <c>{ComparisonValue}</c>.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The rejected string.</param>
    /// <param name="comparison">How to compare, for example <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfEqualTo(this Guard<string> guard, string? other, StringComparison comparison, string? message = null)
    {
        if (guard.IsActive && string.Equals(guard.Value, other, comparison))
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.EqualTo, message, Placeholder.ComparisonValue, other);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string does not equal <paramref name="other"/> under <paramref name="comparison"/>.
    /// The default message does not show <paramref name="other"/>, which may be a secret;
    /// a custom message can use <c>{ComparisonValue}</c>.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The required string.</param>
    /// <param name="comparison">How to compare, for example <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{ComparisonValue}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotEqualTo(this Guard<string> guard, string? other, StringComparison comparison, string? message = null)
    {
        if (guard.IsActive && !string.Equals(guard.Value, other, comparison))
        {
            return Rejection.Fail(guard, FailureKind.Argument, ErrorCodes.NotEqualTo, message, Placeholder.ComparisonValue, other);
        }

        return guard;
    }

    /// <summary>Fails when the string contains <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The rejected substring.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Value}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContains(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.Contains(text, value, comparison)))
        {
            Argument.NotNull(value);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Contains, message, Placeholder.Value, value, TemplateKeys.ContainsValue);
        }

        return guard;
    }

    /// <summary>Fails when the string does not contain <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The required substring.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Value}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotContains(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Formats.Contains(text, value, comparison)))
        {
            Argument.NotNull(value);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotContains, message, Placeholder.Value, value, TemplateKeys.NotContainsValue);
        }

        return guard;
    }

    /// <summary>Fails when the string starts with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The rejected prefix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Value}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfStartsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.StartsWith(value, comparison)))
        {
            Argument.NotNull(value);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.StartsWith, message, Placeholder.Value, value);
        }

        return guard;
    }

    /// <summary>Fails when the string does not start with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The required prefix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Value}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotStartsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !text.StartsWith(value, comparison)))
        {
            Argument.NotNull(value);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotStartsWith, message, Placeholder.Value, value);
        }

        return guard;
    }

    /// <summary>Fails when the string ends with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The rejected suffix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Value}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfEndsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.EndsWith(value, comparison)))
        {
            Argument.NotNull(value);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.EndsWith, message, Placeholder.Value, value);
        }

        return guard;
    }

    /// <summary>Fails when the string does not end with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The required suffix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Value}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotEndsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !text.EndsWith(value, comparison)))
        {
            Argument.NotNull(value);
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotEndsWith, message, Placeholder.Value, value);
        }

        return guard;
    }

    /// <summary>Fails when the string matches the regular expression <paramref name="pattern"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="pattern">The rejected pattern.</param>
    /// <param name="options">The regular expression options.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Pattern}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A match that runs out of time (the <c>matchTimeout</c> overloads, a <see cref="Regex"/>
    /// created with a timeout, or the process-wide <c>REGEX_DEFAULT_MATCH_TIMEOUT</c>) cannot
    /// confirm anything about the value, so the check fails with its normal error code and
    /// message (fail closed); <see cref="RegexMatchTimeoutException"/> never escapes. Patterns
    /// that run on untrusted input should have a timeout: some patterns take exponential time
    /// on crafted input (ReDoS).
    /// </remarks>
    public static Guard<string> IfMatches(this Guard<string> guard, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.None, string? message = null)
    {
        Argument.NotNull(pattern);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.Match(text, pattern, options, null) != Formats.RegexOutcome.NoMatch))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Matches, message, Placeholder.Pattern, pattern);
        }

        return guard;
    }

    /// <summary>Fails when the string matches <paramref name="regex"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="regex">The rejected pattern, for example one made by the <c>[GeneratedRegex]</c> source generator.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Pattern}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A match that runs out of time (the <c>matchTimeout</c> overloads, a <see cref="Regex"/>
    /// created with a timeout, or the process-wide <c>REGEX_DEFAULT_MATCH_TIMEOUT</c>) cannot
    /// confirm anything about the value, so the check fails with its normal error code and
    /// message (fail closed); <see cref="RegexMatchTimeoutException"/> never escapes. Patterns
    /// that run on untrusted input should have a timeout: some patterns take exponential time
    /// on crafted input (ReDoS).
    /// </remarks>
    public static Guard<string> IfMatches(this Guard<string> guard, Regex regex, string? message = null)
    {
        Argument.NotNull(regex);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.Match(text, regex) != Formats.RegexOutcome.NoMatch))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Matches, message, Placeholder.Pattern, regex.ToString());
        }

        return guard;
    }

    /// <summary>Fails when the string matches the regular expression <paramref name="pattern"/> (or the match times out). The match stops after <paramref name="matchTimeout"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="pattern">The regular expression.</param>
    /// <param name="matchTimeout">The longest time one match may take, for example <c>TimeSpan.FromMilliseconds(100)</c>.</param>
    /// <param name="options">The regular expression options.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Pattern}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A match that runs out of time (the <c>matchTimeout</c> overloads, a <see cref="Regex"/>
    /// created with a timeout, or the process-wide <c>REGEX_DEFAULT_MATCH_TIMEOUT</c>) cannot
    /// confirm anything about the value, so the check fails with its normal error code and
    /// message (fail closed); <see cref="RegexMatchTimeoutException"/> never escapes. Patterns
    /// that run on untrusted input should have a timeout: some patterns take exponential time
    /// on crafted input (ReDoS).
    /// </remarks>
    public static Guard<string> IfMatches(
        this Guard<string> guard,
        [StringSyntax(StringSyntaxAttribute.Regex)] string pattern,
        TimeSpan matchTimeout,
        RegexOptions options = RegexOptions.None,
        string? message = null)
    {
        Argument.NotNull(pattern);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.Match(text, pattern, options, matchTimeout) != Formats.RegexOutcome.NoMatch))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.Matches, message, Placeholder.Pattern, pattern);
        }

        return guard;
    }

    /// <summary>Fails when the string does not match the regular expression <paramref name="pattern"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="pattern">The required pattern.</param>
    /// <param name="options">The regular expression options.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Pattern}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A match that runs out of time (the <c>matchTimeout</c> overloads, a <see cref="Regex"/>
    /// created with a timeout, or the process-wide <c>REGEX_DEFAULT_MATCH_TIMEOUT</c>) cannot
    /// confirm anything about the value, so the check fails with its normal error code and
    /// message (fail closed); <see cref="RegexMatchTimeoutException"/> never escapes. Patterns
    /// that run on untrusted input should have a timeout: some patterns take exponential time
    /// on crafted input (ReDoS).
    /// </remarks>
    public static Guard<string> IfNotMatches(this Guard<string> guard, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.None, string? message = null)
    {
        Argument.NotNull(pattern);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.Match(text, pattern, options, null) != Formats.RegexOutcome.Match))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotMatches, message, Placeholder.Pattern, pattern);
        }

        return guard;
    }

    /// <summary>Fails when the string does not match <paramref name="regex"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="regex">The required pattern, for example one made by the <c>[GeneratedRegex]</c> source generator.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Pattern}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A match that runs out of time (the <c>matchTimeout</c> overloads, a <see cref="Regex"/>
    /// created with a timeout, or the process-wide <c>REGEX_DEFAULT_MATCH_TIMEOUT</c>) cannot
    /// confirm anything about the value, so the check fails with its normal error code and
    /// message (fail closed); <see cref="RegexMatchTimeoutException"/> never escapes. Patterns
    /// that run on untrusted input should have a timeout: some patterns take exponential time
    /// on crafted input (ReDoS).
    /// </remarks>
    public static Guard<string> IfNotMatches(this Guard<string> guard, Regex regex, string? message = null)
    {
        Argument.NotNull(regex);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.Match(text, regex) != Formats.RegexOutcome.Match))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotMatches, message, Placeholder.Pattern, regex.ToString());
        }

        return guard;
    }

    /// <summary>Fails when the string does not match the regular expression <paramref name="pattern"/> (or the match times out). The match stops after <paramref name="matchTimeout"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="pattern">The regular expression.</param>
    /// <param name="matchTimeout">The longest time one match may take, for example <c>TimeSpan.FromMilliseconds(100)</c>.</param>
    /// <param name="options">The regular expression options.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Pattern}</c>.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// A match that runs out of time (the <c>matchTimeout</c> overloads, a <see cref="Regex"/>
    /// created with a timeout, or the process-wide <c>REGEX_DEFAULT_MATCH_TIMEOUT</c>) cannot
    /// confirm anything about the value, so the check fails with its normal error code and
    /// message (fail closed); <see cref="RegexMatchTimeoutException"/> never escapes. Patterns
    /// that run on untrusted input should have a timeout: some patterns take exponential time
    /// on crafted input (ReDoS).
    /// </remarks>
    public static Guard<string> IfNotMatches(
        this Guard<string> guard,
        [StringSyntax(StringSyntaxAttribute.Regex)] string pattern,
        TimeSpan matchTimeout,
        RegexOptions options = RegexOptions.None,
        string? message = null)
    {
        Argument.NotNull(pattern);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.Match(text, pattern, options, matchTimeout) != Formats.RegexOutcome.Match))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotMatches, message, Placeholder.Pattern, pattern);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a plausible email address: exactly one <c>@</c> with text
    /// on both sides; no white-space or control characters anywhere; and on each side of the
    /// <c>@</c>, no dot at the start or end and no two dots in a row. This is a format check
    /// only. Send a confirmation email to prove that an address exists.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotEmailAddress(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Formats.IsEmailAddress(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotEmailAddress, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not valid JSON (RFC 8259). Any JSON value passes, including
    /// <c>42</c>, <c>"text"</c> and <c>null</c>. The check does not allocate.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotJson(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Json.IsValid(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotJson, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a well-formed XML document. DTDs are rejected and external
    /// entities are never resolved, so entity attacks (XXE, billion laughs) do not work. This
    /// overload has no size limit, and parsing allocates several times the size of the text:
    /// for untrusted input use <see cref="IfNotXml(Guard{string}, long, string?)"/> with a limit.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotXml(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Formats.IsXml(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotXml, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a well-formed XML document or is longer than
    /// <paramref name="maxCharacters"/>. DTDs are rejected and external entities are never
    /// resolved. Use this overload for untrusted input: parsing allocates several times the
    /// size of the text, so an unbounded document is a memory risk.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="maxCharacters">The largest allowed document, in characters; 0 for no limit.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCharacters"/> is negative.</exception>
    public static Guard<string> IfNotXml(this Guard<string> guard, long maxCharacters, string? message = null)
    {
        if (maxCharacters < 0)
        {
            Argument.ThrowOutOfRange(nameof(maxCharacters), maxCharacters, "The value must not be negative.");
        }

        var text = guard.Value;
        if (guard.IsActive && (text is null || !Formats.IsXml(text, maxCharacters)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotXml, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a URI of the given kind. A bare file path such as
    /// <c>/etc/hosts</c> or <c>C:\temp</c> is not treated as an absolute URI. Write
    /// <c>file:///etc/hosts</c> for a file URI.
    /// </summary>
    /// <remarks>
    /// The same rules hold on every operating system: an empty or white-space string fails
    /// (even though RFC 3986 allows an empty relative reference), a backslash fails anywhere
    /// in the string (RFC 3986 does not allow it), and a Windows drive path such as
    /// <c>C:\temp</c> or <c>C:/temp</c> fails for every kind.
    /// </remarks>
    /// <param name="guard">The guard.</param>
    /// <param name="kind">The required kind. The default is <see cref="UriKind.Absolute"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotUri(this Guard<string> guard, UriKind kind = UriKind.Absolute, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Formats.IsUri(text, kind, out _)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotUri, message, NotUriKey(kind));
        }

        return guard;
    }

    /// <summary>Fails when the string is not an absolute <c>http</c> or <c>https</c> URL.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotHttpUrl(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Formats.IsHttpUrl(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotHttpUrl, message);
        }

        return guard;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Fails when the string cannot be parsed into <typeparamref name="TResult"/>, for example
    /// <c>int</c>, <c>decimal</c>, <c>Guid</c> or <c>DateOnly</c>. Available on .NET 8 and later.
    /// </summary>
    /// <typeparam name="TResult">The type to parse into.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="provider">The format provider. The default is <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Type}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotParsable<TResult>(this Guard<string> guard, IFormatProvider? provider = null, string? message = null)
        where TResult : IParsable<TResult>
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !TResult.TryParse(text, provider ?? System.Globalization.CultureInfo.InvariantCulture, out _)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotParsable, message, Placeholder.Type, typeof(TResult).Name);
        }

        return guard;
    }
#endif

    /// <summary>
    /// Fails when the string has at least one letter and every letter is uppercase.
    /// Digits and symbols are ignored.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfAllUpperCase(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.AllLettersAre(text, upper: true)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.AllUpperCase, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string has at least one letter and every letter is lowercase.
    /// Digits and symbols are ignored.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfAllLowerCase(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || Formats.AllLettersAre(text, upper: false)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.AllLowerCase, message);
        }

        return guard;
    }

    private static string? NotUriKey(UriKind kind) => kind switch
    {
        UriKind.Absolute => TemplateKeys.NotUriAbsolute,
        UriKind.Relative => TemplateKeys.NotUriRelative,
        _ => null,
    };
}
