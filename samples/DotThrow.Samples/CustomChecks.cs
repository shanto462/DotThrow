using System;

namespace DotThrow.Samples;

/// <summary>
/// Your own checks are extension methods on <see cref="Guard{T}"/>. They work in throw mode,
/// in collect mode, and inside a <see cref="Validator{T}"/>, just like the built-in ones.
/// </summary>
public static class CustomChecks
{
    /// <summary>Fails when the string is not a stock keeping unit like "SKU-1234".</summary>
    public static Guard<string> IfNotSku(this Guard<string> guard, string? message = null)
    {
        // 1. Do nothing when an earlier check failed (collect mode) or WhenPresent() skipped a null.
        if (!guard.IsActive)
        {
            return guard;
        }

        // 2. Follow the null rule of the built-in checks: null fails with the "Null" code.
        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        // 3. Check the value. Build the message only when the check fails. Reading the
        //    template from MessageTemplates lets an app reword or translate it.
        if (!IsSku(guard.Value))
        {
            var template = message ?? MessageTemplates.Get("NotSku") ?? "'{PropertyName}' must look like SKU-1234.";
            return guard.Fail("NotSku", MessageTemplates.Format(template, guard.Name));
        }

        return guard;
    }

    /// <summary>Fails when the time is outside business hours (09:00 to 17:00).</summary>
    public static Guard<DateTime> IfOutsideBusinessHours(this Guard<DateTime> guard, string? message = null)
    {
        if (guard.IsActive && (guard.Value.Hour < 9 || guard.Value.Hour >= 17))
        {
            // FailOutOfRange throws ArgumentOutOfRangeException, which carries the actual value.
            var template = message ?? "'{PropertyName}' must be between 09:00 and 17:00.";
            return guard.FailOutOfRange("OutsideBusinessHours", MessageTemplates.Format(template, guard.Name));
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
