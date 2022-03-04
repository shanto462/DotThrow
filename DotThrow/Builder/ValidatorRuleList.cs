using DotThrow.Builder.Report;
using DotThrow.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Builder
{
    public class ValidatorRuleList<T>
    {
        private readonly ConcurrentBag<ValidatorRule<T>> _rules = new();

        public void AddRule(ValidatorRule<T> validationRule)
        {
            _rules.Add(validationRule);
        }

        public void Verify(T target)
        {
            foreach (var rule in _rules)
            {
                if (rule.Predicate.Invoke(target))
                {
                    var obj = Activator.CreateInstance(rule.ExceptionType, new object[] { rule.ExceptionMessage });

                    if (obj == null)
                    {
                        throw new Exception(rule.ExceptionMessage);
                    }
                    else
                    {
                        throw ObjectExtensions.CastToReflected(
                                obj,
                                rule.ExceptionType
                        );
                    }
                }
            }
        }

        public IEnumerable<ValidatorReport<T>> VerifyNotThrow(T target)
        {
            foreach (var rule in _rules)
            {
                yield return new ValidatorReport<T>(!rule.Predicate.Invoke(target), rule);
            }
        }
    }
}
