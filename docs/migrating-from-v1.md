# Migrating from DotThrow 1.x

DotThrow 2 is a rewrite. The idea is the same (chain checks on a value), but the names,
the exceptions and the validator changed. There is no compatibility layer, so plan to
change every call. This page maps every 1.x check and validator method to its 2.x form
and lists the behavior changes.

## The short version

- One namespace: replace `using DotThrow.ExceptionExtensions;`, `using DotThrow.Builder;`
  and `using DotThrow.Throwble;` with `using DotThrow;`.
- `value.CreateThrower()` becomes `value.Throw()`.
- `ThrowIfX()` becomes `IfX()`, and some checks have new names (tables below).
- Checks throw `ArgumentNullException`, `ArgumentOutOfRangeException` or
  `ArgumentException` with `ParamName` set, instead of plain `Exception`.
- The static `Validator<T>.AddRule(...)` registry is gone. Create a `Validator<T>` instance
  and keep it, for example in a static field or as a singleton in dependency injection.
- The predicate of a validator rule is inverted: v1 `AddRule` took "true means broken",
  v2 `Must` takes "true means valid".
- No more Newtonsoft.Json dependency. DotThrow 2 has no dependencies.

Before (1.x):

<!-- snippet: skip -->
```csharp
using DotThrow.ExceptionExtensions;

name.CreateThrower()
    .ThrowIfNullOrWhiteSpace()
    .ThrowIf(n => n.Length > 50, "Name is too long.");
```

After (2.x):

```csharp
var name = "Ada Lovelace";

name.Throw()
    .IfNullOrWhiteSpace()
    .IfLongerThan(50, "Name is too long.");
```

## Entry points

| 1.x | 2.x | Notes |
| --- | --- | --- |
| `using DotThrow.ExceptionExtensions;` | `using DotThrow;` | Everything public is in the `DotThrow` namespace. |
| `using DotThrow.Builder;`, `using DotThrow.Throwble;` | `using DotThrow;` | |
| `value.CreateThrower()` | `value.Throw()` | The parameter name is filled in for you. `value.Throw(factory)` throws your own exception type. |
| `ExceptionThrower<T>` | `Guard<T>` | `Guard<T>` is a `ref struct`: it cannot be stored in a field. It converts to `T`, so `Name = name.Throw().IfNullOrWhiteSpace();` works. |

## String checks

| 1.x | 2.x | Notes |
| --- | --- | --- |
| `ThrowIfNullOrWhiteSpace()` | `IfNullOrWhiteSpace()` | v1 threw `ArgumentNullException` for empty and white-space strings too, with the message in `ParamName`. v2 throws `ArgumentNullException` for null and `ArgumentException` for empty or white space. |
| `ThrowIfNullOrEmpty()` | `IfNullOrEmpty()` | Same change as above. |
| `ThrowIfAllUpper()` | `IfAllUpperCase()` | v2 ignores digits and symbols and needs at least one letter: `"ABC1"` now fails, `""` now passes. |
| `ThrowIfAllLower()` | `IfAllLowerCase()` | Same change as above. |
| `ThrowIf(predicate, message)` | `If(predicate, message)` | Throws `ArgumentException` with the code `Condition`. |
| `ThrowIfNot(predicate)` | `IfNot(predicate)` | Now also takes a `message`. |
| `ThrowIfNotValidJson()` | `IfNotJson()` | See [JSON](#json-and-xml). |
| `ThrowIfNotValidXml()` | `IfNotXml()` | See [JSON and XML](#json-and-xml). |
| `ThrowIfNotValidInteger()` | `IfNotParsable<int>()` | .NET 8 and later. It uses the invariant culture unless you pass a provider; v1 used the current culture. On older frameworks use `IfNot(s => int.TryParse(s, out _))`, or `IfNotDigits()` when only the digits 0 to 9 are allowed. |

## Number checks

v1 had these for `int` only. In v2 they work on every number type, and they throw
`ArgumentOutOfRangeException` instead of `ArgumentException`.

| 1.x | 2.x | Notes |
| --- | --- | --- |
| `ThrowIfZero()` | `IfZero()` | |
| `ThrowIfLessThanZero()` | `IfNegative()` | |
| `ThrowIfLessThanOrEqualZero()` | `IfNegativeOrZero()` | |
| `ThrowIfGreaterThanZero()` | `IfPositive()` | |
| `ThrowIfGreaterThanOrEqualZero()` | `IfPositiveOrZero()` | v1 had a bug: it threw only for negative numbers. v2 fails for zero and positive numbers, as the name says. |
| `ThrowIf(predicate, message)` | `If(predicate, message)` | |
| `ThrowIfEqual(value)` | `IfEqualTo(value)` | Throws `ArgumentException`. |

## Collection checks

| 1.x | 2.x | Notes |
| --- | --- | --- |
| `ThrowIfEmpty()` | `IfNullOrEmpty()` | A null collection now fails with `ArgumentNullException`. `IfEmpty()` works too, on any type. |
| `ThrowIf(predicate, message)` | `If(predicate, message)` | |
| `ThrowIfCountIsGreaterThan(count)` | `IfCountGreaterThan(count)` | |
| `ThrowIfCountIsLessThan(count)` | `IfCountLessThan(count)` | |
| `ThrowIfCountIsEqualTo(count)` | `IfCountEqualTo(count)` | v1 had a wrong message ("less than"). |

v1 counted every item of a sequence. v2 uses `Count` when the collection has one, and
otherwise stops as soon as the answer is known.

## Date checks

The date checks throw `ArgumentOutOfRangeException` now (`If` throws `ArgumentException`).

| 1.x | 2.x | Notes |
| --- | --- | --- |
| `ThrowIfOlderThan(date)` | `IfBefore(date)` | |
| `ThrowIfAfterThan(date)` | `IfAfter(date)` | |
| `ThrowIf(predicate, message)` | `If(predicate, message)` | |
| `ThrowIfOlderThanNow()` | `IfInPast()` | A UTC value is now compared with `DateTime.UtcNow`; v1 always used `DateTime.Now`. On .NET 8 and later you can pass a `TimeProvider`. |
| `ThrowIfAfterThanNow()` | `IfInFuture()` | Same as above. |
| `ThrowIfEquals(date)` | `IfEqualTo(date)` | Throws `ArgumentException`. |

## Validator

v1 kept rules in a static registry per type. v2 validators are objects: you build one,
keep it, and use it from any thread.

Before (1.x):

<!-- snippet: skip -->
```csharp
using DotThrow.Builder;

// true means "the rule is broken"
Validator<string>.AddRule(s => s.Length > 50, "The name is too long.", typeof(ArgumentException), "The name length is fine.", "names");

Validator<string>.Verify(name, "names");                       // throws on the first broken rule
var reports = Validator<string>.VerifyNotThrow(name, "names");  // one item per rule
foreach (var item in reports)
{
    Console.WriteLine(item);   // "Passed: ..." or "Failed: ..."
}
```

After (2.x):

```csharp
var name = "Ada Lovelace";

// true means "the value is valid"
var nameRules = new Validator<string>()
    .Must(s => s.Length <= 50, "The name is too long.", rule => rule.InGroup("names"));

nameRules.ValidateAndThrow(name, "names");               // throws one exception with every failure
ValidationReport report = nameRules.Validate(name, "names");
foreach (var failure in report.Failures)                  // failures only
{
    Console.WriteLine(failure);
}
```

For properties, prefer `For` with checks over `Must`: you get the error codes and the
default messages without extra work. See [validation.md](validation.md).

| 1.x | 2.x | Notes |
| --- | --- | --- |
| `Validator<T>.AddRule(predicate, message, ...)` | `new Validator<T>().Must(predicate, message)` | **The predicate is inverted.** Rewrite `x => broken` as `x => !broken` (or the opposite comparison). |
| `exceptionType` (`typeof(SomeException)`) | none | `ValidateAndThrow` throws `ValidationFailedException`. Catch it and wrap it if you need another type. In guard clauses, `Throw(factory)` creates any exception. |
| `passMessage` | none | Reports list only failures. |
| `group` | `rule => rule.InGroup("name")`, or a `Group("name", v => ...)` block | Rules without a group are in `ValidationGroups.Default`. `ValidationGroups.All` runs every rule. v1 treated `""` as "no group"; in v2 pass no group at all. |
| `Validator<T>.Verify(value, group)` | `validator.ValidateAndThrow(value, group)` | v1 threw on the first broken rule; v2 runs every rule and throws once with all failures. |
| `Validator<T>.VerifyNotThrow(value, group)` | `validator.Validate(value, group)` | Returns a `ValidationReport`. |
| `ValidatorReport<T>.IsPassed` | `report.IsValid` | Per report, not per rule. |
| `ValidatorReport<T>.Rule.ExceptionMessage` | `failure.Message` | Each `ValidationFailure` also has `Name` and `ErrorCode`. |
| `ValidatorReport<T>.Group` | none | |
| `ValidatorReport<T>.ToString()` | `failure.ToString()` | `"Name: Message"`, or only the message when the failure has no name (a `Must` rule without `WithName`). |

## Removed helpers

v1 also had public helper classes. They are gone in v2:

| 1.x | 2.x |
| --- | --- |
| `JsonHelpers.IsValidJson(text)` | `text.Throw().IfNotJson()`, or `report.Check(text).IfNotJson()` to get a result instead of an exception |
| `XmlHelpers.IsValidXml(text)` | `text.Throw().IfNotXml()`, or `report.Check(text).IfNotXml()` |
| `IEunemerableExtensions.Any()`, `Count()` | `System.Linq` `Any()` and `Count()` (after `Cast<object>()` for a non-generic `IEnumerable`) |
| `ObjectExtensions.CastTo<T>()`, `CastToReflected(type)` | A normal cast. v2 does not need these helpers. |

## Behavior changes

### Exceptions and names

v1 threw plain `Exception` in many places and never set the parameter name. v2 throws:

- `ArgumentNullException` for a null value,
- `ArgumentOutOfRangeException` for range, sign, date and enum checks (with
  `ActualValue` for value types),
- `ArgumentException` for everything else,

always with `ParamName` set to the expression you called `Throw()` on. If you catch
`Exception`, your code still works. If you match on exception messages, update the
strings: every default message changed.

### Null values

For a null value, v1 collection checks threw `NullReferenceException`, and other checks
threw exceptions without your parameter name. In v2 every string, collection and
comparison check fails a null value with `ArgumentNullException` and your parameter
name. To let
null through, call `WhenPresent()` first. See [guards.md](guards.md#null-values).

### JSON and XML

- `IfNotJson()` follows RFC 8259 and accepts any JSON value, such as `42` or `"text"`.
  v1 accepted only objects and arrays, and its Newtonsoft.Json parser also accepted some
  input that is not standard JSON.
- `IfNotXml()` rejects documents with a DTD (`<!DOCTYPE ...>`) and never resolves
  external entities. For untrusted input, use `IfNotXml(maxCharacters)` to limit the size.

### Target frameworks

v1 targeted .NET 6 only. v2 targets netstandard2.0, netstandard2.1, net462, net8.0, net9.0
and net10.0, so it runs on .NET Framework 4.6.2 and later, every .NET Core and .NET
version, Mono and Unity. Some checks need .NET 8 or later, such as `IfNotParsable<T>()`,
the `TimeProvider` overloads, the `DateOnly` and `TimeOnly` checks, and the generic math
overloads. [guards.md](guards.md#checks-by-family) marks them.
