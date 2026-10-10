using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;

namespace DotThrow.AspNetCore.Internal;

// Builds the "errorCodes" and "severities" extensions of a validation problem. Keys and order
// match ValidationReport.ToDictionary() (the "errors" member). AttemptedValue is never used (D10).
internal static class ProblemExtensions
{
    internal const string ErrorCodesKey = "errorCodes";
    internal const string SeveritiesKey = "severities";

    // Adds the extensions that the options ask for. With asJson the values are JsonElement, which
    // the framework's own problem details JSON metadata covers, so Native AOT apps need no extra
    // JsonSerializerContext entries. Without it they are Dictionary<string, string[]> (MVC, where
    // Newtonsoft.Json or XML formatters may write the response).
    internal static void Add(IDictionary<string, object?> extensions, IReadOnlyList<ValidationFailure> failures, ValidationProblemOptions? options, bool asJson)
    {
        if (options is null || failures.Count == 0)
        {
            return;
        }

        if (options.IncludeErrorCodes)
        {
            var codes = Group(failures, static failure => failure.ErrorCode);
            extensions[ErrorCodesKey] = asJson ? ToJson(codes) : codes;
        }

        if (options.IncludeSeverities)
        {
            var severities = Group(failures, static failure => SeverityName(failure.Severity));
            extensions[SeveritiesKey] = asJson ? ToJson(severities) : severities;
        }
    }

    internal static string SeverityName(Severity severity) => severity switch
    {
        Severity.Error => "Error",
        Severity.Warning => "Warning",
        Severity.Info => "Info",
        _ => ((int)severity).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    // Same grouping as ValidationReport.ToDictionary(): ordinal keys in first-seen order.
    private static Dictionary<string, string[]> Group(IReadOnlyList<ValidationFailure> failures, Func<ValidationFailure, string> select)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var failure in failures)
        {
            if (!grouped.TryGetValue(failure.Name, out var values))
            {
                values = [];
                grouped.Add(failure.Name, values);
            }

            values.Add(select(failure));
        }

        var result = new Dictionary<string, string[]>(grouped.Count, StringComparer.Ordinal);
        foreach (var pair in grouped)
        {
            result.Add(pair.Key, [.. pair.Value]);
        }

        return result;
    }

    private static JsonElement ToJson(Dictionary<string, string[]> values)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var pair in values)
            {
                writer.WriteStartArray(pair.Key);
                foreach (var value in pair.Value)
                {
                    writer.WriteStringValue(value);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
