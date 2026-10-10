# Writing your own checks

A check is an extension method on `Guard<T>`. When you write one, it works everywhere the
built-in checks work: in guard clauses with `Throw()`, in collect mode with
`ValidationReport`, and in `Validator<T>` rules.

Before you write one, look at the [list of built-in checks](guards.md#checks-by-family):
there are more than 110, including formats such as `IfNotSlug`, `IfNotHex` and
`IfNotIPAddress`.

The examples assume `using DotThrow;`.

## A first check

This check accepts stock keeping units such as `SKU-1234`:

```csharp
public static class SkuChecks
{
    public static Guard<string> IfNotSku(this Guard<string> guard, string? message = null)
    {
        // 1. Do nothing when an earlier check failed (collect mode) or AllowNull() saw null.
        if (!guard.IsActive)
        {
            return guard;
        }

        // 2. Follow the null rule of the built-in checks: null fails with the code "Null".
        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        // 3. Check the value. Build the message only when the check fails, from a template
        //    that an app can reword or translate.
        if (!IsSku(guard.Value))
        {
            var template = message ?? MessageTemplates.Get("NotSku") ?? "'{PropertyName}' must look like SKU-1234.";
            return guard.Fail("NotSku", MessageTemplates.Format(template, guard.Name));
        }

        return guard;
    }

    private static bool IsSku(string value)
    {
        if (value.Length < 5 || !value.StartsWith("SKU-", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 4; i < value.Length; i++)
        {
            if (value[i] is not ((>= '0' and <= '9') or (>= 'A' and <= 'Z')))
            {
                return false;
            }
        }

        return true;
    }
}
```

Use it like any other check:

```csharp
var sku = "sku-12";

// Collect mode records the failure.
var report = new ValidationReport();
report.Check(sku).IfNullOrWhiteSpace().IfNotSku();
Console.WriteLine(report.Failures[0]);   // sku: 'sku' must look like SKU-1234.

// A guard clause throws ArgumentException with ParamName "sku".
sku.Throw().IfNullOrWhiteSpace().IfNotSku();
```

## The rules

1. **Return the guard unchanged when `guard.IsActive` is `false`.** That happens after a
   failed check in collect mode (so each value reports one failure; with the rule cascade
   `Continue` of a validator the guard stays active) and after `AllowNull()` found a null
   value.
2. **Fail a null value with `guard.FailNull(message)`**, unless null is a normal value for
   your check. This keeps the [null rule](guards.md#null-values) of the built-in checks.
3. **Report a failure with `Fail` or `FailOutOfRange`**, and return what they return.
   - `guard.Fail(code, message)` throws `ArgumentException` in throw mode.
   - `guard.FailOutOfRange(code, message)` throws `ArgumentOutOfRangeException`, which
     carries the value when it is a value type. Use it for ranges, limits and order.
   - In collect mode both record a `ValidationFailure` and return a guard that skips the
     later checks.
   - With `Throw(factory)` both throw the exception from the factory.
4. **Build the message only when the check fails.** Then a check that passes never
   formats a string. `Fail` uses the message as it is, so fill in placeholders with
   `MessageTemplates.Format(template, guard.Name, values)`. Read the template with
   `MessageTemplates.Get(code)` first, so an app can reword or translate it (see
   [localization.md](localization.md#templates-in-your-own-checks)).
5. **Never put the checked value in a default message**: it may be a password. Start the
   message with `'{PropertyName}'`, like the built-in messages.
6. **Pick a stable error code**: the check name without `If`, such as `"NotSku"`.
7. **Validate the arguments of your check by throwing.** A bad argument is a bug in the
   calling code, so it should throw even in collect mode.

## A check that throws ArgumentOutOfRangeException

```csharp
public static class OpeningHoursChecks
{
    public static Guard<DateTime> IfOutsideHours(this Guard<DateTime> guard, int openHour, int closeHour, string? message = null)
    {
        // Rule 7: a bad argument always throws.
        if (openHour < 0 || closeHour > 24 || openHour >= closeHour)
        {
            throw new ArgumentOutOfRangeException(nameof(closeHour), closeHour, "The hours must be in 0 to 24, and open before close.");
        }

        if (guard.IsActive && (guard.Value.Hour < openHour || guard.Value.Hour >= closeHour))
        {
            var template = message ?? MessageTemplates.Get("OutsideHours") ?? "'{PropertyName}' must be between {Open}:00 and {Close}:00.";
            var values = new Dictionary<string, object?> { ["Open"] = openHour, ["Close"] = closeHour };
            return guard.FailOutOfRange("OutsideHours", MessageTemplates.Format(template, guard.Name, values));
        }

        return guard;
    }
}
```

```csharp
var pickupTime = new DateTime(2026, 10, 12, 18, 30, 0);
pickupTime.Throw().IfOutsideHours(9, 17);
```

```text
System.ArgumentOutOfRangeException: 'pickupTime' must be between 9:00 and 17:00. (Parameter 'pickupTime')
Actual value was 10/12/2026 18:30:00.
```

A `DateTime` cannot be null, so this check has no null step. The format of the actual
value on the second line comes from .NET and follows the current culture.

## A check built from other checks

A check can simply chain other checks. The rules above still apply, because each
built-in check follows them:

```csharp
public static class PasswordChecks
{
    public static Guard<string> IfWeakPassword(this Guard<string> guard) =>
        guard
            .IfNullOrEmpty()
            .IfShorterThan(12, "Use at least {MinLength} characters.")
            .IfAllLowerCase("Mix uppercase and lowercase letters.")
            .IfAllUpperCase("Mix uppercase and lowercase letters.");
}
```

## Using custom checks in a validator

Rules take any chain, so custom checks fit in:

```csharp
public sealed record OrderLine(string Sku, int Quantity, decimal Price);

public sealed class OrderLineValidator : Validator<OrderLine>
{
    public OrderLineValidator()
    {
        For(x => x.Sku, sku => sku.IfNullOrWhiteSpace().IfNotSku());
        For(x => x.Quantity, quantity => quantity.IfOutOfRange(1, 100));
        For(x => x.Price, price => price.IfNegative().IfPrecisionScaleExceeded(9, 2));
    }
}
```

## What a guard gives you

| Member | Use |
| --- | --- |
| `guard.Value` | The checked value. |
| `guard.Name` | The name for messages: a parameter name, a property path, or an expression. |
| `guard.IsActive` | `false` after a failure in collect mode (unless the rule cascade is `Continue`) or after `AllowNull()` saw null. Return the guard unchanged then. |
| `guard.HasFailed` | `true` after a failure in collect mode. |
| `guard.Fail(code, message)` | Reports a failure. Default exception: `ArgumentException`. |
| `guard.FailOutOfRange(code, message)` | Reports a failure. Default exception: `ArgumentOutOfRangeException` with the value. |
| `guard.FailNull(message?)` | Reports a null value with the code `Null`. Default exception: `ArgumentNullException`. A custom message can use `{PropertyName}`. |

`Guard<T>` is a `ref struct`, so a check cannot store it or capture it in a lambda. Keep
the work inside the method, as in the examples.

The [samples project](../samples/DotThrow.Samples/CustomChecks.cs) has two custom checks
and runs them in all three modes.
