using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;
using Xunit;

namespace DotThrow.Tests.Messages;

// The suite pins the culture to en-US (UseEnglishCultureAttribute), so English message
// assertions pass on any machine. These tests prove the pin, and check that default messages
// follow the UI culture once the built-in translations exist.
public sealed class CultureTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tests_run_with_the_english_culture_everywhere()
    {
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);

        await Task.Yield();
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);

        var inTask = await Task.Run(() => CultureInfo.CurrentUICulture.Name, Token);
        Assert.Equal("en-US", inTask);
        Assert.Equal("'name' must not be null.", MessageOf(null));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("bn")]
    public void Default_messages_follow_the_ui_culture(string cultureName)
    {
        Assert.SkipWhen(
            Translations.Find(cultureName) is null,
            $"No built-in '{cultureName}' translation yet; this test runs once the translations (spec H) are merged.");
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

            var message = MessageOf(null);

            Assert.NotEqual("'name' must not be null.", message);
            Assert.Contains("name", message, StringComparison.Ordinal);
            Assert.Equal(MessageTemplates.Format(Translations.Find(cultureName)![ErrorCodes.Null], "name"), message);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }

        Assert.Equal("'name' must not be null.", MessageOf(null));
    }

    private static string MessageOf(string? name)
    {
        var report = new ValidationReport();
        report.Check(name).IfNull();
        return Assert.Single(report.Failures).Message;
    }
}
