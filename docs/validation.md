# Validation: collect every error

[Guard clauses](guards.md) stop at the first problem. When you check user input, such as
a form or an API request, you usually want every problem at once. DotThrow runs the same
checks in collect mode: a failed check is recorded instead of thrown.

There are two ways to collect errors:

- [`ValidationReport`](#validationreport): check values one by one, in any method.
- [`Validator<T>`](#validatort): a reusable set of rules for one type.

The examples assume `using DotThrow;`.

## ValidationReport

```csharp
var form = new SignUpForm(Name: "", Email: "ada.example.com", Age: 12);

var report = new ValidationReport();
report.Check(form.Name, "Name").IfNullOrWhiteSpace().IfLongerThan(100);
report.Check(form.Email, "Email").IfNullOrWhiteSpace().IfNotEmailAddress();
report.Check(form.Age, "Age").IfLessThan(13, "You must be 13 or older.");

foreach (var failure in report.Failures)
{
    Console.WriteLine($"{failure.Name}: {failure.Message} [{failure.ErrorCode}]");
}

public sealed record SignUpForm(string Name, string Email, int Age);
```

```text
Name: 'Name' must not be empty or white space. [WhiteSpace]
Email: 'Email' must be a valid email address. [NotEmailAddress]
Age: You must be 13 or older. [LessThan]
```

- `Check()` takes the same checks as `Throw()`.
- A failed check adds a `ValidationFailure` to the report and skips the rest of its
  chain, so each value reports at most one failure.
- Like `Throw()`, the name defaults to the text of the expression, here `form.Name`. Pass
  a name, as above, when the names go to API clients.
- A report is not thread-safe. Use one report per validation run.

### What a report gives you

| Member | What it does |
| --- | --- |
| `IsValid` | `true` when nothing failed. |
| `Failures` | The failures in the order the checks ran. Each has `Name`, `Message` and `ErrorCode`. |
| `ToDictionary()` | Groups the messages by name: `Dictionary<string, string[]>`, the shape ASP.NET Core uses for validation problems. |
| `ThrowIfInvalid()` | Throws a `ValidationFailedException` with every failure when the report is not valid. |
| `Add(name, message, errorCode?)` | Adds your own failure. The code defaults to `Invalid`. |
| `Add(failure)` | Adds a `ValidationFailure`. |
| `AddRange(other, prefix?)` | Adds the failures of another report, optionally with a name prefix (`"Address"` turns `"City"` into `"Address.City"`). |
| `ToString()` | `Validation passed.` or `Validation failed with 2 errors:` and one line per failure. |

### Your own failures

Not every rule fits a chain. Add failures yourself:

```csharp
var order = new { Start = new DateOnly(2026, 5, 2), End = new DateOnly(2026, 5, 1) };

var orderReport = new ValidationReport();
if (order.End < order.Start)
{
    orderReport.Add("End", "The end date must not be before the start date.", "EndBeforeStart");
}
```

### Throwing after all

`ThrowIfInvalid()` throws a `ValidationFailedException` that carries the whole report.
It derives from `ArgumentException`, so code that already catches bad arguments catches
it too.

```csharp
var input = new SignUpForm(Name: "", Email: "ada@example.com", Age: 30);

var inputReport = new ValidationReport();
inputReport.Check(input.Name, "Name").IfNullOrWhiteSpace();

try
{
    inputReport.ThrowIfInvalid();
}
catch (ValidationFailedException exception)
{
    Console.WriteLine(exception.Failures[0].Message);   // 'Name' must not be empty or white space.
}
```

### ASP.NET Core

`ToDictionary()` has the shape that `Results.ValidationProblem` takes, so an invalid
request becomes a standard 400 response with an `errors` object:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/sign-up", (SignUpForm form) =>
{
    var report = new ValidationReport();
    report.Check(form.Name, "Name").IfNullOrWhiteSpace().IfLongerThan(100);
    report.Check(form.Email, "Email").IfNullOrWhiteSpace().IfNotEmailAddress();
    report.Check(form.Age, "Age").IfLessThan(13, "You must be 13 or older.");

    return report.IsValid
        ? Results.Ok()
        : Results.ValidationProblem(report.ToDictionary());
});

app.Run();
```

```json
"errors": {
  "Name": ["'Name' must not be empty or white space."],
  "Email": ["'Email' must be a valid email address."],
  "Age": ["You must be 13 or older."]
}
```

## Validator&lt;T&gt;

A `Validator<T>` holds the rules for a type, so you write them once and reuse them. The
rules use the same checks, and failure names come from the property paths.

```csharp
public sealed record Address(string Street, string City);

public sealed record Order(string Number, Address? ShippingAddress, IReadOnlyList<string> Tags);

public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.Street, street => street.IfNullOrWhiteSpace().IfLongerThan(100));
        For(x => x.City, city => city.IfNullOrWhiteSpace());
    }
}

public static class OrderValidation
{
    public static readonly Validator<Order> Rules = new Validator<Order>()
        .For(x => x.Number, number => number.IfNullOrWhiteSpace().IfNotStartsWith("ORD-"))
        .For(x => x.ShippingAddress, address => address.IfNull())
        .For(x => x.ShippingAddress, new AddressValidator())
        .ForEach(x => x.Tags, tag => tag.IfNullOrWhiteSpace().IfLongerThan(20))
        .Must(x => x.Tags.Count <= 5, "An order can have at most 5 tags.", name: "Tags");
}
```

```csharp
var order = new Order("1001", new Address("12 Analytical Way", ""), ["gift", " "]);

ValidationReport orderResult = OrderValidation.Rules.Validate(order);
foreach (var failure in orderResult.Failures)
{
    Console.WriteLine($"{failure.Name}: {failure.Message}");
}
```

```text
Number: 'Number' must start with 'ORD-'.
ShippingAddress.City: 'City' must not be empty or white space.
Tags[1]: 'Tags[1]' must not be empty or white space.
```

A validator can be built inline or as a subclass. Build it once and share it: validating
is thread-safe. The main parts:

| Method | What it does |
| --- | --- |
| `For(selector, chain)` | Checks one property with a chain of checks. |
| `For(selector, validator)` | Validates a nested object with its own validator. A null object is skipped; add an `IfNull()` rule to require it. |
| `ForEach(selector, chain)` | Checks every item of a collection. Names carry the index: `Tags[1]`. |
| `ForEach(selector, validator)` | Validates every item with its own validator: `Lines[2].Quantity`. |
| `Must(predicate, message)` | A rule about the whole object. It fails when the predicate returns `false`. |
| `MustAsync(predicate, message)` | The same with an async predicate, for example a database lookup. |
| `Custom(rule)`, `CustomAsync(rule)` | Free-form rules that get the object and the report. |
| `Include(validator)` | Adds every rule of another validator for the same type. |
| `Validate(value, groups)` | Runs the rules and returns a `ValidationReport`. |
| `ValidateAsync(value)` | Runs the rules, including async rules. |
| `ValidateAndThrow(value)`, `ValidateAndThrowAsync(value)` | Throws a `ValidationFailedException` when a rule fails. |

Rules can take a `group:` and a `when:` condition. Without groups, `Validate` runs the
rules in `ValidationGroups.Default`; pass group names to run those groups instead, or
`ValidationGroups.All` to run every rule. A validator with async rules must run with
`ValidateAsync`; `Validate` throws an `InvalidOperationException` for it.

`Validator<T>` implements `IValidator<T>`, so you can register it for dependency injection,
for example `builder.Services.AddSingleton<IValidator<Order>>(OrderValidation.Rules);`, and
mock it in tests.

The samples project shows nested validators, collections, groups and async rules running
end to end: [samples/DotThrow.Samples](../samples/DotThrow.Samples/Program.cs).
