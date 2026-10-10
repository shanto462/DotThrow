using System;
using System.Linq;
using System.Threading.Tasks;

namespace DotThrow.Samples;

/// <summary>Small helpers that print what happens. Not part of DotThrow.</summary>
internal static class Show
{
    public static void Title(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {text}");
    }

    public static void Line(string text) => Console.WriteLine($"  {text}");

    /// <summary>Runs code that must throw, and prints the exception.</summary>
    public static void Error(Func<object?> code) => Error(() => { code(); });

    /// <summary>Runs code that must throw, and prints the exception.</summary>
    public static void Error(Action code)
    {
        try
        {
            code();
        }
        catch (Exception exception) when (exception is ArgumentException or DomainException or InvalidOperationException)
        {
            Exception(exception);
            return;
        }

        throw new InvalidOperationException("The sample expected an exception, but none was thrown.");
    }

    /// <summary>Runs async code that must throw, and prints the exception.</summary>
    public static async Task ErrorAsync(Func<Task> code)
    {
        try
        {
            await code().ConfigureAwait(false);
        }
        catch (ValidationFailedException exception)
        {
            Exception(exception);
            return;
        }

        throw new InvalidOperationException("The sample expected an exception, but none was thrown.");
    }

    public static void Report(ValidationReport report)
    {
        if (report.IsValid)
        {
            Line("Valid: no failures.");
            return;
        }

        Line($"{report.Failures.Count} failure(s):");
        foreach (var failure in report.Failures)
        {
            var name = failure.Name.Length == 0 ? "(whole object)" : failure.Name;
            Line($"  {name}: {failure.Message} [{failure.ErrorCode}]");
        }
    }

    /// <summary>Prints the dictionary that ASP.NET Core turns into a 400 response.</summary>
    public static void Dictionary(ValidationReport report)
    {
        Line("{");
        foreach (var pair in report.ToDictionary())
        {
            var messages = string.Join(", ", pair.Value.Select(message => $"\"{message}\""));
            Line($"  \"{pair.Key}\": [{messages}]");
        }

        Line("}");
    }

    private static void Exception(Exception exception)
    {
        switch (exception)
        {
            case ValidationFailedException failed:
                Line($"{failed.GetType().Name} with {failed.Failures.Count} failure(s):");
                foreach (var failure in failed.Failures)
                {
                    Line($"  {failure.Name}: {failure.Message} [{failure.ErrorCode}]");
                }

                break;

            case ArgumentOutOfRangeException outOfRange:
                Line(outOfRange.GetType().Name);
                Line($"  ParamName:   {outOfRange.ParamName}");
                Line($"  ActualValue: {outOfRange.ActualValue ?? "(not set)"}");
                Line($"  Message:     {outOfRange.Message.ReplaceLineEndings(" ")}");
                break;

            case ArgumentException argument:
                Line(argument.GetType().Name);
                Line($"  ParamName:   {argument.ParamName}");
                Line($"  Message:     {argument.Message}");
                break;

            case DomainException domain:
                Line($"{domain.GetType().Name} (Code = {domain.Code})");
                Line($"  Message:     {domain.Message}");
                break;

            default:
                Line(exception.GetType().Name);
                Line($"  Message:     {exception.Message}");
                break;
        }
    }
}
