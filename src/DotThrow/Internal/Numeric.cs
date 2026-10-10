using System.Runtime.CompilerServices;

namespace DotThrow.Internal;

internal static class Numeric
{
    // NaN has no order, so every ordering check (greater, less, range, sign) rejects it.
    // The typeof checks are constants for the JIT, so for other types this is free and
    // the casts through object do not box on .NET 8 and later.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN<T>(T value)
    {
        if (typeof(T) == typeof(double))
        {
            return double.IsNaN((double)(object)value!);
        }

        if (typeof(T) == typeof(float))
        {
            return float.IsNaN((float)(object)value!);
        }

#if NET8_0_OR_GREATER
        if (typeof(T) == typeof(System.Half))
        {
            return System.Half.IsNaN((System.Half)(object)value!);
        }
#endif

        return false;
    }
}
