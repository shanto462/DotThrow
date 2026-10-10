using System;
using System.Collections.Generic;
using DotThrow.Internal;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Messages;

/// <summary>Review fix: a bad format in a template never throws; Set rejects it up front.</summary>
[Collection(nameof(MessageTemplates))]
public sealed class TemplateFormatTests : IDisposable
{
    public TemplateFormatTests() => MessageTemplates.Reset();

    public void Dispose() => MessageTemplates.Reset();

    [Fact]
    public void A_bad_format_in_a_custom_message_is_written_as_the_placeholder()
    {
        Assert.Equal("max {ComparisonValue:Q}", Check.MessageOf(5, g => g.IfGreaterThan(3, "max {ComparisonValue:Q}")));
        Assert.Equal("{From:Q} to 2", Check.MessageOf(5, g => g.IfOutOfRange(1, 2, "{From:Q} to {To}")));
        Assert.Equal("not {ComparisonValue:Q}", Check.MessageOf(DateTime.MinValue, g => g.IfBefore(DateTime.MaxValue, "not {ComparisonValue:Q}")));
        Assert.Equal("value {PropertyValue:Q}", Check.MessageOf(Guid.Empty, g => g.IfEmpty("value {PropertyValue:Q}")));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => 5.Throw("n").IfGreaterThan(3, "max {ComparisonValue:Q}"));
        Assert.StartsWith("max {ComparisonValue:Q}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_writes_a_bad_format_as_the_placeholder()
    {
        var values = new Dictionary<string, object?> { ["Limit"] = 5, ["When"] = DateTime.MinValue };
        Assert.Equal("{Limit:Q} {When:Q} 5", MessageTemplates.Format("{Limit:Q} {When:Q} {Limit}", "n", values));
    }

    [Theory]
    [InlineData("{ComparisonValue:Q}")]
    [InlineData("'{PropertyName}' must be at most {MaxLength:K}.")]
    [InlineData("{Custom:Q}")]
    public void Set_rejects_a_format_no_value_can_use(string template)
    {
        var exception = Assert.Throws<ArgumentException>(() => MessageTemplates.Set(ErrorCodes.LongerThan, template));
        Assert.Equal("template", exception.ParamName);
        Assert.Contains("is not valid", exception.Message, StringComparison.Ordinal);
        Assert.False(MessageTemplate.TryValidate(template, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("'{PropertyName}' must be at most {MaxLength:N0} characters.")]
    [InlineData("{From:yyyy-MM-dd} {To:c} {Value:X}")]
    [InlineData("{PropertyName:Q} {Unknown} { not a placeholder")]
    [InlineData("plain text")]
    public void Valid_templates_are_accepted(string template)
    {
        Assert.True(MessageTemplate.TryValidate(template, out var error));
        Assert.Null(error);
        MessageTemplates.Set(ErrorCodes.LongerThan, template);
    }
}
