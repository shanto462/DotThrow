# Validation: collect every error

[Guard clauses](guards.md) stop at the first problem. When you check user input, such as
a form or an API request, you usually want every problem at once. DotThrow runs the same
checks in collect mode: a failed check is recorded instead of thrown.

There are two ways to collect errors:

- [`ValidationReport`](#validationreport): check values one by one, in any method.
- [`Validator<T>`](#validatort): a reusable set of rules for one type, with nested
  objects, collections, conditions, groups, async rules and more. It has the features of
  FluentValidation 12; [fluentvalidation.md](fluentvalidation.md) maps one to the other.

The examples assume `using DotThrow;`.

**Contents:** [ValidationReport](#validationreport) ·
[Validator&lt;T&gt;](#validatort) · [Property rules](#property-rules) ·
[Nested objects](#nested-objects) · [Collections](#collections) ·
[Rules about the whole object](#rules-about-the-whole-object) ·
[Rule options](#rule-options) · [Conditions](#conditions) · [Groups](#groups) ·
[Cascade](#cascade) · [Severity and state](#severity-and-state) ·
[Reusing validators](#reusing-validators) · [Running a validator](#running-a-validator) ·
[Async rules](#async-rules) · [Hooks and defaults](#hooks-and-defaults) ·
[Dependency injection and ASP.NET Core](#dependency-injection-and-aspnet-core) ·
[Testing validators](#testing-validators)

## ValidationReport

```csharp
var form = new SignUpForm(Name: "", Email: "ada.example.com", Age: 12);

var report = new ValidationReport();
report.Check(form, f => f.Name).IfNullOrWhiteSpace().IfLongerThan(100);
report.Check(form, f => f.Email).IfNullOrWhiteSpace().IfNotEmailAddress();
report.Check(form, f => f.Age).IfLessThan(13, "You must be 13 or older.");

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
- `Check(form, f => f.Name)` names the failure after the property path, `Name`, as a
  validator does. `Check(form.Name)` names it after the expression, `form.Name`, and
  `Check(form.Name, "Name")` uses the name you pass.
- A report is not thread-safe. Use one report per validation run.

### What a report gives you

| Member | What it does |
| --- | --- |
| `IsValid` | `true` when nothing failed (failures of every severity count). |
| `Failures` | The failures in the order the checks ran. |
| `ToDictionary()` | Groups the messages by name: `Dictionary<string, string[]>`, the shape ASP.NET Core uses for validation problems. |
| `ThrowIfInvalid()` | Throws a `ValidationFailedException` with every failure when the report is not valid. |
| `Add(name, message, errorCode?)`, `Add(failure)` | Adds your own failure. The code defaults to `Invalid`. |
| `AddRange(other, prefix?)` | Adds the failures of another report, optionally with a name prefix (`"Address"` turns `"City"` into `"Address.City"`). |
| `GroupsExecuted` | The groups a validator ran (see [Groups](#groups)). |
| `ToString()` | `Validation passed.`, or `Validation failed with 2 errors:` and one line per failure. |
| `ToString(separator)` | The messages joined by `separator`, like FluentValidation's `ToString`. |

Each `ValidationFailure` has:

| Member | What it holds |
| --- | --- |
| `Name` | The property path, such as `Address.City` or `Lines[2].Quantity`. Empty for a rule about the whole object. |
| `Message` | The message. |
| `ErrorCode` | The [error code](guards.md#error-codes), such as `LongerThan`. |
| `Severity` | `Error`, `Warning` or `Info` (see [Severity and state](#severity-and-state)). |
| `AttemptedValue` | The value that failed. It may hold a secret, so do not log or send it as is. |
| `CustomState` | Any object a rule attached with `WithState`. |
| `MessageArguments` | The values behind the message placeholders, such as `PropertyName`. |

### Throwing after all

`ThrowIfInvalid()` throws a `ValidationFailedException` that carries the whole report.
It derives from `ArgumentException`, so code that already catches bad arguments catches
it too.

```csharp
var input = new SignUpForm(Name: "", Email: "ada@example.com", Age: 30);

var inputReport = new ValidationReport();
inputReport.Check(input, x => x.Name).IfNullOrWhiteSpace();

try
{
    inputReport.ThrowIfInvalid();
}
catch (ValidationFailedException exception)
{
    Console.WriteLine(exception.Failures[0].Message);   // 'Name' must not be empty or white space.
}
```

## Validator&lt;T&gt;

A `Validator<T>` holds the rules for a type, so you write them once and reuse them. Write
it as a subclass, which suits dependency injection, or build one inline. Build it once
and share it: validating is thread-safe.

```csharp
public sealed record Address(string Street, string City, string? PostalCode);

public sealed record OrderLine(string Sku, int Quantity, decimal Price);

public sealed record Order(string Number, string Email, Address? ShippingAddress, List<OrderLine> Lines)
{
    public List<string> Tags { get; init; } = [];

    public bool IsGift { get; init; }

    public string? GiftMessage { get; init; }

    public DateOnly OrderDate { get; init; }

    public DateOnly? DeliveryDate { get; init; }
}

public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.Street, street => street.IfNullOrWhiteSpace().IfLongerThan(100));
        For(x => x.City, city => city.IfNullOrWhiteSpace());
        For(x => x.PostalCode, code => code.AllowNull().IfNotDigits().IfLengthNotEqualTo(5));
    }
}

public sealed class OrderLineValidator : Validator<OrderLine>
{
    public OrderLineValidator()
    {
        For(x => x.Sku, sku => sku.IfNullOrWhiteSpace().IfNotStartsWith("SKU-"));
        For(x => x.Quantity, quantity => quantity.IfOutOfRange(1, 100));
        For(x => x.Price, price => price.IfNegative().IfPrecisionScaleExceeded(9, 2));
    }
}

public sealed class OrderValidator : Validator<Order>
{
    public OrderValidator()
    {
        For(x => x.Number, number => number.IfNullOrWhiteSpace().IfNotStartsWith("ORD-"));
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
        For(x => x.ShippingAddress, address => address.IfNull());
        For(x => x.ShippingAddress, new AddressValidator());
        For(x => x.Lines, lines => lines.IfNullOrEmpty().IfCountGreaterThan(20));
        ForEach(x => x.Lines, new OrderLineValidator());
        ForEach(x => x.Tags, tag => tag.IfNullOrWhiteSpace().IfLongerThan(20));
        For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace(), rule => rule.When(x => x.IsGift));
        For(x => x.DeliveryDate, (date, x) => date.AllowNull().IfNull().IfBefore(x.OrderDate));
    }
}
```

```csharp
var order = new Order("1001", "ada@example.com", new Address("12 Analytical Way", "", "1234"), [new OrderLine("ENGINE", 0, 9.99m)])
{
    Tags = ["gift", " "],
    IsGift = true,
    OrderDate = new DateOnly(2026, 5, 2),
    DeliveryDate = new DateOnly(2026, 5, 1),
};

ValidationReport result = new OrderValidator().Validate(order);
foreach (var failure in result.Failures)
{
    Console.WriteLine($"{failure.Name}: {failure.Message}");
}
```

```text
Number: 'Number' must start with 'ORD-'.
ShippingAddress.City: 'City' must not be empty or white space.
ShippingAddress.PostalCode: 'PostalCode' must be exactly 5 characters long.
Lines[0].Sku: 'Sku' must start with 'SKU-'.
Lines[0].Quantity: 'Quantity' must be between 1 and 100.
Tags[1]: 'Tags' must not be empty or white space.
GiftMessage: 'GiftMessage' must not be null.
DeliveryDate: 'DeliveryDate' must not be before 2026-05-02.
```

An inline validator has the same methods; each returns the validator, so they chain:

```csharp
var addressRules = new Validator<Address>()
    .For(x => x.Street, street => street.IfNullOrWhiteSpace())
    .For(x => x.City, city => city.IfNullOrWhiteSpace());
```

## Property rules

`For(selector, chain)` checks one property with a chain of checks. Each rule reports at
most one failure, the first failed check of its chain (see [Cascade](#cascade)).

- The failure name is the property path from the selector: `x => x.Address.City` gives
  `Address.City`. The message uses the same name, quoted.
- A nullable property works like any other: `For(x => x.MiddleName, m => m.AllowNull().IfLongerThan(50))`
  for a `string?` property, and `For(x => x.Age, a => a.AllowNull().IfNull().IfNegative())`
  for an `int?` property.
- The selector may compute a value, such as `x => x.Name.Trim()`. Name such a rule with
  `rule => rule.OverridePropertyName("Name")`.

### Rules that read the whole object

A chain with two parameters gets the object too, so it can compare properties:

```csharp
var bookingRules = new Validator<Booking>()
    .For(x => x.End, (end, x) => end.IfBefore(x.Start, "The end must not be before the start."))
    .For(x => x.Guests, (guests, x) => guests.IfGreaterThan(x.RoomSize));

public sealed record Booking(DateTime Start, DateTime End, int Guests, int RoomSize);
```

A two-parameter chain is also the place for plain C# logic, such as a check that applies
only in one case: `(name, x) => { name.IfNullOrEmpty(); if (x.IsCompany) { name.IfLongerThan(20); } }`.

## Nested objects

| Rule | What it does |
| --- | --- |
| `For(x => x.Address, new AddressValidator())` | Validates the nested object with its own validator. Failure names get the path as a prefix (`ShippingAddress.City`); messages keep the child name (`'City'`). A null value is skipped, so add `address => address.IfNull()` to require it. |
| `For(x => x.Address, address => address.For(a => a.City, c => c.IfNullOrEmpty()))` | Inline child rules, without a separate class. |
| `For(x => x.Address, x => x.IsAbroad ? foreignRules : localRules)` | Picks the validator per object. Returning `null` skips the value. A `(x, address) => ...` form gets the value too. |
| `For(x => x.Payment, new PolymorphicValidator<Payment>().Add(new CardValidator()).Add(new InvoiceValidator()))` | Picks the validator by the exact runtime type of the value. See [Reusing validators](#reusing-validators). |

## Collections

| Rule | What it does |
| --- | --- |
| `ForEach(x => x.Tags, tag => tag.IfLongerThan(20))` | Runs the chain for every item. Names carry the index (`Tags[1]`); messages use the collection name (`'Tags'`). |
| `ForEach(x => x.Lines, new OrderLineValidator())` | Validates every item with its own validator: `Lines[2].Quantity`. Null items are skipped. |
| `ForEach(x => x.Lines, line => line.For(l => l.Sku, s => s.IfNullOrEmpty()))` | Inline rules for every item. |
| `ForEach(x => x.Lines, (x, line) => ...)` | Picks the validator per item. |

A null collection is skipped by `ForEach`; check the collection itself with a `For` rule,
such as `For(x => x.Lines, lines => lines.IfNullOrEmpty())`.

`ForEach` rules take `EachRuleOptions`, with three more options:

```csharp
var cartRules = new Validator<Cart>()
    .ForEach(x => x.Items, item => item.IfLongerThan(10), rule => rule
        .Where(item => !item.StartsWith('#'))     // skip some items; indexes stay as they were
        .WithIndexer((item, index) => $"[{item}]")); // Items[apple] instead of Items[0]

public sealed record Cart(List<string> Items);
```

`WhereAsync` filters with an async predicate.

## Rules about the whole object

| Rule | What it does |
| --- | --- |
| `Must(x => x.End > x.Start, "The end must be after the start.")` | Fails when the predicate returns `false`. The failure has no name; give it one with `rule => rule.WithName("End")`. The default code is `Must`. |
| `Must(x => x.Total, total => total <= 10_000m, "Too large.")` | The same for one property; the failure is named `Total`. A `(x, total) => ...` form gets the object too. |
| `MustAsync(x => x.Email, async (email, ct) => !await users.ExistsAsync(email, ct), "Taken.")` | An async predicate, for example a database lookup. |
| `Custom((x, context) => ...)`, `CustomAsync((x, context, ct) => ...)` | Free-form rules. The context can run checks and add failures. |

The `ValidationContext<T>` of a `Custom` rule gives you:

| Member | What it does |
| --- | --- |
| `context.Check(x, x => x.Total)` | Runs checks in collect mode, named after the path (`Total`). `context.Check(value, "Name")` names it yourself. |
| `context.AddFailure(name, message, errorCode?)`, `AddFailure(message)`, `AddFailure(failure)` | Adds a failure. |
| `context.AppendArgument(name, value)` | Adds a value for `{name}` placeholders in `AddFailure` and `WithMessage` messages. |
| `context.Instance`, `Items`, `Groups`, `PropertyPath`, `Parent`, `IsAsync`, `CancellationToken` | What the rule runs on and with. `Items` holds the values passed in `ValidationOptions.Items`. |

```csharp
var invoiceRules = new Validator<Invoice>()
    .Custom((x, context) =>
    {
        context.AppendArgument("Expected", x.Lines.Sum());
        if (x.Total != x.Lines.Sum())
        {
            context.AddFailure("Total", "The total must be {Expected}.", "TotalMismatch");
        }
    });

public sealed record Invoice(decimal Total, List<decimal> Lines);
```

## Rule options

Every rule method takes an optional `configure` callback as its last argument. The
options apply to the whole rule.

```csharp
var signUpRules = new Validator<SignUpForm>()
    .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress(), rule => rule
        .WithName("E-mail address")
        .WithMessage("Please check {PropertyName}.")
        .WithErrorCode("BadEmail"));
```

| Option | What it does |
| --- | --- |
| `WithName(name)`, `WithName(x => ...)` | The name in messages (`{PropertyName}`). The failure name stays the property path. |
| `OverridePropertyName(path)` | The failure name, for example for a computed selector. |
| `WithMessage(text)`, `WithMessage(x => ...)`, `WithMessage((x, value) => ...)` | Replaces the message of every check in the rule. Placeholders: `{PropertyName}`, `{PropertyPath}`, `{PropertyValue}`, `{CollectionIndex}` and appended arguments. To word one check, pass `message:` to that check instead. |
| `WithErrorCode(code)` | Replaces the error code. |
| `WithSeverity(...)`, `WithState(...)` | See [Severity and state](#severity-and-state). |
| `When(...)`, `Unless(...)`, `WhenAsync(...)`, `UnlessAsync(...)` | See [Conditions](#conditions). |
| `InGroup(groups)` | See [Groups](#groups). |
| `Cascade(mode)` | See [Cascade](#cascade). |
| `DependentRules(v => ...)` | Rules that run only when this rule passed. |
| `WithChildGroups(groups)` | The groups a child validator runs with. |
| `Where`, `WhereAsync`, `WithIndexer` | `ForEach` rules only, see [Collections](#collections). |

`DependentRules` is handy for an expensive check that only makes sense when the value has
the right shape:

```csharp
var accountRules = new Validator<SignUpForm>()
    .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress(), rule => rule
        .DependentRules(lookup => lookup.MustAsync(
            x => x.Email,
            (email, cancellationToken) => Task.FromResult(email != "ada@example.com"),
            "The email address is already registered.")));
```

## Conditions

A condition on one rule:

```csharp
var giftRules = new Validator<Order>()
    .For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace(), rule => rule.When(x => x.IsGift));
```

A condition on a block of rules, with an optional `otherwise` block:

```csharp
var shippingRules = new Validator<Order>()
    .When(
        x => x.IsGift,
        gift => gift
            .For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace())
            .For(x => x.ShippingAddress, address => address.IfNull()),
        otherwise: notGift => notGift
            .For(x => x.GiftMessage, message => message.IfNotNull()));
```

`Unless` is the opposite of `When`. `WhenAsync` and `UnlessAsync` take an async condition.
Every condition can also read the `ValidationContext<T>`: `When((x, context) => ...)`.

## Groups

Groups (FluentValidation's rule sets) let one validator serve several situations, such as
saving a draft and checking out.

```csharp
var draftRules = new Validator<Order>()
    .For(x => x.Number, number => number.IfNullOrWhiteSpace())
    .Group("checkout", checkout => checkout
        .For(x => x.ShippingAddress, address => address.IfNull())
        .For(x => x.Lines, lines => lines.IfNullOrEmpty()));

var draft = new Order(Number: "", Email: "ada@example.com", ShippingAddress: null, Lines: []);

Console.WriteLine(draftRules.Validate(draft).Failures.Count);                                       // 1
Console.WriteLine(draftRules.Validate(draft, "checkout").Failures.Count);                           // 2
Console.WriteLine(draftRules.Validate(draft, ValidationGroups.Default, "checkout").Failures.Count); // 3
Console.WriteLine(draftRules.Validate(draft, ValidationGroups.All).Failures.Count);                 // 3
```

- A rule without a group is in `ValidationGroups.Default`. Validating without groups runs
  only those rules.
- Pass group names to run those groups only. Add `ValidationGroups.Default` to run the
  default rules too, or pass `ValidationGroups.All` to run every rule.
- `rule => rule.InGroup("a", "b")` puts one rule in groups; `Group(...)` puts a block of
  rules in them. Group names ignore case.
- A child validator runs with the same groups as its parent rule, unless the rule says
  `WithChildGroups(...)`. Inline child rules and dependent rules take the groups of their
  rule.
- `report.GroupsExecuted` lists the groups that ran.

## Cascade

- **Rule cascade** (default `Stop`): a rule stops at its first failed check, so each rule
  reports at most one failure. Set `RuleCascade = CascadeMode.Continue` on the validator,
  or `rule => rule.Cascade(CascadeMode.Continue)` on one rule, to run every check of the
  chain. A null value still stops the chain, so null is reported once.
- **Class cascade** (default `Continue`): the validator runs every rule. Set
  `ClassCascade = CascadeMode.Stop` to stop at the first failed rule.
- For a `ForEach` rule the rule cascade applies to each item: every item is checked.

## Severity and state

A failure can be a warning or a note instead of an error, and it can carry your own data:

```csharp
var profileRules = new Validator<SignUpForm>()
    .For(x => x.Name, name => name.IfLongerThan(40), rule => rule
        .WithSeverity(Severity.Warning)
        .WithState(x => new { x.Name.Length }));
```

`IsValid` counts failures of every severity, as FluentValidation does. Filter
`report.Failures` by `Severity` when warnings should not block.

## Reusing validators

- `Include(otherValidator)` adds every rule of another validator for the same type.
  `Include(x => validator)` picks it per object. A validator that includes itself, even
  through others, throws an `ArgumentException` when you build it.
- `PolymorphicValidator<T>` picks the validator by the exact runtime type of the value. A
  value whose type has no validator passes:

```csharp
var paymentRules = new PolymorphicValidator<Payment>()
    .Add(new Validator<CardPayment>().For(x => x.CardNumber, number => number.IfNotCreditCardNumber()))
    .Add(new Validator<InvoicePayment>().For(x => x.Iban, iban => iban.IfNullOrWhiteSpace()));

var checkoutRules = new Validator<Checkout>()
    .For(x => x.Payment, payment => payment.IfNull())
    .For(x => x.Payment, paymentRules);

public abstract record Payment;

public sealed record CardPayment(string CardNumber) : Payment;

public sealed record InvoicePayment(string Iban) : Payment;

public sealed record Checkout(Payment? Payment);
```

## Running a validator

| Call | What it does |
| --- | --- |
| `Validate(value)` | Runs the default rules and returns a `ValidationReport`. |
| `Validate(value, "group", ...)` | Runs the rules of the given groups. |
| `Validate(value, options => ...)` | Runs with options, see below. |
| `ValidateAsync(value, cancellationToken)` | Runs the default rules, including async ones. Returns a completed task when no rule had to wait. |
| `ValidateAndThrow(value)`, `ValidateAndThrowAsync(value)` | Throws a `ValidationFailedException` with every failure. |

Options:

| Option | What it does |
| --- | --- |
| `IncludeGroups(...)`, `IncludeDefaultGroup()`, `IncludeAllGroups()` | Selects groups. |
| `IncludeProperties(x => x.Email)`, `IncludeProperties("ShippingAddress.City", "Lines[].Quantity")` | Runs only the rules for these properties, for example the field a user just edited. `[]` matches every index. |
| `ThrowOnFailure()` | Throws instead of returning a report. |
| `Items` | Values that `Custom` rules and conditions read through `context.Items`. |
| `UseRuleSelector(selector)` | Your own `IRuleSelector` decides which rules run. |
| `ClassCascade` | Overrides the class cascade for this run. |

```csharp
var orderRules = new OrderValidator();
var edited = new Order("ORD-1", "ada.example.com", null, []);

var emailOnly = orderRules.Validate(edited, options => options.IncludeProperties(x => x.Email));
Console.WriteLine(emailOnly.Failures.Count);   // 1
```

Validating `null` throws an `ArgumentNullException`, unless an overridden `PreValidate` handles it (see [Hooks and defaults](#hooks-and-defaults)).

## Async rules

`MustAsync`, `CustomAsync`, `WhenAsync`, `UnlessAsync` and `WhereAsync` are async. Run a
validator with async rules with `ValidateAsync`. `Validate` throws an
`AsyncRuleInvokedSynchronouslyException` (an `InvalidOperationException`) when it reaches
an async rule that the selected groups and properties include.
`Describe().HasAsyncRules` tells you in advance whether a validator has async rules.

## Hooks and defaults

A subclass can override:

| Method | What it does |
| --- | --- |
| `PreValidate(context)` | Runs before the rules, also for child validators and for a null object. Return `false` to skip the rules; failures added with `context.AddFailure` stay. |
| `CreateValidationException(report)` | The exception that `ValidateAndThrow` throws. |
| `OnRuleAdded(rule)` | Runs for every rule after its options, for example to put every rule of a validator in a group. |

`ValidatorDefaults` holds global settings, set once at startup:

| Setting | Default | What it does |
| --- | --- | --- |
| `RuleCascade`, `ClassCascade` | `Stop`, `Continue` | Cascade of new validators. |
| `Severity` | `Error` | Severity of failures without `WithSeverity`. |
| `PropertyNameResolver` | none | Turns a type and a property path into the failure name. |
| `DisplayNameResolver` | none | Turns a type and a property path into the message name, for example `PostalCode` into `Postal code`. |
| `PropertyChainSeparator` | `"."` | Separator of nested paths. |
| `ErrorCodeResolver` | none | Maps check codes to your own codes. |
| `OnFailureCreated` | none | Changes or replaces every failure, for example to add a code prefix. |
| `MaxDepth` | 256 | How deep child validators may nest before a clear `InvalidOperationException`. |

`ValidatorDefaults.Reset()` puts every setting back.

`Describe()` returns a `ValidatorDescriptor`: every rule with its path, kind, groups,
checks, conditions and async flag. Tools and tests can read it.

## Dependency injection and ASP.NET Core

`Validator<T>` implements `IValidator<T>` (and the non-generic `IValidator`), so you can
register it and mock it in tests. `IValidator<T>` adds two members to the non-generic
`IValidator`: `Validate(value, options)` and `ValidateAsync(value, options,
cancellationToken)`. The short forms, such as `validator.Validate(value)` and
`validator.ValidateAsync(value)`, are extension methods that work on any `IValidator<T>`.

`DotThrow.Scanning.AssemblyScanner` finds every validator in an assembly. It uses
reflection, so it is not trimming safe.

Two companion packages do the wiring for you ([integrations.md](integrations.md) has the details):

- [DotThrow.DependencyInjection](../src/DotThrow.DependencyInjection/README.md):
  `services.AddValidatorsFromAssemblyContaining<Program>()` (assembly scanning),
  `services.AddValidator<SignUp, SignUpValidator>()` (no reflection, trimming safe), and
  options validation with `ValidateOnStart`.
- [DotThrow.AspNetCore](../src/DotThrow.AspNetCore/README.md): `.WithValidation<T>()` on a
  minimal API endpoint or route group runs the validator before the handler and answers an
  invalid request with a 400 validation problem. MVC apps get a global filter and a
  `[ValidateWithDotThrow]` attribute.

Without the packages, register the validator yourself and call it in the endpoint:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IValidator<SignUpForm>, SignUpFormValidator>();
var app = builder.Build();

app.MapPost("/sign-up", async (SignUpForm form, IValidator<SignUpForm> validator, CancellationToken cancellationToken) =>
{
    var report = await validator.ValidateAsync(form, cancellationToken);
    return report.IsValid
        ? Results.Ok()
        : Results.ValidationProblem(report.ToDictionary());
});

app.Run();

public sealed class SignUpFormValidator : Validator<SignUpForm>
{
    public SignUpFormValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(100));
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
        For(x => x.Age, age => age.IfLessThan(13, "You must be 13 or older."));
    }
}
```

An invalid request gets a standard 400 response with an `errors` object:

```json
"errors": {
  "Name": ["'Name' must not be empty or white space."],
  "Email": ["'Email' must be a valid email address."],
  "Age": ["You must be 13 or older."]
}
```

## Testing validators

`DotThrow.Testing` has the test helpers of FluentValidation's `TestHelper`, with the same
names. They throw a `ValidationTestException` when an expectation fails, so they work with
any test framework.

```csharp
using DotThrow.Testing;

var result = new OrderValidator().TestValidate(new Order("1", "ada@example.com", null, []));

result.ShouldHaveValidationErrorFor(x => x.Number).WithErrorCode(ErrorCodes.NotStartsWith);
result.ShouldHaveValidationErrorFor(x => x.ShippingAddress).WithErrorCode(ErrorCodes.Null);
result.ShouldNotHaveValidationErrorFor(x => x.Email);
```

Also available: `ShouldNotHaveAnyValidationErrors()`, `ShouldHaveValidationErrors()`,
`TestValidateAsync`, `WithErrorMessage`, `WithSeverity`, `WithCustomState`,
`WithMessageArgument`, `WithAttemptedValue`, the `Without...` forms, `When`, `WhenAll`,
`Only`, and `ShouldHaveChildValidator`.
