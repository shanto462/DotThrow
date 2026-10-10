using System;
using System.Globalization;
using System.Reflection;
using Xunit.v3;

[assembly: DotThrow.Tests.Support.UseEnglishCulture]

namespace DotThrow.Tests.Support;

/// <summary>
/// Runs every test with the en-US culture and UI culture. Default messages follow
/// <see cref="CultureInfo.CurrentUICulture"/> (when <see cref="MessageTemplates.Culture"/> is
/// null), so without this the English message assertions would fail on a German or Bengali
/// machine. The culture flows into awaits, tasks and threads the test starts. A test that needs
/// another culture sets it itself and restores it.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class UseEnglishCultureAttribute : BeforeAfterTestAttribute
{
    public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        CultureInfo.CurrentCulture = English;
        CultureInfo.CurrentUICulture = English;
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
    }
}
