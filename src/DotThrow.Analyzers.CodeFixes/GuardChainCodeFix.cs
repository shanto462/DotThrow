using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace DotThrow.Analyzers.CodeFixes;

/// <summary>
/// DT0001: removes a guard statement without checks, or uses the value directly.
/// DT0009: removes an <c>IfNull()</c> or <c>AllowNull()</c> that has no effect.
/// DT0011: checks first, then uses the original nullable value:
/// <c>int? age = x.Throw().WhenPresent().IfNegative();</c> becomes
/// <c>x.Throw().WhenPresent().IfNegative(); int? age = x;</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GuardChainCodeFix))]
[Shared]
public sealed class GuardChainCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.EmptyChain, DiagnosticIds.NullCheckOnValueType, DiagnosticIds.UnwrappedNullable);

    // Fix All applies the fixes one after the other on the same tree (neighbor statements
    // would otherwise give overlapping text changes).
    public override FixAllProvider GetFixAllProvider() =>
        FixAllProvider.Create(async (context, document, diagnostics) =>
        {
            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            var plans = new List<(Plan Plan, SyntaxAnnotation Main, SyntaxAnnotation Value)>();
            var marks = new Dictionary<SyntaxNode, List<SyntaxAnnotation>>();
            foreach (var diagnostic in diagnostics)
            {
                if (Plan.For(root, diagnostic) is not { } plan)
                {
                    continue;
                }

                var main = new SyntaxAnnotation();
                var value = new SyntaxAnnotation();
                Mark(marks, plan.Main, main);
                if (plan.Value is not null)
                {
                    Mark(marks, plan.Value, value);
                }

                plans.Add((plan, main, value));
            }

            root = root.ReplaceNodes(marks.Keys, (original, rewritten) => rewritten.WithAdditionalAnnotations(marks[original]));
            foreach (var (plan, main, value) in plans)
            {
                if (root.GetAnnotatedNodes(main).FirstOrDefault() is { } currentMain)
                {
                    root = plan.Apply(root, currentMain, root.GetAnnotatedNodes(value).FirstOrDefault()) ?? root;
                }
            }

            return document.WithSyntaxRoot(root);
        });

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (Plan.For(root, diagnostic) is not { } plan || plan.Apply(root, plan.Main, plan.Value) is not { } fixedRoot)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(plan.Title, _ => Task.FromResult(context.Document.WithSyntaxRoot(fixedRoot)), plan.Key),
                diagnostic);
        }
    }

    private static void Mark(Dictionary<SyntaxNode, List<SyntaxAnnotation>> marks, SyntaxNode node, SyntaxAnnotation annotation)
    {
        if (!marks.TryGetValue(node, out var list))
        {
            list = [];
            marks[node] = list;
        }

        list.Add(annotation);
    }

    // DT0001: "x.Throw();" or "_ = x.Throw();" as a statement of its own. In a lambda such as
    // items.ForEach(x => x.Throw()) the statement around it is something else.
    private static ExpressionStatementSyntax? OwnStatement(SyntaxNode chain)
    {
        var statement = chain.Parent switch
        {
            ExpressionStatementSyntax direct => direct,
            AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "_" }, Parent: ExpressionStatementSyntax discard } => discard,
            _ => null,
        };

        return statement?.Parent is BlockSyntax or GlobalStatementSyntax or SwitchSectionSyntax ? statement : null;
    }

    private static SyntaxNode? RemoveCall(SyntaxNode root, SyntaxNode node) =>
        node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation
            ? root.ReplaceNode(invocation, member.Expression.WithTrailingTrivia(invocation.GetTrailingTrivia()))
            : null;

    private static SyntaxNode? CheckThenUseValue(SyntaxNode root, SyntaxNode chain, SyntaxNode? value) =>
        chain is ExpressionSyntax expression
        && value is ExpressionSyntax original
        && TargetStatement(expression) is { Parent: BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax } statement
            ? CheckThenUseValue(root, statement, expression, original)
            : null;

    // What one fix needs: its nodes, found by span in the original tree, and how it changes a tree.
    private sealed class Plan
    {
        private Plan(string title, string key, SyntaxNode main, SyntaxNode? value, Func<SyntaxNode, SyntaxNode, SyntaxNode?, SyntaxNode?> apply)
        {
            Title = title;
            Key = key;
            Main = main;
            Value = value;
            Apply = apply;
        }

        public string Title { get; }

        public string Key { get; }

        public SyntaxNode Main { get; }

        public SyntaxNode? Value { get; }

        // Gets the current root, main node and value node; returns the new root, or null.
        public Func<SyntaxNode, SyntaxNode, SyntaxNode?, SyntaxNode?> Apply { get; }

        public static Plan? For(SyntaxNode root, Diagnostic diagnostic)
        {
            var properties = diagnostic.Properties;
            var value = diagnostic.AdditionalLocations.Count > 0
                ? root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true) as ExpressionSyntax
                : null;

            if (diagnostic.Id == DiagnosticIds.NullCheckOnValueType)
            {
                return properties.ContainsKey(GuardChainAnalyzer.RemoveCallProperty)
                    && root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is
                    {
                        Expression: MemberAccessExpressionSyntax member,
                    } invocation
                    ? new Plan("Remove '" + member.Name.Identifier.ValueText + "()'", DiagnosticIds.NullCheckOnValueType, invocation, null, (r, node, _) => RemoveCall(r, node))
                    : null;
            }

            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax chain)
            {
                return null;
            }

            if (diagnostic.Id == DiagnosticIds.UnwrappedNullable)
            {
                return properties.ContainsKey(GuardChainAnalyzer.SimpleValueProperty) && value is not null
                    ? new Plan("Check, then use the original value", DiagnosticIds.UnwrappedNullable, chain, value, CheckThenUseValue)
                    : null;
            }

            properties.TryGetValue(GuardChainAnalyzer.KindProperty, out var kind);
            if (kind == "Statement" && properties.ContainsKey(GuardChainAnalyzer.RemovableProperty))
            {
                return new Plan(
                    "Remove the guard without checks",
                    DiagnosticIds.EmptyChain + "Remove",
                    chain,
                    null,
                    (r, node, _) => OwnStatement(node) is { } statement ? RemoveStatement(r, statement) : null);
            }

            return kind == "Value" && value is not null
                ? new Plan(
                    "Use the value without a guard",
                    DiagnosticIds.EmptyChain + "Value",
                    chain,
                    value,
                    (r, node, currentValue) => node is ExpressionSyntax current && currentValue is ExpressionSyntax v ? r.ReplaceNode(current, Unwrap(current, v)) : null)
                : null;
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

    // The statement that only moves the chain's result: "T? x = chain;", "x = chain;" or "return chain;".
    private static StatementSyntax? TargetStatement(ExpressionSyntax chain) =>
        chain.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: LocalDeclarationStatementSyntax local } } }
                when local.UsingKeyword.IsKind(SyntaxKind.None) && !local.IsConst => local,
            AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax assignment } expression
                when expression.Right == chain && expression.IsKind(SyntaxKind.SimpleAssignmentExpression) => assignment,
            ReturnStatementSyntax returnStatement => returnStatement,
            _ => null,
        };

    // chain;            (the checks, with the comments and indentation of the statement)
    // T? x = value;     (the statement, now with the original value)
    private static SyntaxNode CheckThenUseValue(SyntaxNode root, StatementSyntax statement, ExpressionSyntax chain, ExpressionSyntax value)
    {
        var endOfLine = SyntaxHelpers.EndOfLine(root);
        var leading = statement.GetLeadingTrivia();
        var indentation = leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxFactory.TriviaList();

        var check = SyntaxFactory.ParseStatement(chain.WithoutTrivia().ToString() + ";")
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(endOfLine);
        var use = statement
            .ReplaceNode(chain, value.WithoutTrivia().WithTriviaFrom(chain))
            .WithLeadingTrivia(indentation);

        if (statement.Parent is GlobalStatementSyntax global)
        {
            return root.ReplaceNode(global, new SyntaxNode[]
            {
                SyntaxFactory.GlobalStatement(check),
                SyntaxFactory.GlobalStatement(use),
            });
        }

        return root.ReplaceNode(statement, new SyntaxNode[] { check, use });
    }
}
