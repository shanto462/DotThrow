namespace DotThrow;

/// <summary>How serious a <see cref="ValidationFailure"/> is.</summary>
/// <remarks>
/// Every failure makes <see cref="ValidationReport.IsValid"/> <see langword="false"/>,
/// whatever its severity (the same as FluentValidation). Filter on
/// <see cref="ValidationFailure.Severity"/> to treat warnings differently.
/// </remarks>
public enum Severity
{
    /// <summary>An error. The default.</summary>
    Error = 0,

    /// <summary>A warning.</summary>
    Warning = 1,

    /// <summary>Information.</summary>
    Info = 2,
}
