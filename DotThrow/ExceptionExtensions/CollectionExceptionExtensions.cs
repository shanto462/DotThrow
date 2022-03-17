using DotThrow.Extensions;
using DotThrow.Throwble;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.ExceptionExtensions
{
    public static class CollectionExceptionExtensions
    {
        public static ref readonly ExceptionThrower<TValue> ThrowIfEmpty<TValue>(this in ExceptionThrower<TValue> thrower) where TValue : IEnumerable
        {
            if (!thrower.Value.Any())
                throw new Exception($"Collection is empty!");
            return ref thrower;
        }

        public static ref readonly ExceptionThrower<TValue> ThrowIf<TValue>(this in ExceptionThrower<TValue> thrower, Predicate<TValue> predicate, string message = "") where TValue : IEnumerable
        {
            if (predicate.Invoke(thrower.Value))
                throw new Exception(string.IsNullOrWhiteSpace(message) ? "Condition matched!" : message);
            return ref thrower;
        }

        public static ref readonly ExceptionThrower<TValue> ThrowIfCountIsGreaterThan<TValue>(this in ExceptionThrower<TValue> thrower, int count) where TValue : IEnumerable
        {
            if (thrower.Value.Count() > count)
                throw new Exception($"Collection has item count greater than {count}");
            return ref thrower;
        }

        public static ref readonly ExceptionThrower<TValue> ThrowIfCountIsLessThan<TValue>(this in ExceptionThrower<TValue> thrower, int count) where TValue : IEnumerable
        {
            if (thrower.Value.Count() < count)
                throw new Exception($"Collection has item count less than {count}");
            return ref thrower;
        }

        public static ref readonly ExceptionThrower<TValue> ThrowIfCountIsEqualTo<TValue>(this in ExceptionThrower<TValue> thrower, int count) where TValue : IEnumerable
        {
            if (thrower.Value.Count() == count)
                throw new Exception($"Collection has item count less than {count}");
            return ref thrower;
        }
    }
}
