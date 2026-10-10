using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;

namespace DotThrow.AspNetCore.Tests;

// Every action answers "valid" or "invalid" from ModelState, so tests see what the action saw.
[ApiController]
[Route("mvc/api")]
public sealed class SignUpApiController : ControllerBase
{
    [HttpPost("sign-up")]
    public IActionResult Register(SignUp request) => Ok(State());

    [HttpPost("adult")]
    [ValidateWithDotThrow("adult")]
    public IActionResult Adult(SignUp request) => Ok(State());

    [HttpPost("skip")]
    [ValidateWithDotThrow(Skip = true)]
    public IActionResult Skip(SignUp request) => Ok(State());

    [HttpGet("filter")]
    public IActionResult Filter([FromQuery(Name = "filter")] Filter filter) => Ok(State());

    [HttpPost("services")]
    public IActionResult Services(SignUp request, [FromServices] IValidator<SignUp> validator, CancellationToken cancellationToken) => Ok(State());

    // Only IValidator<BaseItem> is registered; the declared type Item has none, so nothing runs.
    [HttpPost("item")]
    public IActionResult Item(Item item) => Ok(State());

    [HttpPost("optional")]
    public IActionResult Optional([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] SignUp? request) => Ok(request is null ? "none" : State());

    private string State() => ModelState.IsValid ? "valid" : "invalid";
}

// Validated through the attribute, also when the global filter is not added.
[ApiController]
[Route("mvc/attribute")]
[ValidateWithDotThrow]
public sealed class AttributeController : ControllerBase
{
    [HttpPost]
    public IActionResult Post(SignUp request) => Ok(ModelState.IsValid ? "valid" : "invalid");
}

// Not an API controller: the action runs and reads ModelState itself.
[Route("mvc/form")]
public sealed class FormController : Controller
{
    [HttpPost]
    public IActionResult Post([FromBody] SignUp request) => Content(ModelState.IsValid ? "valid" : ErrorKeys(ModelState));

    internal static string ErrorKeys(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary state) =>
        string.Join(",", state.Where(entry => entry.Value!.Errors.Count > 0).Select(entry => entry.Key + "=" + entry.Value!.Errors.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
