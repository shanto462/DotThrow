namespace DotThrow;

/// <summary>
/// The error codes that the built-in checks put on <see cref="ValidationFailure.ErrorCode"/>.
/// A code is the check name without the <c>If</c> prefix, so <c>IfLongerThan</c> reports
/// <see cref="LongerThan"/>. Codes never change between versions, so they are safe to
/// store, translate, or send to API clients.
/// </summary>
public static class ErrorCodes
{
    /// <summary>The value is <see langword="null"/>.</summary>
    public const string Null = nameof(Null);

    /// <summary>The value is not <see langword="null"/>.</summary>
    public const string NotNull = nameof(NotNull);

    /// <summary>The value is the default value of its type.</summary>
    public const string Default = nameof(Default);

    /// <summary>The value equals a value it must not equal.</summary>
    public const string EqualTo = nameof(EqualTo);

    /// <summary>The value does not equal the value it must equal.</summary>
    public const string NotEqualTo = nameof(NotEqualTo);

    /// <summary>The value is one of a set of rejected values.</summary>
    public const string OneOf = nameof(OneOf);

    /// <summary>The value is not one of a set of allowed values.</summary>
    public const string NotOneOf = nameof(NotOneOf);

    /// <summary>A custom condition rejected the value.</summary>
    public const string Condition = nameof(Condition);

    /// <summary>The value is of a rejected type.</summary>
    public const string Type = nameof(Type);

    /// <summary>The value is not of the required type.</summary>
    public const string NotType = nameof(NotType);

    /// <summary>The value is greater than the limit.</summary>
    public const string GreaterThan = nameof(GreaterThan);

    /// <summary>The value is greater than or equal to the limit.</summary>
    public const string GreaterThanOrEqualTo = nameof(GreaterThanOrEqualTo);

    /// <summary>The value is less than the limit.</summary>
    public const string LessThan = nameof(LessThan);

    /// <summary>The value is less than or equal to the limit.</summary>
    public const string LessThanOrEqualTo = nameof(LessThanOrEqualTo);

    /// <summary>The value is outside the allowed range.</summary>
    public const string OutOfRange = nameof(OutOfRange);

    /// <summary>The value is inside a rejected range.</summary>
    public const string InRange = nameof(InRange);

    /// <summary>The value is negative.</summary>
    public const string Negative = nameof(Negative);

    /// <summary>The value is negative or zero.</summary>
    public const string NegativeOrZero = nameof(NegativeOrZero);

    /// <summary>The value is positive.</summary>
    public const string Positive = nameof(Positive);

    /// <summary>The value is positive or zero.</summary>
    public const string PositiveOrZero = nameof(PositiveOrZero);

    /// <summary>The value is zero.</summary>
    public const string Zero = nameof(Zero);

    /// <summary>The value is not a number (NaN).</summary>
    public const string NaN = nameof(NaN);

    /// <summary>The value is positive or negative infinity.</summary>
    public const string Infinity = nameof(Infinity);

    /// <summary>The string, collection or GUID is empty.</summary>
    public const string Empty = nameof(Empty);

    /// <summary>The string is empty or contains only white-space characters.</summary>
    public const string WhiteSpace = nameof(WhiteSpace);

    /// <summary>The string is longer than allowed.</summary>
    public const string LongerThan = nameof(LongerThan);

    /// <summary>The string is shorter than allowed.</summary>
    public const string ShorterThan = nameof(ShorterThan);

    /// <summary>The string does not have the exact required length.</summary>
    public const string LengthNotEqualTo = nameof(LengthNotEqualTo);

    /// <summary>The string length is outside the allowed range.</summary>
    public const string LengthOutOfRange = nameof(LengthOutOfRange);

    /// <summary>The string or collection contains a rejected value.</summary>
    public const string Contains = nameof(Contains);

    /// <summary>The string or collection does not contain a required value.</summary>
    public const string NotContains = nameof(NotContains);

    /// <summary>The string starts with a rejected value.</summary>
    public const string StartsWith = nameof(StartsWith);

    /// <summary>The string does not start with the required value.</summary>
    public const string NotStartsWith = nameof(NotStartsWith);

    /// <summary>The string ends with a rejected value.</summary>
    public const string EndsWith = nameof(EndsWith);

    /// <summary>The string does not end with the required value.</summary>
    public const string NotEndsWith = nameof(NotEndsWith);

    /// <summary>The string matches a rejected pattern.</summary>
    public const string Matches = nameof(Matches);

    /// <summary>The string does not match the required pattern.</summary>
    public const string NotMatches = nameof(NotMatches);

    /// <summary>The string is not a valid email address.</summary>
    public const string NotEmailAddress = nameof(NotEmailAddress);

    /// <summary>The string is not valid JSON.</summary>
    public const string NotJson = nameof(NotJson);

    /// <summary>The string is not well-formed XML.</summary>
    public const string NotXml = nameof(NotXml);

    /// <summary>The string is not a valid URI.</summary>
    public const string NotUri = nameof(NotUri);

    /// <summary>The string is not an absolute HTTP or HTTPS URL.</summary>
    public const string NotHttpUrl = nameof(NotHttpUrl);

    /// <summary>The string cannot be parsed into the required type.</summary>
    public const string NotParsable = nameof(NotParsable);

    /// <summary>All letters in the string are uppercase.</summary>
    public const string AllUpperCase = nameof(AllUpperCase);

    /// <summary>All letters in the string are lowercase.</summary>
    public const string AllLowerCase = nameof(AllLowerCase);

    /// <summary>The collection has exactly the rejected number of items.</summary>
    public const string CountEqualTo = nameof(CountEqualTo);

    /// <summary>The collection does not have the required number of items.</summary>
    public const string CountNotEqualTo = nameof(CountNotEqualTo);

    /// <summary>The collection has more items than allowed.</summary>
    public const string CountGreaterThan = nameof(CountGreaterThan);

    /// <summary>The collection has fewer items than required.</summary>
    public const string CountLessThan = nameof(CountLessThan);

    /// <summary>The number of items is outside the allowed range.</summary>
    public const string CountOutOfRange = nameof(CountOutOfRange);

    /// <summary>The collection contains a <see langword="null"/> item.</summary>
    public const string ContainsNull = nameof(ContainsNull);

    /// <summary>The collection contains duplicate items.</summary>
    public const string HasDuplicates = nameof(HasDuplicates);

    /// <summary>The value is <see langword="true"/>.</summary>
    public const string True = nameof(True);

    /// <summary>The value is <see langword="false"/>.</summary>
    public const string False = nameof(False);

    /// <summary>The enum value is not a defined member.</summary>
    public const string NotDefined = nameof(NotDefined);

    /// <summary>The date or time is before the limit.</summary>
    public const string Before = nameof(Before);

    /// <summary>The date or time is after the limit.</summary>
    public const string After = nameof(After);

    /// <summary>The date or time is in the past.</summary>
    public const string InPast = nameof(InPast);

    /// <summary>The date or time is in the future.</summary>
    public const string InFuture = nameof(InFuture);

    /// <summary>A <see cref="Validator{T}.Must"/> or <see cref="Validator{T}.MustAsync"/> rule failed.</summary>
    public const string Must = nameof(Must);

    /// <summary>A failure added with <see cref="ValidationReport.Add(string, string, string?)"/> without an error code.</summary>
    public const string Invalid = nameof(Invalid);
}
