using DotThrow.Throwble;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.ExceptionExtensions
{
    public static class IntExceptionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<int> ThrowIfZero(this in ExceptionThrower<int> thrower)
        {
            if (thrower.Value == 0)
                throw new ArgumentException($"{thrower.Value} is equal to zero");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<int> ThrowIfLessThanZero(this in ExceptionThrower<int> thrower)
        {
            if (thrower.Value < 0)
                throw new ArgumentException($"{thrower.Value} is less than zero");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<int> ThrowIfLessThanOrEqualZero(this in ExceptionThrower<int> thrower)
        {
            if (thrower.Value <= 0)
                throw new ArgumentException($"{thrower.Value} is less than or equal to zero");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<int> ThrowIfGreaterThanZero(this in ExceptionThrower<int> thrower)
        {
            if (thrower.Value > 0)
                throw new ArgumentException($"{thrower.Value} is greater than zero");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<int> ThrowIfGreaterThanOrEqualZero(this in ExceptionThrower<int> thrower)
        {
            if (thrower.Value < 0)
                throw new ArgumentException($"{thrower.Value} is greater than or equal to zero");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<int> ThrowIf(this in ExceptionThrower<int> thrower, Predicate<int> predicate, string message = "")
        {
            if (predicate.Invoke(thrower.Value))
                throw new Exception(string.IsNullOrWhiteSpace(message) ? "Condition matched!" : message);
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<int> ThrowIfEqual(this in ExceptionThrower<int> thrower, int value)
        {
            if (thrower.Value == value)
                throw new Exception($"Value is equal to {value}");
            return ref thrower;
        }
    }
}
