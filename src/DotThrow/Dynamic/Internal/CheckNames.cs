using System;
using System.Collections.Generic;

namespace DotThrow.Dynamic.Internal;

// The names of the built-in checks: the error code of each check method, plus two aliases
// for methods that share a code with another check. The catalog also says, for error
// messages and docs, which value types a check applies to.
internal static class CheckNames
{
    public const string Null = nameof(ErrorCodes.Null);
    public const string NotNull = nameof(ErrorCodes.NotNull);
    public const string Default = nameof(ErrorCodes.Default);
    public const string EqualTo = nameof(ErrorCodes.EqualTo);
    public const string NotEqualTo = nameof(ErrorCodes.NotEqualTo);
    public const string OneOf = nameof(ErrorCodes.OneOf);
    public const string NotOneOf = nameof(ErrorCodes.NotOneOf);
    public const string Type = nameof(ErrorCodes.Type);
    public const string NotType = nameof(ErrorCodes.NotType);
    public const string Empty = nameof(ErrorCodes.Empty);
    public const string NotEmpty = nameof(ErrorCodes.NotEmpty);
    public const string GreaterThan = nameof(ErrorCodes.GreaterThan);
    public const string GreaterThanOrEqualTo = nameof(ErrorCodes.GreaterThanOrEqualTo);
    public const string LessThan = nameof(ErrorCodes.LessThan);
    public const string LessThanOrEqualTo = nameof(ErrorCodes.LessThanOrEqualTo);
    public const string OutOfRange = nameof(ErrorCodes.OutOfRange);
    public const string OutOfRangeExclusive = nameof(ErrorCodes.OutOfRangeExclusive);
    public const string InRange = nameof(ErrorCodes.InRange);
    public const string Negative = nameof(ErrorCodes.Negative);
    public const string NegativeOrZero = nameof(ErrorCodes.NegativeOrZero);
    public const string Positive = nameof(ErrorCodes.Positive);
    public const string PositiveOrZero = nameof(ErrorCodes.PositiveOrZero);
    public const string Zero = nameof(ErrorCodes.Zero);
    public const string NaN = nameof(ErrorCodes.NaN);
    public const string Infinity = nameof(ErrorCodes.Infinity);
    public const string NotFinite = nameof(ErrorCodes.NotFinite);
    public const string Odd = nameof(ErrorCodes.Odd);
    public const string Even = nameof(ErrorCodes.Even);
    public const string NotMultipleOf = nameof(ErrorCodes.NotMultipleOf);
    public const string NotPowerOfTwo = nameof(ErrorCodes.NotPowerOfTwo);
    public const string NotInteger = nameof(ErrorCodes.NotInteger);
    public const string PrecisionScaleExceeded = nameof(ErrorCodes.PrecisionScaleExceeded);
    public const string NullOrEmpty = "NullOrEmpty";
    public const string WhiteSpace = nameof(ErrorCodes.WhiteSpace);
    public const string NullOrWhiteSpace = "NullOrWhiteSpace";
    public const string LongerThan = nameof(ErrorCodes.LongerThan);
    public const string ShorterThan = nameof(ErrorCodes.ShorterThan);
    public const string LengthNotEqualTo = nameof(ErrorCodes.LengthNotEqualTo);
    public const string LengthOutOfRange = nameof(ErrorCodes.LengthOutOfRange);
    public const string Contains = nameof(ErrorCodes.Contains);
    public const string NotContains = nameof(ErrorCodes.NotContains);
    public const string StartsWith = nameof(ErrorCodes.StartsWith);
    public const string NotStartsWith = nameof(ErrorCodes.NotStartsWith);
    public const string EndsWith = nameof(ErrorCodes.EndsWith);
    public const string NotEndsWith = nameof(ErrorCodes.NotEndsWith);
    public const string Matches = nameof(ErrorCodes.Matches);
    public const string NotMatches = nameof(ErrorCodes.NotMatches);
    public const string NotEmailAddress = nameof(ErrorCodes.NotEmailAddress);
    public const string NotJson = nameof(ErrorCodes.NotJson);
    public const string NotXml = nameof(ErrorCodes.NotXml);
    public const string NotUri = nameof(ErrorCodes.NotUri);
    public const string NotHttpUrl = nameof(ErrorCodes.NotHttpUrl);
    public const string NotParsable = nameof(ErrorCodes.NotParsable);
    public const string AllUpperCase = nameof(ErrorCodes.AllUpperCase);
    public const string AllLowerCase = nameof(ErrorCodes.AllLowerCase);
    public const string NotTrimmed = nameof(ErrorCodes.NotTrimmed);
    public const string ContainsWhiteSpace = nameof(ErrorCodes.ContainsWhiteSpace);
    public const string ContainsControlCharacters = nameof(ErrorCodes.ContainsControlCharacters);
    public const string ContainsAny = nameof(ErrorCodes.ContainsAny);
    public const string ContainsAnyExcept = nameof(ErrorCodes.ContainsAnyExcept);
    public const string NotAscii = nameof(ErrorCodes.NotAscii);
    public const string NotDigits = nameof(ErrorCodes.NotDigits);
    public const string NotLettersOrDigits = nameof(ErrorCodes.NotLettersOrDigits);
    public const string NotHex = nameof(ErrorCodes.NotHex);
    public const string NotBase64 = nameof(ErrorCodes.NotBase64);
    public const string NotGuid = nameof(ErrorCodes.NotGuid);
    public const string NotIPAddress = nameof(ErrorCodes.NotIPAddress);
    public const string NotSlug = nameof(ErrorCodes.NotSlug);
    public const string NotVersion = nameof(ErrorCodes.NotVersion);
    public const string NotCreditCardNumber = nameof(ErrorCodes.NotCreditCardNumber);
    public const string NotEnumName = nameof(ErrorCodes.NotEnumName);
    public const string NotFileName = nameof(ErrorCodes.NotFileName);
    public const string CountEqualTo = nameof(ErrorCodes.CountEqualTo);
    public const string CountNotEqualTo = nameof(ErrorCodes.CountNotEqualTo);
    public const string CountGreaterThan = nameof(ErrorCodes.CountGreaterThan);
    public const string CountLessThan = nameof(ErrorCodes.CountLessThan);
    public const string CountOutOfRange = nameof(ErrorCodes.CountOutOfRange);
    public const string ContainsNull = nameof(ErrorCodes.ContainsNull);
    public const string HasDuplicates = nameof(ErrorCodes.HasDuplicates);
    public const string ContainsKey = nameof(ErrorCodes.ContainsKey);
    public const string NotContainsKey = nameof(ErrorCodes.NotContainsKey);
    public const string True = nameof(ErrorCodes.True);
    public const string False = nameof(ErrorCodes.False);
    public const string NotDefined = nameof(ErrorCodes.NotDefined);
    public const string HasFlag = nameof(ErrorCodes.HasFlag);
    public const string NotHasFlag = nameof(ErrorCodes.NotHasFlag);
    public const string Before = nameof(ErrorCodes.Before);
    public const string After = nameof(ErrorCodes.After);
    public const string InPast = nameof(ErrorCodes.InPast);
    public const string InFuture = nameof(ErrorCodes.InFuture);
    public const string NotUtc = nameof(ErrorCodes.NotUtc);
    public const string Weekend = nameof(ErrorCodes.Weekend);
    public const string Weekday = nameof(ErrorCodes.Weekday);
    public const string HasTimeOfDay = nameof(ErrorCodes.HasTimeOfDay);
    public const string OlderThan = nameof(ErrorCodes.OlderThan);
    public const string NewerThan = nameof(ErrorCodes.NewerThan);
    public const string Relative = nameof(ErrorCodes.Relative);
    public const string NotHttps = nameof(ErrorCodes.NotHttps);
    public const string NotHttpOrHttps = nameof(ErrorCodes.NotHttpOrHttps);
    public const string NotScheme = nameof(ErrorCodes.NotScheme);
    public const string Loopback = nameof(ErrorCodes.Loopback);
    public const string Private = nameof(ErrorCodes.Private);
    public const string NotIPv4 = nameof(ErrorCodes.NotIPv4);
    public const string NotIPv6 = nameof(ErrorCodes.NotIPv6);
    public const string NotReadable = nameof(ErrorCodes.NotReadable);
    public const string NotWritable = nameof(ErrorCodes.NotWritable);
    public const string NotSeekable = nameof(ErrorCodes.NotSeekable);
    public const string NotAssignableTo = nameof(ErrorCodes.NotAssignableTo);
    public const string Abstract = nameof(ErrorCodes.Abstract);
    public const string Interface = nameof(ErrorCodes.Interface);

    // Name, the types it applies to (for errors and docs).
    private static readonly Dictionary<string, string> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        [Null] = "any type",
        [NotNull] = "any type",
        [Default] = "any type",
        [EqualTo] = "any type with a text form",
        [NotEqualTo] = "any type with a text form",
        [OneOf] = "any type with a text form",
        [NotOneOf] = "any type with a text form",
        [Type] = "any type",
        [NotType] = "any type",
        [Empty] = "any type",
        [NotEmpty] = "any type",
        [GreaterThan] = "comparable types (numbers, dates, TimeSpan, string, Version, char, Guid...)",
        [GreaterThanOrEqualTo] = "comparable types",
        [LessThan] = "comparable types",
        [LessThanOrEqualTo] = "comparable types",
        [OutOfRange] = "comparable types",
        [OutOfRangeExclusive] = "comparable types",
        [InRange] = "comparable types",
        [Negative] = "comparable value types",
        [NegativeOrZero] = "comparable value types",
        [Positive] = "comparable value types",
        [PositiveOrZero] = "comparable value types",
        [Zero] = "comparable value types",
        [NaN] = "double, float (and Half on .NET 8+)",
        [Infinity] = "double, float (and Half on .NET 8+)",
        [NotFinite] = "double, float (and Half on .NET 8+)",
        [Odd] = "int, long (and every integer type on .NET 8+)",
        [Even] = "int, long (and every integer type on .NET 8+)",
        [NotMultipleOf] = "int, long, decimal (and every integer type on .NET 8+)",
        [NotPowerOfTwo] = "int, long (and every integer type on .NET 8+)",
        [NotInteger] = "double, float, decimal",
        [PrecisionScaleExceeded] = "decimal",
        [NullOrEmpty] = "string, collections",
        [WhiteSpace] = "string",
        [NullOrWhiteSpace] = "string",
        [LongerThan] = "string",
        [ShorterThan] = "string",
        [LengthNotEqualTo] = "string",
        [LengthOutOfRange] = "string",
        [Contains] = "string, collections",
        [NotContains] = "string, collections",
        [StartsWith] = "string",
        [NotStartsWith] = "string",
        [EndsWith] = "string",
        [NotEndsWith] = "string",
        [Matches] = "string",
        [NotMatches] = "string",
        [NotEmailAddress] = "string",
        [NotJson] = "string",
        [NotXml] = "string",
        [NotUri] = "string",
        [NotHttpUrl] = "string",
        [NotParsable] = "string (.NET 8+)",
        [AllUpperCase] = "string",
        [AllLowerCase] = "string",
        [NotTrimmed] = "string",
        [ContainsWhiteSpace] = "string",
        [ContainsControlCharacters] = "string",
        [ContainsAny] = "string",
        [ContainsAnyExcept] = "string",
        [NotAscii] = "string",
        [NotDigits] = "string",
        [NotLettersOrDigits] = "string",
        [NotHex] = "string",
        [NotBase64] = "string",
        [NotGuid] = "string",
        [NotIPAddress] = "string",
        [NotSlug] = "string",
        [NotVersion] = "string",
        [NotCreditCardNumber] = "string",
        [NotEnumName] = "string",
        [NotFileName] = "string",
        [CountEqualTo] = "collections",
        [CountNotEqualTo] = "collections",
        [CountGreaterThan] = "collections",
        [CountLessThan] = "collections",
        [CountOutOfRange] = "collections",
        [ContainsNull] = "collections",
        [HasDuplicates] = "collections",
        [ContainsKey] = "dictionaries",
        [NotContainsKey] = "dictionaries",
        [True] = "bool",
        [False] = "bool",
        [NotDefined] = "enums",
        [HasFlag] = "enums",
        [NotHasFlag] = "enums",
        [Before] = "DateTime, DateTimeOffset (and DateOnly, TimeOnly on .NET 8+)",
        [After] = "DateTime, DateTimeOffset (and DateOnly, TimeOnly on .NET 8+)",
        [InPast] = "DateTime, DateTimeOffset (and DateOnly on .NET 8+)",
        [InFuture] = "DateTime, DateTimeOffset (and DateOnly on .NET 8+)",
        [NotUtc] = "DateTime, DateTimeOffset",
        [Weekend] = "DateTime, DateTimeOffset (and DateOnly on .NET 8+)",
        [Weekday] = "DateTime, DateTimeOffset (and DateOnly on .NET 8+)",
        [HasTimeOfDay] = "DateTime, DateTimeOffset",
        [OlderThan] = "DateTime, DateTimeOffset",
        [NewerThan] = "DateTime, DateTimeOffset",
        [Relative] = "Uri",
        [NotHttps] = "Uri",
        [NotHttpOrHttps] = "Uri",
        [NotScheme] = "Uri",
        [Loopback] = "Uri, IPAddress",
        [Private] = "IPAddress",
        [NotIPv4] = "IPAddress",
        [NotIPv6] = "IPAddress",
        [NotReadable] = "Stream",
        [NotWritable] = "Stream",
        [NotSeekable] = "Stream",
        [NotAssignableTo] = "Type",
        [Abstract] = "Type",
        [Interface] = "Type",
    };

    // Check codes that cannot be described with text arguments (they need a lambda).
    private static readonly string[] CodeOnly =
        [nameof(ErrorCodes.Condition), nameof(ErrorCodes.Any), nameof(ErrorCodes.None), nameof(ErrorCodes.Must), nameof(ErrorCodes.Invalid)];

    public static IReadOnlyCollection<string> All => Catalog.Keys;

    // The canonical spelling of a built-in name, or null.
    public static string? Find(string name)
    {
        foreach (var key in Catalog.Keys)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    public static string AppliesTo(string name) => Catalog.TryGetValue(name, out var types) ? types : "nothing";

    public static bool IsCodeOnly(string name)
    {
        foreach (var code in CodeOnly)
        {
            if (string.Equals(code, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
