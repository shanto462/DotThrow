# Messages and localization

Every default message of DotThrow comes from a template, keyed by the error code of the
check. You can reword a template for the whole app, translate it per culture, and use the
same templates in your own checks.

The examples assume `using DotThrow;` and `using System.Globalization;`.

- [Templates](#templates)
- [Reword a message](#reword-a-message)
- [Translate messages](#translate-messages)
- [Which template is used](#which-template-is-used)
- [Placeholders](#placeholders)
- [Messages for one check or one rule](#messages-for-one-check-or-one-rule)
- [Templates in your own checks](#templates-in-your-own-checks)
- [Built-in translations](#built-in-translations)
- [Every default template](#every-default-template)

## Templates

A template is text with named placeholders in braces:

```text
'{PropertyName}' must be at most {MaxLength} characters long.
```

`{PropertyName}` is the name of the value. The other placeholders are the values of the
check, such as the limit of `IfLongerThan`. The templates are listed at the
[end of this page](#every-default-template).

## Reword a message

`MessageTemplates.Set` replaces the template of an error code for every culture:

```csharp
MessageTemplates.Set(ErrorCodes.LongerThan, "{PropertyName} is too long: {TotalLength} of {MaxLength} characters.");

var title = "A very long title for a short field";
title.Throw().IfLongerThan(20);
```

```text
System.ArgumentException: title is too long: 35 of 20 characters. (Parameter 'title')
```

Set templates at startup. `MessageTemplates.Remove(key)` removes one, and
`MessageTemplates.Reset()` removes all of them and sets `Culture` back to `null`.
`MessageTemplates.Get(key)` returns the template that would be used now.

## Translate messages

Pass a culture to `Set` to add a template for that culture only:

```csharp
MessageTemplates.Set(ErrorCodes.Null, "'{PropertyName}' darf nicht null sein.", new CultureInfo("de"));
MessageTemplates.Set(ErrorCodes.LongerThan, "'{PropertyName}' darf höchstens {MaxLength} Zeichen haben.", new CultureInfo("de"));
```

The message culture is `MessageTemplates.Culture`. When it is `null` (the default),
DotThrow uses `CultureInfo.CurrentUICulture`, read when a check fails. So in a web app
that sets the UI culture per request, each request gets messages in its own language.

```csharp
MessageTemplates.Culture = new CultureInfo("de-AT");
```

## Which template is used

When a check fails, DotThrow looks for the template in this order. At each step it tries
a variant key first (see below), then the plain error code.

1. Templates set for the message culture, then for its parent cultures (`de-AT`, then
   `de`).
2. Templates set for every culture (`Set` without a culture).
3. Built-in translations for the culture and its parents.
4. The English defaults.
5. A last fallback: `'{PropertyName}' is not valid.`

A template you set for every culture beats a built-in translation, so the text of your
app always wins. Resolved templates are cached per culture, so a failure does not search twice, and
every member of `MessageTemplates` is thread-safe.

A few codes have **variant keys**, `"Code.Variant"`, for wording that depends on the
arguments. For example `CountEqualTo.One` is used for a count of exactly one item ("1
item" instead of "1 items"), and `EqualTo.Value` shows the compared value, which DotThrow
does only for value types. Setting the plain code replaces its variants too, unless you
set a variant as well. The table at the end lists every variant.

## Placeholders

| Placeholder | Value |
| --- | --- |
| `{PropertyName}` | The name of the value. Every template can use it. |
| `{PropertyValue}` | The checked value. No default template uses it, so it appears only when you write it. Think about secrets first. |
| `{ComparisonValue}` | The limit or compared value of comparison and equality checks. |
| `{From}`, `{To}` | The ends of a range. |
| `{MinLength}`, `{MaxLength}`, `{Length}`, `{TotalLength}` | String length limits, and the actual length (`{TotalLength}`). |
| `{Count}`, `{MinCount}`, `{MaxCount}` | Collection count limits. |
| `{ExpectedPrecision}`, `{ExpectedScale}`, `{Digits}`, `{ActualScale}` | `IfPrecisionScaleExceeded`. |
| `{Divisor}` | `IfNotMultipleOf`. |
| `{MaxAge}`, `{MinAge}` | `IfOlderThan`, `IfNewerThan`. |
| `{Pattern}` | A regular expression. |
| `{Value}` | A substring, prefix, suffix or collection item. |
| `{Characters}` | The characters of `IfContainsAny` and `IfContainsAnyExcept`. |
| `{Key}` | A dictionary key. |
| `{Flag}` | An enum flag. |
| `{Schemes}` | The URI schemes of `IfNotScheme`. |
| `{Type}` | A type name. |
| `{Condition}` | The source text of a predicate. |

[guards.md](guards.md#checks-by-family) lists the placeholders of every check.

Values are written with the invariant culture, so a message does not change with the
machine: strings in single quotes, `null` as `null`, `double` and `float` in the shortest
form that reads back to the same number, and dates in the round-trip format (`"O"`): a
`DateOnly` as `2026-05-02`, a `DateTime` as `2026-05-02T00:00:00.0000000`. To show a
date in another way, use a format in your template, such as `{ComparisonValue:yyyy-MM-dd}`. A placeholder can carry a format: `{From:N2}`. A placeholder without a
value, and any other text in braces, stays as written.

## Messages for one check or one rule

The `message` argument of every check takes the same placeholders:

```csharp
var pageSize = 500;
pageSize.Throw().IfGreaterThan(100, "Ask for at most {ComparisonValue} items per page.");
```

In a `Validator<T>`, `rule => rule.WithMessage(...)` replaces the message of the whole
rule. It takes `{PropertyName}`, `{PropertyPath}`, `{PropertyValue}`,
`{CollectionIndex}`, and the arguments added with `context.AppendArgument` (see
[validation.md](validation.md#rule-options)).

## Templates in your own checks

A custom check can read a template from `MessageTemplates` and fill it in with
`MessageTemplates.Format`. Then an app can reword and translate it like a built-in
message:

```csharp
public static class SkuChecks
{
    public static Guard<string> IfNotSku(this Guard<string> guard, string? message = null)
    {
        if (guard.IsActive && (guard.Value is null || !guard.Value.StartsWith("SKU-", StringComparison.Ordinal)))
        {
            var template = message ?? MessageTemplates.Get("NotSku") ?? "'{PropertyName}' must look like SKU-1234.";
            return guard.Fail("NotSku", MessageTemplates.Format(template, guard.Name));
        }

        return guard;
    }
}
```

```csharp
MessageTemplates.Set("NotSku", "'{PropertyName}' muss wie SKU-1234 aussehen.", new CultureInfo("de"));
```

`MessageTemplates.Format(template, name, values)` also takes a dictionary of values for
your own placeholders. See [custom-checks.md](custom-checks.md).

## Built-in translations

DotThrow ships translations of every default template for 58 languages, the set that
FluentValidation ships: `ar`, `az`, `be`, `bg`, `bn`, `bs`, `ca`, `cs`, `cy`, `da`, `de`,
`el`, `es`, `et`, `fa`, `fi`, `fr`, `he`, `hi`, `hr`, `hu`, `id`, `is`, `it`, `ja`, `ka`,
`kk`, `km`, `ko`, `lt`, `lv`, `mk`, `nb`, `nl`, `nn`, `pl`, `pt`, `pt-BR`, `rm`, `ro`,
`ru`, `sk`, `sl`, `sq`, `sr`, `sr-Latn`, `sv`, `ta`, `te`, `tg`, `th`, `tr`, `uk`, `uz`,
`uz-Cyrl`, `vi`, `zh-Hans` and `zh-Hant`. A specific culture uses its parent, so `de`
serves `de-AT` and `de-CH`.

The translations are machine-assisted; native review is welcome. They are step 3 of the
[lookup order](#which-template-is-used), so templates you set always win. A language is
loaded the first time a message needs it, and only then.

```csharp
MessageTemplates.Culture = new CultureInfo("fr-FR");
var title = "A very long title for a short field";
title.Throw().IfLongerThan(20);   // the message is in French
```

To improve or add a translation, edit `src/DotThrow/Translations/<culture>.txt`: one
`Key<TAB>Template` line for every key in the table below. Keep every placeholder of the
English text, and do not add `{PropertyValue}`. [CONTRIBUTING.md](../CONTRIBUTING.md)
explains how to send a change.

## Every default template

The English default for every key. A key with a dot is a variant.

<!-- templates:start -->

**General**

| Key | English template |
| --- | --- |
| `Null` | '{PropertyName}' must not be null. |
| `NotNull` | '{PropertyName}' must be null. |
| `Default` | '{PropertyName}' must not be the default value. |
| `Empty` | '{PropertyName}' must not be empty. |
| `Empty.Guid` | '{PropertyName}' must not be an empty GUID. |
| `NotEmpty` | '{PropertyName}' must be empty. |
| `EqualTo` | '{PropertyName}' must not be equal to the rejected value. |
| `EqualTo.Value` | '{PropertyName}' must not be equal to {ComparisonValue}. |
| `NotEqualTo` | '{PropertyName}' must be equal to the required value. |
| `NotEqualTo.Value` | '{PropertyName}' must be equal to {ComparisonValue}. |
| `OneOf` | '{PropertyName}' must not be one of the rejected values. |
| `NotOneOf` | '{PropertyName}' must be one of the allowed values. |
| `Condition` | '{PropertyName}' is not valid. |
| `Condition.If` | '{PropertyName}' must not satisfy the condition: {Condition}. |
| `Condition.IfNot` | '{PropertyName}' must satisfy the condition: {Condition}. |
| `Type` | '{PropertyName}' must not be of type {Type}. |
| `NotType` | '{PropertyName}' must be of type {Type}. |
| `True` | '{PropertyName}' must be false. |
| `False` | '{PropertyName}' must be true. |
| `Must` | '{PropertyName}' is not valid. |
| `Invalid` | '{PropertyName}' is not valid. |

**Comparisons and numbers**

| Key | English template |
| --- | --- |
| `GreaterThan` | '{PropertyName}' must be less than or equal to {ComparisonValue}. |
| `GreaterThanOrEqualTo` | '{PropertyName}' must be less than {ComparisonValue}. |
| `LessThan` | '{PropertyName}' must be greater than or equal to {ComparisonValue}. |
| `LessThanOrEqualTo` | '{PropertyName}' must be greater than {ComparisonValue}. |
| `OutOfRange` | '{PropertyName}' must be between {From} and {To}. |
| `OutOfRangeExclusive` | '{PropertyName}' must be between {From} and {To} (exclusive). |
| `InRange` | '{PropertyName}' must not be between {From} and {To}. |
| `Negative` | '{PropertyName}' must not be negative. |
| `NegativeOrZero` | '{PropertyName}' must be greater than zero. |
| `Positive` | '{PropertyName}' must not be greater than zero. |
| `PositiveOrZero` | '{PropertyName}' must be less than zero. |
| `Zero` | '{PropertyName}' must not be zero. |
| `NaN` | '{PropertyName}' must be a number, not NaN. |
| `Infinity` | '{PropertyName}' must be a finite number. |
| `NotFinite` | '{PropertyName}' must be a finite number. |
| `Odd` | '{PropertyName}' must be an even number. |
| `Even` | '{PropertyName}' must be an odd number. |
| `NotMultipleOf` | '{PropertyName}' must be a multiple of {Divisor}. |
| `NotPowerOfTwo` | '{PropertyName}' must be a power of two. |
| `NotInteger` | '{PropertyName}' must be a whole number. |
| `PrecisionScaleExceeded` | '{PropertyName}' must have at most {ExpectedPrecision} digits in total, with at most {ExpectedScale} decimals. Found {Digits} digits before the decimal point and {ActualScale} decimals. |

**Strings**

| Key | English template |
| --- | --- |
| `WhiteSpace` | '{PropertyName}' must not be empty or white space. |
| `LongerThan` | '{PropertyName}' must be at most {MaxLength} characters long. |
| `ShorterThan` | '{PropertyName}' must be at least {MinLength} characters long. |
| `LengthNotEqualTo` | '{PropertyName}' must be exactly {Length} characters long. |
| `LengthOutOfRange` | '{PropertyName}' must be between {MinLength} and {MaxLength} characters long. |
| `Contains` | '{PropertyName}' must not contain the rejected item. |
| `Contains.Value` | '{PropertyName}' must not contain {Value}. |
| `NotContains` | '{PropertyName}' must contain the required item. |
| `NotContains.Value` | '{PropertyName}' must contain {Value}. |
| `StartsWith` | '{PropertyName}' must not start with {Value}. |
| `NotStartsWith` | '{PropertyName}' must start with {Value}. |
| `EndsWith` | '{PropertyName}' must not end with {Value}. |
| `NotEndsWith` | '{PropertyName}' must end with {Value}. |
| `Matches` | '{PropertyName}' must not match the pattern {Pattern}. |
| `NotMatches` | '{PropertyName}' must match the pattern {Pattern}. |
| `NotEmailAddress` | '{PropertyName}' must be a valid email address. |
| `NotJson` | '{PropertyName}' must be valid JSON. |
| `NotXml` | '{PropertyName}' must be well-formed XML. |
| `NotUri` | '{PropertyName}' must be a valid URI. |
| `NotUri.Absolute` | '{PropertyName}' must be a valid absolute URI. |
| `NotUri.Relative` | '{PropertyName}' must be a valid relative URI. |
| `NotHttpUrl` | '{PropertyName}' must be an absolute HTTP or HTTPS URL. |
| `NotParsable` | '{PropertyName}' must be a valid {Type}. |
| `AllUpperCase` | '{PropertyName}' must not be all uppercase. |
| `AllLowerCase` | '{PropertyName}' must not be all lowercase. |
| `NotTrimmed` | '{PropertyName}' must not start or end with white space. |
| `ContainsWhiteSpace` | '{PropertyName}' must not contain white space. |
| `ContainsControlCharacters` | '{PropertyName}' must not contain control characters. |
| `ContainsAny` | '{PropertyName}' must not contain any of the rejected characters. |
| `ContainsAnyExcept` | '{PropertyName}' must contain only the allowed characters. |
| `NotAscii` | '{PropertyName}' must contain only ASCII characters. |
| `NotDigits` | '{PropertyName}' must contain only the digits 0 to 9. |
| `NotLettersOrDigits` | '{PropertyName}' must contain only letters or digits. |
| `NotHex` | '{PropertyName}' must be a hexadecimal string. |
| `NotBase64` | '{PropertyName}' must be a valid Base64 string. |
| `NotGuid` | '{PropertyName}' must be a valid GUID. |
| `NotIPAddress` | '{PropertyName}' must be a valid IP address. |
| `NotSlug` | '{PropertyName}' must be a slug of lowercase letters, digits and single hyphens. |
| `NotVersion` | '{PropertyName}' must be a valid version number. |
| `NotCreditCardNumber` | '{PropertyName}' must be a valid credit card number. |
| `NotEnumName` | '{PropertyName}' must be a defined {Type} name. |
| `NotFileName` | '{PropertyName}' must be a valid file name. |

**Collections and dictionaries**

| Key | English template |
| --- | --- |
| `CountEqualTo` | '{PropertyName}' must not have exactly {Count} items. |
| `CountEqualTo.One` | '{PropertyName}' must not have exactly 1 item. |
| `CountNotEqualTo` | '{PropertyName}' must have exactly {Count} items. |
| `CountNotEqualTo.One` | '{PropertyName}' must have exactly 1 item. |
| `CountGreaterThan` | '{PropertyName}' must have at most {MaxCount} items. |
| `CountGreaterThan.One` | '{PropertyName}' must have at most 1 item. |
| `CountLessThan` | '{PropertyName}' must have at least {MinCount} items. |
| `CountLessThan.One` | '{PropertyName}' must have at least 1 item. |
| `CountOutOfRange` | '{PropertyName}' must have between {MinCount} and {MaxCount} items. |
| `ContainsNull` | '{PropertyName}' must not contain null items. |
| `HasDuplicates` | '{PropertyName}' must not contain duplicate items. |
| `Any` | '{PropertyName}' must not contain any item that matches the condition. |
| `None` | '{PropertyName}' must contain at least one item that matches the condition. |
| `ContainsKey` | '{PropertyName}' must not contain the rejected key. |
| `ContainsKey.Value` | '{PropertyName}' must not contain the key {Key}. |
| `NotContainsKey` | '{PropertyName}' must contain the required key. |
| `NotContainsKey.Value` | '{PropertyName}' must contain the key {Key}. |

**Enums**

| Key | English template |
| --- | --- |
| `NotDefined` | '{PropertyName}' must be a defined {Type} value. |
| `HasFlag` | '{PropertyName}' must not have the flag {Flag}. |
| `NotHasFlag` | '{PropertyName}' must have the flag {Flag}. |

**Dates and times**

| Key | English template |
| --- | --- |
| `Before` | '{PropertyName}' must not be before {ComparisonValue}. |
| `After` | '{PropertyName}' must not be after {ComparisonValue}. |
| `InPast` | '{PropertyName}' must not be in the past. |
| `InFuture` | '{PropertyName}' must not be in the future. |
| `NotUtc` | '{PropertyName}' must be in UTC. |
| `Weekend` | '{PropertyName}' must be a weekday. |
| `Weekday` | '{PropertyName}' must be a weekend day. |
| `HasTimeOfDay` | '{PropertyName}' must not have a time of day. |
| `OlderThan` | '{PropertyName}' must not be more than {MaxAge} in the past. |
| `NewerThan` | '{PropertyName}' must be at least {MinAge} in the past. |

**Uri and IPAddress**

| Key | English template |
| --- | --- |
| `Relative` | '{PropertyName}' must be an absolute URI. |
| `NotHttps` | '{PropertyName}' must be an absolute HTTPS URL. |
| `NotHttpOrHttps` | '{PropertyName}' must be an absolute HTTP or HTTPS URL. |
| `NotScheme` | '{PropertyName}' must use one of these URI schemes: {Schemes}. |
| `Loopback` | '{PropertyName}' must not be a loopback address. |
| `Private` | '{PropertyName}' must not be a private or link-local address. |
| `NotIPv4` | '{PropertyName}' must be an IPv4 address. |
| `NotIPv6` | '{PropertyName}' must be an IPv6 address. |

**Streams and types**

| Key | English template |
| --- | --- |
| `NotReadable` | '{PropertyName}' must be readable. |
| `NotWritable` | '{PropertyName}' must be writable. |
| `NotSeekable` | '{PropertyName}' must be seekable. |
| `NotAssignableTo` | '{PropertyName}' must be assignable to {Type}. |
| `Abstract` | '{PropertyName}' must not be an abstract type. |
| `Interface` | '{PropertyName}' must not be an interface. |

<!-- templates:end -->
