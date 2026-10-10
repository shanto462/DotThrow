using System;
using System.Globalization;

namespace DotThrow.Internal;

// Default messages. They are built only when a check fails, so the happy path never
// formats a string. Values in messages always use the invariant culture.
internal static class Messages
{
    public static string Null(string name) => $"'{name}' must not be null.";

    public static string NotNull(string name) => $"'{name}' must be null.";

    public static string Default(string name) => $"'{name}' must not be the default value.";

    public static string EqualTo<T>(string name, T other) => $"'{name}' must not be equal to {Format(other)}.";

    public static string NotEqualTo<T>(string name, T other) => $"'{name}' must be equal to {Format(other)}.";

    public static string OneOf(string name) => $"'{name}' must not be one of the rejected values.";

    public static string NotOneOf(string name) => $"'{name}' must be one of the allowed values.";

    public static string Condition(string name, string? expression) =>
        string.IsNullOrEmpty(expression) ? $"'{name}' is not valid." : $"'{name}' must not satisfy the condition: {expression}.";

    public static string NotCondition(string name, string? expression) =>
        string.IsNullOrEmpty(expression) ? $"'{name}' is not valid." : $"'{name}' must satisfy the condition: {expression}.";

    public static string Type(string name, Type type) => $"'{name}' must not be of type {type.Name}.";

    public static string NotType(string name, Type type) => $"'{name}' must be of type {type.Name}.";

    public static string GreaterThan<T>(string name, T limit) => $"'{name}' must be less than or equal to {Format(limit)}.";

    public static string GreaterThanOrEqualTo<T>(string name, T limit) => $"'{name}' must be less than {Format(limit)}.";

    public static string LessThan<T>(string name, T limit) => $"'{name}' must be greater than or equal to {Format(limit)}.";

    public static string LessThanOrEqualTo<T>(string name, T limit) => $"'{name}' must be greater than {Format(limit)}.";

    public static string OutOfRange<T>(string name, T min, T max) => $"'{name}' must be between {Format(min)} and {Format(max)}.";

    public static string InRange<T>(string name, T min, T max) => $"'{name}' must not be between {Format(min)} and {Format(max)}.";

    public static string Negative(string name) => $"'{name}' must not be negative.";

    public static string NegativeOrZero(string name) => $"'{name}' must be greater than zero.";

    public static string Positive(string name) => $"'{name}' must not be greater than zero.";

    public static string PositiveOrZero(string name) => $"'{name}' must be less than zero.";

    public static string Zero(string name) => $"'{name}' must not be zero.";

    public static string NaN(string name) => $"'{name}' must be a number, not NaN.";

    public static string Infinity(string name) => $"'{name}' must be a finite number.";

    public static string Empty(string name) => $"'{name}' must not be empty.";

    public static string WhiteSpace(string name) => $"'{name}' must not be empty or white space.";

    public static string EmptyGuid(string name) => $"'{name}' must not be an empty GUID.";

    public static string LongerThan(string name, int maxLength) => $"'{name}' must be at most {Format(maxLength)} characters long.";

    public static string ShorterThan(string name, int minLength) => $"'{name}' must be at least {Format(minLength)} characters long.";

    public static string LengthNotEqualTo(string name, int length) => $"'{name}' must be exactly {Format(length)} characters long.";

    public static string LengthOutOfRange(string name, int min, int max) =>
        $"'{name}' must be between {Format(min)} and {Format(max)} characters long.";

    public static string Contains(string name, string value) => $"'{name}' must not contain '{value}'.";

    public static string NotContains(string name, string value) => $"'{name}' must contain '{value}'.";

    public static string StartsWith(string name, string value) => $"'{name}' must not start with '{value}'.";

    public static string NotStartsWith(string name, string value) => $"'{name}' must start with '{value}'.";

    public static string EndsWith(string name, string value) => $"'{name}' must not end with '{value}'.";

    public static string NotEndsWith(string name, string value) => $"'{name}' must end with '{value}'.";

    public static string Matches(string name, string pattern) => $"'{name}' must not match the pattern '{pattern}'.";

    public static string NotMatches(string name, string pattern) => $"'{name}' must match the pattern '{pattern}'.";

    public static string NotEmailAddress(string name) => $"'{name}' must be a valid email address.";

    public static string NotJson(string name) => $"'{name}' must be valid JSON.";

    public static string NotXml(string name) => $"'{name}' must be well-formed XML.";

    public static string NotUri(string name, UriKind kind) => kind switch
    {
        UriKind.Absolute => $"'{name}' must be a valid absolute URI.",
        UriKind.Relative => $"'{name}' must be a valid relative URI.",
        _ => $"'{name}' must be a valid URI.",
    };

    public static string NotHttpUrl(string name) => $"'{name}' must be an absolute HTTP or HTTPS URL.";

    public static string NotParsable(string name, Type type) => $"'{name}' must be a valid {type.Name}.";

    public static string AllUpperCase(string name) => $"'{name}' must not be all uppercase.";

    public static string AllLowerCase(string name) => $"'{name}' must not be all lowercase.";

    public static string ContainsItem<T>(string name, T item) => $"'{name}' must not contain {Format(item)}.";

    public static string NotContainsItem<T>(string name, T item) => $"'{name}' must contain {Format(item)}.";

    public static string CountEqualTo(string name, int count) => $"'{name}' must not have exactly {Items(count)}.";

    public static string CountNotEqualTo(string name, int count) => $"'{name}' must have exactly {Items(count)}.";

    public static string CountGreaterThan(string name, int max) => $"'{name}' must have at most {Items(max)}.";

    public static string CountLessThan(string name, int min) => $"'{name}' must have at least {Items(min)}.";

    public static string CountOutOfRange(string name, int min, int max) =>
        $"'{name}' must have between {Format(min)} and {Format(max)} items.";

    public static string ContainsNull(string name) => $"'{name}' must not contain null items.";

    public static string Duplicates(string name) => $"'{name}' must not contain duplicate items.";

    public static string True(string name) => $"'{name}' must be false.";

    public static string False(string name) => $"'{name}' must be true.";

    public static string NotDefined(string name, Type enumType) => $"'{name}' must be a defined {enumType.Name} value.";

    public static string Before<T>(string name, T limit) => $"'{name}' must not be before {Format(limit)}.";

    public static string After<T>(string name, T limit) => $"'{name}' must not be after {Format(limit)}.";

    public static string InPast(string name) => $"'{name}' must not be in the past.";

    public static string InFuture(string name) => $"'{name}' must not be in the future.";

    private static string Items(int count) => count == 1 ? "1 item" : $"{Format(count)} items";

    public static string Format<T>(T value) => value switch
    {
        null => "null",
        string text => $"'{text}'",

        // "R" gives the shortest round-trip text on every runtime. Without it, .NET Framework
        // prints 15 digits (1.79769313486232E+308) and .NET Core prints 17.
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
#if NET8_0_OR_GREATER
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
#endif
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
