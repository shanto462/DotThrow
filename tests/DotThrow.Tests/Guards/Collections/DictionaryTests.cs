using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

public sealed class DictionaryTests
{
    private static readonly Dictionary<string, int> Stock = new() { ["apple"] = 3 };

    [Fact]
    public void Every_dictionary_shape_checks_keys()
    {
        Check.Fails<ArgumentException, Dictionary<string, int>>(Stock, g => g.IfContainsKey("apple"), ErrorCodes.ContainsKey, "'input' must not contain the rejected key.");
        Check.Passes(Stock, g => g.IfContainsKey("pear").IfNotContainsKey("apple"));
        Check.Fails<ArgumentException, Dictionary<string, int>>(Stock, g => g.IfNotContainsKey("pear"), ErrorCodes.NotContainsKey, "'input' must contain the required key.");

        IDictionary<string, int> mutable = new Dictionary<string, int>(Stock);
        Check.Fails<ArgumentException, IDictionary<string, int>>(mutable, g => g.IfContainsKey("apple"), ErrorCodes.ContainsKey);
        Check.Passes(mutable, g => g.IfNotContainsKey("apple"));

        IReadOnlyDictionary<string, int> readOnly = Stock;
        Check.Fails<ArgumentException, IReadOnlyDictionary<string, int>>(readOnly, g => g.IfNotContainsKey("pear"), ErrorCodes.NotContainsKey);
        Check.Passes(readOnly, g => g.IfContainsKey("pear"));

        var concurrent = new ConcurrentDictionary<string, int>(Stock);
        Check.Fails<ArgumentException, ConcurrentDictionary<string, int>>(concurrent, g => g.IfContainsKey("apple"), ErrorCodes.ContainsKey);
        Check.Passes(concurrent, g => g.IfNotContainsKey("apple"));

        var table = new Hashtable { [42] = "x" };
        Check.Fails<ArgumentException, Hashtable>(table, g => g.IfContainsKey(42), ErrorCodes.ContainsKey, "'input' must not contain the key 42.");
        Check.Fails<ArgumentException, Hashtable>(table, g => g.IfNotContainsKey("x"), ErrorCodes.NotContainsKey, "'input' must contain the required key.");
    }

    [Fact]
    public void Value_type_keys_appear_in_the_default_message()
    {
        var byId = new Dictionary<int, string> { [7] = "seven" };
        Assert.Equal("'input' must not contain the key 7.", Check.MessageOf(byId, g => g.IfContainsKey(7)));
        Assert.Equal("'input' must contain the key 8.", Check.MessageOf(byId, g => g.IfNotContainsKey(8)));
        Assert.Equal("key 'apple'", Check.MessageOf(Stock, g => g.IfContainsKey("apple", "key {Key}")));
    }

    [Fact]
    public void Null_dictionaries_and_null_keys()
    {
        Check.Fails<ArgumentNullException, Dictionary<string, int>>(null!, g => g.IfContainsKey("a"), ErrorCodes.Null);
        Assert.Throws<ArgumentNullException>(() => Stock.Throw().IfContainsKey(null!));
        Assert.Throws<ArgumentNullException>(() => new Hashtable().Throw().IfNotContainsKey(null!));
    }
}
