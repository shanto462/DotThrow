# Analyzers

The DotThrow package includes Roslyn analyzers and code fixes. There is nothing else to
install: they run in `dotnet build` and in the IDE for every project that references
DotThrow, on every target framework. They need the .NET 8 SDK or later (Visual Studio 2022
17.8 or later, or a current Rider or VS Code with C# Dev Kit).

| ID | Title | Severity | Category | Code fix |
| --- | --- | --- | --- | --- |
| [DT0001](#dt0001) | Guard chain has no checks | Warning | Usage | Yes |
| [DT0002](#dt0002) | Use a DotThrow check instead of a manual guard | Info | Style | Yes |
| [DT0003](#dt0003) | Remove the name argument that the compiler fills in | Info | Style | Yes |
| [DT0004](#dt0004) | Validator with async rules is run synchronously | Warning | Usage | Yes (in async code) |
| [DT0005](#dt0005) | Regular expression pattern is not valid | Warning | Usage | No |
| [DT0006](#dt0006) | Use a generated regular expression for a constant pattern | Info | Performance | Yes |
| [DT0007](#dt0007) | Check argument is never valid | Error | Usage | No |
| [DT0008](#dt0008) | Use IfInPast or IfInFuture to compare with the current time | Info | Style | Yes |
| [DT0009](#dt0009) | Null check on a value that cannot be null | Warning | Usage | Yes (IfNull, AllowNull) |
| [DT0010](#dt0010) | Validation result is not used | Warning | Usage | Yes (most cases) |
| [DT0011](#dt0011) | Unwrapped nullable value is converted back to a nullable | Warning | Usage | Yes (simple values) |

All rules shipped in DotThrow 2.0.0. Info diagnostics show in the IDE as suggestions;
`dotnet build` prints warnings and errors.
The code fixes run from the light bulb in the IDE, one place at a time or for the whole
document, project or solution.

## Configure the rules

Change a severity or turn a rule off in `.editorconfig`:

```ini
[*.cs]
# Make the manual guard suggestion a build warning.
dotnet_diagnostic.DT0002.severity = warning
# Turn the generated regex suggestion off.
dotnet_diagnostic.DT0006.severity = none
```

The severities are `error`, `warning`, `suggestion` (Info), `silent` and `none`. Suppress
one place with `#pragma warning disable DT0004` and `#pragma warning restore DT0004`, or
with `[SuppressMessage("Usage", "DT0004")]`.

## DT0001

**Guard chain has no checks** (Warning)

`Throw()` and `Check()` only start a chain. Without a check after them nothing is checked.
When the guard is converted back to its value, the value passes through unchecked.

```csharp
void Examples(string name, ValidationReport report, SignUpRequest request)
{
    name.Throw();                                  // DT0001: nothing is checked
    string notChecked = name.Throw();              // DT0001: the value is not checked
    report.Check(request.Email);                   // DT0001

    string checkedName = name.Throw().IfNullOrWhiteSpace();   // fine
}

public sealed record SignUpRequest(string Email);
```

`AllowNull()` is not a check, so `name.Throw().AllowNull();` is reported too. A guard
stored in a variable or passed to a method is not reported.

Code fix: removes the statement when that loses no side effect (the value and the
arguments are variables, fields, properties or constants), or uses the value directly in
place of the guard.

## DT0002

**Use a DotThrow check instead of a manual guard** (Info)

An `if` statement whose only job is to throw `ArgumentException`, `ArgumentNullException`
or `ArgumentOutOfRangeException` can be one DotThrow check.

| Manual guard | DotThrow check |
| --- | --- |
| `if (x is null)`, `x == null`, `ReferenceEquals(x, null)` | `x.Throw().IfNull()` |
| `if (string.IsNullOrEmpty(s))` | `s.Throw().IfNullOrEmpty()` |
| `if (string.IsNullOrWhiteSpace(s))` | `s.Throw().IfNullOrWhiteSpace()` |
| `if (list.Count == 0)`, `array.Length == 0`, `!items.Any()` | `x.Throw().IfNullOrEmpty()` |
| `if (id == Guid.Empty)` | `id.Throw().IfEmpty()` |
| `if (!Enum.IsDefined(typeof(Color), color))` | `color.Throw().IfNotDefined()` |
| `x ?? throw new ArgumentNullException(nameof(x))` | `x.Throw().IfNull()` |
| `if (n < 0)`, `n <= 0`, `n > 0`, `n >= 0`, `n == 0` | `IfNegative()`, `IfNegativeOrZero()`, `IfPositive()`, `IfPositiveOrZero()`, `IfZero()` |
| `if (n < limit)` and the other comparisons | `IfLessThan(limit)` and so on |
| `if (n < min \|\| n > max)` | `n.Throw().IfOutOfRange(min, max)` |
| `if (n <= min \|\| n >= max)` | `n.Throw().IfOutOfRangeExclusive(min, max)` |

```csharp
void Before(int count)
{
    if (count < 1 || count > 10)
    {
        throw new ArgumentOutOfRangeException(nameof(count), "Count must be from 1 to 10.");
    }
}

void After(int count)
{
    // After the code fix
    count.Throw().IfOutOfRange(1, 10, "Count must be from 1 to 10.");
}
```

The code fix keeps your message, keeps a parameter name that differs from the variable
(`x.Throw("name")`), and adds `using DotThrow;` when it is missing. Before C# 10 the
compiler cannot fill in the name, so the fix passes it. For a value declared nullable
(`string?`, `int?`, a `var` local) the fix assigns the result back,
`text = text.Throw().IfNull();`, so nullable analysis still knows the value is not null
after the check.

The rule stays quiet when the change could break callers or change behavior:

- another exception type, or one that DotThrow's exception would not be caught as (such
  as `ArgumentNullException` for an empty string);
- more than one statement, an `else`, or an inner exception;
- a message that is not a constant (DotThrow would build it on every call);
- a computed value or limit, or a limit whose type does not convert to the value's type
  (an `int` limit for a `byte` value);
- a nullable (lifted) comparison, a `[Flags]` enum, a reversed constant range, or a
  nullable field or property;
- an ordering comparison of `float` or `double`, because DotThrow's ordering checks reject
  NaN, which a manual `x < 0` lets through.

A `?? throw` is reported only when its result goes to a typed target: an assignment, a
typed variable, an argument or a method's return value.

On .NET 7 and later, CA1510 and CA1511 also suggest `ArgumentNullException.ThrowIfNull`
and `ArgumentException.ThrowIfNullOrEmpty` for the null and empty cases. Keep the one you
prefer and set the other to `none`.

## DT0003

**Remove the name argument that the compiler fills in** (Info)

`Throw()`, `Check()` and `context.Check()` use `[CallerArgumentExpression]`, so the
compiler passes the text of the checked expression as the name. Passing the same text by
hand is noise, and it goes stale when the variable is renamed.

```csharp
void Examples(string name, ValidationReport report, SignUpRequest request)
{
    name.Throw(nameof(name)).IfNull();              // DT0003
    report.Check(request.Email, "request.Email").IfNullOrWhiteSpace();   // DT0003

    name.Throw().IfNull();                          // fine
    request.Email.Throw("Email").IfNull();          // fine: a different name on purpose
}
```

Only an argument with exactly the text the compiler would pass is reported. The rule needs
C# 10 or later (older compilers do not fill the name in). Code fix: removes the argument.

## DT0004

**Validator with async rules is run synchronously** (Warning)

A validator with `MustAsync`, `CustomAsync`, `WhenAsync`, `UnlessAsync` or `WhereAsync`
rules must run with `ValidateAsync`, `ValidateAndThrowAsync` or `TestValidateAsync`.
`Validate`, `ValidateAndThrow` and `TestValidate` throw
`AsyncRuleInvokedSynchronouslyException` when they reach an async rule (a rule behind a
false condition is not reached, so the message says "can throw").

```csharp
async Task Examples(IUserStore users, SignUp signUp)
{
    var report = new SignUpValidator(users).Validate(signUp);                // DT0004
    var asyncReport = await new SignUpValidator(users).ValidateAsync(signUp); // fine
}

public interface IUserStore
{
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken);
}

public sealed record SignUp(string Email);

public sealed class SignUpValidator : Validator<SignUp>
{
    public SignUpValidator(IUserStore users) =>
        MustAsync(x => x.Email, async (email, token) => !await users.ExistsAsync(email, token));
}
```

The analyzer sees the rules when they are in the same project: a validator class whose
constructors add them (base classes too), or a `new Validator<T>()` chain reached through
a local, a field, a property or a method. It follows `For` and `ForEach` child
validators, `Include`, `When` and `Unless` blocks, `DependentRules` and inline child
rules.

Groups count: `Validate(x, "checkout")` is reported only when an async rule is in that
group (names ignore case, `ValidationGroups.All` selects every rule). When the groups come
from options, a callback or a variable, the analyzer cannot know them and stays quiet. A
validator that comes from dependency injection as `IValidator<T>` is not visible.

Code fix (in an async method, lambda, local function or top-level code): awaits the async
method, for example `var report = await validator.ValidateAsync(order);`.

## DT0005

**Regular expression pattern is not valid** (Warning)

`IfMatches` and `IfNotMatches` build a `Regex` from the pattern every time they run. A
constant pattern that does not parse (or a constant options value the engine rejects, or
a construct that `RegexOptions.NonBacktracking` does not support) throws
`ArgumentException` on every call.

```csharp
void Example(string code)
{
    code.Throw().IfMatches("[a-");   // DT0005: Unterminated [] set
}
```

The pattern is checked with the regular expression engine that runs the compiler. That
engine can differ between hosts (`msbuild` on .NET Framework and `dotnet build`), so the
rule is a warning, not an error.

## DT0006

**Use a generated regular expression for a constant pattern** (Info)

On .NET 7 and later with C# 11 or later, a constant pattern can be compiled at build time
by the regular expression source generator. The generated `Regex` is faster, needs no
warm-up and works well with trimming and Native AOT.

Before:

```csharp
public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.PostalCode, code => code.IfNotMatches("^[0-9]{5}$"));   // DT0006
    }
}

public sealed record Address(string PostalCode);
```

After the code fix:

```csharp
using System.Text.RegularExpressions;

public sealed partial class FixedAddressValidator : Validator<Address>
{
    public FixedAddressValidator()
    {
        For(x => x.PostalCode, code => code.IfNotMatches(CodeRegex()));
    }

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex CodeRegex();
}
```

The code fix adds the method to the containing type, makes the type (and the types around
it) `partial`, keeps the options and the message, and adds
`using System.Text.RegularExpressions;` when needed. Patterns with
`RegexOptions.NonBacktracking` are not reported.

## DT0007

**Check argument is never valid** (Error)

A constant argument that makes a check throw `ArgumentException` on every call, or fail
every value:

- a negative length or count: `IfLongerThan(-1)`, `IfCountGreaterThan(-2)`;
- a minimum greater than the maximum: `IfOutOfRange(10, 5)`, `IfInRange`,
  `IfLengthOutOfRange`, `IfCountOutOfRange`;
- an empty exclusive range: `IfOutOfRangeExclusive(5, 5)` (every value fails);
- a NaN limit: `IfGreaterThan(double.NaN)`;
- a zero divisor: `IfNotMultipleOf(0)`;
- a precision below 1, or a scale outside 0 to the precision:
  `IfPrecisionScaleExceeded(4, 5)`.

A test that checks on purpose that such a call throws can turn the rule off for that line
with `#pragma warning disable DT0007`.

## DT0008

**Use IfInPast or IfInFuture to compare with the current time** (Info)

```csharp
void Examples(DateTime when, DateTimeOffset moment, TimeProvider clock, DateOnly day)
{
    when.Throw().IfBefore(DateTime.Now);                          // DT0008: use IfInPast()
    moment.Throw().IfAfter(DateTimeOffset.UtcNow);                // DT0008: use IfInFuture()
    moment.Throw().IfBefore(clock.GetUtcNow());                   // DT0008: use IfInPast(clock)
    day.Throw().IfBefore(DateOnly.FromDateTime(DateTime.Today));  // DT0008: use IfInPast()
}
```

Only exact equivalents are reported. `IfBefore(DateTime.UtcNow)` on a `DateTime` is not:
for a value with `DateTimeKind.Unspecified`, `IfInPast()` compares with the local time. A
custom message that shows the limit with `{ComparisonValue}` is left alone.

Code fix: calls `IfInPast` or `IfInFuture`, keeps the message and passes the
`TimeProvider`. The error code of a failure changes from `Before` and `After` to `InPast`
and `InFuture`, and so does the default message.

## DT0009

**Null check on a value that cannot be null** (Warning)

A non-nullable value type is never null: `IfNull()` never fails, `AllowNull()` does
nothing and `IfNotNull()` always fails. Often `IfDefault()` (or `IfEmpty()` for a `Guid`)
was meant.

```csharp
void Examples(int count, Guid id, int? maybe)
{
    count.Throw().IfNull();     // DT0009: never fails for int
    id.Throw().IfNotNull();     // DT0009: always fails for Guid
    maybe.Throw().IfNull();     // fine for int?: it also unwraps the value
}
```

Code fix: removes `IfNull()` or `AllowNull()` when another check follows it in the chain.

## DT0010

**Validation result is not used** (Warning)

`Validate` and `ValidateAsync` return a report and do not throw. Calling them as a
statement ignores every failure. A validation task that is never awaited hides its
failures too (inside an async method the compiler already warns with CS4014, so DT0010
reports it only elsewhere).

```csharp
async Task Examples(Validator<Order> validator, Order order)
{
    validator.Validate(order);                    // DT0010
    await validator.ValidateAsync(order);         // DT0010

    validator.ValidateAndThrow(order);            // fine
    var report = validator.Validate(order);       // fine
}

void NotAsync(Validator<Order> validator, Order order)
{
    validator.ValidateAndThrowAsync(order);       // DT0010 in a method that is not async
}

public sealed record Order(string Number);
```

Calls with options or an options callback are not reported, because `ThrowOnFailures`
may be set. Code fix: calls `ValidateAndThrow` or `ValidateAndThrowAsync`.

## DT0011

**Unwrapped nullable value is converted back to a nullable** (Warning)

`WhenPresent()` on an `int?` (and `AllowNull()` followed by `IfNull()`) unwraps the value
to `int` and skips the rest of the chain when there is no value. The guard then holds
`default(int)`, so converting the chain back to `int?` turns a missing value into `0`,
not `null`.

```csharp
void Examples(AgeRequest request)
{
    int? age = request.Age.Throw().WhenPresent().IfOutOfRange(0, 150);   // DT0011: null becomes 0

    request.Age.Throw().WhenPresent().IfOutOfRange(0, 150);              // fine: check...
    int? checkedAge = request.Age;                                       // ...then use the value
}

public sealed record AgeRequest(int? Age);
```

The rule fires when the result goes to an `int?` (or any `T?`) through an assignment, a
variable, a return value or an argument. `WhenPresent()` on a reference type keeps `null`,
so it is not reported.

Code fix: splits the statement into the check and an assignment of the original value, as
above, for a variable declaration, an assignment or a return whose checked value is a
variable, field or property. Otherwise there is no fix.

## Not covered yet

Rules for [dynamic rule](dynamic-rules.md) JSON files, such as unknown check names, are
planned.
