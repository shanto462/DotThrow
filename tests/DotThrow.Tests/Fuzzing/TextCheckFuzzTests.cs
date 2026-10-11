using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using Xunit;
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System.Buffers;
using System.Numerics;
#endif

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Fuzz tests for the string checks: every check keeps the contract (see <see cref="Contract"/>)
/// on any text, and the format checks agree with a reference: the BCL parser where the
/// documentation promises the same result, otherwise a simple restatement of the documented rule.
/// </summary>
[Trait(Fuzz.Trait, Fuzz.Category)]
public sealed class TextCheckFuzzTests
{
    // Long enough that the patterns of the table never time out.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    /// <summary>Every string check, by name, with its error code.</summary>
    private static readonly Dictionary<string, Row> Rows = new()
    {
        ["NullOrEmpty"] = new(g => g.IfNullOrEmpty(), ErrorCodes.Empty),
        ["NullOrWhiteSpace"] = new(g => g.IfNullOrWhiteSpace(), ErrorCodes.WhiteSpace),
        ["LongerThan"] = new(g => g.IfLongerThan(10), ErrorCodes.LongerThan),
        ["ShorterThan"] = new(g => g.IfShorterThan(3), ErrorCodes.ShorterThan),
        ["LengthNotEqualTo"] = new(g => g.IfLengthNotEqualTo(5), ErrorCodes.LengthNotEqualTo),
        ["LengthOutOfRange"] = new(g => g.IfLengthOutOfRange(2, 8), ErrorCodes.LengthOutOfRange),
        ["EqualTo"] = new(g => g.IfEqualTo("abc", StringComparison.InvariantCultureIgnoreCase), ErrorCodes.EqualTo, ErrorCodes.EqualTo),
        ["NotEqualTo"] = new(g => g.IfNotEqualTo("\u00E9", StringComparison.CurrentCulture), ErrorCodes.NotEqualTo, ErrorCodes.NotEqualTo),
        ["Contains"] = new(g => g.IfContains("e\u0301", StringComparison.InvariantCulture), ErrorCodes.Contains),
        ["NotContains"] = new(g => g.IfNotContains("a", StringComparison.OrdinalIgnoreCase), ErrorCodes.NotContains),
        ["StartsWith"] = new(g => g.IfStartsWith("\u00AD", StringComparison.CurrentCulture), ErrorCodes.StartsWith),
        ["NotStartsWith"] = new(g => g.IfNotStartsWith("I", StringComparison.InvariantCultureIgnoreCase), ErrorCodes.NotStartsWith),
        ["EndsWith"] = new(g => g.IfEndsWith("\0", StringComparison.Ordinal), ErrorCodes.EndsWith),
        ["NotEndsWith"] = new(g => g.IfNotEndsWith("\u212A", StringComparison.OrdinalIgnoreCase), ErrorCodes.NotEndsWith),
        ["Matches"] = new(g => g.IfMatches("^[\\p{L}\\d]+$", Timeout), ErrorCodes.Matches),
        ["NotMatches"] = new(g => g.IfNotMatches("[\\p{L}]+", Timeout, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), ErrorCodes.NotMatches),
        ["NotEmailAddress"] = new(g => g.IfNotEmailAddress(), ErrorCodes.NotEmailAddress),
        ["NotJson"] = new(g => g.IfNotJson(), ErrorCodes.NotJson),
        ["NotXml"] = new(g => g.IfNotXml(), ErrorCodes.NotXml),
        ["NotXmlLimit"] = new(g => g.IfNotXml(64), ErrorCodes.NotXml),
        ["NotUri"] = new(g => g.IfNotUri(), ErrorCodes.NotUri),
        ["NotUriRelative"] = new(g => g.IfNotUri(UriKind.Relative), ErrorCodes.NotUri),
        ["NotUriAny"] = new(g => g.IfNotUri(UriKind.RelativeOrAbsolute), ErrorCodes.NotUri),
        ["NotHttpUrl"] = new(g => g.IfNotHttpUrl(), ErrorCodes.NotHttpUrl),
        ["AllUpperCase"] = new(g => g.IfAllUpperCase(), ErrorCodes.AllUpperCase),
        ["AllLowerCase"] = new(g => g.IfAllLowerCase(), ErrorCodes.AllLowerCase),
        ["NotTrimmed"] = new(g => g.IfNotTrimmed(), ErrorCodes.NotTrimmed),
        ["ContainsWhiteSpace"] = new(g => g.IfContainsWhiteSpace(), ErrorCodes.ContainsWhiteSpace),
        ["ContainsControlCharacters"] = new(g => g.IfContainsControlCharacters(), ErrorCodes.ContainsControlCharacters),
        ["ContainsAny"] = new(g => g.IfContainsAny(['<', '>', '\0', '\u00E9', '\uD800']), ErrorCodes.ContainsAny),
        ["ContainsAnyExcept"] = new(g => g.IfContainsAnyExcept(['a', 'b', '?', '\u4F8B']), ErrorCodes.ContainsAnyExcept),
        ["NotAscii"] = new(g => g.IfNotAscii(), ErrorCodes.NotAscii),
        ["NotDigits"] = new(g => g.IfNotDigits(), ErrorCodes.NotDigits),
        ["NotLettersOrDigits"] = new(g => g.IfNotLettersOrDigits(), ErrorCodes.NotLettersOrDigits),
        ["NotHex"] = new(g => g.IfNotHex(), ErrorCodes.NotHex),
        ["NotBase64"] = new(g => g.IfNotBase64(), ErrorCodes.NotBase64),
        ["NotGuid"] = new(g => g.IfNotGuid(), ErrorCodes.NotGuid),
        ["NotIPAddress"] = new(g => g.IfNotIPAddress(), ErrorCodes.NotIPAddress),
        ["NotSlug"] = new(g => g.IfNotSlug(), ErrorCodes.NotSlug),
        ["NotVersion"] = new(g => g.IfNotVersion(), ErrorCodes.NotVersion),
        ["NotCreditCardNumber"] = new(g => g.IfNotCreditCardNumber(), ErrorCodes.NotCreditCardNumber),
        ["NotEnumName"] = new(g => g.IfNotEnumName<DayOfWeek>(), ErrorCodes.NotEnumName),
        ["NotEnumNameIgnoreCase"] = new(g => g.IfNotEnumName<StringComparison>(ignoreCase: true), ErrorCodes.NotEnumName),
        ["NotFileName"] = new(g => g.IfNotFileName(), ErrorCodes.NotFileName),
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        ["ContainsAnySpan"] = new(g => g.IfContainsAny("<>&\"".AsSpan(), message: null), ErrorCodes.ContainsAny),
        ["ContainsAnyExceptSearchValues"] = new(g => g.IfContainsAnyExcept(SearchValues.Create("0123456789abcdef")), ErrorCodes.ContainsAnyExcept),
        ["NotParsableInt"] = new(g => g.IfNotParsable<int>(), ErrorCodes.NotParsable),
        ["NotParsableDouble"] = new(g => g.IfNotParsable<double>(CultureInfo.GetCultureInfo("de-DE")), ErrorCodes.NotParsable),
        ["NotParsableDecimal"] = new(g => g.IfNotParsable<decimal>(), ErrorCodes.NotParsable),
        ["NotParsableBigInteger"] = new(g => g.IfNotParsable<BigInteger>(), ErrorCodes.NotParsable),
        ["NotParsableHalf"] = new(g => g.IfNotParsable<Half>(), ErrorCodes.NotParsable),
        ["NotParsableDateTime"] = new(g => g.IfNotParsable<DateTime>(), ErrorCodes.NotParsable),
        ["NotParsableDateTimeOffset"] = new(g => g.IfNotParsable<DateTimeOffset>(CultureInfo.GetCultureInfo("ar-SA")), ErrorCodes.NotParsable),
        ["NotParsableDateOnly"] = new(g => g.IfNotParsable<DateOnly>(), ErrorCodes.NotParsable),
        ["NotParsableTimeSpan"] = new(g => g.IfNotParsable<TimeSpan>(), ErrorCodes.NotParsable),
        ["NotParsableGuid"] = new(g => g.IfNotParsable<Guid>(), ErrorCodes.NotParsable),
        ["NotParsableChar"] = new(g => g.IfNotParsable<char>(), ErrorCodes.NotParsable),
#endif
    };

    public static TheoryData<string> CheckNames => [.. Rows.Keys];

    /// <summary>Random text and text from every grammar, so each check sees both near-misses and noise.</summary>
    private static readonly Arbitrary<string?> MixedText = Arb.From(
        Gen.Frequency(
            (6, Generators.AnyTextGen),
            (1, Generators.AsNullable(Generators.EmailLike)),
            (1, Generators.AsNullable(Generators.UriLike)),
            (1, Generators.AsNullable(Generators.JsonLike)),
            (1, Generators.AsNullable(Generators.XmlLike)),
            (1, Generators.AsNullable(Generators.IPv6Text)),
            (1, Generators.AsNullable(Generators.Template))),
        Generators.Shrink);

    [Theory]
    [MemberData(nameof(CheckNames))]
    public void Every_check_keeps_the_contract_on_any_text(string check)
    {
        var row = Rows[check];
        Fuzz.Run(MixedText, text => Contract.Fails(text, row.Chain, row.Code, row.NullCode), share: 0.5);
    }

    [Fact]
    public void NotGuid_agrees_with_Guid_TryParse() =>
        Fuzz.Run(GuidText, text => Assert.Equal(!IsGuid(text), Contract.Fails(text, g => g.IfNotGuid(), ErrorCodes.NotGuid)));

#if NET
    // The documentation promises Convert.FromBase64String's reading (white space ignored), and an
    // empty text fails. Only on .NET: .NET Framework's Convert reads some inputs differently (D30).
    [Fact]
    public void NotBase64_agrees_with_Convert_FromBase64String() =>
        Fuzz.Run(Base64Text, text =>
        {
            var expected = text is not null && text.Any(c => c is not (' ' or '\t' or '\r' or '\n')) && DecodesAsBase64(text);
            Assert.True(expected != Contract.Fails(text, g => g.IfNotBase64(), ErrorCodes.NotBase64), $"Convert says {expected} for {Contract.Show(text)}.");
        });
#endif

    [Fact]
    public void Character_class_checks_agree_with_a_plain_loop() =>
        Fuzz.Run(Generators.AnyText, text =>
        {
            var units = text ?? string.Empty;
            Assert.Equal(text is null || (units.Length > 0 && (char.IsWhiteSpace(units[0]) || char.IsWhiteSpace(units[units.Length - 1]))), Contract.Fails(text, g => g.IfNotTrimmed(), ErrorCodes.NotTrimmed));
            Assert.Equal(text is null || units.Any(char.IsWhiteSpace), Contract.Fails(text, g => g.IfContainsWhiteSpace(), ErrorCodes.ContainsWhiteSpace));
            Assert.Equal(text is null || units.Any(char.IsControl), Contract.Fails(text, g => g.IfContainsControlCharacters(), ErrorCodes.ContainsControlCharacters));
            Assert.Equal(text is null || units.Any(c => c > '\x7F'), Contract.Fails(text, g => g.IfNotAscii(), ErrorCodes.NotAscii));
            Assert.Equal(text is null || units.Length == 0 || units.Any(c => c is < '0' or > '9'), Contract.Fails(text, g => g.IfNotDigits(), ErrorCodes.NotDigits));
            Assert.Equal(text is null || units.Length == 0 || !units.All(Uri.IsHexDigit), Contract.Fails(text, g => g.IfNotHex(), ErrorCodes.NotHex));
            Assert.Equal(text is null || !IsLettersOrDigits(units), Contract.Fails(text, g => g.IfNotLettersOrDigits(), ErrorCodes.NotLettersOrDigits));
            Assert.Equal(text is null || !Regex.IsMatch(units, "^[a-z0-9]+(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant), Contract.Fails(text, g => g.IfNotSlug(), ErrorCodes.NotSlug));
            Assert.Equal(text is null || (units.Any(char.IsUpper) && !units.Any(char.IsLower)), Contract.Fails(text, g => g.IfAllUpperCase(), ErrorCodes.AllUpperCase));
            Assert.Equal(text is null || (units.Any(char.IsLower) && !units.Any(char.IsUpper)), Contract.Fails(text, g => g.IfAllLowerCase(), ErrorCodes.AllLowerCase));
            Assert.Equal(text is null || units.Length == 0 || units is "." or ".." || units.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0, Contract.Fails(text, g => g.IfNotFileName(), ErrorCodes.NotFileName));
        });

    // IfContainsAny and IfContainsAnyExcept keep the ASCII part of the set in a bitmap; every
    // overload must give the answer of a plain search.
    [Fact]
    public void ContainsAny_overloads_agree_with_a_plain_search() =>
        Fuzz.Run(Arb.From(Gen.Zip(Generators.AnyTextGen, Generators.TextOf(Generators.AnyUnit, 12))), input =>
        {
            var (text, set) = input;
            var characters = set.ToCharArray();
            var containsAny = text is null || text.IndexOfAny(characters) >= 0;
            var containsOther = text is null || text.Any(c => Array.IndexOf(characters, c) < 0);
            Assert.Equal(containsAny, Contract.Fails(text, g => g.IfContainsAny(characters), ErrorCodes.ContainsAny));
            Assert.Equal(containsOther, Contract.Fails(text, g => g.IfContainsAnyExcept(characters), ErrorCodes.ContainsAnyExcept));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
            Assert.Equal(containsAny, Contract.Fails(text, g => g.IfContainsAny(set.AsSpan(), message: null), ErrorCodes.ContainsAny));
            Assert.Equal(containsOther, Contract.Fails(text, g => g.IfContainsAnyExcept(set.AsSpan(), message: null), ErrorCodes.ContainsAnyExcept));
            var values = SearchValues.Create(set);
            Assert.Equal(containsAny, Contract.Fails(text, g => g.IfContainsAny(values), ErrorCodes.ContainsAny));
            Assert.Equal(containsOther, Contract.Fails(text, g => g.IfContainsAnyExcept(values), ErrorCodes.ContainsAnyExcept));
#endif
        });

    // IfNotVersion is stricter than Version.TryParse (digits and dots only), and the same on text
    // made of digits and dots.
    [Fact]
    public void NotVersion_agrees_with_Version_TryParse_on_digits_and_dots() =>
        Fuzz.Run(VersionText, text =>
        {
            var fails = Contract.Fails(text, g => g.IfNotVersion(), ErrorCodes.NotVersion);
            var parses = Version.TryParse(text, out _);
            if (text is not null && text.All(c => c is '.' or (>= '0' and <= '9')))
            {
                Assert.True(parses != fails, $"Version.TryParse says {parses} for {Contract.Show(text)}.");
            }
            else
            {
                Assert.True(fails || parses, $"IfNotVersion passed {Contract.Show(text)}, which Version.TryParse rejects.");
            }
        });

    [Fact]
    public void NotCreditCardNumber_agrees_with_the_Luhn_checksum() =>
        Fuzz.Run(CardText, text =>
            Assert.True(
                IsLuhn(text) != Contract.Fails(text, g => g.IfNotCreditCardNumber(), ErrorCodes.NotCreditCardNumber),
                $"The Luhn reference says {IsLuhn(text)} for {Contract.Show(text)}."));

    [Fact]
    public void NotEnumName_agrees_with_the_member_names() =>
        Fuzz.Run(EnumText, text =>
        {
            var names = DayNames;
            Assert.Equal(text is null || !names.Contains(text, StringComparer.Ordinal), Contract.Fails(text, g => g.IfNotEnumName<DayOfWeek>(), ErrorCodes.NotEnumName));
            Assert.Equal(text is null || !names.Contains(text, StringComparer.OrdinalIgnoreCase), Contract.Fails(text, g => g.IfNotEnumName<DayOfWeek>(ignoreCase: true), ErrorCodes.NotEnumName));
        });

    private static readonly string[] DayNames =
    [
        nameof(DayOfWeek.Sunday), nameof(DayOfWeek.Monday), nameof(DayOfWeek.Tuesday), nameof(DayOfWeek.Wednesday),
        nameof(DayOfWeek.Thursday), nameof(DayOfWeek.Friday), nameof(DayOfWeek.Saturday),
    ];

    private static readonly Arbitrary<string?> GuidText = Generators.Of(Generators.Mutated(
        Gen.Zip(Gen.Choose(0, 255).Select(b => (byte)b).ArrayOf(16), Gen.Elements("N", "D", "B", "P", "X")).Select(pair => new Guid(pair.Item1).ToString(pair.Item2, CultureInfo.InvariantCulture)),
        "{}()-,0x ab"));

    private static readonly Arbitrary<string?> Base64Text = Generators.Of(Generators.Mutated(
        Gen.Choose(0, 40).SelectMany(length => Gen.Choose(0, 255).Select(b => (byte)b).ArrayOf(length)).Select(bytes => Convert.ToBase64String(bytes)),
        "=+/ \t\r\n-_A"));

    private static readonly Arbitrary<string?> VersionText = Generators.Of(Generators.Mutated(
        Generators.Join(Gen.Frequency((8, Gen.Choose(0, 99999).Select(n => n.ToString(CultureInfo.InvariantCulture))), (1, Gen.Elements("2147483647", "2147483648", "00", "-1", "+1", " 1", ""))), 1, 5, "."),
        ".0123456789 +-"));

    private static readonly Arbitrary<string?> CardText = Generators.Of(Generators.Mutated(
        Gen.Choose(0, 19).SelectMany(length => Gen.Choose(0, 9).ArrayOf(length)).Select(digits => WithCheckDigit(digits)),
        "0123456789 -"));

    private static readonly Arbitrary<string?> EnumText = Generators.Of(Generators.Mutated(
        Gen.Elements(DayNames.Concat(["monday", "MONDAY", "1", "Monday, Friday", " Monday", "Funday"]).ToArray()),
        "aeiouyMF, 1"));

    // A number with the right Luhn check digit, in groups of four with spaces or hyphens now and then.
    private static string WithCheckDigit(int[] digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            // The check digit goes last, so the rightmost digit here is doubled.
            var digit = digits[digits.Length - 1 - i];
            sum += i % 2 == 0 ? (digit * 2 > 9 ? (digit * 2) - 9 : digit * 2) : digit;
        }

        var separator = (digits.Length % 3) switch { 0 => string.Empty, 1 => " ", _ => "-" };
        var all = digits.Concat([(10 - (sum % 10)) % 10]).Select((d, i) => (i > 0 && i % 4 == 0 ? separator : string.Empty) + d.ToString(CultureInfo.InvariantCulture));
        return string.Concat(all);
    }

    // The documented rule, written as a reference: spaces and hyphens are ignored, everything
    // else must be an ASCII digit, there is at least one digit, and the Luhn sum ends in 0.
    private static bool IsLuhn(string? text)
    {
        if (text is null)
        {
            return false;
        }

        var digits = text.Where(c => c is not (' ' or '-')).ToArray();
        if (digits.Length == 0 || digits.Any(c => c is < '0' or > '9'))
        {
            return false;
        }

        var sum = Enumerable.Reverse(digits).Select((c, i) => i % 2 == 0 ? c - '0' : ((c - '0') * 2) - ((c - '0') >= 5 ? 9 : 0)).Sum();
        return sum % 10 == 0;
    }

    // Guid.TryParse, where a throw (.NET Framework, some hexadecimal text) means "not a GUID".
    private static bool IsGuid(string? text)
    {
        try
        {
            return Guid.TryParse(text, out _);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            return false;
        }
    }

    // Letters and digits (char.IsLetterOrDigit), with a surrogate pair read as one character.
    private static bool IsLettersOrDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            if (!char.IsLetterOrDigit(text, i))
            {
                return false;
            }
        }

        return true;
    }

#if NET
    private static bool DecodesAsBase64(string text)
    {
        try
        {
            _ = Convert.FromBase64String(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
#endif

    private sealed record Row(Support.Chain<string> Chain, string Code, string? NullCode = null);
}
