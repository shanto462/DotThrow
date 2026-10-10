using System;
using System.Linq;
using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace DotThrow.Benchmarks;

/// <summary>
/// A few checks next to the .NET code you would write instead. Every input is valid, so
/// nothing throws. The .NET code is the baseline of each category.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CheckBenchmarks
{
    private const string IPv4 = "IPv4 text";
    private const string IPv6 = "IPv6 text";
    private const string Defined = "Enum value is defined";
    private const string OneOf = "Int is one of 1, 2, 3";

    private string[] _ipv4 = [];
    private string[] _ipv6 = [];
    private DayOfWeek[] _days = [];
    private int[] _codes = [];

    [GlobalSetup]
    public void Setup()
    {
        _ipv4 = TestData.IPv4Texts();
        _ipv6 = TestData.IPv6Texts();
        _days = [.. Enumerable.Range(0, TestData.Count).Select(i => (DayOfWeek)(i % 7))];
        _codes = [.. Enumerable.Range(0, TestData.Count).Select(i => (i % 3) + 1)];
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = ".NET IPAddress.TryParse")]
    [BenchmarkCategory(IPv4)]
    public int IPv4BuiltIn() => CountParsed(_ipv4);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow IfNotIPAddress")]
    [BenchmarkCategory(IPv4)]
    public int IPv4DotThrow() => CountChecked(_ipv4);

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = ".NET IPAddress.TryParse")]
    [BenchmarkCategory(IPv6)]
    public int IPv6BuiltIn() => CountParsed(_ipv6);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow IfNotIPAddress")]
    [BenchmarkCategory(IPv6)]
    public int IPv6DotThrow() => CountChecked(_ipv6);

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = ".NET Enum.IsDefined")]
    [BenchmarkCategory(Defined)]
    public int DefinedBuiltIn()
    {
        var sum = 0;
        foreach (var day in _days)
        {
            if (!Enum.IsDefined(day))
            {
                throw new ArgumentOutOfRangeException(nameof(day));
            }

            sum += (int)day;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow IfNotDefined")]
    [BenchmarkCategory(Defined)]
    public int DefinedDotThrow()
    {
        var sum = 0;
        foreach (var day in _days)
        {
            sum += (int)day.Throw().IfNotDefined().Value;
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "Hand-written is 1 or 2 or 3")]
    [BenchmarkCategory(OneOf)]
    public int OneOfHandWritten()
    {
        var sum = 0;
        foreach (var code in _codes)
        {
            if (code is not (1 or 2 or 3))
            {
                throw new ArgumentOutOfRangeException(nameof(code));
            }

            sum += code;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow IfNotOneOf(1, 2, 3)")]
    [BenchmarkCategory(OneOf)]
    public int OneOfDotThrow()
    {
        var sum = 0;
        foreach (var code in _codes)
        {
            sum += code.Throw().IfNotOneOf(1, 2, 3);
        }

        return sum;
    }

    private static int CountParsed(string[] texts)
    {
        var count = 0;
        foreach (var text in texts)
        {
            if (!IPAddress.TryParse(text, out _))
            {
                throw new ArgumentException("Not an IP address.", nameof(texts));
            }

            count++;
        }

        return count;
    }

    private static int CountChecked(string[] texts)
    {
        var count = 0;
        foreach (var text in texts)
        {
            text.Throw().IfNotIPAddress();
            count++;
        }

        return count;
    }
}
