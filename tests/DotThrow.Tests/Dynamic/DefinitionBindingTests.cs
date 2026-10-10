using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DotThrow.Dynamic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DotThrow.Tests.Dynamic;

// The definition classes bind from configuration, System.Text.Json and code, not only FromJson.
public sealed class DefinitionBindingTests
{
    private const string RuleSet = """
        {
          "Rules": [
            { "Property": "Text", "Severity": "Warning", "Checks": [ { "Check": "LongerThan", "Value": 5 } ] },
            { "Property": "Tags", "Each": true, "Checks": [ { "Check": "OneOf", "Values": [ "x", "y" ] } ] }
          ]
        }
        """;

    private const string Json = "{ \"Validation\": " + RuleSet + " }";

    // System.Text.Json does not read numbers into strings, so the argument is a string here.
    private const string TextArguments = """
        {
          "Rules": [
            { "Property": "Text", "Severity": "Warning", "Checks": [ { "Check": "LongerThan", "Value": "5" } ] },
            { "Property": "Tags", "Each": true, "Checks": [ { "Check": "OneOf", "Values": [ "x", "y" ] } ] }
          ]
        }
        """;

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private static void Check(RuleSetDefinition definition)
    {
        var validator = new DynamicValidator<Sample>(definition, Maps.Sample());
        var report = validator.Validate(new Sample { Tags = ["a", "x"] });
        Assert.Equal(["Text", "Tags[1]"], report.Failures.Select(f => f.Name));
        Assert.Equal(DotThrow.Severity.Warning, report.Failures[0].Severity);
    }

    [Fact]
    public void Binds_from_a_json_configuration_file()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var definition = configuration.GetSection("Validation").Get<RuleSetDefinition>()!;
        Assert.Equal("5", definition.Rules[0].Checks[0].Value);
        Check(definition);
    }

    [Fact]
    public void Binds_from_in_memory_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rules:0:Property"] = "Text",
                ["Rules:0:Severity"] = "Warning",
                ["Rules:0:Checks:0:Check"] = "LongerThan",
                ["Rules:0:Checks:0:Value"] = "5",
                ["Rules:1:Property"] = "Tags",
                ["Rules:1:Each"] = "true",
                ["Rules:1:Checks:0:Check"] = "OneOf",
                ["Rules:1:Checks:0:Values:0"] = "x",
                ["Rules:1:Checks:0:Values:1"] = "y",
            })
            .Build();
        Check(configuration.Get<RuleSetDefinition>()!);
    }

    [Fact]
    public void Binds_from_system_text_json_and_from_code()
    {
        Check(JsonSerializer.Deserialize<RuleSetDefinition>(TextArguments, CaseInsensitive)!);

        Check(new RuleSetDefinition
        {
            Rules =
            {
                new RuleDefinition { Property = "Text", Severity = "Warning", Checks = { new CheckDefinition { Check = "LongerThan", Value = "5" } } },
                new RuleDefinition { Property = "Tags", Each = true, Checks = { new CheckDefinition { Check = "OneOf", Values = ["x", "y"] } } },
            },
        });

        Check(RuleSetDefinition.FromJson(RuleSet));
    }
}
