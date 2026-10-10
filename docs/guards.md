# Guard clauses and every check

A guard clause checks an argument at the start of a method and throws when the argument
is wrong. With DotThrow you call `Throw()` on a value and chain the checks you need. The
same checks also run in [collect mode](validation.md), where a failure is recorded instead
of thrown.

The examples assume `using DotThrow;`.

- [How a chain works](#how-a-chain-works)
- [What is thrown](#what-is-thrown)
- [Names and messages](#names-and-messages)
- [Custom exceptions](#custom-exceptions)
- [Null values](#null-values)
- [AllowNull for optional values](#allownull-for-optional-values)
- [NaN](#nan)
- [Wrong arguments to a check](#wrong-arguments-to-a-check)
- [Checks by family](#checks-by-family)
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
| The value is outside a range or order | `ArgumentOutOfRangeException` | `IfOutOfRange`, `IfGreaterThan`, `IfNegative`, `IfOdd`, `IfBefore`, `IfWeekend`, `IfNotDefined` |
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

Every check takes an optional `message` that replaces the default message. A message can
use placeholders: `{PropertyName}` always, and the placeholders of the check, which the
tables below list. For example `IfLongerThan` fills in `{MaxLength}` and `{TotalLength}`:

```csharp
var title = "A very long title for a short field";
title.Throw().IfLongerThan(20, "{PropertyName} has {TotalLength} characters; keep it under {MaxLength}.");
```

```text
System.ArgumentException: title has 35 characters; keep it under 20. (Parameter 'title')
```

To reword or translate the default messages for the whole app, see
[localization.md](localization.md).

Default messages never contain the checked value, and never contain a compared value of
a reference type (the `other` of `IfEqualTo` and `IfNotEqualTo`, the `item` of a
collection `IfContains`, the `key` of `IfContainsKey`), so passwords and tokens do not end
up in logs. Limits and rules that you pass, such as `150`, `'ORD-'` or a pattern, do
appear. `{PropertyValue}` shows the value only when you write it in your own message.

.NET adds ` (Parameter 'name')` to the message, and `ArgumentOutOfRangeException` adds a
second line such as `Actual value was 200.`

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
- Every content check (strings, string formats, collections, dictionaries, comparisons,
  URIs, IP addresses, streams, types) also fails a null value, with
  `ArgumentNullException` and the code `Null`. So `name.Throw().IfLongerThan(50)` never
  lets a null through. The "Null value" column of the tables says how each check treats
  null.
- These checks treat null as a normal value: `IfNotNull`, `IfEqualTo`, `IfNotEqualTo`,
  `IfOneOf`, `IfNotOneOf`, `If` and `IfNot` (your predicate gets the null), `IfType` (null
  passes, it is not an instance of any type) and `IfNotEmpty` (null passes).
- `IfDefault` and `IfNotType` fail a null value with `ArgumentNullException`, but keep
  their own codes, `Default` and `NotType`.

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
`IfLessThanOrEqualTo`, `IfOutOfRange`, `IfOutOfRangeExclusive`, `IfInRange`,
`IfNegative`, `IfNegativeOrZero`, `IfPositive` and `IfPositiveOrZero`. NaN has no order,
so letting it pass an upper limit would let a bad value through. `IfZero` passes NaN,
because NaN is not zero. Use `IfNaN()`, `IfInfinity()` or `IfNotFinite()` to reject them
on their own.

## Wrong arguments to a check

The arguments of a check are checked too: a negative length or count, a minimum above
the maximum, a NaN or null limit, a zero divisor, a null predicate. These are bugs in the
calling code, so they always throw, even in collect mode:

```csharp
var age = 30;
age.Throw().IfOutOfRange(150, 0);
```

```text
System.ArgumentOutOfRangeException: The maximum must be greater than or equal to the minimum. (Parameter 'max')
Actual value was 0.
```

So that a passing check costs nothing extra, most argument mistakes that make every value
fail (such as `IfOutOfRange(150, 0)`) are detected only when the check fails. So they are
not reported when an earlier check already stopped the chain, or when `AllowNull()`
skipped a null value. A few are checked on every call, for example a negative minimum of
`IfShorterThan`, `IfLengthOutOfRange` or `IfCountLessThan`, and the arguments of
`IfInRange` and `IfPrecisionScaleExceeded`.

## Checks by family

Every check also takes an optional `message`. In the tables:

- **Throws**: "Argument" is `ArgumentException`, "OutOfRange" is
  `ArgumentOutOfRangeException`, "ArgumentNull" is `ArgumentNullException`.
- **Null value**: "fails, `Null`" means a null value fails first, with
  `ArgumentNullException` and the code `Null`. "checked" means null is a normal value for
  the check. "n/a" means the type cannot be null.
- **Placeholders**: the values a custom message or template can use, besides
  `{PropertyName}` and `{PropertyValue}`.
- **.NET 8+**: the overload needs .NET 8 or later. Everything else works on every target
  (netstandard2.0, netstandard2.1, net462, net8.0, net9.0, net10.0).

<!-- checks:start -->

### Any value

Work on a value of any type. Null is a normal value here, except where noted.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNull()` | any value, `T?` value types | the value is null. | ArgumentNull | `Null` | fails | - |
| `IfNotNull()` | any value | the value is not null. | Argument | `NotNull` | checked | - |
| `IfDefault()` | any value | the value is the default value of its type: null, zero, `Guid.Empty`, `DateTime.MinValue` and so on. A null value throws an `ArgumentNullException` (still with the `ErrorCodes.Default` code). | Argument | `Default` | fails, ArgumentNull | - |
| `IfEqualTo(other, comparer?)` | any value | the value equals `other`. | Argument | `EqualTo` | checked | `{ComparisonValue}` |
| `IfNotEqualTo(other, comparer?)` | any value | the value does not equal `other`. | Argument | `NotEqualTo` | checked | `{ComparisonValue}` |
| `IfOneOf(values)`<br>`IfOneOf(values, comparer?)` | any value | the value is one of `values`. | Argument | `OneOf` | checked | - |
| `IfNotOneOf(values)`<br>`IfNotOneOf(values, comparer?)` | any value | the value is not one of `values`. | Argument | `NotOneOf` | checked | - |
| `If(predicate)` | any value | `predicate` returns true for the value. | Argument | `Condition` | checked | `{Condition}` |
| `IfNot(predicate)` | any value | `predicate` returns false for the value. | Argument | `Condition` | checked | `{Condition}` |
| `IfType(type)` | any value | the value is an instance of `type` or a type derived from it. | Argument | `Type` | checked | `{Type}` |
| `IfNotType(type)` | any value | the value is not an instance of `type` or a type derived from it. A null value fails with an `ArgumentNullException` (still with the `ErrorCodes.NotType` code). | Argument | `NotType` | fails, ArgumentNull | `{Type}` |

### Empty values

FluentValidation's `NotEmpty` and `Empty`, for any type.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfEmpty()` | any value | the value is empty, like FluentValidation's `NotEmpty`: an empty or white-space string, a collection without items, a seekable stream of length 0, or the default value of a value type (`0`, `Empty`, ...). A null value fails with the `ErrorCodes.Null` code. | Argument | `Empty` | fails, `Null` | - |
| `IfNotEmpty()` | any value | the value is not empty, like FluentValidation's `Empty`. A null value, an empty or white-space string, an empty collection or stream, and the default value of a value type all pass. | Argument | `NotEmpty` | checked | - |

### Comparisons and ranges

Work on any type that implements `IComparable<T>`: every number type, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Version`, `string` (ordinal) and your own types. The sign checks treat `default(T)` as zero. NaN fails every ordering check.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfGreaterThan(limit)` | comparable values | the value is greater than `limit`. | OutOfRange | `GreaterThan` | fails, `Null` | `{ComparisonValue}` |
| `IfGreaterThanOrEqualTo(limit)` | comparable values | the value is greater than or equal to `limit`. | OutOfRange | `GreaterThanOrEqualTo` | fails, `Null` | `{ComparisonValue}` |
| `IfLessThan(limit)` | comparable values | the value is less than `limit`. | OutOfRange | `LessThan` | fails, `Null` | `{ComparisonValue}` |
| `IfLessThanOrEqualTo(limit)` | comparable values | the value is less than or equal to `limit`. | OutOfRange | `LessThanOrEqualTo` | fails, `Null` | `{ComparisonValue}` |
| `IfOutOfRange(min, max)` | comparable values | the value is outside the range from `min` to `max`. Both ends are allowed (FluentValidation `InclusiveBetween`). | OutOfRange | `OutOfRange` | fails, `Null` | `{From}`, `{To}` |
| `IfOutOfRangeExclusive(min, max)` | comparable values | the value is not strictly between `min` and `max`: the ends themselves fail too (FluentValidation `ExclusiveBetween`). | OutOfRange | `OutOfRangeExclusive` | fails, `Null` | `{From}`, `{To}` |
| `IfInRange(min, max)` | comparable values | the value is inside the range from `min` to `max`, ends included. | OutOfRange | `InRange` | fails, `Null` | `{From}`, `{To}` |
| `IfNegative()` | numbers and other value types | the value is less than zero. NaN fails. | OutOfRange | `Negative` | n/a | - |
| `IfNegativeOrZero()` | numbers and other value types | the value is less than or equal to zero. NaN fails. | OutOfRange | `NegativeOrZero` | n/a | - |
| `IfPositive()` | numbers and other value types | the value is greater than zero. NaN fails. | OutOfRange | `Positive` | n/a | - |
| `IfPositiveOrZero()` | numbers and other value types | the value is greater than or equal to zero. NaN fails. | OutOfRange | `PositiveOrZero` | n/a | - |
| `IfZero()` | numbers and other value types | the value is zero. NaN is not zero, so it passes. | OutOfRange | `Zero` | n/a | - |

### Numbers

Number-specific checks. The generic overloads use generic math and need .NET 8 or later.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNaN()` | `double`, `float`, any floating-point type (.NET 8+) | the value is NaN (not a number). | OutOfRange | `NaN` | n/a | - |
| `IfInfinity()` | `double`, `float`, any floating-point type (.NET 8+) | the value is positive or negative infinity. | OutOfRange | `Infinity` | n/a | - |
| `IfNotFinite()` | `double`, `float`, any floating-point type (.NET 8+) | the value is NaN or positive or negative infinity. | OutOfRange | `NotFinite` | n/a | - |
| `IfOdd()` | `int`, `long`, any integer type (.NET 8+) | the number is odd. Negative odd numbers fail too. | OutOfRange | `Odd` | n/a | - |
| `IfEven()` | `int`, `long`, any integer type (.NET 8+) | the number is even. Zero is even. | OutOfRange | `Even` | n/a | - |
| `IfNotMultipleOf(divisor)` | `int`, `long`, `decimal`, any integer type (.NET 8+) | the number is not a multiple of `divisor`. Zero is a multiple of every divisor. | OutOfRange | `NotMultipleOf` | n/a | `{Divisor}` |
| `IfNotPowerOfTwo()` | `int`, `long`, any integer type (.NET 8+) | the number is not a power of two (1, 2, 4, 8, ...). Zero and negative numbers fail. | OutOfRange | `NotPowerOfTwo` | n/a | - |
| `IfNotInteger()` | `double`, `float`, `decimal` | the number has a fractional part. NaN and infinity fail. | OutOfRange | `NotInteger` | n/a | - |
| `IfPrecisionScaleExceeded(precision, scale, ignoreTrailingZeros?)` | `decimal` | the number has more than `precision` digits in total, or more than `scale` digits after the decimal point, like FluentValidation's `PrecisionScale` (a SQL `decimal(precision, scale)` column). So the number may have at most `precision - scale` digits before the decimal point. | OutOfRange | `PrecisionScaleExceeded` | n/a | `{ExpectedPrecision}`, `{ExpectedScale}`, `{Digits}`, `{ActualScale}` |

### Strings

A null string fails every check here with the code `Null`, except `IfEqualTo` and `IfNotEqualTo`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNullOrEmpty()` | `string` | the string is null or empty. | Argument | `Empty` | fails, `Null` | - |
| `IfNullOrWhiteSpace()` | `string` | the string is null, empty, or only white-space characters. | Argument | `WhiteSpace` | fails, `Null` | - |
| `IfLongerThan(maxLength)` | `string` | the string has more than `maxLength` characters. | Argument | `LongerThan` | fails, `Null` | `{MaxLength}`, `{TotalLength}` |
| `IfShorterThan(minLength)` | `string` | the string has fewer than `minLength` characters. | Argument | `ShorterThan` | fails, `Null` | `{MinLength}`, `{TotalLength}` |
| `IfLengthNotEqualTo(length)` | `string` | the string does not have exactly `length` characters. | Argument | `LengthNotEqualTo` | fails, `Null` | `{Length}`, `{MinLength}`, `{MaxLength}`, `{TotalLength}` |
| `IfLengthOutOfRange(minLength, maxLength)` | `string` | the string length is outside the range from `minLength` to `maxLength`. Both ends are allowed. | Argument | `LengthOutOfRange` | fails, `Null` | `{MinLength}`, `{MaxLength}`, `{TotalLength}` |
| `IfEqualTo(other, comparison)` | `string` | the string equals `other` under `comparison`. The default message does not show `other`, which may be a secret; a custom message can use `{ComparisonValue}`. | Argument | `EqualTo` | checked | `{ComparisonValue}` |
| `IfNotEqualTo(other, comparison)` | `string` | the string does not equal `other` under `comparison`. The default message does not show `other`, which may be a secret; a custom message can use `{ComparisonValue}`. | Argument | `NotEqualTo` | checked | `{ComparisonValue}` |
| `IfContains(value, comparison?)` | `string` | the string contains `value`. | Argument | `Contains` | fails, `Null` | `{Value}` |
| `IfNotContains(value, comparison?)` | `string` | the string does not contain `value`. | Argument | `NotContains` | fails, `Null` | `{Value}` |
| `IfStartsWith(value, comparison?)` | `string` | the string starts with `value`. | Argument | `StartsWith` | fails, `Null` | `{Value}` |
| `IfNotStartsWith(value, comparison?)` | `string` | the string does not start with `value`. | Argument | `NotStartsWith` | fails, `Null` | `{Value}` |
| `IfEndsWith(value, comparison?)` | `string` | the string ends with `value`. | Argument | `EndsWith` | fails, `Null` | `{Value}` |
| `IfNotEndsWith(value, comparison?)` | `string` | the string does not end with `value`. | Argument | `NotEndsWith` | fails, `Null` | `{Value}` |
| `IfMatches(pattern, options?)`<br>`IfMatches(regex)` | `string` | the string matches the regular expression `pattern`. | Argument | `Matches` | fails, `Null` | `{Pattern}` |
| `IfNotMatches(pattern, options?)`<br>`IfNotMatches(regex)` | `string` | the string does not match the regular expression `pattern`. | Argument | `NotMatches` | fails, `Null` | `{Pattern}` |
| `IfNotEmailAddress()` | `string` | the string is not a plausible email address: one `@` with text on both sides, no white space, and no dot at the start or end of either side. This is a format check only. Send a confirmation email to prove that an address exists. | Argument | `NotEmailAddress` | fails, `Null` | - |
| `IfNotJson()` | `string` | the string is not valid JSON (RFC 8259). Any JSON value passes, including `42`, `"text"` and `null`. The check does not allocate. | Argument | `NotJson` | fails, `Null` | - |
| `IfNotXml()` | `string` | the string is not a well-formed XML document. DTDs are rejected and external entities are never resolved, so the check is safe for untrusted input. | Argument | `NotXml` | fails, `Null` | - |
| `IfNotUri(kind?)` | `string` | the string is not a URI of the given kind. A bare file path such as `/etc/hosts` or `C:\temp` is not treated as an absolute URI. Write `file:///etc/hosts` for a file URI. | Argument | `NotUri` | fails, `Null` | - |
| `IfNotHttpUrl()` | `string` | the string is not an absolute `http` or `https` URL. | Argument | `NotHttpUrl` | fails, `Null` | - |
| `IfNotParsable<T>(provider?)` (.NET 8+) | `string` | the string cannot be parsed into `TResult`, for example `int`, `decimal`, `Guid` or `DateOnly`. | Argument | `NotParsable` | fails, `Null` | `{Type}` |
| `IfAllUpperCase()` | `string` | the string has at least one letter and every letter is uppercase. Digits and symbols are ignored. | Argument | `AllUpperCase` | fails, `Null` | - |
| `IfAllLowerCase()` | `string` | the string has at least one letter and every letter is lowercase. Digits and symbols are ignored. | Argument | `AllLowerCase` | fails, `Null` | - |

`IfNotEmailAddress` also fails a control character and two dots in a row. It checks the format only; send a confirmation email to prove that an address exists.

### String formats and characters

A null string fails with the code `Null`. The "Not..." format checks also fail an empty string.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNotTrimmed()` | `string` | the string starts or ends with a white-space character. An empty string passes. | Argument | `NotTrimmed` | fails, `Null` | - |
| `IfContainsWhiteSpace()` | `string` | the string contains a white-space character (`IsWhiteSpace`), including Unicode spaces. | Argument | `ContainsWhiteSpace` | fails, `Null` | - |
| `IfContainsControlCharacters()` | `string` | the string contains a control character (`IsControl`: U+0000 to U+001F and U+007F to U+009F), such as a null character, a tab or a line break. | Argument | `ContainsControlCharacters` | fails, `Null` | - |
| `IfContainsAny(characters)` | `string` | the string contains any of `characters`. | Argument | `ContainsAny` | fails, `Null` | `{Characters}` |
| `IfContainsAnyExcept(characters)` | `string` | the string contains a character that is not one of `characters`. An empty string passes. | Argument | `ContainsAnyExcept` | fails, `Null` | `{Characters}` |
| `IfNotAscii()` | `string` | the string contains a character outside the ASCII range (above U+007F). An empty string passes. | Argument | `NotAscii` | fails, `Null` | - |
| `IfNotDigits()` | `string` | the string is empty or contains a character other than the ASCII digits `0` to `9`. Signs, spaces and digits of other scripts fail. | Argument | `NotDigits` | fails, `Null` | - |
| `IfNotLettersOrDigits()` | `string` | the string is empty or contains a character that is not a letter or a digit (`IsLetterOrDigit`, any script). Combine with `IfNotAscii` to allow only `A` to `Z`, `a` to `z` and `0` to `9`. | Argument | `NotLettersOrDigits` | fails, `Null` | - |
| `IfNotHex()` | `string` | the string is empty or contains a character other than the hexadecimal digits `0-9`, `a-f` and `A-F`. A `0x` prefix fails. | Argument | `NotHex` | fails, `Null` | - |
| `IfNotBase64()` | `string` | the string is not standard Base64 (RFC 4648, with `+`, `/` and `=` padding), read the way `FromBase64String` reads it: spaces, tabs and line breaks are ignored. An empty string fails. The URL-safe alphabet (`-` and `_`) fails. The check does not decode or allocate. | Argument | `NotBase64` | fails, `Null` | - |
| `IfNotGuid()` | `string` | the string is not a GUID in one of the formats that `TryParse` reads (`N`, `D`, `B`, `P` or `X`). | Argument | `NotGuid` | fails, `Null` | - |
| `IfNotIPAddress()` | `string` | the string is not an IPv4 or IPv6 address. IPv4 must be four decimal numbers from 0 to 255 without leading zeros (`192.168.0.1`); the short and octal forms that `TryParse` accepts, such as `1` or `010.0.0.1`, fail. IPv6 follows RFC 4291 (eight groups of up to four hex digits, one `::`, an optional IPv4 tail such as `::ffff:1.2.3.4`) and may have a zone (`fe80::1%eth0`), but no brackets or port. The check does not allocate and gives the same answer on every framework. | Argument | `NotIPAddress` | fails, `Null` | - |
| `IfNotSlug()` | `string` | the string is not a URL slug: lowercase ASCII letters and digits in groups joined by single hyphens, such as `hello-world-2`. No hyphen at the start or end. | Argument | `NotSlug` | fails, `Null` | - |
| `IfNotVersion()` | `string` | the string is not a `Version`: two to four numbers joined by dots, such as `1.2` or `1.2.3.4`, each from 0 to `MaxValue`. Only digits and dots are allowed (no spaces or signs), so this is stricter than `TryParse`. Semantic versions with a label such as `1.2.3-beta` fail. | Argument | `NotVersion` | fails, `Null` | - |
| `IfNotCreditCardNumber()` | `string` | the string is not a credit card number, like FluentValidation's `CreditCard`: spaces and hyphens are ignored, every other character must be a digit, and the digits must pass the Luhn checksum. Unlike FluentValidation, only the ASCII digits count and a string without digits fails. The length is not checked. | Argument | `NotCreditCardNumber` | fails, `Null` | - |
| `IfNotEnumName<TEnum>(ignoreCase?)` | `string` | the string is not the name of a member of `TEnum`, like FluentValidation's `IsEnumName`. Numbers (`"1"`) and flag combinations (`"Read, Write"`) fail. Call it with the enum type: `IfNotEnumName<Color>()`. | Argument | `NotEnumName` | fails, `Null` | `{Type}` |
| `IfNotFileName()` | `string` | the string is not a valid file name (not a path) on the current operating system: it is empty, it is `.` or `..`, or it contains a character from `GetInvalidFileNameChars`. | Argument | `NotFileName` | fails, `Null` | - |

### Collections

Work on arrays, lists, sets, dictionaries and any other `IEnumerable`. The count checks have overloads for the common shapes (`T[]`, `List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`, `ICollection<T>`, `IList<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`) that read `Count` or `Length` directly. A null collection fails with the code `Null`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNullOrEmpty()` | collections, dictionaries | the collection is null or has no items. | Argument | `Empty` | fails, `Null` | - |
| `IfCountEqualTo(count)` | collections, dictionaries | the collection has exactly `count` items. | Argument | `CountEqualTo` | fails, `Null` | `{Count}` |
| `IfCountNotEqualTo(count)` | collections, dictionaries | the collection does not have exactly `count` items. | Argument | `CountNotEqualTo` | fails, `Null` | `{Count}` |
| `IfCountGreaterThan(maxCount)` | collections, dictionaries | the collection has more than `maxCount` items. | Argument | `CountGreaterThan` | fails, `Null` | `{MaxCount}` |
| `IfCountLessThan(minCount)` | collections, dictionaries | the collection has fewer than `minCount` items. | Argument | `CountLessThan` | fails, `Null` | `{MinCount}` |
| `IfCountOutOfRange(minCount, maxCount)` | collections, dictionaries | the number of items is outside the range from `minCount` to `maxCount`. Both ends are allowed. | Argument | `CountOutOfRange` | fails, `Null` | `{MinCount}`, `{MaxCount}` |
| `IfContains(item, comparer?)` | collections | the collection contains `item`. | Argument | `Contains` | fails, `Null` | `{Value}` |
| `IfNotContains(item, comparer?)` | collections | the collection does not contain `item`. | Argument | `NotContains` | fails, `Null` | `{Value}` |
| `IfContainsNull()` | collections | any item in the collection is null. | Argument | `ContainsNull` | fails, `Null` | - |
| `IfHasDuplicates()` | collections | two items in the collection are equal, by their own `Equals`. This check allocates a `HashSet` for collections with two or more items. | Argument | `HasDuplicates` | fails, `Null` | - |
| `IfAny(predicate)` | collections | any item in the collection matches `predicate`. | Argument | `Any` | fails, `Null` | `{Condition}` |
| `IfNone(predicate)` | collections | no item in the collection matches `predicate` (an empty collection fails). | Argument | `None` | fails, `Null` | `{Condition}` |

### Dictionaries

For `Dictionary<TKey, TValue>`, `IDictionary<TKey, TValue>`, `IReadOnlyDictionary<TKey, TValue>` and any non-generic `IDictionary`. A null dictionary fails with the code `Null`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfContainsKey(key)` | dictionaries | the dictionary contains `key`. | Argument | `ContainsKey` | fails, `Null` | `{Key}` |
| `IfNotContainsKey(key)` | dictionaries | the dictionary does not contain `key`. | Argument | `NotContainsKey` | fails, `Null` | `{Key}` |

### Booleans, GUIDs and enums

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfTrue()` | `bool` | the value is true. | Argument | `True` | n/a | - |
| `IfFalse()` | `bool` | the value is false. | Argument | `False` | n/a | - |
| `IfEmpty()` | `Guid` | the value is `Guid.Empty`. | Argument | `Empty` | n/a | - |
| `IfNotDefined()` | enums | the value is not a defined value of `TEnum`, for example `(Color)42`. For a `[Flags]` enum any combination of defined flags passes, including `0` (no flags), like FluentValidation's `IsInEnum`. | OutOfRange | `NotDefined` | n/a | `{Type}` |
| `IfHasFlag(flag)` | enums with `[Flags]` | the value has every bit of `flag` set (`HasFlag`). A `flag` of `0` is in every value, so it always fails. | OutOfRange | `HasFlag` | n/a | `{Flag}` |
| `IfNotHasFlag(flag)` | enums with `[Flags]` | the value does not have every bit of `flag` set (`HasFlag`). | OutOfRange | `NotHasFlag` | n/a | `{Flag}` |

### Dates and times

On .NET 8 and later, `IfInPast` and `IfInFuture` also take a `TimeProvider`, so tests can control "now".

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfBefore(limit)` | `DateTime`, `DateTimeOffset`, `DateOnly` (.NET 8+), `TimeOnly` (.NET 8+) | `DateTime`: the value is earlier than `limit`.<br>`DateTimeOffset`: the value is earlier than `limit`.<br>`DateOnly`: the date is earlier than `limit`.<br>`TimeOnly`: the time is earlier than `limit`. | OutOfRange | `Before` | n/a | `{ComparisonValue}` |
| `IfAfter(limit)` | `DateTime`, `DateTimeOffset`, `DateOnly` (.NET 8+), `TimeOnly` (.NET 8+) | `DateTime`: the value is later than `limit`.<br>`DateTimeOffset`: the value is later than `limit`.<br>`DateOnly`: the date is later than `limit`.<br>`TimeOnly`: the time is later than `limit`. | OutOfRange | `After` | n/a | `{ComparisonValue}` |
| `IfInPast()`<br>`IfInPast(timeProvider)` | `DateTime`, `DateTimeOffset`, `DateOnly` (.NET 8+) | `DateTime`: the value is earlier than now. A UTC value is compared with `DateTime.UtcNow`, any other value with `DateTime.Now`.<br>`DateTimeOffset`: the value is earlier than now.<br>`DateOnly`: the date is before today (local time). | OutOfRange | `InPast` | n/a | - |
| `IfInFuture()`<br>`IfInFuture(timeProvider)` | `DateTime`, `DateTimeOffset`, `DateOnly` (.NET 8+) | `DateTime`: the value is later than now. A UTC value is compared with `DateTime.UtcNow`, any other value with `DateTime.Now`.<br>`DateTimeOffset`: the value is later than now.<br>`DateOnly`: the date is after today (local time). | OutOfRange | `InFuture` | n/a | - |

### Calendar

On .NET 8 and later, `IfOlderThan` and `IfNewerThan` also take a `TimeProvider`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNotUtc()` | `DateTime`, `DateTimeOffset` | `DateTime`: the `DateTime.Kind` is not `DateTimeKind.Utc`.<br>`DateTimeOffset`: the `DateTimeOffset.Offset` is not zero. | Argument | `NotUtc` | n/a | - |
| `IfWeekend()` | `DateTime`, `DateTimeOffset`, `DateOnly` (.NET 8+) | `DateTime`: the date is a Saturday or a Sunday.<br>`DateTimeOffset`: the date (in the value's own offset) is a Saturday or a Sunday.<br>`DateOnly`: the date is a Saturday or a Sunday. | OutOfRange | `Weekend` | n/a | - |
| `IfWeekday()` | `DateTime`, `DateTimeOffset`, `DateOnly` (.NET 8+) | `DateTime`: the date is a Monday to Friday.<br>`DateTimeOffset`: the date (in the value's own offset) is a Monday to Friday.<br>`DateOnly`: the date is a Monday to Friday. | OutOfRange | `Weekday` | n/a | - |
| `IfHasTimeOfDay()` | `DateTime`, `DateTimeOffset` | `DateTime`: the value has a time of day other than midnight.<br>`DateTimeOffset`: the value (in its own offset) has a time of day other than midnight. | Argument | `HasTimeOfDay` | n/a | - |
| `IfOlderThan(maxAge)`<br>`IfOlderThan(maxAge, timeProvider)` | `DateTime`, `DateTimeOffset` | `DateTime`: the value is more than `maxAge` in the past, for example a token issued too long ago. A value in the future passes.<br>`DateTimeOffset`: the value is more than `maxAge` in the past. A value in the future passes. | OutOfRange | `OlderThan` | n/a | `{MaxAge}` |
| `IfNewerThan(minAge)`<br>`IfNewerThan(minAge, timeProvider)` | `DateTime`, `DateTimeOffset` | `DateTime`: the value is less than `minAge` in the past, for example an account created too recently. A value in the future fails.<br>`DateTimeOffset`: the value is less than `minAge` in the past. A value in the future fails. | OutOfRange | `NewerThan` | n/a | `{MinAge}` |

### URIs

Checks on a `Uri` object. To check text, use the string checks `IfNotUri` and `IfNotHttpUrl`. A null URI fails with the code `Null`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfRelative()` | `Uri` | the URI is relative (`Uri.IsAbsoluteUri` is false). | Argument | `Relative` | fails, `Null` | - |
| `IfNotHttps()` | `Uri` | the URI is not an absolute `https` URI. A relative URI fails. | Argument | `NotHttps` | fails, `Null` | - |
| `IfNotHttpOrHttps()` | `Uri` | the URI is not an absolute `http` or `https` URI. A relative URI fails. | Argument | `NotHttpOrHttps` | fails, `Null` | - |
| `IfLoopback()` | `Uri` | the URI points to the local machine: the host is `localhost`, a name ending in `.localhost`, or a loopback IP address (`127.0.0.0/8`, `::1`). A relative URI has no host, so it passes. | Argument | `Loopback` | fails, `Null` | - |
| `IfNotScheme(schemes)` | `Uri` | the URI is relative or its scheme is not one of `schemes` (compared without case). | Argument | `NotScheme` | fails, `Null` | `{Schemes}` |

### IP addresses

Checks on an `IPAddress` object. To check text, use the string check `IfNotIPAddress`. A null address fails with the code `Null`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfLoopback()` | `IPAddress` | the address is a loopback address: `127.0.0.0/8`, `::1`, or an IPv4-mapped loopback address. | Argument | `Loopback` | fails, `Null` | - |
| `IfPrivate()` | `IPAddress` | the address is in a private or link-local range: IPv4 `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16` and `169.254.0.0/16`; IPv6 `fc00::/7` (unique local) and `fe80::/10` (link-local). Loopback addresses are not in these ranges; use `IfLoopback` for them. | Argument | `Private` | fails, `Null` | - |
| `IfNotIPv4()` | `IPAddress` | the address is not an IPv4 address. An IPv4-mapped IPv6 address is IPv6, so it fails. | Argument | `NotIPv4` | fails, `Null` | - |
| `IfNotIPv6()` | `IPAddress` | the address is not an IPv6 address. | Argument | `NotIPv6` | fails, `Null` | - |

### Streams

For any `Stream` type. An empty stream is caught by the generic `IfEmpty()`. A null stream fails with the code `Null`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNotReadable()` | streams | the stream cannot be read (`Stream.CanRead` is false). | Argument | `NotReadable` | fails, `Null` | - |
| `IfNotWritable()` | streams | the stream cannot be written (`Stream.CanWrite` is false). | Argument | `NotWritable` | fails, `Null` | - |
| `IfNotSeekable()` | streams | the stream cannot seek (`Stream.CanSeek` is false). | Argument | `NotSeekable` | fails, `Null` | - |

### Types

Checks on a `Type` object. A null type fails with the code `Null`.

| Check | On | Fails when | Throws | Code | Null value | Placeholders |
| --- | --- | --- | --- | --- | --- | --- |
| `IfNotAssignableTo(targetType)` | `Type` | a value of the type cannot be assigned to `targetType` (`IsAssignableFrom`), for example a type that does not implement a required interface. An open generic type such as `typeof(List<>)` is not assignable to a closed one. | Argument | `NotAssignableTo` | fails, `Null` | `{Type}` |
| `IfAbstract()` | `Type` | the type is abstract. Interfaces and static classes are abstract too. | Argument | `Abstract` | fails, `Null` | - |
| `IfInterface()` | `Type` | the type is an interface. | Argument | `Interface` | fails, `Null` | - |

<!-- checks:end -->

## Error codes

Every failure has a stable error code, a constant on `ErrorCodes`. The code is the check
name without `If`, with a few exceptions: `IfNullOrEmpty` reports `Empty`,
`IfNullOrWhiteSpace` reports `WhiteSpace`, `If` and `IfNot` report `Condition`, and a
null value reports `Null`. Two codes do not come from checks:
`Must` is the default of a `Validator<T>.Must` rule, and `Invalid` is the default of a
failure added with `ValidationReport.Add`. Codes do not change between versions, so they
are safe to store, translate, or send to API clients. In throw mode the code reaches you
through a [custom exception factory](#custom-exceptions); in [collect mode](validation.md)
it is on every `ValidationFailure`.
