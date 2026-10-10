using System;
using System.Collections;
using System.Collections.Generic;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests.Guards;

// Case tables for the dictionary key checks added in v2. Reference-type keys are left out of
// default messages (they may be secrets); value-type keys are shown.
public sealed class DictionaryGuardTests
{
    public static IEnumerable<CheckCase> FailureCases()
    {
        var stock = new Dictionary<string, int> { ["apple"] = 1 };
        var ids = new Dictionary<int, string> { [7] = "Ada Lovelace" };
        IDictionary<string, int> asInterface = new Dictionary<string, int> { ["apple"] = 1 };
        IReadOnlyDictionary<int, string> asReadOnly = new Dictionary<int, string> { [7] = "Ada Lovelace" };
        var table = new Hashtable { ["apple"] = 1 };

        yield return Fail(stock, (g, m) => g.IfContainsKey("apple", m), ErrorCodes.ContainsKey, Expect.Argument, "'input' must not contain the rejected key.");
        yield return Fail(ids, (g, m) => g.IfContainsKey(7, m), ErrorCodes.ContainsKey, Expect.Argument, "'input' must not contain the key 7.");
        yield return Fail(stock, (g, m) => g.IfNotContainsKey("pear", m), ErrorCodes.NotContainsKey, Expect.Argument, "'input' must contain the required key.");
        yield return Fail(ids, (g, m) => g.IfNotContainsKey(8, m), ErrorCodes.NotContainsKey, Expect.Argument, "'input' must contain the key 8.");
        yield return Fail(asInterface, (g, m) => g.IfContainsKey("apple", m), ErrorCodes.ContainsKey, Expect.Argument, "'input' must not contain the rejected key.");
        yield return Fail(asInterface, (g, m) => g.IfNotContainsKey("pear", m), ErrorCodes.NotContainsKey, Expect.Argument, "'input' must contain the required key.");
        yield return Fail(asReadOnly, (g, m) => g.IfContainsKey(7, m), ErrorCodes.ContainsKey, Expect.Argument, "'input' must not contain the key 7.");
        yield return Fail(asReadOnly, (g, m) => g.IfNotContainsKey(8, m), ErrorCodes.NotContainsKey, Expect.Argument, "'input' must contain the key 8.");
        yield return Fail(table, (g, m) => g.IfContainsKey("apple", m), ErrorCodes.ContainsKey, Expect.Argument, "'input' must not contain the rejected key.");
        yield return Fail(table, (g, m) => g.IfNotContainsKey("pear", m), ErrorCodes.NotContainsKey, Expect.Argument, "'input' must contain the required key.");

        yield return Null<Dictionary<string, int>>((g, m) => g.IfContainsKey("apple", m));
        yield return Null<Dictionary<string, int>>((g, m) => g.IfNotContainsKey("apple", m));
        yield return Null<IDictionary<string, int>>((g, m) => g.IfContainsKey("apple", m));
        yield return Null<IDictionary<string, int>>((g, m) => g.IfNotContainsKey("apple", m));
        yield return Null<IReadOnlyDictionary<string, int>>((g, m) => g.IfContainsKey("apple", m));
        yield return Null<IReadOnlyDictionary<string, int>>((g, m) => g.IfNotContainsKey("apple", m));
        yield return Null<Hashtable>((g, m) => g.IfContainsKey("apple", m));
        yield return Null<Hashtable>((g, m) => g.IfNotContainsKey("apple", m));
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        var stock = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["apple"] = 1 };
        IDictionary<string, int> asInterface = new Dictionary<string, int> { ["apple"] = 1 };
        IReadOnlyDictionary<int, string> asReadOnly = new Dictionary<int, string> { [7] = "Ada Lovelace" };
        var table = new Hashtable { ["apple"] = 1 };

        yield return Pass(stock, (g, m) => g.IfContainsKey("pear", m));
        yield return Pass(stock, (g, m) => g.IfNotContainsKey("APPLE", m));
        yield return Pass(asInterface, (g, m) => g.IfContainsKey("pear", m));
        yield return Pass(asInterface, (g, m) => g.IfNotContainsKey("apple", m));
        yield return Pass(asReadOnly, (g, m) => g.IfContainsKey(8, m));
        yield return Pass(asReadOnly, (g, m) => g.IfNotContainsKey(7, m));
        yield return Pass(table, (g, m) => g.IfContainsKey("pear", m));
        yield return Pass(table, (g, m) => g.IfNotContainsKey("apple", m));
    }

    public static TheoryData<CheckCase> Failures => Data(FailureCases());

    public static TheoryData<CheckCase> Passes => Data(PassCases());

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_throws_the_default_exception(CheckCase check) => check.AssertThrows();

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_uses_a_custom_message(CheckCase check) => check.AssertCustomMessage();

    [Theory]
    [MemberData(nameof(Passes))]
    public void Passing_check_returns_the_value(CheckCase check) => check.AssertPasses();
}
