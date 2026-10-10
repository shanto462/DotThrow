using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>
/// A set of validation groups: the groups of a rule, or the groups a run selects. Names ignore
/// case, as in <c>Validator&lt;T&gt;</c>.
/// </summary>
internal sealed class GroupSet
{
    private GroupSet(ImmutableHashSet<string> names, bool all, bool any, bool unknown)
    {
        Names = names;
        IsAll = all;
        IsAny = any;
        IsUnknown = unknown;
    }

    /// <summary>Gets a set the analyzer cannot know (a name that is not a constant). It never matches.</summary>
    public static GroupSet Unknown { get; } = new(ImmutableHashSet<string>.Empty, all: false, any: false, unknown: true);

    /// <summary>Gets the groups of an include without groups of its own: it runs in every run.</summary>
    public static GroupSet Any { get; } = new(ImmutableHashSet<string>.Empty, all: false, any: true, unknown: false);

    public ImmutableHashSet<string> Names { get; }

    /// <summary>Gets a value indicating whether a run selects every rule (<c>ValidationGroups.All</c>).</summary>
    public bool IsAll { get; }

    public bool IsAny { get; }

    public bool IsUnknown { get; }

    public static GroupSet Of(IEnumerable<string> names, string all)
    {
        var set = ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, names);
        return new GroupSet(set, all: set.Contains(all), any: false, unknown: false);
    }

    /// <summary>Returns <see langword="true"/> when a run with these groups selects a rule with <paramref name="rule"/> groups.</summary>
    public bool Selects(GroupSet rule)
    {
        if (IsUnknown || rule.IsUnknown)
        {
            return false;
        }

        if (IsAll || rule.IsAny)
        {
            return true;
        }

        foreach (var name in rule.Names)
        {
            if (Names.Contains(name))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Finds the async rules of a validator whose rules are visible in the compilation: a
/// subclass whose constructors add rules, or an inline <c>new Validator&lt;T&gt;()...</c> chain
/// reached through a local, a field, a property or a method.
/// </summary>
internal sealed class AsyncRuleFinder
{
    private const int MaxDepth = 6;

    // The most expressions one Collect follows (locals, fields, properties, methods).
    private const int MaxExpressions = 256;

    private readonly DotThrowSymbols _symbols;
    private readonly Compilation _compilation;
    private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<RuleEntry>> _typeRules = new(SymbolEqualityComparer.Default);

    // Where a local's validator comes from, found by walking its method once (a test method
    // can call Validate on the same local many times). The walk itself never recurses.
    private readonly ConcurrentDictionary<ILocalSymbol, LocalSources> _localSources = new(SymbolEqualityComparer.Default);

    public AsyncRuleFinder(DotThrowSymbols symbols, Compilation compilation)
    {
        _symbols = symbols;
        _compilation = compilation;
        var groups = compilation.GetTypeByMetadataName("DotThrow.ValidationGroups");
        DefaultGroup = ConstantOf(groups, "Default") ?? "default";
        AllGroups = ConstantOf(groups, "All") ?? "*";
        Default = GroupSet.Of([DefaultGroup], AllGroups);
    }

    /// <summary>Gets the name of <c>ValidationGroups.Default</c>.</summary>
    public string DefaultGroup { get; }

    /// <summary>Gets the name of <c>ValidationGroups.All</c>.</summary>
    public string AllGroups { get; }

    /// <summary>Gets the groups of a rule without groups, and of a run without groups.</summary>
    public GroupSet Default { get; }

    /// <summary>
    /// Returns the name of the method that made an async rule (for example <c>MustAsync</c>)
    /// that a run with <paramref name="run"/> groups selects, or <see langword="null"/>.
    /// </summary>
    public string? Find(IOperation validator, GroupSet run) => Find(validator, run, depth: 0, new HashSet<ISymbol>(SymbolEqualityComparer.Default));

    private static string? ConstantOf(INamedTypeSymbol? type, string name)
    {
        if (type is null)
        {
            return null;
        }

        foreach (var member in type.GetMembers(name))
        {
            if (member is IFieldSymbol { HasConstantValue: true, ConstantValue: string value })
            {
                return value;
            }
        }

        return null;
    }

    private static IOperation WithoutConversions(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private string? Find(IOperation validator, GroupSet run, int depth, HashSet<ISymbol> visited)
    {
        // Child validators nest at most MaxDepth deep. The stack check turns any future
        // recursion bug into an analyzer exception (AD0001) instead of a crashed compiler.
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if (depth > MaxDepth)
        {
            return null;
        }

        var rules = new List<RuleEntry>();
        Collect(validator, rules, visited);
        foreach (var rule in rules)
        {
            if (!run.Selects(rule.Groups))
            {
                continue;
            }

            if (rule.AsyncMethod is { } name)
            {
                return name;
            }

            if (rule.Child is { } child && Find(child, rule.ChildGroups ?? run, depth + 1, visited) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Collects the rules of the validator that an expression produces. Iterative, with one
    // visited set for the whole search, so aliases (a = b; b = a;) and cycles between fields,
    // properties and methods end.
    private void Collect(IOperation start, List<RuleEntry> rules, HashSet<ISymbol> visited)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var pending = new Stack<IOperation>();
        pending.Push(start);
        for (var budget = MaxExpressions; pending.Count > 0 && budget > 0; budget--)
        {
            var expression = WithoutConversions(pending.Pop());

            // A builder chain: new Validator<T>().For(...).MustAsync(...)
            var chainEnded = false;
            while (expression is IInvocationOperation call && IsBuilder(call))
            {
                AddRuleCall(call, Scope.Root, rules);
                if (call.Instance is null)
                {
                    chainEnded = true;
                    break;
                }

                expression = WithoutConversions(call.Instance);
            }

            if (chainEnded)
            {
                continue;
            }

            // Rules from the constructors of a validator class declared in this compilation.
            if (expression.Type is INamedTypeSymbol type)
            {
                rules.AddRange(TypeRules(type));
            }

            switch (expression)
            {
                case ILocalReferenceOperation local when visited.Add(local.Local):
                    var sources = _localSources.GetOrAdd(local.Local, _ => LocalSources.Find(local, this));
                    foreach (var call in sources.Calls)
                    {
                        AddRuleCall(call, Scope.Root, rules);
                    }

                    foreach (var value in sources.Values)
                    {
                        pending.Push(value);
                    }

                    break;
                case IFieldReferenceOperation field when visited.Add(field.Field):
                    foreach (var value in InitializerValues(field.Field))
                    {
                        pending.Push(value);
                    }

                    break;
                case IPropertyReferenceOperation property when visited.Add(property.Property):
                    foreach (var value in InitializerValues(property.Property))
                    {
                        pending.Push(value);
                    }

                    break;
                case IInvocationOperation method when visited.Add(method.TargetMethod):
                    foreach (var value in ReturnedValues(method.TargetMethod))
                    {
                        pending.Push(value);
                    }

                    break;
            }
        }
    }

    private ImmutableArray<RuleEntry> TypeRules(INamedTypeSymbol type) =>
        _typeRules.GetOrAdd(type, ComputeTypeRules);

    private ImmutableArray<RuleEntry> ComputeTypeRules(INamedTypeSymbol type)
    {
        var rules = new List<RuleEntry>();
        for (var current = type; current is not null && _symbols.IsValidatorType(current); current = current.BaseType)
        {
            if (_symbols.Validator is { } validator && SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, validator))
            {
                break;
            }

            foreach (var constructor in current.InstanceConstructors)
            {
                foreach (var reference in constructor.DeclaringSyntaxReferences)
                {
                    if (OperationOf(reference.GetSyntax()) is not { } body)
                    {
                        continue;
                    }

                    foreach (var operation in body.Descendants())
                    {
                        if (operation is IInvocationOperation call && IsBuilder(call) && ChainRoot(call) is IInstanceReferenceOperation)
                        {
                            AddRuleCall(call, Scope.Root, rules);
                        }
                    }
                }
            }
        }

        return rules.ToImmutableArray();
    }

    // The values a field or property starts with: its initializer or its getter's returns.
    private IEnumerable<IOperation> InitializerValues(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            switch (reference.GetSyntax())
            {
                case VariableDeclaratorSyntax { Initializer: { } initializer }:
                    if (OperationOf(initializer) is ISymbolInitializerOperation fieldInitializer)
                    {
                        yield return fieldInitializer.Value;
                    }

                    break;
                case PropertyDeclarationSyntax property:
                    if (property.Initializer is { } propertyInitializer && OperationOf(propertyInitializer) is ISymbolInitializerOperation value)
                    {
                        yield return value.Value;
                    }

                    if (property.ExpressionBody is { } arrow && OperationOf(arrow) is { } arrowBody)
                    {
                        foreach (var returned in Returns(arrowBody))
                        {
                            yield return returned;
                        }
                    }

                    if (property.AccessorList is { } accessors)
                    {
                        foreach (var accessor in accessors.Accessors)
                        {
                            if (accessor.Keyword.ValueText == "get" && OperationOf(accessor) is { } getter)
                            {
                                foreach (var returned in Returns(getter))
                                {
                                    yield return returned;
                                }
                            }
                        }
                    }

                    break;
            }
        }
    }

    // The values an ordinary, non-virtual method in this compilation returns.
    private IEnumerable<IOperation> ReturnedValues(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary || method.IsVirtual || method.IsAbstract || method.IsOverride || method.IsExtern)
        {
            yield break;
        }

        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            if (OperationOf(reference.GetSyntax()) is { } body)
            {
                foreach (var returned in Returns(body))
                {
                    yield return returned;
                }
            }
        }
    }

    // The returned values of a body, without the returns of lambdas and local functions in it.
    private static IEnumerable<IOperation> Returns(IOperation body)
    {
        foreach (var operation in body.DescendantsAndSelf())
        {
            if (operation is IReturnOperation { ReturnedValue: { } value } returnOperation && !InNestedFunction(returnOperation, body))
            {
                yield return value;
            }
        }
    }

    private static bool InNestedFunction(IOperation operation, IOperation body)
    {
        for (var current = operation.Parent; current is not null && current != body; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return true;
            }
        }

        return false;
    }

    // The operation of a declaration in any syntax tree of the compilation.
    [SuppressMessage(
        "MicrosoftCodeAnalysisPerformance",
        "RS1030:Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer",
        Justification = "The rules of a validator are declared in another member, often in another file. The analyzer driver caches semantic models, and the reads are cached per validator type.")]
    private IOperation? OperationOf(SyntaxNode syntax)
    {
        if (!_compilation.ContainsSyntaxTree(syntax.SyntaxTree))
        {
            return null;
        }

        var model = _compilation.GetSemanticModel(syntax.SyntaxTree);
        return model.GetOperation(syntax);
    }

    private bool IsBuilder(IInvocationOperation call) =>
        _symbols.IsValidatorMethod(call.TargetMethod) && _symbols.IsValidatorType(call.TargetMethod.ReturnType);

    private IOperation ChainRoot(IInvocationOperation call)
    {
        IOperation current = call;
        while (current is IInvocationOperation invocation && IsBuilder(invocation) && invocation.Instance is { } instance)
        {
            current = WithoutConversions(instance);
        }

        return current;
    }

    // Adds what one builder call contributes: a rule, nested rules, or a child validator.
    private void AddRuleCall(IInvocationOperation call, Scope scope, List<RuleEntry> rules)
    {
        // Recursion here follows nested lambdas in the source, so it is as deep as the code.
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var name = call.TargetMethod.Name;
        switch (name)
        {
            case "Group":
                var groups = GroupArgument(call);
                foreach (var block in RuleBlocks(call))
                {
                    AddBlock(block, new Scope(groups, scope.ForcedAsync), rules);
                }

                return;

            case "When" or "Unless":
                foreach (var block in RuleBlocks(call))
                {
                    AddBlock(block, scope, rules);
                }

                return;

            case "WhenAsync" or "UnlessAsync":
                foreach (var block in RuleBlocks(call))
                {
                    AddBlock(block, new Scope(scope.Groups, name), rules);
                }

                return;
        }

        var configure = Lambda(call.Argument("configure"), out var hasConfigure);
        var isInclude = name == "Include";
        var ruleGroups = RuleGroups(configure, hasConfigure, scope, isInclude);
        var asyncMethod = name is "MustAsync" or "CustomAsync" ? name : scope.ForcedAsync ?? AsyncOption(configure);
        if (asyncMethod is not null)
        {
            rules.Add(new RuleEntry(ruleGroups, asyncMethod, child: null, childGroups: null));
        }

        // Inline child rules and dependent rules run as part of this rule.
        var nested = new Scope(ruleGroups, scope.ForcedAsync);
        foreach (var block in RuleBlocks(call))
        {
            AddBlock(block, nested, rules);
        }

        if (configure is not null)
        {
            foreach (var operation in configure.Body.Descendants())
            {
                if (operation is IInvocationOperation { TargetMethod.Name: "DependentRules" } dependent && _symbols.IsRuleOptionsMethod(dependent.TargetMethod))
                {
                    foreach (var block in RuleBlocks(dependent))
                    {
                        AddBlock(block, nested, rules);
                    }
                }
            }
        }

        if (call.Argument("validator") is { ArgumentKind: ArgumentKind.Explicit } child)
        {
            rules.Add(new RuleEntry(ruleGroups, asyncMethod: null, child.Value, ChildGroups(configure)));
        }
    }

    // The rules added inside a lambda that gets a validator, such as When(c, v => v.For(...)).
    private void AddBlock(IAnonymousFunctionOperation block, Scope scope, List<RuleEntry> rules)
    {
        if (block.Symbol.Parameters.Length == 0)
        {
            return;
        }

        var parameter = block.Symbol.Parameters[0];
        foreach (var operation in block.Body.Descendants())
        {
            if (operation is IInvocationOperation call
                && IsBuilder(call)
                && ChainRoot(call) is IParameterReferenceOperation root
                && SymbolEqualityComparer.Default.Equals(root.Parameter, parameter))
            {
                AddRuleCall(call, scope, rules);
            }
        }
    }

    // The arguments of a call that are lambdas getting a validator.
    private IEnumerable<IAnonymousFunctionOperation> RuleBlocks(IInvocationOperation call)
    {
        foreach (var argument in call.Arguments)
        {
            if (Lambda(argument, out _) is { } lambda
                && lambda.Symbol.Parameters.Length == 1
                && _symbols.IsValidatorType(lambda.Symbol.Parameters[0].Type))
            {
                yield return lambda;
            }
        }
    }

    private static IAnonymousFunctionOperation? Lambda(IArgumentOperation? argument, out bool present)
    {
        present = argument is { ArgumentKind: not ArgumentKind.DefaultValue }
            && argument.Value.WithoutImplicitConversions().ConstantValue is not { HasValue: true, Value: null };
        return present && WithoutConversions(argument!.Value) is IDelegateCreationOperation { Target: IAnonymousFunctionOperation lambda }
            ? lambda
            : null;
    }

    private GroupSet RuleGroups(IAnonymousFunctionOperation? configure, bool hasConfigure, Scope scope, bool isInclude)
    {
        var inherited = scope.Groups ?? (isInclude ? GroupSet.Any : Default);
        if (!hasConfigure)
        {
            return inherited;
        }

        if (configure is null)
        {
            return GroupSet.Unknown;
        }

        var groups = OptionGroups(configure, "InGroup");
        if (groups is null)
        {
            return inherited;
        }

        // InGroup inside a Group block: not worth guessing.
        return scope.Groups is null ? groups : GroupSet.Unknown;
    }

    private GroupSet? ChildGroups(IAnonymousFunctionOperation? configure) =>
        configure is null ? null : OptionGroups(configure, "WithChildGroups");

    // The groups passed to InGroup or WithChildGroups in a configure lambda, or null when not called.
    private GroupSet? OptionGroups(IAnonymousFunctionOperation configure, string method)
    {
        foreach (var operation in configure.Body.Descendants())
        {
            if (operation is IInvocationOperation call && call.TargetMethod.Name == method && _symbols.IsRuleOptionsMethod(call.TargetMethod))
            {
                return ConstantStrings(call.Argument("groups")) is { } names ? GroupSet.Of(names, AllGroups) : GroupSet.Unknown;
            }
        }

        return null;
    }

    // WhenAsync, UnlessAsync or WhereAsync in a configure lambda.
    private string? AsyncOption(IAnonymousFunctionOperation? configure)
    {
        if (configure is null)
        {
            return null;
        }

        foreach (var operation in configure.Body.Descendants())
        {
            if (operation is IInvocationOperation { TargetMethod.Name: "WhenAsync" or "UnlessAsync" or "WhereAsync" } call
                && _symbols.IsRuleOptionsMethod(call.TargetMethod))
            {
                return call.TargetMethod.Name;
            }
        }

        return null;
    }

    private GroupSet GroupArgument(IInvocationOperation call)
    {
        if (call.Argument("group") is { } single)
        {
            return single.Value.ConstantValue is { HasValue: true, Value: string name } ? GroupSet.Of([name], AllGroups) : GroupSet.Unknown;
        }

        return ConstantStrings(call.Argument("groups")) is { } names ? GroupSet.Of(names, AllGroups) : GroupSet.Unknown;
    }

    /// <summary>
    /// Returns the constant strings of a params or array argument, or <see langword="null"/>
    /// when any of them is not a constant.
    /// </summary>
    public static List<string>? ConstantStrings(IArgumentOperation? argument)
    {
        if (argument is null)
        {
            return null;
        }

        if (WithoutConversions(argument.Value) is not IArrayCreationOperation { Initializer: { } initializer })
        {
            return null;
        }

        var names = new List<string>(initializer.ElementValues.Length);
        foreach (var element in initializer.ElementValues)
        {
            if (element.ConstantValue is not { HasValue: true, Value: string name })
            {
                return null;
            }

            names.Add(name);
        }

        return names;
    }

    // The values a local gets (initializer, assignments) and the builder calls made through it.
    private sealed class LocalSources
    {
        private LocalSources(ImmutableArray<IOperation> values, ImmutableArray<IInvocationOperation> calls)
        {
            Values = values;
            Calls = calls;
        }

        public ImmutableArray<IOperation> Values { get; }

        public ImmutableArray<IInvocationOperation> Calls { get; }

        public static LocalSources Find(ILocalReferenceOperation reference, AsyncRuleFinder finder)
        {
            var local = reference.Local;
            IOperation root = reference;
            while (root.Parent is { } parent)
            {
                root = parent;
            }

            var values = ImmutableArray.CreateBuilder<IOperation>();
            var calls = ImmutableArray.CreateBuilder<IInvocationOperation>();
            foreach (var operation in root.Descendants())
            {
                switch (operation)
                {
                    case IVariableDeclaratorOperation declarator when SymbolEqualityComparer.Default.Equals(declarator.Symbol, local):
                        if (declarator.GetVariableInitializer() is { } initializer)
                        {
                            values.Add(initializer.Value);
                        }

                        break;
                    case ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } assignment
                        when SymbolEqualityComparer.Default.Equals(target.Local, local):
                        values.Add(assignment.Value);
                        break;
                    case IInvocationOperation call when finder.IsBuilder(call)
                        && finder.ChainRoot(call) is ILocalReferenceOperation chainRoot
                        && SymbolEqualityComparer.Default.Equals(chainRoot.Local, local):
                        calls.Add(call);
                        break;
                }
            }

            return new LocalSources(values.ToImmutable(), calls.ToImmutable());
        }
    }

    private readonly struct Scope
    {
        public Scope(GroupSet? groups, string? forcedAsync)
        {
            Groups = groups;
            ForcedAsync = forcedAsync;
        }

        public static Scope Root => default;

        // The groups of an enclosing Group block or parent rule, or null at the top level.
        public GroupSet? Groups { get; }

        // The async block method (WhenAsync, UnlessAsync) that makes every rule inside async.
        public string? ForcedAsync { get; }
    }

    private sealed class RuleEntry
    {
        public RuleEntry(GroupSet groups, string? asyncMethod, IOperation? child, GroupSet? childGroups)
        {
            Groups = groups;
            AsyncMethod = asyncMethod;
            Child = child;
            ChildGroups = childGroups;
        }

        public GroupSet Groups { get; }

        public string? AsyncMethod { get; }

        public IOperation? Child { get; }

        public GroupSet? ChildGroups { get; }
    }
}
