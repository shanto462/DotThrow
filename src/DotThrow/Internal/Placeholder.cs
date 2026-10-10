namespace DotThrow.Internal;

// The placeholders that messages and templates know by name. Templates are parsed into
// these ids once, so rendering compares small numbers instead of strings.
internal enum Placeholder : byte
{
    // A literal piece of text.
    None,

    // A name in braces that DotThrow does not know. It stays in the message as written,
    // unless MessageTemplates.Format gets a value for it.
    Custom,

    PropertyName,
    PropertyValue,
    ComparisonValue,
    From,
    To,
    MinLength,
    MaxLength,
    TotalLength,
    Length,
    ExpectedPrecision,
    ExpectedScale,
    Digits,
    ActualScale,
    Count,
    MinCount,
    MaxCount,
    Type,
    Pattern,
    Value,
    Condition,
    Divisor,
    MaxAge,
    MinAge,
    Key,
    Flag,
    Schemes,
    Characters,
}

internal static class Placeholders
{
    public static Placeholder Find(string name) => name switch
    {
        nameof(Placeholder.PropertyName) => Placeholder.PropertyName,
        nameof(Placeholder.PropertyValue) => Placeholder.PropertyValue,
        nameof(Placeholder.ComparisonValue) => Placeholder.ComparisonValue,
        nameof(Placeholder.From) => Placeholder.From,
        nameof(Placeholder.To) => Placeholder.To,
        nameof(Placeholder.MinLength) => Placeholder.MinLength,
        nameof(Placeholder.MaxLength) => Placeholder.MaxLength,
        nameof(Placeholder.TotalLength) => Placeholder.TotalLength,
        nameof(Placeholder.Length) => Placeholder.Length,
        nameof(Placeholder.ExpectedPrecision) => Placeholder.ExpectedPrecision,
        nameof(Placeholder.ExpectedScale) => Placeholder.ExpectedScale,
        nameof(Placeholder.Digits) => Placeholder.Digits,
        nameof(Placeholder.ActualScale) => Placeholder.ActualScale,
        nameof(Placeholder.Count) => Placeholder.Count,
        nameof(Placeholder.MinCount) => Placeholder.MinCount,
        nameof(Placeholder.MaxCount) => Placeholder.MaxCount,
        nameof(Placeholder.Type) => Placeholder.Type,
        nameof(Placeholder.Pattern) => Placeholder.Pattern,
        nameof(Placeholder.Value) => Placeholder.Value,
        nameof(Placeholder.Condition) => Placeholder.Condition,
        nameof(Placeholder.Divisor) => Placeholder.Divisor,
        nameof(Placeholder.MaxAge) => Placeholder.MaxAge,
        nameof(Placeholder.MinAge) => Placeholder.MinAge,
        nameof(Placeholder.Key) => Placeholder.Key,
        nameof(Placeholder.Flag) => Placeholder.Flag,
        nameof(Placeholder.Schemes) => Placeholder.Schemes,
        nameof(Placeholder.Characters) => Placeholder.Characters,
        _ => Placeholder.Custom,
    };

    // Text placeholders are names and descriptions (a type name, the source text of a
    // condition, a list of schemes), so they are written as is. Every other placeholder
    // holds a value and is formatted like one: strings in single quotes, numbers and dates
    // with the invariant culture.
    public static bool IsText(Placeholder id) =>
        id is Placeholder.PropertyName or Placeholder.Type or Placeholder.Condition or Placeholder.Schemes or Placeholder.Characters;
}
