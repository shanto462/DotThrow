<img src="assets/icon.png" alt="DotThrow icon" width="72" align="right">

# DotThrow

[![CI](https://github.com/shanto462/DotThrow/actions/workflows/ci.yml/badge.svg)](https://github.com/shanto462/DotThrow/actions/workflows/ci.yml)
[![CodeQL](https://github.com/shanto462/DotThrow/actions/workflows/codeql.yml/badge.svg)](https://github.com/shanto462/DotThrow/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/shanto462/DotThrow/badge)](https://scorecard.dev/viewer/?uri=github.com/shanto462/DotThrow)
[![NuGet](https://img.shields.io/nuget/v/DotThrow.svg)](https://www.nuget.org/packages/DotThrow)
[![NuGet downloads](https://img.shields.io/nuget/dt/DotThrow.svg)](https://www.nuget.org/packages/DotThrow)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Fluent guard clauses and validation for .NET, with one set of checks. As a guard clause,
a chain of checks throws the right exception (`ArgumentNullException`,
`ArgumentOutOfRangeException` or `ArgumentException`) with the parameter name filled in.
In collect mode, the same checks record the errors of every value in a report that you
can return from an API. DotThrow is MIT licensed and has no dependencies. The common
checks allocate nothing when they pass, and the library is safe for trimming and Native
AOT.

## Install

```shell
dotnet add package DotThrow
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
`request.Email`.

### Collect every error

```csharp
var form = new SignUpForm(Name: "", Email: "ada.example.com", Age: 12);

var report = new ValidationReport();
report.Check(form.Name, "Name").IfNullOrWhiteSpace().IfLongerThan(100);
report.Check(form.Email, "Email").IfNullOrWhiteSpace().IfNotEmailAddress();
report.Check(form.Age, "Age").IfLessThan(13, "You must be 13 or older.");

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
a standard 400 response.

### Reusable validators

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

public sealed class OrderValidator : Validator<Order>
{
    public OrderValidator()
    {
        For(x => x.Number, number => number.IfNullOrWhiteSpace().IfNotStartsWith("ORD-"));
        For(x => x.ShippingAddress, address => address.IfNull());
        For(x => x.ShippingAddress, new AddressValidator());
        ForEach(x => x.Tags, tag => tag.IfNullOrWhiteSpace().IfLongerThan(20));
    }
}
```

```csharp
var order = new Order("1001", new Address("12 Analytical Way", ""), ["gift", " "]);

foreach (var failure in new OrderValidator().Validate(order).Failures)
{
    Console.WriteLine($"{failure.Name}: {failure.Message} [{failure.ErrorCode}]");
}
```

```text
Number: 'Number' must start with 'ORD-'. [NotStartsWith]
ShippingAddress.City: 'City' must not be empty or white space. [WhiteSpace]
Tags[1]: 'Tags[1]' must not be empty or white space. [WhiteSpace]
```

## Features

- **Fluent guard clauses** that return the value, so a check and an assignment are one
  line.
- **Automatic names** from the calling code, through `[CallerArgumentExpression]`.
- **The right exception types**: `ArgumentNullException`, `ArgumentOutOfRangeException`
  (with `ActualValue` for value types) or `ArgumentException`, always with `ParamName`.
- **Collect mode**: the same checks fill a `ValidationReport`. `ToDictionary()` gives the
  shape that ASP.NET Core validation problems use.
- **`Validator<T>`** for reusable rules: nested objects, collections, rules about the
  whole object, groups, conditions, async rules, and `IValidator<T>` for dependency
  injection.
- **Stable error codes** on every failure, such as `LongerThan` or `NotEmailAddress`.
- **Custom exceptions** through a factory: `value.Throw(f => new MyException(f.Message))`.
- **Your own checks** as small extension methods that work in every mode.
- **Safe defaults**: string, collection and comparison checks fail a null value, range and
  sign checks fail NaN, default messages never contain the checked string, and the XML
  check rejects DTDs.
- **No dependencies**, trimming and Native AOT safe (`IsAotCompatible` on .NET 8 and
  later). The common checks allocate nothing when they pass; `IfHasDuplicates`,
  `IfNotXml`, `IfNotUri` and `IfNotHttpUrl` do allocate.

## Checks at a glance

Every check takes an optional custom `message`. The [full list](docs/guards.md#checks-by-type)
says what fails, which exception is thrown and which error code is reported.

| Values | Checks |
| --- | --- |
| [Any value](docs/guards.md#any-value) | `IfNull`, `IfNotNull`, `IfDefault`, `IfEqualTo`, `IfNotEqualTo`, `IfOneOf`, `IfNotOneOf`, `If`, `IfNot`, `IfType`, `IfNotType` |
| [Numbers and comparable values](docs/guards.md#numbers-and-other-comparable-values) | `IfGreaterThan`, `IfGreaterThanOrEqualTo`, `IfLessThan`, `IfLessThanOrEqualTo`, `IfOutOfRange`, `IfInRange`, `IfNegative`, `IfNegativeOrZero`, `IfPositive`, `IfPositiveOrZero`, `IfZero`, `IfNaN`, `IfInfinity` |
| [Strings](docs/guards.md#strings) | `IfNullOrEmpty`, `IfNullOrWhiteSpace`, `IfLongerThan`, `IfShorterThan`, `IfLengthNotEqualTo`, `IfLengthOutOfRange`, `IfEqualTo`, `IfNotEqualTo`, `IfContains`, `IfNotContains`, `IfStartsWith`, `IfNotStartsWith`, `IfEndsWith`, `IfNotEndsWith`, `IfMatches`, `IfNotMatches`, `IfNotEmailAddress`, `IfNotJson`, `IfNotXml`, `IfNotUri`, `IfNotHttpUrl`, `IfNotParsable<T>`, `IfAllUpperCase`, `IfAllLowerCase` |
| [Collections](docs/guards.md#collections) | `IfNullOrEmpty`, `IfCountEqualTo`, `IfCountNotEqualTo`, `IfCountGreaterThan`, `IfCountLessThan`, `IfCountOutOfRange`, `IfContains`, `IfNotContains`, `IfContainsNull`, `IfHasDuplicates` |
| [Booleans, GUIDs and enums](docs/guards.md#booleans-guids-and-enums) | `IfTrue`, `IfFalse`, `IfEmpty`, `IfNotDefined` |
| [Dates and times](docs/guards.md#dates-and-times) | `IfBefore`, `IfAfter`, `IfInPast`, `IfInFuture` |

Optional values: `AllowNull()` lets a null skip the rest of the chain, as in
`middleName.Throw().AllowNull().IfLongerThan(50)`.

## Custom exceptions

```csharp
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class Account
{
    public decimal Balance { get; private set; }

    public void Withdraw(decimal amount)
    {
        amount.Throw(failure => new DomainException(failure.ErrorCode, failure.Message))
            .IfNegativeOrZero()
            .IfGreaterThan(Balance, "The amount must not be more than the balance.");

        Balance -= amount;
    }
}
```

## Custom checks

A check is an extension method on `Guard<T>`. It works in guard clauses, in collect mode
and in validators, like the built-in checks.

```csharp
public static class SkuChecks
{
    public static Guard<string> IfNotSku(this Guard<string> guard, string? message = null)
    {
        if (!guard.IsActive)
        {
            return guard;   // an earlier check failed, or AllowNull() saw null
        }

        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        return guard.Value.StartsWith("SKU-", StringComparison.Ordinal)
            ? guard
            : guard.Fail("NotSku", message ?? $"'{guard.Name}' must start with 'SKU-'.");
    }
}
```

See [Writing your own checks](docs/custom-checks.md) for the rules and more examples.

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
| Reusable validators with rule sets | Yes (groups) | No | No | No | Yes | No |
| Async rules | Yes | `InvalidInputAsync` only | No | No | Yes | No |
| Error codes | Yes | No | No | No | Yes | No |
| Declares Native AOT compatibility | Yes | No | No | Trimming only | No | Part of .NET |
| Dependencies | None | None | OneOf | None on net8.0; System.Memory or Unsafe on netstandard | None | None |
| Targets | netstandard2.0, net8.0, net10.0 | netstandard2.0, netstandard2.1, net8.0 | net6.0 | netstandard2.0, netstandard2.1, net8.0 | net8.0 | .NET 6 and later |
| Latest release | 2.0.0 (this repository) | 5.0.0, Sep 2024 | 1.4.0, Sep 2023 | 8.4.2, Mar 2026 | 12.1.1, Dec 2025 | Ships with .NET |

- The .NET built-in helpers (`ArgumentNullException.ThrowIfNull` and friends, .NET 6 to 8)
  cover the basics only: no collection checks, no custom messages, and no collect mode.
- Light.GuardClauses (MIT, active) has a very large API in the `value.MustNotBeNull()`
  style.
- Some older libraries are no longer maintained: Dawn.Guard (archived, NuGet package
  deprecated), Ensure.That (no activity since 2021 to 2022) and Validot (archived in
  2025).

## Performance

DotThrow next to the other libraries, on an Apple M4 Pro with .NET 10.0.12
(BenchmarkDotNet 0.15.8). [Performance](docs/performance.md) explains the method and has
the full tables.

**Guard clauses**, time per check with valid input. No library allocated memory, except
where noted.

| Check | Hand-written `if` | .NET built-in | DotThrow | Ardalis.GuardClauses | CommunityToolkit.Diagnostics | Throw |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Int in 0..150 | 0.25 ns | 0.30 ns | 0.61 ns | 0.74 ns | 0.25 ns | 0.51 ns |
| String not blank, max 50 | 0.59 ns | 0.62 ns | 1.62 ns | 2.86 ns, 24 B | 0.59 ns | 8.68 ns, 96 B |
| List not null or empty, max 10 | 0.37 ns | 0.39 ns | 1.90 ns | 2.05 ns | 0.36 ns | 0.97 ns |

**Validation** of a small model with four rules.

| Input | FluentValidation | DotThrow `Validator<T>` | DotThrow `ValidationReport` |
| --- | ---: | ---: | ---: |
| Valid | 159.85 ns, 704 B | 53.82 ns, 56 B | 43.94 ns, 56 B |
| Invalid, 4 failures | 1,672.67 ns, 7,712 B | 112.80 ns, 704 B | 98.95 ns, 752 B |

A DotThrow guard chain cost 0.6 to 1.9 ns per check here and allocated nothing. Plain static
helpers, such as the .NET built-ins and CommunityToolkit.Diagnostics, are faster still.
For validation, DotThrow was about 3 times faster than FluentValidation on valid input
and about 15 times faster on invalid input, with far less memory.

## Supported frameworks

| Package target | Used by | Notes |
| --- | --- | --- |
| netstandard2.0 | .NET Framework 4.6.1 and later, .NET Core 2.0 and later, Mono, Unity | Every check except the ones below. |
| net8.0 | .NET 8 and .NET 9 | Adds `IfNotParsable<T>()`, the `TimeProvider` overloads, and the `DateOnly` and `TimeOnly` checks. Declared trimming and Native AOT compatible. |
| net10.0 | .NET 10 | Same API as net8.0. |

## Documentation

- [Guard clauses and every check](docs/guards.md)
- [Validation: collect mode and `Validator<T>`](docs/validation.md)
- [Writing your own checks](docs/custom-checks.md)
- [Performance](docs/performance.md)
- [Migrating from DotThrow 1.x](docs/migrating-from-v1.md)
- [Samples](samples/DotThrow.Samples/Program.cs): a console app that runs every feature and
  prints what happens.

## Migrating from 1.x

DotThrow 2 is a rewrite with a new API: `CreateThrower()` becomes `Throw()`, `ThrowIfX()`
becomes `IfX()`, checks throw the right exception types, and the static validator
registry is replaced by `Validator<T>` instances. The
[migration guide](docs/migrating-from-v1.md) maps every 1.x check and validator method.

## Contributing

Issues and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and
the [code of conduct](CODE_OF_CONDUCT.md). To report a security problem, follow
[SECURITY.md](SECURITY.md) instead of opening a public issue.

## License

[MIT](LICENSE)
