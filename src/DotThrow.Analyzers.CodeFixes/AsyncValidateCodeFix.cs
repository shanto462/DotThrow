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

/// <summary>
/// DT0004: awaits the async method instead, for example <c>await validator.ValidateAsync(order)</c>,
/// when the code is in an async method, lambda or local function.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncValidateCodeFix))]
[Shared]
public sealed class AsyncValidateCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.AsyncRulesRunSynchronously);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(ValidatorUsageAnalyzer.ReplacementProperty, out var asyncMethod)
                || asyncMethod is null
                || root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
                || !SyntaxHelpers.CanAwait(invocation)
                || Rewrite(invocation, model, asyncMethod, context.CancellationToken) is not { } replacement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Await '" + asyncMethod + "'",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement))),
                    equivalenceKey: DiagnosticIds.AsyncRulesRunSynchronously),
                diagnostic);
        }
    }

    private static ExpressionSyntax? Rewrite(InvocationExpressionSyntax invocation, SemanticModel model, string asyncMethod, CancellationToken cancellationToken)
    {
        // Not for named arguments, or the static form ValidatorExtensions.Validate(validator, value).
        if (model.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation
            || invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null)
            || (operation.TargetMethod.IsExtensionMethod && operation.Arguments[0].Syntax is ArgumentSyntax))
        {
            return null;
        }

        var arguments = new List<ArgumentSyntax>();
        var groups = new List<ExpressionSyntax>();
        foreach (var argument in operation.Arguments)
        {
            if (argument.ArgumentKind == ArgumentKind.DefaultValue || (operation.TargetMethod.IsExtensionMethod && argument.Parameter?.Ordinal == 0))
            {
                continue;
            }

            if (argument.ArgumentKind == ArgumentKind.ParamArray)
            {
                if (argument.Value is not IArrayCreationOperation { Initializer: { } initializer })
                {
                    return null;
                }

                groups.AddRange(initializer.ElementValues.Select(element => (ExpressionSyntax)element.Syntax));
                continue;
            }

            if (argument.Syntax is not ArgumentSyntax syntax)
            {
                return null;
            }

            arguments.Add(syntax.WithoutTrivia());
        }

        // Groups: ValidateAsync takes one group as a string, or a sequence; the others take a sequence.
        if (groups.Count > 0)
        {
            if (asyncMethod == "TestValidateAsync")
            {
                return null;
            }

            arguments.Add(groups.Count == 1 && asyncMethod == "ValidateAsync"
                ? SyntaxFactory.Argument(groups[0].WithoutTrivia())
                : SyntaxFactory.Argument(StringArray(groups)));
        }

        var renamed = SyntaxHelpers.WithMethodName(invocation, asyncMethod)
            .WithArgumentList(SyntaxHelpers.Arguments(arguments).WithTriviaFrom(invocation.ArgumentList))
            .WithoutTrivia();

        ExpressionSyntax awaited = SyntaxFactory.AwaitExpression(
            SyntaxHelpers.Plain(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            renamed);
        if (NeedsParentheses(invocation))
        {
            awaited = SyntaxHelpers.Parenthesize(awaited);
        }

        return awaited.WithTriviaFrom(invocation);
    }

    // await binds looser than member access, element access, calls and postfix operators.
    private static bool NeedsParentheses(InvocationExpressionSyntax invocation) =>
        invocation.Parent switch
        {
            MemberAccessExpressionSyntax member => member.Expression == invocation,
            ConditionalAccessExpressionSyntax conditional => conditional.Expression == invocation,
            ElementAccessExpressionSyntax element => element.Expression == invocation,
            InvocationExpressionSyntax call => call.Expression == invocation,
            PostfixUnaryExpressionSyntax => true,
            _ => false,
        };

    private static ExpressionSyntax StringArray(List<ExpressionSyntax> items) =>
        SyntaxFactory.ParseExpression("new[] { " + string.Join(", ", items.Select(item => item.WithoutTrivia().ToString())) + " }");
}
