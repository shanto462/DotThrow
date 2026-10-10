# Guard clauses

A guard clause checks an argument at the start of a method and throws when the argument
is wrong. With DotThrow you call `Throw()` on a value and chain the checks you need.

The examples assume `using DotThrow;`.

- [How a chain works](#how-a-chain-works)
- [What is thrown](#what-is-thrown)
- [Names and messages](#names-and-messages)
- [Custom exceptions](#custom-exceptions)
- [Null values](#null-values)
- [AllowNull for optional values](#allownull-for-optional-values)
- [NaN](#nan)
- [Wrong arguments to a check](#wrong-arguments-to-a-check)
- [Checks by type](#checks-by-type)
- [Error codes](#error-codes)

## How a chain works

```csharp
public sealed class Customer
{
    public Customer(string name, int age)
    {
        Name = name.Throw().IfNullOrWhiteSpace().IfLongerThan(100);
        Age = age.Throw().IfOutOfRange(0, 150);
    }

    public string Name { get; }

    public int Age { get; }
}
```

- `Throw()` starts a chain for the value. Each check returns the chain, so you can add
  the next check.
- The first check that fails throws. The checks after it do not run.
- The chain converts to the value, so you can assign it directly, as above.
- The chain is a `Guard<T>`, a `ref struct`. It never allocates. You cannot keep it in a
  field, so use it in one expression.

## What is thrown

| Problem | Exception | Examples |
| --- | --- | --- |
| The value is null | `ArgumentNullException` | `IfNull`, and every string, collection and comparison check |
| The value is outside a range or order | `ArgumentOutOfRangeException` | `IfOutOfRange`, `IfGreaterThan`, `IfNegative`, `IfBefore`, `IfInPast`, `IfNotDefined` |
| Anything else | `ArgumentException` | `IfNullOrWhiteSpace`, `IfLongerThan`, `IfNotEmailAddress`, `IfCountGreaterThan` |

- `ParamName` is always set to the name of the value.
- `ArgumentOutOfRangeException.ActualValue` holds the value when it is a value type (a
  number, date or enum). For a reference type, such as a string, it stays null.
- All three types derive from `ArgumentException`, so `catch (ArgumentException)` catches
  every DotThrow failure.

## Names and messages

The name comes from your code. The compiler passes the text of the expression that you
call `Throw()` on, so a property path stays readable:

```csharp
var request = new SignUpRequest(Name: "Ada Lovelace", Email: "");
request.Email.Throw().IfNullOrWhiteSpace();

public sealed record SignUpRequest(string Name, string Email);
```

```text
System.ArgumentException: 'request.Email' must not be empty or white space. (Parameter 'request.Email')
```

To use another name, pass it: `request.Email.Throw("email")`.

The default message names the value and says what is wrong. It never contains the
checked string, so passwords and tokens do not end up in logs. It does contain the values
that you pass to the check, such as `150` or `'ORD-'`, so do not pass a secret as the
compared value of `IfEqualTo`, `IfNotEqualTo` or `IfContains`. .NET adds ` (Parameter 'name')` to the message,
and `ArgumentOutOfRangeException` adds a second line such as `Actual value was 200.`

Every check takes an optional `message` that replaces the default message:

```csharp
var pageSize = 25;
pageSize.Throw().IfNegativeOrZero("Ask for at least one item per page.");
```

`IfOneOf` and `IfNotOneOf` with a plain list of values (`params`) have no `message`
parameter. Pass the values as a collection to set one:

```csharp
var role = "editor";
role.Throw().IfNotOneOf(["admin", "editor"], message: "Unknown role.");
```

## Custom exceptions

Pass a factory to `Throw()` to throw your own exception. The factory gets a
`ValidationFailure` with the `Name`, the `Message` and the `ErrorCode`, and runs only when
a check fails.

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

## Null values

- `IfNull()` fails when the value is null.
- Every string, collection and comparison check also fails a null value, with
  `ArgumentNullException` and the code `Null`. So `name.Throw().IfLongerThan(50)` never
  lets a null through.
- These checks treat null as a normal value instead:
  - `IfNotNull`, `IfEqualTo`, `IfNotEqualTo`, `IfOneOf`, `IfNotOneOf`.
  - `If` and `IfNot`: your predicate gets the null.
  - `IfDefault`: null is the default of a reference type, so it fails, with
    `ArgumentException` and the code `Default`.
  - `IfType`: null passes, because it is not an instance of any type.
  - `IfNotType`: null fails, with `ArgumentException` and the code `NotType`.
  - String `IfEqualTo` and `IfNotEqualTo` with a `StringComparison`.

On a nullable value type such as `int?`, `IfNull()` also unwraps the value, so the
number checks become available:

```csharp
int? maybeCount = 3;
int count = maybeCount.Throw().IfNull().IfNegative();
```

## AllowNull for optional values

`AllowNull()` lets a null value skip every later check in the chain. A value that is not
null is checked as usual.

```csharp
string? middleName = null;
middleName.Throw().AllowNull().IfLongerThan(50);   // passes
```

Checks before `AllowNull()` still run. For an optional nullable value type, add
`IfNull()` after `AllowNull()` to unwrap it. `AllowNull()` already stopped the chain for
null, so `IfNull()` cannot fail here:

```csharp
int? quantity = null;
quantity.Throw().AllowNull().IfNull().IfOutOfRange(1, 100);   // passes
```

## NaN

NaN fails every ordering check: `IfGreaterThan`, `IfGreaterThanOrEqualTo`, `IfLessThan`,
`IfLessThanOrEqualTo`, `IfOutOfRange`, `IfInRange`, `IfNegative`, `IfNegativeOrZero`,
`IfPositive` and `IfPositiveOrZero`. NaN has no order, so letting it pass an upper limit
would let a bad value through. `IfZero` passes NaN, because NaN is not zero. Use
`IfNaN()` and `IfInfinity()` to reject NaN and infinity on their own.

## Wrong arguments to a check

The arguments of a check are checked too: a negative length, a minimum above the
maximum, a null predicate or a null limit. These are bugs in the calling code, so they
always throw at once, even in [collect mode](validation.md):

```csharp
var age = 30;
age.Throw().IfOutOfRange(150, 0);
```

```text
System.ArgumentOutOfRangeException: The maximum must be greater than or equal to the minimum. (Parameter 'max')
Actual value was 0.
```

## Checks by type

Every check also takes an optional `message`. In the tables, "Argument" means
`ArgumentException`, "ArgumentNull" means `ArgumentNullException` and "OutOfRange" means
`ArgumentOutOfRangeException`. A check that fails a null value always throws
`ArgumentNullException` with the code `Null`.

### Any value

| Check | Fails when | Throws | Code |
| --- | --- | --- | --- |
| `IfNull()` | the value is null. On `T?` value types it also unwraps the value. | ArgumentNull | `Null` |
| `IfNotNull()` | the value is not null | Argument | `NotNull` |
| `IfDefault()` | the value is `default(T)`: null, 0, `Guid.Empty`, `DateTime.MinValue`... | Argument | `Default` |
| `IfEqualTo(other, comparer?)` | the value equals `other` | Argument | `EqualTo` |
| `IfNotEqualTo(other, comparer?)` | the value does not equal `other` | Argument | `NotEqualTo` |
| `IfOneOf(values)` | the value is one of `values` | Argument | `OneOf` |
| `IfNotOneOf(values)` | the value is not one of `values` | Argument | `NotOneOf` |
| `If(predicate)` | the predicate returns true | Argument | `Condition` |
| `IfNot(predicate)` | the predicate returns false | Argument | `Condition` |
| `IfType(type)` | the value is an instance of `type` or a derived type | Argument | `Type` |
| `IfNotType(type)` | the value is not an instance of `type` (null fails) | Argument | `NotType` |

### Numbers and other comparable values

The range checks work on any type that implements `IComparable<T>`: every number type,
`DateTime`, `DateTimeOffset`, `TimeSpan`, `Version`, and your own types. The sign checks
work on value types and treat `default(T)` as zero, so they also work on `TimeSpan`.

| Check | Fails when | Throws | Code |
| --- | --- | --- | --- |
| `IfGreaterThan(limit)` | value > limit | OutOfRange | `GreaterThan` |
| `IfGreaterThanOrEqualTo(limit)` | value >= limit | OutOfRange | `GreaterThanOrEqualTo` |
| `IfLessThan(limit)` | value < limit | OutOfRange | `LessThan` |
| `IfLessThanOrEqualTo(limit)` | value <= limit | OutOfRange | `LessThanOrEqualTo` |
| `IfOutOfRange(min, max)` | value < min or value > max (both ends are allowed) | OutOfRange | `OutOfRange` |
| `IfInRange(min, max)` | min <= value <= max | OutOfRange | `InRange` |
| `IfNegative()` | value < 0 | OutOfRange | `Negative` |
| `IfNegativeOrZero()` | value <= 0 | OutOfRange | `NegativeOrZero` |
| `IfPositive()` | value > 0 | OutOfRange | `Positive` |
| `IfPositiveOrZero()` | value >= 0 | OutOfRange | `PositiveOrZero` |
| `IfZero()` | value == 0 (NaN passes) | OutOfRange | `Zero` |
| `IfNaN()` | a `double` or `float` is NaN | OutOfRange | `NaN` |
| `IfInfinity()` | a `double` or `float` is positive or negative infinity | OutOfRange | `Infinity` |

### Strings

A null string fails every check here with the code `Null`, except `IfEqualTo` and
`IfNotEqualTo`.

| Check | Fails when | Throws | Code |
| --- | --- | --- | --- |
| `IfNullOrEmpty()` | the string is null or empty | Argument | `Empty` |
| `IfNullOrWhiteSpace()` | the string is null, empty, or only white space | Argument | `WhiteSpace` |
| `IfLongerThan(maxLength)` | it has more than `maxLength` characters | Argument | `LongerThan` |
| `IfShorterThan(minLength)` | it has fewer than `minLength` characters | Argument | `ShorterThan` |
| `IfLengthNotEqualTo(length)` | it does not have exactly `length` characters | Argument | `LengthNotEqualTo` |
| `IfLengthOutOfRange(min, max)` | its length is outside `min` to `max` | Argument | `LengthOutOfRange` |
| `IfEqualTo(other, comparison)` | it equals `other` under a `StringComparison` | Argument | `EqualTo` |
| `IfNotEqualTo(other, comparison)` | it does not equal `other` under a `StringComparison` | Argument | `NotEqualTo` |
| `IfContains(value, comparison?)` | it contains `value` | Argument | `Contains` |
| `IfNotContains(value, comparison?)` | it does not contain `value` | Argument | `NotContains` |
| `IfStartsWith(value, comparison?)` | it starts with `value` | Argument | `StartsWith` |
| `IfNotStartsWith(value, comparison?)` | it does not start with `value` | Argument | `NotStartsWith` |
| `IfEndsWith(value, comparison?)` | it ends with `value` | Argument | `EndsWith` |
| `IfNotEndsWith(value, comparison?)` | it does not end with `value` | Argument | `NotEndsWith` |
| `IfMatches(pattern)` or `IfMatches(regex)` | it matches the regular expression | Argument | `Matches` |
| `IfNotMatches(pattern)` or `IfNotMatches(regex)` | it does not match the regular expression | Argument | `NotMatches` |
| `IfNotEmailAddress()` | it is not one `@` with text on both sides, or it has white space, a control character, two dots in a row, or a dot at the start or end of either side | Argument | `NotEmailAddress` |
| `IfNotJson()` | it is not valid JSON (RFC 8259). Any JSON value passes, such as `42`. | Argument | `NotJson` |
| `IfNotXml()` | it is not a well-formed XML document. DTDs are rejected. | Argument | `NotXml` |
| `IfNotUri(kind?)` | it is not a URI of that kind (absolute by default). A bare file path such as `/etc/hosts` is not an absolute URI. | Argument | `NotUri` |
| `IfNotHttpUrl()` | it is not an absolute `http` or `https` URL | Argument | `NotHttpUrl` |
| `IfNotParsable<T>(provider?)` | `T.TryParse` fails, with the invariant culture by default. .NET 8 and later. | Argument | `NotParsable` |
| `IfAllUpperCase()` | it has a letter and every letter is uppercase | Argument | `AllUpperCase` |
| `IfAllLowerCase()` | it has a letter and every letter is lowercase | Argument | `AllLowerCase` |

- The length is `string.Length`, the number of UTF-16 characters.
- `IfContains`, `IfStartsWith`, `IfEndsWith` and their `IfNot` forms use an ordinal
  comparison unless you pass a `StringComparison`.
- `IfNotEmailAddress` checks the format only. Send a confirmation email to prove that an
  address exists.

### Collections

These checks work on any `IEnumerable`: arrays, lists, sets and dictionaries. A null
collection fails with the code `Null`.

| Check | Fails when | Throws | Code |
| --- | --- | --- | --- |
| `IfNullOrEmpty()` | the collection is null or has no items | Argument | `Empty` |
| `IfCountEqualTo(count)` | it has exactly `count` items | Argument | `CountEqualTo` |
| `IfCountNotEqualTo(count)` | it does not have exactly `count` items | Argument | `CountNotEqualTo` |
| `IfCountGreaterThan(maxCount)` | it has more than `maxCount` items | Argument | `CountGreaterThan` |
| `IfCountLessThan(minCount)` | it has fewer than `minCount` items | Argument | `CountLessThan` |
| `IfCountOutOfRange(min, max)` | the number of items is outside `min` to `max` | Argument | `CountOutOfRange` |
| `IfContains(item, comparer?)` | it contains `item` | Argument | `Contains` |
| `IfNotContains(item, comparer?)` | it does not contain `item` | Argument | `NotContains` |
| `IfContainsNull()` | an item is null | Argument | `ContainsNull` |
| `IfHasDuplicates()` | two items are equal | Argument | `HasDuplicates` |

- Count checks read `Count` without enumerating for arrays, `List<T>` and other
  collections that implement the non-generic `ICollection`, and for read-only collections
  of reference types such as `HashSet<string>`. Other sequences are enumerated only as far
  as needed. A lazy sequence is enumerated, so a sequence that can be read only once is
  empty afterward.
- `IfContains` and `IfNotContains` without a comparer use the collection's own `Contains`,
  so a `HashSet<T>` keeps its own comparer.
- `IfHasDuplicates` uses each item's own `Equals` and allocates a `HashSet`.

### Booleans, GUIDs and enums

| Check | Fails when | Throws | Code |
| --- | --- | --- | --- |
| `IfTrue()` | the value is `true` | Argument | `True` |
| `IfFalse()` | the value is `false` | Argument | `False` |
| `IfEmpty()` | the `Guid` is `Guid.Empty` | Argument | `Empty` |
| `IfNotDefined()` | the enum value is not a named member, such as `(Color)42`. A `[Flags]` combination fails unless it has its own name. | OutOfRange | `NotDefined` |

### Dates and times

| Check | Fails when | Throws | Code |
| --- | --- | --- | --- |
| `IfBefore(limit)` | the value is earlier than `limit` | OutOfRange | `Before` |
| `IfAfter(limit)` | the value is later than `limit` | OutOfRange | `After` |
| `IfInPast()` | the value is earlier than now | OutOfRange | `InPast` |
| `IfInFuture()` | the value is later than now | OutOfRange | `InFuture` |

- `IfBefore` and `IfAfter` work on `DateTime` and `DateTimeOffset`, and on .NET 8 and
  later also on `DateOnly` and `TimeOnly`.
- `IfInPast` and `IfInFuture` work on `DateTime` and `DateTimeOffset`, and on .NET 8 and
  later also on `DateOnly` (compared with today, local time). A UTC `DateTime` is compared
  with `DateTime.UtcNow`, any other `DateTime` with `DateTime.Now`.
- On .NET 8 and later, pass a `TimeProvider` to control "now", for example a fake clock in
  tests:

```csharp
var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
expiresAt.Throw().IfInPast(TimeProvider.System);
```

## Error codes

Every failure has a stable error code, a constant on `ErrorCodes`. The code is the check
name without `If`, with a few exceptions: `IfNullOrEmpty` reports `Empty`,
`IfNullOrWhiteSpace` reports `WhiteSpace`, `If` and `IfNot` report `Condition`, and a null
value reports `Null`. Two codes do not come from checks: `Must` is the default for a
`Validator<T>.Must` rule, and `Invalid` is the default for a failure added with
`ValidationReport.Add`. Codes do not change between versions, so they are safe to store,
translate, or send to API clients. In throw mode the code reaches you through a
[custom exception factory](#custom-exceptions); in [collect mode](validation.md) it is on
every `ValidationFailure`.
