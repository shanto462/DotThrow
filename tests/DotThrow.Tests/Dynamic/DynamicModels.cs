// Test models: operators and type-named properties are not needed here.
#pragma warning disable CA2231, CA1036, CA1720
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using DotThrow.Dynamic;

namespace DotThrow.Tests.Dynamic;

// Models for the dynamic rule tests. Every value is a placeholder (Ada Lovelace, ada@example.com).
public enum Status
{
    Draft,
    Active,
    Closed,
}

[Flags]
public enum Access
{
    None = 0,
    Read = 1,
    Write = 2,
    Admin = 4,
}

// A comparable type DotThrow does not know.
public readonly struct Money : IComparable<Money>, IEquatable<Money>
{
    public Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Money Parse(string text) => new(decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public bool Equals(Money other) => Amount == other.Amount;

    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    public override int GetHashCode() => Amount.GetHashCode();

    public override string ToString() => Amount.ToString(CultureInfo.InvariantCulture);
}

public sealed class Sample
{
    public string? Text { get; set; } = "Ada Lovelace";

    public int Number { get; set; } = 10;

    public int? MaybeNumber { get; set; } = 10;

    public long Big { get; set; } = 10;

    public short Short { get; set; } = 10;

    public decimal Price { get; set; } = 10.5m;

    public double Ratio { get; set; } = 0.5;

    public float Single { get; set; } = 0.5f;

    public bool Flag { get; set; }

    public bool? MaybeFlag { get; set; }

    public DateTime When { get; set; } = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    public DateTimeOffset At { get; set; } = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

#if NET8_0_OR_GREATER
    public DateOnly Day { get; set; } = new(2026, 1, 5);

    public TimeOnly Time { get; set; } = new(9, 30);
#endif

    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

    public Guid Id { get; set; } = new("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    public Guid? MaybeId { get; set; }

    public Status Status { get; set; } = Status.Active;

    public Status? MaybeStatus { get; set; }

    public Access Access { get; set; } = Access.Read;

    public Uri? Link { get; set; } = new("https://example.com/a");

    public IPAddress? Ip { get; set; } = IPAddress.Parse("203.0.113.5");

    public Version? Version { get; set; } = new(1, 2);

    public char Letter { get; set; } = 'a';

    public List<string?> Tags { get; set; } = ["math"];

    public int[] Scores { get; set; } = [1, 2];

    public Dictionary<string, int> Counts { get; set; } = new() { ["a"] = 1 };

    public Money Cost { get; set; } = new(5);

    public Type? Kind { get; set; } = typeof(string);

    public Stream? Body { get; set; }

    public Address? Address { get; set; } = new() { City = "London", Zip = "12345" };

    public List<Line?>? Lines { get; set; } = [new Line { Sku = "SKU-1", Quantity = 1 }];
}

public sealed class Address
{
    public string? City { get; set; }

    public string? Zip { get; set; }

    public int Floor { get; set; }

    public GeoPoint? Point { get; set; }
}

public struct GeoPoint
{
    public double Latitude { get; set; }
}

public sealed class Line
{
    public string? Sku { get; set; }

    public int Quantity { get; set; }

    public Status Status { get; set; }

    public List<string?> Notes { get; set; } = [];
}

// Property shapes the reflection constructor must handle without crashing.
public sealed class Edge
{
#if NET8_0_OR_GREATER
    public System.Collections.Immutable.ImmutableArray<int> Fixed { get; set; } = [1, 2, 3];
#endif

    public Money? MaybeCost { get; set; }

    public string? Secret { private get; set; }

#if NET8_0_OR_GREATER
#pragma warning disable CA1822 // A ref struct property is the point of the test.
    public Span<int> Window => default;
#pragma warning restore CA1822
#endif

    public string? Shown => Secret is null ? null : "set";
}

public class BaseItem
{
    public object? Value { get; set; }
}

// Hides the base property with "new", and has two properties that differ only by case.
#pragma warning disable CA1708 // Names differ only by case: that is the point of the test.
public sealed class DerivedItem : BaseItem
{
    public new string? Value { get; set; }

    public string? Code { get; set; }

#pragma warning disable IDE1006 // Naming: the lower-case twin is the point of the test.
    public string? code { get; set; }
#pragma warning restore IDE1006
}
#pragma warning restore CA1708

public static class Maps
{
    // Every property of Sample, for the AOT-safe constructor.
    public static PropertyMap<Sample> Sample() => new PropertyMap<Sample>()
        .Add(x => x.Text)
        .Add(x => x.Number)
        .Add(x => x.MaybeNumber)
        .Add(x => x.Big)
        .Add(x => x.Short)
        .Add(x => x.Price)
        .Add(x => x.Ratio)
        .Add(x => x.Single)
        .Add(x => x.Flag)
        .Add(x => x.MaybeFlag)
        .Add(x => x.When)
        .Add(x => x.At)
#if NET8_0_OR_GREATER
        .Add(x => x.Day)
        .Add(x => x.Time)
#endif
        .Add(x => x.Duration)
        .Add(x => x.Id)
        .Add(x => x.MaybeId)
        .AddEnum(x => x.Status)
        .AddEnum(x => x.MaybeStatus)
        .AddEnum(x => x.Access)
        .Add(x => x.Link)
        .Add(x => x.Ip)
        .Add(x => x.Version)
        .Add(x => x.Letter)
        .AddCollection(x => x.Tags)
        .AddCollection(x => x.Scores)
        .Add(x => x.Counts)
        .AddComparable(x => x.Cost, Money.Parse)
        .Add(x => x.Kind)
        .Add(x => x.Body)
        .Add(x => x.Address)
        .Add(x => x.Address?.City)
        .Add(x => x.Address?.Zip)
        .Add(x => x.Address?.Floor)
        .Add(x => x.Address?.Point?.Latitude)
        .AddCollection(x => x.Lines, line => line.Add(l => l.Sku).Add(l => l.Quantity).AddEnum(l => l.Status).AddCollection(l => l.Notes))
        .AddType(typeof(MemoryStream));
}

public static class Rules
{
    // A definition with one rule and one check, the check given as JSON.
    public static RuleSetDefinition One(string property, string checkJson, string? ruleOptions = null) =>
        RuleSetDefinition.FromJson($$"""{ "rules": [ { "property": "{{property}}", {{(ruleOptions is null ? "" : ruleOptions + ",")}} "checks": [ {{checkJson}} ] } ] }""");

    public static string Show(ValidationReport report) =>
        string.Join(" | ", report.Failures.Select(f => $"{f.Name}: {f.Message} [{f.ErrorCode}, {f.Severity}, {f.AttemptedValue}]"));
}
