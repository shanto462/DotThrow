using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>
/// DT0003: a name passed by hand to a DotThrow parameter that the compiler fills in with
/// [CallerArgumentExpression], when the text is the same, for example <c>x.Throw(nameof(x))</c>.
/// A name that differs on purpose, such as <c>request.Email.Throw("Email")</c>, is left alone.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantNameAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.RedundantName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (DotThrowSymbols.Create(start.Compilation) is { } symbols)
            {
                var assembly = symbols.ThrowExtensions.ContainingAssembly;
                start.RegisterOperationAction(operation => Analyze(operation, assembly), OperationKind.Invocation);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, IAssemblySymbol dotThrow)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingAssembly, dotThrow)
            || !invocation.Syntax.SyntaxTree.SupportsCallerArgumentExpression())
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.ArgumentKind != ArgumentKind.Explicit
                || argument.Parameter?.CallerArgumentExpressionTarget() is not { } target
                || argument.Value.ConstantValue is not { HasValue: true, Value: string name }
                || invocation.Argument(target) is not { } source
                || source.CallerArgumentText() != name)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Descriptors.RedundantName, argument.Syntax.GetLocation(), name));
        }
    }
}
