using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotThrow.Analyzers.CodeFixes;

/// <summary>Syntax helpers shared by the code fixes.</summary>
internal static class SyntaxHelpers
{
    /// <summary>
    /// Adds <c>using <paramref name="name"/>;</c> next to the other using directives, in sorted
    /// order with System directives first, unless it is already there.
    /// </summary>
    public static CompilationUnitSyntax AddUsing(CompilationUnitSyntax root, string name)
    {
        if (root.Usings.Any(u => IsPlainUsing(u) && u.Name!.ToString() == name))
        {
            return root;
        }

        var endOfLine = EndOfLine(root);
        var directive = SyntaxFactory.ParseCompilationUnit("using " + name + ";" + endOfLine.ToFullString()).Usings[0];

        // Usings inside a namespace: add it there.
        if (root.Usings.Count == 0
            && root.Members.Count == 1
            && root.Members[0] is BaseNamespaceDeclarationSyntax { Usings.Count: > 0 } scoped)
        {
            var inner = scoped.WithUsings(Insert(scoped.Usings, directive, name));
            return root.ReplaceNode(scoped, inner);
        }

        if (root.Usings.Count > 0)
        {
            return root.WithUsings(Insert(root.Usings, directive, name));
        }

        // No usings yet: the file header (comments) stays first, then the directive and a blank
        // line. Preprocessor directives (#if, #nullable) stay with the code after it.
        var first = root.GetFirstToken(includeZeroWidth: true);
        var (header, rest) = SplitHeader(first.LeadingTrivia);
        var updated = root.ReplaceToken(first, first.WithLeadingTrivia(rest));
        return updated.WithUsings(SyntaxFactory.SingletonList(
            directive.WithLeadingTrivia(header).WithTrailingTrivia(endOfLine, endOfLine)));
    }

    // Where the new directive goes:
    // - after every global using (a plain using before a global one is error CS8915);
    // - not after a using that starts a preprocessor region (#if), so it is never conditional;
    // - otherwise in sorted order, System first.
    private static SyntaxList<UsingDirectiveSyntax> Insert(SyntaxList<UsingDirectiveSyntax> usings, UsingDirectiveSyntax directive, string name)
    {
        var lowest = 0;
        var highest = usings.Count;
        for (var i = 0; i < usings.Count; i++)
        {
            if (!usings[i].GlobalKeyword.IsKind(SyntaxKind.None))
            {
                lowest = i + 1;
            }

            if (highest == usings.Count && usings[i].GetLeadingTrivia().Any(trivia => trivia.IsDirective))
            {
                highest = i;
            }
        }

        var index = -1;
        for (var i = 0; i < usings.Count; i++)
        {
            if (IsPlainUsing(usings[i]) && Compare(name, usings[i].Name!.ToString()) < 0)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            // After the last plain using, keeping aliases and static usings after it where they are.
            for (var i = 0; i < usings.Count; i++)
            {
                if (IsPlainUsing(usings[i]))
                {
                    index = i + 1;
                }
            }
        }

        index = Math.Max(lowest, Math.Min(Math.Max(index, 0), highest));
        if (index == 0)
        {
            // The first directive may carry the file header: keep the header on top, and keep
            // preprocessor directives with the directive they belong to.
            var first = usings[0];
            var (header, rest) = SplitHeader(first.GetLeadingTrivia());
            directive = directive.WithLeadingTrivia(header);
            usings = usings.Replace(first, first.WithLeadingTrivia(rest.Count > 0 ? rest : Indentation(first)));
        }
        else
        {
            directive = directive.WithLeadingTrivia(Indentation(usings[index - 1]));
        }

        return usings.Insert(index, directive);
    }

    // Splits leading trivia at the first preprocessor directive: comments before it are the file
    // header, the directive and everything after it stay where they are.
    private static (SyntaxTriviaList Header, SyntaxTriviaList Remainder) SplitHeader(SyntaxTriviaList leading)
    {
        for (var i = 0; i < leading.Count; i++)
        {
            if (leading[i].IsDirective)
            {
                return (SyntaxFactory.TriviaList(leading.Take(i)), SyntaxFactory.TriviaList(leading.Skip(i)));
            }
        }

        return (leading, SyntaxFactory.TriviaList());
    }

    // The whitespace before a directive on its own line.
    private static SyntaxTriviaList Indentation(SyntaxNode node)
    {
        var leading = node.GetLeadingTrivia();
        return leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxFactory.TriviaList();
    }

    private static bool IsPlainUsing(UsingDirectiveSyntax directive) =>
        directive.Alias is null
        && directive.StaticKeyword.IsKind(SyntaxKind.None)
        && directive.GlobalKeyword.IsKind(SyntaxKind.None)
        && directive.Name is not null;

    // System namespaces first, then ordinal, ignoring case.
    private static int Compare(string first, string second)
    {
        var firstSystem = IsSystem(first);
        var secondSystem = IsSystem(second);
        if (firstSystem != secondSystem)
        {
            return firstSystem ? -1 : 1;
        }

        return string.Compare(first, second, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystem(string name) => name == "System" || name.StartsWith("System.", StringComparison.Ordinal);

    /// <summary>Returns the end of line the document uses (the first one found), or a line feed.</summary>
    public static SyntaxTrivia EndOfLine(SyntaxNode root)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return trivia;
            }
        }

        return SyntaxFactory.LineFeed;
    }

    /// <summary>
    /// Returns <see langword="true"/> when an <c>await</c> can be added at this node: inside an
    /// async method, lambda or local function, or in top-level statements, and not in a lock.
    /// </summary>
    public static bool CanAwait(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case LockStatementSyntax:
                case QueryExpressionSyntax:
                    return false;
                case AnonymousFunctionExpressionSyntax function:
                    return function.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case MethodDeclarationSyntax method:
                    return method.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case GlobalStatementSyntax:
                    return true;
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns a token without elastic trivia. Code fixes never leave elastic trivia or
    /// formatter annotations behind: the formatter would write the workspace's default end of
    /// line (CRLF on Windows) into the document, whatever line ending the file uses.
    /// </summary>
    public static SyntaxToken Plain(SyntaxKind kind) => SyntaxFactory.Token(SyntaxTriviaList.Empty, kind, SyntaxTriviaList.Empty);

    /// <summary>Returns an argument list "(a, b)" with plain tokens and one space after each comma.</summary>
    public static ArgumentListSyntax Arguments(System.Collections.Generic.IReadOnlyList<ArgumentSyntax> arguments)
    {
        var separators = new SyntaxToken[Math.Max(0, arguments.Count - 1)];
        for (var i = 0; i < separators.Length; i++)
        {
            separators[i] = Plain(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);
        }

        return SyntaxFactory.ArgumentList(
            Plain(SyntaxKind.OpenParenToken),
            SyntaxFactory.SeparatedList(arguments, separators),
            Plain(SyntaxKind.CloseParenToken));
    }

    /// <summary>Returns "(expression)" with plain tokens.</summary>
    public static ParenthesizedExpressionSyntax Parenthesize(ExpressionSyntax expression) =>
        SyntaxFactory.ParenthesizedExpression(Plain(SyntaxKind.OpenParenToken), expression, Plain(SyntaxKind.CloseParenToken));

    /// <summary>Returns the method name of a call, or <see langword="null"/>.</summary>
    public static SimpleNameSyntax? MethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name,
            MemberBindingExpressionSyntax binding => binding.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };

    /// <summary>Returns the call with a new method name, keeping type arguments and trivia.</summary>
    public static InvocationExpressionSyntax WithMethodName(InvocationExpressionSyntax invocation, string name)
    {
        if (MethodName(invocation) is not { } oldName)
        {
            return invocation;
        }

        var identifier = SyntaxFactory.Identifier(name).WithTriviaFrom(oldName.Identifier);
        SimpleNameSyntax newName = oldName is GenericNameSyntax generic
            ? generic.WithIdentifier(identifier)
            : SyntaxFactory.IdentifierName(identifier);
        return invocation.ReplaceNode(oldName, newName);
    }
}
