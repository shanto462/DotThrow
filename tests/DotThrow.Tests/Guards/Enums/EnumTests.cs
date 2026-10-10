using System;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

public sealed class EnumTests
{
    [Flags]
    public enum Access : byte
    {
        Read = 1,
        Write = 2,
        Execute = 4,
    }

    [Flags]
    public enum Wide : ulong
    {
        None = 0,
        Low = 1,
        High = 1UL << 63,
    }

    public enum Balance : sbyte
    {
        Minus = -1,
        Zero = 0,
    }

    [Fact]
    public void IfNotDefined_accepts_any_combination_of_defined_flags()
    {
        Check.Passes(Access.Read | Access.Write | Access.Execute, g => g.IfNotDefined());
        Check.Passes((Access)0, g => g.IfNotDefined());
        Check.Fails<ArgumentOutOfRangeException, Access>((Access)8, g => g.IfNotDefined(), ErrorCodes.NotDefined, "'input' must be a defined Access value.");
        Check.Fails<ArgumentOutOfRangeException, Access>((Access)9, g => g.IfNotDefined(), ErrorCodes.NotDefined);
        Check.Passes(Wide.High | Wide.Low, g => g.IfNotDefined());
        Check.Fails<ArgumentOutOfRangeException, Wide>((Wide)2, g => g.IfNotDefined(), ErrorCodes.NotDefined);
    }

    [Fact]
    public void IfNotDefined_for_plain_enums_needs_a_named_value()
    {
        Check.Passes(DayOfWeek.Friday, g => g.IfNotDefined());
        Check.Fails<ArgumentOutOfRangeException, DayOfWeek>((DayOfWeek)7, g => g.IfNotDefined(), ErrorCodes.NotDefined);
        Check.Passes(Balance.Minus, g => g.IfNotDefined());
        Check.Fails<ArgumentOutOfRangeException, Balance>((Balance)(-2), g => g.IfNotDefined(), ErrorCodes.NotDefined);
    }

    [Fact]
    public void IfHasFlag_and_IfNotHasFlag()
    {
        var access = Access.Read | Access.Write;
        Check.Fails<ArgumentOutOfRangeException, Access>(access, g => g.IfHasFlag(Access.Write), ErrorCodes.HasFlag, "'input' must not have the flag Write.");
        Check.Passes(access, g => g.IfHasFlag(Access.Execute));
        Check.Passes(access, g => g.IfHasFlag(Access.Write | Access.Execute));
        Check.Fails<ArgumentOutOfRangeException, Access>(access, g => g.IfNotHasFlag(Access.Execute), ErrorCodes.NotHasFlag, "'input' must have the flag Execute.");
        Check.Passes(access, g => g.IfNotHasFlag(Access.Read | Access.Write));
        Check.Fails<ArgumentOutOfRangeException, Access>(access, g => g.IfHasFlag((Access)0), ErrorCodes.HasFlag);
        Check.Fails<ArgumentOutOfRangeException, Wide>(Wide.High, g => g.IfHasFlag(Wide.High), ErrorCodes.HasFlag);
        Assert.Equal("'input' must not have the flag Read, Write.", Check.MessageOf(access, g => g.IfHasFlag(access)));
    }
}
