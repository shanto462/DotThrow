﻿using DotThrow.Builder.Report;
using DotThrow.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotThrow.Builder
{
    public static class Validator<T>
    {
        private static readonly ConcurrentDictionary<Type, ValidatorRuleList<T>> _validationRulesByType = new();

        public static void AddRule(Predicate<T> predicate, string message, Type exceptionType, string passMessage = "")
        {
            var type = typeof(T);
            if (!_validationRulesByType.ContainsKey(type))
            {
                var ruleList = new ValidatorRuleList<T>();
                ruleList.AddRule(GetValidationRule(predicate, message, exceptionType, passMessage));
                _validationRulesByType.TryAdd(type, ruleList);
            }
            else
            {
                _validationRulesByType[type].AddRule(GetValidationRule(predicate, message, exceptionType, passMessage));
            }
        }

        private static ValidatorRule<T> GetValidationRule(Predicate<T> predicate, string message, Type exceptionType, string passMessage)
        {
            return new ValidatorRule<T>(predicate, message, exceptionType, passMessage);
        }

        public static void Verify(T target)
        {
            var type = typeof(T);
            if (_validationRulesByType.ContainsKey(type))
            {
                _validationRulesByType[type].Verify(target);
            }
        }

        public static IEnumerable<ValidatorReport<T>> VerifyNotThrow(T target)
        {
            var type = typeof(T);
            if (_validationRulesByType.ContainsKey(type))
            {
                return _validationRulesByType[type].VerifyNotThrow(target);
            }
            return Enumerable.Empty<ValidatorReport<T>>();
        }
    }
}
