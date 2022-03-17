using DotThrow.Throwble;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.ExceptionExtensions
{
    public static class ExceptionCreationExtensions
    {
        public static ExceptionThrower<TValue> CreateThrower<TValue>(this TValue obj)
        {
            return new ExceptionThrower<TValue>(obj);
        }
    }
}
