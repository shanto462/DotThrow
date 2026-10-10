using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System.Buffers;
#endif

namespace DotThrow.Tests.Guards;

// Case tables for the text checks added in v2. TextCheckTests holds the detailed edge cases.
public sealed class TextGuardTests
{
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    private static readonly SearchValues<char> Angles = SearchValues.Create("<>");
    private static readonly SearchValues<char> Lowercase = SearchValues.Create("abcdefghijklmnopqrstuvwxyz");
#endif

    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(" Ada", (g, m) => g.IfNotTrimmed(m), ErrorCodes.NotTrimmed, Expect.Argument, "'input' must not start or end with white space.");
        yield return Fail("Ada\t", (g, m) => g.IfNotTrimmed(m), ErrorCodes.NotTrimmed, Expect.Argument, "'input' must not start or end with white space.");
        yield return Fail("Ada Lovelace", (g, m) => g.IfContainsWhiteSpace(m), ErrorCodes.ContainsWhiteSpace, Expect.Argument, "'input' must not contain white space.");
        yield return Fail("Ada\u0001", (g, m) => g.IfContainsControlCharacters(m), ErrorCodes.ContainsControlCharacters, Expect.Argument, "'input' must not contain control characters.");
        yield return Fail("a<b", (g, m) => g.IfContainsAny(['<', '>'], m), ErrorCodes.ContainsAny, Expect.Argument, "'input' must not contain any of the rejected characters.");
        yield return Fail("abc!", (g, m) => g.IfContainsAnyExcept(['a', 'b', 'c'], m), ErrorCodes.ContainsAnyExcept, Expect.Argument, "'input' must contain only the allowed characters.");
        yield return Fail("café", (g, m) => g.IfNotAscii(m), ErrorCodes.NotAscii, Expect.Argument, "'input' must contain only ASCII characters.");
        yield return Fail("12a", (g, m) => g.IfNotDigits(m), ErrorCodes.NotDigits, Expect.Argument, "'input' must contain only the digits 0 to 9.");
        yield return Fail("", (g, m) => g.IfNotDigits(m), ErrorCodes.NotDigits, Expect.Argument, "'input' must contain only the digits 0 to 9.");
        yield return Fail("a-b", (g, m) => g.IfNotLettersOrDigits(m), ErrorCodes.NotLettersOrDigits, Expect.Argument, "'input' must contain only letters or digits.");
        yield return Fail("xyz", (g, m) => g.IfNotHex(m), ErrorCodes.NotHex, Expect.Argument, "'input' must be a hexadecimal string.");
        yield return Fail("abc", (g, m) => g.IfNotBase64(m), ErrorCodes.NotBase64, Expect.Argument, "'input' must be a valid Base64 string.");
        yield return Fail("not-a-guid", (g, m) => g.IfNotGuid(m), ErrorCodes.NotGuid, Expect.Argument, "'input' must be a valid GUID.");
        yield return Fail("256.1.1.1", (g, m) => g.IfNotIPAddress(m), ErrorCodes.NotIPAddress, Expect.Argument, "'input' must be a valid IP address.");
        yield return Fail("Not A Slug", (g, m) => g.IfNotSlug(m), ErrorCodes.NotSlug, Expect.Argument, "'input' must be a slug of lowercase letters, digits and single hyphens.");
        yield return Fail("1", (g, m) => g.IfNotVersion(m), ErrorCodes.NotVersion, Expect.Argument, "'input' must be a valid version number.");
        yield return Fail("4111111111111112", (g, m) => g.IfNotCreditCardNumber(m), ErrorCodes.NotCreditCardNumber, Expect.Argument, "'input' must be a valid credit card number.");
        yield return Fail("Funday", (g, m) => g.IfNotEnumName<DayOfWeek>(message: m), ErrorCodes.NotEnumName, Expect.Argument, "'input' must be a defined DayOfWeek name.");
        yield return Fail("monday", (g, m) => g.IfNotEnumName<DayOfWeek>(message: m), ErrorCodes.NotEnumName, Expect.Argument, "'input' must be a defined DayOfWeek name.");
        yield return Fail("a/b", (g, m) => g.IfNotFileName(m), ErrorCodes.NotFileName, Expect.Argument, "'input' must be a valid file name.");
        yield return Fail("..", (g, m) => g.IfNotFileName(m), ErrorCodes.NotFileName, Expect.Argument, "'input' must be a valid file name.");

        // Plain arrays bind to the char[] overloads (collection expressions bind to spans on .NET 8+).
        yield return Fail("a<b", (g, m) => g.IfContainsAny(new[] { '<', '>' }, m), ErrorCodes.ContainsAny, Expect.Argument, "'input' must not contain any of the rejected characters.");
        yield return Fail("abc!", (g, m) => g.IfContainsAnyExcept(new[] { 'a', 'b', 'c' }, m), ErrorCodes.ContainsAnyExcept, Expect.Argument, "'input' must contain only the allowed characters.");
        yield return Null<string>((g, m) => g.IfContainsAny(new[] { 'a' }, m));
        yield return Null<string>((g, m) => g.IfContainsAnyExcept(new[] { 'a' }, m));

        yield return Null<string>((g, m) => g.IfNotTrimmed(m));
        yield return Null<string>((g, m) => g.IfContainsWhiteSpace(m));
        yield return Null<string>((g, m) => g.IfContainsControlCharacters(m));
        yield return Null<string>((g, m) => g.IfContainsAny(['a'], m));
        yield return Null<string>((g, m) => g.IfContainsAnyExcept(['a'], m));
        yield return Null<string>((g, m) => g.IfNotAscii(m));
        yield return Null<string>((g, m) => g.IfNotDigits(m));
        yield return Null<string>((g, m) => g.IfNotLettersOrDigits(m));
        yield return Null<string>((g, m) => g.IfNotHex(m));
        yield return Null<string>((g, m) => g.IfNotBase64(m));
        yield return Null<string>((g, m) => g.IfNotGuid(m));
        yield return Null<string>((g, m) => g.IfNotIPAddress(m));
        yield return Null<string>((g, m) => g.IfNotSlug(m));
        yield return Null<string>((g, m) => g.IfNotVersion(m));
        yield return Null<string>((g, m) => g.IfNotCreditCardNumber(m));
        yield return Null<string>((g, m) => g.IfNotEnumName<DayOfWeek>(message: m));
        yield return Null<string>((g, m) => g.IfNotFileName(m));

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        yield return Fail("a<b", (g, m) => g.IfContainsAny(Angles, m), ErrorCodes.ContainsAny, Expect.Argument, "'input' must not contain any of the rejected characters.");
        yield return Fail("abc!", (g, m) => g.IfContainsAnyExcept(Lowercase, m), ErrorCodes.ContainsAnyExcept, Expect.Argument, "'input' must contain only the allowed characters.");
#endif
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass("Ada Lovelace", (g, m) => g.IfNotTrimmed(m));
        yield return Pass("", (g, m) => g.IfNotTrimmed(m));
        yield return Pass("AdaLovelace", (g, m) => g.IfContainsWhiteSpace(m));
        yield return Pass("Ada Lovelace", (g, m) => g.IfContainsControlCharacters(m));
        yield return Pass("ab", (g, m) => g.IfContainsAny(['<', '>'], m));
        yield return Pass("", (g, m) => g.IfContainsAny(['<'], m));
        yield return Pass("abc", (g, m) => g.IfContainsAnyExcept(['a', 'b', 'c'], m));
        yield return Pass("ab", (g, m) => g.IfContainsAny(new[] { '<', '>' }, m));
        yield return Pass("abc", (g, m) => g.IfContainsAnyExcept(new[] { 'a', 'b', 'c' }, m));
        yield return Pass("", (g, m) => g.IfNotAscii(m));
        yield return Pass("ada@example.com", (g, m) => g.IfNotAscii(m));
        yield return Pass("0123456789", (g, m) => g.IfNotDigits(m));
        yield return Pass("Ada1", (g, m) => g.IfNotLettersOrDigits(m));
        yield return Pass("日本2", (g, m) => g.IfNotLettersOrDigits(m));
        yield return Pass("0aF9", (g, m) => g.IfNotHex(m));
        yield return Pass("QWRhIExvdmVsYWNl", (g, m) => g.IfNotBase64(m));
        yield return Pass("6f9619ff-8b86-d011-b42d-00cf4fc964ff", (g, m) => g.IfNotGuid(m));
        yield return Pass("192.168.0.1", (g, m) => g.IfNotIPAddress(m));
        yield return Pass("fe80::1%eth0", (g, m) => g.IfNotIPAddress(m));
        yield return Pass("ada-lovelace-1815", (g, m) => g.IfNotSlug(m));
        yield return Pass("1.2.3.4", (g, m) => g.IfNotVersion(m));
        yield return Pass("4111111111111111", (g, m) => g.IfNotCreditCardNumber(m));
        yield return Pass("4111 1111 1111 1111", (g, m) => g.IfNotCreditCardNumber(m));
        yield return Pass("Monday", (g, m) => g.IfNotEnumName<DayOfWeek>(message: m));
        yield return Pass("monday", (g, m) => g.IfNotEnumName<DayOfWeek>(ignoreCase: true, message: m));
        yield return Pass("report.txt", (g, m) => g.IfNotFileName(m));

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        yield return Pass("ab", (g, m) => g.IfContainsAny(Angles, m));
        yield return Pass("abc", (g, m) => g.IfContainsAnyExcept(Lowercase, m));
#endif
    }

    [Fact]
    public void Params_array_forms_without_a_message()
    {
        Assert.Throws<ArgumentException>(() => "a<b".Throw().IfContainsAny(new[] { '<', '>' }));
        Assert.Throws<ArgumentException>(() => "abc!".Throw().IfContainsAnyExcept(new[] { 'a', 'b', 'c' }));
        "abc".Throw().IfContainsAny(new[] { '<' }).IfContainsAnyExcept(new[] { 'a', 'b', 'c' });
    }

    public static TheoryData<CheckCase> Failures => Data(FailureCases());

    public static TheoryData<CheckCase> Passes => Data(PassCases());

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_throws_the_default_exception(CheckCase check) => check.AssertThrows();

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_uses_a_custom_message(CheckCase check) => check.AssertCustomMessage();

    [Theory]
    [MemberData(nameof(Passes))]
    public void Passing_check_returns_the_value(CheckCase check) => check.AssertPasses();
}
