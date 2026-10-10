using System;
using System.Collections.Generic;
using System.Text.Json;
using DotThrow.AspNetCore.Internal;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DotThrow.AspNetCore.Tests;

public sealed class ProblemDetailsTests
{
    private static readonly ValidationProblemOptions All = new() { IncludeErrorCodes = true, IncludeSeverities = true };

    private static readonly JsonSerializerOptions CamelKeys = new() { DictionaryKeyPolicy = JsonNamingPolicy.CamelCase };

    private static ValidationReport Report() => new ValidationReport()
        .Add(new ValidationFailure("Email", "'Email' is not an email address.", ErrorCodes.NotEmailAddress, Severity.Error, attemptedValue: "secret-value"))
        .Add(new ValidationFailure("Email", "'Email' is too long.", ErrorCodes.LongerThan, Severity.Warning))
        .Add(new ValidationFailure("", "The order is empty.", "EmptyOrder", Severity.Info));

    [Fact]
    public void ToValidationProblem_matches_TypedResults_and_adds_json_extensions()
    {
        var result = Report().ToValidationProblem(All);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(Report().ToDictionary(), result.ProblemDetails.Errors);
        Assert.Equal(
            """{"Email":["NotEmailAddress","LongerThan"],"":["EmptyOrder"]}""",
            Assert.IsType<JsonElement>(result.ProblemDetails.Extensions["errorCodes"]).GetRawText());
        Assert.Equal(
            """{"Email":["Error","Warning"],"":["Info"]}""",
            Assert.IsType<JsonElement>(result.ProblemDetails.Extensions["severities"]).GetRawText());
    }

    [Fact]
    public void Key_policy_renames_the_extension_keys()
    {
        var result = Report().ToValidationProblem(All, JsonNamingPolicy.CamelCase);
        var details = Report().ToProblemDetails(All, JsonNamingPolicy.CamelCase);

        Assert.Equal("""{"email":["NotEmailAddress","LongerThan"],"":["EmptyOrder"]}""", ((JsonElement)result.ProblemDetails.Extensions["errorCodes"]!).GetRawText());
        Assert.Equal("""{"email":["Error","Warning"],"":["Info"]}""", ((JsonElement)details.Extensions["severities"]!).GetRawText());

        // The serializer applies the same policy to "errors", so the three dictionaries line up.
        var text = JsonSerializer.Serialize(details, CamelKeys);
        Assert.Contains("\"errors\":{\"email\":", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Extensions_are_added_only_when_asked()
    {
        Assert.Empty(Report().ToValidationProblem().ProblemDetails.Extensions);
        Assert.Empty(Report().ToValidationProblem(new ValidationProblemOptions()).ProblemDetails.Extensions);
        Assert.Equal(["errorCodes"], Report().ToProblemDetails(new ValidationProblemOptions { IncludeErrorCodes = true }).Extensions.Keys);
        Assert.Equal(["severities"], Report().ToValidationProblemDetails(new ValidationProblemOptions { IncludeSeverities = true }).Extensions.Keys);
        Assert.Empty(new ValidationReport().ToProblemDetails(All).Extensions);
        Assert.Empty(new ValidationReport().ToValidationProblem(All).ProblemDetails.Errors);
    }

    [Fact]
    public void ToProblemDetails_is_a_400_with_json_extensions()
    {
        var details = Report().ToProblemDetails(All);

        Assert.Equal(400, details.Status);
        Assert.Equal(Report().ToDictionary(), details.Errors);
        Assert.IsType<JsonElement>(details.Extensions["errorCodes"]);
        Assert.IsType<JsonElement>(details.Extensions["severities"]);
    }

    [Fact]
    public void ToValidationProblemDetails_is_an_mvc_400_with_dictionary_extensions()
    {
        var details = Report().ToValidationProblemDetails(All);

        Assert.Equal(400, details.Status);
        Assert.Equal(Report().ToDictionary(), details.Errors);
        var codes = Assert.IsType<Dictionary<string, string[]>>(details.Extensions["errorCodes"]);
        Assert.Equal(["NotEmailAddress", "LongerThan"], codes["Email"]);
        var severities = Assert.IsType<Dictionary<string, string[]>>(details.Extensions["severities"]);
        Assert.Equal(["Info"], severities[""]);
    }

    [Fact]
    public void Attempted_values_are_never_written()
    {
        var details = Report().ToProblemDetails(All);
        var text = JsonSerializer.Serialize(details);

        Assert.DoesNotContain("secret-value", text, StringComparison.Ordinal);
        Assert.Contains("\"errorCodes\":{\"Email\":[\"NotEmailAddress\",\"LongerThan\"]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_severities_are_written_as_numbers()
    {
        Assert.Equal("7", ProblemExtensions.SeverityName((Severity)7));
    }

    [Fact]
    public void Null_reports_throw()
    {
        ValidationReport report = null!;

        Assert.Throws<ArgumentNullException>("report", () => report.ToValidationProblem());
        Assert.Throws<ArgumentNullException>("report", () => report.ToProblemDetails());
        Assert.Throws<ArgumentNullException>("report", () => report.ToValidationProblemDetails());
    }
}
