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
        public static ExceptionThrower<T> CreateThrower<T>(this T obj)
        {
            return new ExceptionThrower<T>(obj);
        }
    }
}
