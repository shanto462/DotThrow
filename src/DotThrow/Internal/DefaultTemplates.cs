using System;
using System.Collections.Generic;

namespace DotThrow.Internal;

// The English default templates, keyed by error code. A few codes have variants, keyed
// "Code.Variant", for wording that depends on the arguments (a count of exactly one, a
// relative URI, a comparison value that may be shown). Lookup tries the variant first and
// then the plain code, at every level of the culture fallback chain.
//
// Rules for every template, here and in translations:
// - Never use {PropertyValue}: a default message must not contain the checked value.
// - A string comparand is shown only when a developer passes it as a pattern or a
//   substring. Comparands that may be secrets (equality, collection items, keys) are
//   shown only through the ".Value" variants, which the checks use for value types only.
internal static class DefaultTemplates
{
    public static readonly IReadOnlyDictionary<string, string> All = Create();

    private static Dictionary<string, string> Create() => new(StringComparer.Ordinal)
    {
        // General
        [ErrorCodes.Null] = "'{PropertyName}' must not be null.",
        [ErrorCodes.NotNull] = "'{PropertyName}' must be null.",
        [ErrorCodes.Default] = "'{PropertyName}' must not be the default value.",
        [ErrorCodes.Empty] = "'{PropertyName}' must not be empty.",
        [TemplateKeys.EmptyGuid] = "'{PropertyName}' must not be an empty GUID.",
        [ErrorCodes.NotEmpty] = "'{PropertyName}' must be empty.",
        [ErrorCodes.EqualTo] = "'{PropertyName}' must not be equal to the rejected value.",
        [TemplateKeys.EqualToValue] = "'{PropertyName}' must not be equal to {ComparisonValue}.",
        [ErrorCodes.NotEqualTo] = "'{PropertyName}' must be equal to the required value.",
        [TemplateKeys.NotEqualToValue] = "'{PropertyName}' must be equal to {ComparisonValue}.",
        [ErrorCodes.OneOf] = "'{PropertyName}' must not be one of the rejected values.",
        [ErrorCodes.NotOneOf] = "'{PropertyName}' must be one of the allowed values.",
        [ErrorCodes.Condition] = "'{PropertyName}' is not valid.",
        [TemplateKeys.ConditionIf] = "'{PropertyName}' must not satisfy the condition: {Condition}.",
        [TemplateKeys.ConditionIfNot] = "'{PropertyName}' must satisfy the condition: {Condition}.",
        [ErrorCodes.Type] = "'{PropertyName}' must not be of type {Type}.",
        [ErrorCodes.NotType] = "'{PropertyName}' must be of type {Type}.",
        [ErrorCodes.True] = "'{PropertyName}' must be false.",
        [ErrorCodes.False] = "'{PropertyName}' must be true.",
        [ErrorCodes.Must] = "'{PropertyName}' is not valid.",
        [ErrorCodes.Invalid] = "'{PropertyName}' is not valid.",

        // Comparisons and numbers
        [ErrorCodes.GreaterThan] = "'{PropertyName}' must be less than or equal to {ComparisonValue}.",
        [ErrorCodes.GreaterThanOrEqualTo] = "'{PropertyName}' must be less than {ComparisonValue}.",
        [ErrorCodes.LessThan] = "'{PropertyName}' must be greater than or equal to {ComparisonValue}.",
        [ErrorCodes.LessThanOrEqualTo] = "'{PropertyName}' must be greater than {ComparisonValue}.",
        [ErrorCodes.OutOfRange] = "'{PropertyName}' must be between {From} and {To}.",
        [ErrorCodes.OutOfRangeExclusive] = "'{PropertyName}' must be between {From} and {To} (exclusive).",
        [ErrorCodes.InRange] = "'{PropertyName}' must not be between {From} and {To}.",
        [ErrorCodes.Negative] = "'{PropertyName}' must not be negative.",
        [ErrorCodes.NegativeOrZero] = "'{PropertyName}' must be greater than zero.",
        [ErrorCodes.Positive] = "'{PropertyName}' must not be greater than zero.",
        [ErrorCodes.PositiveOrZero] = "'{PropertyName}' must be less than zero.",
        [ErrorCodes.Zero] = "'{PropertyName}' must not be zero.",
        [ErrorCodes.NaN] = "'{PropertyName}' must be a number, not NaN.",
        [ErrorCodes.Infinity] = "'{PropertyName}' must be a finite number.",
        [ErrorCodes.NotFinite] = "'{PropertyName}' must be a finite number.",
        [ErrorCodes.Odd] = "'{PropertyName}' must be an even number.",
        [ErrorCodes.Even] = "'{PropertyName}' must be an odd number.",
        [ErrorCodes.NotMultipleOf] = "'{PropertyName}' must be a multiple of {Divisor}.",
        [ErrorCodes.NotPowerOfTwo] = "'{PropertyName}' must be a power of two.",
        [ErrorCodes.NotInteger] = "'{PropertyName}' must be a whole number.",
        [ErrorCodes.PrecisionScaleExceeded] = "'{PropertyName}' must have at most {ExpectedPrecision} digits in total, with at most {ExpectedScale} decimals. Found {Digits} digits before the decimal point and {ActualScale} decimals.",

        // Strings
        [ErrorCodes.WhiteSpace] = "'{PropertyName}' must not be empty or white space.",
        [ErrorCodes.LongerThan] = "'{PropertyName}' must be at most {MaxLength} characters long.",
        [ErrorCodes.ShorterThan] = "'{PropertyName}' must be at least {MinLength} characters long.",
        [ErrorCodes.LengthNotEqualTo] = "'{PropertyName}' must be exactly {Length} characters long.",
        [ErrorCodes.LengthOutOfRange] = "'{PropertyName}' must be between {MinLength} and {MaxLength} characters long.",
        [ErrorCodes.Contains] = "'{PropertyName}' must not contain the rejected item.",
        [TemplateKeys.ContainsValue] = "'{PropertyName}' must not contain {Value}.",
        [ErrorCodes.NotContains] = "'{PropertyName}' must contain the required item.",
        [TemplateKeys.NotContainsValue] = "'{PropertyName}' must contain {Value}.",
        [ErrorCodes.StartsWith] = "'{PropertyName}' must not start with {Value}.",
        [ErrorCodes.NotStartsWith] = "'{PropertyName}' must start with {Value}.",
        [ErrorCodes.EndsWith] = "'{PropertyName}' must not end with {Value}.",
        [ErrorCodes.NotEndsWith] = "'{PropertyName}' must end with {Value}.",
        [ErrorCodes.Matches] = "'{PropertyName}' must not match the pattern {Pattern}.",
        [ErrorCodes.NotMatches] = "'{PropertyName}' must match the pattern {Pattern}.",
        [ErrorCodes.NotEmailAddress] = "'{PropertyName}' must be a valid email address.",
        [ErrorCodes.NotJson] = "'{PropertyName}' must be valid JSON.",
        [ErrorCodes.NotXml] = "'{PropertyName}' must be well-formed XML.",
        [ErrorCodes.NotUri] = "'{PropertyName}' must be a valid URI.",
        [TemplateKeys.NotUriAbsolute] = "'{PropertyName}' must be a valid absolute URI.",
        [TemplateKeys.NotUriRelative] = "'{PropertyName}' must be a valid relative URI.",
        [ErrorCodes.NotHttpUrl] = "'{PropertyName}' must be an absolute HTTP or HTTPS URL.",
        [ErrorCodes.NotParsable] = "'{PropertyName}' must be a valid {Type}.",
        [ErrorCodes.AllUpperCase] = "'{PropertyName}' must not be all uppercase.",
        [ErrorCodes.AllLowerCase] = "'{PropertyName}' must not be all lowercase.",
        [ErrorCodes.NotTrimmed] = "'{PropertyName}' must not start or end with white space.",
        [ErrorCodes.ContainsWhiteSpace] = "'{PropertyName}' must not contain white space.",
        [ErrorCodes.ContainsControlCharacters] = "'{PropertyName}' must not contain control characters.",
        [ErrorCodes.ContainsAny] = "'{PropertyName}' must not contain any of the rejected characters.",
        [ErrorCodes.ContainsAnyExcept] = "'{PropertyName}' must contain only the allowed characters.",
        [ErrorCodes.NotAscii] = "'{PropertyName}' must contain only ASCII characters.",
        [ErrorCodes.NotDigits] = "'{PropertyName}' must contain only the digits 0 to 9.",
        [ErrorCodes.NotLettersOrDigits] = "'{PropertyName}' must contain only letters or digits.",
        [ErrorCodes.NotHex] = "'{PropertyName}' must be a hexadecimal string.",
        [ErrorCodes.NotBase64] = "'{PropertyName}' must be a valid Base64 string.",
        [ErrorCodes.NotGuid] = "'{PropertyName}' must be a valid GUID.",
        [ErrorCodes.NotIPAddress] = "'{PropertyName}' must be a valid IP address.",
        [ErrorCodes.NotSlug] = "'{PropertyName}' must be a slug of lowercase letters, digits and single hyphens.",
        [ErrorCodes.NotVersion] = "'{PropertyName}' must be a valid version number.",
        [ErrorCodes.NotCreditCardNumber] = "'{PropertyName}' must be a valid credit card number.",
        [ErrorCodes.NotEnumName] = "'{PropertyName}' must be a defined {Type} name.",
        [ErrorCodes.NotFileName] = "'{PropertyName}' must be a valid file name.",

        // Collections and dictionaries
        [ErrorCodes.CountEqualTo] = "'{PropertyName}' must not have exactly {Count} items.",
        [TemplateKeys.CountEqualToOne] = "'{PropertyName}' must not have exactly 1 item.",
        [ErrorCodes.CountNotEqualTo] = "'{PropertyName}' must have exactly {Count} items.",
        [TemplateKeys.CountNotEqualToOne] = "'{PropertyName}' must have exactly 1 item.",
        [ErrorCodes.CountGreaterThan] = "'{PropertyName}' must have at most {MaxCount} items.",
        [TemplateKeys.CountGreaterThanOne] = "'{PropertyName}' must have at most 1 item.",
        [ErrorCodes.CountLessThan] = "'{PropertyName}' must have at least {MinCount} items.",
        [TemplateKeys.CountLessThanOne] = "'{PropertyName}' must have at least 1 item.",
        [ErrorCodes.CountOutOfRange] = "'{PropertyName}' must have between {MinCount} and {MaxCount} items.",
        [ErrorCodes.ContainsNull] = "'{PropertyName}' must not contain null items.",
        [ErrorCodes.HasDuplicates] = "'{PropertyName}' must not contain duplicate items.",
        [ErrorCodes.Any] = "'{PropertyName}' must not contain any item that matches the condition.",
        [ErrorCodes.None] = "'{PropertyName}' must contain at least one item that matches the condition.",
        [ErrorCodes.ContainsKey] = "'{PropertyName}' must not contain the rejected key.",
        [TemplateKeys.ContainsKeyValue] = "'{PropertyName}' must not contain the key {Key}.",
        [ErrorCodes.NotContainsKey] = "'{PropertyName}' must contain the required key.",
        [TemplateKeys.NotContainsKeyValue] = "'{PropertyName}' must contain the key {Key}.",

        // Enums
        [ErrorCodes.NotDefined] = "'{PropertyName}' must be a defined {Type} value.",
        [ErrorCodes.HasFlag] = "'{PropertyName}' must not have the flag {Flag}.",
        [ErrorCodes.NotHasFlag] = "'{PropertyName}' must have the flag {Flag}.",

        // Dates and times
        [ErrorCodes.Before] = "'{PropertyName}' must not be before {ComparisonValue}.",
        [ErrorCodes.After] = "'{PropertyName}' must not be after {ComparisonValue}.",
        [ErrorCodes.InPast] = "'{PropertyName}' must not be in the past.",
        [ErrorCodes.InFuture] = "'{PropertyName}' must not be in the future.",
        [ErrorCodes.NotUtc] = "'{PropertyName}' must be in UTC.",
        [ErrorCodes.Weekend] = "'{PropertyName}' must be a weekday.",
        [ErrorCodes.Weekday] = "'{PropertyName}' must be a weekend day.",
        [ErrorCodes.HasTimeOfDay] = "'{PropertyName}' must not have a time of day.",
        [ErrorCodes.OlderThan] = "'{PropertyName}' must not be more than {MaxAge} in the past.",
        [ErrorCodes.NewerThan] = "'{PropertyName}' must be at least {MinAge} in the past.",

        // Uri and IPAddress
        [ErrorCodes.Relative] = "'{PropertyName}' must be an absolute URI.",
        [ErrorCodes.NotHttps] = "'{PropertyName}' must be an absolute HTTPS URL.",
        [ErrorCodes.NotHttpOrHttps] = "'{PropertyName}' must be an absolute HTTP or HTTPS URL.",
        [ErrorCodes.NotScheme] = "'{PropertyName}' must use one of these URI schemes: {Schemes}.",
        [ErrorCodes.Loopback] = "'{PropertyName}' must not be a loopback address.",
        [ErrorCodes.Private] = "'{PropertyName}' must not be a private or link-local address.",
        [ErrorCodes.NotIPv4] = "'{PropertyName}' must be an IPv4 address.",
        [ErrorCodes.NotIPv6] = "'{PropertyName}' must be an IPv6 address.",

        // Streams and types
        [ErrorCodes.NotReadable] = "'{PropertyName}' must be readable.",
        [ErrorCodes.NotWritable] = "'{PropertyName}' must be writable.",
        [ErrorCodes.NotSeekable] = "'{PropertyName}' must be seekable.",
        [ErrorCodes.NotAssignableTo] = "'{PropertyName}' must be assignable to {Type}.",
        [ErrorCodes.Abstract] = "'{PropertyName}' must not be an abstract type.",
        [ErrorCodes.Interface] = "'{PropertyName}' must not be an interface.",
    };
}

// Keys of the template variants. A variant key is the error code, a dot, and a name.
internal static class TemplateKeys
{
    public const string EmptyGuid = "Empty.Guid";
    public const string EqualToValue = "EqualTo.Value";
    public const string NotEqualToValue = "NotEqualTo.Value";
    public const string ConditionIf = "Condition.If";
    public const string ConditionIfNot = "Condition.IfNot";
    public const string ContainsValue = "Contains.Value";
    public const string NotContainsValue = "NotContains.Value";
    public const string NotUriAbsolute = "NotUri.Absolute";
    public const string NotUriRelative = "NotUri.Relative";
    public const string CountEqualToOne = "CountEqualTo.One";
    public const string CountNotEqualToOne = "CountNotEqualTo.One";
    public const string CountGreaterThanOne = "CountGreaterThan.One";
    public const string CountLessThanOne = "CountLessThan.One";
    public const string ContainsKeyValue = "ContainsKey.Value";
    public const string NotContainsKeyValue = "NotContainsKey.Value";

    // "Code.Variant" -> "Code". A key without a dot is returned as is.
    public static string CodeOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot < 0 ? key : key.Substring(0, dot);
    }
}
