using DotThrow.Throwble;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.ExceptionExtensions
{
    public static class DatetimeExceptionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<DateTime> ThrowIfOlderThan(this in ExceptionThrower<DateTime> thrower, DateTime dateTime)
        {
            if (thrower.Value < dateTime)
                throw new Exception($"Value is older than {dateTime.ToString("dd/MM/yyyy HH:mm")}");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<DateTime> ThrowIfAfterThan(this in ExceptionThrower<DateTime> thrower, DateTime dateTime)
        {
            if (thrower.Value > dateTime)
                throw new Exception($"Value is after than {dateTime.ToString("dd/MM/yyyy HH:mm")}");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<DateTime> ThrowIf(this in ExceptionThrower<DateTime> thrower, Predicate<DateTime> predicate, string message = "")
        {
            if (predicate(thrower.Value))
                throw new Exception(string.IsNullOrWhiteSpace(message) ? "Condition matched!" : message);
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<DateTime> ThrowIfOlderThanNow(this in ExceptionThrower<DateTime> thrower)
        {
            thrower.Value.CreateThrower().ThrowIfOlderThan(DateTime.Now);
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<DateTime> ThrowIfAfterThanNow(this in ExceptionThrower<DateTime> thrower)
        {
            thrower.Value.CreateThrower().ThrowIfAfterThan(DateTime.Now);
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<DateTime> ThrowIfEquals(this in ExceptionThrower<DateTime> thrower, DateTime dateTime)
        {
            if (dateTime == thrower.Value)
                throw new Exception($"Value is equal to {dateTime.ToString("dd/MM/yyyy")}");
            return ref thrower;
        }
    }
}
