using System;
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
/// DT0006: moves a constant pattern into a <c>[GeneratedRegex]</c> method of the containing type
/// and passes the generated <c>Regex</c> to the check. The type becomes partial.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexCodeFix))]
[Shared]
public sealed class GeneratedRegexCodeFix : CodeFixProvider
{
    private const string RegexNamespace = "System.Text.RegularExpressions";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.UseGeneratedRegex);

    public override FixAllProvider GetFixAllProvider() =>
        FixAllProvider.Create(async (context, document, diagnostics) =>
            await FixAsync(document, diagnostics, context.CancellationToken).ConfigureAwait(false));

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (model is null || Target(root, diagnostic, model, context.CancellationToken) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use a [GeneratedRegex] method",
                    token => FixAsync(context.Document, ImmutableArray.Create(diagnostic), token),
                    equivalenceKey: DiagnosticIds.UseGeneratedRegex),
                diagnostic);
        }
    }

    // The pattern argument, its call and the type that gets the method, or null. The call must
    // be the extension form (guard.IfMatches(...)), and the pattern and options must not read
    // locals, which the attribute on the new method cannot see.
    private static (ArgumentSyntax Pattern, InvocationExpressionSyntax Invocation, TypeDeclarationSyntax Type)? Target(
        SyntaxNode root,
        Diagnostic diagnostic,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>() is not
            {
                Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation },
            } pattern
            || invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type
            || type is InterfaceDeclarationSyntax
            || model.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation
            || operation.Arguments.Length == 0
            || operation.Arguments[0].Syntax is ArgumentSyntax)
        {
            return null;
        }

        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter?.Name is "pattern" or "options"
                && argument.Value.DescendantsAndSelf().Any(value => value is ILocalReferenceOperation))
            {
                return null;
            }
        }

        return (pattern, invocation, type);
    }

    private static async Task<Document> FixAsync(Document document, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax || model is null)
        {
            return document;
        }

        // Mark every call and its type, so they can be found again after each edit.
        var targets = new List<(SyntaxAnnotation Call, SyntaxAnnotation Pattern, SyntaxAnnotation Type)>();
        var marks = new Dictionary<SyntaxNode, SyntaxAnnotation>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var position = -1;
        foreach (var diagnostic in diagnostics.OrderBy(d => d.Location.SourceSpan.Start))
        {
            if (Target(root, diagnostic, model, cancellationToken) is not { } target || marks.ContainsKey(target.Invocation) || marks.ContainsKey(target.Pattern))
            {
                continue;
            }

            if (!marks.TryGetValue(target.Type, out var typeMark))
            {
                typeMark = new SyntaxAnnotation();
                marks[target.Type] = typeMark;
                if (model.GetDeclaredSymbol(target.Type, cancellationToken) is { } symbol)
                {
                    usedNames.UnionWith(symbol.MemberNames);
                }
            }

            var callMark = new SyntaxAnnotation();
            var patternMark = new SyntaxAnnotation();
            marks[target.Invocation] = callMark;
            marks[target.Pattern] = patternMark;
            targets.Add((callMark, patternMark, typeMark));
            position = position < 0 ? target.Invocation.SpanStart : position;
        }

        if (targets.Count == 0)
        {
            return document;
        }

        root = root.ReplaceNodes(marks.Keys, (original, rewritten) => rewritten.WithAdditionalAnnotations(marks[original]));
        var endOfLine = SyntaxHelpers.EndOfLine(root);
        foreach (var (callMark, patternMark, typeMark) in targets)
        {
            var type = root.GetAnnotatedNodes(typeMark).OfType<TypeDeclarationSyntax>().First();
            var invocation = type.GetAnnotatedNodes(callMark).OfType<InvocationExpressionSyntax>().First();
            var pattern = invocation.GetAnnotatedNodes(patternMark).OfType<ArgumentSyntax>().First();
            var name = UniqueName(MethodNameFor(invocation), usedNames);

            var (options, message) = OtherArguments(invocation.ArgumentList, pattern);
            var regexCall = SyntaxFactory.ParseExpression(name + "()");
            var arguments = new List<ArgumentSyntax> { SyntaxFactory.Argument(regexCall).WithTriviaFrom(pattern) };
            if (message is not null)
            {
                arguments.Add(message.WithoutTrivia());
            }

            var newInvocation = invocation.WithArgumentList(SyntaxHelpers.Arguments(arguments).WithTriviaFrom(invocation.ArgumentList));

            // Written out with the document's own end of line and the indentation of the
            // members, so no formatter runs (it would use the machine's default end of line).
            var eol = endOfLine.ToFullString();
            var indentation = MemberIndentation(type);
            var attributeArguments = pattern.Expression.WithoutTrivia().ToString()
                + (options is null ? string.Empty : ", " + options.Expression.WithoutTrivia());
            var method = SyntaxFactory.ParseMemberDeclaration(
                "[GeneratedRegex(" + attributeArguments + ")]" + eol
                + indentation + "private static partial Regex " + name + "();" + eol)!;
            method = method.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(eol + indentation));

            var newType = type.ReplaceNode(invocation, newInvocation).AddMembers(method);
            root = root.ReplaceNode(type, newType);
            root = MakePartial(root, typeMark);
        }

        if (!RegexIsInScope(model, position))
        {
            root = SyntaxHelpers.AddUsing((CompilationUnitSyntax)root, RegexNamespace);
        }

        return document.WithSyntaxRoot(root);
    }

    // The options and message arguments of IfMatches(pattern, options, message).
    private static (ArgumentSyntax? Options, ArgumentSyntax? Message) OtherArguments(ArgumentListSyntax list, ArgumentSyntax pattern)
    {
        ArgumentSyntax? options = null;
        ArgumentSyntax? message = null;
        for (var i = 0; i < list.Arguments.Count; i++)
        {
            var argument = list.Arguments[i];
            if (argument == pattern)
            {
                continue;
            }

            switch (argument.NameColon?.Name.Identifier.ValueText)
            {
                case "options":
                    options = argument;
                    break;
                case "message":
                    message = argument;
                    break;
                case null when i == 1:
                    options = argument;
                    break;
                case null when i == 2:
                    message = argument;
                    break;
            }
        }

        return (options, message);
    }

    private static SyntaxNode MakePartial(SyntaxNode root, SyntaxAnnotation typeMark)
    {
        while (true)
        {
            var type = root.GetAnnotatedNodes(typeMark).OfType<TypeDeclarationSyntax>().First();
            var missing = type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault(t => !t.Modifiers.Any(SyntaxKind.PartialKeyword));
            if (missing is null)
            {
                return root;
            }

            root = root.ReplaceNode(missing, AddPartial(missing));
        }
    }

    // The whitespace before the members of a type: the first member's, or one level deeper
    // than the closing brace.
    private static string MemberIndentation(TypeDeclarationSyntax type)
    {
        if (type.Members.Count > 0)
        {
            var leading = type.Members[0].GetLeadingTrivia();
            return leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
                ? leading[leading.Count - 1].ToFullString()
                : string.Empty;
        }

        var brace = type.CloseBraceToken.LeadingTrivia;
        var outer = brace.Count > 0 && brace[brace.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia) ? brace[brace.Count - 1].ToFullString() : string.Empty;
        return outer + "    ";
    }

    // "partial" goes right before the class, struct or record keyword.
    private static TypeDeclarationSyntax AddPartial(TypeDeclarationSyntax type)
    {
        var partial = SyntaxHelpers.Plain(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (type.Modifiers.Count > 0)
        {
            return type.AddModifiers(partial);
        }

        partial = partial.WithLeadingTrivia(type.Keyword.LeadingTrivia);
        return type.WithKeyword(type.Keyword.WithLeadingTrivia()).WithModifiers(SyntaxFactory.TokenList(partial));
    }

    // A name from the checked value, for example "PostalCodeRegex" for x.PostalCode.
    private static string MethodNameFor(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression : null;
        while (expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax inner } call)
        {
            var method = inner.Name.Identifier.ValueText;
            if (method == "Throw")
            {
                expression = inner.Expression;
                break;
            }

            if (method == "Check")
            {
                var arguments = call.ArgumentList.Arguments;
                expression = arguments.Count switch
                {
                    1 => arguments[0].Expression,
                    > 1 when arguments[1].Expression is SimpleLambdaExpressionSyntax { ExpressionBody: { } body } => body,
                    > 0 => arguments[0].Expression,
                    _ => null,
                };
                break;
            }

            expression = inner.Expression;
        }

        var text = expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };

        text = text?.TrimStart('_', '@');
        return string.IsNullOrEmpty(text) ? "Pattern" : char.ToUpperInvariant(text![0]) + text.Substring(1) + "Regex";
    }

    private static string UniqueName(string name, HashSet<string> usedNames)
    {
        var candidate = name;
        for (var i = 2; !usedNames.Add(candidate); i++)
        {
            candidate = name + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static bool RegexIsInScope(SemanticModel model, int position)
    {
        foreach (var symbol in model.LookupNamespacesAndTypes(position, name: "Regex"))
        {
            if (symbol.ContainingNamespace?.ToDisplayString() == RegexNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
