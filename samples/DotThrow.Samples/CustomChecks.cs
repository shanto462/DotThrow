using System;
using System.Globalization;

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
        // 1. Do nothing when an earlier check failed (collect mode) or AllowNull() saw null.
        if (!guard.IsActive)
        {
            return guard;
        }

        // 2. Follow the null rule of the built-in checks: null fails with the "Null" code.
        if (guard.Value is null)
        {
            return guard.FailNull(message);
        }

        // 3. Check the value. Build the message only when the check fails.
        if (!IsSku(guard.Value))
        {
            return guard.Fail("NotSku", message ?? $"'{guard.Name}' must look like SKU-1234.");
        }

        return guard;
    }

    /// <summary>Fails when the number has more decimal places than <paramref name="places"/>.</summary>
    public static Guard<decimal> IfMoreDecimalPlacesThan(this Guard<decimal> guard, int places, string? message = null)
    {
        // A bad argument to the check itself is a bug in the caller, so it always throws.
        ArgumentOutOfRangeException.ThrowIfNegative(places);

        if (guard.IsActive && decimal.Round(guard.Value, places) != guard.Value)
        {
            // FailOutOfRange throws ArgumentOutOfRangeException, which carries the actual value.
            var text = message ?? $"'{guard.Name}' must have at most {places.ToString(CultureInfo.InvariantCulture)} decimal places.";
            return guard.FailOutOfRange("MoreDecimalPlaces", text);
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
