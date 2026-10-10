using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotThrow.AspNetCore;

/// <summary>
/// Settings of the DotThrow MVC action filter, set with <c>AddDotThrowValidation(o =&gt; ...)</c>.
/// They apply to the global filter and to <see cref="ValidateWithDotThrowAttribute"/>.
/// </summary>
public sealed class MvcValidationOptions
{
    /// <summary>
    /// Gets or sets the groups to run when the attribute names none, or <see langword="null"/> for
    /// the rules in <see cref="ValidationGroups.Default"/>.
    /// </summary>
    public IReadOnlyList<string>? Groups { get; set; }

    /// <summary>
    /// Gets or sets a callback that adjusts the options of each run, for example to pass data from
    /// the request to the rules through <see cref="ValidationOptions.Items"/>. It gets new options
    /// (with the groups already set) for every validated argument.
    /// </summary>
    public Action<ValidationOptions, ActionExecutingContext>? ConfigureValidation { get; set; }
}
