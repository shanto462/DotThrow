<img src="assets/icon.png" alt="DotThrow icon" width="72" align="right">

# DotThrow

[![CI](https://github.com/shanto462/DotThrow/actions/workflows/ci.yml/badge.svg)](https://github.com/shanto462/DotThrow/actions/workflows/ci.yml)
[![CodeQL](https://github.com/shanto462/DotThrow/actions/workflows/codeql.yml/badge.svg)](https://github.com/shanto462/DotThrow/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/shanto462/DotThrow/badge)](https://scorecard.dev/viewer/?uri=github.com/shanto462/DotThrow)
[![NuGet](https://img.shields.io/nuget/v/DotThrow.svg)](https://www.nuget.org/packages/DotThrow)
[![NuGet downloads](https://img.shields.io/nuget/dt/DotThrow.svg)](https://www.nuget.org/packages/DotThrow)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Guard clauses and validation for .NET, with one set of checks. As a guard clause, a chain
of checks throws the right exception (`ArgumentNullException`,
`ArgumentOutOfRangeException` or `ArgumentException`) with the parameter name filled in.
In collect mode, the same checks record the errors of every value in a report that you
can return from an API. `Validator<T>` has the features of FluentValidation 12, rules can
also come from JSON or configuration, and Roslyn analyzers catch mistakes while you type.
DotThrow is MIT licensed, has no dependencies, runs on every .NET from .NET Framework
4.6.2 to .NET 10, and is safe for trimming and Native AOT.

**Contents:** [Why DotThrow](#why-dotthrow) · [Install](#install) ·
[Quick start](#quick-start) · [Checks at a glance](#checks-at-a-glance) ·
[ASP.NET Core and dependency injection](#aspnet-core-and-dependency-injection) ·
[Dynamic rules](#dynamic-rules) · [Analyzers](#analyzers) ·
[Coming from FluentValidation](#coming-from-fluentvalidation) ·
[Messages and languages](#messages-and-languages) · [Performance](#performance) ·
[Supported frameworks](#supported-frameworks) · [Trimming and Native AOT](#trimming-and-native-aot) ·
[Security](#security) · [Documentation](#documentation) · [Contributing](#contributing)

## Why DotThrow

- **One vocabulary for two jobs.** The checks you use in a constructor
  (`age.Throw().IfNegative()`) are the checks you use to validate a request
  (`For(x => x.Age, age => age.IfNegative())`). Other libraries do one or the other.
- **Free.** DotThrow is MIT licensed. In the last two years
  several popular .NET libraries changed their terms: FluentAssertions 8 moved to a
  commercial license (January 2025), and AutoMapper 15 and MediatR 13 moved to dual
  licenses (July 2025). None of the guard libraries below has done that; the risk there is
  that they stop being maintained.
- **Maintained.** Some guard and validation libraries are no longer maintained: Dawn.Guard
  (archived, NuGet package deprecated), Ensure.That (no activity since 2021 to 2022),
  Validot (archived in 2025), and Throw (last release in September 2023, .NET 6 only).
- **No dependencies, every .NET version.** One package for netstandard2.0,
  netstandard2.1, .NET Framework 4.6.2 and .NET 8, 9 and 10.
- **Mistakes found at build time.** Eleven analyzers, most with code fixes, ship inside the
  package: a chain without checks, an invalid regex, a range whose minimum is above its
  maximum, an async validator run synchronously, and more.
- **Rules as data.** Load rules from JSON or `IConfiguration`, update them at run time,
  and get the same failures as from code.
- **Fast.** A passing guard chain compiles to about the same code as a hand-written `if`
  and allocates nothing, and `Validator<T>` was 2.7 to 7 times faster than
  FluentValidation in [our benchmarks](#performance).
- **Safe defaults.** Null fails content checks, NaN fails range checks, default messages
  never contain the checked value, and the XML check rejects DTDs.

## Install

```shell
dotnet add package DotThrow
```

| Package | What it has | Targets |
| --- | --- | --- |
| `DotThrow` | Guard clauses, collect mode, `Validator<T>`, dynamic rules (`DotThrow.Dynamic`), message templates with 58 built-in languages, test helpers (`DotThrow.Testing`), an assembly scanner (`DotThrow.Scanning`), and Roslyn analyzers with code fixes. No dependencies. | netstandard2.0, netstandard2.1, net462, net8.0, net9.0, net10.0 |
| [`DotThrow.DependencyInjection`](src/DotThrow.DependencyInjection/README.md) | `AddValidatorsFromAssemblyContaining<T>()`, `AddValidator<TModel, TValidator>()` (trimming safe), options validation with `ValidateOnStart`, and dynamic validators from configuration with reload. | netstandard2.0, net462, net8.0, net9.0, net10.0 |
| [`DotThrow.AspNetCore`](src/DotThrow.AspNetCore/README.md) | `.WithValidation<T>()` for minimal API endpoints and route groups, an MVC filter and `[ValidateWithDotThrow]`, and validation problem helpers. | net8.0, net9.0, net10.0 |

```shell
dotnet add package DotThrow.DependencyInjection
dotnet add package DotThrow.AspNetCore
```

## Quick start

### Guard clauses

```csharp
using DotThrow;

public sealed class Customer
{
    public Customer(string name, string email, int age)
    {
        Name = name.Throw().IfNullOrWhiteSpace().IfLongerThan(100);
        Email = email.Throw().IfNullOrWhiteSpace().IfNotEmailAddress();
        Age = age.Throw().IfOutOfRange(0, 150);
    }

    public string Name { get; }

    public string Email { get; }

    public int Age { get; }
}
```

The first failed check throws, with the parameter name filled in for you:

```text
new Customer("   ", "ada@example.com", 36)
System.ArgumentException: 'name' must not be empty or white space. (Parameter 'name')

new Customer("Ada Lovelace", "ada@example.com", 200)
System.ArgumentOutOfRangeException: 'age' must be between 0 and 150. (Parameter 'age')
Actual value was 200.
```

The name is the expression you call `Throw()` on, so `request.Email.Throw()` reports
`request.Email`. Every check takes an optional message, with placeholders:
`IfLongerThan(100, "Keep {PropertyName} under {MaxLength} characters.")`.

### Collect every error

```csharp
var form = new SignUpForm(Name: "", Email: "ada.example.com", Age: 12);

var report = new ValidationReport();
report.Check(form, f => f.Name).IfNullOrWhiteSpace().IfLongerThan(100);
report.Check(form, f => f.Email).IfNullOrWhiteSpace().IfNotEmailAddress();
report.Check(form, f => f.Age).IfLessThan(13, "You must be 13 or older.");

if (!report.IsValid)
{
    Console.WriteLine(report);
}

public sealed record SignUpForm(string Name, string Email, int Age);
```

```text
Validation failed with 3 errors:
 - Name: 'Name' must not be empty or white space.
 - Email: 'Email' must be a valid email address.
 - Age: You must be 13 or older.
```

In ASP.NET Core, `Results.ValidationProblem(report.ToDictionary())` turns the report into
a standard 400 response. With `DotThrow.AspNetCore`, `.WithValidation<T>()` on an endpoint
runs the registered validator before the handler.

### Validator&lt;T&gt;

```csharp
public sealed class OrderValidator : Validator<Order>
{
    public OrderValidator()
    {
        For(x => x.Number, number => number.IfNullOrWhiteSpace().IfNotStartsWith("ORD-"));
        For(x => x.ShippingAddress, address => address.IfNull());
        For(x => x.ShippingAddress, new AddressValidator());
        ForEach(x => x.Tags, tag => tag.IfNullOrWhiteSpace().IfLongerThan(20));
        For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace(), rule => rule.When(x => x.IsGift));
        For(x => x.DeliveryDate, (date, x) => date.IfBefore(x.OrderDate));
    }
}

public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.Street, street => street.IfNullOrWhiteSpace().IfLongerThan(100));
        For(x => x.City, city => city.IfNullOrWhiteSpace());
    }
}

public sealed record Address(string Street, string City);

public sealed record Order(string Number, Address? ShippingAddress, List<string> Tags, bool IsGift, string? GiftMessage, DateTime OrderDate, DateTime DeliveryDate);
```

```csharp
var order = new Order("1001", new Address("12 Analytical Way", ""), ["gift", " "], IsGift: true, GiftMessage: null,
    OrderDate: new DateTime(2026, 5, 2), DeliveryDate: new DateTime(2026, 5, 1));

foreach (var failure in new OrderValidator().Validate(order).Failures)
{
    Console.WriteLine($"{failure.Name}: {failure.Message} [{failure.ErrorCode}]");
}
```

```text
Number: 'Number' must start with 'ORD-'. [NotStartsWith]
ShippingAddress.City: 'City' must not be empty or white space. [WhiteSpace]
Tags[1]: 'Tags' must not be empty or white space. [WhiteSpace]
GiftMessage: 'GiftMessage' must not be null. [Null]
DeliveryDate: 'DeliveryDate' must not be before 2026-05-02. [Before]
```

Validators also have rule options (`WithMessage`, `WithName`, `WithSeverity`,
`WithState`, `DependentRules`), `When` and `Unless` blocks, groups, async rules,
polymorphic and per-object validators, property filters, and test helpers. See
[validation.md](docs/validation.md).

## Checks at a glance

More than 110 checks in 15 families. Every check takes an optional custom `message`. The
[full reference](docs/guards.md#checks-by-family) says what fails, which exception is
thrown, the error code, the placeholders, and which checks need .NET 8.

| Family | Checks |
| --- | --- |
| [Any value](docs/guards.md#any-value) | `IfNull`, `IfNotNull`, `IfDefault`, `IfEqualTo`, `IfNotEqualTo`, `IfOneOf`, `IfNotOneOf`, `If`, `IfNot`, `IfType`, `IfNotType` |
| [Empty values](docs/guards.md#empty-values) | `IfEmpty`, `IfNotEmpty` |
| [Comparisons and ranges](docs/guards.md#comparisons-and-ranges) | `IfGreaterThan`, `IfGreaterThanOrEqualTo`, `IfLessThan`, `IfLessThanOrEqualTo`, `IfOutOfRange`, `IfOutOfRangeExclusive`, `IfInRange`, `IfNegative`, `IfNegativeOrZero`, `IfPositive`, `IfPositiveOrZero`, `IfZero` |
| [Numbers](docs/guards.md#numbers) | `IfNaN`, `IfInfinity`, `IfNotFinite`, `IfOdd`, `IfEven`, `IfNotMultipleOf`, `IfNotPowerOfTwo`, `IfNotInteger`, `IfPrecisionScaleExceeded` |
| [Strings](docs/guards.md#strings) | `IfNullOrEmpty`, `IfNullOrWhiteSpace`, `IfLongerThan`, `IfShorterThan`, `IfLengthNotEqualTo`, `IfLengthOutOfRange`, `IfEqualTo`, `IfNotEqualTo`, `IfContains`, `IfNotContains`, `IfStartsWith`, `IfNotStartsWith`, `IfEndsWith`, `IfNotEndsWith`, `IfMatches`, `IfNotMatches`, `IfNotEmailAddress`, `IfNotJson`, `IfNotXml`, `IfNotUri`, `IfNotHttpUrl`, `IfNotParsable<T>`, `IfAllUpperCase`, `IfAllLowerCase` |
| [String formats and characters](docs/guards.md#string-formats-and-characters) | `IfNotTrimmed`, `IfContainsWhiteSpace`, `IfContainsControlCharacters`, `IfContainsAny`, `IfContainsAnyExcept`, `IfNotAscii`, `IfNotDigits`, `IfNotLettersOrDigits`, `IfNotHex`, `IfNotBase64`, `IfNotGuid`, `IfNotIPAddress`, `IfNotSlug`, `IfNotVersion`, `IfNotCreditCardNumber`, `IfNotEnumName<TEnum>`, `IfNotFileName` |
| [Collections](docs/guards.md#collections) | `IfNullOrEmpty`, `IfCountEqualTo`, `IfCountNotEqualTo`, `IfCountGreaterThan`, `IfCountLessThan`, `IfCountOutOfRange`, `IfContains`, `IfNotContains`, `IfContainsNull`, `IfHasDuplicates`, `IfAny`, `IfNone` |
| [Dictionaries](docs/guards.md#dictionaries) | `IfContainsKey`, `IfNotContainsKey` |
| [Booleans, GUIDs and enums](docs/guards.md#booleans-guids-and-enums) | `IfTrue`, `IfFalse`, `IfEmpty`, `IfNotDefined`, `IfHasFlag`, `IfNotHasFlag` |
| [Dates and times](docs/guards.md#dates-and-times) | `IfBefore`, `IfAfter`, `IfInPast`, `IfInFuture` |
| [Calendar](docs/guards.md#calendar) | `IfNotUtc`, `IfWeekend`, `IfWeekday`, `IfHasTimeOfDay`, `IfOlderThan`, `IfNewerThan` |
| [URIs](docs/guards.md#uris) | `IfRelative`, `IfNotHttps`, `IfNotHttpOrHttps`, `IfLoopback`, `IfNotScheme` |
| [IP addresses](docs/guards.md#ip-addresses) | `IfLoopback`, `IfPrivate`, `IfNotIPv4`, `IfNotIPv6` |
| [Streams](docs/guards.md#streams) | `IfNotReadable`, `IfNotWritable`, `IfNotSeekable` (and `IfEmpty`) |
| [Types](docs/guards.md#types) | `IfNotAssignableTo`, `IfAbstract`, `IfInterface` |

Optional values: `WhenPresent()` lets a null skip the rest of the chain, and unwraps an
`int?`, as in `age.Throw().WhenPresent().IfOutOfRange(0, 150)`. Your own checks are small extension
methods that work in every mode: see [custom-checks.md](docs/custom-checks.md).

Custom exceptions come from a factory:
`amount.Throw(f => new DomainException(f.ErrorCode, f.Message)).IfNegativeOrZero()`.

## ASP.NET Core and dependency injection

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddValidator<SignUp, SignUpValidator>();   // or AddValidatorsFromAssemblyContaining<Program>()

var app = builder.Build();
app.MapPost("/sign-up", (SignUp request) => TypedResults.Ok())
    .WithValidation<SignUp>();   // an invalid request gets a 400 validation problem
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

`DotThrow.DependencyInjection` registers validators (by assembly scanning, or one by one
for trimmed and Native AOT apps) and validates options classes with `ValidateOnStart`.
`DotThrow.AspNetCore` validates minimal API endpoints and route groups, and MVC actions
through a filter or `[ValidateWithDotThrow]`. See [integrations.md](docs/integrations.md).

## Dynamic rules

Rules can be data: JSON, a section of `IConfiguration`, or objects from a database. They
compile once into a normal validator that calls the same checks, so the failures are the
same as from code.

```csharp
using DotThrow.Dynamic;

var definition = RuleSetDefinition.FromJson("""
    {
      "rules": [
        { "property": "Name", "checks": [ "NullOrWhiteSpace", { "check": "LongerThan", "value": 100 } ] },
        { "property": "Age", "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] }
      ]
    }
    """);

var validator = new DynamicValidator<Person>(definition);
var report = validator.Validate(new Person("Ada Lovelace", 16));   // Age: 'Age' must be between 18 and 130.

public sealed record Person(string Name, int Age);
```

Every error in a definition is reported at once, with hints for misspelled names.
`Update` swaps the rules at run time and keeps the old ones when the new ones have errors.
A `PropertyMap<T>` makes it work with Native AOT, and a `CheckRegistry` adds your own
checks by name. With `DotThrow.DependencyInjection`,
`AddDynamicValidator<T>("Validation:Person")` registers rules from `appsettings.json`
and reloads them when the file changes. See [dynamic-rules.md](docs/dynamic-rules.md).

## Analyzers

The DotThrow package includes eleven Roslyn analyzers, and code fixes for nine of them.
They run in `dotnet build` and in the IDE, with nothing else to install (.NET 8 SDK or
later).

| ID | Finds | Severity |
| --- | --- | --- |
| DT0001 | A guard chain with no checks: `name.Throw();` | Warning |
| DT0002 | A manual `if (...) throw` guard that can be one DotThrow check | Info |
| DT0003 | A name argument the compiler fills in anyway | Info |
| DT0004 | A validator with async rules run with `Validate` | Warning |
| DT0005 | An invalid constant regex pattern | Error |
| DT0006 | A constant pattern that can use the regex source generator | Info |
| DT0007 | A check argument that is never valid, such as `IfOutOfRange(10, 5)` | Error |
| DT0008 | `IfBefore(DateTime.Now)` that should be `IfInPast()` | Info |
| DT0009 | `IfNull()` on a value that cannot be null | Warning |
| DT0010 | A validation report that is never read | Warning |
| DT0011 | An unwrapped `int?` chain assigned back to an `int?`, which turns null into 0 | Warning |

Change a severity in `.editorconfig`, for example `dotnet_diagnostic.DT0002.severity = warning`.
See [analyzers.md](docs/analyzers.md).

## Coming from FluentValidation

| FluentValidation | DotThrow |
| --- | --- |
| `class OrderValidator : AbstractValidator<Order>` | `class OrderValidator : Validator<Order>` |
| `RuleFor(x => x.Name).NotEmpty().MaximumLength(50)` | `For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(50))` |
| `RuleFor(x => x.Age).InclusiveBetween(18, 120)` | `For(x => x.Age, age => age.IfOutOfRange(18, 120))` |
| `.WithMessage("...").When(x => x.IsGift)` | `rule => rule.WithMessage("...").When(x => x.IsGift)` |
| `RuleForEach(x => x.Lines).SetValidator(new LineValidator())` | `ForEach(x => x.Lines, new LineValidator())` |
| `RuleSet("Admin", () => ...)` | `Group("Admin", admin => ...)` |
| `result.Errors` | `report.Failures` |
| `FluentValidation.TestHelper` | `DotThrow.Testing` (same method names) |

The checks say what fails (`IfLongerThan`), where FluentValidation says what must be true
(`MaximumLength`). A few defaults differ on purpose: null fails content checks, and a rule
stops at its first failure. [fluentvalidation.md](docs/fluentvalidation.md) maps every
feature and lists every difference, with migration steps.

## Messages and languages

Every default message is a template keyed by its error code, with FluentValidation
placeholder names:

```csharp
MessageTemplates.Set(ErrorCodes.LongerThan, "{PropertyName} is too long: {TotalLength} of {MaxLength} characters.");
MessageTemplates.Set(ErrorCodes.LongerThan, "'{PropertyName}' darf höchstens {MaxLength} Zeichen haben.", new CultureInfo("de"));
```

The message culture follows `CultureInfo.CurrentUICulture` per request or thread, with a
fallback chain (`de-AT`, then `de`, then English). DotThrow ships machine-assisted
translations for 58 languages, the set FluentValidation ships; templates you set always
win over them. Every failure also has a stable error code, such as `LongerThan`, so
clients can translate on their side. See [localization.md](docs/localization.md).

## Performance

Measured with BenchmarkDotNet on an Apple M4 Pro (macOS 27.0.1, .NET 10.0.12, Arm64).
Every input is the same for every library, and the results show the time per check or per
validated object. [performance.md](docs/performance.md) has the method, the full tables
and the techniques behind them.

**Guard clauses**, time per check with valid input. No library allocated memory, except
where noted.

| Check | Hand-written `if` | .NET built-in | DotThrow | Ardalis.GuardClauses | CommunityToolkit.Diagnostics | Throw |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Int in 0..150 | 0.25 ns | 0.32 ns | **0.27 ns** | 0.54 ns | 0.27 ns | 0.56 ns |
| String not blank, max 50 | 0.69 ns | 0.73 ns | **0.70 ns** | 3.29 ns, 24 B | 0.68 ns | 9.39 ns, 96 B |
| List not null or empty, max 10 | 0.41 ns | 0.49 ns | **0.51 ns** | 2.06 ns | 0.44 ns | 3.04 ns |
| HashSet&lt;int&gt; not null or empty, max 10 | 0.40 ns | 0.51 ns | **0.45 ns** | 1.46 ns | 1.75 ns | 23.65 ns, 80 B |

**Validation**, time and memory per validated object.

| Case | FluentValidation 12.1.1 | DotThrow `Validator<T>` |
| --- | ---: | ---: |
| Small model, valid | 202.31 ns, 704 B | **76.27 ns, 40 B** |
| Small model, 4 failures | 1,747.51 ns, 7,712 B | **260.18 ns, 864 B** |
| Nested model (child validators, a collection, a `When` block), valid | 1,322.27 ns, 5,064 B | **224.96 ns, 424 B** |
| Nested model, 5 failures | 3,902.97 ns, 14,256 B | **561.42 ns, 1,856 B** |
| Small model, `ValidateAsync`, valid | 298.60 ns, 776 B | **91.55 ns, 112 B** |
| Small model and an async rule, valid | 462.26 ns, 1,336 B | **114.23 ns, 208 B** |

A DotThrow guard chain costs about as much as the hand-written `if` (at most 0.1 ns more
in these cases) and allocates nothing. `Validator<T>` was 2.7 to 7 times faster than FluentValidation and allocated 6%
to 16% of its memory. Other work was running on the machine, so absolute times may be a
little high.

## How it compares

Facts checked on 2026-10-10 against the latest stable NuGet packages.

| | DotThrow | Ardalis.GuardClauses | Throw | CommunityToolkit.Diagnostics | FluentValidation | .NET built-in |
| --- | --- | --- | --- | --- | --- | --- |
| License | MIT | MIT | MIT | MIT | Apache-2.0 | MIT |
| Style | `x.Throw().IfX()` | `Guard.Against.X(x)` | `x.Throw().IfX()` | `Guard.IsX(x)` | `RuleFor(x => x.P).X()` | `ArgumentException.ThrowIfX(x)` |
| Parameter name filled in | Yes | Yes | Yes | Yes | Yes (property names) | Yes |
| Returns the checked value | Yes | Yes | Yes | No | Not applicable | No |
| Chains several checks | Yes | No | Yes | No | Yes | No |
| Collects every error | Yes | No | No | No | Yes | No |
| Validators with rule sets, conditions, child validators | Yes | No | No | No | Yes | No |
| Async rules | Yes | `InvalidInputAsync` only | No | No | Yes | No |
| Error codes | Yes | No | No | No | Yes | No |
| Message templates per culture | Yes | No | No | No | Yes | No |
| Rules from JSON or configuration | Yes | No | No | No | No | No |
| Analyzers in the package | Yes | No | No | No | No | No |
| Declares Native AOT compatibility | Yes | No | No | Trimming only | No | Part of .NET |
| Dependencies | None | None | OneOf | None on net8.0; System.Memory or Unsafe on netstandard | None | None |
| Targets | netstandard2.0, netstandard2.1, net462, net8.0, net9.0, net10.0 | netstandard2.0, netstandard2.1, net8.0 | net6.0 | netstandard2.0, netstandard2.1, net8.0 | net8.0 | .NET 6 and later |
| Latest release | 2.0.0 (this repository) | 5.0.0, Sep 2024 | 1.4.0, Sep 2023 | 8.4.2, Mar 2026 | 12.1.1, Dec 2025 | Ships with .NET |

The .NET built-in helpers (`ArgumentNullException.ThrowIfNull` and friends, .NET 6 to 8)
cover the basics only: no collection checks, no custom messages, and no collect mode.
Light.GuardClauses (MIT, active) has a very large API in the `value.MustNotBeNull()`
style.

## Supported frameworks

| Package target | Used by | What it adds |
| --- | --- | --- |
| netstandard2.0 | .NET Framework 4.6.1, .NET Core 2.x, Mono, Xamarin, Unity | Every check except the .NET 8 ones below. |
| netstandard2.1 | .NET Core 3.x, .NET 5, 6 and 7 | The same checks as netstandard2.0, with span-based code inside some text checks. |
| net462 | .NET Framework 4.6.2 to 4.8.1 | A direct .NET Framework build, so no netstandard facades are needed. |
| net8.0 | .NET 8 | Generic math overloads (`IfOdd<T>`, `IfNaN<T>`, ...), `IfNotParsable<T>`, `TimeProvider` overloads, `DateOnly` and `TimeOnly` checks, `params ReadOnlySpan<T>` and `SearchValues<char>` overloads, trimming and AOT annotations. |
| net9.0 | .NET 9 | The same API as net8.0. |
| net10.0 | .NET 10 | The same API as net8.0. |

The 5,725 tests of the core package run on .NET 8, 9 and 10 on Linux, macOS and Windows,
on .NET Framework 4.7.2 and 4.8.1, and against the netstandard2.0 and netstandard2.1
builds.

## Trimming and Native AOT

On .NET 8 and later DotThrow is marked `IsAotCompatible`. The checks use no reflection
that trimming or Native AOT cannot handle, validators read property names from the
compiler (`[CallerArgumentExpression]`) instead of expression trees, and the
[samples app](samples/DotThrow.Samples) is published as a Native AOT binary in CI with zero
trimming or AOT warnings.

The APIs that need reflection are marked `[RequiresUnreferencedCode]`, so the compiler
warns where you call them: assembly scanning (`DotThrow.Scanning.AssemblyScanner` and
`AddValidatorsFromAssembly...`) and the reflection constructor of `DynamicValidator<T>`.
In trimmed and Native AOT apps, use `AddValidator` and a `PropertyMap<T>` instead.

## Security

- **No secrets in messages.** Default messages never contain the checked value, and never
  contain a compared value of a reference type (`IfEqualTo`, collection `IfContains`,
  `IfContainsKey`). `{PropertyValue}` appears only in a message you write.
  `ArgumentOutOfRangeException.ActualValue` is set for value types only.
  `ValidationFailure.AttemptedValue` does hold the value, so do not log it as is.
- **XML is safe to check.** `IfNotXml` rejects DTDs and never resolves external entities.
- **Strict parsing.** JSON is checked by DotThrow's own RFC 8259 recognizer, which is
  iterative (deep nesting cannot overflow the stack) and allocates nothing. IP addresses
  and version strings are parsed by DotThrow's own strict code, so the answer is the same
  on every runtime. URI checks reject bare file paths and backslashes.
- **Limits on nesting.** A validator that includes itself is rejected when you build it,
  and nesting deeper than `ValidatorDefaults.MaxDepth` (256) stops with a clear error
  instead of a stack overflow.

To report a security problem, see [SECURITY.md](SECURITY.md).

## Documentation

- [Guard clauses and every check](docs/guards.md)
- [Validation: collect mode and `Validator<T>`](docs/validation.md)
- [DotThrow for FluentValidation users](docs/fluentvalidation.md)
- [Messages and localization](docs/localization.md)
- [Dynamic rules from JSON and configuration](docs/dynamic-rules.md)
- [Dependency injection and ASP.NET Core](docs/integrations.md)
- [Analyzers](docs/analyzers.md)
- [Writing your own checks](docs/custom-checks.md)
- [Performance](docs/performance.md)
- [Migrating from DotThrow 1.x](docs/migrating-from-v1.md)
- [Samples](samples/DotThrow.Samples/Program.cs): a console app that runs every feature and
  prints what happens.

DotThrow 2 is a rewrite of DotThrow 1.x with a new API: `CreateThrower()` becomes
`Throw()`, `ThrowIfX()` becomes `IfX()`, and the static validator registry is replaced by
`Validator<T>`. The [migration guide](docs/migrating-from-v1.md) maps every 1.x check and
validator method.

## Contributing

Issues and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and
the [code of conduct](CODE_OF_CONDUCT.md).

The tests run on several .NET versions. If you do not want to install the .NET 8 and 9
runtimes, run the whole test matrix in Docker with one command:

```shell
eng/test-matrix.sh          # macOS and Linux
pwsh eng/test-matrix.ps1    # Windows
```

To report a security problem, follow [SECURITY.md](SECURITY.md) instead of opening a
public issue.

## License

[MIT](LICENSE)
