using DotThrow.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Builder
{
    public static class Validation<T>
    {
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<ValidationRule<T>, byte>> _validationRuleList = new();

        public static void AddRule(Predicate<T> predicate, string message, Type exceptionType)
        {
            var type = typeof(T);
            if (!_validationRuleList.ContainsKey(type))
            {
                _validationRuleList.TryAdd(type, new ConcurrentDictionary<ValidationRule<T>, byte>()
                {
                    [new ValidationRule<T>(predicate, message, exceptionType)] = new byte()
                });
            }
            else
            {
                _validationRuleList[type].TryAdd(new ValidationRule<T>(predicate, message, exceptionType), new byte());
            }
        }

        public static void Verify(T target)
        {
            var type = typeof(T);
            if (_validationRuleList.ContainsKey(type))
            {
                var rules = _validationRuleList[type];
                foreach (var rule in rules)
                {
                    if (rule.Key.Predicate.Invoke(target))
                    {
                        var obj = Activator.CreateInstance(rule.Key.ExceptionType, new object[] { rule.Key.ExceptionMessage });

                        if (obj == null)
                        {
                            throw new Exception(rule.Key.ExceptionMessage);
                        }
                        else
                        {
                            throw ObjectExtensions.CastToReflected(
                                    obj,
                                    rule.Key.ExceptionType
                            );
                        }
                    }
                }
            }
        }
    }
}
