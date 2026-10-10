using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>
/// DT0005: a constant pattern in <c>IfMatches</c> or <c>IfNotMatches</c> that does not parse.
/// DT0006: a constant pattern that the regular expression source generator can compile
/// (.NET 7 and later, C# 11 and later).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegexPatternAnalyzer : DiagnosticAnalyzer
{
    // RegexOptions.NonBacktracking (.NET 7). The generator does not compile it into code.
    private const int NonBacktracking = 1024;

    // Every option the regex engine of this process knows. An option it does not know (an
    // older host) cannot be checked here, so such a pattern is not reported.
    private static readonly int KnownOptions = KnownOptionBits();

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.InvalidPattern, Descriptors.UseGeneratedRegex);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (DotThrowSymbols.Create(start.Compilation) is not { } symbols)
            {
                return;
            }

            var generator = start.Compilation.GetTypeByMetadataName("System.Text.RegularExpressions.GeneratedRegexAttribute") is not null;

            // The same pattern often appears many times; parse it once per compilation.
            var errors = new ConcurrentDictionary<(string Pattern, int Options), string>();
            start.RegisterOperationAction(operation => Analyze(operation, symbols, generator, errors), OperationKind.Invocation);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        DotThrowSymbols symbols,
        bool generator,
        ConcurrentDictionary<(string Pattern, int Options), string> errors)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name is not ("IfMatches" or "IfNotMatches")
            || !symbols.IsCheck(invocation.TargetMethod)
            || invocation.Argument("pattern") is not { } patternArgument
            || patternArgument.Value.ConstantValue is not { HasValue: true, Value: string pattern })
        {
            return;
        }

        var options = 0;
        if (invocation.Argument("options") is { } optionsArgument)
        {
            if (optionsArgument.Value.ConstantValue is not { HasValue: true, Value: int value })
            {
                return;
            }

            options = value;
        }

        if ((options & ~KnownOptions) != 0)
        {
            return;
        }

        var location = patternArgument.Value.Syntax.GetLocation();
        var error = errors.GetOrAdd((pattern, options), key => ParseError(key.Pattern, (RegexOptions)key.Options) ?? string.Empty);
        if (error.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.InvalidPattern, location, error));
            return;
        }

        if (generator
            && (options & NonBacktracking) == 0
            && invocation.Syntax.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp11 })
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.UseGeneratedRegex, location));
        }
    }

    // Builds the Regex the check would build at run time (without compiling it to IL) and
    // returns the error message, or null when the pattern is valid.
    private static string? ParseError(string pattern, RegexOptions options)
    {
        try
        {
            _ = new Regex(pattern, options & ~RegexOptions.Compiled);
            return null;
        }
        catch (ArgumentException exception)
        {
            return MessageOf(exception);
        }
        catch (NotSupportedException exception)
        {
            // NonBacktracking does not support some constructs, such as backreferences.
            return MessageOf(exception);
        }
    }

    private static string MessageOf(Exception exception) =>
        string.IsNullOrEmpty(exception.Message) ? exception.GetType().Name : exception.Message;

    private static int KnownOptionBits()
    {
        var bits = 0;
        foreach (var value in Enum.GetValues(typeof(RegexOptions)))
        {
            bits |= (int)value;
        }

        return bits;
    }
}
