using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotThrow.Analyzers.CodeFixes;

/// <summary>DT0003: removes the name argument that the compiler fills in.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantNameCodeFix))]
[Shared]
public sealed class RedundantNameCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.RedundantName);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>() is not
                {
                    Parent: ArgumentListSyntax list,
                } argument)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the name argument",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(
                        root.ReplaceNode(list, list.WithArguments(list.Arguments.Remove(argument))))),
                    equivalenceKey: DiagnosticIds.RedundantName),
                diagnostic);
        }
    }
}

/// <summary>DT0008: uses <c>IfInPast()</c> or <c>IfInFuture()</c> instead of a comparison with the current time.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CurrentTimeCodeFix))]
[Shared]
public sealed class CurrentTimeCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.UseCurrentTimeCheck);

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
            if (!diagnostic.Properties.TryGetValue(CheckArgumentAnalyzer.ReplacementProperty, out var replacement)
                || replacement is null
                || root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
                || LimitArgument(invocation.ArgumentList) is not { } limit)
            {
                continue;
            }

            ExpressionSyntax? timeProvider = null;
            if (diagnostic.AdditionalLocations.Count > 0)
            {
                timeProvider = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true) as ExpressionSyntax;
                if (timeProvider is null)
                {
                    continue;
                }
            }

            var arguments = timeProvider is null
                ? invocation.ArgumentList.Arguments.Remove(limit)
                : invocation.ArgumentList.Arguments.Replace(limit, SyntaxFactory.Argument(timeProvider.WithoutTrivia()).WithTriviaFrom(limit));
            var fixedInvocation = SyntaxHelpers.WithMethodName(invocation, replacement)
                .WithArgumentList(invocation.ArgumentList.WithArguments(arguments));

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use '" + replacement + "'",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, fixedInvocation))),
                    equivalenceKey: DiagnosticIds.UseCurrentTimeCheck),
                diagnostic);
        }
    }

    // The limit is the first positional argument, or the one named "limit".
    private static ArgumentSyntax? LimitArgument(ArgumentListSyntax list)
    {
        foreach (var argument in list.Arguments)
        {
            if (argument.NameColon?.Name.Identifier.ValueText == "limit")
            {
                return argument;
            }
        }

        return list.Arguments.Count > 0 && list.Arguments[0].NameColon is null ? list.Arguments[0] : null;
    }
}

/// <summary>DT0010: calls <c>ValidateAndThrow</c> (or <c>ValidateAndThrowAsync</c>) when the report is not used.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnusedValidationResultCodeFix))]
[Shared]
public sealed class UnusedValidationResultCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.UnusedValidationResult);

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
            if (!diagnostic.Properties.TryGetValue(ValidatorUsageAnalyzer.ReplacementProperty, out var replacement)
                || replacement is null
                || root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Call '" + replacement + "'",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(
                        root.ReplaceNode(invocation, SyntaxHelpers.WithMethodName(invocation, replacement)))),
                    equivalenceKey: DiagnosticIds.UnusedValidationResult),
                diagnostic);
        }
    }
}
