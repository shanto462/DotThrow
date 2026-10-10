using System.Collections.Generic;
using System.Linq;

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
            .Concat(DateTimeGuardTests.FailureCases());

    public static IEnumerable<CheckCase> Passes() =>
        GeneralGuardTests.PassCases()
            .Concat(ComparisonGuardTests.PassCases())
            .Concat(StringGuardTests.PassCases())
            .Concat(CollectionGuardTests.PassCases())
            .Concat(ValueGuardTests.PassCases())
            .Concat(DateTimeGuardTests.PassCases());
}
