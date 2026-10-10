using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DotThrow.Analyzers;

/// <summary>A manual guard that a DotThrow check can replace, found by <see cref="ManualGuardMatcher"/>.</summary>
internal sealed class ManualGuardMatch
{
    public ManualGuardMatch(IOperation value, string check, ImmutableArray<IOperation> checkArguments, IOperation? name, string? literalName, IOperation? message, bool assign)
    {
        Value = value;
        Check = check;
        CheckArguments = checkArguments;
        Name = name;
        LiteralName = literalName;
        Message = message;
        Assign = assign;
    }

    /// <summary>Gets the checked value: a local, a parameter, a field or a property.</summary>
    public IOperation Value { get; }

    /// <summary>Gets the name of the DotThrow check, for example <c>IfNull</c>.</summary>
    public string Check { get; }

    /// <summary>Gets the arguments of the check, for example the limit of <c>IfLessThan</c>.</summary>
    public ImmutableArray<IOperation> CheckArguments { get; }

    /// <summary>Gets the parameter name passed to the exception when it differs from what the compiler fills in.</summary>
    public IOperation? Name { get; }

    /// <summary>Gets a name to pass as a string literal (C# before 10 has no [CallerArgumentExpression]).</summary>
    public string? LiteralName { get; }

    /// <summary>Gets the message passed to the exception, if any. It is always a constant.</summary>
    public IOperation? Message { get; }

    /// <summary>
    /// Gets a value indicating whether the replacement assigns the checked value back
    /// (<c>x = x.Throw().IfNull();</c>), so the compiler's nullable analysis still knows that
    /// the value is not null after the check.
    /// </summary>
    public bool Assign { get; }

    /// <summary>Returns the replacement as text, for the diagnostic message.</summary>
    public string Display()
    {
        var arguments = new string[CheckArguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = CheckArguments[i].ExpressionSyntax()?.ToString() ?? string.Empty;
        }

        var call = Value.Syntax + ".Throw()." + Check + "(" + string.Join(", ", arguments) + ")";
        return Assign ? Value.Syntax + " = " + call : call;
    }
}

/// <summary>
/// Matches <c>if (condition) throw new ArgumentXxxException(...);</c> statements that a DotThrow
/// check replaces without changing which exceptions callers can catch.
/// </summary>
internal static class ManualGuardMatcher
{
    private const string ArgumentException = "ArgumentException";
    private const string ArgumentNullException = "ArgumentNullException";
    private const string ArgumentOutOfRangeException = "ArgumentOutOfRangeException";

    /// <summary>Matches <c>if (condition) throw new ArgumentXxxException(...);</c>.</summary>
    public static ManualGuardMatch? Match(IConditionalOperation conditional)
    {
        if (conditional.Syntax is not IfStatementSyntax { Else: null }
            || conditional.WhenFalse is not null
            || ThrownCreation(conditional.WhenTrue) is not { } creation
            || ExceptionName(creation) is not { } exception
            || MatchCondition(conditional.Condition, creation) is not { } condition
            || !condition.Exceptions.Contains(exception))
        {
            return null;
        }

        return Finish(condition, creation, conditional.Syntax.SyntaxTree, statement: true);
    }

    /// <summary>
    /// Matches <c>x ?? throw new ArgumentNullException(...)</c> where the result goes to a
    /// typed target (an assignment, a typed variable, an argument or a return), so the guard
    /// converts back to the value.
    /// </summary>
    public static ManualGuardMatch? MatchCoalesce(ICoalesceOperation coalesce)
    {
        if (coalesce.WhenNull.WithoutImplicitConversions() is not IThrowOperation { Exception: { } thrown }
            || thrown.WithoutImplicitConversions() is not IObjectCreationOperation creation
            || ExceptionName(creation) is not ArgumentNullException and not ArgumentException
            || !HasTypedTarget(coalesce)
            || NullCheck(coalesce.Value.WithoutImplicitConversions()) is not { } condition)
        {
            return null;
        }

        return Finish(condition, creation, coalesce.Syntax.SyntaxTree, statement: false);
    }

    private static ManualGuardMatch? Finish(Condition condition, IObjectCreationOperation creation, SyntaxTree tree, bool statement)
    {
        if (!IsGuardableValue(condition.Value))
        {
            return null;
        }

        // Only a constant message: DotThrow takes it on every call, so a computed one would
        // run (and allocate) even when the check passes.
        var message = creation.Argument("message")?.Value;
        if (message is not null && IsNullConstant(message))
        {
            message = null;
        }

        if (message is not null && message.ConstantValue is not { HasValue: true, Value: string })
        {
            return null;
        }

        // After "if (x is null) throw", nullable analysis knows x is not null. After
        // x.Throw().IfNull() it does not, so a maybe-null value is assigned back, or not matched.
        var assign = false;
        if (statement && condition.Check is "IfNull" or "IfNullOrEmpty" or "IfNullOrWhiteSpace" && MaybeNull(condition.Value))
        {
            if (!IsAssignable(condition.Value))
            {
                return null;
            }

            assign = true;
        }

        // The name: dropped when the compiler fills in the same text, kept when it differs.
        // Before C# 10 the compiler fills in nothing, so the name is always passed.
        IOperation? name = null;
        string? literalName = null;
        var valueText = condition.Value.Syntax.ToString();
        var callerArgumentExpression = tree.SupportsCallerArgumentExpression();
        var paramName = creation.Argument("paramName")?.Value;
        if (paramName is not null && !IsNullConstant(paramName))
        {
            var sameText = paramName.ConstantValue is { HasValue: true, Value: string text } && text == valueText;
            if (!sameText || !callerArgumentExpression)
            {
                name = paramName;
            }
        }
        else if (!callerArgumentExpression)
        {
            literalName = valueText;
        }

        return new ManualGuardMatch(condition.Value, condition.Check, condition.Arguments, name, literalName, message, assign);
    }

    // ArgumentException, ArgumentNullException or ArgumentOutOfRangeException created with
    // only a parameter name, a message and an actual value; null otherwise.
    private static string? ExceptionName(IObjectCreationOperation creation)
    {
        if (creation.Constructor is not { } constructor
            || creation.Initializer is not null
            || !IsSystemType(constructor.ContainingType, out var name)
            || name is not (ArgumentException or ArgumentNullException or ArgumentOutOfRangeException))
        {
            return null;
        }

        foreach (var parameter in constructor.Parameters)
        {
            if (parameter.Name is not ("paramName" or "message" or "actualValue"))
            {
                return null;
            }
        }

        return name;
    }

    /// <summary>Returns <see langword="true"/> for a type declared directly in the System namespace.</summary>
    public static bool IsSystemType(ITypeSymbol? type, out string name)
    {
        name = type?.Name ?? string.Empty;
        return type?.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
    }

    private static bool IsSystemType(ITypeSymbol? type, string name) => IsSystemType(type, out var actual) && actual == name;

    // Declared as nullable: string? or int? (var locals of reference types too). The flow state
    // at the null test itself is not useful: the compiler reports a pure null test's operand
    // as maybe-null.
    private static bool MaybeNull(IOperation value)
    {
        if (value.Type?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        var annotation = value switch
        {
            ILocalReferenceOperation local => local.Local.NullableAnnotation,
            IParameterReferenceOperation parameter => parameter.Parameter.NullableAnnotation,
            IFieldReferenceOperation field => field.Field.NullableAnnotation,
            IPropertyReferenceOperation property => property.Property.NullableAnnotation,
            _ => NullableAnnotation.None,
        };

        return annotation == NullableAnnotation.Annotated;
    }

    // A local or a parameter that can be assigned.
    private static bool IsAssignable(IOperation value) =>
        value switch
        {
            ILocalReferenceOperation local => !local.Local.IsConst && !local.Local.IsForEach && !local.Local.IsUsing && !local.Local.IsFixed
                && local.Local.RefKind is RefKind.None or RefKind.Ref,
            IParameterReferenceOperation parameter => parameter.Parameter.RefKind is RefKind.None or RefKind.Ref or RefKind.Out,
            _ => false,
        };

    // The coalesce result goes to a target that converts the guard back to the value.
    private static bool HasTypedTarget(ICoalesceOperation coalesce)
    {
        IOperation current = coalesce;
        while (current.Parent is IConversionOperation { IsImplicit: true } conversion)
        {
            current = conversion;
        }

        return current.Parent switch
        {
            ISimpleAssignmentOperation assignment => assignment.Value == current && assignment.Target is not IDiscardOperation,
            IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } =>
                declarator.Syntax.Parent is VariableDeclarationSyntax { Type.IsVar: false },
            IArgumentOperation { Parameter: { } parameter } => parameter.OriginalDefinition.Type.TypeKind != TypeKind.TypeParameter,
            IReturnOperation => !InLambda(current),
            _ => false,
        };
    }

    private static bool InLambda(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation)
            {
                return true;
            }
        }

        return false;
    }

    // The exception created by "throw new X(...)", the only statement of the branch.
    private static IObjectCreationOperation? ThrownCreation(IOperation branch)
    {
        if (branch is IBlockOperation block)
        {
            if (block.Operations.Length != 1)
            {
                return null;
            }

            branch = block.Operations[0];
        }

        if (branch is IExpressionStatementOperation statement)
        {
            branch = statement.Operation;
        }

        return branch is IThrowOperation { Exception: { } exception }
            ? exception.WithoutImplicitConversions() as IObjectCreationOperation
            : null;
    }

    private static bool IsGuardableValue(IOperation value) =>
        value.IsSimpleReference()
        && value.Type is { } type
        && !type.IsRefLikeType
        && type.TypeKind is not (TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Dynamic)
        && type.SpecialType != SpecialType.System_Void;

    private static bool IsNullConstant(IOperation operation) =>
        operation.WithoutImplicitConversions().ConstantValue is { HasValue: true, Value: null };

    private static Condition? MatchCondition(IOperation condition, IObjectCreationOperation creation)
    {
        switch (condition)
        {
            // x is null
            case IIsPatternOperation { Pattern: IConstantPatternOperation { Value: var constant } } pattern when IsNullConstant(constant):
                return NullCheck(pattern.Value.WithoutImplicitConversions());

            // x == null, null == x (built-in reference or lifted equality only)
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, OperatorMethod: null } equals:
                if (IsNullConstant(equals.RightOperand))
                {
                    return NullCheck(equals.LeftOperand.WithoutImplicitConversions());
                }

                if (IsNullConstant(equals.LeftOperand))
                {
                    return NullCheck(equals.RightOperand.WithoutImplicitConversions());
                }

                return MatchEmptyCount(equals) ?? MatchComparison(equals, creation);

            // ReferenceEquals(x, null), string.IsNullOrEmpty(x), string.IsNullOrWhiteSpace(x)
            case IInvocationOperation invocation:
                return MatchInvocation(invocation);

            // !x.Any(), !Enum.IsDefined(...)
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not, Operand: IInvocationOperation negated }:
                return MatchNegatedInvocation(negated);

            // x || y: a range
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalOr } or:
                return MatchRange(or, creation);

            // Comparisons, x.Count == 0, x == Guid.Empty
            case IBinaryOperation binary:
                return MatchGuidEmpty(binary) ?? MatchEmptyCount(binary) ?? MatchComparison(binary, creation);

            default:
                return null;
        }
    }

    private static Condition? NullCheck(IOperation value)
    {
        // A non-nullable value type is never null; the compiler already warns about it.
        if (value.Type is { IsValueType: true } type && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
        {
            return null;
        }

        return new Condition(value, "IfNull", ImmutableArray<IOperation>.Empty, ArgumentNullException, ArgumentException);
    }

    private static Condition? MatchInvocation(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (method.ContainingType?.SpecialType == SpecialType.System_Object
            && method.Name == "ReferenceEquals"
            && invocation.Arguments.Length == 2)
        {
            var first = invocation.Arguments[0].Value;
            var second = invocation.Arguments[1].Value;
            if (IsNullConstant(second))
            {
                return NullCheck(first.WithoutImplicitConversions());
            }

            if (IsNullConstant(first))
            {
                return NullCheck(second.WithoutImplicitConversions());
            }

            return null;
        }

        if (method.ContainingType?.SpecialType == SpecialType.System_String && invocation.Arguments.Length == 1)
        {
            var value = invocation.Arguments[0].Value.WithoutImplicitConversions();
            return method.Name switch
            {
                "IsNullOrEmpty" => new Condition(value, "IfNullOrEmpty", ImmutableArray<IOperation>.Empty, ArgumentException),
                "IsNullOrWhiteSpace" => new Condition(value, "IfNullOrWhiteSpace", ImmutableArray<IOperation>.Empty, ArgumentException),
                _ => null,
            };
        }

        return null;
    }

    private static Condition? MatchNegatedInvocation(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;

        // !x.Any() from System.Linq: the collection is empty.
        if (method.Name == "Any"
            && method.Parameters.Length == 1
            && method.ContainingType is { Name: "Enumerable", ContainingNamespace: { Name: "Linq", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } }
            && invocation.Arguments.Length == 1)
        {
            var collection = invocation.Arguments[0].Value.WithoutImplicitConversions();
            return IsEnumerable(collection.Type)
                ? new Condition(collection, "IfNullOrEmpty", ImmutableArray<IOperation>.Empty, ArgumentException)
                : null;
        }

        // !Enum.IsDefined(typeof(E), x), !Enum.IsDefined(x), !Enum.IsDefined<E>(x)
        if (method.Name == "IsDefined" && method.ContainingType?.SpecialType == SpecialType.System_Enum)
        {
            IOperation? value = null;
            if (invocation.Arguments.Length == 2
                && invocation.Arguments[0].Value is ITypeOfOperation typeOf
                && invocation.Arguments[1].Value.WithoutImplicitConversions() is { } boxed
                && SymbolEqualityComparer.Default.Equals(typeOf.TypeOperand, boxed.Type))
            {
                value = boxed;
            }
            else if (invocation.Arguments.Length == 1 && method.IsGenericMethod)
            {
                value = invocation.Arguments[0].Value.WithoutImplicitConversions();
            }

            // [Flags] enums: DotThrow accepts any combination of defined flags, Enum.IsDefined does not.
            if (value?.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType && !HasFlagsAttribute(enumType))
            {
                return new Condition(value, "IfNotDefined", ImmutableArray<IOperation>.Empty, ArgumentOutOfRangeException, ArgumentException);
            }
        }

        return null;
    }

    private static bool HasFlagsAttribute(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (IsSystemType(attribute.AttributeClass, "FlagsAttribute"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnumerable(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        if (type.SpecialType == SpecialType.System_String || type is IArrayTypeSymbol)
        {
            return true;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (implemented.SpecialType == SpecialType.System_Collections_IEnumerable)
            {
                return true;
            }
        }

        return false;
    }

    // x == Guid.Empty, x == default
    private static Condition? MatchGuidEmpty(IBinaryOperation binary)
    {
        if (binary.OperatorKind != BinaryOperatorKind.Equals || binary.IsLifted)
        {
            return null;
        }

        var left = binary.LeftOperand.WithoutImplicitConversions();
        var right = binary.RightOperand.WithoutImplicitConversions();
        if (IsGuid(left.Type) && IsEmptyGuid(right))
        {
            return new Condition(left, "IfEmpty", ImmutableArray<IOperation>.Empty, ArgumentException);
        }

        if (IsGuid(right.Type) && IsEmptyGuid(left))
        {
            return new Condition(right, "IfEmpty", ImmutableArray<IOperation>.Empty, ArgumentException);
        }

        return null;
    }

    private static bool IsGuid(ITypeSymbol? type) => IsSystemType(type, "Guid");

    private static bool IsEmptyGuid(IOperation operation) =>
        operation switch
        {
            IFieldReferenceOperation { Field.Name: "Empty", Field.IsStatic: true } field => IsGuid(field.Field.ContainingType),
            IDefaultValueOperation defaultValue => IsGuid(defaultValue.Type),
            _ => false,
        };

    // x.Count == 0, x.Length == 0, 0 == x.Count, x.Count < 1, x.Count <= 0
    private static Condition? MatchEmptyCount(IBinaryOperation binary)
    {
        var left = binary.LeftOperand.WithoutImplicitConversions();
        var right = binary.RightOperand.WithoutImplicitConversions();
        var kind = binary.OperatorKind;
        if (left.ConstantValue.HasValue && !right.ConstantValue.HasValue)
        {
            (left, right) = (right, left);
            kind = Flip(kind);
        }

        if (left is not IPropertyReferenceOperation { Property.Name: "Count" or "Length", Instance: { } instance, Arguments.Length: 0 }
            || left.Type?.SpecialType != SpecialType.System_Int32
            || right.ConstantValue is not { HasValue: true, Value: int limit })
        {
            return null;
        }

        var empty = (kind == BinaryOperatorKind.Equals && limit == 0)
            || (kind == BinaryOperatorKind.LessThan && limit == 1)
            || (kind == BinaryOperatorKind.LessThanOrEqual && limit == 0);
        var collection = instance.WithoutImplicitConversions();
        return empty && IsEnumerable(collection.Type)
            ? new Condition(collection, "IfNullOrEmpty", ImmutableArray<IOperation>.Empty, ArgumentException)
            : null;
    }

    // x < limit, limit > x, x == 0 and so on, for numbers.
    private static Condition? MatchComparison(IBinaryOperation binary, IObjectCreationOperation creation)
    {
        if (Comparison(binary, creation) is not { } comparison)
        {
            return null;
        }

        var zero = IsZero(comparison.Limit);
        string? check = comparison.Kind switch
        {
            BinaryOperatorKind.LessThan => zero ? "IfNegative" : "IfLessThan",
            BinaryOperatorKind.LessThanOrEqual => zero ? "IfNegativeOrZero" : "IfLessThanOrEqualTo",
            BinaryOperatorKind.GreaterThan => zero ? "IfPositive" : "IfGreaterThan",
            BinaryOperatorKind.GreaterThanOrEqual => zero ? "IfPositiveOrZero" : "IfGreaterThanOrEqualTo",
            BinaryOperatorKind.Equals => zero ? "IfZero" : null,
            _ => null,
        };

        // NaN: a manual x < 0 lets NaN through, DotThrow's ordering checks reject it (IfZero
        // passes NaN, like x == 0). A limit must convert by its type, or T is not inferred.
        if (check is null
            || (comparison.Kind != BinaryOperatorKind.Equals && IsFloatingPoint(comparison.Value.Type))
            || (!zero && !LimitFits(comparison.Limit, comparison.Value.Type!)))
        {
            return null;
        }

        var arguments = zero ? ImmutableArray<IOperation>.Empty : ImmutableArray.Create(comparison.Limit);
        return new Condition(comparison.Value, check, arguments, ArgumentOutOfRangeException, ArgumentException);
    }

    // x < min || x > max (IfOutOfRange), x <= min || x >= max (IfOutOfRangeExclusive)
    private static Condition? MatchRange(IBinaryOperation or, IObjectCreationOperation creation)
    {
        if (or.LeftOperand is not IBinaryOperation leftBinary
            || or.RightOperand is not IBinaryOperation rightBinary
            || Comparison(leftBinary, creation) is not { } first
            || Comparison(rightBinary, creation) is not { } second
            || !SameValue(first.Value, second.Value)
            || IsFloatingPoint(first.Value.Type)
            || !first.Limit.HasNoSideEffects()
            || !second.Limit.HasNoSideEffects()
            || !LimitFits(first.Limit, first.Value.Type!)
            || !LimitFits(second.Limit, first.Value.Type!))
        {
            return null;
        }

        var (low, high) = first.Kind is BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
            ? (first, second)
            : (second, first);
        string check;
        if (low.Kind == BinaryOperatorKind.LessThan && high.Kind == BinaryOperatorKind.GreaterThan)
        {
            check = "IfOutOfRange";
        }
        else if (low.Kind == BinaryOperatorKind.LessThanOrEqual && high.Kind == BinaryOperatorKind.GreaterThanOrEqual)
        {
            check = "IfOutOfRangeExclusive";
        }
        else
        {
            return null;
        }

        // A reversed constant range throws in DotThrow, while the manual code fails every value.
        if (ConstantComparer.Compare(low.Limit.ConstantValue, high.Limit.ConstantValue) is > 0)
        {
            return null;
        }

        return new Condition(low.Value, check, ImmutableArray.Create(low.Limit, high.Limit), ArgumentOutOfRangeException, ArgumentException);
    }

    private static bool SameValue(IOperation first, IOperation second) =>
        SyntaxFactory.AreEquivalent(first.Syntax, second.Syntax)
        && ReferencedSymbol(first) is { } symbol
        && SymbolEqualityComparer.Default.Equals(symbol, ReferencedSymbol(second));

    private static ISymbol? ReferencedSymbol(IOperation operation) =>
        operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFieldReferenceOperation field => field.Field,
            IPropertyReferenceOperation property => property.Property,
            _ => null,
        };

    // A number comparison as "value OP limit", or null.
    private static ComparisonParts? Comparison(IBinaryOperation binary, IObjectCreationOperation creation)
    {
        if (binary.IsLifted
            || binary.OperatorKind is not (BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
                or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual or BinaryOperatorKind.Equals)
            || (binary.OperatorMethod is { } method && method.ContainingType?.SpecialType != SpecialType.System_Decimal))
        {
            return null;
        }

        var left = binary.LeftOperand.WithoutImplicitConversions();
        var right = binary.RightOperand.WithoutImplicitConversions();
        // The value is the side that is not a constant. When neither is, it is the side the
        // exception names, and the left side otherwise.
        bool leftIsValue;
        if (left.ConstantValue.HasValue)
        {
            leftIsValue = false;
        }
        else if (right.ConstantValue.HasValue)
        {
            leftIsValue = true;
        }
        else
        {
            leftIsValue = NamesOperand(creation, left) || !NamesOperand(creation, right);
        }

        var value = leftIsValue ? left : right;
        var limit = leftIsValue ? right : left;
        var kind = leftIsValue ? binary.OperatorKind : Flip(binary.OperatorKind);
        if (!IsNumber(value.Type) || !value.IsSimpleReference() || !limit.HasNoSideEffects())
        {
            return null;
        }

        return new ComparisonParts(value, limit, kind);
    }

    // The exception names this operand: nameof(x) or "x".
    private static bool NamesOperand(IObjectCreationOperation creation, IOperation operand) =>
        creation.Argument("paramName")?.Value.ConstantValue is { HasValue: true, Value: string name }
        && (name == operand.Syntax.ToString() || name == ReferencedSymbol(operand)?.Name);

    // The limit's type converts to the value's type (not only its constant value: for a byte
    // value, IfGreaterThan(200) cannot infer T from Guard<byte> and an int).
    private static bool LimitFits(IOperation limit, ITypeSymbol type) =>
        limit.Type is { } limitType
        && limit.SemanticModel is { } model
        && model.Compilation.ClassifyCommonConversion(limitType, type).IsImplicit;

    private static bool IsFloatingPoint(ITypeSymbol? type) =>
        type?.SpecialType is SpecialType.System_Single or SpecialType.System_Double;

    private static bool IsNumber(ITypeSymbol? type) =>
        type?.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Decimal;

    private static bool IsZero(IOperation operation) =>
        operation.ConstantValue is { HasValue: true, Value: { } value } && ConstantComparer.IsZero(value);

    private static BinaryOperatorKind Flip(BinaryOperatorKind kind) =>
        kind switch
        {
            BinaryOperatorKind.LessThan => BinaryOperatorKind.GreaterThan,
            BinaryOperatorKind.LessThanOrEqual => BinaryOperatorKind.GreaterThanOrEqual,
            BinaryOperatorKind.GreaterThan => BinaryOperatorKind.LessThan,
            BinaryOperatorKind.GreaterThanOrEqual => BinaryOperatorKind.LessThanOrEqual,
            _ => kind,
        };

    private sealed class Condition
    {
        public Condition(IOperation value, string check, ImmutableArray<IOperation> arguments, params string[] exceptions)
        {
            Value = value;
            Check = check;
            Arguments = arguments;
            Exceptions = ImmutableArray.Create(exceptions);
        }

        public IOperation Value { get; }

        public string Check { get; }

        public ImmutableArray<IOperation> Arguments { get; }

        // The exception types the manual code may throw so that callers still catch DotThrow's.
        public ImmutableArray<string> Exceptions { get; }
    }

    private sealed class ComparisonParts
    {
        public ComparisonParts(IOperation value, IOperation limit, BinaryOperatorKind kind)
        {
            Value = value;
            Limit = limit;
            Kind = kind;
        }

        public IOperation Value { get; }

        public IOperation Limit { get; }

        public BinaryOperatorKind Kind { get; }
    }
}
