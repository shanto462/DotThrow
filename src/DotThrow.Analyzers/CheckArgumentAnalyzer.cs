using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>
/// DT0007: a constant argument that makes a check throw on every call or fail every value.
/// DT0008: <c>IfBefore(DateTime.Now)</c> and similar, where <c>IfInPast()</c> or <c>IfInFuture()</c> exists.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CheckArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic property: the check to call instead (DT0008).</summary>
    internal const string ReplacementProperty = "Replacement";

    // Checks whose int arguments must not be negative.
    private static readonly Dictionary<string, string[]> NonNegative = new(StringComparer.Ordinal)
    {
        ["IfLongerThan"] = ["maxLength"],
        ["IfShorterThan"] = ["minLength"],
        ["IfLengthNotEqualTo"] = ["length"],
        ["IfLengthOutOfRange"] = ["minLength"],
        ["IfCountEqualTo"] = ["count"],
        ["IfCountNotEqualTo"] = ["count"],
        ["IfCountGreaterThan"] = ["maxCount"],
        ["IfCountLessThan"] = ["minCount"],
        ["IfCountOutOfRange"] = ["minCount"],
    };

    // Checks with a range: the minimum must not be greater than the maximum.
    private static readonly Dictionary<string, (string Min, string Max)> Ranges = new(StringComparer.Ordinal)
    {
        ["IfOutOfRange"] = ("min", "max"),
        ["IfOutOfRangeExclusive"] = ("min", "max"),
        ["IfInRange"] = ("min", "max"),
        ["IfLengthOutOfRange"] = ("minLength", "maxLength"),
        ["IfCountOutOfRange"] = ("minCount", "maxCount"),
    };

    // Ordering checks: a NaN limit has no order, so the check throws.
    private static readonly Dictionary<string, string[]> Limits = new(StringComparer.Ordinal)
    {
        ["IfGreaterThan"] = ["limit"],
        ["IfGreaterThanOrEqualTo"] = ["limit"],
        ["IfLessThan"] = ["limit"],
        ["IfLessThanOrEqualTo"] = ["limit"],
        ["IfOutOfRange"] = ["min", "max"],
        ["IfOutOfRangeExclusive"] = ["min", "max"],
        ["IfInRange"] = ["min", "max"],
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.InvalidArgument, Descriptors.UseCurrentTimeCheck);

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
        if (!symbols.IsCheck(invocation.TargetMethod))
        {
            return;
        }

        if (InvalidArgument(invocation) is { } problem)
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.InvalidArgument, problem.Location, problem.Message));
            return;
        }

        if (invocation.TargetMethod.Name is "IfBefore" or "IfAfter")
        {
            AnalyzeCurrentTime(context, invocation, symbols);
        }
    }

    private static Problem? InvalidArgument(IInvocationOperation invocation)
    {
        var name = invocation.TargetMethod.Name;
        if (NonNegative.TryGetValue(name, out var nonNegative))
        {
            foreach (var parameter in nonNegative)
            {
                if (Constant(invocation, parameter) is { } argument && ConstantComparer.IsNegative(argument.Value.ConstantValue.Value))
                {
                    return new Problem(argument, $"'{parameter}' is {Text(argument)}, but it must not be negative, so this check always throws");
                }
            }
        }

        if (Limits.TryGetValue(name, out var limits))
        {
            foreach (var parameter in limits)
            {
                if (Constant(invocation, parameter) is { } argument && ConstantComparer.IsNaN(argument.Value.ConstantValue.Value))
                {
                    return new Problem(argument, $"'{parameter}' is NaN, which has no order, so this check always throws");
                }
            }
        }

        if (Ranges.TryGetValue(name, out var range)
            && Constant(invocation, range.Min) is { } min
            && Constant(invocation, range.Max) is { } max
            && ConstantComparer.Compare(min.Value.ConstantValue, max.Value.ConstantValue) is { } order)
        {
            if (order > 0)
            {
                return new Problem(max, $"The minimum {Text(min)} is greater than the maximum {Text(max)}, so this check always throws");
            }

            if (order == 0 && name == "IfOutOfRangeExclusive")
            {
                return new Problem(max, $"No value is strictly between {Text(min)} and {Text(max)}, so every value fails");
            }
        }

        if (name == "IfNotMultipleOf"
            && Constant(invocation, "divisor") is { } divisor
            && divisor.Value.ConstantValue.Value is { } value
            && ConstantComparer.IsZero(value))
        {
            return new Problem(divisor, "The divisor is zero, so this check always throws");
        }

        if (name == "IfPrecisionScaleExceeded")
        {
            var precision = Constant(invocation, "precision");
            var scale = Constant(invocation, "scale");
            if (precision?.Value.ConstantValue.Value is int p && p < 1)
            {
                return new Problem(precision, $"The precision is {Text(precision)}, but it must be at least 1, so this check always throws");
            }

            if (scale?.Value.ConstantValue.Value is int s && (s < 0 || (precision?.Value.ConstantValue.Value is int q && s > q)))
            {
                return new Problem(scale, $"The scale is {Text(scale)}, but it must be from 0 to the precision, so this check always throws");
            }
        }

        return null;
    }

    private static IArgumentOperation? Constant(IInvocationOperation invocation, string parameter) =>
        invocation.Argument(parameter) is { ArgumentKind: not ArgumentKind.DefaultValue } argument && argument.Value.ConstantValue.HasValue
            ? argument
            : null;

    private static string Text(IArgumentOperation argument) =>
        Convert.ToString(argument.Value.ConstantValue.Value, CultureInfo.InvariantCulture) ?? "null";

    private static void AnalyzeCurrentTime(OperationAnalysisContext context, IInvocationOperation invocation, DotThrowSymbols symbols)
    {
        // Only the extension form guard.IfBefore(limit), which the fix rewrites. A message that
        // shows the limit ({ComparisonValue}) would lose it, so it is left alone.
        if (invocation.Argument("limit") is not { } limit
            || invocation.Arguments[0].Syntax is ArgumentSyntax
            || invocation.Argument("message")?.Value.ConstantValue.Value is string message && message.IndexOf("{ComparisonValue", StringComparison.Ordinal) >= 0
            || symbols.GuardValueType(invocation.Receiver()?.Type) is not { } valueType)
        {
            return;
        }

        // Only comparisons that mean exactly what IfInPast and IfInFuture do. DateTime.UtcNow is
        // left out for DateTime: an Unspecified value is compared with it as UTC, while
        // IfInPast compares an Unspecified value with the local time.
        var now = limit.Value.WithoutImplicitConversions();
        IOperation? timeProvider = null;
        var matches = valueType.ToDisplayString() switch
        {
            "System.DateTime" => IsStaticProperty(now, "System.DateTime", "Now"),
            "System.DateTimeOffset" => IsStaticProperty(now, "System.DateTimeOffset", "Now", "UtcNow")
                || IsStaticProperty(now, "System.DateTime", "Now", "UtcNow")
                || IsTimeProviderNow(now, out timeProvider, "GetUtcNow", "GetLocalNow"),
            "System.DateOnly" => IsDateOnlyToday(now, out timeProvider),
            _ => false,
        };

        if (!matches)
        {
            return;
        }

        var replacement = invocation.TargetMethod.Name == "IfBefore" ? "IfInPast" : "IfInFuture";
        if (!HasOverload(symbols, replacement, valueType, timeProvider is not null))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(ReplacementProperty, replacement);
        Location[] additional = timeProvider is null ? [] : [timeProvider.Syntax.GetLocation()];
        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.UseCurrentTimeCheck,
            invocation.NameToEndLocation(),
            additional,
            properties,
            replacement + (timeProvider is null ? "()" : "(" + timeProvider.Syntax + ")"),
            invocation.TargetMethod.Name));
    }

    private static bool IsStaticProperty(IOperation operation, string type, params string[] names) =>
        operation is IPropertyReferenceOperation { Instance: null } property
        && Array.IndexOf(names, property.Property.Name) >= 0
        && property.Property.ContainingType.ToDisplayString() == type;

    // timeProvider.GetUtcNow() or timeProvider.GetLocalNow()
    private static bool IsTimeProviderNow(IOperation operation, out IOperation? timeProvider, params string[] names)
    {
        timeProvider = null;
        if (operation is IInvocationOperation { Instance: { } instance, Arguments.Length: 0 } call
            && Array.IndexOf(names, call.TargetMethod.Name) >= 0
            && IsTimeProvider(call.TargetMethod.ContainingType))
        {
            timeProvider = instance.WithoutImplicitConversions();
            return true;
        }

        return false;
    }

    private static bool IsTimeProvider(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "System.TimeProvider")
            {
                return true;
            }
        }

        return false;
    }

    // DateOnly.FromDateTime(DateTime.Now), DateOnly.FromDateTime(DateTime.Today), or
    // DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime).
    private static bool IsDateOnlyToday(IOperation operation, out IOperation? timeProvider)
    {
        timeProvider = null;
        if (operation is not IInvocationOperation { TargetMethod.Name: "FromDateTime", Arguments.Length: 1 } call
            || call.TargetMethod.ContainingType.ToDisplayString() != "System.DateOnly")
        {
            return false;
        }

        var argument = call.Arguments[0].Value.WithoutImplicitConversions();
        if (IsStaticProperty(argument, "System.DateTime", "Now", "Today"))
        {
            return true;
        }

        return argument is IPropertyReferenceOperation { Property.Name: "DateTime", Instance: { } local }
            && local.WithoutImplicitConversions() is { } localNow
            && IsTimeProviderNow(localNow, out timeProvider, "GetLocalNow");
    }

    private static bool HasOverload(DotThrowSymbols symbols, string name, ITypeSymbol valueType, bool withTimeProvider)
    {
        foreach (var member in symbols.GuardExtensions.GetMembers(name))
        {
            if (member is IMethodSymbol { Parameters.Length: > 0 } method
                && SymbolEqualityComparer.Default.Equals(symbols.GuardValueType(method.Parameters[0].Type), valueType)
                && (method.Parameters.Length > 1 && method.Parameters[1].Type.Name == "TimeProvider") == withTimeProvider)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class Problem
    {
        public Problem(IArgumentOperation argument, string message)
        {
            Location = argument.Syntax.GetLocation();
            Message = message;
        }

        public Location Location { get; }

        public string Message { get; }
    }
}
