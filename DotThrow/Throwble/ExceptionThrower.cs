using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Throwble
{
    public struct ExceptionThrower<T>
    {
        public T Value { get; }

        public ExceptionThrower(T value)
        {
            this.Value = value;
        }
    }
}
