using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers.CodeFixes;

/// <summary>DT0002: replaces a manual guard with the DotThrow check, and adds <c>using DotThrow;</c>.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualGuardCodeFix))]
[Shared]
public sealed class ManualGuardCodeFix : CodeFixProvider
{
    private const string Title = "Use a DotThrow check";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.ManualGuard);

    public override FixAllProvider GetFixAllProvider() =>
        FixAllProvider.Create(async (context, document, diagnostics) =>
            await FixAsync(document, diagnostics, context.CancellationToken).ConfigureAwait(false));

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    token => FixAsync(context.Document, ImmutableArray.Create(diagnostic), token),
                    equivalenceKey: DiagnosticIds.ManualGuard),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    private static async Task<Document> FixAsync(Document document, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit || model is null)
        {
            return document;
        }

        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();
        var needsUsing = false;
        foreach (var diagnostic in diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is BinaryExpressionSyntax coalesce && coalesce.IsKind(SyntaxKind.CoalesceExpression))
            {
                // x ?? throw new ArgumentNullException(...) becomes x.Throw().IfNull()
                if (model.GetOperation(coalesce, cancellationToken) is not ICoalesceOperation operation
                    || ManualGuardMatcher.MatchCoalesce(operation) is not { } coalesceMatch)
                {
                    continue;
                }

                replacements[coalesce] = SyntaxFactory.ParseExpression(Chain(coalesceMatch)).WithTriviaFrom(coalesce);
                needsUsing |= !ThrowIsInScope(model, coalesce.SpanStart, coalesceMatch.Value.Type);
                continue;
            }

            if (node.FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
                || model.GetOperation(ifStatement, cancellationToken) is not IConditionalOperation conditional
                || ManualGuardMatcher.Match(conditional) is not { } match)
            {
                continue;
            }

            replacements[ifStatement] = Replacement(ifStatement, match);
            needsUsing |= !ThrowIsInScope(model, ifStatement.SpanStart, match.Value.Type);
        }

        if (replacements.Count == 0)
        {
            return document;
        }

        var updated = compilationUnit.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        if (needsUsing)
        {
            updated = SyntaxHelpers.AddUsing(updated, "DotThrow");
        }

        return document.WithSyntaxRoot(updated);
    }

    // x.Throw().IfNull(); or x.Throw("name").IfOutOfRange(min, max, "message");
    // or x = x.Throw().IfNull(); when nullable analysis must learn that x is not null.
    // Parsed from text, so it has no elastic trivia for the formatter to fill in.
    private static StatementSyntax Replacement(IfStatementSyntax ifStatement, ManualGuardMatch match)
    {
        var expression = Chain(match);
        if (match.Assign)
        {
            expression = Text(match.Value) + " = " + expression;
        }

        return SyntaxFactory.ParseStatement(expression + ";")
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());
    }

    private static string Chain(ManualGuardMatch match)
    {
        var name = match.Name is not null
            ? Text(match.Name)
            : match.LiteralName is { } literal
                ? SyntaxFactory.Literal(literal).Text
                : string.Empty;

        var arguments = match.CheckArguments.Select(Text).ToList();
        if (match.Message is not null)
        {
            arguments.Add(Text(match.Message));
        }

        return Text(match.Value) + ".Throw(" + name + ")." + match.Check + "(" + string.Join(", ", arguments) + ")";
    }

    // The source text of an expression, without its outer trivia.
    private static string Text(IOperation operation) =>
        (operation.ExpressionSyntax() ?? (SyntaxNode)operation.Syntax).WithoutTrivia().ToString();

    // Throw() is in scope when DotThrow is imported (or the code is in the DotThrow namespace).
    private static bool ThrowIsInScope(SemanticModel model, int position, ITypeSymbol? type)
    {
        var candidates = type is null
            ? model.LookupSymbols(position, name: "Throw")
            : model.LookupSymbols(position, type, "Throw", includeReducedExtensionMethods: true);
        foreach (var candidate in candidates)
        {
            if (candidate is IMethodSymbol method && (method.ReducedFrom ?? method).ContainingType?.ToDisplayString() == "DotThrow.ThrowExtensions")
            {
                return true;
            }
        }

        return false;
    }
}
