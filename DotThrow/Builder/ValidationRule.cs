using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Builder
{
    public class ValidationRule<T>
    {
        public Predicate<T> Predicate { get; set; }
        public string ExceptionMessage { get; set; }
        public Type ExceptionType { get; set; }

        public ValidationRule(Predicate<T> predicate, string exceptionMessage, Type exceptionType)
        {
            Predicate = predicate;
            ExceptionMessage = exceptionMessage;
            ExceptionType = exceptionType;
        }
    }
}
