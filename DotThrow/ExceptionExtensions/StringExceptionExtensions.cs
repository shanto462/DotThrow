using DotThrow.Helpers;
using DotThrow.Throwble;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.ExceptionExtensions
{
    public static class StringExceptionExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfNullOrWhiteSpace(this in ExceptionThrower<string> thrower)
        {
            if (string.IsNullOrWhiteSpace(thrower.Value))
                throw new ArgumentNullException("Value is Null or White space!");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfNullOrEmpty(this in ExceptionThrower<string> thrower)
        {
            if (string.IsNullOrEmpty(thrower.Value))
                throw new ArgumentNullException("Value is Null or Empty!");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfAllUpper(this in ExceptionThrower<string> thrower)
        {
            if (thrower.Value.Count(c => char.IsUpper(c)) == thrower.Value.Length)
                throw new Exception("All character's are in uppercase");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfAllLower(this in ExceptionThrower<string> thrower)
        {
            if (thrower.Value.Count(c => char.IsLower(c)) == thrower.Value.Length)
                throw new Exception("All character's are in lowercase");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIf(this in ExceptionThrower<string> thrower, Predicate<string> predicate, string message = "")
        {
            if (predicate.Invoke(thrower.Value))
                throw new Exception(string.IsNullOrWhiteSpace(message) ? "Condition matched!" : message);
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfNot(this in ExceptionThrower<string> thrower, Predicate<string> predicate)
        {
            if (!predicate.Invoke(thrower.Value))
                throw new Exception("Condition failed!");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfNotValidJson(this in ExceptionThrower<string> thrower)
        {
            if (!JsonHelpers.IsValidJson(thrower.Value))
                throw new Exception("Invalid Json!");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfNotValidXml(this in ExceptionThrower<string> thrower)
        {
            if (!XmlHelpers.IsValidXml(thrower.Value))
                throw new Exception("Invalid Xml!");
            return ref thrower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ExceptionThrower<string> ThrowIfNotValidInteger(this in ExceptionThrower<string> thrower)
        {
            if (!int.TryParse(thrower.Value, out int _))
                throw new Exception("Not a valid integer!");
            return ref thrower;
        }
    }
}
