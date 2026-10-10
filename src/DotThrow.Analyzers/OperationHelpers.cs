using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>Small helpers on operations and syntax shared by the analyzers and code fixes.</summary>
internal static class OperationHelpers
{
    /// <summary>Removes implicit conversions, for example a value boxed for <c>== null</c>.</summary>
    public static IOperation WithoutImplicitConversions(this IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    /// <summary>Returns the argument for the parameter with the given name, or <see langword="null"/>.</summary>
    public static IArgumentOperation? Argument(this IInvocationOperation invocation, string parameterName)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>Returns the argument for the parameter with the given name, or <see langword="null"/>.</summary>
    public static IArgumentOperation? Argument(this IObjectCreationOperation creation, string parameterName)
    {
        foreach (var argument in creation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the receiver of a call: the instance, or the first argument of an extension
    /// method, without implicit conversions.
    /// </summary>
    public static IOperation? Receiver(this IInvocationOperation invocation)
    {
        if (invocation.Instance is { } instance)
        {
            return instance.WithoutImplicitConversions();
        }

        return invocation.TargetMethod.IsExtensionMethod && invocation.Arguments.Length > 0
            ? invocation.Arguments[0].Value.WithoutImplicitConversions()
            : null;
    }

    /// <summary>Returns the expression of an argument, without the <c>name:</c> part.</summary>
    public static ExpressionSyntax? ExpressionSyntax(this IOperation operation) =>
        operation.Syntax switch
        {
            ArgumentSyntax argument => argument.Expression,
            ExpressionSyntax expression => expression,
            _ => null,
        };

    /// <summary>
    /// Returns <see langword="true"/> for a value that can be read again without side effects:
    /// a local, a parameter, <c>this</c>, a field, or a property of such a value.
    /// </summary>
    public static bool IsSimpleReference(this IOperation operation)
    {
        while (true)
        {
            switch (operation.WithoutImplicitConversions())
            {
                case ILocalReferenceOperation:
                case IParameterReferenceOperation:
                case IInstanceReferenceOperation:
                    return true;
                case IFieldReferenceOperation field:
                    if (field.Instance is null)
                    {
                        return true;
                    }

                    operation = field.Instance;
                    continue;
                case IPropertyReferenceOperation { Arguments.Length: 0 } property:
                    if (property.Instance is null)
                    {
                        return true;
                    }

                    operation = property.Instance;
                    continue;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when evaluating the operation has no side effects that
    /// matter: no calls, object creations, assignments, increments or awaits.
    /// </summary>
    public static bool HasNoSideEffects(this IOperation operation)
    {
        foreach (var descendant in operation.DescendantsAndSelf())
        {
            switch (descendant)
            {
                case IInvocationOperation:
                case IObjectCreationOperation:
                case IAssignmentOperation:
                case IIncrementOrDecrementOperation:
                case IAwaitOperation:
                case IAnonymousFunctionOperation:
                    return false;
            }
        }

        return true;
    }

    /// <summary>Returns <see langword="true"/> when the syntax tree's language version supports [CallerArgumentExpression].</summary>
    public static bool SupportsCallerArgumentExpression(this SyntaxTree tree) =>
        tree.Options is CSharpParseOptions options && options.LanguageVersion >= LanguageVersion.CSharp10;

    /// <summary>
    /// Returns the text the compiler passes for a [CallerArgumentExpression] parameter that
    /// refers to this argument: the source text of the expression, without outer trivia.
    /// </summary>
    public static string? CallerArgumentText(this IArgumentOperation argument) =>
        (argument.Syntax is ArgumentSyntax syntax ? syntax.Expression : argument.Syntax).ToString();

    /// <summary>Returns the name of the parameter that a [CallerArgumentExpression] attribute points to.</summary>
    public static string? CallerArgumentExpressionTarget(this IParameterSymbol parameter)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "CallerArgumentExpressionAttribute" } type
                && type.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is string target)
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>Returns the location of the method name of a call through the end of its argument list.</summary>
    public static Location NameToEndLocation(this IInvocationOperation invocation)
    {
        if (invocation.Syntax is InvocationExpressionSyntax syntax)
        {
            var name = syntax.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name,
                MemberBindingExpressionSyntax binding => binding.Name,
                SimpleNameSyntax simple => simple,
                _ => null,
            };

            if (name is not null)
            {
                return Location.Create(syntax.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(name.SpanStart, syntax.Span.End));
            }
        }

        return invocation.Syntax.GetLocation();
    }

    /// <summary>Returns the location of the method name of a call.</summary>
    public static Location NameLocation(this IInvocationOperation invocation)
    {
        if (invocation.Syntax is InvocationExpressionSyntax syntax)
        {
            switch (syntax.Expression)
            {
                case MemberAccessExpressionSyntax member:
                    return member.Name.GetLocation();
                case MemberBindingExpressionSyntax binding:
                    return binding.Name.GetLocation();
                case SimpleNameSyntax simple:
                    return simple.GetLocation();
            }
        }

        return invocation.Syntax.GetLocation();
    }
}
