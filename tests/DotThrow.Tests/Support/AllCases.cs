using System.Collections.Generic;
using System.Linq;
using DotThrow.Tests.Guards;

namespace DotThrow.Tests.Support;

/// <summary>Every case table of every area, for tests that apply to all checks.</summary>
public static class AllCases
{
    public static IEnumerable<CheckCase> Failures() =>
        GeneralGuardTests.FailureCases()
            .Concat(ComparisonGuardTests.FailureCases())
            .Concat(StringGuardTests.FailureCases())
            .Concat(CollectionGuardTests.FailureCases())
            .Concat(ValueGuardTests.FailureCases())
            .Concat(DateTimeGuardTests.FailureCases())
            .Concat(NumberGuardTests.FailureCases())
            .Concat(TextGuardTests.FailureCases())
            .Concat(CalendarGuardTests.FailureCases())
            .Concat(DictionaryGuardTests.FailureCases())
            .Concat(NetworkGuardTests.FailureCases())
            .Concat(ObjectGuardTests.FailureCases())
            .Concat(CollectionShapeGuardTests.FailureCases())
            .Concat(ComparisonMatrixGuardTests.FailureCases());

    public static IEnumerable<CheckCase> Passes() =>
        GeneralGuardTests.PassCases()
            .Concat(ComparisonGuardTests.PassCases())
            .Concat(StringGuardTests.PassCases())
            .Concat(CollectionGuardTests.PassCases())
            .Concat(ValueGuardTests.PassCases())
            .Concat(DateTimeGuardTests.PassCases())
            .Concat(NumberGuardTests.PassCases())
            .Concat(TextGuardTests.PassCases())
            .Concat(CalendarGuardTests.PassCases())
            .Concat(DictionaryGuardTests.PassCases())
            .Concat(NetworkGuardTests.PassCases())
            .Concat(ObjectGuardTests.PassCases())
            .Concat(CollectionShapeGuardTests.PassCases())
            .Concat(ComparisonMatrixGuardTests.PassCases());
}
