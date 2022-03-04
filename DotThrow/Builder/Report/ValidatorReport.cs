using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Builder.Report
{
    public class ValidatorReport<T>
    {
        public bool IsPassed { get; }
        public ValidatorRule<T> Rule { get; }

        public ValidatorReport(bool passed, ValidatorRule<T> rule)
        {
            IsPassed = passed;
            Rule = rule;
        }

        public override string ToString()
        {
            return IsPassed ? $"Passed: {(string.IsNullOrWhiteSpace(Rule.PassedMessage) ? "No Exception!" : Rule.PassedMessage)}" : $"Failed: {Rule.ExceptionMessage}";
        }
    }
}
