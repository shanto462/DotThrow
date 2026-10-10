#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Performance;

/// <summary>
/// The happy path of the new checks must not allocate. Valid for an optimized (Release) build
/// of the library on .NET 8 and later; other builds skip.
/// </summary>
public sealed class NewCheckAllocationTests
{
    private const int Iterations = 2000;

    private static readonly List<int> Numbers = [1, 2, 3];
    private static readonly HashSet<int> Set = [1, 2, 3];
    private static readonly int[] Array = [1, 2, 3];
    private static readonly Dictionary<string, int> Stock = new() { ["apple"] = 1 };
    private static readonly Uri Https = new("https://example.com/a");
    private static readonly IPAddress Address = IPAddress.Parse("8.8.8.8");
    private static readonly MemoryStream Stream = new([1]);
    private static readonly DateTime Monday = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Number_checks_do_not_allocate() => AssertNoAllocations(static () =>
    {
        int a = 64.Throw().IfOdd().IfNotMultipleOf(8).IfNotPowerOfTwo(message: null).IfOutOfRangeExclusive(0, 100);
        long b = 64L.Throw().IfOdd().IfNotMultipleOf(8L).IfNotPowerOfTwo();
        double c = 2.0.Throw().IfNotFinite().IfNotInteger().IfNaN().IfGreaterThan(10.0).IfNegative();
        decimal d = 12.34m.Throw().IfPrecisionScaleExceeded(5, 2).IfNotMultipleOf(0.01m).IfLessThan(0m);
        decimal e = 12m.Throw().IfNotInteger();
        return a + (int)b + (int)c + (int)d + (int)e;
    });

    [Fact]
    public void String_checks_do_not_allocate() => AssertNoAllocations(static () =>
    {
        string a = "hello-world-2".Throw().IfNotSlug().IfNotTrimmed().IfContainsWhiteSpace().IfContainsControlCharacters()
            .IfNotAscii().AllowNull().IfContainsAnyExcept("abcdefghijklmnopqrstuvwxyz-0123456789");
        string b = "0123456789".Throw().IfNotDigits().IfNotHex().IfNotLettersOrDigits(message: null).IfEmpty();
        string card = "4111 1111 1111 1111".Throw().IfNotCreditCardNumber();
        string c = "SGVsbG8gV29ybGQ=".Throw().IfNotBase64();
        string d = "3fa85f64-5717-4562-b3fc-2c963f66afa6".Throw().IfNotGuid();
        string e = "192.168.0.1".Throw().IfNotIPAddress();
        string f = "2001:db8::1".Throw().IfNotIPAddress();
        string g = "1.2.3.4".Throw().IfNotVersion().IfGreaterThan("9");
        string h = "Monday".Throw().IfNotEnumName<DayOfWeek>().IfNotEnumName<DayOfWeek>(ignoreCase: true);
        string i = "report.pdf".Throw().IfNotFileName().IfEqualTo("x").IfNotEqualTo("report.pdf");
        return a.Length + b.Length + card.Length + c.Length + d.Length + e.Length + f.Length + g.Length + h.Length + i.Length;
    });

    [Fact]
    public void Collection_checks_do_not_allocate() => AssertNoAllocations(static () =>
    {
        var a = Numbers.Throw().IfNullOrEmpty().IfCountGreaterThan(10).IfCountOutOfRange(1, 5).IfAny(static (int n) => n < 0).IfNone(static (int n) => n == 2).IfEmpty().IfContains(9).Value.Count;
        var b = Set.Throw().IfNullOrEmpty().IfCountGreaterThan(10).IfCountLessThan(1).IfContains(9).Value.Count;
        var c = Array.Throw().IfNullOrEmpty().IfCountNotEqualTo(3).IfAny(static (int n) => n > 5).IfContainsNull().Value.Length;
        c += Numbers.Throw().IfContainsNull().Value.Count;
        var d = Stock.Throw().IfNotContainsKey("apple").IfContainsKey("pear").IfCountGreaterThan(5).Value.Count;
        return a + b + c + d;
    });

    [Fact]
    public void Other_checks_do_not_allocate() => AssertNoAllocations(static () =>
    {
        var a = Https.Throw().IfRelative().IfNotHttps().IfNotHttpOrHttps().IfLoopback().IfNotScheme("https", "wss").Value.Port;
        var b = Address.Throw().IfLoopback().IfPrivate().IfNotIPv4().Value.AddressFamily;
        var c = Stream.Throw().IfNotReadable().IfNotSeekable().IfEmpty().Value.Length;
        var d = typeof(List<int>).Throw().IfAbstract().IfInterface().IfNotAssignableTo(typeof(IEnumerable<int>)).Value.IsClass;
        var e = Monday.Throw().IfNotUtc().IfWeekend().IfHasTimeOfDay().IfBefore(DateTime.MinValue).IfNewerThan(TimeSpan.FromDays(1)).Value.Day;
        var f = DayOfWeek.Monday.Throw().IfNotDefined().IfHasFlag(DayOfWeek.Tuesday).IfNotHasFlag(DayOfWeek.Monday);
        var g = new DateTimeOffset(Monday).Throw().IfNotUtc().IfWeekend().IfOlderThan(TimeSpan.FromDays(36500)).Value.Day;
        return a + (int)b + (int)c + (d ? 1 : 0) + e + (int)(DayOfWeek)f + g;
    });

    [Fact]
    public void Params_span_overloads_do_not_allocate()
    {
        // Found with real runtimes (eng/test-matrix.sh): on the .NET 8 runtime these two calls
        // allocate 64 bytes per call in steady state with the default settings. The optimized
        // code is allocation-free (no allocation with DOTNET_TieredCompilation=0), and the
        // allocation goes away with DOTNET_TieredPGO=0, so .NET 8's dynamic PGO causes it.
        // .NET 9 and .NET 10 do not allocate.
        Assert.SkipWhen(
            Environment.Version.Major == 8,
            "Known .NET 8 issue: IfOneOf(params ReadOnlySpan<T>) and IfContainsAny(params ReadOnlySpan<char>) allocate 64 B per call under dynamic PGO.");

        AssertNoAllocations(static () =>
        {
            int a = 64.Throw().IfOneOf(1, 2, 3).IfNotOneOf(64, 128);
            string b = "hello".Throw().IfContainsAny('<', '>').IfContainsAnyExcept('h', 'e', 'l', 'o');
            return a + b.Length;
        });
    }

    private static void AssertNoAllocations(Func<int> chain)
    {
        Allocations.SkipUnlessOptimized();

        // Warms up first (static caches such as SearchValues, enum data and templates, and the
        // tiered JIT); a one-time allocation does not count, a per-call allocation does.
        var sum = 0;
        var allocated = Allocations.Measure(() => sum += chain(), Iterations);

        Assert.True(sum != 0);
        Assert.Equal(0, allocated);
    }
}
#endif
