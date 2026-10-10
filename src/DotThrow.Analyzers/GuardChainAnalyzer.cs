using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>
/// DT0001: a <c>Throw()</c> or <c>Check()</c> chain without checks.
/// DT0009: <c>IfNull()</c>, <c>IfNotNull()</c> or <c>AllowNull()</c> on a non-nullable value type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GuardChainAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic property: <c>Statement</c> (remove it) or <c>Value</c> (use the value).</summary>
    internal const string KindProperty = "Kind";

    /// <summary>Diagnostic property: present when the statement can be removed safely.</summary>
    internal const string RemovableProperty = "Removable";

    /// <summary>Diagnostic property: present when the null check can be removed.</summary>
    internal const string RemoveCallProperty = "RemoveCall";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.EmptyChain, Descriptors.NullCheckOnValueType);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (DotThrowSymbols.Create(start.Compilation) is { } symbols)
            {
                start.RegisterOperationAction(operation => Analyze(operation, symbols), OperationKind.Invocation);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, DotThrowSymbols symbols)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (symbols.IsChainStart(method))
        {
            AnalyzeChainStart(context, invocation);
        }
        else if (method.Name is "IfNull" or "IfNotNull" or "AllowNull")
        {
            AnalyzeNullCheck(context, invocation, symbols);
        }
    }

    private static void AnalyzeChainStart(OperationAnalysisContext context, IInvocationOperation start)
    {
        // Walk up through AllowNull(), which is not a check.
        IOperation chain = start;
        while (chain.Parent is IInvocationOperation parent
            && parent.Instance == chain
            && parent.TargetMethod.Name == "AllowNull")
        {
            chain = parent;
        }

        // The checked value. Check(root, selector) checks selector(root), so it has none here.
        var value = start.Argument("value")?.Value.WithoutImplicitConversions();
        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        switch (chain.Parent)
        {
            case IExpressionStatementOperation:
            case ISimpleAssignmentOperation { Target: IDiscardOperation, Parent: IExpressionStatementOperation }:
                properties[KindProperty] = "Statement";
                if (AllArgumentsAreSimple(start))
                {
                    properties[RemovableProperty] = "true";
                }

                break;

            // The guard converts back to its value, so the value passes through unchecked.
            case IConversionOperation { OperatorMethod: { } conversion } when conversion.MethodKind == MethodKind.Conversion:
                if (value is not null)
                {
                    properties[KindProperty] = "Value";
                }

                break;

            default:
                return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.EmptyChain,
            chain.Syntax.GetLocation(),
            additionalLocations: value is null ? [] : [value.Syntax.GetLocation()],
            properties: properties.ToImmutable(),
            start.TargetMethod.Name));
    }

    // Every argument (and the receiver of Check) can be dropped without losing a side effect.
    private static bool AllArgumentsAreSimple(IInvocationOperation start)
    {
        if (start.Instance is { } instance && !instance.IsSimpleReference())
        {
            return false;
        }

        foreach (var argument in start.Arguments)
        {
            if (argument.ArgumentKind != ArgumentKind.DefaultValue
                && !argument.Value.IsSimpleReference()
                && !argument.Value.WithoutImplicitConversions().ConstantValue.HasValue)
            {
                return false;
            }
        }

        return true;
    }

    // guard.Call() in the reduced form, followed by another call on its result.
    private static bool IsChainedCall(IInvocationOperation invocation) =>
        invocation.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax, Parent: MemberAccessExpressionSyntax next }
        && next.Expression == invocation.Syntax
        && (invocation.Instance is not null || (invocation.Arguments.Length > 0 && invocation.Arguments[0].Syntax is not ArgumentSyntax));

    private static void AnalyzeNullCheck(OperationAnalysisContext context, IInvocationOperation invocation, DotThrowSymbols symbols)
    {
        var method = invocation.TargetMethod;
        if (!symbols.IsCheck(method) && !symbols.IsAllowNull(method))
        {
            return;
        }

        var guardType = invocation.Receiver()?.Type;
        if (symbols.GuardValueType(guardType) is not { IsValueType: true } valueType
            || valueType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return;
        }

        // IfNull<T>(this Guard<T?>) unwraps a Nullable<T>; only the Guard<T> overloads matter here.
        // The fix removes the call only from the middle of a chain written as x.IfNull().Next(),
        // so what is left is still a check.
        var removable = method.Name is "IfNull" or "AllowNull" && IsChainedCall(invocation);
        var effect = method.Name switch
        {
            "IfNull" => "never fails",
            "AllowNull" => "does nothing",
            _ => "always fails",
        };
        var properties = removable
            ? ImmutableDictionary<string, string?>.Empty.Add(RemoveCallProperty, "true")
            : ImmutableDictionary<string, string?>.Empty;

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.NullCheckOnValueType,
            invocation.NameToEndLocation(),
            properties,
            method.Name + "()",
            effect,
            valueType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }
}
