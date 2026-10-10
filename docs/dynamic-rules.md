# Dynamic rules

Sometimes the rules are data, not code: a tenant sets its own limits, an admin changes a
maximum length without a deploy, or the rules live in a database. `DotThrow.Dynamic` reads
rules from JSON, from `IConfiguration` or from objects you build, and compiles them once
into a normal `Validator<T>`. The rules call the same checks as code, so a failure from a
dynamic rule is the same as a failure from code: same name, message, error code and
exception.

The examples assume `using DotThrow;` and `using DotThrow.Dynamic;`.

- [A first dynamic validator](#a-first-dynamic-validator)
- [The definition](#the-definition)
- [Checks you can use](#checks-you-can-use)
- [Errors in a definition](#errors-in-a-definition)
- [Rules from configuration](#rules-from-configuration)
- [Hot reload with Update](#hot-reload-with-update)
- [Native AOT and trimming: PropertyMap](#native-aot-and-trimming-propertymap)
- [Custom checks: CheckRegistry](#custom-checks-checkregistry)
- [Mixing code and data](#mixing-code-and-data)
- [Performance](#performance)

## A first dynamic validator

```csharp
var definition = RuleSetDefinition.FromJson("""
    {
      "rules": [
        { "property": "Name", "checks": [ "NullOrWhiteSpace", { "check": "LongerThan", "value": 100 } ] },
        { "property": "Email", "checks": [ "NullOrWhiteSpace", "NotEmailAddress" ] },
        { "property": "Age", "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] },
        { "property": "Tags[]", "checks": [ { "check": "LongerThan", "value": 20 } ] },
        { "property": "Address.City", "checks": [ "NullOrWhiteSpace" ] }
      ]
    }
    """);

var validator = new DynamicValidator<Customer>(definition);

var customer = new Customer("Ada Lovelace", "ada.example.com", 16, ["math", "a-tag-that-is-far-too-long"], new Address(""));
foreach (var failure in validator.Validate(customer).Failures)
{
    Console.WriteLine($"{failure.Name}: {failure.Message} [{failure.ErrorCode}]");
}

public sealed record Address(string City);

public sealed record Customer(string Name, string Email, int Age, List<string> Tags, Address? Address);
```

```text
Email: 'Email' must be a valid email address. [NotEmailAddress]
Age: 'Age' must be between 18 and 130. [OutOfRange]
Tags[1]: 'Tags' must be at most 20 characters long. [LongerThan]
Address.City: 'Address.City' must not be empty or white space. [WhiteSpace]
```

A `DynamicValidator<T>` is an `IValidator<T>`: it has `Validate`, `ValidateAsync`,
`ValidateAndThrow`, groups, options and `Describe()`, and it works with the
[test helpers](validation.md#testing-validators) and with
[dependency injection](integrations.md). It is thread-safe; build it once and share it.

This constructor finds the properties with reflection. For trimmed and Native AOT apps,
pass a [`PropertyMap<T>`](#native-aot-and-trimming-propertymap) instead.

## The definition

A definition is three plain classes. They bind from JSON (`RuleSetDefinition.FromJson`),
from `IConfiguration` (`section.Get<RuleSetDefinition>()`) or from code. Every argument is
text, the way configuration gives it, and is converted with the invariant culture when the
validator is built. In JSON, keys ignore case, a number and the same number in quotes are
the same, and a check without arguments can be written as its name alone (`"NotEmailAddress"`).

**`RuleSetDefinition`**

| Key | Meaning |
| --- | --- |
| `rules` | The rules, in the order they run. |
| `ruleCascade` | `"Stop"` (default) or `"Continue"`, see [Cascade](validation.md#cascade). |
| `classCascade` | `"Continue"` (default) or `"Stop"`. |

**`RuleDefinition`**

| Key | Meaning |
| --- | --- |
| `property` | The property path: `"Email"`, `"Address.City"`. A null part on the way reads as null, like `?.`. `"Lines[].Sku"` checks a property of every item (failures named `Lines[2].Sku`); `"Tags[]"` checks every item (`Tags[1]`). Paths nest: `"Lines[].Notes[]"`. Empty means the object itself. Names ignore case. |
| `each` | `true` runs the checks on every item, like `"Tags[]"`. |
| `allowNull` | `true` lets a null value pass, like `WhenPresent()`. |
| `group`, `groups` | The [groups](validation.md#groups) of the rule. |
| `when`, `unless` | A condition, see below. |
| `severity` | `"Error"` (default), `"Warning"` or `"Info"`. |
| `errorCode` | The error code of every failure of the rule. |
| `message` | The message of every check of the rule, with [placeholders](localization.md#placeholders). A check message wins. |
| `name` | The name in messages, like `WithName`. |
| `cascade` | `"Stop"` or `"Continue"` for this rule. |
| `checks` | The checks, in order. |

**`CheckDefinition`**

| Key | Meaning |
| --- | --- |
| `check` | The check name: the error code of the check, such as `"LongerThan"` or `"NotEmailAddress"`. |
| `value` | The one argument of checks such as `LongerThan`, `GreaterThan`, `StartsWith` or `Matches`. |
| `min`, `max` | The range of `OutOfRange`, `LengthOutOfRange`, `CountOutOfRange` and similar checks. |
| `values` | A list, for `OneOf`, `NotOneOf`, `NotScheme`, and `[precision, scale]` of `PrecisionScaleExceeded`. |
| `comparison` | A `StringComparison` name for string checks, such as `"OrdinalIgnoreCase"`. |
| `options` | Extra options: `RegexOptions` names for `Matches`, a `UriKind` for `NotUri`, a culture for `NotParsable`, `IgnoreCase` for `NotEnumName`, `IgnoreTrailingZeros` for `PrecisionScaleExceeded`. |
| `message` | The message of this check. |
| `errorCode` | The error code of this check. |

**`ConditionDefinition`** (`when` and `unless`)

| Key | Meaning |
| --- | --- |
| `property` | The property to look at. |
| `equalTo`, `notEqualTo` | A value, converted to the property type. |
| `isNull` | `true` or `false`. |

Every part you give must hold. For example, check a gift message only for gifts:

```json
{ "property": "GiftMessage", "when": { "property": "IsGift", "equalTo": "true" }, "checks": [ "NullOrWhiteSpace" ] }
```

**Nullable values.** A null `int?` fails the number checks with the code `Null`, like
`IfNull().IfNegative()` in code. Set `"allowNull": true` to let null pass. A property after
a part that can be null is nullable too: in `"Customer.Age"`, `Age` is null when
`Customer` is null.

**Regular expressions** from a definition get a match timeout of one second, because the
pattern is data. A pattern that runs longer throws `RegexMatchTimeoutException`.

## Checks you can use

Every built-in check can be used by its error code, except the ones that need code:
`Condition` (`If`, `IfNot`), `Any`, `None`, `Must` and `Invalid`. `CheckRegistry.BuiltInChecks`
lists the names.

Arguments: V = `value`, Min and Max = `min` and `max`, Vs = `values`, C = `comparison`,
O = `options`.

| Checks | Arguments | Property types |
| --- | --- | --- |
| `Null`, `NotNull`, `Default`, `Empty`, `NotEmpty` | none | any |
| `EqualTo`, `NotEqualTo` | V (strings: and C) | any type with a text form |
| `OneOf`, `NotOneOf` | Vs (strings: and C) | any type with a text form |
| `Type`, `NotType` | V = type name | any |
| `GreaterThan`, `GreaterThanOrEqualTo`, `LessThan`, `LessThanOrEqualTo` | V | comparable types: numbers, dates, `TimeSpan`, `char`, `string`, `Version`, `Guid`, and your own `IComparable<T>` types |
| `OutOfRange`, `OutOfRangeExclusive`, `InRange` | Min, Max | comparable types |
| `Negative`, `NegativeOrZero`, `Positive`, `PositiveOrZero`, `Zero` | none | comparable value types |
| `NaN`, `Infinity`, `NotFinite` | none | `double`, `float` (and `Half` on .NET 8+) |
| `Odd`, `Even`, `NotPowerOfTwo` | none | `int`, `long` (every integer type on .NET 8+) |
| `NotMultipleOf` | V (not 0) | `int`, `long`, `decimal` (every integer type on .NET 8+) |
| `NotInteger` | none | `double`, `float`, `decimal` |
| `PrecisionScaleExceeded` | Vs = [precision, scale], O = `IgnoreTrailingZeros` | `decimal` |
| `NullOrEmpty` | none | strings, collections |
| `WhiteSpace` (or `NullOrWhiteSpace`) | none | strings |
| `LongerThan`, `ShorterThan`, `LengthNotEqualTo` | V (a number) | strings |
| `LengthOutOfRange` | Min, Max | strings |
| `Contains`, `NotContains` | V (strings: and C) | strings, collections |
| `StartsWith`, `NotStartsWith`, `EndsWith`, `NotEndsWith` | V, C | strings |
| `Matches`, `NotMatches` | V = pattern, O = `RegexOptions` | strings |
| `NotUri` | O = `UriKind` (default `Absolute`) | strings |
| `ContainsAny`, `ContainsAnyExcept` | V = the characters | strings |
| `NotEnumName` | V = enum type name, O = `IgnoreCase` | strings |
| `NotParsable` (.NET 8+) | V = type name (`int`, `Guid`, `DateOnly`...), O = culture name | strings |
| `NotEmailAddress`, `NotJson`, `NotXml`, `NotHttpUrl`, `AllUpperCase`, `AllLowerCase`, `NotTrimmed`, `ContainsWhiteSpace`, `ContainsControlCharacters`, `NotAscii`, `NotDigits`, `NotLettersOrDigits`, `NotHex`, `NotBase64`, `NotGuid`, `NotIPAddress`, `NotSlug`, `NotVersion`, `NotCreditCardNumber`, `NotFileName` | none | strings |
| `ContainsNull`, `HasDuplicates` | none | collections |
| `CountEqualTo`, `CountNotEqualTo`, `CountGreaterThan`, `CountLessThan` | V (a number, 0 or more) | collections |
| `CountOutOfRange` | Min, Max | collections |
| `ContainsKey`, `NotContainsKey` | V (converted to the key type) | dictionaries |
| `True`, `False` | none | `bool` |
| `NotDefined` | none | enums |
| `HasFlag`, `NotHasFlag` | V (`"Read, Write"`) | enums |
| `Before`, `After` | V | `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` |
| `InPast`, `InFuture`, `Weekend`, `Weekday` | none | `DateTime`, `DateTimeOffset`, `DateOnly` |
| `NotUtc`, `HasTimeOfDay` | none | `DateTime`, `DateTimeOffset` |
| `OlderThan`, `NewerThan` | V = a `TimeSpan` (`d.hh:mm:ss`) | `DateTime`, `DateTimeOffset` |
| `Relative`, `NotHttps`, `NotHttpOrHttps` | none | `Uri` |
| `NotScheme` | Vs = schemes | `Uri` |
| `Loopback` | none | `Uri`, `IPAddress` |
| `Private`, `NotIPv4`, `NotIPv6` | none | `IPAddress` |
| `NotReadable`, `NotWritable`, `NotSeekable` | none | `Stream` |
| `NotAssignableTo` | V = type name | `Type` |
| `Abstract`, `Interface` | none | `Type` |

[guards.md](guards.md#checks-by-family) says what each check does. Dates are written in the
round-trip format (`2026-05-02` or `2026-05-02T10:30:00Z`). For type names, prefer full
names (`System.Int32`).

## Errors in a definition

A definition is checked completely when the validator is built. Every problem is
collected, with its place and, for a misspelled name, a hint. A
`RuleDefinitionException` lists them all in `Errors`:

```csharp
var broken = RuleSetDefinition.FromJson("""
    {
      "rules": [
        { "property": "Emial", "checks": [ "NotEmailAddress" ] },
        { "property": "Age", "checks": [ { "check": "OutOfRange", "min": 130, "max": 18 } ] },
        { "property": "Name", "checks": [ "NotEmail" ] }
      ]
    }
    """);

try
{
    _ = new DynamicValidator<Customer>(broken);
}
catch (RuleDefinitionException exception)
{
    foreach (var error in exception.Errors)
    {
        Console.WriteLine(error);
    }
}
```

```text
Rules[0].Property: 'Emial' is not a property of Customer (did you mean 'Email'?)
Rules[1].Checks[0]: check 'OutOfRange' on 'Age': 'Min' is greater than 'Max'
Rules[2].Checks[0]: check 'NotEmail' on 'Name': is not a known check
```

`FromJson` reports JSON and shape errors (bad syntax, an unknown key, a value of the wrong
kind) with line and column. `validator.FindErrors(definition)` returns the errors of a
definition without building anything, for example to check a change in an admin screen.

## Rules from configuration

Rules can live in `appsettings.json` or any other `IConfiguration` source, in the same
shape as the JSON above:

```json
{
  "Validation": {
    "Customer": {
      "rules": [
        { "property": "Name", "checks": [ "WhiteSpace", { "check": "LongerThan", "value": 100 } ] },
        { "property": "Age", "severity": "Warning", "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] }
      ]
    }
  }
}
```

### Dependency injection with reload

`DotThrow.DependencyInjection` registers a dynamic validator as a singleton
`IValidator<T>` (and `DynamicValidator<T>`), one per type. Pick the source of the rules:

| Source | Reflection | Trimming and Native AOT |
| --- | --- | --- |
| A configuration section | `AddDynamicValidator<T>(section)` | `AddDynamicValidator(section, properties)` |
| A section path of the app's configuration | `AddDynamicValidator<T>("Validation:Customer")` | `AddDynamicValidator("Validation:Customer", properties)` |
| A fixed definition (code, a database, `FromJson`) | `AddDynamicValidator<T>(definition)` | `AddDynamicValidator(definition, properties)` |

`properties` is a [`PropertyMap<T>`](#native-aot-and-trimming-propertymap).

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDynamicValidator(
    "Validation:Customer",
    new PropertyMap<Customer>().Add(x => x.Name).Add(x => x.Age),
    o =>
    {
        o.CodeRules = v => v.For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
        o.OnReloadError = (error, services) =>
            services.GetRequiredService<ILogger<Customer>>().LogError(error, "The Customer rules were not reloaded.");
    });
```

- **Reload.** When the configuration changes (for example `reloadOnChange: true` on a JSON
  file), the rules are read again and swapped in one step. A change with errors is not
  used: the old rules stay, and `OnReloadError` gets every error (without it, they go to
  `Trace.TraceError`). One bad edit may be reported two or three times, because
  configuration raises several change notifications for one save. A fixed definition does
  not reload; call `Update` on the resolved `DynamicValidator<T>`.
- **ValidateOnStart** (on by default). With a host (Microsoft.Extensions.Hosting 8 or
  later), a definition with errors stops the app at startup with an
  `OptionsValidationException` that lists every error. Without a host, the validator is
  built when it is first resolved.
- **CodeRules** adds rules written in code, which run before the dynamic rules. The
  registered `IValidator<T>` is then a `Validator<T>` that includes the dynamic validator;
  a reload changes only the dynamic part. **Checks** sets a
  [`CheckRegistry`](#custom-checks-checkregistry).
- **Null values.** The section is read with `RuleSetConfiguration.Read`, without the
  configuration binder, so it is safe for trimming and Native AOT and accepts the short form
  `"WhiteSpace"`. Configuration stores a JSON `null` as an empty string, so a `null` or
  empty value reads as "not set".

### Without dependency injection

`RuleSetConfiguration.Read(section)` gives you the definition:

```csharp
using DotThrow.DependencyInjection;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

var definition = RuleSetConfiguration.Read(configuration.GetSection("Validation:Customer"));
var validator = new DynamicValidator<Customer>(definition);
```

## Hot reload with Update

`Update(definition)` builds the new rules and swaps them in one step. Runs that already
started finish with the old rules. When the new definition has errors, `Update` throws a
`RuleDefinitionException` and keeps the old rules; `TryUpdate` returns the errors instead.
`AddDynamicValidator` does this for you; without it, watch the configuration yourself:

```csharp
using DotThrow.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();
var section = configuration.GetSection("Validation:Customer");
var validator = new DynamicValidator<Customer>(RuleSetConfiguration.Read(section));

ChangeToken.OnChange(section.GetReloadToken, () =>
{
    try
    {
        validator.Update(RuleSetConfiguration.Read(section));
    }
    catch (RuleDefinitionException exception)
    {
        Console.Error.WriteLine("The old rules stay: " + string.Join("; ", exception.Errors));
    }
});
```

`validator.Definition` returns the definition that is in use. Changing a definition object
after it was used has no effect until you call `Update` with it.

## Native AOT and trimming: PropertyMap

The reflection constructor is marked `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`, so a trimmed or Native AOT app gets a warning where it calls it.
Such apps list the properties that definitions may check in a `PropertyMap<T>`. It uses no
reflection, and a definition that names any other property fails to build.

```csharp
var properties = new PropertyMap<Customer>()
    .Add(x => x.Name)
    .Add(x => x.Email)
    .Add(x => x.Age)
    .AddCollection(x => x.Tags)
    .Add(x => x.Address?.City);

var definition = RuleSetDefinition.FromJson("""
    { "rules": [ { "property": "Address.City", "checks": [ "NullOrWhiteSpace" ] } ] }
    """);

var validator = new DynamicValidator<Customer>(definition, properties);
```

| Method | Use |
| --- | --- |
| `Add(x => x.Email)` | A property. The path comes from the getter text: `x => x.Address?.City` is `Address.City`. |
| `Add("Name", x => x.Name.Trim())` | A computed value, with the path you give. |
| `AddCollection(x => x.Lines, lines => lines.Add(l => l.Sku))` | A collection, for `"Lines[]"` and, with the item map, `"Lines[].Sku"`. |
| `AddEnum(x => x.Status)` | An enum property (plain or nullable), so enum checks and values work. |
| `AddComparable(x => x.Size, text => ShoeSize.Parse(text))` | Your own `IComparable<T>` type, with a parser for the arguments. |
| `AddEnumType<TEnum>()`, `AddType(type)` | Types that `NotEnumName`, `Type`, `NotType` and `NotAssignableTo` may name. |
| `Paths` | Every path the map knows. |

## Custom checks: CheckRegistry

Definitions can use your own checks by name. Add them to a `CheckRegistry` and pass it to
the validator:

```csharp
var checks = new CheckRegistry()
    .Add<string>("NotSku", (sku, arguments) => sku.IfNotMatches("^SKU-[0-9]+$", message: arguments.Message));

var withSku = RuleSetDefinition.FromJson("""
    { "rules": [ { "property": "Sku", "checks": [ "NotSku" ] } ] }
    """);

var validator = new DynamicValidator<OrderLine>(withSku, checks);

public sealed record OrderLine(string Sku, int Quantity);
```

- A check gets the guard of the value and a `CheckArguments` with the arguments of the
  definition (`Value`, `Min`, `Max`, `Values`, `Comparison`, `Options`, `Message`,
  `ErrorCode`). `CheckArguments.Parse<T>(text)` converts an argument like DotThrow does.
- `Add<T>(name, arguments => check)` reads the arguments once, when the validator is built,
  so a bad argument is a build error and nothing is parsed per validation.
- A check is found by its name and the exact property type; a check added for `object`
  works for every type. Names ignore case and cannot be built-in names.
- `CheckRegistry.Default` (used when you pass none) has only the built-in checks and is
  read-only.

## Mixing code and data

A dynamic validator is an `IValidator<T>`, so code validators can include it, and it can
run as a child or for every item of a collection:

```csharp
var dataRules = new DynamicValidator<Customer>(RuleSetDefinition.FromJson("""
    { "rules": [ { "property": "Age", "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] } ] }
    """));

var customerRules = new Validator<Customer>()
    .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress())
    .Include(dataRules);
```

The dynamic rules then run inside the same report, with the same groups and options.

## Performance

A definition is compiled once into typed delegates that call the guard methods. Validating
uses no reflection, no expression trees and no boxing; a valid run allocates only the
report, the same as code. Measured with the core-validation benchmark
(`verify/CoreC.Bench`, BenchmarkDotNet, Apple M4 Pro, .NET 10, in-process), for a model with
4 rules and 7 checks:

| Case | Code `Validator<T>` | Dynamic, `PropertyMap` | Dynamic, reflection |
| --- | ---: | ---: | ---: |
| Valid | 62 ns, 40 B | 76 ns, 40 B | 80 ns, 40 B |
| Invalid, 5 failures | 246 ns, 864 B | 288 ns, 864 B | 285 ns, 864 B |

The extra time is one delegate call per check. Building costs a few microseconds, once:
`FromJson` about 2.7 microseconds and 16 KB, a build with a `PropertyMap` about 1.8
microseconds and 9.1 KB, with reflection about 5.1 microseconds and 13.5 KB.
