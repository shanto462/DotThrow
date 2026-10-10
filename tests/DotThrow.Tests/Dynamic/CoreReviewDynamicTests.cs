using DotThrow.Dynamic;
using Xunit;

namespace DotThrow.Tests.Dynamic;

// Findings of the blind review of the core library that concern dynamic rules.
public sealed class CoreReviewDynamicTests
{
    // 3. A message format no value can use is a definition error, found when the validator is built.
    [Fact]
    public void Messages_with_an_unusable_format_are_definition_errors()
    {
        var errors = Assert.Throws<RuleDefinitionException>(() => new DynamicValidator<Sample>(
            RuleSetDefinition.FromJson("""
                { "rules": [
                  { "property": "Number", "message": "{PropertyName} is over {ComparisonValue:Q}.", "checks": [ { "check": "GreaterThan", "value": 5 } ] },
                  { "property": "Price", "checks": [ { "check": "GreaterThan", "value": 5, "message": "{ComparisonValue:Q} is too much." } ] },
                  { "property": "Lines[].Quantity", "message": "{PropertyValue:Q}", "checks": [ "Zero" ] } ] }
                """),
            Maps.Sample())).Errors;

        Assert.Equal(
            [
                "Rules[0].Message: The format 'Q' of the placeholder {ComparisonValue} is not valid.",
                "Rules[1].Checks[0].Message: The format 'Q' of the placeholder {ComparisonValue} is not valid.",
                "Rules[2].Message: The format 'Q' of the placeholder {PropertyValue} is not valid.",
            ],
            errors);
    }

    [Fact]
    public void Messages_with_valid_formats_and_unknown_names_build_and_render()
    {
        var validator = new DynamicValidator<Sample>(
            Rules.One("Price", """{ "check": "GreaterThan", "value": 5, "message": "{PropertyName} {PropertyValue:N2} is over {ComparisonValue:N1} {Unknown}." }"""),
            Maps.Sample());

        Assert.Equal("Price 10.50 is over 5.0 {Unknown}.", Assert.Single(validator.Validate(new Sample()).Failures).Message);
    }

    // 4. A pattern that takes too long (1 s) fails the check with its own code and message
    // (fail closed, the same policy as IfMatches in code) instead of throwing out of Validate.
    [Theory]
    [InlineData("Matches", "'Text' must not match the pattern '^(a|aa)+$'.")]
    [InlineData("NotMatches", "'Text' must match the pattern '^(a|aa)+$'.")]
    public void A_regex_timeout_fails_the_check_with_its_own_code(string check, string message)
    {
        var validator = new DynamicValidator<Sample>(Rules.One("Text", $$"""{ "check": "{{check}}", "value": "^(a|aa)+$" }"""), Maps.Sample());

        var failure = Assert.Single(validator.Validate(new Sample { Text = new string('a', 40) + "b" }).Failures);

        Assert.Equal(check, failure.ErrorCode);
        Assert.Equal(message, failure.Message);
    }

    [Fact]
    public void A_regex_timeout_uses_the_message_of_the_definition()
    {
        var validator = new DynamicValidator<Sample>(
            Rules.One("Text", """{ "check": "Matches", "value": "^(a|aa)+$", "message": "{PropertyName} is not allowed." }"""),
            Maps.Sample());

        var failure = Assert.Single(validator.Validate(new Sample { Text = new string('a', 40) + "b" }).Failures);

        Assert.Equal("Text is not allowed.", failure.Message);
    }
}
