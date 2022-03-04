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
        private readonly ConcurrentDictionary<string, List<ValidatorRule<T>>> _groupedRules = new();

        public void AddRule(ValidatorRule<T> validationRule, string group = "")
        {
            if (string.IsNullOrWhiteSpace(group))
                _rules.Add(validationRule);
            else
            {
                AddGroupRule(validationRule, group);
            }
        }

        private void AddGroupRule(ValidatorRule<T> validationRule, string group)
        {
            if (!_groupedRules.ContainsKey(group))
                _groupedRules.TryAdd(group, new() { validationRule });
            else
            {
                var ruleList = _groupedRules[group];
                lock (ruleList)
                    ruleList.Add(validationRule);
            }
        }

        public void Verify(T target, string group = "")
        {
            if (string.IsNullOrWhiteSpace(group))
                VerifyDefaultRuleList(target);
            else
            {
                VerifyGroupRuleList(target, group);
            }
        }

        private void VerifyGroupRuleList(T target, string group)
        {
            if (_groupedRules.ContainsKey(group))
            {
                var ruleList = _groupedRules[group];
                lock (ruleList)
                {
                    foreach (var rule in ruleList)
                    {
                        ThrowIfRuleMatches(target, rule, group);
                    }
                }
            }
        }

        private void VerifyDefaultRuleList(T target)
        {
            foreach (var rule in _rules)
            {
                ThrowIfRuleMatches(target, rule);
            }
        }

        private static void ThrowIfRuleMatches(T target, ValidatorRule<T> rule, string group = "")
        {
            if (rule.Predicate.Invoke(target))
            {
                var message = rule.ExceptionMessage + (string.IsNullOrWhiteSpace(group) ? string.Empty : $" : Rule Group -> {group}");

                var obj = Activator.CreateInstance(rule.ExceptionType, new object[] { message });

                if (obj == null)
                {
                    throw new Exception(message);
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

        public IEnumerable<ValidatorReport<T>> VerifyNotThrow(T target, string group = "")
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                foreach (var rule in _rules)
                {
                    yield return new ValidatorReport<T>(!rule.Predicate.Invoke(target), rule);
                }
            }
            else
            {
                if (_groupedRules.ContainsKey(group))
                {
                    var ruleList = _groupedRules[group];
                    lock (ruleList)
                    {
                        foreach (var rule in ruleList)
                        {
                            yield return new ValidatorReport<T>(!rule.Predicate.Invoke(target), rule, group);
                        }
                    }
                }
            }
        }
    }
}
