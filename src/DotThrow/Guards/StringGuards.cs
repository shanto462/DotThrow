using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DotThrow.Internal;

namespace DotThrow;

// String checks. A null string fails every check here with the "Null" code. Call
// AllowNull() first to let null pass. Messages never include the string itself, so
// passwords and tokens do not leak into logs.
public static partial class GuardExtensions
{
    /// <summary>Fails when the string is <see langword="null"/> or empty.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfNullOrEmpty(this Guard<string> guard, string? message = null)
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (guard.Value.Length == 0)
        {
            return guard.Fail(ErrorCodes.Empty, message ?? Messages.Empty(guard.Name));
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
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (string.IsNullOrWhiteSpace(guard.Value))
        {
            return guard.Fail(ErrorCodes.WhiteSpace, message ?? Messages.WhiteSpace(guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the string has more than <paramref name="maxLength"/> characters.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="maxLength">The largest allowed length.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfLongerThan(this Guard<string> guard, int maxLength, string? message = null)
    {
        Argument.NotNegative(maxLength);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (guard.Value.Length > maxLength)
        {
            return guard.Fail(ErrorCodes.LongerThan, message ?? Messages.LongerThan(guard.Name, maxLength));
        }

        return guard;
    }

    /// <summary>Fails when the string has fewer than <paramref name="minLength"/> characters.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="minLength">The smallest allowed length.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfShorterThan(this Guard<string> guard, int minLength, string? message = null)
    {
        Argument.NotNegative(minLength);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (guard.Value.Length < minLength)
        {
            return guard.Fail(ErrorCodes.ShorterThan, message ?? Messages.ShorterThan(guard.Name, minLength));
        }

        return guard;
    }

    /// <summary>Fails when the string does not have exactly <paramref name="length"/> characters.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="length">The required length.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfLengthNotEqualTo(this Guard<string> guard, int length, string? message = null)
    {
        Argument.NotNegative(length);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (guard.Value.Length != length)
        {
            return guard.Fail(ErrorCodes.LengthNotEqualTo, message ?? Messages.LengthNotEqualTo(guard.Name, length));
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
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfLengthOutOfRange(this Guard<string> guard, int minLength, int maxLength, string? message = null)
    {
        Argument.NotNegative(minLength);
        Argument.Range(minLength, maxLength);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (guard.Value.Length < minLength || guard.Value.Length > maxLength)
        {
            return guard.Fail(ErrorCodes.LengthOutOfRange, message ?? Messages.LengthOutOfRange(guard.Name, minLength, maxLength));
        }

        return guard;
    }

    /// <summary>Fails when the string equals <paramref name="other"/> under <paramref name="comparison"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The rejected string.</param>
    /// <param name="comparison">How to compare, for example <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfEqualTo(this Guard<string> guard, string? other, StringComparison comparison, string? message = null)
    {
        if (guard.IsActive && string.Equals(guard.Value, other, comparison))
        {
            return guard.Fail(ErrorCodes.EqualTo, message ?? Messages.EqualTo(guard.Name, other));
        }

        return guard;
    }

    /// <summary>Fails when the string does not equal <paramref name="other"/> under <paramref name="comparison"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="other">The required string.</param>
    /// <param name="comparison">How to compare, for example <see cref="StringComparison.OrdinalIgnoreCase"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotEqualTo(this Guard<string> guard, string? other, StringComparison comparison, string? message = null)
    {
        if (guard.IsActive && !string.Equals(guard.Value, other, comparison))
        {
            return guard.Fail(ErrorCodes.NotEqualTo, message ?? Messages.NotEqualTo(guard.Name, other));
        }

        return guard;
    }

    /// <summary>Fails when the string contains <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The rejected substring.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContains(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        Argument.NotNull(value);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Formats.Contains(guard.Value, value, comparison))
        {
            return guard.Fail(ErrorCodes.Contains, message ?? Messages.Contains(guard.Name, value));
        }

        return guard;
    }

    /// <summary>Fails when the string does not contain <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The required substring.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotContains(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        Argument.NotNull(value);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Formats.Contains(guard.Value, value, comparison))
        {
            return guard.Fail(ErrorCodes.NotContains, message ?? Messages.NotContains(guard.Name, value));
        }

        return guard;
    }

    /// <summary>Fails when the string starts with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The rejected prefix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfStartsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        Argument.NotNull(value);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (guard.Value.StartsWith(value, comparison))
        {
            return guard.Fail(ErrorCodes.StartsWith, message ?? Messages.StartsWith(guard.Name, value));
        }

        return guard;
    }

    /// <summary>Fails when the string does not start with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The required prefix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotStartsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        Argument.NotNull(value);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!guard.Value.StartsWith(value, comparison))
        {
            return guard.Fail(ErrorCodes.NotStartsWith, message ?? Messages.NotStartsWith(guard.Name, value));
        }

        return guard;
    }

    /// <summary>Fails when the string ends with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The rejected suffix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfEndsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        Argument.NotNull(value);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (guard.Value.EndsWith(value, comparison))
        {
            return guard.Fail(ErrorCodes.EndsWith, message ?? Messages.EndsWith(guard.Name, value));
        }

        return guard;
    }

    /// <summary>Fails when the string does not end with <paramref name="value"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="value">The required suffix.</param>
    /// <param name="comparison">How to compare. The default is <see cref="StringComparison.Ordinal"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotEndsWith(this Guard<string> guard, string value, StringComparison comparison = StringComparison.Ordinal, string? message = null)
    {
        Argument.NotNull(value);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!guard.Value.EndsWith(value, comparison))
        {
            return guard.Fail(ErrorCodes.NotEndsWith, message ?? Messages.NotEndsWith(guard.Name, value));
        }

        return guard;
    }

    /// <summary>Fails when the string matches the regular expression <paramref name="pattern"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="pattern">The rejected pattern.</param>
    /// <param name="options">The regular expression options.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfMatches(this Guard<string> guard, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.None, string? message = null)
    {
        Argument.NotNull(pattern);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Regex.IsMatch(guard.Value, pattern, options))
        {
            return guard.Fail(ErrorCodes.Matches, message ?? Messages.Matches(guard.Name, pattern));
        }

        return guard;
    }

    /// <summary>Fails when the string matches <paramref name="regex"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="regex">The rejected pattern, for example one made by the <c>[GeneratedRegex]</c> source generator.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfMatches(this Guard<string> guard, Regex regex, string? message = null)
    {
        Argument.NotNull(regex);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (regex.IsMatch(guard.Value))
        {
            return guard.Fail(ErrorCodes.Matches, message ?? Messages.Matches(guard.Name, regex.ToString()));
        }

        return guard;
    }

    /// <summary>Fails when the string does not match the regular expression <paramref name="pattern"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="pattern">The required pattern.</param>
    /// <param name="options">The regular expression options.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotMatches(this Guard<string> guard, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.None, string? message = null)
    {
        Argument.NotNull(pattern);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Regex.IsMatch(guard.Value, pattern, options))
        {
            return guard.Fail(ErrorCodes.NotMatches, message ?? Messages.NotMatches(guard.Name, pattern));
        }

        return guard;
    }

    /// <summary>Fails when the string does not match <paramref name="regex"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="regex">The required pattern, for example one made by the <c>[GeneratedRegex]</c> source generator.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotMatches(this Guard<string> guard, Regex regex, string? message = null)
    {
        Argument.NotNull(regex);
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!regex.IsMatch(guard.Value))
        {
            return guard.Fail(ErrorCodes.NotMatches, message ?? Messages.NotMatches(guard.Name, regex.ToString()));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a plausible email address: one <c>@</c> with text on both
    /// sides, no white space, and no dot at the start or end of either side. This is a format
    /// check only. Send a confirmation email to prove that an address exists.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotEmailAddress(this Guard<string> guard, string? message = null)
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Formats.IsEmailAddress(guard.Value))
        {
            return guard.Fail(ErrorCodes.NotEmailAddress, message ?? Messages.NotEmailAddress(guard.Name));
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
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Json.IsValid(guard.Value))
        {
            return guard.Fail(ErrorCodes.NotJson, message ?? Messages.NotJson(guard.Name));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a well-formed XML document. DTDs are rejected and external
    /// entities are never resolved, so the check is safe for untrusted input.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotXml(this Guard<string> guard, string? message = null)
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Formats.IsXml(guard.Value))
        {
            return guard.Fail(ErrorCodes.NotXml, message ?? Messages.NotXml(guard.Name));
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a URI of the given kind. A bare file path such as
    /// <c>/etc/hosts</c> or <c>C:\temp</c> is not treated as an absolute URI. Write
    /// <c>file:///etc/hosts</c> for a file URI.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="kind">The required kind. The default is <see cref="UriKind.Absolute"/>.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotUri(this Guard<string> guard, UriKind kind = UriKind.Absolute, string? message = null)
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Formats.IsUri(guard.Value, kind, out _))
        {
            return guard.Fail(ErrorCodes.NotUri, message ?? Messages.NotUri(guard.Name, kind));
        }

        return guard;
    }

    /// <summary>Fails when the string is not an absolute <c>http</c> or <c>https</c> URL.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotHttpUrl(this Guard<string> guard, string? message = null)
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!Formats.IsUri(guard.Value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return guard.Fail(ErrorCodes.NotHttpUrl, message ?? Messages.NotHttpUrl(guard.Name));
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
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotParsable<TResult>(this Guard<string> guard, IFormatProvider? provider = null, string? message = null)
        where TResult : IParsable<TResult>
    {
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (!TResult.TryParse(guard.Value, provider ?? System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return guard.Fail(ErrorCodes.NotParsable, message ?? Messages.NotParsable(guard.Name, typeof(TResult)));
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
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Formats.AllLettersAre(guard.Value, upper: true))
        {
            return guard.Fail(ErrorCodes.AllUpperCase, message ?? Messages.AllUpperCase(guard.Name));
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
        if (!guard.IsActive)
        {
            return guard;
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        if (Formats.AllLettersAre(guard.Value, upper: false))
        {
            return guard.Fail(ErrorCodes.AllLowerCase, message ?? Messages.AllLowerCase(guard.Name));
        }

        return guard;
    }
}
