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
#if !NET9_0_OR_GREATER
    private static readonly int[] OneTwoThree = [1, 2, 3];
    private static readonly int[] SixtyFourAnd128 = [64, 128];
    private static readonly char[] AngleBrackets = ['<', '>'];
    private static readonly char[] Hello = ['h', 'e', 'l', 'o'];
#endif

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

    // One row per chain, so an allocation seen only on some machine names the exact check.
    private static readonly Dictionary<string, Func<int>> StringChains = new()
    {
        ["slug, trimmed, white space, control, ASCII"] = static () => "hello-world-2".Throw().IfNotSlug().IfNotTrimmed().IfContainsWhiteSpace().IfContainsControlCharacters().IfNotAscii().AllowNull().Value.Length,
        ["IfContainsAnyExcept with 37 characters"] = static () => "hello-world-2".Throw().IfContainsAnyExcept("abcdefghijklmnopqrstuvwxyz-0123456789").Value.Length,
        ["digits, hex, letters or digits, IfEmpty"] = static () => "0123456789".Throw().IfNotDigits().IfNotHex().IfNotLettersOrDigits(message: null).IfEmpty().Value.Length,
        ["credit card number"] = static () => "4111 1111 1111 1111".Throw().IfNotCreditCardNumber().Value.Length,
        ["Base64"] = static () => "SGVsbG8gV29ybGQ=".Throw().IfNotBase64().Value.Length,
        ["GUID"] = static () => "3fa85f64-5717-4562-b3fc-2c963f66afa6".Throw().IfNotGuid().Value.Length,
        ["IPv4 address"] = static () => "192.168.0.1".Throw().IfNotIPAddress().Value.Length,
        ["IPv6 address"] = static () => "2001:db8::1".Throw().IfNotIPAddress().Value.Length,
        ["version and ordinal comparison"] = static () => "1.2.3.4".Throw().IfNotVersion().IfGreaterThan("9").Value.Length,
        ["enum names"] = static () => "Monday".Throw().IfNotEnumName<DayOfWeek>().IfNotEnumName<DayOfWeek>(ignoreCase: true).Value.Length,
        ["file name and string equality"] = static () => "report.pdf".Throw().IfNotFileName().IfEqualTo("x").IfNotEqualTo("report.pdf").Value.Length,

        // Every check of the combined rows above, measured alone.
        ["IfNotSlug alone"] = static () => "hello-world-2".Throw().IfNotSlug().Value.Length,
        ["IfNotTrimmed alone"] = static () => "hello-world-2".Throw().IfNotTrimmed().Value.Length,
        ["IfContainsWhiteSpace alone"] = static () => "hello-world-2".Throw().IfContainsWhiteSpace().Value.Length,
        ["IfContainsControlCharacters alone"] = static () => "hello-world-2".Throw().IfContainsControlCharacters().Value.Length,
        ["IfNotAscii alone"] = static () => "hello-world-2".Throw().IfNotAscii().Value.Length,
        ["IfNotDigits alone"] = static () => "0123456789".Throw().IfNotDigits().Value.Length,
        ["IfNotHex alone"] = static () => "0123456789".Throw().IfNotHex().Value.Length,
        ["IfNotLettersOrDigits alone"] = static () => "0123456789".Throw().IfNotLettersOrDigits(message: null).Value.Length,
        ["IfEmpty (generic) alone"] = static () => "0123456789".Throw().IfEmpty().Value.Length,
        ["IfNotVersion alone"] = static () => "1.2.3.4".Throw().IfNotVersion().Value.Length,
        ["IfGreaterThan (ordinal string) alone"] = static () => "1.2.3.4".Throw().IfGreaterThan("9").Value.Length,
        ["IfNotEnumName alone"] = static () => "Monday".Throw().IfNotEnumName<DayOfWeek>().Value.Length,
        ["IfNotEnumName ignoring case alone"] = static () => "Monday".Throw().IfNotEnumName<DayOfWeek>(ignoreCase: true).Value.Length,
        ["IfNotFileName alone"] = static () => "report.pdf".Throw().IfNotFileName().Value.Length,
        ["IfEqualTo (string) alone"] = static () => "report.pdf".Throw().IfEqualTo("x").Value.Length,
        ["IfNotEqualTo (string) alone"] = static () => "report.pdf".Throw().IfNotEqualTo("report.pdf").Value.Length,
    };

    public static TheoryData<string> StringChainNames => new(StringChains.Keys);

    [Theory]
    [MemberData(nameof(StringChainNames))]
    public void String_checks_do_not_allocate(string chain) => AssertNoAllocations(StringChains[chain]);

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
        // Real runtimes (eng/test-matrix.sh) once showed 64 B per call here on .NET 8: the
        // caller's not yet optimized code (Tier0, or the instrumented tier of dynamic PGO)
        // allocates a RuntimeFieldInfoStub for the compiler's RuntimeHelpers.CreateSpan of the
        // constant params values. Allocations.Measure now waits for optimized code.
        AssertNoAllocations(static () =>
        {
#if NET9_0_OR_GREATER
            int a = 64.Throw().IfOneOf(1, 2, 3).IfNotOneOf(64, 128);
            string b = "hello".Throw().IfContainsAny('<', '>').IfContainsAnyExcept('h', 'e', 'l', 'o');
#else
            // On .NET 8 the waiting is not always enough on loaded CI runners, so the constant
            // values come from static arrays (the RuntimeFieldInfoStub is the caller's cost).
            int a = 64.Throw().IfOneOf(OneTwoThree.AsSpan()).IfNotOneOf(SixtyFourAnd128.AsSpan());
            string b = "hello".Throw().IfContainsAny(AngleBrackets.AsSpan()).IfContainsAnyExcept(Hello.AsSpan());
#endif
            return a + b.Length;
        });
    }

    [Fact]
    public void Allocation_diagnostic_names_what_was_allocated()
    {
        // Proves that the diagnostic of a failed allocation test works on this runtime.
        var text = Allocations.Explain(static () => Sink = new byte[64]);

        Assert.SkipWhen(text.StartsWith("No GCAllocationTick", StringComparison.Ordinal), text);
        Assert.Contains("Byte[]", text, StringComparison.Ordinal);
        Assert.Contains("this thread", text, StringComparison.Ordinal);
    }

    private static object? Sink { get; set; }

    private static void AssertNoAllocations(Func<int> chain)
    {
        Allocations.SkipUnlessOptimized();

        // Warms up first (static caches such as SearchValues, enum data and templates, and the
        // tiered JIT); a one-time allocation does not count, a per-call allocation does.
        var sum = 0;
        Allocations.AssertAtMost(() => sum += chain(), Iterations);

        Assert.True(sum != 0);
    }
}
#endif
