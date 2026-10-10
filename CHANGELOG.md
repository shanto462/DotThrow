# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.1] - 2026-10-10

No code changes. The packages are the same as 2.0.0 apart from their README.

### Changed

- The README of each package on NuGet.org links all three packages:
  `DotThrow`, `DotThrow.DependencyInjection` and `DotThrow.AspNetCore`.
- The packages are also published to GitHub Packages, so they are listed on the
  repository page. Install them from NuGet.org as before.

## [2.0.0] - 2026-10-10

Complete rewrite with a new API. Code written for 1.x needs changes, see
Changed and Removed below.

### Added

#### Guard clauses

- `Throw()` on any value starts a chain of checks, for example
  `name.Throw().IfNullOrWhiteSpace().IfLongerThan(50)`. The compiler fills in
  the parameter name (`CallerArgumentExpression`), and the chain converts back
  to the value, so it can be assigned directly.
- `Guard<T>` is a `ref struct`, so a chain allocates nothing when every check
  passes.
- Failed checks throw `ArgumentNullException`, `ArgumentOutOfRangeException` or
  `ArgumentException` with `ParamName` set. `Throw(failure => new MyException(...))`
  throws your own exception instead.
- Checks for any value: `IfNull`, `IfNotNull`, `IfDefault`, `IfEqualTo`,
  `IfNotEqualTo`, `IfOneOf`, `IfNotOneOf`, `IfType`, `IfNotType`, and `If` and
  `IfNot` with a predicate. `AllowNull()` lets a `null` value skip the rest of
  the chain, and `IfNull()` on a nullable value type unwraps it.
- Checks for comparable values: `IfGreaterThan`, `IfGreaterThanOrEqualTo`,
  `IfLessThan`, `IfLessThanOrEqualTo`, `IfOutOfRange` and `IfInRange`. For
  numbers also `IfNegative`, `IfNegativeOrZero`, `IfPositive`,
  `IfPositiveOrZero` and `IfZero`, plus `IfNaN` and `IfInfinity` for `double`
  and `float`. NaN fails every ordering check.
- Checks for strings: `IfNullOrEmpty`, `IfNullOrWhiteSpace`, `IfLongerThan`,
  `IfShorterThan`, `IfLengthNotEqualTo`, `IfLengthOutOfRange`, `IfEqualTo` and
  `IfNotEqualTo` with a `StringComparison`, `IfContains`, `IfStartsWith`,
  `IfEndsWith` and their `IfNot...` forms, `IfMatches` and `IfNotMatches`
  (pattern or `Regex`), `IfNotEmailAddress`, `IfNotJson`, `IfNotXml`,
  `IfNotUri`, `IfNotHttpUrl`, `IfAllUpperCase`, `IfAllLowerCase`, and
  `IfNotParsable<T>` on .NET 8 and later.
- Checks for collections: `IfNullOrEmpty`, `IfCountEqualTo`,
  `IfCountNotEqualTo`, `IfCountGreaterThan`, `IfCountLessThan`,
  `IfCountOutOfRange`, `IfContains`, `IfNotContains`, `IfContainsNull` and
  `IfHasDuplicates`.
- Checks for dates and times: `IfBefore`, `IfAfter`, `IfInPast` and
  `IfInFuture` for `DateTime` and `DateTimeOffset`. On .NET 8 and later also for
  `DateOnly`, `IfBefore` and `IfAfter` for `TimeOnly`, and `TimeProvider`
  overloads so tests can control the clock.
- Other checks: `IfTrue` and `IfFalse` for `bool`, `IfEmpty` for `Guid`, and
  `IfNotDefined` for enums.
- Many new checks for other BCL types.
- Custom checks: write an extension method on `Guard<T>` and call `Fail`,
  `FailOutOfRange` or `FailNull`. Custom checks work in throw mode and in
  collect mode.
- Error messages never include the checked value, and `ActualValue` is set only
  for value types, so strings such as passwords do not end up in logs.

#### Validation

- Collect mode: `ValidationReport.Check(value)` runs the same checks but
  records failures instead of throwing. A report has `IsValid`, `Failures`,
  `ThrowIfInvalid()` and `ToDictionary()` (ready for
  `Results.ValidationProblem`).
- `Validator<T>`: a reusable, thread-safe set of rules built with `For`,
  `ForEach`, `Must`, `MustAsync`, `Custom`, `CustomAsync` and `Include`. Rules
  can have a group and a `when` condition. Nested objects and collection items
  get names like `Address.City` and `Lines[2].Quantity`. `ValidateAsync` runs
  async rules with a `CancellationToken`, and `IValidator<T>` makes validators
  easy to register in dependency injection and to mock.
- `ValidationFailedException`, which derives from `ArgumentException` and
  carries every failure.
- `ErrorCodes`: a stable error code for every built-in check, on each
  `ValidationFailure`.
- FluentValidation-style features: rule options, severity, cascade modes,
  dependent rules, polymorphic validators, test helpers, assembly scanning,
  and message templates with localization.

#### Dynamic rules

- Rules can be defined in JSON and applied at runtime, with no extra
  dependencies. They use reflection with trimming annotations, and an AOT-safe
  mode works without it. Rules bound to `IConfiguration` come with the
  `DotThrow.DependencyInjection` package.

#### Platforms and packaging

- Targets `netstandard2.0`, `netstandard2.1`, `net462`, `net8.0`, `net9.0` and
  `net10.0`, with no package dependencies on any of them. Trimming and Native
  AOT compatible on .NET 8 and later.
- Source Link, deterministic builds, a symbol package (`.snupkg`), XML
  documentation, and a package README and icon.

#### Project

- CI on Linux, macOS and Windows: tests on net8.0, net9.0 and net10.0, plus
  net472 and net481 on Windows, and against the `netstandard2.0` and
  `netstandard2.1` builds of the library. Also package validation, a Native AOT
  smoke test, code coverage, CodeQL, OpenSSF Scorecard, dependency review and
  Dependabot. Releases are published to NuGet.org with trusted publishing and
  come with signed build provenance.

### Changed

- The entry point is `value.Throw()` instead of `value.CreateThrower()`, and
  checks dropped the `Throw` prefix. For example `ThrowIfNullOrWhiteSpace()` is
  now `IfNullOrWhiteSpace()`, `ThrowIfLessThanZero()` is `IfNegative()`,
  `ThrowIfOlderThan(date)` is `IfBefore(date)`, `ThrowIfAfterThanNow()` is
  `IfInFuture()`, `ThrowIfCountIsGreaterThan(n)` is `IfCountGreaterThan(n)`,
  `ThrowIfEmpty()` on a collection is `IfNullOrEmpty()`, `ThrowIfNotValidJson()`
  is `IfNotJson()` and `ThrowIfNotValidInteger()` is `IfNotParsable<int>()`.
- `Validator<T>` is now an object that you create, configure once and share,
  instead of a static registry per type. `Verify` is now `ValidateAndThrow`, and
  `VerifyNotThrow` is now `Validate`, which returns a `ValidationReport` with
  only the failures.
- A failure reports the property path or parameter name with its message and
  error code, instead of a free text message only.

### Removed

- `ExceptionThrower<T>`, `CreateThrower()` and the `...ExceptionExtensions`
  classes. Use `Throw()`.
- The static `Validator<T>.AddRule(...)` registry, `ValidatorRule<T>`,
  `ValidatorRuleList<T>` and `ValidatorReport<T>`, including the exception
  `Type` and "passed message" of a rule. Use a `Validator<T>` instance, and
  `Throw(failure => ...)` for a custom exception.
- Public helpers that were not guard checks: `JsonHelpers`, `XmlHelpers`,
  `ObjectExtensions` (`CastTo`, `CastToReflected`) and `IEunemerableExtensions`
  (`Any` and `Count` on a non-generic `IEnumerable`).

### Fixed

- `ThrowIfGreaterThanOrEqualZero` checked `< 0`, so it threw for negative
  numbers and let positive numbers pass. `IfPositiveOrZero` checks `>= 0`.
- `ThrowIfCountIsEqualTo` said "less than" in its error message.
- `ThrowIfNullOrWhiteSpace` and `ThrowIfNullOrEmpty` passed the message as the
  parameter name of the `ArgumentNullException`.
- `ThrowIfAllUpper` and `ThrowIfAllLower` threw for an empty string, and threw
  `NullReferenceException` for a `null` string. `IfAllUpperCase` and
  `IfAllLowerCase` look only at letters, pass an empty string, and report a
  `null` string as null.
- The static validator registry could drop rules when two threads added the
  first rule for a type at the same time, and it held a lock while it yielded
  results to the caller.
- Exceptions were created with `Activator` and `dynamic`, which is slow and not
  safe for trimming or Native AOT.
- The JSON check rejected valid JSON values that are not objects or arrays,
  such as `"text"`, `42` or `true`.
- Failed checks threw the base `Exception` type in many places instead of
  `ArgumentException`, `ArgumentNullException` or
  `ArgumentOutOfRangeException`, and `ParamName` was never set.
- The package depended on Newtonsoft.Json only to check JSON syntax. The JSON
  check now uses a built-in parser, and the `DotThrow` package has no
  dependencies.
- The package targeted only `net6.0`, which is out of support, so .NET Framework
  and .NET Standard projects could not use it.

## [2.0.0-rc.1] - 2026-10-10

Release candidate of 2.0.0, published to rehearse the release: it contains the
changes listed under 2.0.0 above and is the first publish of the
DotThrow.DependencyInjection and DotThrow.AspNetCore packages.

## [1.0.2] - 2022-03-26

### Added

- `DateTime` checks: `ThrowIfOlderThan`, `ThrowIfAfterThan`,
  `ThrowIfOlderThanNow`, `ThrowIfAfterThanNow`, `ThrowIfEquals` and `ThrowIf`.

### Changed

- `ThrowIfNullOrWhiteSpace` and `ThrowIfNullOrEmpty` pass a text message to
  `ArgumentNullException`.

## [1.0.1] - 2022-03-21

### Added

- Collection checks: `ThrowIfEmpty`, `ThrowIf`, `ThrowIfCountIsGreaterThan`,
  `ThrowIfCountIsLessThan` and `ThrowIfCountIsEqualTo`.
- `int` checks: `ThrowIfZero`, `ThrowIfLessThanZero`,
  `ThrowIfLessThanOrEqualZero`, `ThrowIfGreaterThanZero`,
  `ThrowIfGreaterThanOrEqualZero`, `ThrowIfEqual` and `ThrowIf`.

## [1.0.0] - 2022-03-17

First release (.NET 6).

### Added

- Method chaining for strings with `value.CreateThrower()`:
  `ThrowIfNullOrWhiteSpace`, `ThrowIfNullOrEmpty`, `ThrowIfAllUpper`,
  `ThrowIfAllLower`, `ThrowIf`, `ThrowIfNot`, `ThrowIfNotValidJson`,
  `ThrowIfNotValidXml` and `ThrowIfNotValidInteger`.
- Validation rules with `Validator<T>.AddRule(...)`, checked with `Verify`
  (throws) or `VerifyNotThrow` (returns a report).
- Grouped rules, so `Verify` and `VerifyNotThrow` can run only the rules of one
  group.

[Unreleased]: https://github.com/shanto462/DotThrow/compare/v2.0.1...HEAD
[2.0.1]: https://github.com/shanto462/DotThrow/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/shanto462/DotThrow/compare/v1.0.2...v2.0.0
[2.0.0-rc.1]: https://github.com/shanto462/DotThrow/compare/v1.0.2...v2.0.0-rc.1
[1.0.2]: https://github.com/shanto462/DotThrow/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/shanto462/DotThrow/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/shanto462/DotThrow/releases/tag/v1.0.0
