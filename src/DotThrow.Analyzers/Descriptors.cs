using Microsoft.CodeAnalysis;

namespace DotThrow.Analyzers;

/// <summary>The diagnostic IDs. Each one has a section in docs/analyzers.md.</summary>
internal static class DiagnosticIds
{
    public const string EmptyChain = "DT0001";
    public const string ManualGuard = "DT0002";
    public const string RedundantName = "DT0003";
    public const string AsyncRulesRunSynchronously = "DT0004";
    public const string InvalidPattern = "DT0005";
    public const string UseGeneratedRegex = "DT0006";
    public const string InvalidArgument = "DT0007";
    public const string UseCurrentTimeCheck = "DT0008";
    public const string NullCheckOnValueType = "DT0009";
    public const string UnusedValidationResult = "DT0010";
}

/// <summary>The diagnostic descriptors of every DotThrow rule.</summary>
internal static class Descriptors
{
    private const string Usage = "Usage";
    private const string Style = "Style";
    private const string Performance = "Performance";

    public static readonly DiagnosticDescriptor EmptyChain = Create(
        DiagnosticIds.EmptyChain,
        "Guard chain has no checks",
        "'{0}()' is not followed by a check, so nothing is checked",
        Usage,
        DiagnosticSeverity.Warning,
        "A Throw() or Check() call only starts a chain. Without a check such as IfNull() it checks nothing, and converting the guard back to its value passes the value through unchecked.");

    public static readonly DiagnosticDescriptor ManualGuard = Create(
        DiagnosticIds.ManualGuard,
        "Use a DotThrow check instead of a manual guard",
        "Use '{0}' instead of this manual guard",
        Style,
        DiagnosticSeverity.Info,
        "This if statement throws an argument exception that a DotThrow check also throws, with the parameter name filled in and the same message rules as every other check.");

    public static readonly DiagnosticDescriptor RedundantName = Create(
        DiagnosticIds.RedundantName,
        "Remove the name argument that the compiler fills in",
        "The compiler already fills in '{0}' for this parameter, so the argument can be removed",
        Style,
        DiagnosticSeverity.Info,
        "Throw() and Check() use [CallerArgumentExpression], so the compiler passes the text of the checked expression as the name. Passing the same text again by hand adds noise and can go stale when the variable is renamed.");

    public static readonly DiagnosticDescriptor AsyncRulesRunSynchronously = Create(
        DiagnosticIds.AsyncRulesRunSynchronously,
        "Validator with async rules is run synchronously",
        "The validator has an async rule ('{0}'), so '{1}' can throw AsyncRuleInvokedSynchronouslyException; call '{2}' instead",
        Usage,
        DiagnosticSeverity.Warning,
        "A validator with MustAsync, CustomAsync, WhenAsync, UnlessAsync or WhereAsync rules must be run with ValidateAsync, ValidateAndThrowAsync or TestValidateAsync. The synchronous methods throw AsyncRuleInvokedSynchronouslyException when they reach an async rule.");

    public static readonly DiagnosticDescriptor InvalidPattern = Create(
        DiagnosticIds.InvalidPattern,
        "Regular expression pattern is not valid",
        "The pattern is not a valid regular expression, so this check always throws: {0}",
        Usage,
        DiagnosticSeverity.Error,
        "IfMatches and IfNotMatches build a Regex from the pattern on every call. A pattern that does not parse throws an ArgumentException every time the check runs.");

    public static readonly DiagnosticDescriptor UseGeneratedRegex = Create(
        DiagnosticIds.UseGeneratedRegex,
        "Use a generated regular expression for a constant pattern",
        "Use a [GeneratedRegex] method for this constant pattern",
        Performance,
        DiagnosticSeverity.Info,
        "A constant pattern can be compiled at build time by the regular expression source generator (.NET 7 and later). The generated Regex is faster, needs no warm-up and works well with trimming and Native AOT. Pass it to the IfMatches(Regex) or IfNotMatches(Regex) overload.");

    public static readonly DiagnosticDescriptor InvalidArgument = Create(
        DiagnosticIds.InvalidArgument,
        "Check argument is never valid",
        "{0}",
        Usage,
        DiagnosticSeverity.Error,
        "A constant argument makes this check throw on every call, or makes every value fail, for example a negative length or a range whose minimum is greater than its maximum.");

    public static readonly DiagnosticDescriptor UseCurrentTimeCheck = Create(
        DiagnosticIds.UseCurrentTimeCheck,
        "Use IfInPast or IfInFuture to compare with the current time",
        "Use '{0}' instead of '{1}' with the current time",
        Style,
        DiagnosticSeverity.Info,
        "IfInPast() and IfInFuture() compare with the current time, read the clock only when the check runs, take a TimeProvider on .NET 8 and later, and have clearer default messages.");

    public static readonly DiagnosticDescriptor NullCheckOnValueType = Create(
        DiagnosticIds.NullCheckOnValueType,
        "Null check on a value that cannot be null",
        "'{0}' {1}, because '{2}' is a non-nullable value type",
        Usage,
        DiagnosticSeverity.Warning,
        "A non-nullable value type is never null, so IfNull() never fails, AllowNull() does nothing and IfNotNull() always fails. A check such as IfDefault() or IfEmpty() is often what was meant.");

    public static readonly DiagnosticDescriptor UnusedValidationResult = Create(
        DiagnosticIds.UnusedValidationResult,
        "Validation result is not used",
        "The result of '{0}' is not used, so validation failures are ignored",
        Usage,
        DiagnosticSeverity.Warning,
        "Validate and ValidateAsync return a report and do not throw. Read the report, or call ValidateAndThrow or ValidateAndThrowAsync. A task that is not awaited hides its failures too.");

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string message,
        string category,
        DiagnosticSeverity severity,
        string description) =>
        new(
            id,
            title,
            message,
            category,
            severity,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: "https://github.com/shanto462/DotThrow/blob/main/docs/analyzers.md#dt" + id.Substring(2));
}
