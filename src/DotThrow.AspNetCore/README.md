# DotThrow.AspNetCore

ASP.NET Core support for [DotThrow](https://github.com/shanto462/DotThrow) validators:

- Minimal APIs: `.WithValidation<T>()` on an endpoint or a route group. Works with Native AOT and
  the request delegate generator.
- MVC: a global action filter (`AddDotThrowValidation()`) and a `[ValidateWithDotThrow]` attribute.
- `ValidationReport` to RFC 7807 validation problem helpers.

Targets .NET 8, .NET 9 and .NET 10. It depends on `DotThrow` and the ASP.NET Core shared framework.
Register validators with [DotThrow.DependencyInjection](https://www.nuget.org/packages/DotThrow.DependencyInjection).

## Install

```shell
dotnet add package DotThrow.AspNetCore
dotnet add package DotThrow.DependencyInjection
```

## Minimal APIs

```csharp
using DotThrow;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValidator<SignUp, SignUpValidator>();

var app = builder.Build();

app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok())
    .WithValidation<SignUp>();

app.Run();

public sealed record SignUp(string? Name, string? Email);

public sealed class SignUpValidator : Validator<SignUp>
{
    public SignUpValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace());
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
    }
}
```

An invalid request never reaches the handler. The answer is a 400 `application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["'Email' must be a valid email address."]
  }
}
```

The filter finds the handler parameters of type `T` once, when the endpoints are built. A parameter
whose type derives from `T` is validated too. Parameters from services (`[FromServices]`,
`[FromKeyedServices]`, or a registered service type without a binding attribute) are never
validated, and an optional argument that the request did not send is skipped. When several
arguments are validated, the answer has all their failures, named after the parameter
(`"first.Value"`, `"second.Value"`).

Setup mistakes (a handler without a `T` parameter from the request, no `IValidator<T>`
registered, JSON options that cannot write a validation problem) never break other endpoints:
ASP.NET Core builds the endpoints at the first request, the mistake is logged as an error, and only
that endpoint answers 500. To stop the app at startup instead, with every mistake listed:

```csharp
builder.Services.ValidateEndpointsOnStart();
```

More forms:

```csharp
// Groups.
app.MapPost("/orders", (Order order) => TypedResults.Ok()).WithValidation<Order>("checkout");

// A validator object, no registration needed.
app.MapPost("/notes", (Note note) => TypedResults.Ok()).WithValidation(new NoteValidator());

// Every endpoint of a group that has an Order parameter. Other endpoints are not changed.
var orders = app.MapGroup("/orders").WithValidation<Order>();

// Per-request options and a custom response.
app.MapPost("/admin/users", (CreateUser user) => TypedResults.Ok()).WithValidation<CreateUser>(o =>
{
    o.Groups = ["admin"];
    o.ConfigureValidation = (options, http) => options.Items["user"] = http.User.Identity?.Name;
    o.ResultFactory = (report, http) => report.ToValidationProblem(new ValidationProblemOptions { IncludeErrorCodes = true });
});
```

Validated endpoints also describe the 400 response for OpenAPI.

## MVC

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddControllers().AddDotThrowValidation();
```

Every action argument whose type has an `IValidator<T>` registered is validated before the action
runs, and each failure becomes a `ModelState` error named by its property path.

- `[ApiController]` actions do not run: the answer is the app's usual invalid model state response,
  a 400 `ValidationProblemDetails` by default.
- Other actions run and check `ModelState.IsValid` themselves, as with data annotations.

Without the global filter, put `[ValidateWithDotThrow]` on a controller or an action. The closest
setting wins, so an attribute can change the groups or turn validation off for one action:

```csharp
[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    [ValidateWithDotThrow("checkout")]
    public IActionResult Create(Order order) => Ok();

    [HttpPost("draft")]
    [ValidateWithDotThrow(Skip = true)]
    public IActionResult SaveDraft(Order order) => Ok();
}
```

`AddDotThrowValidation(o => ...)` sets the default groups and a per-request `ConfigureValidation`.

## Error codes and severities

```csharp
builder.Services.Configure<ValidationProblemOptions>(o =>
{
    o.IncludeErrorCodes = true;
    o.IncludeSeverities = true;
});
```

Both filters then add two extensions, keyed and ordered like `errors` (keys follow the JSON
options' `DictionaryKeyPolicy`, the same as `errors`):

```json
"errorCodes": { "Email": ["NotEmailAddress"] },
"severities": { "Email": ["Error"] }
```

The value that failed (`ValidationFailure.AttemptedValue`) is never written to a response, because
it may be a password or a token.

## Helpers

```csharp
var report = await validator.ValidateAsync(request);
if (!report.IsValid)
{
    return report.ToValidationProblem();           // minimal APIs: TypedResults.ValidationProblem
}

HttpValidationProblemDetails details = report.ToProblemDetails();       // minimal APIs, IProblemDetailsService
ValidationProblemDetails mvcDetails = report.ToValidationProblemDetails(); // MVC: BadRequest(mvcDetails)
```

## Native AOT

- `.WithValidation<T>()` uses no reflection per request and works with the request delegate
  generator (`PublishAot`). Register validators with `AddValidator<TModel, TValidator>()`, not
  assembly scanning.
- Call `builder.Services.AddProblemDetails()`. It gives the app's JSON options the metadata that
  every `ValidationProblem` result needs in Native AOT. Without it the endpoint is a setup mistake
  (see above) unless you set a `ResultFactory`, instead of answering 500 only to invalid requests.
- The `errorCodes` and `severities` extensions are written as JSON elements, so they need no
  entries in your `JsonSerializerContext`. When you call `ToValidationProblem()` or
  `ToProblemDetails()` yourself with a `DictionaryKeyPolicy`, pass it as `keyPolicy` so the keys
  match `errors`. `ToValidationProblemDetails()` (MVC) uses
  dictionaries instead, which every MVC formatter can write.
- MVC itself does not support Native AOT, so the MVC filter does not either.

## Links

- [DotThrow on GitHub](https://github.com/shanto462/DotThrow)
- [Validation guide](https://github.com/shanto462/DotThrow/blob/main/docs/validation.md)
- [Changelog](https://github.com/shanto462/DotThrow/blob/main/CHANGELOG.md)
