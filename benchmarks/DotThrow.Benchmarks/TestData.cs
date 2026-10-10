using System;
using System.Collections.Generic;
using System.Linq;

namespace DotThrow.Benchmarks;

public sealed record SignUp(string Name, string Email, int Age, List<string> Tags);

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
}
