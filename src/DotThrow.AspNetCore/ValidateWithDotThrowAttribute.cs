using System;
using System.Collections.Generic;
using DotThrow.AspNetCore.Internal;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotThrow.AspNetCore;

/// <summary>
/// Validates the arguments of MVC actions with DotThrow: every argument whose type has an
/// <see cref="IValidator{T}"/> registered is validated, and each failure becomes a
/// <c>ModelState</c> error named by its property path. In <c>[ApiController]</c> controllers the
/// action does not run and the response is a 400 validation problem (the app's
/// <c>InvalidModelStateResponseFactory</c>); other actions run and check <c>ModelState.IsValid</c>.
/// </summary>
/// <remarks>
/// Put it on a controller or an action, or add it to every action with <c>AddDotThrowValidation()</c>.
/// Only the closest one runs: an action attribute replaces the controller attribute, which replaces
/// the global filter. Arguments from services and special arguments (such as a
/// <see cref="System.Threading.CancellationToken"/>) are not validated.
/// </remarks>
/// <example>
/// <code>
/// [ApiController]
/// [Route("orders")]
/// [ValidateWithDotThrow]
/// public sealed class OrdersController : ControllerBase
/// {
///     [HttpPost]
///     [ValidateWithDotThrow("checkout")]
///     public IActionResult Create(Order order) =&gt; Ok();
///
///     [HttpPost("draft")]
///     [ValidateWithDotThrow(Skip = true)]
///     public IActionResult SaveDraft(Order order) =&gt; Ok();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ValidateWithDotThrowAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    private readonly string[] _groups;

    /// <summary>Initializes a new instance of the <see cref="ValidateWithDotThrowAttribute"/> class.</summary>
    /// <param name="groups">
    /// The groups to run. With no groups, the groups of <see cref="MvcValidationOptions.Groups"/> run,
    /// or the rules in <see cref="ValidationGroups.Default"/>.
    /// </param>
    public ValidateWithDotThrowAttribute(params string[] groups)
    {
        _groups = groups ?? [];
    }

    /// <summary>Gets the groups to run. Empty means the default groups.</summary>
    public IReadOnlyList<string> Groups => _groups;

    /// <summary>
    /// Gets or sets a value indicating whether validation is turned off for the controller or action,
    /// for example to opt one action out of the global filter. The default is <see langword="false"/>.
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Gets or sets the filter order. The default is 0, which runs after model binding and after the
    /// <c>[ApiController]</c> model state check (order -2000).
    /// </summary>
    public int Order { get; set; }

    /// <inheritdoc/>
    public bool IsReusable => true;

    /// <inheritdoc/>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new DotThrowValidationFilter(_groups.Length == 0 ? null : _groups, Skip, Order);
}
