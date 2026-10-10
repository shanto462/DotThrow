using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DotThrow.Analyzers.Tests.Infrastructure;

/// <summary>The project the test code compiles in.</summary>
public enum TestTarget
{
    /// <summary>.NET 10 with the net10.0 build of DotThrow.</summary>
    Net,

    /// <summary>netstandard2.0 with the netstandard2.0 build of DotThrow.</summary>
    NetStandard20,

    /// <summary>.NET 10 without DotThrow.</summary>
    NetWithoutDotThrow,
}

/// <summary>An analyzer test against the real DotThrow assembly.</summary>
public sealed class DotThrowAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    private TestTarget _target;

    public DotThrowAnalyzerTest() => TestSetup.Configure(this, TestTarget.Net);

    public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.CSharp12;

    public TestTarget Target
    {
        get => _target;
        set
        {
            _target = value;
            TestSetup.Configure(this, value);
        }
    }

    protected override ParseOptions CreateParseOptions() =>
        ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);

    protected override CompilationOptions CreateCompilationOptions() =>
        ((CSharpCompilationOptions)base.CreateCompilationOptions()).WithNullableContextOptions(NullableContextOptions.Enable);
}

/// <summary>A code fix test against the real DotThrow assembly. It also runs Fix All.</summary>
public sealed class DotThrowCodeFixTest<TAnalyzer, TCodeFix> : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public DotThrowCodeFixTest()
    {
        TestSetup.Configure(this, TestTarget.Net);

        // Diagnostics left in the fixed code (no fix offered for them) are written as markup there.
        FixedState.MarkupHandling = MarkupMode.Allow;
    }

    public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.CSharp12;

    protected override ParseOptions CreateParseOptions() =>
        ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);

    protected override CompilationOptions CreateCompilationOptions() =>
        ((CSharpCompilationOptions)base.CreateCompilationOptions()).WithNullableContextOptions(NullableContextOptions.Enable);
}

/// <summary>Short entry points for the tests.</summary>
public static class Verify
{
    public static DiagnosticResult Diagnostic<TAnalyzer>(string id)
        where TAnalyzer : DiagnosticAnalyzer, new() =>
        CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(id);

    public static Task AnalyzerAsync<TAnalyzer>(string source, params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new DotThrowAnalyzerTest<TAnalyzer> { TestCode = source };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    public static Task CodeFixAsync<TAnalyzer, TCodeFix>(string source, string fixedSource, params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new() =>
        CodeFixAsync(() =>
        {
            var test = new DotThrowCodeFixTest<TAnalyzer, TCodeFix> { TestCode = source, FixedCode = fixedSource };
            test.ExpectedDiagnostics.AddRange(expected);
            return test;
        });

    /// <summary>
    /// Runs a code fix test once per line ending: an LF document where the formatting default is
    /// CRLF (a Windows machine), and a CRLF document where the default is LF. The fixed code must
    /// keep the document's own line ending everywhere, including the lines a fix adds.
    /// </summary>
    public static async Task CodeFixAsync<TAnalyzer, TCodeFix>(Func<DotThrowCodeFixTest<TAnalyzer, TCodeFix>> create)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        foreach (var (document, formatting) in new[] { ("\n", "crlf"), ("\r\n", "lf") })
        {
            var test = create();
            UseLineEnding(test.TestState, document);
            UseLineEnding(test.FixedState, document);
            UseLineEnding(test.BatchFixedState, document);
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", "root = true" + document + document + "[*]" + document + "end_of_line = " + formatting + document));
            await test.RunAsync(TestContext.Current.CancellationToken);
        }
    }

    private static void UseLineEnding(SolutionState state, string endOfLine)
    {
        var sources = state.Sources.ToList();
        state.Sources.Clear();
        foreach (var (name, text) in sources)
        {
            var content = text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            if (endOfLine != "\n")
            {
                content = content.Replace("\n", endOfLine, StringComparison.Ordinal);
            }

            state.Sources.Add((name, SourceText.From(content, text.Encoding, text.ChecksumAlgorithm)));
        }
    }
}

internal static class TestSetup
{
    public static void Configure<TVerifier>(AnalyzerTest<TVerifier> test, TestTarget target)
        where TVerifier : IVerifier, new()
    {
        // No package download: the references come from disk (see TestReferences).
        test.ReferenceAssemblies = new ReferenceAssemblies(target == TestTarget.NetStandard20 ? "netstandard2.0" : "net10.0");
        test.TestState.AdditionalReferences.Clear();
        test.TestState.AdditionalReferences.AddRange(target switch
        {
            TestTarget.NetStandard20 => TestReferences.NetStandard20,
            TestTarget.NetWithoutDotThrow => TestReferences.Framework,
            _ => TestReferences.Net,
        });
    }
}
