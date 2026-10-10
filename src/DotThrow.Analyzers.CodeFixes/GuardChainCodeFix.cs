using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace DotThrow.Analyzers.CodeFixes;

/// <summary>
/// DT0001: removes a guard statement without checks, or uses the value directly.
/// DT0009: removes an <c>IfNull()</c> or <c>AllowNull()</c> that has no effect.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GuardChainCodeFix))]
[Shared]
public sealed class GuardChainCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.EmptyChain, DiagnosticIds.NullCheckOnValueType);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id == DiagnosticIds.NullCheckOnValueType)
            {
                RegisterRemoveCall(context, root, diagnostic);
            }
            else
            {
                RegisterEmptyChain(context, root, diagnostic);
            }
        }
    }

    private static void RegisterEmptyChain(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax chain)
        {
            return;
        }

        diagnostic.Properties.TryGetValue(GuardChainAnalyzer.KindProperty, out var kind);
        if (kind == "Statement")
        {
            // Only a statement of its own: "x.Throw();" or "_ = x.Throw();". In a lambda such as
            // items.ForEach(x => x.Throw()) the statement around it is something else.
            if (!diagnostic.Properties.ContainsKey(GuardChainAnalyzer.RemovableProperty)
                || chain.Parent switch
                {
                    ExpressionStatementSyntax direct => direct,
                    AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "_" }, Parent: ExpressionStatementSyntax discard } => discard,
                    _ => null,
                } is not { } statement
                || statement.Parent is not (BlockSyntax or GlobalStatementSyntax or SwitchSectionSyntax))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the guard without checks",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(RemoveStatement(root, statement))),
                    equivalenceKey: DiagnosticIds.EmptyChain + "Remove"),
                diagnostic);
            return;
        }

        if (kind == "Value"
            && diagnostic.AdditionalLocations.Count > 0
            && root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true) is ExpressionSyntax value)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the value without a guard",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(chain, Unwrap(chain, value)))),
                    equivalenceKey: DiagnosticIds.EmptyChain + "Value"),
                diagnostic);
        }
    }

    private static SyntaxNode RemoveStatement(SyntaxNode root, ExpressionStatementSyntax statement)
    {
        SyntaxNode node = statement.Parent is GlobalStatementSyntax global ? global : statement;
        return root.RemoveNode(node, SyntaxRemoveOptions.KeepUnbalancedDirectives) ?? root;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax chain, ExpressionSyntax value)
    {
        var replacement = value.WithoutTrivia();
        if (replacement is not (IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax or LiteralExpressionSyntax or ThisExpressionSyntax or ParenthesizedExpressionSyntax))
        {
            // The simplifier removes the parentheses again where they are not needed.
            replacement = SyntaxHelpers.Parenthesize(replacement).WithAdditionalAnnotations(Simplifier.Annotation);
        }

        return replacement.WithTriviaFrom(chain);
    }

    private static void RegisterRemoveCall(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.ContainsKey(GuardChainAnalyzer.RemoveCallProperty)
            || root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not
            {
                Expression: MemberAccessExpressionSyntax member,
            } invocation)
        {
            return;
        }

        var title = "Remove '" + member.Name.Identifier.ValueText + "()'";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                _ => Task.FromResult(context.Document.WithSyntaxRoot(
                    root.ReplaceNode(invocation, member.Expression.WithTrailingTrivia(invocation.GetTrailingTrivia())))),
                equivalenceKey: DiagnosticIds.NullCheckOnValueType),
            diagnostic);
    }
}
