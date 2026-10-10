using System;
using Xunit;

namespace DotThrow.Tests.Support;

/// <summary>Runs one chain in throw mode and in collect mode and checks the outcome.</summary>
// GuardChain<T> is void after the merge, so the helper uses its own delegate that returns the guard.
internal delegate Guard<T> Chain<T>(Guard<T> guard);

internal static class Check
{
    public const string Name = "input";

    /// <summary>The chain passes in both modes.</summary>
    public static void Passes<T>(T value, Chain<T> chain)
    {
        _ = chain(value.Throw(Name));
        var report = new ValidationReport();
        _ = chain(report.Check(value, Name));
        Assert.True(report.IsValid, report.ToString());
    }

    /// <summary>The chain fails with this code (and message, when given) in collect mode, and throws TException in throw mode.</summary>
    public static ValidationFailure Fails<TException, T>(T value, Chain<T> chain, string code, string? message = null)
        where TException : Exception
    {
        var exception = Assert.Throws<TException>(() => chain(value.Throw(Name)));
        Assert.Equal(Name, (exception as ArgumentException)?.ParamName);

        var report = new ValidationReport();
        var guard = chain(report.Check(value, Name));
        Assert.True(guard.HasFailed);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(code, failure.ErrorCode);
        Assert.Equal(Name, failure.Name);
        if (message is not null)
        {
            Assert.Equal(message, failure.Message);
            Assert.StartsWith(message, exception.Message, StringComparison.Ordinal);
        }

        return failure;
    }

    /// <summary>The message of the first failure in collect mode.</summary>
    public static string MessageOf<T>(T value, Chain<T> chain)
    {
        var report = new ValidationReport();
        _ = chain(report.Check(value, Name));
        return Assert.Single(report.Failures).Message;
    }
}
