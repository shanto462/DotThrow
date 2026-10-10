using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DotThrow.Internal;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Internals;

public sealed class FormatsTests
{
    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData("a@b", true)]
    [InlineData("a.b@c.d", true)]
    [InlineData("a@b@c", false)]
    [InlineData(".a@b", false)]
    [InlineData("a.@b", false)]
    [InlineData("a..b@c", false)]
    [InlineData("a@.b", false)]
    [InlineData("a@b.", false)]
    [InlineData("a@b..c", false)]
    [InlineData("a b@c", false)]
    [InlineData("@b", false)]
    [InlineData("a@", false)]
    [InlineData("", false)]
    [InlineData(".@.", false)]
    [InlineData("a@\u0007", false)]
    public void IsEmailAddress(string value, bool expected) => Assert.Equal(expected, Formats.IsEmailAddress(value));

    [Theory]
    [InlineData("https://example.com", UriKind.Absolute, true)]
    [InlineData("file:///etc/hosts", UriKind.Absolute, true)]
    [InlineData("File:///etc/hosts", UriKind.RelativeOrAbsolute, true)]
    [InlineData("/etc/hosts", UriKind.Absolute, false)]
    [InlineData("C:\\temp", UriKind.Absolute, false)]
    [InlineData("C:\\temp", UriKind.RelativeOrAbsolute, false)]
    [InlineData("/etc/hosts", UriKind.Relative, true)]
    [InlineData("a/b", UriKind.Relative, true)]
    [InlineData("a/b", UriKind.Absolute, false)]
    public void IsUri(string value, UriKind kind, bool expected)
    {
        var result = Formats.IsUri(value, kind, out var uri);

        Assert.Equal(expected, result);
        Assert.Equal(expected, uri is not null);
    }

    [Fact]
    public void IsUri_returns_the_parsed_uri()
    {
        Assert.True(Formats.IsUri("https://example.com/a?b=c", UriKind.Absolute, out var uri));

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("/a", uri.AbsolutePath);
    }

    [Theory]
    [InlineData("<a/>", true)]
    [InlineData("<?xml version=\"1.0\"?><a/>", true)]
    [InlineData("<!DOCTYPE a><a/>", false)]
    [InlineData("<!DOCTYPE a [<!ENTITY e \"x\">]><a>&e;</a>", false)]
    [InlineData("<a>", false)]
    [InlineData("", false)]
    public void IsXml(string value, bool expected) => Assert.Equal(expected, Formats.IsXml(value));

    [Theory]
    [InlineData("ABC", true, true)]
    [InlineData("ABC", false, false)]
    [InlineData("abc", false, true)]
    [InlineData("abc", true, false)]
    [InlineData("A1!", true, true)]
    [InlineData("a1!", false, true)]
    [InlineData("1!", true, false)]
    [InlineData("1!", false, false)]
    [InlineData("日本", true, false)]
    [InlineData("日本", false, false)]
    [InlineData("", true, false)]
    [InlineData("Ǆ", true, true)]
    [InlineData("ǅ", true, false)]
    [InlineData("ǅ", false, false)]
    public void AllLettersAre(string value, bool upper, bool expected) => Assert.Equal(expected, Formats.AllLettersAre(value, upper));

    [Theory]
    [InlineData("Ada Lovelace", "love", StringComparison.Ordinal, false)]
    [InlineData("Ada Lovelace", "love", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("Ada Lovelace", "", StringComparison.Ordinal, true)]
    [InlineData("", "a", StringComparison.Ordinal, false)]
    public void Contains(string value, string part, StringComparison comparison, bool expected) =>
        Assert.Equal(expected, Formats.Contains(value, part, comparison));

    [Fact]
    public void CountUpTo_stops_at_the_limit()
    {
        var endless = new CountingSequence<int>(i => i);

        Assert.Equal(5, Collections.CountUpTo(endless, 5));
        Assert.Equal(5, endless.ItemsRead);
        Assert.Equal(1, endless.Disposals);
    }

    [Fact]
    public void CountUpTo_with_a_zero_limit_reads_nothing()
    {
        var endless = new CountingSequence<int>(i => i);

        Assert.Equal(0, Collections.CountUpTo(endless, 0));
        Assert.Equal(0, endless.ItemsRead);
    }

    [Fact]
    public void CountUpTo_returns_the_full_count_of_a_short_sequence()
    {
        var three = new CountingSequence<int>(i => i, count: 3);

        Assert.Equal(3, Collections.CountUpTo(three, 10));
    }

    [Fact]
    public void CountUpTo_uses_Count_when_the_collection_has_one()
    {
        Assert.Equal(3, Collections.CountUpTo(new ArrayList { 1, 2, 3 }, 10));
        Assert.Equal(2, Collections.CountUpTo(new List<int> { 1, 2, 3 }, 2));
        Assert.Equal(7, Collections.CountUpTo(new CountOnlyCollection<string>(1000), 7));
        Assert.Equal(1000, Collections.CountUpTo(new HashSet<string>(Enumerable.Range(0, 1000).Select(i => "item" + i)), int.MaxValue));
    }

    [Fact]
    public void CountUpTo_works_with_a_non_disposable_enumerator()
    {
        Assert.Equal(2, Collections.CountUpTo(new PlainEnumerable(), 2));
    }

    [Fact]
    public void Contains_uses_the_collection_comparer_only_without_a_comparer()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ada" };

        Assert.True(Collections.Contains(set, "ADA", comparer: null));
        Assert.False(Collections.Contains(set, "ADA", StringComparer.Ordinal));
        Assert.True(Collections.Contains(new CountingSequence<string>(i => "item" + i, count: 3), "item2", comparer: null));
        Assert.False(Collections.Contains(new CountingSequence<string>(i => "item" + i, count: 3), "ITEM2", comparer: null));
    }

    [Fact]
    public void ContainsNull_and_HasDuplicates()
    {
        Assert.True(Collections.ContainsNull(new object?[] { 1, null }));
        Assert.False(Collections.ContainsNull(new object?[] { 1, 2 }));
        Assert.False(Collections.ContainsNull(Array.Empty<object>()));
        Assert.True(Collections.HasDuplicates(new[] { 1, 2, 1 }));
        Assert.True(Collections.HasDuplicates(new object?[] { null, null }));
        Assert.False(Collections.HasDuplicates(new[] { 1, 2, 3 }));
        Assert.False(Collections.HasDuplicates(Array.Empty<int>()));
    }

    private sealed class PlainEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator() => new PlainEnumerator();

        private sealed class PlainEnumerator : IEnumerator
        {
            private int _index = -1;

            public object Current => _index;

            public bool MoveNext() => ++_index < 3;

            public void Reset() => _index = -1;
        }
    }
}
