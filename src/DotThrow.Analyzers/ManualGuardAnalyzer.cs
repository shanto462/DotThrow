using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace DotThrow.Analyzers;

/// <summary>
/// DT0002: an <c>if (...) throw new ArgumentException(...)</c> statement, or a
/// <c>x ?? throw new ArgumentNullException(...)</c> expression, that a DotThrow check replaces,
/// such as <c>x.Throw().IfNull()</c> or <c>count.Throw().IfOutOfRange(1, 10)</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualGuardAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.ManualGuard);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (DotThrowSymbols.Create(start.Compilation) is { } symbols)
            {
                start.RegisterOperationAction(operation => Analyze(operation, symbols), OperationKind.Conditional);
                start.RegisterOperationAction(operation => AnalyzeCoalesce(operation, symbols), OperationKind.Coalesce);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, DotThrowSymbols symbols)
    {
        var conditional = (IConditionalOperation)context.Operation;
        if (conditional.Syntax is not IfStatementSyntax ifStatement
            || ManualGuardMatcher.Match(conditional) is not { } match
            || symbols.GuardExtensions.GetMembers(match.Check).IsEmpty)
        {
            return;
        }

        var span = TextSpan.FromBounds(ifStatement.IfKeyword.SpanStart, ifStatement.CloseParenToken.Span.End);
        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.ManualGuard,
            Location.Create(ifStatement.SyntaxTree, span),
            match.Display()));
    }

    // name ?? throw new ArgumentNullException(nameof(name))
    private static void AnalyzeCoalesce(OperationAnalysisContext context, DotThrowSymbols symbols)
    {
        var coalesce = (ICoalesceOperation)context.Operation;
        if (ManualGuardMatcher.MatchCoalesce(coalesce) is not { } match
            || symbols.GuardExtensions.GetMembers(match.Check).IsEmpty)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Descriptors.ManualGuard, coalesce.Syntax.GetLocation(), match.Display()));
    }
}
