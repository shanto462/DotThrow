#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace DotThrow.Tests;

/// <summary>
/// The happy path must not allocate. This holds for an optimized (Release) build of the
/// library. A Debug build disables JIT optimizations, so generic code boxes value types for
/// null checks, and these tests skip. Run them with "dotnet test -c Release".
/// </summary>
public sealed class AllocationTests
{
    private const int Iterations = 10_000;

    private static readonly List<int> Numbers = [1, 2, 3];
    private static readonly int[] NumberArray = [1, 2, 3];
    private static readonly DateTime Moment = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Int_checks_do_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            var value = 42;
            int result = value.Throw().IfNegative().IfNegativeOrZero().IfZero().IfGreaterThan(100).IfLessThan(1)
                .IfOutOfRange(0, 100).IfInRange(50, 60).IfEqualTo(7).IfDefault();
            return result;
        });

    [Fact]
    public void Double_checks_do_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            var value = 1.5;
            double result = value.Throw().IfNaN().IfInfinity().IfNegative().IfZero().IfGreaterThan(2.0).IfOutOfRange(0.0, 2.0);
            return (int)result;
        });

    [Fact]
    public void Decimal_checks_do_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            var value = 19.99m;
            decimal result = value.Throw().IfNegative().IfGreaterThan(100m).IfLessThanOrEqualTo(0m);
            return (int)result;
        });

    [Fact]
    public void String_checks_do_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            var value = "Ada Lovelace";
            string result = value.Throw().IfNull().IfNullOrEmpty().IfNullOrWhiteSpace().IfLongerThan(50).IfShorterThan(2)
                .IfLengthOutOfRange(1, 100).IfStartsWith("Bob").IfEndsWith("Smith").IfContains("@").IfAllUpperCase().AllowNull();
            string email = "ada@example.com".Throw().IfNullOrWhiteSpace().IfNotEmailAddress().IfNotContains("@");
            return result.Length + email.Length;
        });

    [Fact]
    public void Json_check_does_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            var value = "{\"name\":\"Ada Lovelace\",\"tags\":[1,2.5,true,null,{\"a\":[]}]}";
            string result = value.Throw().IfNotJson();
            return result.Length;
        });

    [Fact]
    public void Date_checks_do_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            var value = Moment;
            DateTime result = value.Throw().IfBefore(DateTime.MinValue).IfAfter(DateTime.MaxValue).IfDefault()
                .IfGreaterThan(DateTime.MaxValue).IfInFuture();
            return result.Day;
        });

    [Fact]
    public void Collection_count_checks_on_a_list_do_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            List<int> result = Numbers.Throw().IfNull().IfNullOrEmpty().IfCountGreaterThan(10).IfCountLessThan(1)
                .IfCountOutOfRange(1, 5).IfCountEqualTo(9).IfContains(9);
            int[] array = NumberArray.Throw().IfNullOrEmpty().IfCountNotEqualTo(3);
            return result.Count + array.Length;
        });

    [Fact]
    public void Value_checks_do_not_allocate() =>
        AssertNoAllocations(static () =>
        {
            var id = new Guid("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
            int? maybe = 5;
            Guid result = id.Throw().IfEmpty().IfDefault();
            bool flag = true.Throw().IfFalse();
            int unwrapped = maybe.Throw().IfNull().IfNegative();
            return result.GetHashCode() + (flag ? 1 : 0) + unwrapped;
        });

    [Fact]
    public void Collect_mode_happy_path_does_not_allocate_beyond_the_report()
    {
        var report = new ValidationReport();

        AssertNoAllocations(() =>
        {
            int age = report.Check(36, "age").IfNegative().IfGreaterThan(150);
            string name = report.Check("Ada Lovelace", "name").IfNullOrWhiteSpace().IfLongerThan(50);
            return age + name.Length;
        });

        Assert.True(report.IsValid);
    }

    private static void AssertNoAllocations(Func<int> chain)
    {
        Assert.SkipWhen(
            IsJitOptimizerDisabled(typeof(Guard<>).Assembly),
            "The library is a Debug build. Run with -c Release to check allocations.");

        // Warm up so that every method is compiled (and promoted by tiered compilation).
        var sink = 0;
        for (var round = 0; round < 5; round++)
        {
            sink += Run(chain);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        sink += Run(chain);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated == 0, $"The happy path allocated {allocated} bytes over {Iterations} runs.");
        Assert.NotEqual(int.MinValue, sink);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(Func<int> chain)
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            sum += chain();
        }

        return sum;
    }

    private static bool IsJitOptimizerDisabled(Assembly assembly) =>
        assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false;
}
#endif
