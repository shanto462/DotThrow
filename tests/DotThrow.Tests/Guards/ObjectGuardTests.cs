using System;
using System.Collections.Generic;
using System.IO;
using DotThrow.Tests.Support;
using Xunit;
using static DotThrow.Tests.Support.Cases;

namespace DotThrow.Tests.Guards;

// Case tables for the stream, type and emptiness checks added in v2.
public sealed class ObjectGuardTests
{
    public static IEnumerable<CheckCase> FailureCases()
    {
        yield return Fail(new CapabilityStream(canRead: false), (g, m) => g.IfNotReadable(m), ErrorCodes.NotReadable, Expect.Argument, "'input' must be readable.");
        yield return Fail(new CapabilityStream(canWrite: false), (g, m) => g.IfNotWritable(m), ErrorCodes.NotWritable, Expect.Argument, "'input' must be writable.");
        yield return Fail(new CapabilityStream(canSeek: false), (g, m) => g.IfNotSeekable(m), ErrorCodes.NotSeekable, Expect.Argument, "'input' must be seekable.");
        yield return Fail(typeof(string), (g, m) => g.IfNotAssignableTo(typeof(IDisposable), m), ErrorCodes.NotAssignableTo, Expect.Argument, "'input' must be assignable to IDisposable.");
        yield return Fail(typeof(Stream), (g, m) => g.IfAbstract(m), ErrorCodes.Abstract, Expect.Argument, "'input' must not be an abstract type.");
        yield return Fail(typeof(IDisposable), (g, m) => g.IfAbstract(m), ErrorCodes.Abstract, Expect.Argument, "'input' must not be an abstract type.");
        yield return Fail(typeof(IDisposable), (g, m) => g.IfInterface(m), ErrorCodes.Interface, Expect.Argument, "'input' must not be an interface.");
        yield return Fail("", (g, m) => g.IfEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail("  ", (g, m) => g.IfEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(new List<int>(), (g, m) => g.IfEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(new MemoryStream(), (g, m) => g.IfEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(0, (g, m) => g.IfEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(default(DateTime), (g, m) => g.IfEmpty(m), ErrorCodes.Empty, Expect.Argument, "'input' must not be empty.");
        yield return Fail(5, (g, m) => g.IfNotEmpty(m), ErrorCodes.NotEmpty, Expect.Argument, "'input' must be empty.");
        yield return Fail("Ada", (g, m) => g.IfNotEmpty(m), ErrorCodes.NotEmpty, Expect.Argument, "'input' must be empty.");
        yield return Fail(new List<int> { 1 }, (g, m) => g.IfNotEmpty(m), ErrorCodes.NotEmpty, Expect.Argument, "'input' must be empty.");

        yield return Null<CapabilityStream>((g, m) => g.IfNotReadable(m));
        yield return Null<CapabilityStream>((g, m) => g.IfNotWritable(m));
        yield return Null<CapabilityStream>((g, m) => g.IfNotSeekable(m));
        yield return Null<Type>((g, m) => g.IfNotAssignableTo(typeof(object), m));
        yield return Null<Type>((g, m) => g.IfAbstract(m));
        yield return Null<Type>((g, m) => g.IfInterface(m));
        yield return Null<string>((g, m) => g.IfEmpty(m));
    }

    public static IEnumerable<CheckCase> PassCases()
    {
        yield return Pass(new CapabilityStream(), (g, m) => g.IfNotReadable(m));
        yield return Pass(new CapabilityStream(), (g, m) => g.IfNotWritable(m));
        yield return Pass(new CapabilityStream(), (g, m) => g.IfNotSeekable(m));
        yield return Pass(typeof(MemoryStream), (g, m) => g.IfNotAssignableTo(typeof(IDisposable), m));
        yield return Pass(typeof(MemoryStream), (g, m) => g.IfAbstract(m));
        yield return Pass(typeof(Stream), (g, m) => g.IfInterface(m));
        yield return Pass("Ada", (g, m) => g.IfEmpty(m));
        yield return Pass(new List<int> { 1 }, (g, m) => g.IfEmpty(m));
        yield return Pass(new MemoryStream([1]), (g, m) => g.IfEmpty(m));
        yield return Pass("", (g, m) => g.IfNotEmpty(m));
        yield return Pass(1, (g, m) => g.IfEmpty(m));
        yield return Pass<int?>(0, (g, m) => g.IfEmpty(m));
        yield return Pass(0, (g, m) => g.IfNotEmpty(m));
        yield return Pass<string?>(null, (g, m) => g.IfNotEmpty(m));
        yield return Pass(new List<int>(), (g, m) => g.IfNotEmpty(m));
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
