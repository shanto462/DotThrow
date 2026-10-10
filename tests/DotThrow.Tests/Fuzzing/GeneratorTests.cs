using System;
using System.Collections.Generic;
using System.Linq;
using DotThrow.Dynamic;
using DotThrow.Internal;
using DotThrow.Tests.Dynamic;
using FsCheck.Fluent;
using Xunit;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// The grammar generators reach deep into the parsers: a fair share of their inputs is valid
/// and a fair share is not. A generator that makes only one kind tests only the first character.
/// </summary>
[Trait(Fuzz.Trait, Fuzz.Category)]
public sealed class GeneratorTests
{
    public static TheoryData<string> Grammars => ["Email", "AbsoluteUri", "RelativeUri", "Json", "Xml", "IPv6", "Template", "RuleDefinition"];

    [Theory]
    [MemberData(nameof(Grammars))]
    public void Grammar_inputs_are_both_valid_and_not_valid(string grammar)
    {
        var (gen, valid) = grammar switch
        {
            "Email" => (Generators.EmailLike, (Func<string, bool>)Formats.IsEmailAddress),
            "AbsoluteUri" => (Generators.UriLike, text => Formats.IsUri(text, UriKind.Absolute, out _)),
            "RelativeUri" => (Generators.UriLike, text => Formats.IsUri(text, UriKind.Relative, out _)),
            "Json" => (Generators.JsonLike, Json.IsValid),
            "Xml" => (Generators.XmlLike, text => Formats.IsXml(text)),
            "IPv6" => (Generators.IPv6Text, Text.IsIPAddress),
            "Template" => (Generators.Template, text => MessageTemplates.Format(text, "N", null) != MessageTemplates.Format(text, "N", new Dictionary<string, object?> { ["MaxLength"] = 1, ["A"] = 1, ["PropertyValue"] = 1 })),
            "RuleDefinition" => (Generators.RuleDefinitionJson, Builds),
            _ => throw new ArgumentOutOfRangeException(nameof(grammar)),
        };

        var samples = gen.Sample(400);
        var share = (double)samples.Count(valid) / samples.Length;
        Assert.InRange(share, 0.05, 0.95);
    }

    private static bool Builds(string json)
    {
        try
        {
            _ = new DynamicValidator<Sample>(RuleSetDefinition.FromJson(json), Maps.Sample());
            return true;
        }
        catch (RuleDefinitionException)
        {
            return false;
        }
    }
}
