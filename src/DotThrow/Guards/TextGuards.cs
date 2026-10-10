using System;
using System.Runtime.CompilerServices;
using DotThrow.Internal;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace DotThrow;

// Character and format checks for strings. A null string fails with the "Null" code.
// "Not<Format>" checks fail an empty string (it is not a number, a GUID, ...); "Contains"
// checks pass it (it contains nothing). On .NET 8 and later the character checks are
// vectorized (SearchValues, IndexOfAnyExcept, Ascii); every framework gives the same answer.
public static partial class GuardExtensions
{
    /// <summary>Fails when the string starts or ends with a white-space character. An empty string passes.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guard<string> IfNotTrimmed(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsTrimmed(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotTrimmed, message);
        }

        return guard;
    }

    /// <summary>Fails when the string contains a white-space character (<see cref="char.IsWhiteSpace(char)"/>), including Unicode spaces.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsWhiteSpace(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || Text.ContainsWhiteSpace(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsWhiteSpace, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string contains a control character (<see cref="char.IsControl(char)"/>:
    /// U+0000 to U+001F and U+007F to U+009F), such as a null character, a tab or a line break.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsControlCharacters(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || Text.ContainsControlCharacters(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsControlCharacters, message);
        }

        return guard;
    }

    /// <summary>Fails when the string contains any of <paramref name="characters"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The rejected characters.</param>
    /// <returns>The guard.</returns>
    /// <example><c>fileName.Throw().IfContainsAny('/', '\\');</c></example>
    public static Guard<string> IfContainsAny(this Guard<string> guard, params char[] characters) =>
        guard.IfContainsAny(characters, message: null);

    /// <summary>Fails when the string contains any of <paramref name="characters"/>.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The rejected characters.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Characters}</c> (the characters as one string).</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAny(this Guard<string> guard, char[] characters, string? message)
    {
        Argument.NotNull(characters);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Text.ContainsAny(text, characters)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsAny, message, Placeholder.Characters, new string(characters));
        }

        return guard;
    }

    /// <summary>Fails when the string contains a character that is not one of <paramref name="characters"/>. An empty string passes.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The allowed characters.</param>
    /// <returns>The guard.</returns>
    /// <example><c>code.Throw().IfContainsAnyExcept('A', 'B', 'C');</c></example>
    public static Guard<string> IfContainsAnyExcept(this Guard<string> guard, params char[] characters) =>
        guard.IfContainsAnyExcept(characters, message: null);

    /// <summary>Fails when the string contains a character that is not one of <paramref name="characters"/>. An empty string passes.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The allowed characters.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Characters}</c> (the characters as one string).</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAnyExcept(this Guard<string> guard, char[] characters, string? message)
    {
        Argument.NotNull(characters);
        var text = guard.Value;
        if (guard.IsActive && (text is null || Text.ContainsAnyExcept(text, characters)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsAnyExcept, message, Placeholder.Characters, new string(characters));
        }

        return guard;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Fails when the string contains any of <paramref name="characters"/>. With C# 13 or later
    /// the compiler picks this overload for a list of characters and passes them on the stack,
    /// so the check does not allocate. Available on .NET 8 and later.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The rejected characters.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAny(this Guard<string> guard, params ReadOnlySpan<char> characters) =>
        guard.IfContainsAny(characters, message: null);

    /// <summary>Fails when the string contains any of <paramref name="characters"/>. Available on .NET 8 and later.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The rejected characters, for example <c>"&lt;&gt;&amp;"</c> or a collection expression.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Characters}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAny(this Guard<string> guard, scoped ReadOnlySpan<char> characters, string? message)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.AsSpan().ContainsAny(characters)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsAny, message, Placeholder.Characters, characters.ToString());
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string contains any of the characters in <paramref name="characters"/>.
    /// Create the <see cref="SearchValues{T}"/> once (in a static field) and reuse it: this is
    /// the fastest form for a large set. Available on .NET 8 and later.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The rejected characters.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAny(this Guard<string> guard, SearchValues<char> characters, string? message = null)
    {
        Argument.NotNull(characters);
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.AsSpan().ContainsAny(characters)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsAny, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string contains a character that is not one of <paramref name="characters"/>.
    /// With C# 13 or later the compiler picks this overload for a list of characters and passes
    /// them on the stack, so the check does not allocate. Available on .NET 8 and later.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The allowed characters.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAnyExcept(this Guard<string> guard, params ReadOnlySpan<char> characters) =>
        guard.IfContainsAnyExcept(characters, message: null);

    /// <summary>Fails when the string contains a character that is not one of <paramref name="characters"/>. Available on .NET 8 and later.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The allowed characters.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Characters}</c>.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAnyExcept(this Guard<string> guard, scoped ReadOnlySpan<char> characters, string? message)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.AsSpan().ContainsAnyExcept(characters)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsAnyExcept, message, Placeholder.Characters, characters.ToString());
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string contains a character that is not in <paramref name="characters"/>.
    /// Create the <see cref="SearchValues{T}"/> once and reuse it. Available on .NET 8 and later.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="characters">The allowed characters.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfContainsAnyExcept(this Guard<string> guard, SearchValues<char> characters, string? message = null)
    {
        Argument.NotNull(characters);
        var text = guard.Value;
        if (guard.IsActive && (text is null || text.AsSpan().ContainsAnyExcept(characters)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.ContainsAnyExcept, message);
        }

        return guard;
    }
#endif

    /// <summary>Fails when the string contains a character outside the ASCII range (above U+007F). An empty string passes.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotAscii(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsAscii(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotAscii, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is empty or contains a character other than the ASCII digits
    /// <c>0</c> to <c>9</c>. Signs, spaces and other scripts' digits fail.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotDigits(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsDigits(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotDigits, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is empty or contains a character that is not a letter or a digit
    /// (<see cref="char.IsLetterOrDigit(char)"/>, any script). Combine with
    /// <see cref="IfNotAscii"/> to allow only <c>A</c> to <c>Z</c>, <c>a</c> to <c>z</c> and <c>0</c> to <c>9</c>.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotLettersOrDigits(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsLettersOrDigits(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotLettersOrDigits, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is empty or contains a character other than the hexadecimal digits
    /// <c>0-9</c>, <c>a-f</c> and <c>A-F</c>. A <c>0x</c> prefix fails.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotHex(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsHex(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotHex, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not standard Base64 (RFC 4648, with <c>+</c>, <c>/</c> and
    /// <c>=</c> padding), read the way <see cref="Convert.FromBase64String(string)"/> reads it:
    /// spaces, tabs and line breaks are ignored. An empty string fails. The URL-safe alphabet
    /// (<c>-</c> and <c>_</c>) fails. The check does not decode or allocate.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotBase64(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsBase64(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotBase64, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a GUID in one of the formats that <see cref="Guid.TryParse(string, out Guid)"/>
    /// reads (<c>N</c>, <c>D</c>, <c>B</c>, <c>P</c> or <c>X</c>).
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotGuid(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsGuid(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotGuid, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not an IPv4 or IPv6 address. IPv4 must be four decimal numbers
    /// from 0 to 255 without leading zeros (<c>192.168.0.1</c>); the short and octal forms that
    /// <see cref="System.Net.IPAddress.TryParse(string, out System.Net.IPAddress)"/> accepts, such as <c>1</c> or
    /// <c>010.0.0.1</c>, fail. IPv6 follows RFC 4291 (eight groups of up to four hex digits, one
    /// <c>::</c>, an optional IPv4 tail such as <c>::ffff:1.2.3.4</c>) and may have a zone
    /// (<c>fe80::1%eth0</c>), but no brackets or port. The check does not allocate and gives the
    /// same answer on every framework.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotIPAddress(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsIPAddress(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotIPAddress, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a URL slug: lowercase ASCII letters and digits in groups
    /// joined by single hyphens, such as <c>hello-world-2</c>. No hyphen at the start or end.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotSlug(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsSlug(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotSlug, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a <see cref="Version"/>: two to four numbers joined by dots,
    /// such as <c>1.2</c> or <c>1.2.3.4</c>, each from 0 to <see cref="int.MaxValue"/>. Only digits
    /// and dots are allowed (no spaces or signs), so this is stricter than
    /// <see cref="Version.TryParse(string, out Version)"/>. Semantic versions with a label such as
    /// <c>1.2.3-beta</c> fail.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotVersion(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsVersion(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotVersion, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a credit card number, like FluentValidation's
    /// <c>CreditCard</c>: spaces and hyphens are ignored, every other character must be a
    /// digit, and the digits must pass the Luhn checksum. Unlike FluentValidation, only the
    /// ASCII digits count and a string without digits fails. The length is not checked.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    public static Guard<string> IfNotCreditCardNumber(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsCreditCardNumber(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotCreditCardNumber, message);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not the name of a member of <typeparamref name="TEnum"/>, like
    /// FluentValidation's <c>IsEnumName</c>. Numbers (<c>"1"</c>) and flag combinations
    /// (<c>"Read, Write"</c>) fail. Call it with the enum type: <c>IfNotEnumName&lt;Color&gt;()</c>.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="guard">The guard.</param>
    /// <param name="ignoreCase"><see langword="true"/> to compare names without case (ordinal).</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one. Placeholder: <c>{Type}</c> (the enum name).</param>
    /// <returns>The guard.</returns>
    /// <remarks>The names are read once per enum type and kept in a set (a frozen set on .NET 8 and later).</remarks>
    public static Guard<string> IfNotEnumName<TEnum>(this Guard<string> guard, bool ignoreCase = false, string? message = null)
        where TEnum : struct, Enum
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Enums.IsName<TEnum>(text, ignoreCase)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotEnumName, message, Placeholder.Type, typeof(TEnum).Name);
        }

        return guard;
    }

    /// <summary>
    /// Fails when the string is not a valid file name (not a path) on the current operating
    /// system: it is empty, it is <c>.</c> or <c>..</c>, or it contains a character from
    /// <see cref="System.IO.Path.GetInvalidFileNameChars"/>.
    /// </summary>
    /// <param name="guard">The guard.</param>
    /// <param name="message">A custom message, or <see langword="null"/> for the default one.</param>
    /// <returns>The guard.</returns>
    /// <remarks>
    /// The result depends on the operating system: on Windows the characters
    /// <c>\ / : * ? " &lt; &gt; |</c> and control characters are invalid; on Linux and macOS only
    /// <c>/</c> and the null character are. Reserved Windows names such as <c>CON</c> are not checked.
    /// For names that must work everywhere, add <c>IfContainsAny('\\', '/', ':', '*', '?', '"', '&lt;', '&gt;', '|')</c>.
    /// </remarks>
    public static Guard<string> IfNotFileName(this Guard<string> guard, string? message = null)
    {
        var text = guard.Value;
        if (guard.IsActive && (text is null || !Text.IsFileName(text)))
        {
            return Rejection.Content(guard, FailureKind.Argument, ErrorCodes.NotFileName, message);
        }

        return guard;
    }
}
