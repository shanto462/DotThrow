using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace DotThrow.Internal;

// FluentValidation NotEmpty semantics for a value that is not null: a string that is
// empty or white space, a collection without items, a seekable stream of length 0, or the
// default value of a value type.
internal static class Emptiness
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty<T>(T value)
    {
        if (typeof(T) == typeof(string))
        {
            return string.IsNullOrWhiteSpace((string)(object)value!);
        }

        if (typeof(T).IsValueType)
        {
            // Value-type collections such as ImmutableArray<T> are empty by count too.
            return EqualityComparer<T>.Default.Equals(value, default!) || (value is ICollection collection && collection.Count == 0);
        }

        return IsEmptyObject(value!);
    }

    private static bool IsEmptyObject(object value) => value switch
    {
        string text => string.IsNullOrWhiteSpace(text),
        ICollection collection => collection.Count == 0,
        IEnumerable sequence => Collections.CountUpTo(sequence, 1) == 0,
        Stream stream => stream.CanSeek && stream.Length == 0,
        _ => false,
    };
}
