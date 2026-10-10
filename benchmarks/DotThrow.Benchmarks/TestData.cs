using System;
using System.Collections.Generic;
using System.Linq;

namespace DotThrow.Benchmarks;

public sealed record SignUp(string Name, string Email, int Age, List<string> Tags);

public sealed record Customer(string Name, string Email);

public sealed record OrderLine(string Sku, int Quantity, decimal Price);

public sealed record Order(string Number, Customer? Customer, List<OrderLine> Lines, bool IsGift, string? GiftMessage);

/// <summary>
/// The inputs every benchmark uses. They are made once, outside the measured methods,
/// with a fixed seed, so every library checks exactly the same values.
/// </summary>
internal static class TestData
{
    /// <summary>
    /// One check takes about a nanosecond, which is too short to measure alone. So each
    /// benchmark checks this many inputs, and BenchmarkDotNet reports the time per input
    /// (<c>OperationsPerInvoke</c>). The loop costs the same in every variant.
    /// </summary>
    public const int Count = 1024;

    private static readonly string[] FirstNames = ["Ada", "Charles", "Mary", "Alan", "Grace", "Edsger"];

    public static int[] Ages()
    {
        var random = new Random(42);
        return [.. Enumerable.Range(0, Count).Select(_ => random.Next(0, 151))];
    }

    public static string[] Names() =>
        [.. Enumerable.Range(0, Count).Select(i => $"{FirstNames[i % FirstNames.Length]} Example {i}")];

    public static List<string>[] TagLists()
    {
        var random = new Random(42);
        return [.. Enumerable.Range(0, Count).Select(_ => Enumerable.Repeat("tag", random.Next(1, 11)).ToList())];
    }

    public static HashSet<int>[] IdSets()
    {
        var random = new Random(42);
        return [.. Enumerable.Range(0, Count).Select(_ => Enumerable.Range(1, random.Next(1, 11)).ToHashSet())];
    }

    /// <summary>Sign-ups that pass every rule.</summary>
    public static SignUp[] ValidSignUps()
    {
        var names = Names();
        var ages = Ages();
        var tags = TagLists();
        return [.. Enumerable.Range(0, Count).Select(i => new SignUp(names[i], $"user{i}@example.com", ages[i], tags[i]))];
    }

    /// <summary>Sign-ups that break one check on every property, so each validator reports 4 failures.</summary>
    public static SignUp[] InvalidSignUps()
    {
        var tooMany = Enumerable.Repeat("tag", 11).ToList();
        return [.. Enumerable.Range(0, Count).Select(i => new SignUp(" ", $"user{i}.example.com", 151 + (i % 50), tooMany))];
    }

    /// <summary>Orders with a customer, three lines and (for every second order) a gift message, all valid.</summary>
    public static Order[] ValidOrders()
    {
        var names = Names();
        return [.. Enumerable.Range(0, Count).Select(i => new Order(
            $"ORD-{i}",
            new Customer(names[i], $"user{i}@example.com"),
            [new OrderLine("SKU-1", 1, 9.99m), new OrderLine("SKU-2", 2, 19.99m), new OrderLine("SKU-3", 3, 0m)],
            IsGift: i % 2 == 0,
            GiftMessage: i % 2 == 0 ? "Happy birthday!" : null))];
    }

    /// <summary>Orders with 5 failures: number, customer email, two lines and the gift message.</summary>
    public static Order[] InvalidOrders()
    {
        var names = Names();
        return [.. Enumerable.Range(0, Count).Select(i => new Order(
            $"{i}",
            new Customer(names[i], $"user{i}.example.com"),
            [new OrderLine("SKU-1", 1, 9.99m), new OrderLine("SKU-2", 0, 19.99m), new OrderLine("SKU-3", 3, -1m)],
            IsGift: true,
            GiftMessage: ""))];
    }

    public static string[] IPv4Texts() =>
        [.. Enumerable.Range(0, Count).Select(i => $"192.168.{i % 256}.{(i * 7) % 256}")];

    public static string[] IPv6Texts() =>
        [.. Enumerable.Range(0, Count).Select(i => $"2001:db8:85a3::{i:x}:8a2e:370:7334")];
}
