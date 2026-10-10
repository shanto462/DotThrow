using System;
using System.Collections;
using System.Collections.Generic;

namespace DotThrow.Testing;

internal sealed class TestValidationContinuation : ITestValidationWith
{
    private readonly IReadOnlyList<ValidationFailure> _all;
    private readonly List<Func<ValidationFailure, bool>> _predicates = [];

    public TestValidationContinuation(IReadOnlyList<ValidationFailure> failures, TestValidationContinuation? parent = null)
    {
        _all = failures;
        Parent = parent;
    }

    public TestValidationContinuation? Parent { get; }

    public IEnumerable<ValidationFailure> MatchedFailures => Matched();

    public IEnumerable<ValidationFailure> UnmatchedFailures => Unmatched();

    public void Add(Func<ValidationFailure, bool> predicate) => _predicates.Add(predicate);

    public IEnumerator<ValidationFailure> GetEnumerator() => Matched().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal List<ValidationFailure> Matched()
    {
        var result = new List<ValidationFailure>();
        foreach (var failure in _all)
        {
            if (MatchesAll(failure))
            {
                result.Add(failure);
            }
        }

        return result;
    }

    internal List<ValidationFailure> Unmatched()
    {
        var result = new List<ValidationFailure>();
        foreach (var failure in _all)
        {
            if (!MatchesAll(failure))
            {
                result.Add(failure);
            }
        }

        return result;
    }

    private bool MatchesAll(ValidationFailure failure)
    {
        foreach (var predicate in _predicates)
        {
            if (!predicate(failure))
            {
                return false;
            }
        }

        return true;
    }
}
