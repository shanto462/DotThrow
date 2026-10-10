# Dependency injection and ASP.NET Core

Two companion packages connect DotThrow validators to the rest of a .NET app. They have
the same version as `DotThrow`.

| Package | What it does | Targets |
| --- | --- | --- |
| `DotThrow.DependencyInjection` | Registers validators (by assembly scanning, or one by one without reflection) and validates options classes. | netstandard2.0, net462, net8.0, net9.0, net10.0 |
| `DotThrow.AspNetCore` | Validates minimal API requests with `.WithValidation<T>()`, MVC actions with a filter, and turns reports into validation problems. | net8.0, net9.0, net10.0 |

```shell
dotnet add package DotThrow.DependencyInjection
dotnet add package DotThrow.AspNetCore
```

The examples on this page use these types:

```csharp
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

## Register validators

```csharp
var builder = WebApplication.CreateBuilder(args);

// Every public validator class in the assembly, scoped (one per request in web apps).
builder.Services.AddValidatorsFromAssemblyContaining<SignUpValidator>();

// No scanning: safe for trimming and Native AOT.
builder.Services.AddValidator<SignUp, SignUpValidator>();

// A validator object, or a factory that gets the service provider.
builder.Services.AddValidator(new Validator<Contact>()
    .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress()));

public sealed record Contact(string? Email);
```

- A validator class is registered as every `IValidator<T>` it implements and as the class
  itself, so you can ask for `IValidator<SignUp>` in a constructor or a minimal API
  handler.
- `AddValidatorsFromAssembly(assembly, lifetime, filter, includeInternalTypes)`,
  `AddValidatorsFromAssemblies` and `AddValidatorsFromAssemblyContaining` take the same
  arguments as in FluentValidation. The filter gets an `AssemblyScanResult` with
  `InterfaceType`, `ValidatorType` and `ModelType`. One difference: a class that
  implements several `IValidator<T>` interfaces is registered for all of them;
  FluentValidation registers the first.
- Scanning registers a class after its base classes, so a single `IValidator<T>` resolves
  to the most derived validator (`IEnumerable<IValidator<T>>` gets all of them). Types that
  cannot be loaded, because an assembly they need is missing, are skipped.
- Assembly scanning uses reflection and is marked `[RequiresUnreferencedCode]`. In trimmed
  and Native AOT apps, use `AddValidator`.

## Validate options

`ValidateWithDotThrow()` validates an options class with its registered validator. With
`ValidateOnStart()`, a bad configuration stops the app at startup instead of failing later:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidator<SmtpOptions, SmtpOptionsValidator>();
builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration("Smtp")
    .ValidateWithDotThrow()
    .ValidateOnStart();

public sealed class SmtpOptions
{
    public string? Host { get; set; }

    public int Port { get; set; }
}

public sealed class SmtpOptionsValidator : Validator<SmtpOptions>
{
    public SmtpOptionsValidator()
    {
        For(x => x.Host, host => host.IfNullOrWhiteSpace());
        For(x => x.Port, port => port.IfOutOfRange(1, 65535));
    }
}
```

With `"Port": 0` in the configuration the app does not start:

```text
Microsoft.Extensions.Options.OptionsValidationException: SmtpOptions.Port: 'Port' must be between 1 and 65535.
```

- `ValidateWithDotThrow(validator)` takes a validator object, and
  `ValidateWithDotThrow(v => v.For(...))` takes rules written inline.
- Each failure becomes one message, named by its property path.
- Named options are validated by name, and reloaded configuration is validated again.
- The registered validator may have any lifetime: it is resolved in its own scope.
- Options validation is synchronous, so the validator must not have async rules.

## Minimal APIs

`.WithValidation<T>()` validates the handler argument of type `T` with the registered
`IValidator<T>` before the handler runs. An invalid request never reaches the handler; the
answer is a 400 `application/problem+json` with an `errors` object.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddValidator<SignUp, SignUpValidator>();

var app = builder.Build();

app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok())
    .WithValidation<SignUp>();

app.Run();
```

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

More forms:

| Call | What it does |
| --- | --- |
| `.WithValidation<Order>("checkout")` | Runs these [groups](validation.md#groups). |
| `.WithValidation(new NoteValidator())` | Uses this validator object; no registration needed. |
| `app.MapGroup("/orders").WithValidation<Order>()` | Validates every endpoint of the group that has an `Order` parameter. |
| `.WithValidation<CreateUser>(o => { ... })` | Sets `Groups`, `ConfigureValidation` (per-request options, such as `Items`) and `ResultFactory` (your own response). |

- The filter finds the handler parameters of type `T` once, when the endpoints are built.
  A parameter whose type derives from `T` is validated too. Parameters from services
  (`[FromServices]`, `[FromKeyedServices]`, or a registered service type) are never
  validated, and an optional argument that the request did not send is skipped. When
  several arguments are validated, the answer has all their failures, named after the
  parameter (`first.Value`, `second.Value`).
- A setup mistake (a handler without a `T` parameter from the request, no `IValidator<T>`
  registered, JSON options that cannot write a validation problem) never breaks other
  endpoints: it is logged as an error at the first request, and only that endpoint answers
  500. To stop the app at startup instead, with every mistake listed, call
  `builder.Services.ValidateEndpointsOnStart()`.
- Validated endpoints describe the 400 response for OpenAPI.

## MVC

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValidatorsFromAssemblyContaining<SignUpValidator>();
builder.Services.AddControllers().AddDotThrowValidation();
```

Every action argument whose type has a registered `IValidator<T>` is validated before the
action runs, and each failure becomes a `ModelState` error named by its property path.

- `[ApiController]` actions do not run for an invalid model: the answer is the app's usual
  invalid model response, a 400 `ValidationProblemDetails` by default.
- Other actions run and check `ModelState.IsValid` themselves, as with data annotations.

Without the global filter, put `[ValidateWithDotThrow]` on a controller or an action. The
closest attribute wins, so it can change the groups or turn validation off for one action:

```csharp
using DotThrow.AspNetCore;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    [ValidateWithDotThrow("checkout")]
    public IActionResult Create(SignUp order) => Ok();

    [HttpPost("draft")]
    [ValidateWithDotThrow(Skip = true)]
    public IActionResult SaveDraft(SignUp order) => Ok();
}
```

`AddDotThrowValidation(o => ...)` sets the default groups and a per-request
`ConfigureValidation`.

## Error codes and severities in responses

```csharp
using DotThrow.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ValidationProblemOptions>(o =>
{
    o.IncludeErrorCodes = true;
    o.IncludeSeverities = true;
});
```

Both filters then add two extensions, keyed and ordered like `errors` (keys follow the
JSON options' `DictionaryKeyPolicy`, the same as `errors`):

```json
"errorCodes": { "Email": ["NotEmailAddress"] },
"severities": { "Email": ["Error"] }
```

The value that failed (`ValidationFailure.AttemptedValue`) is never written to a response,
because it may be a password or a token.

## Problem details helpers

When you validate yourself, these helpers turn a report into a response:

```csharp
using DotThrow.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddValidator<SignUp, SignUpValidator>();
var app = builder.Build();

app.MapPost("/sign-up/manual", async (SignUp request, IValidator<SignUp> validator) =>
{
    var report = await validator.ValidateAsync(request);
    return report.IsValid
        ? Results.Ok()
        : report.ToValidationProblem();
});

app.Run();
```

| Helper | Gives |
| --- | --- |
| `report.ToValidationProblem()` | A minimal API result (`TypedResults.ValidationProblem`). |
| `report.ToProblemDetails()` | An `HttpValidationProblemDetails`, for example for `IProblemDetailsService`. |
| `report.ToValidationProblemDetails()` | An MVC `ValidationProblemDetails`, for example for `BadRequest(details)`. |

Each takes an optional `ValidationProblemOptions` to add error codes and severities.
`ToValidationProblem` and `ToProblemDetails` also take a `keyPolicy`: pass your JSON
`DictionaryKeyPolicy` so the keys of the extensions match `errors`.

## Native AOT

- `.WithValidation<T>()` uses no reflection per request and works with the request
  delegate generator (`PublishAot`). Register validators with `AddValidator`, not with
  assembly scanning.
- Call `builder.Services.AddProblemDetails()`. It gives the app's JSON options the metadata
  that every validation problem result needs in Native AOT. Without it, the endpoint is a
  setup mistake (see above), unless you set a `ResultFactory`.
- The `errorCodes` and `severities` extensions are written as JSON elements, so they need
  no entries in your `JsonSerializerContext`.
- MVC itself does not support Native AOT, so the MVC filter does not either.

## Dynamic validators

`AddDynamicValidator` registers a [dynamic validator](dynamic-rules.md) whose rules come
from a configuration section, a section path, or a fixed definition, and reloads it when
the configuration changes:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDynamicValidator("Validation:SignUp", new PropertyMap<SignUp>().Add(x => x.Name).Add(x => x.Email));
```

A reload with errors keeps the old rules and is logged through `ILogger` (and passed to
`OnReloadError`), and a definition with errors stops the app at startup
(`ValidateOnStart`, on by default). See
[Dependency injection with reload](dynamic-rules.md#dependency-injection-with-reload).

## More

- [DotThrow.DependencyInjection README](../src/DotThrow.DependencyInjection/README.md)
- [DotThrow.AspNetCore README](../src/DotThrow.AspNetCore/README.md)
- [Validation guide](validation.md)
