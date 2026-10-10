using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Throw;
using ArdalisGuard = Ardalis.GuardClauses.Guard;
using ToolkitGuard = CommunityToolkit.Diagnostics.Guard;

namespace DotThrow.Benchmarks;

/// <summary>
/// Guard clauses on the happy path: every value is valid, so nothing throws. Each category
/// runs the same checks on the same inputs with a hand-written <c>if</c> (the baseline), the
/// .NET built-in throw helpers, DotThrow, and three other guard libraries.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GuardBenchmarks
{
    private const string IntRange = "Int in 0..150";
    private const string StringCheck = "String not blank, max 50";
    private const string ListCheck = "List not null or empty, max 10";

    private int[] _ages = [];
    private string[] _names = [];
    private List<string>[] _tagLists = [];

    [GlobalSetup]
    public void Setup()
    {
        _ages = TestData.Ages();
        _names = TestData.Names();
        _tagLists = TestData.TagLists();
    }

    // ---- int in 0..150 -----------------------------------------------------------------

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "Hand-written if")]
    [BenchmarkCategory(IntRange)]
    public int IntHandWritten()
    {
        var sum = 0;
        foreach (var age in _ages)
        {
            if (age < 0 || age > 150)
            {
                throw new ArgumentOutOfRangeException(nameof(age), age, "Age must be between 0 and 150.");
            }

            sum += age;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = ".NET built-in")]
    [BenchmarkCategory(IntRange)]
    public int IntBuiltIn()
    {
        var sum = 0;
        foreach (var age in _ages)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(age);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(age, 150);
            sum += age;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow")]
    [BenchmarkCategory(IntRange)]
    public int IntDotThrow()
    {
        var sum = 0;
        foreach (var age in _ages)
        {
            sum += age.Throw().IfOutOfRange(0, 150);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "Ardalis.GuardClauses")]
    [BenchmarkCategory(IntRange)]
    public int IntArdalis()
    {
        var sum = 0;
        foreach (var age in _ages)
        {
            sum += ArdalisGuard.Against.OutOfRange(age, nameof(age), 0, 150);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "CommunityToolkit.Diagnostics")]
    [BenchmarkCategory(IntRange)]
    public int IntToolkit()
    {
        var sum = 0;
        foreach (var age in _ages)
        {
            ToolkitGuard.IsBetweenOrEqualTo(age, 0, 150);
            sum += age;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "Throw (amantinband)")]
    [BenchmarkCategory(IntRange)]
    public int IntThrowLibrary()
    {
        var sum = 0;
        foreach (var age in _ages)
        {
            // Both DotThrow and Throw add a Throw() extension method, and DotThrow's wins
            // inside this namespace. Calling Throw's static method directly is the same IL.
            sum += ValidatableCreationExtensions.Throw(age).IfOutOfRange(0, 150).Value;
        }

        return sum;
    }

    // ---- string not blank, max 50 ------------------------------------------------------

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "Hand-written if")]
    [BenchmarkCategory(StringCheck)]
    public int StringHandWritten()
    {
        var length = 0;
        foreach (var name in _names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name must not be blank.", nameof(name));
            }

            if (name.Length > 50)
            {
                throw new ArgumentException("Name must be at most 50 characters long.", nameof(name));
            }

            length += name.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = ".NET built-in")]
    [BenchmarkCategory(StringCheck)]
    public int StringBuiltIn()
    {
        var length = 0;
        foreach (var name in _names)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, 50);
            length += name.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow")]
    [BenchmarkCategory(StringCheck)]
    public int StringDotThrow()
    {
        var length = 0;
        foreach (var name in _names)
        {
            string checkedName = name.Throw().IfNullOrWhiteSpace().IfLongerThan(50);
            length += checkedName.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "Ardalis.GuardClauses")]
    [BenchmarkCategory(StringCheck)]
    public int StringArdalis()
    {
        var length = 0;
        foreach (var name in _names)
        {
            ArdalisGuard.Against.NullOrWhiteSpace(name);
            length += ArdalisGuard.Against.StringTooLong(name, 50).Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "CommunityToolkit.Diagnostics")]
    [BenchmarkCategory(StringCheck)]
    public int StringToolkit()
    {
        var length = 0;
        foreach (var name in _names)
        {
            ToolkitGuard.IsNotNullOrWhiteSpace(name);
            ToolkitGuard.HasSizeLessThanOrEqualTo(name, 50);
            length += name.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "Throw (amantinband)")]
    [BenchmarkCategory(StringCheck)]
    public int StringThrowLibrary()
    {
        var length = 0;
        foreach (var name in _names)
        {
            // Throw() rejects null, and IfWhiteSpace() rejects empty and white space.
            length += ValidatableCreationExtensions.Throw(name).IfWhiteSpace().IfLongerThan(50).Value.Length;
        }

        return length;
    }

    // ---- list not null or empty, max 10 ------------------------------------------------

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "Hand-written if")]
    [BenchmarkCategory(ListCheck)]
    public int ListHandWritten()
    {
        var items = 0;
        foreach (var tags in _tagLists)
        {
            if (tags is null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            if (tags.Count == 0 || tags.Count > 10)
            {
                throw new ArgumentException("Tags must have between 1 and 10 items.", nameof(tags));
            }

            items += tags.Count;
        }

        return items;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = ".NET built-in")]
    [BenchmarkCategory(ListCheck)]
    public int ListBuiltIn()
    {
        var items = 0;
        foreach (var tags in _tagLists)
        {
            ArgumentNullException.ThrowIfNull(tags);
            ArgumentOutOfRangeException.ThrowIfZero(tags.Count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, 10);
            items += tags.Count;
        }

        return items;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow")]
    [BenchmarkCategory(ListCheck)]
    public int ListDotThrow()
    {
        var items = 0;
        foreach (var tags in _tagLists)
        {
            List<string> checkedTags = tags.Throw().IfNullOrEmpty().IfCountGreaterThan(10);
            items += checkedTags.Count;
        }

        return items;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "Ardalis.GuardClauses")]
    [BenchmarkCategory(ListCheck)]
    public int ListArdalis()
    {
        var items = 0;
        foreach (var tags in _tagLists)
        {
            ArdalisGuard.Against.NullOrEmpty(tags);
            items += ArdalisGuard.Against.OutOfRange(tags.Count, nameof(tags), 1, 10);
        }

        return items;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "CommunityToolkit.Diagnostics")]
    [BenchmarkCategory(ListCheck)]
    public int ListToolkit()
    {
        var items = 0;
        foreach (var tags in _tagLists)
        {
            ToolkitGuard.IsNotNull(tags);
            ToolkitGuard.IsNotEmpty(tags);
            ToolkitGuard.HasSizeLessThanOrEqualTo(tags, 10);
            items += tags.Count;
        }

        return items;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "Throw (amantinband)")]
    [BenchmarkCategory(ListCheck)]
    public int ListThrowLibrary()
    {
        var items = 0;
        foreach (var tags in _tagLists)
        {
            items += tags.ThrowIfNull().IfEmpty().IfCountGreaterThan(10).Value.Count;
        }

        return items;
    }
}
