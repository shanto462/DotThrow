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
using Microsoft.CodeAnalysis.Formatting;
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

                replacements[coalesce] = Chain(coalesceMatch).WithTriviaFrom(coalesce).WithAdditionalAnnotations(Formatter.Annotation);
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
    private static ExpressionStatementSyntax Replacement(IfStatementSyntax ifStatement, ManualGuardMatch match)
    {
        ExpressionSyntax expression = Chain(match);
        if (match.Assign)
        {
            expression = SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Expression(match.Value), expression);
        }

        return SyntaxFactory.ExpressionStatement(expression)
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static InvocationExpressionSyntax Chain(ManualGuardMatch match)
    {
        var value = Expression(match.Value);
        var throwArguments = new List<ArgumentSyntax>();
        if (match.Name is not null)
        {
            throwArguments.Add(SyntaxFactory.Argument(Expression(match.Name)));
        }
        else if (match.LiteralName is { } literal)
        {
            throwArguments.Add(SyntaxFactory.Argument(
                SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(literal))));
        }

        var checkArguments = match.CheckArguments.Select(argument => SyntaxFactory.Argument(Expression(argument))).ToList();
        if (match.Message is not null)
        {
            checkArguments.Add(SyntaxFactory.Argument(Expression(match.Message)));
        }

        var throwCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, value, SyntaxFactory.IdentifierName("Throw")),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(throwArguments)));
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, throwCall, SyntaxFactory.IdentifierName(match.Check)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(checkArguments)));
    }

    private static ExpressionSyntax Expression(IOperation operation) =>
        (operation.ExpressionSyntax() ?? SyntaxFactory.ParseExpression(operation.Syntax.ToString())).WithoutTrivia();

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
