using Microsoft.CodeAnalysis;

namespace DotThrow.Analyzers;

/// <summary>
/// The DotThrow types the analyzers look for, resolved once per compilation. A compilation
/// that does not reference DotThrow gets no symbols, so no analyzer runs on it.
/// </summary>
internal sealed class DotThrowSymbols
{
    private DotThrowSymbols(Compilation compilation, INamedTypeSymbol throwExtensions, INamedTypeSymbol guardExtensions, INamedTypeSymbol guard, INamedTypeSymbol report)
    {
        ThrowExtensions = throwExtensions;
        GuardExtensions = guardExtensions;
        Guard = guard;
        ValidationReport = report;
        ValidationContext = compilation.GetTypeByMetadataName("DotThrow.ValidationContext");
        Validator = compilation.GetTypeByMetadataName("DotThrow.Validator`1");
        ValidatorInterface = compilation.GetTypeByMetadataName("DotThrow.IValidator`1");
        ValidatorExtensions = compilation.GetTypeByMetadataName("DotThrow.ValidatorExtensions");
        TestValidationExtensions = compilation.GetTypeByMetadataName("DotThrow.Testing.TestValidationExtensions");
        RuleOptionsBase = compilation.GetTypeByMetadataName("DotThrow.RuleOptionsBase`3");
        EachRuleOptions = compilation.GetTypeByMetadataName("DotThrow.EachRuleOptions`2");
        ValidationOptions = compilation.GetTypeByMetadataName("DotThrow.ValidationOptions");
    }

    public INamedTypeSymbol ThrowExtensions { get; }

    public INamedTypeSymbol GuardExtensions { get; }

    /// <summary>Gets <c>Guard&lt;T&gt;</c> (the unbound definition).</summary>
    public INamedTypeSymbol Guard { get; }

    public INamedTypeSymbol ValidationReport { get; }

    public INamedTypeSymbol? ValidationContext { get; }

    public INamedTypeSymbol? Validator { get; }

    public INamedTypeSymbol? ValidatorInterface { get; }

    public INamedTypeSymbol? ValidatorExtensions { get; }

    public INamedTypeSymbol? TestValidationExtensions { get; }

    public INamedTypeSymbol? RuleOptionsBase { get; }

    public INamedTypeSymbol? EachRuleOptions { get; }

    public INamedTypeSymbol? ValidationOptions { get; }

    public static DotThrowSymbols? Create(Compilation compilation)
    {
        var throwExtensions = compilation.GetTypeByMetadataName("DotThrow.ThrowExtensions");
        var guardExtensions = compilation.GetTypeByMetadataName("DotThrow.GuardExtensions");
        var guard = compilation.GetTypeByMetadataName("DotThrow.Guard`1");
        var report = compilation.GetTypeByMetadataName("DotThrow.ValidationReport");
        return throwExtensions is null || guardExtensions is null || guard is null || report is null
            ? null
            : new DotThrowSymbols(compilation, throwExtensions, guardExtensions, guard, report);
    }

    /// <summary>Returns <see langword="true"/> for <c>Guard&lt;T&gt;</c> of any <c>T</c>.</summary>
    public bool IsGuard(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, Guard);

    /// <summary>Returns the <c>T</c> of a <c>Guard&lt;T&gt;</c>, or <see langword="null"/>.</summary>
    public ITypeSymbol? GuardValueType(ITypeSymbol? type) =>
        IsGuard(type) ? ((INamedTypeSymbol)type!).TypeArguments[0] : null;

    /// <summary>Returns <see langword="true"/> for <c>Throw()</c>, <c>report.Check()</c> and <c>context.Check()</c>.</summary>
    public bool IsChainStart(IMethodSymbol method)
    {
        var type = method.ContainingType?.OriginalDefinition;
        return method.Name switch
        {
            "Throw" => Is(type, ThrowExtensions),
            "Check" => Is(type, ValidationReport) || (ValidationContext is not null && Is(type, ValidationContext)),
            _ => false,
        };
    }

    /// <summary>Returns <see langword="true"/> for a check: a method of <c>GuardExtensions</c>.</summary>
    public bool IsCheck(IMethodSymbol method) => Is(method.ContainingType, GuardExtensions);

    /// <summary>Returns <see langword="true"/> for <c>Guard&lt;T&gt;.AllowNull()</c>.</summary>
    public bool IsAllowNull(IMethodSymbol method) => method.Name == "AllowNull" && Is(method.ContainingType?.OriginalDefinition, Guard);

    /// <summary>Returns <see langword="true"/> for a method declared on <c>Validator&lt;T&gt;</c>.</summary>
    public bool IsValidatorMethod(IMethodSymbol method) =>
        Validator is not null && Is(method.ContainingType?.OriginalDefinition, Validator);

    /// <summary>Returns <see langword="true"/> for a method declared on a rule options type.</summary>
    public bool IsRuleOptionsMethod(IMethodSymbol method)
    {
        var type = method.ContainingType?.OriginalDefinition;
        return (RuleOptionsBase is not null && Is(type, RuleOptionsBase)) || (EachRuleOptions is not null && Is(type, EachRuleOptions));
    }

    /// <summary>Returns <see langword="true"/> when the type is <c>Validator&lt;T&gt;</c> or derives from it.</summary>
    public bool IsValidatorType(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (Validator is not null && Is(current.OriginalDefinition, Validator))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Is(ISymbol? symbol, ISymbol? other) => SymbolEqualityComparer.Default.Equals(symbol, other);
}
