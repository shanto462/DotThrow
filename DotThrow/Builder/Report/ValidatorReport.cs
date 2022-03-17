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

        public bool IsGrouped => !string.IsNullOrWhiteSpace(Group);

        public string Group { get; }

        public ValidatorReport(bool passed, ValidatorRule<T> rule, string group = "")
        {
            IsPassed = passed;
            Rule = rule;
            Group = group;
        }

        public override string ToString()
        {
            return IsPassed ?
                $"Passed: {(string.IsNullOrWhiteSpace(Rule.PassedMessage) ? "No Exception!" : Rule.PassedMessage)} {GetGroupString()}"
                :
                $"Failed: {Rule.ExceptionMessage} {GetGroupString()}";
        }

        private string GetGroupString()
        {
            return string.IsNullOrWhiteSpace(Group) ? "" : $"Group : {Group}";
        }
    }
}
