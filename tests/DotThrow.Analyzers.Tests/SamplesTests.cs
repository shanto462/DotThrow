using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotThrow.Analyzers.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace DotThrow.Analyzers.Tests;

/// <summary>
/// The analyzers run on the repository's samples, which show idiomatic DotThrow code. Only the
/// diagnostics listed here are expected; anything else is noise on good code.
/// </summary>
public sealed class SamplesTests
{
    // Id, file and the text under the diagnostic.
    private static readonly string[] Expected =
    [
        // Section 9 shows on purpose that Validate throws for a validator with async rules.
        "DT0004 Program.cs Validate",
        // A constant pattern: the generated regular expression suggestion.
        "DT0006 Validators.cs \"^[0-9]{5}$\"",
    ];

    [Fact]
    public async Task Samples_get_only_the_intended_diagnostics()
    {
        var directory = Path.Combine(TestReferences.RepositoryRoot, "samples", "DotThrow.Samples");
        var separator = Path.DirectorySeparatorChar;
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var trees = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(separator + "bin" + separator, StringComparison.Ordinal)
                && !path.Contains(separator + "obj" + separator, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path))
            .ToList();
        Assert.NotEmpty(trees);

        var compilation = CSharpCompilation.Create(
            "DotThrow.Samples",
            trees,
            TestReferences.Net,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToList();
        Assert.True(errors.Count == 0, "The samples do not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));

        var analyzers = typeof(GuardChainAnalyzer).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(DiagnosticAnalyzer)) && !type.IsAbstract)
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();
        Assert.Equal(6, analyzers.Length);

        var diagnostics = await compilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        var actual = diagnostics
            .Select(diagnostic => diagnostic.Id + " " + Path.GetFileName(diagnostic.Location.SourceTree!.FilePath) + " "
                + diagnostic.Location.SourceTree.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Expected, actual);
    }
}
