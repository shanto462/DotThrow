using System;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests.Guards;

// Every ordering and sign check on every type with a fast path (int, long, double, float,
// decimal, DateTime, ordinal string) and on types that use the generic IComparable path
// (short, TimeSpan, Version): a pass and a fail row each, with the exact message.
public sealed class ComparisonMatrixGuardTests
{
    private static readonly DateTime Jan = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Jun = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dec = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail<int>(10, (g, m) => g.IfGreaterThan(5, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 5.", label: "int: g.IfGreaterThan(5, m)");
        yield return Fail<int>(5, (g, m) => g.IfGreaterThanOrEqualTo(5, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 5.", label: "int: g.IfGreaterThanOrEqualTo(5, m)");
        yield return Fail<int>(1, (g, m) => g.IfLessThan(5, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 5.", label: "int: g.IfLessThan(5, m)");
        yield return Fail<int>(5, (g, m) => g.IfLessThanOrEqualTo(5, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 5.", label: "int: g.IfLessThanOrEqualTo(5, m)");
        yield return Fail<int>(10, (g, m) => g.IfOutOfRange(1, 5, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1 and 5.", label: "int: g.IfOutOfRange(1, 5, m)");
        yield return Fail<int>(5, (g, m) => g.IfInRange(1, 5, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1 and 5.", label: "int: g.IfInRange(1, 5, m)");
        yield return Fail<int>(1, (g, m) => g.IfOutOfRangeExclusive(1, 10, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1 and 10 (exclusive).", label: "int: g.IfOutOfRangeExclusive(1, 10, m)");
        yield return Fail<long>(10L, (g, m) => g.IfGreaterThan(5L, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 5.", label: "long: g.IfGreaterThan(5L, m)");
        yield return Fail<long>(5L, (g, m) => g.IfGreaterThanOrEqualTo(5L, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 5.", label: "long: g.IfGreaterThanOrEqualTo(5L, m)");
        yield return Fail<long>(1L, (g, m) => g.IfLessThan(5L, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 5.", label: "long: g.IfLessThan(5L, m)");
        yield return Fail<long>(5L, (g, m) => g.IfLessThanOrEqualTo(5L, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 5.", label: "long: g.IfLessThanOrEqualTo(5L, m)");
        yield return Fail<long>(10L, (g, m) => g.IfOutOfRange(1L, 5L, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1 and 5.", label: "long: g.IfOutOfRange(1L, 5L, m)");
        yield return Fail<long>(5L, (g, m) => g.IfInRange(1L, 5L, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1 and 5.", label: "long: g.IfInRange(1L, 5L, m)");
        yield return Fail<long>(1L, (g, m) => g.IfOutOfRangeExclusive(1L, 10L, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1 and 10 (exclusive).", label: "long: g.IfOutOfRangeExclusive(1L, 10L, m)");
        yield return Fail<short>((short)10, (g, m) => g.IfGreaterThan((short)5, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 5.", label: "short: g.IfGreaterThan((short)5, m)");
        yield return Fail<short>((short)5, (g, m) => g.IfGreaterThanOrEqualTo((short)5, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 5.", label: "short: g.IfGreaterThanOrEqualTo((short)5, m)");
        yield return Fail<short>((short)1, (g, m) => g.IfLessThan((short)5, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 5.", label: "short: g.IfLessThan((short)5, m)");
        yield return Fail<short>((short)5, (g, m) => g.IfLessThanOrEqualTo((short)5, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 5.", label: "short: g.IfLessThanOrEqualTo((short)5, m)");
        yield return Fail<short>((short)10, (g, m) => g.IfOutOfRange((short)1, (short)5, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1 and 5.", label: "short: g.IfOutOfRange((short)1, (short)5, m)");
        yield return Fail<short>((short)5, (g, m) => g.IfInRange((short)1, (short)5, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1 and 5.", label: "short: g.IfInRange((short)1, (short)5, m)");
        yield return Fail<short>((short)1, (g, m) => g.IfOutOfRangeExclusive((short)1, (short)10, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1 and 10 (exclusive).", label: "short: g.IfOutOfRangeExclusive((short)1, (short)10, m)");
        yield return Fail<double>(10.5, (g, m) => g.IfGreaterThan(5.5, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 5.5.", label: "double: g.IfGreaterThan(5.5, m)");
        yield return Fail<double>(5.5, (g, m) => g.IfGreaterThanOrEqualTo(5.5, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 5.5.", label: "double: g.IfGreaterThanOrEqualTo(5.5, m)");
        yield return Fail<double>(1.5, (g, m) => g.IfLessThan(5.5, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 5.5.", label: "double: g.IfLessThan(5.5, m)");
        yield return Fail<double>(5.5, (g, m) => g.IfLessThanOrEqualTo(5.5, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 5.5.", label: "double: g.IfLessThanOrEqualTo(5.5, m)");
        yield return Fail<double>(10.5, (g, m) => g.IfOutOfRange(1.5, 5.5, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1.5 and 5.5.", label: "double: g.IfOutOfRange(1.5, 5.5, m)");
        yield return Fail<double>(5.5, (g, m) => g.IfInRange(1.5, 5.5, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1.5 and 5.5.", label: "double: g.IfInRange(1.5, 5.5, m)");
        yield return Fail<double>(1.5, (g, m) => g.IfOutOfRangeExclusive(1.5, 10.5, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1.5 and 10.5 (exclusive).", label: "double: g.IfOutOfRangeExclusive(1.5, 10.5, m)");
        yield return Fail<float>(10.5f, (g, m) => g.IfGreaterThan(5.5f, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 5.5.", label: "float: g.IfGreaterThan(5.5f, m)");
        yield return Fail<float>(5.5f, (g, m) => g.IfGreaterThanOrEqualTo(5.5f, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 5.5.", label: "float: g.IfGreaterThanOrEqualTo(5.5f, m)");
        yield return Fail<float>(1.5f, (g, m) => g.IfLessThan(5.5f, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 5.5.", label: "float: g.IfLessThan(5.5f, m)");
        yield return Fail<float>(5.5f, (g, m) => g.IfLessThanOrEqualTo(5.5f, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 5.5.", label: "float: g.IfLessThanOrEqualTo(5.5f, m)");
        yield return Fail<float>(10.5f, (g, m) => g.IfOutOfRange(1.5f, 5.5f, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1.5 and 5.5.", label: "float: g.IfOutOfRange(1.5f, 5.5f, m)");
        yield return Fail<float>(5.5f, (g, m) => g.IfInRange(1.5f, 5.5f, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1.5 and 5.5.", label: "float: g.IfInRange(1.5f, 5.5f, m)");
        yield return Fail<float>(1.5f, (g, m) => g.IfOutOfRangeExclusive(1.5f, 10.5f, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1.5 and 10.5 (exclusive).", label: "float: g.IfOutOfRangeExclusive(1.5f, 10.5f, m)");
        yield return Fail<decimal>(10.5m, (g, m) => g.IfGreaterThan(5.5m, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 5.5.", label: "decimal: g.IfGreaterThan(5.5m, m)");
        yield return Fail<decimal>(5.5m, (g, m) => g.IfGreaterThanOrEqualTo(5.5m, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 5.5.", label: "decimal: g.IfGreaterThanOrEqualTo(5.5m, m)");
        yield return Fail<decimal>(1.5m, (g, m) => g.IfLessThan(5.5m, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 5.5.", label: "decimal: g.IfLessThan(5.5m, m)");
        yield return Fail<decimal>(5.5m, (g, m) => g.IfLessThanOrEqualTo(5.5m, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 5.5.", label: "decimal: g.IfLessThanOrEqualTo(5.5m, m)");
        yield return Fail<decimal>(10.5m, (g, m) => g.IfOutOfRange(1.5m, 5.5m, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1.5 and 5.5.", label: "decimal: g.IfOutOfRange(1.5m, 5.5m, m)");
        yield return Fail<decimal>(5.5m, (g, m) => g.IfInRange(1.5m, 5.5m, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1.5 and 5.5.", label: "decimal: g.IfInRange(1.5m, 5.5m, m)");
        yield return Fail<decimal>(1.5m, (g, m) => g.IfOutOfRangeExclusive(1.5m, 10.5m, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1.5 and 10.5 (exclusive).", label: "decimal: g.IfOutOfRangeExclusive(1.5m, 10.5m, m)");
        yield return Fail<DateTime>(Dec, (g, m) => g.IfGreaterThan(Jun, m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 2026-06-01T00:00:00.0000000Z.", label: "DateTime: g.IfGreaterThan(Jun, m)");
        yield return Fail<DateTime>(Jun, (g, m) => g.IfGreaterThanOrEqualTo(Jun, m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 2026-06-01T00:00:00.0000000Z.", label: "DateTime: g.IfGreaterThanOrEqualTo(Jun, m)");
        yield return Fail<DateTime>(Jan, (g, m) => g.IfLessThan(Jun, m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 2026-06-01T00:00:00.0000000Z.", label: "DateTime: g.IfLessThan(Jun, m)");
        yield return Fail<DateTime>(Jun, (g, m) => g.IfLessThanOrEqualTo(Jun, m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 2026-06-01T00:00:00.0000000Z.", label: "DateTime: g.IfLessThanOrEqualTo(Jun, m)");
        yield return Fail<DateTime>(Dec, (g, m) => g.IfOutOfRange(Jan, Jun, m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 2026-01-01T00:00:00.0000000Z and 2026-06-01T00:00:00.0000000Z.", label: "DateTime: g.IfOutOfRange(Jan, Jun, m)");
        yield return Fail<DateTime>(Jun, (g, m) => g.IfInRange(Jan, Jun, m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 2026-01-01T00:00:00.0000000Z and 2026-06-01T00:00:00.0000000Z.", label: "DateTime: g.IfInRange(Jan, Jun, m)");
        yield return Fail<DateTime>(Jan, (g, m) => g.IfOutOfRangeExclusive(Jan, Dec, m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 2026-01-01T00:00:00.0000000Z and 2026-12-01T00:00:00.0000000Z (exclusive).", label: "DateTime: g.IfOutOfRangeExclusive(Jan, Dec, m)");
        yield return Fail<string>("y", (g, m) => g.IfGreaterThan("m", m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 'm'.", label: "string: g.IfGreaterThan(\"m\", m)");
        yield return Fail<string>("m", (g, m) => g.IfGreaterThanOrEqualTo("m", m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 'm'.", label: "string: g.IfGreaterThanOrEqualTo(\"m\", m)");
        yield return Fail<string>("b", (g, m) => g.IfLessThan("m", m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 'm'.", label: "string: g.IfLessThan(\"m\", m)");
        yield return Fail<string>("m", (g, m) => g.IfLessThanOrEqualTo("m", m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 'm'.", label: "string: g.IfLessThanOrEqualTo(\"m\", m)");
        yield return Fail<string>("y", (g, m) => g.IfOutOfRange("b", "m", m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 'b' and 'm'.", label: "string: g.IfOutOfRange(\"b\", \"m\", m)");
        yield return Fail<string>("m", (g, m) => g.IfInRange("b", "m", m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 'b' and 'm'.", label: "string: g.IfInRange(\"b\", \"m\", m)");
        yield return Fail<string>("b", (g, m) => g.IfOutOfRangeExclusive("b", "y", m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 'b' and 'y' (exclusive).", label: "string: g.IfOutOfRangeExclusive(\"b\", \"y\", m)");
        yield return Fail<TimeSpan>(TimeSpan.FromMinutes(10), (g, m) => g.IfGreaterThan(TimeSpan.FromMinutes(5), m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 00:05:00.", label: "TimeSpan: g.IfGreaterThan(TimeSpan.FromMinutes(5), m)");
        yield return Fail<TimeSpan>(TimeSpan.FromMinutes(5), (g, m) => g.IfGreaterThanOrEqualTo(TimeSpan.FromMinutes(5), m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 00:05:00.", label: "TimeSpan: g.IfGreaterThanOrEqualTo(TimeSpan.FromMinutes(5), m)");
        yield return Fail<TimeSpan>(TimeSpan.FromMinutes(1), (g, m) => g.IfLessThan(TimeSpan.FromMinutes(5), m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 00:05:00.", label: "TimeSpan: g.IfLessThan(TimeSpan.FromMinutes(5), m)");
        yield return Fail<TimeSpan>(TimeSpan.FromMinutes(5), (g, m) => g.IfLessThanOrEqualTo(TimeSpan.FromMinutes(5), m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 00:05:00.", label: "TimeSpan: g.IfLessThanOrEqualTo(TimeSpan.FromMinutes(5), m)");
        yield return Fail<TimeSpan>(TimeSpan.FromMinutes(10), (g, m) => g.IfOutOfRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 00:01:00 and 00:05:00.", label: "TimeSpan: g.IfOutOfRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m)");
        yield return Fail<TimeSpan>(TimeSpan.FromMinutes(5), (g, m) => g.IfInRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 00:01:00 and 00:05:00.", label: "TimeSpan: g.IfInRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m)");
        yield return Fail<TimeSpan>(TimeSpan.FromMinutes(1), (g, m) => g.IfOutOfRangeExclusive(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 00:01:00 and 00:10:00 (exclusive).", label: "TimeSpan: g.IfOutOfRangeExclusive(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), m)");
        yield return Fail<Version>(new Version(10, 0), (g, m) => g.IfGreaterThan(new Version(5, 0), m), ErrorCodes.GreaterThan, Expect.OutOfRange, "'input' must be less than or equal to 5.0.", label: "Version: g.IfGreaterThan(new Version(5, 0), m)");
        yield return Fail<Version>(new Version(5, 0), (g, m) => g.IfGreaterThanOrEqualTo(new Version(5, 0), m), ErrorCodes.GreaterThanOrEqualTo, Expect.OutOfRange, "'input' must be less than 5.0.", label: "Version: g.IfGreaterThanOrEqualTo(new Version(5, 0), m)");
        yield return Fail<Version>(new Version(1, 0), (g, m) => g.IfLessThan(new Version(5, 0), m), ErrorCodes.LessThan, Expect.OutOfRange, "'input' must be greater than or equal to 5.0.", label: "Version: g.IfLessThan(new Version(5, 0), m)");
        yield return Fail<Version>(new Version(5, 0), (g, m) => g.IfLessThanOrEqualTo(new Version(5, 0), m), ErrorCodes.LessThanOrEqualTo, Expect.OutOfRange, "'input' must be greater than 5.0.", label: "Version: g.IfLessThanOrEqualTo(new Version(5, 0), m)");
        yield return Fail<Version>(new Version(10, 0), (g, m) => g.IfOutOfRange(new Version(1, 0), new Version(5, 0), m), ErrorCodes.OutOfRange, Expect.OutOfRange, "'input' must be between 1.0 and 5.0.", label: "Version: g.IfOutOfRange(new Version(1, 0), new Version(5, 0), m)");
        yield return Fail<Version>(new Version(5, 0), (g, m) => g.IfInRange(new Version(1, 0), new Version(5, 0), m), ErrorCodes.InRange, Expect.OutOfRange, "'input' must not be between 1.0 and 5.0.", label: "Version: g.IfInRange(new Version(1, 0), new Version(5, 0), m)");
        yield return Fail<Version>(new Version(1, 0), (g, m) => g.IfOutOfRangeExclusive(new Version(1, 0), new Version(10, 0), m), ErrorCodes.OutOfRangeExclusive, Expect.OutOfRange, "'input' must be between 1.0 and 10.0 (exclusive).", label: "Version: g.IfOutOfRangeExclusive(new Version(1, 0), new Version(10, 0), m)");
        yield return Fail<int>(-1, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.", label: "int: g.IfNegative(m)");
        yield return Fail<int>(0, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.", label: "int: g.IfNegativeOrZero(m)");
        yield return Fail<int>(1, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.", label: "int: g.IfPositive(m)");
        yield return Fail<int>(0, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.", label: "int: g.IfPositiveOrZero(m)");
        yield return Fail<int>(0, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.", label: "int: g.IfZero(m)");
        yield return Fail<long>(-1L, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.", label: "long: g.IfNegative(m)");
        yield return Fail<long>(0L, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.", label: "long: g.IfNegativeOrZero(m)");
        yield return Fail<long>(1L, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.", label: "long: g.IfPositive(m)");
        yield return Fail<long>(0L, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.", label: "long: g.IfPositiveOrZero(m)");
        yield return Fail<long>(0L, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.", label: "long: g.IfZero(m)");
        yield return Fail<short>((short)-1, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.", label: "short: g.IfNegative(m)");
        yield return Fail<short>((short)0, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.", label: "short: g.IfNegativeOrZero(m)");
        yield return Fail<short>((short)1, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.", label: "short: g.IfPositive(m)");
        yield return Fail<short>((short)0, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.", label: "short: g.IfPositiveOrZero(m)");
        yield return Fail<short>((short)0, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.", label: "short: g.IfZero(m)");
        yield return Fail<double>(-0.5, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.", label: "double: g.IfNegative(m)");
        yield return Fail<double>(0.0, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.", label: "double: g.IfNegativeOrZero(m)");
        yield return Fail<double>(0.5, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.", label: "double: g.IfPositive(m)");
        yield return Fail<double>(0.0, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.", label: "double: g.IfPositiveOrZero(m)");
        yield return Fail<double>(0.0, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.", label: "double: g.IfZero(m)");
        yield return Fail<float>(-0.5f, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.", label: "float: g.IfNegative(m)");
        yield return Fail<float>(0f, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.", label: "float: g.IfNegativeOrZero(m)");
        yield return Fail<float>(0.5f, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.", label: "float: g.IfPositive(m)");
        yield return Fail<float>(0f, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.", label: "float: g.IfPositiveOrZero(m)");
        yield return Fail<float>(0f, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.", label: "float: g.IfZero(m)");
        yield return Fail<decimal>(-0.5m, (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.", label: "decimal: g.IfNegative(m)");
        yield return Fail<decimal>(0m, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.", label: "decimal: g.IfNegativeOrZero(m)");
        yield return Fail<decimal>(0.5m, (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.", label: "decimal: g.IfPositive(m)");
        yield return Fail<decimal>(0m, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.", label: "decimal: g.IfPositiveOrZero(m)");
        yield return Fail<decimal>(0m, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.", label: "decimal: g.IfZero(m)");
        yield return Fail<TimeSpan>(TimeSpan.FromTicks(-1), (g, m) => g.IfNegative(m), ErrorCodes.Negative, Expect.OutOfRange, "'input' must not be negative.", label: "TimeSpan: g.IfNegative(m)");
        yield return Fail<TimeSpan>(TimeSpan.Zero, (g, m) => g.IfNegativeOrZero(m), ErrorCodes.NegativeOrZero, Expect.OutOfRange, "'input' must be greater than zero.", label: "TimeSpan: g.IfNegativeOrZero(m)");
        yield return Fail<TimeSpan>(TimeSpan.FromTicks(1), (g, m) => g.IfPositive(m), ErrorCodes.Positive, Expect.OutOfRange, "'input' must not be greater than zero.", label: "TimeSpan: g.IfPositive(m)");
        yield return Fail<TimeSpan>(TimeSpan.Zero, (g, m) => g.IfPositiveOrZero(m), ErrorCodes.PositiveOrZero, Expect.OutOfRange, "'input' must be less than zero.", label: "TimeSpan: g.IfPositiveOrZero(m)");
        yield return Fail<TimeSpan>(TimeSpan.Zero, (g, m) => g.IfZero(m), ErrorCodes.Zero, Expect.OutOfRange, "'input' must not be zero.", label: "TimeSpan: g.IfZero(m)");
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass<int>(5, (g, m) => g.IfGreaterThan(5, m), label: "int: g.IfGreaterThan(5, m)");
        yield return Pass<int>(1, (g, m) => g.IfGreaterThanOrEqualTo(5, m), label: "int: g.IfGreaterThanOrEqualTo(5, m)");
        yield return Pass<int>(5, (g, m) => g.IfLessThan(5, m), label: "int: g.IfLessThan(5, m)");
        yield return Pass<int>(10, (g, m) => g.IfLessThanOrEqualTo(5, m), label: "int: g.IfLessThanOrEqualTo(5, m)");
        yield return Pass<int>(5, (g, m) => g.IfOutOfRange(1, 5, m), label: "int: g.IfOutOfRange(1, 5, m)");
        yield return Pass<int>(10, (g, m) => g.IfInRange(1, 5, m), label: "int: g.IfInRange(1, 5, m)");
        yield return Pass<int>(5, (g, m) => g.IfOutOfRangeExclusive(1, 10, m), label: "int: g.IfOutOfRangeExclusive(1, 10, m)");
        yield return Pass<long>(5L, (g, m) => g.IfGreaterThan(5L, m), label: "long: g.IfGreaterThan(5L, m)");
        yield return Pass<long>(1L, (g, m) => g.IfGreaterThanOrEqualTo(5L, m), label: "long: g.IfGreaterThanOrEqualTo(5L, m)");
        yield return Pass<long>(5L, (g, m) => g.IfLessThan(5L, m), label: "long: g.IfLessThan(5L, m)");
        yield return Pass<long>(10L, (g, m) => g.IfLessThanOrEqualTo(5L, m), label: "long: g.IfLessThanOrEqualTo(5L, m)");
        yield return Pass<long>(5L, (g, m) => g.IfOutOfRange(1L, 5L, m), label: "long: g.IfOutOfRange(1L, 5L, m)");
        yield return Pass<long>(10L, (g, m) => g.IfInRange(1L, 5L, m), label: "long: g.IfInRange(1L, 5L, m)");
        yield return Pass<long>(5L, (g, m) => g.IfOutOfRangeExclusive(1L, 10L, m), label: "long: g.IfOutOfRangeExclusive(1L, 10L, m)");
        yield return Pass<short>((short)5, (g, m) => g.IfGreaterThan((short)5, m), label: "short: g.IfGreaterThan((short)5, m)");
        yield return Pass<short>((short)1, (g, m) => g.IfGreaterThanOrEqualTo((short)5, m), label: "short: g.IfGreaterThanOrEqualTo((short)5, m)");
        yield return Pass<short>((short)5, (g, m) => g.IfLessThan((short)5, m), label: "short: g.IfLessThan((short)5, m)");
        yield return Pass<short>((short)10, (g, m) => g.IfLessThanOrEqualTo((short)5, m), label: "short: g.IfLessThanOrEqualTo((short)5, m)");
        yield return Pass<short>((short)5, (g, m) => g.IfOutOfRange((short)1, (short)5, m), label: "short: g.IfOutOfRange((short)1, (short)5, m)");
        yield return Pass<short>((short)10, (g, m) => g.IfInRange((short)1, (short)5, m), label: "short: g.IfInRange((short)1, (short)5, m)");
        yield return Pass<short>((short)5, (g, m) => g.IfOutOfRangeExclusive((short)1, (short)10, m), label: "short: g.IfOutOfRangeExclusive((short)1, (short)10, m)");
        yield return Pass<double>(5.5, (g, m) => g.IfGreaterThan(5.5, m), label: "double: g.IfGreaterThan(5.5, m)");
        yield return Pass<double>(1.5, (g, m) => g.IfGreaterThanOrEqualTo(5.5, m), label: "double: g.IfGreaterThanOrEqualTo(5.5, m)");
        yield return Pass<double>(5.5, (g, m) => g.IfLessThan(5.5, m), label: "double: g.IfLessThan(5.5, m)");
        yield return Pass<double>(10.5, (g, m) => g.IfLessThanOrEqualTo(5.5, m), label: "double: g.IfLessThanOrEqualTo(5.5, m)");
        yield return Pass<double>(5.5, (g, m) => g.IfOutOfRange(1.5, 5.5, m), label: "double: g.IfOutOfRange(1.5, 5.5, m)");
        yield return Pass<double>(10.5, (g, m) => g.IfInRange(1.5, 5.5, m), label: "double: g.IfInRange(1.5, 5.5, m)");
        yield return Pass<double>(5.5, (g, m) => g.IfOutOfRangeExclusive(1.5, 10.5, m), label: "double: g.IfOutOfRangeExclusive(1.5, 10.5, m)");
        yield return Pass<float>(5.5f, (g, m) => g.IfGreaterThan(5.5f, m), label: "float: g.IfGreaterThan(5.5f, m)");
        yield return Pass<float>(1.5f, (g, m) => g.IfGreaterThanOrEqualTo(5.5f, m), label: "float: g.IfGreaterThanOrEqualTo(5.5f, m)");
        yield return Pass<float>(5.5f, (g, m) => g.IfLessThan(5.5f, m), label: "float: g.IfLessThan(5.5f, m)");
        yield return Pass<float>(10.5f, (g, m) => g.IfLessThanOrEqualTo(5.5f, m), label: "float: g.IfLessThanOrEqualTo(5.5f, m)");
        yield return Pass<float>(5.5f, (g, m) => g.IfOutOfRange(1.5f, 5.5f, m), label: "float: g.IfOutOfRange(1.5f, 5.5f, m)");
        yield return Pass<float>(10.5f, (g, m) => g.IfInRange(1.5f, 5.5f, m), label: "float: g.IfInRange(1.5f, 5.5f, m)");
        yield return Pass<float>(5.5f, (g, m) => g.IfOutOfRangeExclusive(1.5f, 10.5f, m), label: "float: g.IfOutOfRangeExclusive(1.5f, 10.5f, m)");
        yield return Pass<decimal>(5.5m, (g, m) => g.IfGreaterThan(5.5m, m), label: "decimal: g.IfGreaterThan(5.5m, m)");
        yield return Pass<decimal>(1.5m, (g, m) => g.IfGreaterThanOrEqualTo(5.5m, m), label: "decimal: g.IfGreaterThanOrEqualTo(5.5m, m)");
        yield return Pass<decimal>(5.5m, (g, m) => g.IfLessThan(5.5m, m), label: "decimal: g.IfLessThan(5.5m, m)");
        yield return Pass<decimal>(10.5m, (g, m) => g.IfLessThanOrEqualTo(5.5m, m), label: "decimal: g.IfLessThanOrEqualTo(5.5m, m)");
        yield return Pass<decimal>(5.5m, (g, m) => g.IfOutOfRange(1.5m, 5.5m, m), label: "decimal: g.IfOutOfRange(1.5m, 5.5m, m)");
        yield return Pass<decimal>(10.5m, (g, m) => g.IfInRange(1.5m, 5.5m, m), label: "decimal: g.IfInRange(1.5m, 5.5m, m)");
        yield return Pass<decimal>(5.5m, (g, m) => g.IfOutOfRangeExclusive(1.5m, 10.5m, m), label: "decimal: g.IfOutOfRangeExclusive(1.5m, 10.5m, m)");
        yield return Pass<DateTime>(Jun, (g, m) => g.IfGreaterThan(Jun, m), label: "DateTime: g.IfGreaterThan(Jun, m)");
        yield return Pass<DateTime>(Jan, (g, m) => g.IfGreaterThanOrEqualTo(Jun, m), label: "DateTime: g.IfGreaterThanOrEqualTo(Jun, m)");
        yield return Pass<DateTime>(Jun, (g, m) => g.IfLessThan(Jun, m), label: "DateTime: g.IfLessThan(Jun, m)");
        yield return Pass<DateTime>(Dec, (g, m) => g.IfLessThanOrEqualTo(Jun, m), label: "DateTime: g.IfLessThanOrEqualTo(Jun, m)");
        yield return Pass<DateTime>(Jun, (g, m) => g.IfOutOfRange(Jan, Jun, m), label: "DateTime: g.IfOutOfRange(Jan, Jun, m)");
        yield return Pass<DateTime>(Dec, (g, m) => g.IfInRange(Jan, Jun, m), label: "DateTime: g.IfInRange(Jan, Jun, m)");
        yield return Pass<DateTime>(Jun, (g, m) => g.IfOutOfRangeExclusive(Jan, Dec, m), label: "DateTime: g.IfOutOfRangeExclusive(Jan, Dec, m)");
        yield return Pass<string>("m", (g, m) => g.IfGreaterThan("m", m), label: "string: g.IfGreaterThan(\"m\", m)");
        yield return Pass<string>("b", (g, m) => g.IfGreaterThanOrEqualTo("m", m), label: "string: g.IfGreaterThanOrEqualTo(\"m\", m)");
        yield return Pass<string>("m", (g, m) => g.IfLessThan("m", m), label: "string: g.IfLessThan(\"m\", m)");
        yield return Pass<string>("y", (g, m) => g.IfLessThanOrEqualTo("m", m), label: "string: g.IfLessThanOrEqualTo(\"m\", m)");
        yield return Pass<string>("m", (g, m) => g.IfOutOfRange("b", "m", m), label: "string: g.IfOutOfRange(\"b\", \"m\", m)");
        yield return Pass<string>("y", (g, m) => g.IfInRange("b", "m", m), label: "string: g.IfInRange(\"b\", \"m\", m)");
        yield return Pass<string>("m", (g, m) => g.IfOutOfRangeExclusive("b", "y", m), label: "string: g.IfOutOfRangeExclusive(\"b\", \"y\", m)");
        yield return Pass<TimeSpan>(TimeSpan.FromMinutes(5), (g, m) => g.IfGreaterThan(TimeSpan.FromMinutes(5), m), label: "TimeSpan: g.IfGreaterThan(TimeSpan.FromMinutes(5), m)");
        yield return Pass<TimeSpan>(TimeSpan.FromMinutes(1), (g, m) => g.IfGreaterThanOrEqualTo(TimeSpan.FromMinutes(5), m), label: "TimeSpan: g.IfGreaterThanOrEqualTo(TimeSpan.FromMinutes(5), m)");
        yield return Pass<TimeSpan>(TimeSpan.FromMinutes(5), (g, m) => g.IfLessThan(TimeSpan.FromMinutes(5), m), label: "TimeSpan: g.IfLessThan(TimeSpan.FromMinutes(5), m)");
        yield return Pass<TimeSpan>(TimeSpan.FromMinutes(10), (g, m) => g.IfLessThanOrEqualTo(TimeSpan.FromMinutes(5), m), label: "TimeSpan: g.IfLessThanOrEqualTo(TimeSpan.FromMinutes(5), m)");
        yield return Pass<TimeSpan>(TimeSpan.FromMinutes(5), (g, m) => g.IfOutOfRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m), label: "TimeSpan: g.IfOutOfRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m)");
        yield return Pass<TimeSpan>(TimeSpan.FromMinutes(10), (g, m) => g.IfInRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m), label: "TimeSpan: g.IfInRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), m)");
        yield return Pass<TimeSpan>(TimeSpan.FromMinutes(5), (g, m) => g.IfOutOfRangeExclusive(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), m), label: "TimeSpan: g.IfOutOfRangeExclusive(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), m)");
        yield return Pass<Version>(new Version(5, 0), (g, m) => g.IfGreaterThan(new Version(5, 0), m), label: "Version: g.IfGreaterThan(new Version(5, 0), m)");
        yield return Pass<Version>(new Version(1, 0), (g, m) => g.IfGreaterThanOrEqualTo(new Version(5, 0), m), label: "Version: g.IfGreaterThanOrEqualTo(new Version(5, 0), m)");
        yield return Pass<Version>(new Version(5, 0), (g, m) => g.IfLessThan(new Version(5, 0), m), label: "Version: g.IfLessThan(new Version(5, 0), m)");
        yield return Pass<Version>(new Version(10, 0), (g, m) => g.IfLessThanOrEqualTo(new Version(5, 0), m), label: "Version: g.IfLessThanOrEqualTo(new Version(5, 0), m)");
        yield return Pass<Version>(new Version(5, 0), (g, m) => g.IfOutOfRange(new Version(1, 0), new Version(5, 0), m), label: "Version: g.IfOutOfRange(new Version(1, 0), new Version(5, 0), m)");
        yield return Pass<Version>(new Version(10, 0), (g, m) => g.IfInRange(new Version(1, 0), new Version(5, 0), m), label: "Version: g.IfInRange(new Version(1, 0), new Version(5, 0), m)");
        yield return Pass<Version>(new Version(5, 0), (g, m) => g.IfOutOfRangeExclusive(new Version(1, 0), new Version(10, 0), m), label: "Version: g.IfOutOfRangeExclusive(new Version(1, 0), new Version(10, 0), m)");
        yield return Pass<int>(0, (g, m) => g.IfNegative(m), label: "int: g.IfNegative(m)");
        yield return Pass<int>(1, (g, m) => g.IfNegativeOrZero(m), label: "int: g.IfNegativeOrZero(m)");
        yield return Pass<int>(0, (g, m) => g.IfPositive(m), label: "int: g.IfPositive(m)");
        yield return Pass<int>(-1, (g, m) => g.IfPositiveOrZero(m), label: "int: g.IfPositiveOrZero(m)");
        yield return Pass<int>(1, (g, m) => g.IfZero(m), label: "int: g.IfZero(m)");
        yield return Pass<long>(0L, (g, m) => g.IfNegative(m), label: "long: g.IfNegative(m)");
        yield return Pass<long>(1L, (g, m) => g.IfNegativeOrZero(m), label: "long: g.IfNegativeOrZero(m)");
        yield return Pass<long>(0L, (g, m) => g.IfPositive(m), label: "long: g.IfPositive(m)");
        yield return Pass<long>(-1L, (g, m) => g.IfPositiveOrZero(m), label: "long: g.IfPositiveOrZero(m)");
        yield return Pass<long>(1L, (g, m) => g.IfZero(m), label: "long: g.IfZero(m)");
        yield return Pass<short>((short)0, (g, m) => g.IfNegative(m), label: "short: g.IfNegative(m)");
        yield return Pass<short>((short)1, (g, m) => g.IfNegativeOrZero(m), label: "short: g.IfNegativeOrZero(m)");
        yield return Pass<short>((short)0, (g, m) => g.IfPositive(m), label: "short: g.IfPositive(m)");
        yield return Pass<short>((short)-1, (g, m) => g.IfPositiveOrZero(m), label: "short: g.IfPositiveOrZero(m)");
        yield return Pass<short>((short)1, (g, m) => g.IfZero(m), label: "short: g.IfZero(m)");
        yield return Pass<double>(0.0, (g, m) => g.IfNegative(m), label: "double: g.IfNegative(m)");
        yield return Pass<double>(0.5, (g, m) => g.IfNegativeOrZero(m), label: "double: g.IfNegativeOrZero(m)");
        yield return Pass<double>(0.0, (g, m) => g.IfPositive(m), label: "double: g.IfPositive(m)");
        yield return Pass<double>(-0.5, (g, m) => g.IfPositiveOrZero(m), label: "double: g.IfPositiveOrZero(m)");
        yield return Pass<double>(0.5, (g, m) => g.IfZero(m), label: "double: g.IfZero(m)");
        yield return Pass<float>(0f, (g, m) => g.IfNegative(m), label: "float: g.IfNegative(m)");
        yield return Pass<float>(0.5f, (g, m) => g.IfNegativeOrZero(m), label: "float: g.IfNegativeOrZero(m)");
        yield return Pass<float>(0f, (g, m) => g.IfPositive(m), label: "float: g.IfPositive(m)");
        yield return Pass<float>(-0.5f, (g, m) => g.IfPositiveOrZero(m), label: "float: g.IfPositiveOrZero(m)");
        yield return Pass<float>(0.5f, (g, m) => g.IfZero(m), label: "float: g.IfZero(m)");
        yield return Pass<decimal>(0m, (g, m) => g.IfNegative(m), label: "decimal: g.IfNegative(m)");
        yield return Pass<decimal>(0.5m, (g, m) => g.IfNegativeOrZero(m), label: "decimal: g.IfNegativeOrZero(m)");
        yield return Pass<decimal>(0m, (g, m) => g.IfPositive(m), label: "decimal: g.IfPositive(m)");
        yield return Pass<decimal>(-0.5m, (g, m) => g.IfPositiveOrZero(m), label: "decimal: g.IfPositiveOrZero(m)");
        yield return Pass<decimal>(0.5m, (g, m) => g.IfZero(m), label: "decimal: g.IfZero(m)");
        yield return Pass<TimeSpan>(TimeSpan.Zero, (g, m) => g.IfNegative(m), label: "TimeSpan: g.IfNegative(m)");
        yield return Pass<TimeSpan>(TimeSpan.FromTicks(1), (g, m) => g.IfNegativeOrZero(m), label: "TimeSpan: g.IfNegativeOrZero(m)");
        yield return Pass<TimeSpan>(TimeSpan.Zero, (g, m) => g.IfPositive(m), label: "TimeSpan: g.IfPositive(m)");
        yield return Pass<TimeSpan>(TimeSpan.FromTicks(-1), (g, m) => g.IfPositiveOrZero(m), label: "TimeSpan: g.IfPositiveOrZero(m)");
        yield return Pass<TimeSpan>(TimeSpan.FromTicks(1), (g, m) => g.IfZero(m), label: "TimeSpan: g.IfZero(m)");
    }

    public static TheoryData<CheckCase> Failures => Data(FailureCases());

    public static TheoryData<CheckCase> Passes => Data(PassCases());

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_throws_the_default_exception(CheckCase check) => check.AssertThrows();

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_uses_a_custom_message(CheckCase check) => check.AssertCustomMessage();

    [Theory]
    [MemberData(nameof(Passes))]
    public void Passing_check_returns_the_value(CheckCase check) => check.AssertPasses();
}
