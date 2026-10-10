using System;
using System.Globalization;
using System.Text;
using Xunit;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// The contract every check keeps, whatever the input:
/// <list type="bullet">
/// <item>Throw mode throws only an <see cref="ArgumentException"/> (or a subclass) with the guard name as <see cref="ArgumentException.ParamName"/>, or a custom exception from the exception factory. Any other exception is a bug.</item>
/// <item>Collect mode never throws and records at most one failure per chain, with the guard name and the check's error code (<c>Null</c> for a null value).</item>
/// <item>Both modes agree: a value fails in one exactly when it fails in the other, with the same message.</item>
/// </list>
/// </summary>
internal static class Contract
{
    public const string Name = "input";

    /// <summary>Runs the chain in every mode, checks the contract, and returns whether the value failed.</summary>
    /// <param name="value">The value.</param>
    /// <param name="chain">The checks.</param>
    /// <param name="code">The error code of a failure (a null value fails with <c>Null</c> instead, unless <paramref name="nullCode"/> says otherwise).</param>
    /// <param name="nullCode">The error code of a null value, when it is not <c>Null</c>.</param>
    /// <returns><see langword="true"/> when the value failed.</returns>
    public static bool Fails<T>(T? value, Support.Chain<T> chain, string code, string? nullCode = null)
    {
        var outcome = Run(value, chain, code, nullCode);
        Assert.True(
            outcome.Collected == outcome.Thrown && outcome.Collected == outcome.FromFactory,
            $"The modes disagree for {Show(value)}: collect {Word(outcome.Collected)}, throw {Word(outcome.Thrown)}, factory {Word(outcome.FromFactory)}.");
        if (outcome.Collected)
        {
            Assert.Equal(outcome.Message, outcome.FactoryMessage);
            Assert.StartsWith(outcome.Message!, outcome.ThrownMessage, StringComparison.Ordinal);
        }

        return outcome.Collected;
    }

    /// <summary>
    /// Runs the chain in every mode and checks each mode on its own (exception types, parameter
    /// name, error code, one failure at most), without comparing the modes. For checks whose
    /// result may differ from run to run, such as a regular expression that can time out.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="chain">The checks.</param>
    /// <param name="code">The error code of a failure.</param>
    /// <param name="nullCode">The error code of a null value, when it is not <c>Null</c>.</param>
    /// <returns>Whether each mode failed.</returns>
    public static Outcome Run<T>(T? value, Support.Chain<T> chain, string code, string? nullCode = null)
    {
        var expectedCode = value is null ? nullCode ?? ErrorCodes.Null : code;

        // Throw mode with the default exceptions.
        Exception? thrown = null;
        try
        {
            _ = chain(value!.Throw(Name));
        }
#pragma warning disable CA1031 // Any exception is checked below.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            thrown = exception;
        }

        if (thrown is not null && thrown is not ArgumentException)
        {
            Assert.Fail($"Throw mode threw {thrown.GetType().FullName} for {Show(value)}: {thrown}");
        }

        if (thrown is ArgumentException argument)
        {
            Assert.True(argument.ParamName == Name, $"ParamName was {Show(argument.ParamName)} for {Show(value)}.");
            Assert.True(
                (value is null && expectedCode == ErrorCodes.Null) == (argument is ArgumentNullException),
                $"{argument.GetType().Name} for {Show(value)} with the code {expectedCode}.");
        }

        // Throw mode with an exception factory: the failure carries the error code.
        ValidationFailure? factoryFailure = null;
        try
        {
            _ = chain(value!.Throw(failure => new FactoryException(failure), Name));
        }
        catch (FactoryException exception)
        {
            factoryFailure = exception.Failure;
        }
#pragma warning disable CA1031 // Any other exception is a bug.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Assert.Fail($"Throw mode with a factory threw {exception.GetType().FullName} for {Show(value)}: {exception}");
        }

        if (factoryFailure is not null)
        {
            Assert.Equal(Name, factoryFailure.Name);
            Assert.True(factoryFailure.ErrorCode == expectedCode, $"The factory got the code {factoryFailure.ErrorCode}, not {expectedCode}, for {Show(value)}.");
        }

        // Collect mode.
        var report = new ValidationReport();
        bool hasFailed;
        try
        {
            hasFailed = chain(report.Check(value!, Name)).HasFailed;
        }
#pragma warning disable CA1031 // Any exception is a bug.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            throw new InvalidOperationException($"Collect mode threw {exception.GetType().FullName} for {Show(value)}.", exception);
        }

        Assert.True(report.Failures.Count <= 1, $"Collect mode recorded {report.Failures.Count} failures for {Show(value)}: {report}");
        var collected = report.Failures.Count == 1;
        Assert.True(hasFailed == collected, $"HasFailed is {hasFailed} with {report.Failures.Count} failures for {Show(value)}.");
        if (collected)
        {
            var failure = report.Failures[0];
            Assert.Equal(Name, failure.Name);
            Assert.True(failure.ErrorCode == expectedCode, $"The code was {failure.ErrorCode}, not {expectedCode}, for {Show(value)}.");
        }

        return new Outcome(thrown is not null, factoryFailure is not null, collected, thrown?.Message, factoryFailure?.Message, collected ? report.Failures[0].Message : null);
    }

    private static string Word(bool failed) => failed ? "failed" : "passed";

    /// <summary>Text for assertion messages: control, non-ASCII and surrogate characters as <c>\uXXXX</c>, so every input can be read and copied.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Show(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is not string text)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        var builder = new StringBuilder(text.Length + 2).Append('"');
        foreach (var c in text)
        {
            if (c is < ' ' or > '~' or '"' or '\\')
            {
                builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.Append('"').Append(" (length ").Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(')').ToString();
    }

    /// <summary>Whether the text has a surrogate that is not part of a pair (invalid UTF-16).</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the text is not valid UTF-16.</returns>
    public static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether each mode failed, and the messages.</summary>
    internal sealed record Outcome(bool Thrown, bool FromFactory, bool Collected, string? ThrownMessage, string? FactoryMessage, string? Message);

    private sealed class FactoryException(ValidationFailure failure) : Exception(failure.Message)
    {
        public ValidationFailure Failure { get; } = failure;
    }
}
