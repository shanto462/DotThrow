using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>
/// DT0004: a validator with async rules run through <c>Validate</c>, <c>ValidateAndThrow</c> or
/// <c>TestValidate</c>, when its rules are visible in the compilation.
/// DT0010: the report of <c>Validate</c> or <c>ValidateAsync</c> is thrown away, or the task of an
/// async run is never awaited.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidatorUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic property: the method to call instead.</summary>
    internal const string ReplacementProperty = "Replacement";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.AsyncRulesRunSynchronously, Descriptors.UnusedValidationResult);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (DotThrowSymbols.Create(start.Compilation) is not { Validator: not null } symbols)
            {
                return;
            }

            var finder = new AsyncRuleFinder(symbols, start.Compilation);
            start.RegisterOperationAction(operation => Analyze(operation, symbols, finder), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, DotThrowSymbols symbols, AsyncRuleFinder finder)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.Name is not ("Validate" or "ValidateAndThrow" or "TestValidate" or "ValidateAsync" or "ValidateAndThrowAsync" or "TestValidateAsync")
            || !IsValidationApi(method, symbols))
        {
            return;
        }

        AnalyzeUnusedResult(context, invocation);

        if (method.Name is "Validate" or "ValidateAndThrow" or "TestValidate"
            && RunGroups(invocation, finder) is { } run
            && invocation.Receiver() is { } validator
            && finder.Find(validator, run) is { } asyncRule)
        {
            var asyncMethod = method.Name + "Async";
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.AsyncRulesRunSynchronously,
                invocation.NameLocation(),
                ImmutableDictionary<string, string?>.Empty.Add(ReplacementProperty, asyncMethod),
                asyncRule,
                method.Name,
                asyncMethod));
        }
    }

    private static bool IsValidationApi(IMethodSymbol method, DotThrowSymbols symbols)
    {
        var type = method.ContainingType?.OriginalDefinition;
        return DotThrowSymbols.Is(type, symbols.Validator)
            || (symbols.ValidatorInterface is not null && DotThrowSymbols.Is(type, symbols.ValidatorInterface))
            || (symbols.ValidatorExtensions is not null && DotThrowSymbols.Is(type, symbols.ValidatorExtensions))
            || (symbols.TestValidationExtensions is not null && DotThrowSymbols.Is(type, symbols.TestValidationExtensions));
    }

    // The groups a run selects, or null when they are not constants (or options decide).
    private static GroupSet? RunGroups(IInvocationOperation invocation, AsyncRuleFinder finder)
    {
        var names = new List<string>();
        foreach (var argument in invocation.Arguments)
        {
            var parameter = argument.Parameter;
            if (parameter is null || parameter.Ordinal == 0 || (invocation.TargetMethod.IsExtensionMethod && parameter.Ordinal == 1))
            {
                continue;
            }

            var isDefault = argument.ArgumentKind == ArgumentKind.DefaultValue
                || argument.Value.WithoutImplicitConversions().ConstantValue is { HasValue: true, Value: null };
            switch (parameter.Name)
            {
                case "groups":
                    if (isDefault)
                    {
                        break;
                    }

                    if (AsyncRuleFinder.ConstantStrings(argument) is not { } groups)
                    {
                        return null;
                    }

                    names.AddRange(groups);
                    break;
                case "options":
                    if (!isDefault)
                    {
                        return null;
                    }

                    break;
                default:
                    // configure callbacks and anything new: the analyzer cannot know the groups.
                    return null;
            }
        }

        return names.Count == 0 ? finder.Default : GroupSet.Of(names, finder.AllGroups);
    }

    private static void AnalyzeUnusedResult(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var parent = invocation.Parent;
        if (parent is IInvocationOperation { TargetMethod.Name: "ConfigureAwait" } configureAwait && configureAwait.Instance == invocation)
        {
            parent = configureAwait.Parent;
        }

        var awaited = parent is IAwaitOperation;
        if (awaited)
        {
            parent = parent!.Parent;
        }

        if (parent is not IExpressionStatementOperation)
        {
            return;
        }

        var name = invocation.TargetMethod.Name;
        string? replacement = null;
        if (name.EndsWith("Async", System.StringComparison.Ordinal) && !awaited)
        {
            // An async method that is not awaited: in an async function the compiler warns
            // (CS4014), elsewhere nothing does.
            if (InAsyncFunction(invocation, context.ContainingSymbol))
            {
                return;
            }
        }
        else
        {
            switch (name)
            {
                case "Validate":
                case "ValidateAsync":
                    // A report from options may throw by itself (ThrowOnFailures).
                    if (!OnlyGroupParameters(invocation.TargetMethod))
                    {
                        return;
                    }

                    replacement = name == "Validate" ? "ValidateAndThrow" : AsyncReplacement(invocation.TargetMethod);
                    break;
                case "TestValidate":
                case "TestValidateAsync":
                    break;
                default:
                    return;
            }
        }

        var properties = replacement is null
            ? ImmutableDictionary<string, string?>.Empty
            : ImmutableDictionary<string, string?>.Empty.Add(ReplacementProperty, replacement);
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.UnusedValidationResult, invocation.NameLocation(), properties, name));
    }

    private static bool OnlyGroupParameters(IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            if (parameter.Ordinal == 0 || (method.IsExtensionMethod && parameter.Ordinal == 1))
            {
                continue;
            }

            if (parameter.Name is not ("groups" or "group" or "cancellationToken"))
            {
                return false;
            }
        }

        return true;
    }

    // ValidateAndThrowAsync has the overloads (value, token) and (value, groups, token), but not (value, group).
    private static string? AsyncReplacement(IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            if (parameter.Name == "group")
            {
                return null;
            }
        }

        return "ValidateAndThrowAsync";
    }

    private static bool InAsyncFunction(IOperation operation, ISymbol containingSymbol)
    {
        for (var node = operation.Syntax.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case AnonymousFunctionExpressionSyntax function:
                    return function.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case MethodDeclarationSyntax methodDeclaration:
                    return methodDeclaration.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case MemberDeclarationSyntax and not GlobalStatementSyntax:
                    return false;
            }
        }

        // Top-level statements: the generated entry point is async when it awaits.
        return containingSymbol is IMethodSymbol { IsAsync: true };
    }
}
