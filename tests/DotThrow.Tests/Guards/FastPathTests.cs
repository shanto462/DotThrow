using System;
using System.Linq;
using Xunit;
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
using System.Buffers;
#endif

namespace DotThrow.Tests.Guards;

// The fast paths of IfOneOf/IfNotOneOf (up to four values compared without a loop, longer
// lists vectorized for primitive types) and of IfContainsAny/IfContainsAnyExcept (long and
// non-ASCII character sets), checked against a simple reference on every list length.
public sealed class FastPathTests
{
    [Fact]
    public void Params_lists_of_every_length()
    {
        Assert.Throws<ArgumentException>(() => 5.Throw().IfOneOf(5));
        Assert.Throws<ArgumentException>(() => 5.Throw().IfOneOf(4, 5));
        Assert.Throws<ArgumentException>(() => 5.Throw().IfOneOf(3, 4, 5));
        Assert.Throws<ArgumentException>(() => 5.Throw().IfOneOf(2, 3, 4, 5));
        Assert.Throws<ArgumentException>(() => 5.Throw().IfOneOf(1, 2, 3, 4, 5));
        Assert.Throws<ArgumentException>(() => 5.Throw().IfOneOf(9, 8, 7, 6, 5, 4, 3, 2, 1));
        5.Throw().IfOneOf().IfOneOf(1).IfOneOf(1, 2).IfOneOf(1, 2, 3).IfOneOf(1, 2, 3, 4).IfOneOf(1, 2, 3, 4, 6, 7, 8);

        5.Throw().IfNotOneOf(5).IfNotOneOf(5, 6).IfNotOneOf(4, 5, 6).IfNotOneOf(1, 2, 3, 5).IfNotOneOf(1, 2, 3, 4, 5).IfNotOneOf(9, 8, 7, 6, 5);
        Assert.Throws<ArgumentException>(() => 5.Throw().IfNotOneOf());
        Assert.Throws<ArgumentException>(() => 5.Throw().IfNotOneOf(1, 2, 3, 4));
        Assert.Throws<ArgumentException>(() => 5.Throw().IfNotOneOf(1, 2, 3, 4, 6, 7));
        Assert.Throws<ArgumentException>(() => "ADA".Throw().IfNotOneOf("ada", "bob", "cy", "dan", "eve"));
    }

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
    [Fact]
    public void Span_lists_find_every_position_for_every_type_family()
    {
        AssertOneOf(new[] { 3, -7, int.MaxValue, int.MinValue, 0, 42, 9 }, 5);
        AssertOneOf(new uint[] { 3, uint.MaxValue, 0, 7, 2147483648u, 9, 10 }, 5u);
        AssertOneOf(new[] { 3L, long.MinValue, long.MaxValue, 0L, -1L, 9L, 10L }, 5L);
        AssertOneOf(new ulong[] { 3, ulong.MaxValue, 0, 7, 9223372036854775808ul, 9, 10 }, 5ul);
        AssertOneOf(new[] { 'a', '\uFFFF', '\u00E9', 'z', '\0', ' ', '\u65E5' }, 'b');
        AssertOneOf(new short[] { 3, short.MinValue, short.MaxValue, 0, -1, 9, 10 }, (short)5);
        AssertOneOf(new ushort[] { 3, ushort.MaxValue, 0, 7, 32768, 9, 10 }, (ushort)5);
        AssertOneOf(new byte[] { 3, byte.MaxValue, 0, 7, 128, 9, 10 }, (byte)5);
        AssertOneOf(new sbyte[] { 3, sbyte.MinValue, sbyte.MaxValue, 0, -1, 9, 10 }, (sbyte)5);
        AssertOneOf(new[] { 1.5, -0.25, double.MaxValue, double.NaN, 0.0, 9.5, 10.5 }, 5.5);
        AssertOneOf(new[] { 1.5m, -0.25m, decimal.MaxValue, 0m, 7m, 9.5m, 10.5m }, 5.5m);
        AssertOneOf(new[] { DayOfWeek.Monday, DayOfWeek.Friday, DayOfWeek.Sunday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Wednesday }, DayOfWeek.Saturday);
        AssertOneOf(new[] { "a", "Ada", null, "", "bob", "cy", "dan" }, "eve");
        AssertOneOf(new[] { Guid.Empty, new Guid("6f9619ff-8b86-d011-b42d-00cf4fc964ff"), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }, Guid.NewGuid());
    }

    [Fact]
    public void Span_lists_with_a_comparer_of_every_length()
    {
        string[] names = ["ada", "bob", "cy", "dan", "eve", "fay"];
        for (var length = 1; length <= names.Length; length++)
        {
            var report = new ValidationReport();
            report.Check(names[length - 1].ToUpperInvariant(), "name").IfOneOf(names.AsSpan(0, length), StringComparer.OrdinalIgnoreCase, message: null);
            report.Check("ZED", "other").IfOneOf(names.AsSpan(0, length), StringComparer.OrdinalIgnoreCase, message: null);
            report.Check("ZED", "missing").IfNotOneOf(names.AsSpan(0, length), StringComparer.OrdinalIgnoreCase, message: null);

            Assert.Equal(["name", "missing"], report.Failures.Select(f => f.Name));
        }
    }

    private static void AssertOneOf<T>(T[] values, T absent)
    {
        for (var length = 0; length <= values.Length; length++)
        {
            ReadOnlySpan<T> list = values.AsSpan(0, length);
            for (var i = 0; i < length; i++)
            {
                Assert.True(IsRejected(values[i], list), $"{typeof(T).Name}: {values[i]} at {i} of {length}");
                Assert.False(IsMissing(values[i], list), $"{typeof(T).Name}: {values[i]} at {i} of {length}");
            }

            Assert.False(IsRejected(absent, list), $"{typeof(T).Name}: {absent} in {length}");
            Assert.True(IsMissing(absent, list), $"{typeof(T).Name}: {absent} in {length}");
        }
    }

    private static bool IsRejected<T>(T value, scoped ReadOnlySpan<T> list)
    {
        var report = new ValidationReport();
        report.Check(value, "item").IfOneOf(list, comparer: null, message: null);
        var collected = !report.IsValid;
        try
        {
            value.Throw("item").IfOneOf(list, comparer: null, message: null);
            Assert.False(collected);
        }
        catch (ArgumentException)
        {
            Assert.True(collected);
        }

        return collected;
    }

    private static bool IsMissing<T>(T value, scoped ReadOnlySpan<T> list)
    {
        var report = new ValidationReport();
        report.Check(value, "item").IfNotOneOf(list, comparer: null, message: null);
        return !report.IsValid;
    }
#endif

    [Fact]
    public void Character_sets_of_every_size_match_a_simple_loop_on_random_input()
    {
        const string Pool = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_ .,!?<>\u00E9\u00FC\u00DF\u00E7\u00F1\u65E5\u672C\u8A9E\u4E2D\uD55C\u00A0\u2028\uFFFF\u0000\t\uD83D\uDE00";
        var random = new Random(20261010);
        for (var i = 0; i < 4000; i++)
        {
            var set = Enumerable.Range(0, random.Next(0, 41)).Select(_ => Pool[random.Next(Pool.Length)]).ToArray();
            var text = new string(Enumerable.Range(0, random.Next(0, 31)).Select(_ => Pool[random.Next(Pool.Length)]).ToArray());

            AssertMatches(text, set);
        }
    }

    [Theory]
    [InlineData("abcde", "xyz e")]
    [InlineData("abcdef", "uvwxyz")]
    [InlineData("abcdefghijklmnopqrstuvwxyz-0123456789", "hello-world-2")]
    [InlineData("abcdefghijklmnopqrstuvwxyz-0123456789", "hello world")]
    [InlineData("\u00E9\u00FC\u00DF\u00E7\u00F1\u65E5", "caf\u00E9")]
    [InlineData("\u00E9\u00FC\u00DF\u00E7\u00F1\u65E5", "plain ascii")]
    [InlineData("abcdefg", "\u65E5\u672C")]
    [InlineData("\u0000\uFFFF", "\uFFFF")]
    [InlineData("", "anything")]
    [InlineData("abcdefghij", "")]
    public void Character_set_edge_cases(string characters, string text) => AssertMatches(text, characters.ToCharArray());

    private static void AssertMatches(string text, char[] set)
    {
        var expectedAny = text.Any(c => Array.IndexOf(set, c) >= 0);
        var expectedExcept = text.Any(c => Array.IndexOf(set, c) < 0);
        var label = $"text '{Escape(text)}' set '{Escape(new string(set))}'";

        var report = new ValidationReport();
        report.Check(text, "any").IfContainsAny(set, message: null);
        report.Check(text, "except").IfContainsAnyExcept(set, message: null);
        report.Check(text, "anyParams").IfContainsAny(set);
        report.Check(text, "exceptParams").IfContainsAnyExcept(set);
        var names = report.Failures.Select(f => f.Name).ToList();

        Assert.True(expectedAny == names.Contains("any"), label);
        Assert.True(expectedAny == names.Contains("anyParams"), label);
        Assert.True(expectedExcept == names.Contains("except"), label);
        Assert.True(expectedExcept == names.Contains("exceptParams"), label);

#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        var span = new ValidationReport();
        span.Check(text, "any").IfContainsAny(set.AsSpan(), message: null);
        span.Check(text, "except").IfContainsAnyExcept(set.AsSpan(), message: null);
        span.Check(text, "anyText").IfContainsAny(new string(set));
        span.Check(text, "exceptText").IfContainsAnyExcept(new string(set));
        span.Check(text, "anySearch").IfContainsAny(SearchValues.Create(set));
        span.Check(text, "exceptSearch").IfContainsAnyExcept(SearchValues.Create(set));
        var spanNames = span.Failures.Select(f => f.Name).ToList();

        Assert.Equal(expectedAny, text.AsSpan().IndexOfAny(set) >= 0);
        Assert.Equal(expectedExcept, text.AsSpan().IndexOfAnyExcept(set) >= 0);
        Assert.True(expectedAny == spanNames.Contains("any"), label);
        Assert.True(expectedAny == spanNames.Contains("anyText"), label);
        Assert.True(expectedAny == spanNames.Contains("anySearch"), label);
        Assert.True(expectedExcept == spanNames.Contains("except"), label);
        Assert.True(expectedExcept == spanNames.Contains("exceptText"), label);
        Assert.True(expectedExcept == spanNames.Contains("exceptSearch"), label);
#endif
    }

    private static string Escape(string text) =>
        string.Concat(text.Select(c => c is >= ' ' and <= '~' ? c.ToString() : "\\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
}
