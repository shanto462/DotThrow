using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#else
using System.Collections.Generic;
#endif

namespace DotThrow.Internal;

// Enum facts, computed once per enum type the first time a check needs them. No
// reflection beyond Enum.GetValues/GetNames and the [Flags] attribute, so it is AOT safe.
internal static class Enums
{
    public static bool IsDefined<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        if (EnumFlags<TEnum>.IsFlags)
        {
            // Any combination of defined flags, including 0 (no flags), like FluentValidation.
            return (ToBits(value) & ~EnumFlags<TEnum>.AllBits) == 0;
        }

#if NET5_0_OR_GREATER
        return Enum.IsDefined(value);
#else
        return Enum.IsDefined(typeof(TEnum), value);
#endif
    }

    public static bool IsName<TEnum>(string value, bool ignoreCase)
        where TEnum : struct, Enum =>
        ignoreCase ? EnumNames<TEnum>.IgnoreCase.Contains(value) : EnumNames<TEnum>.Exact.Contains(value);

    // The raw bits of an enum value, zero-extended to 64 bits. On .NET 8 and later this is a
    // reinterpretation that the JIT reduces to a register move; older frameworks box.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToBits<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
#if NET8_0_OR_GREATER
        return Unsafe.SizeOf<TEnum>() switch
        {
            1 => Unsafe.As<TEnum, byte>(ref value),
            2 => Unsafe.As<TEnum, ushort>(ref value),
            4 => Unsafe.As<TEnum, uint>(ref value),
            _ => Unsafe.As<TEnum, ulong>(ref value),
        };
#else
        return EnumFlags<TEnum>.Size switch
        {
            1 => (byte)Convert.ToInt64(value, null),
            2 => (ushort)Convert.ToInt64(value, null),
            4 => (uint)Convert.ToInt64(value, null),
            _ => EnumFlags<TEnum>.Unsigned ? Convert.ToUInt64(value, null) : (ulong)Convert.ToInt64(value, null),
        };
#endif
    }

    private static class EnumFlags<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly bool IsFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);

#if !NET8_0_OR_GREATER
        public static readonly int Size = Type.GetTypeCode(typeof(TEnum)) switch
        {
            TypeCode.SByte or TypeCode.Byte => 1,
            TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 => 4,
            _ => 8,
        };

        public static readonly bool Unsigned = Type.GetTypeCode(typeof(TEnum)) == TypeCode.UInt64;
#endif

        public static readonly ulong AllBits = Combine();

        private static ulong Combine()
        {
            var bits = 0UL;
#if NET5_0_OR_GREATER
            foreach (var value in Enum.GetValues<TEnum>())
            {
                bits |= ToBits(value);
            }
#else
            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                bits |= ToBits(value);
            }
#endif
            return bits;
        }
    }

    private static class EnumNames<TEnum>
        where TEnum : struct, Enum
    {
#if NET5_0_OR_GREATER
        private static readonly string[] Names = Enum.GetNames<TEnum>();
#else
        private static readonly string[] Names = Enum.GetNames(typeof(TEnum));
#endif

#if NET8_0_OR_GREATER
        public static readonly FrozenSet<string> Exact = Names.ToFrozenSet(StringComparer.Ordinal);
        public static readonly FrozenSet<string> IgnoreCase = Names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
#else
        public static readonly HashSet<string> Exact = new(Names, StringComparer.Ordinal);
        public static readonly HashSet<string> IgnoreCase = new(Names, StringComparer.OrdinalIgnoreCase);
#endif
    }
}
