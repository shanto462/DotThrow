using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using DotThrow.Dynamic;
using Xunit;

namespace DotThrow.Tests.Dynamic;

// The same check from a definition and from code must give the same failures: name, message,
// error code, severity, attempted value and message arguments, for the default sample and
// after each change. Every built-in check name is in
// at least one case, and every case runs with the PropertyMap and the reflection constructors.
public sealed class DynamicParityTests
{
    private static readonly Dictionary<string, ParityCase> Cases = BuildCases();

    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var id in Cases.Keys)
        {
            data.Add(id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void Definition_and_code_give_the_same_failures(string id)
    {
        var parityCase = Cases[id];
        var definition = Rules.One(parityCase.Property, parityCase.Check, parityCase.RuleOptions);
        var code = new Validator<Sample>();
        parityCase.Code(code);
        var mapped = new DynamicValidator<Sample>(definition, Maps.Sample());
        var reflected = new DynamicValidator<Sample>(definition);

        var failures = 0;
        foreach (var change in parityCase.Changes)
        {
            var sample = new Sample();
            change(sample);
            var expected = code.Validate(sample);
            Same(expected, mapped.Validate(sample), id + " (map)");
            Same(expected, reflected.Validate(sample), id + " (reflection)");
            failures += expected.Failures.Count;
        }

        Assert.True(failures > 0, $"{id}: no change made the check fail, so the case proves nothing.");
    }

    [Fact]
    public void Every_built_in_check_has_a_parity_case()
    {
        var covered = new HashSet<string>(Cases.Values.Select(c => c.CheckName), StringComparer.OrdinalIgnoreCase);
#if !NET8_0_OR_GREATER || DOTTHROW_LEGACY_ASSET
        // NotParsable needs the .NET 8 build of the library (IParsable).
        covered.Add("NotParsable");
#endif
        var missing = CheckRegistry.BuiltInChecks.Where(name => !covered.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "No parity case for: " + string.Join(", ", missing));
    }

    private static void Same(ValidationReport expected, ValidationReport actual, string id)
    {
        Assert.True(
            expected.Failures.Count == actual.Failures.Count,
            $"{id}: code gave [{Rules.Show(expected)}], definition gave [{Rules.Show(actual)}]");
        for (var i = 0; i < expected.Failures.Count; i++)
        {
            var e = expected.Failures[i];
            var a = actual.Failures[i];
            Assert.True(e.Name == a.Name, $"{id}: name '{e.Name}' vs '{a.Name}'");
            Assert.True(e.Message == a.Message, $"{id}: message '{e.Message}' vs '{a.Message}'");
            Assert.True(e.ErrorCode == a.ErrorCode, $"{id}: code '{e.ErrorCode}' vs '{a.ErrorCode}'");
            Assert.Equal(e.Severity, a.Severity);
            Assert.True(Equals(e.AttemptedValue, a.AttemptedValue), $"{id}: attempted value '{e.AttemptedValue}' vs '{a.AttemptedValue}'");
            Assert.Equal(
                e.MessageArguments.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + Convert.ToString(p.Value, CultureInfo.InvariantCulture)),
                a.MessageArguments.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + Convert.ToString(p.Value, CultureInfo.InvariantCulture)));
        }
    }

    private sealed class ParityCase(string property, string check, Action<Validator<Sample>> code, Action<Sample>[] changes, string? ruleOptions)
    {
        public string Property { get; } = property;

        public string Check { get; } = check;

        public string? RuleOptions { get; } = ruleOptions;

        public Action<Validator<Sample>> Code { get; } = code;

        public Action<Sample>[] Changes { get; } = changes;

        public string CheckName
        {
            get
            {
                var definition = RuleSetDefinition.FromJson($$"""{ "rules": [ { "property": "x", "checks": [ {{Check}} ] } ] }""");
                return definition.Rules[0].Checks[0].Check!;
            }
        }
    }

    private static Dictionary<string, ParityCase> BuildCases()
    {
        var cases = new Dictionary<string, ParityCase>(StringComparer.Ordinal);
        var now = DateTime.UtcNow;

        void Add(string id, string property, string check, Action<Validator<Sample>> code, params Action<Sample>[] changes) =>
            cases.Add(id, new ParityCase(property, check, code, [_ => { }, .. changes], null));

        void AddWith(string id, string property, string ruleOptions, string check, Action<Validator<Sample>> code, params Action<Sample>[] changes) =>
            cases.Add(id, new ParityCase(property, check, code, [_ => { }, .. changes], ruleOptions));

        // Checks every type has.
        Add("Null", "Text", "\"Null\"", v => v.For(x => x.Text, g => g.IfNull()), s => s.Text = null, s => s.Text = "x");
        Add("NotNull", "MaybeId", "\"NotNull\"", v => v.For(x => x.MaybeId, g => g.IfNotNull()), s => s.MaybeId = Guid.Empty, s => s.MaybeId = null);
        Add("Default", "Number", "\"Default\"", v => v.For(x => x.Number, g => g.IfDefault()), s => s.Number = 0, s => s.Number = 1);
        Add("Empty.Guid", "Id", "\"Empty\"", v => v.For(x => x.Id, g => g.IfEmpty()), s => s.Id = Guid.Empty);
        Add("Empty.String", "Text", "\"Empty\"", v => v.For(x => x.Text, g => g.IfEmpty()), s => s.Text = string.Empty, s => s.Text = null);
        Add("NotEmpty", "Text", "\"NotEmpty\"", v => v.For(x => x.Text, g => g.IfNotEmpty()), s => s.Text = "x", s => s.Text = string.Empty);
        Add("EqualTo.Int", "Number", """{ "check": "EqualTo", "value": 5 }""", v => v.For(x => x.Number, g => g.IfEqualTo(5)), s => s.Number = 5);
        Add("EqualTo.String", "Text", """{ "check": "EqualTo", "value": "ada", "comparison": "OrdinalIgnoreCase" }""", v => v.For(x => x.Text, g => g.IfEqualTo("ada", StringComparison.OrdinalIgnoreCase)), s => s.Text = "ADA", s => s.Text = null);
        Add("EqualTo.StringOrdinal", "Text", """{ "check": "EqualTo", "value": "ada" }""", v => v.For(x => x.Text, g => g.IfEqualTo("ada")), s => s.Text = "ada", s => s.Text = "ADA");
        Add("NotEqualTo", "Price", """{ "check": "NotEqualTo", "value": "10.5" }""", v => v.For(x => x.Price, g => g.IfNotEqualTo(10.5m)), s => s.Price = 1);
        Add("NotEqualTo.String", "Text", """{ "check": "NotEqualTo", "value": "ada", "comparison": "OrdinalIgnoreCase" }""", v => v.For(x => x.Text, g => g.IfNotEqualTo("ada", StringComparison.OrdinalIgnoreCase)), s => s.Text = "ADA");
        Add("OneOf.Enum", "Status", """{ "check": "OneOf", "values": [ "Closed", "Draft" ] }""", v => v.For(x => x.Status, g => g.IfOneOf([Status.Closed, Status.Draft])), s => s.Status = Status.Closed);
        Add("OneOf.String", "Text", """{ "check": "OneOf", "values": [ "a", "b" ], "comparison": "OrdinalIgnoreCase" }""", v => v.For(x => x.Text, g => g.IfOneOf(new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase)), s => s.Text = "A");
        Add("NotOneOf", "Text", """{ "check": "NotOneOf", "values": [ "a", "b" ] }""", v => v.For(x => x.Text, g => g.IfNotOneOf(["a", "b"])), s => s.Text = "a", s => s.Text = "c");
        Add("NotOneOf.Number", "Number", """{ "check": "NotOneOf", "values": [ 1, 2, 3 ] }""", v => v.For(x => x.Number, g => g.IfNotOneOf([1, 2, 3])), s => s.Number = 2);
        Add("Type", "Text", """{ "check": "Type", "value": "String" }""", v => v.For(x => x.Text, g => g.IfType(typeof(string))));
        Add("NotType", "Body", """{ "check": "NotType", "value": "System.IO.MemoryStream" }""", v => v.For(x => x.Body, g => g.IfNotType(typeof(MemoryStream))), s => s.Body = new MemoryStream(), s => s.Body = Stream.Null);

        // Comparisons.
        Add("GreaterThan", "Number", """{ "check": "GreaterThan", "value": 100 }""", v => v.For(x => x.Number, g => g.IfGreaterThan(100)), s => s.Number = 101);
        Add("GreaterThanOrEqualTo", "Duration", """{ "check": "GreaterThanOrEqualTo", "value": "00:10:00" }""", v => v.For(x => x.Duration, g => g.IfGreaterThanOrEqualTo(TimeSpan.FromMinutes(10))), s => s.Duration = TimeSpan.FromMinutes(10));
        Add("LessThan", "Version", """{ "check": "LessThan", "value": "1.5" }""", v => v.For(x => x.Version, g => g.IfLessThan(new Version(1, 5))), s => s.Version = new Version(1, 9), s => s.Version = null);
        Add("LessThanOrEqualTo", "Letter", """{ "check": "LessThanOrEqualTo", "value": "c" }""", v => v.For(x => x.Letter, g => g.IfLessThanOrEqualTo('c')), s => s.Letter = 'd');
        Add("OutOfRange", "Price", """{ "check": "OutOfRange", "min": 1, "max": 100 }""", v => v.For(x => x.Price, g => g.IfOutOfRange(1m, 100m)), s => s.Price = 0, s => s.Price = 100.01m);
        Add("OutOfRange.String", "Text", """{ "check": "OutOfRange", "min": "a", "max": "m" }""", v => v.For(x => x.Text, g => g.IfOutOfRange("a", "m")), s => s.Text = "z");
        Add("OutOfRangeExclusive", "Short", """{ "check": "OutOfRangeExclusive", "min": 1, "max": 10 }""", v => v.For(x => x.Short, g => g.IfOutOfRangeExclusive((short)1, (short)10)), s => s.Short = 5);
        Add("InRange", "Big", """{ "check": "InRange", "min": 100, "max": 200 }""", v => v.For(x => x.Big, g => g.IfInRange(100L, 200L)), s => s.Big = 150);
        Add("Negative", "Ratio", "\"Negative\"", v => v.For(x => x.Ratio, g => g.IfNegative()), s => s.Ratio = -1);
        Add("NegativeOrZero", "Single", "\"NegativeOrZero\"", v => v.For(x => x.Single, g => g.IfNegativeOrZero()), s => s.Single = 0);
        Add("Positive", "Number", "\"Positive\"", v => v.For(x => x.Number, g => g.IfPositive()), s => s.Number = -1);
        Add("PositiveOrZero", "Big", "\"PositiveOrZero\"", v => v.For(x => x.Big, g => g.IfPositiveOrZero()), s => s.Big = -1);
        Add("Zero", "Price", "\"Zero\"", v => v.For(x => x.Price, g => g.IfZero()), s => s.Price = 0);
        Add("Nullable.GreaterThan", "MaybeNumber", """{ "check": "GreaterThan", "value": 5 }""", v => v.For(x => x.MaybeNumber, g => g.IfNull().IfGreaterThan(5)), s => s.MaybeNumber = null, s => s.MaybeNumber = 6, s => s.MaybeNumber = 1);
        AddWith("Nullable.AllowNull", "MaybeNumber", "\"allowNull\": true", """{ "check": "GreaterThan", "value": 5 }""", v => v.For(x => x.MaybeNumber, g => g.AllowNull().IfNull().IfGreaterThan(5)), s => s.MaybeNumber = null, s => s.MaybeNumber = 6);
        Add("Comparable.Custom", "Cost", """{ "check": "GreaterThan", "value": "10" }""", v => v.For(x => x.Cost, g => g.IfGreaterThan(new Money(10))), s => s.Cost = new Money(11));

        // Numbers.
        Add("NaN", "Ratio", "\"NaN\"", v => v.For(x => x.Ratio, g => g.IfNaN()), s => s.Ratio = double.NaN);
        Add("Infinity", "Single", "\"Infinity\"", v => v.For(x => x.Single, g => g.IfInfinity()), s => s.Single = float.PositiveInfinity);
        Add("NotFinite", "Ratio", "\"NotFinite\"", v => v.For(x => x.Ratio, g => g.IfNotFinite()), s => s.Ratio = double.NegativeInfinity);
        Add("Odd", "Number", "\"Odd\"", v => v.For(x => x.Number, g => g.IfOdd()), s => s.Number = 3);
        Add("Even", "Big", "\"Even\"", v => v.For(x => x.Big, g => g.IfEven()), s => s.Big = 3);
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Add("Even.Short", "Short", "\"Even\"", v => v.For(x => x.Short, g => g.IfEven()));
#endif
        Add("NotMultipleOf", "Number", """{ "check": "NotMultipleOf", "value": 3 }""", v => v.For(x => x.Number, g => g.IfNotMultipleOf(3)), s => s.Number = 9);
        Add("NotMultipleOf.Decimal", "Price", """{ "check": "NotMultipleOf", "value": 0.5 }""", v => v.For(x => x.Price, g => g.IfNotMultipleOf(0.5m)), s => s.Price = 10.25m);
        Add("NotPowerOfTwo", "Number", "\"NotPowerOfTwo\"", v => v.For(x => x.Number, g => g.IfNotPowerOfTwo()), s => s.Number = 8);
        Add("NotInteger", "Price", "\"NotInteger\"", v => v.For(x => x.Price, g => g.IfNotInteger()), s => s.Price = 10);
        Add("NotInteger.Double", "Ratio", "\"NotInteger\"", v => v.For(x => x.Ratio, g => g.IfNotInteger()));
        Add("PrecisionScaleExceeded", "Price", """{ "check": "PrecisionScaleExceeded", "values": [ 4, 1 ] }""", v => v.For(x => x.Price, g => g.IfPrecisionScaleExceeded(4, 1)), s => s.Price = 123.45m, s => s.Price = 1.50m);
        Add("PrecisionScaleExceeded.Trailing", "Price", """{ "check": "PrecisionScaleExceeded", "values": [ 4, 1 ], "options": "IgnoreTrailingZeros" }""", v => v.For(x => x.Price, g => g.IfPrecisionScaleExceeded(4, 1, ignoreTrailingZeros: true)), s => s.Price = 1.50m, s => s.Price = 1.55m);

        // Strings.
        Add("NullOrEmpty", "Text", "\"NullOrEmpty\"", v => v.For(x => x.Text, g => g.IfNullOrEmpty()), s => s.Text = string.Empty, s => s.Text = null);
        Add("WhiteSpace", "Text", "\"WhiteSpace\"", v => v.For(x => x.Text, g => g.IfNullOrWhiteSpace()), s => s.Text = " ", s => s.Text = null);
        Add("NullOrWhiteSpace", "Text", "\"NullOrWhiteSpace\"", v => v.For(x => x.Text, g => g.IfNullOrWhiteSpace()), s => s.Text = " ");
        Add("LongerThan", "Text", """{ "check": "LongerThan", "value": 5 }""", v => v.For(x => x.Text, g => g.IfLongerThan(5)), s => s.Text = "Ada", s => s.Text = null);
        Add("ShorterThan", "Text", """{ "check": "ShorterThan", "value": 20 }""", v => v.For(x => x.Text, g => g.IfShorterThan(20)));
        Add("LengthNotEqualTo", "Text", """{ "check": "LengthNotEqualTo", "value": 12 }""", v => v.For(x => x.Text, g => g.IfLengthNotEqualTo(12)), s => s.Text = "Ada");
        Add("LengthOutOfRange", "Text", """{ "check": "LengthOutOfRange", "min": 1, "max": 5 }""", v => v.For(x => x.Text, g => g.IfLengthOutOfRange(1, 5)), s => s.Text = string.Empty);
        Add("Contains", "Text", """{ "check": "Contains", "value": "love", "comparison": "OrdinalIgnoreCase" }""", v => v.For(x => x.Text, g => g.IfContains("love", StringComparison.OrdinalIgnoreCase)));
        Add("NotContains", "Text", """{ "check": "NotContains", "value": "xyz" }""", v => v.For(x => x.Text, g => g.IfNotContains("xyz")), s => s.Text = "axyz");
        Add("StartsWith", "Text", """{ "check": "StartsWith", "value": "Ada" }""", v => v.For(x => x.Text, g => g.IfStartsWith("Ada")));
        Add("NotStartsWith", "Text", """{ "check": "NotStartsWith", "value": "Bob" }""", v => v.For(x => x.Text, g => g.IfNotStartsWith("Bob")));
        Add("EndsWith", "Text", """{ "check": "EndsWith", "value": "lace" }""", v => v.For(x => x.Text, g => g.IfEndsWith("lace")));
        Add("NotEndsWith", "Text", """{ "check": "NotEndsWith", "value": "X", "comparison": "OrdinalIgnoreCase" }""", v => v.For(x => x.Text, g => g.IfNotEndsWith("X", StringComparison.OrdinalIgnoreCase)), s => s.Text = "abx");
        Add("Matches", "Text", """{ "check": "Matches", "value": "^ada", "options": "IgnoreCase" }""", v => v.For(x => x.Text, g => g.IfMatches("^ada", System.Text.RegularExpressions.RegexOptions.IgnoreCase)), s => s.Text = "Bob");
        Add("NotMatches", "Address.Zip", """{ "check": "NotMatches", "value": "^[0-9]{5}$" }""", v => v.For(x => x.Address?.Zip, g => g.IfNotMatches("^[0-9]{5}$")), s => s.Address!.Zip = "x");
        Add("NotEmailAddress", "Text", "\"NotEmailAddress\"", v => v.For(x => x.Text, g => g.IfNotEmailAddress()), s => s.Text = "ada@example.com");
        Add("NotJson", "Text", "\"NotJson\"", v => v.For(x => x.Text, g => g.IfNotJson()), s => s.Text = "{}");
        Add("NotXml", "Text", "\"NotXml\"", v => v.For(x => x.Text, g => g.IfNotXml()), s => s.Text = "<a/>");
        Add("NotUri", "Text", """{ "check": "NotUri", "options": "Absolute" }""", v => v.For(x => x.Text, g => g.IfNotUri(UriKind.Absolute)), s => s.Text = "https://example.com");
        Add("NotUri.Default", "Text", "\"NotUri\"", v => v.For(x => x.Text, g => g.IfNotUri()));
        Add("NotHttpUrl", "Text", "\"NotHttpUrl\"", v => v.For(x => x.Text, g => g.IfNotHttpUrl()), s => s.Text = "https://example.com");
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Add("NotParsable", "Text", """{ "check": "NotParsable", "value": "int" }""", v => v.For(x => x.Text, g => g.IfNotParsable<int>(CultureInfo.InvariantCulture)), s => s.Text = "42");
#endif
        Add("AllUpperCase", "Text", "\"AllUpperCase\"", v => v.For(x => x.Text, g => g.IfAllUpperCase()), s => s.Text = "ADA");
        Add("AllLowerCase", "Text", "\"AllLowerCase\"", v => v.For(x => x.Text, g => g.IfAllLowerCase()), s => s.Text = "ada");
        Add("NotTrimmed", "Text", "\"NotTrimmed\"", v => v.For(x => x.Text, g => g.IfNotTrimmed()), s => s.Text = " ada");
        Add("ContainsWhiteSpace", "Text", "\"ContainsWhiteSpace\"", v => v.For(x => x.Text, g => g.IfContainsWhiteSpace()));
        Add("ContainsControlCharacters", "Text", "\"ContainsControlCharacters\"", v => v.For(x => x.Text, g => g.IfContainsControlCharacters()), s => s.Text = "a\u0001");
        Add("ContainsAny", "Text", """{ "check": "ContainsAny", "value": "@#" }""", v => v.For(x => x.Text, g => g.IfContainsAny('@', '#')), s => s.Text = "a#b");
        Add("ContainsAnyExcept", "Text", """{ "check": "ContainsAnyExcept", "value": "abc" }""", v => v.For(x => x.Text, g => g.IfContainsAnyExcept('a', 'b', 'c')), s => s.Text = "abc");
        Add("NotAscii", "Text", "\"NotAscii\"", v => v.For(x => x.Text, g => g.IfNotAscii()), s => s.Text = "café");
        Add("NotDigits", "Text", "\"NotDigits\"", v => v.For(x => x.Text, g => g.IfNotDigits()), s => s.Text = "123");
        Add("NotLettersOrDigits", "Text", "\"NotLettersOrDigits\"", v => v.For(x => x.Text, g => g.IfNotLettersOrDigits()), s => s.Text = "Ada1");
        Add("NotHex", "Text", "\"NotHex\"", v => v.For(x => x.Text, g => g.IfNotHex()), s => s.Text = "0aF9");
        Add("NotBase64", "Text", "\"NotBase64\"", v => v.For(x => x.Text, g => g.IfNotBase64()), s => s.Text = "QWRh");
        Add("NotGuid", "Text", "\"NotGuid\"", v => v.For(x => x.Text, g => g.IfNotGuid()), s => s.Text = Guid.Empty.ToString());
        Add("NotIPAddress", "Text", "\"NotIPAddress\"", v => v.For(x => x.Text, g => g.IfNotIPAddress()), s => s.Text = "203.0.113.5");
        Add("NotSlug", "Text", "\"NotSlug\"", v => v.For(x => x.Text, g => g.IfNotSlug()), s => s.Text = "ada-lovelace");
        Add("NotVersion", "Text", "\"NotVersion\"", v => v.For(x => x.Text, g => g.IfNotVersion()), s => s.Text = "1.2.3");
        Add("NotCreditCardNumber", "Text", "\"NotCreditCardNumber\"", v => v.For(x => x.Text, g => g.IfNotCreditCardNumber()), s => s.Text = "4111111111111111");
        Add("NotEnumName", "Text", """{ "check": "NotEnumName", "value": "DotThrow.Tests.Dynamic.Status", "options": "IgnoreCase" }""", v => v.For(x => x.Text, g => g.IfNotEnumName<Status>(ignoreCase: true)), s => s.Text = "active");
        Add("NotFileName", "Text", "\"NotFileName\"", v => v.For(x => x.Text, g => g.IfNotFileName()), s => s.Text = "a/b\0");

        // Collections and dictionaries.
        Add("NullOrEmpty.List", "Tags", "\"NullOrEmpty\"", v => v.For(x => x.Tags, g => g.IfNullOrEmpty()), s => s.Tags = []);
        Add("ContainsNull", "Tags", "\"ContainsNull\"", v => v.For(x => x.Tags, g => g.IfContainsNull()), s => s.Tags = ["a", null]);
        Add("HasDuplicates", "Scores", "\"HasDuplicates\"", v => v.For(x => x.Scores, g => g.IfHasDuplicates()), s => s.Scores = [1, 1]);
        Add("CountEqualTo", "Tags", """{ "check": "CountEqualTo", "value": 1 }""", v => v.For(x => x.Tags, g => g.IfCountEqualTo(1)));
        Add("CountNotEqualTo", "Scores", """{ "check": "CountNotEqualTo", "value": 2 }""", v => v.For(x => x.Scores, g => g.IfCountNotEqualTo(2)), s => s.Scores = [1]);
        Add("CountGreaterThan", "Tags", """{ "check": "CountGreaterThan", "value": 1 }""", v => v.For(x => x.Tags, g => g.IfCountGreaterThan(1)), s => s.Tags = ["a", "b"]);
        Add("CountLessThan", "Scores", """{ "check": "CountLessThan", "value": 3 }""", v => v.For(x => x.Scores, g => g.IfCountLessThan(3)));
        Add("CountOutOfRange", "Tags", """{ "check": "CountOutOfRange", "min": 1, "max": 2 }""", v => v.For(x => x.Tags, g => g.IfCountOutOfRange(1, 2)), s => s.Tags = [], s => s.Tags = ["a", "b", "c"]);
        Add("Contains.List", "Tags", """{ "check": "Contains", "value": "math" }""", v => v.For(x => x.Tags, g => g.IfContains((string?)"math")));
        Add("NotContains.Array", "Scores", """{ "check": "NotContains", "value": 5 }""", v => v.For(x => x.Scores, g => g.IfNotContains(5)), s => s.Scores = [5]);
        Add("ContainsKey", "Counts", """{ "check": "ContainsKey", "value": "a" }""", v => v.For(x => x.Counts, g => g.IfContainsKey("a")));
        Add("NotContainsKey", "Counts", """{ "check": "NotContainsKey", "value": "b" }""", v => v.For(x => x.Counts, g => g.IfNotContainsKey("b")), s => s.Counts["b"] = 2);

        // Bool and enums.
        Add("True", "Flag", "\"True\"", v => v.For(x => x.Flag, g => g.IfTrue()), s => s.Flag = true);
        Add("False", "Flag", "\"False\"", v => v.For(x => x.Flag, g => g.IfFalse()));
        Add("True.Nullable", "MaybeFlag", "\"True\"", v => v.For(x => x.MaybeFlag, g => g.IfNull().IfTrue()), s => s.MaybeFlag = true, s => s.MaybeFlag = false);
        Add("NotDefined", "Status", "\"NotDefined\"", v => v.For(x => x.Status, g => g.IfNotDefined()), s => s.Status = (Status)42);
        Add("NotDefined.Nullable", "MaybeStatus", "\"NotDefined\"", v => v.For(x => x.MaybeStatus, g => g.IfNull().IfNotDefined()), s => s.MaybeStatus = (Status)42, s => s.MaybeStatus = Status.Draft);
        Add("HasFlag", "Access", """{ "check": "HasFlag", "value": "Admin" }""", v => v.For(x => x.Access, g => g.IfHasFlag(Access.Admin)), s => s.Access = Access.Read | Access.Admin);
        Add("NotHasFlag", "Access", """{ "check": "NotHasFlag", "value": "Read, Write" }""", v => v.For(x => x.Access, g => g.IfNotHasFlag(Access.Read | Access.Write)), s => s.Access = Access.Read | Access.Write);

        // Dates and times.
        Add("Before", "When", """{ "check": "Before", "value": "2026-01-01T00:00:00Z" }""", v => v.For(x => x.When, g => g.IfBefore(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))), s => s.When = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Add("Before.DateOnly", "Day", """{ "check": "Before", "value": "2026-01-06" }""", v => v.For(x => x.Day, g => g.IfBefore(new DateOnly(2026, 1, 6))));
#endif
        Add("After", "At", """{ "check": "After", "value": "2026-01-01T00:00:00+00:00" }""", v => v.For(x => x.At, g => g.IfAfter(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Add("After.TimeOnly", "Time", """{ "check": "After", "value": "09:00" }""", v => v.For(x => x.Time, g => g.IfAfter(new TimeOnly(9, 0))));
#endif
        Add("InPast", "When", "\"InPast\"", v => v.For(x => x.When, g => g.IfInPast()), s => s.When = now.AddDays(10));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Add("InPast.DateOnly", "Day", "\"InPast\"", v => v.For(x => x.Day, g => g.IfInPast()));
#endif
        Add("InFuture", "At", "\"InFuture\"", v => v.For(x => x.At, g => g.IfInFuture()), s => s.At = new DateTimeOffset(now.AddDays(10)));
        Add("NotUtc", "When", "\"NotUtc\"", v => v.For(x => x.When, g => g.IfNotUtc()), s => s.When = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Local));
        Add("Weekend", "When", "\"Weekend\"", v => v.For(x => x.When, g => g.IfWeekend()), s => s.When = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        Add("Weekday", "When", "\"Weekday\"", v => v.For(x => x.When, g => g.IfWeekday()), s => s.When = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        Add("Weekday.DateOnly", "Day", "\"Weekday\"", v => v.For(x => x.Day, g => g.IfWeekday()));
#endif
        Add("HasTimeOfDay", "When", "\"HasTimeOfDay\"", v => v.For(x => x.When, g => g.IfHasTimeOfDay()), s => s.When = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc));
        Add("OlderThan", "When", """{ "check": "OlderThan", "value": "30.00:00:00" }""", v => v.For(x => x.When, g => g.IfOlderThan(TimeSpan.FromDays(30))), s => s.When = now.AddDays(-1));
        Add("NewerThan", "At", """{ "check": "NewerThan", "value": "1.00:00:00" }""", v => v.For(x => x.At, g => g.IfNewerThan(TimeSpan.FromDays(1))), s => s.At = new DateTimeOffset(now.AddHours(-1)));

        // Uri, IPAddress, Stream and Type.
        Add("Relative", "Link", "\"Relative\"", v => v.For(x => x.Link, g => g.IfRelative()), s => s.Link = new Uri("/a", UriKind.Relative));
        Add("NotHttps", "Link", "\"NotHttps\"", v => v.For(x => x.Link, g => g.IfNotHttps()), s => s.Link = new Uri("http://example.com"));
        Add("NotHttpOrHttps", "Link", "\"NotHttpOrHttps\"", v => v.For(x => x.Link, g => g.IfNotHttpOrHttps()), s => s.Link = new Uri("ftp://example.com"));
        Add("NotScheme", "Link", """{ "check": "NotScheme", "values": [ "https", "mailto" ] }""", v => v.For(x => x.Link, g => g.IfNotScheme("https", "mailto")), s => s.Link = new Uri("http://example.com"));
        Add("Loopback.Uri", "Link", "\"Loopback\"", v => v.For(x => x.Link, g => g.IfLoopback()), s => s.Link = new Uri("http://localhost/"));
        Add("Loopback.IP", "Ip", "\"Loopback\"", v => v.For(x => x.Ip, g => g.IfLoopback()), s => s.Ip = IPAddress.Loopback);
        Add("Private", "Ip", "\"Private\"", v => v.For(x => x.Ip, g => g.IfPrivate()), s => s.Ip = IPAddress.Parse("10.0.0.1"));
        Add("NotIPv4", "Ip", "\"NotIPv4\"", v => v.For(x => x.Ip, g => g.IfNotIPv4()), s => s.Ip = IPAddress.IPv6Loopback);
        Add("NotIPv6", "Ip", "\"NotIPv6\"", v => v.For(x => x.Ip, g => g.IfNotIPv6()));
        Add("NotReadable", "Body", "\"NotReadable\"", v => v.For(x => x.Body, g => g.IfNotReadable()), s => s.Body = new MemoryStream(), s => s.Body = ClosedStream());
        Add("NotWritable", "Body", "\"NotWritable\"", v => v.For(x => x.Body, g => g.IfNotWritable()), s => s.Body = new MemoryStream(new byte[1], writable: false));
        Add("NotSeekable", "Body", "\"NotSeekable\"", v => v.For(x => x.Body, g => g.IfNotSeekable()), s => s.Body = ClosedStream());
        Add("NotAssignableTo", "Kind", """{ "check": "NotAssignableTo", "value": "System.IDisposable" }""", v => v.For(x => x.Kind, g => g.IfNotAssignableTo(typeof(IDisposable))), s => s.Kind = typeof(MemoryStream));
        Add("Abstract", "Kind", "\"Abstract\"", v => v.For(x => x.Kind, g => g.IfAbstract()), s => s.Kind = typeof(Stream));
        Add("Interface", "Kind", "\"Interface\"", v => v.For(x => x.Kind, g => g.IfInterface()), s => s.Kind = typeof(IDisposable));

        // Nested paths, Each, item rules and rule options.
        Add("Nested", "Address.City", """{ "check": "LongerThan", "value": 3 }""", v => v.For(x => x.Address?.City, g => g.IfLongerThan(3)), s => s.Address = null, s => s.Address!.City = "Rome");
        Add("Nested.Lifted", "Address.Floor", """{ "check": "GreaterThan", "value": 10 }""", v => v.For(x => x.Address?.Floor, g => g.IfNull().IfGreaterThan(10)), s => s.Address = null, s => s.Address!.Floor = 11);
        Add("Nested.Struct", "Address.Point.Latitude", """{ "check": "OutOfRange", "min": -90, "max": 90 }""", v => v.For(x => x.Address?.Point?.Latitude, g => g.IfNull().IfOutOfRange(-90d, 90d)), s => s.Address!.Point = new GeoPoint { Latitude = 91 }, s => s.Address!.Point = new GeoPoint { Latitude = 1 });
        AddWith("Each", "Tags", "\"each\": true", """{ "check": "LongerThan", "value": 3 }""", v => v.ForEach(x => x.Tags, t => t.IfLongerThan(3)), s => s.Tags = ["abcd", "ab", null, "abcde"]);
        AddWith("Each.Scores", "Scores", "\"each\": true", """{ "check": "GreaterThan", "value": 1 }""", v => v.ForEach(x => x.Scores, t => t.IfGreaterThan(1)), s => s.Scores = [1, 2, 3]);
        Add("Items", "Lines[].Sku", "\"NullOrEmpty\"", v => v.ForEach(x => x.Lines, line => line.For(l => l.Sku, g => g.IfNullOrEmpty())), s => s.Lines = [new Line(), null, new Line { Sku = "x" }, new Line { Sku = string.Empty }]);
        Add("Items.Each", "Lines[]", "\"Null\"", v => v.ForEach(x => x.Lines, line => line.IfNull()), s => s.Lines = [new Line(), null]);
        AddWith("Options", "Number", "\"severity\": \"Warning\", \"errorCode\": \"N1\", \"name\": \"Count\"", """{ "check": "GreaterThan", "value": 1 }""", v => v.For(x => x.Number, g => g.IfGreaterThan(1), o => o.WithSeverity(Severity.Warning).WithErrorCode("N1").WithName("Count")));
        AddWith("Options.Message", "Text", "\"message\": \"Too long: {PropertyName}\"", """{ "check": "LongerThan", "value": 3 }""", v => v.For(x => x.Text, g => g.IfLongerThan(3, "Too long: {PropertyName}")), s => s.Text = null);
        Add("CheckMessage", "Text", """{ "check": "LongerThan", "value": 3, "message": "{PropertyName} has {Length} characters" }""", v => v.For(x => x.Text, g => g.IfLongerThan(3, "{PropertyName} has {Length} characters")));
        Add("CheckErrorCode", "Number", """{ "check": "GreaterThan", "value": 1, "errorCode": "TooBig" }""", v => v.For(x => x.Number, g => g.IfGreaterThan(1), o => o.WithErrorCode("TooBig")));
        AddWith("Cascade.Continue", "Text", "\"cascade\": \"Continue\"", """{ "check": "LongerThan", "value": 3 }, "NotEmailAddress" """, v => v.For(x => x.Text, g => g.IfLongerThan(3).IfNotEmailAddress(), o => o.Cascade(CascadeMode.Continue)));
        Add("Chain", "Text", """{ "check": "LongerThan", "value": 3 }, "NotEmailAddress" """, v => v.For(x => x.Text, g => g.IfLongerThan(3).IfNotEmailAddress()), s => s.Text = "ab");
        return cases;
    }

    private static MemoryStream ClosedStream()
    {
        var stream = new MemoryStream();
        stream.Dispose();
        return stream;
    }
}
