using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Builder
{
    public class ValidatorRule<T>
    {
        public Predicate<T> Predicate { get; }
        public string ExceptionMessage { get; }
        public string PassedMessage { get; }
        public Type ExceptionType { get; }

        public ValidatorRule(Predicate<T> predicate, string exceptionMessage, Type exceptionType, string passedMessage = "")
        {
            Predicate = predicate;
            ExceptionMessage = exceptionMessage;
            ExceptionType = exceptionType;
            PassedMessage = passedMessage;
        }
    }
}
