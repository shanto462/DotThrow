using System;
using System.Collections.Generic;

namespace DotThrow.Dynamic.Internal;

// Checks every type has: Null, NotNull, Default, EqualTo, NotEqualTo, OneOf, NotOneOf,
// Empty, NotEmpty, Type, NotType. Subclasses add the checks of their type and may replace
// these (for example string EqualTo with a comparison).
internal class ValueFamily<TValue>(Func<string, TValue>? parser) : CheckFamily<TValue>
{
    protected Func<string, TValue>? Parser { get; } = parser;

    public sealed override bool TryCreate(string name, CheckArgs args, out GuardStep<TValue>? step) =>
        TryCreateSpecific(name, args, out step) || TryCreateGeneral(name, args, out step);

    protected virtual bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        step = null;
        return false;
    }

    private bool TryCreateGeneral(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.Null:
                step = g => g.IfNull(message);
                return true;
            case CheckNames.NotNull:
                step = g => g.IfNotNull(message);
                return true;
            case CheckNames.Default:
                step = g => g.IfDefault(message);
                return true;
            case CheckNames.Empty:
                step = g => g.IfEmpty(message);
                return true;
            case CheckNames.NotEmpty:
                step = g => g.IfNotEmpty(message);
                return true;
            case CheckNames.EqualTo:
                if (args.TryParse(Arg.Value, Parser, out var equal))
                {
                    step = g => g.IfEqualTo(equal, null, message);
                }

                return true;
            case CheckNames.NotEqualTo:
                if (args.TryParse(Arg.Value, Parser, out var notEqual))
                {
                    step = g => g.IfNotEqualTo(notEqual, null, message);
                }

                return true;
            case CheckNames.OneOf:
                if (args.TryParseList(Parser, out var rejected))
                {
                    step = g => g.IfOneOf((IEnumerable<TValue>)rejected, null, message);
                }

                return true;
            case CheckNames.NotOneOf:
                if (args.TryParseList(Parser, out var allowed))
                {
                    step = g => g.IfNotOneOf((IEnumerable<TValue>)allowed, null, message);
                }

                return true;
            case CheckNames.Type:
                if (args.Context.Types.ResolveType(args, Arg.Value) is { } type)
                {
                    step = g => g.IfType(type, message);
                }

                return true;
            case CheckNames.NotType:
                if (args.Context.Types.ResolveType(args, Arg.Value) is { } notType)
                {
                    step = g => g.IfNotType(notType, message);
                }

                return true;
            default:
                return false;
        }
    }
}

// GreaterThan, GreaterThanOrEqualTo, LessThan, LessThanOrEqualTo, OutOfRange,
// OutOfRangeExclusive and InRange for any IComparable<T>.
internal class ComparableFamily<TValue>(Func<string, TValue>? parser) : ValueFamily<TValue>(parser)
    where TValue : IComparable<TValue>
{
    protected override bool TryCreateSpecific(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.GreaterThan:
                if (Limit(args, Arg.Value, out var greater))
                {
                    step = g => g.IfGreaterThan(greater, message);
                }

                return true;
            case CheckNames.GreaterThanOrEqualTo:
                if (Limit(args, Arg.Value, out var greaterOrEqual))
                {
                    step = g => g.IfGreaterThanOrEqualTo(greaterOrEqual, message);
                }

                return true;
            case CheckNames.LessThan:
                if (Limit(args, Arg.Value, out var less))
                {
                    step = g => g.IfLessThan(less, message);
                }

                return true;
            case CheckNames.LessThanOrEqualTo:
                if (Limit(args, Arg.Value, out var lessOrEqual))
                {
                    step = g => g.IfLessThanOrEqualTo(lessOrEqual, message);
                }

                return true;
            case CheckNames.OutOfRange:
                if (TryRange(args, out var min, out var max))
                {
                    step = g => g.IfOutOfRange(min, max, message);
                }

                return true;
            case CheckNames.OutOfRangeExclusive:
                if (TryRange(args, out var exclusiveMin, out var exclusiveMax))
                {
                    step = g => g.IfOutOfRangeExclusive(exclusiveMin, exclusiveMax, message);
                }

                return true;
            case CheckNames.InRange:
                if (TryRange(args, out var rejectedMin, out var rejectedMax))
                {
                    step = g => g.IfInRange(rejectedMin, rejectedMax, message);
                }

                return true;
            default:
                return TryCreateMore(name, args, out step);
        }
    }

    protected virtual bool TryCreateMore(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        step = null;
        return false;
    }

    // An ordering limit: NaN has no order, so the guards reject it; here it is a definition error.
    private bool Limit(CheckArgs args, Arg arg, out TValue value)
    {
        if (!args.TryParse(arg, Parser, out value))
        {
            return false;
        }

        if (DotThrow.Internal.Numeric.IsNaN(value))
        {
            args.Error($"'{arg}' must not be NaN");
            return false;
        }

        return true;
    }

    private bool TryRange(CheckArgs args, out TValue min, out TValue max)
    {
        var hasMin = Limit(args, Arg.Min, out min);
        var hasMax = Limit(args, Arg.Max, out max);
        if (hasMin && hasMax && min!.CompareTo(max) > 0)
        {
            args.Error("'Min' is greater than 'Max'");
            return false;
        }

        return hasMin && hasMax;
    }
}

// Adds Negative, NegativeOrZero, Positive, PositiveOrZero and Zero for value types.
internal class StructComparableFamily<TValue>(Func<string, TValue>? parser) : ComparableFamily<TValue>(parser)
    where TValue : struct, IComparable<TValue>
{
    protected sealed override bool TryCreateMore(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        var message = args.Message;
        step = null;
        switch (name)
        {
            case CheckNames.Negative:
                step = g => g.IfNegative(message);
                return true;
            case CheckNames.NegativeOrZero:
                step = g => g.IfNegativeOrZero(message);
                return true;
            case CheckNames.Positive:
                step = g => g.IfPositive(message);
                return true;
            case CheckNames.PositiveOrZero:
                step = g => g.IfPositiveOrZero(message);
                return true;
            case CheckNames.Zero:
                step = g => g.IfZero(message);
                return true;
            default:
                return TryCreateType(name, args, out step);
        }
    }

    // Checks of one concrete type (int, double, DateTime...).
    protected virtual bool TryCreateType(string name, CheckArgs args, out GuardStep<TValue>? step)
    {
        step = null;
        return false;
    }
}

// Nullable<T>: the general checks see the nullable value; the checks of T unwrap it, and a
// null value fails them with "Null" (decision D6), like a null string fails a string check.
internal sealed class NullableFamily<TValue>(CheckFamily<TValue> inner) : CheckFamily<TValue?>
    where TValue : struct
{
    private readonly ValueFamily<TValue?> _general = new(ValueText.Parser<TValue?>());

    public override bool TryCreate(string name, CheckArgs args, out GuardStep<TValue?>? step)
    {
        if (_general.TryCreate(name, args, out step))
        {
            return true;
        }

        if (!inner.TryCreate(name, args, out var innerStep))
        {
            return false;
        }

        if (innerStep is null)
        {
            step = null;
            return true;
        }

        var message = args.Message;
        step = g =>
        {
            var value = g.Value;
            if (!value.HasValue)
            {
                return g.FailNull(message);
            }

            return innerStep(g.Map(value.GetValueOrDefault())).Map(value);
        };
        return true;
    }
}
