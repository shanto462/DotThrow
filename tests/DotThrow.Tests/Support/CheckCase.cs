using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace DotThrow.Tests.Support;

/// <summary>One check with an optional custom message, as used by the case tables.</summary>
public delegate Guard<T> Check<T>(Guard<T> guard, string? message);

/// <summary>The default exception a failed check throws.</summary>
public enum Expect
{
    Argument,
    OutOfRange,
    Null,
}

/// <summary>
/// One row of a case table: a value, one check, and (for failures) what the failure must
/// look like. The same row is run in throw mode, collect mode, with a custom message,
/// with an exception factory, and on an inactive guard.
/// </summary>
public abstract class CheckCase
{
    /// <summary>The name every case table uses for the checked value.</summary>
    public const string Subject = "input";

    public const string CustomMessage = "A custom message for the input.";

    protected CheckCase(string label, bool shouldFail, string? code, Expect kind, string? message)
    {
        Label = label;
        ShouldFail = shouldFail;
        ExpectedCode = code;
        Kind = kind;
        ExpectedMessage = message;
    }

    public string Label { get; }

    public bool ShouldFail { get; }

    public string? ExpectedCode { get; }

    public Expect Kind { get; }

    public string? ExpectedMessage { get; }

    public abstract void AssertPasses();

    public abstract void AssertThrows();

    public abstract void AssertCustomMessage();

    public abstract void AssertCollected();

    public abstract void AssertSkippedWhenInactive();

    public abstract void AssertExceptionFactory();

    public override string ToString() => Label;

    protected static string Describe(object? value) => value switch
    {
        null => "null",
        string text => "\"" + string.Concat(text.Select(c => c < ' ' || c > '~' ? "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture) : c.ToString())) + "\"",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Take(10).Select(Describe)) + "]",
        _ => value.ToString() ?? string.Empty,
    };
}

public sealed class CheckCase<T> : CheckCase
{
    private readonly T _value;
    private readonly Check<T> _check;

    public CheckCase(T value, Check<T> check, string label, bool shouldFail, string? code = null, Expect kind = Expect.Argument, string? message = null)
        : base($"{label} on {Describe(value)}", shouldFail, code, kind, message)
    {
        _value = value;
        _check = check;
    }

    public override void AssertPasses()
    {
        Assert.False(ShouldFail);

        var guard = _check(_value.Throw(paramName: Subject), null);
        Assert.True(guard.IsActive);
        Assert.False(guard.HasFailed);
        Assert.Equal(Subject, guard.Name);
        T converted = guard;
        AssertSameValue(_value, converted);

        // A custom message changes nothing when the check passes.
        _check(_value.Throw(paramName: Subject), CustomMessage);

        var report = new ValidationReport();
        var collected = _check(report.Check(_value, Subject), null);
        Assert.True(report.IsValid);
        Assert.True(collected.IsActive);
        Assert.False(collected.HasFailed);
    }

    public override void AssertThrows()
    {
        Assert.True(ShouldFail);
        var exception = Record.Exception(() => _check(_value.Throw(paramName: Subject), null));
        AssertDefaultException(exception, ExpectedMessage!);
    }

    public override void AssertCustomMessage()
    {
        var exception = Record.Exception(() => _check(_value.Throw(paramName: Subject), CustomMessage));
        AssertDefaultException(exception, CustomMessage);

        var report = new ValidationReport();
        _check(report.Check(_value, Subject), CustomMessage);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(CustomMessage, failure.Message);
        Assert.Equal(ExpectedCode, failure.ErrorCode);
    }

    public override void AssertCollected()
    {
        var report = new ValidationReport();
        var guard = _check(report.Check(_value, Subject), null);

        Assert.True(guard.HasFailed);
        Assert.False(guard.IsActive);
        Assert.Equal(Subject, guard.Name);
        AssertSameValue(_value, guard.Value);
        Assert.False(report.IsValid);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(Subject, failure.Name);
        Assert.Equal(ExpectedCode, failure.ErrorCode);
        Assert.Equal(ExpectedMessage, failure.Message);

        // A failed guard skips the rest of its chain, so running the check again adds nothing.
        var again = _check(guard, null);
        Assert.True(again.HasFailed);
        Assert.Single(report.Failures);
    }

    public override void AssertSkippedWhenInactive()
    {
        // Collect mode: a guard that already failed does not run the check.
        var report = new ValidationReport();
        var failed = report.Check(_value, Subject).Fail("Earlier", "An earlier failure.");
        var result = _check(failed, null);
        Assert.True(result.HasFailed);
        Assert.False(result.IsActive);
        var failure = Assert.Single(report.Failures);
        Assert.Equal("Earlier", failure.ErrorCode);

        if (_value is null)
        {
            // AllowNull stops the chain for a null value, in both modes.
            var skipped = _check(_value.Throw(paramName: Subject).AllowNull(), null);
            Assert.False(skipped.IsActive);
            Assert.False(skipped.HasFailed);

            var nullReport = new ValidationReport();
            _check(nullReport.Check(_value, Subject).AllowNull(), null);
            Assert.True(nullReport.IsValid);
        }
        else
        {
            // AllowNull has no effect on a value that is not null.
            var exception = Record.Exception(() => _check(_value.Throw(paramName: Subject).AllowNull(), null));
            AssertDefaultException(exception, ExpectedMessage!);
        }
    }

    public override void AssertExceptionFactory()
    {
        ValidationFailure? seen = null;
        var exception = Record.Exception(() => _check(
            _value.Throw(
                f =>
                {
                    seen = f;
                    return new CustomException(f);
                },
                Subject),
            null));

        var custom = Assert.IsType<CustomException>(exception);
        Assert.NotNull(seen);
        Assert.Same(seen, custom.Failure);
        Assert.Equal(Subject, seen.Name);
        Assert.Equal(ExpectedCode, seen.ErrorCode);
        Assert.Equal(ExpectedMessage, seen.Message);

        // A factory that returns null falls back to the default exception.
        var fallback = Record.Exception(() => _check(_value.Throw(_ => null!, Subject), null));
        AssertDefaultException(fallback, ExpectedMessage!);
    }

    private void AssertDefaultException(Exception? exception, string message)
    {
        Assert.NotNull(exception);
        switch (Kind)
        {
            case Expect.Null:
                Assert.IsType<ArgumentNullException>(exception);
                break;
            case Expect.OutOfRange:
                var outOfRange = Assert.IsType<ArgumentOutOfRangeException>(exception);

                // Value types are shown. Reference types (strings above all) are not.
                Assert.Equal(typeof(T).IsValueType ? _value : null, outOfRange.ActualValue);
                break;
            default:
                Assert.IsType<ArgumentException>(exception);
                break;
        }

        var argument = (ArgumentException)exception;
        Assert.Equal(Subject, argument.ParamName);
        Assert.StartsWith(message, argument.Message, StringComparison.Ordinal);
    }

    private static void AssertSameValue(T expected, T actual)
    {
        if (typeof(T).IsValueType)
        {
            Assert.True(EqualityComparer<T>.Default.Equals(expected, actual));
        }
        else
        {
            Assert.True(ReferenceEquals(expected, actual), "The guard must return the same instance.");
        }
    }
}

/// <summary>Builds case table rows. The label is the source text of the check.</summary>
public static class Cases
{
    public static CheckCase Pass<T>(T value, Check<T> check, [CallerArgumentExpression(nameof(check))] string label = "") =>
        new CheckCase<T>(value, check, label, shouldFail: false);

    public static CheckCase Fail<T>(
        T value,
        Check<T> check,
        string code,
        Expect kind,
        string message,
        [CallerArgumentExpression(nameof(check))] string label = "") =>
        new CheckCase<T>(value, check, label, shouldFail: true, code, kind, message);

    /// <summary>A null value that a content check rejects with the "Null" code.</summary>
    public static CheckCase Null<T>(Check<T> check, [CallerArgumentExpression(nameof(check))] string label = "")
        where T : class =>
        new CheckCase<T>(null!, check, label, shouldFail: true, ErrorCodes.Null, Expect.Null, $"'{CheckCase.Subject}' must not be null.");

    public static TheoryData<CheckCase> Data(IEnumerable<CheckCase> cases)
    {
        var data = new TheoryData<CheckCase>();
        foreach (var item in cases)
        {
            data.Add(item);
        }

        return data;
    }
}

/// <summary>The exception the exception factory tests create.</summary>
public sealed class CustomException : Exception
{
    public CustomException()
    {
    }

    public CustomException(string message)
        : base(message)
    {
    }

    public CustomException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CustomException(ValidationFailure failure)
        : base(failure?.Message)
    {
        Failure = failure;
    }

    public ValidationFailure? Failure { get; }
}
