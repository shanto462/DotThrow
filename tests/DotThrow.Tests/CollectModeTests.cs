using System;
using System.Linq;
using System.Reflection;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests;

public sealed class CollectModeTests
{
    public static TheoryData<CheckCase> Failures => Cases.Data(AllCases.Failures());

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_records_one_failure_with_its_error_code(CheckCase check) => check.AssertCollected();

    [Theory]
    [MemberData(nameof(Failures))]
    public void Inactive_guard_skips_the_check(CheckCase check) => check.AssertSkippedWhenInactive();

    [Fact]
    public void Every_error_code_is_the_check_name_without_If()
    {
        var codes = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (Name: f.Name, Value: (string)f.GetRawConstantValue()!))
            .ToList();
        var checks = typeof(GuardExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => m.Name)
            .Where(n => n.StartsWith("If", StringComparison.Ordinal) && n.Length > 2)
            .Select(n => n.Substring(2))
            .Distinct()
            .ToList();

        Assert.All(codes, code => Assert.Equal(code.Name, code.Value));

        // Every check has a code, except the ones that report "Null" or "Condition" by design.
        var exceptions = new[] { "NullOrEmpty", "NullOrWhiteSpace", string.Empty };
        Assert.All(
            checks.Where(c => !exceptions.Contains(c, StringComparer.Ordinal) && c != "Not"),
            check => Assert.Contains(check, codes.Select(c => c.Value)));
    }

    [Fact]
    public void Every_error_code_used_by_a_check_appears_in_the_case_tables()
    {
        var tested = AllCases.Failures().Select(c => c.ExpectedCode).ToHashSet(StringComparer.Ordinal);
#if NET8_0_OR_GREATER && !DOTTHROW_LEGACY_ASSET
        var unused = new[] { ErrorCodes.Must, ErrorCodes.Invalid };
#else
        // IfNotParsable exists only in the .NET 8 and later builds of the library.
        var unused = new[] { ErrorCodes.Must, ErrorCodes.Invalid, ErrorCodes.NotParsable };
#endif
        var codes = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(c => !unused.Contains(c, StringComparer.Ordinal));

        Assert.All(codes, code => Assert.Contains(code, tested));
    }

    [Fact]
    public void Report_collects_failures_of_many_values_in_order()
    {
        var name = "";
        var age = 200;
        var email = "ada.example.com";
        string? nickname = null;
        var tags = new[] { "a", "a" };
        var report = new ValidationReport();

        report.Check(name).IfNullOrWhiteSpace().IfLongerThan(50);
        report.Check(age).IfNegative().IfGreaterThan(150);
        report.Check(email).IfNullOrWhiteSpace().IfNotEmailAddress();
        report.Check(nickname).AllowNull().IfShorterThan(3);
        report.Check(tags).IfNullOrEmpty().IfHasDuplicates();

        Assert.False(report.IsValid);
        Assert.Collection(
            report.Failures,
            f => Assert.Equal(("name", ErrorCodes.WhiteSpace), (f.Name, f.ErrorCode)),
            f => Assert.Equal(("age", ErrorCodes.GreaterThan), (f.Name, f.ErrorCode)),
            f => Assert.Equal(("email", ErrorCodes.NotEmailAddress), (f.Name, f.ErrorCode)),
            f => Assert.Equal(("tags", ErrorCodes.HasDuplicates), (f.Name, f.ErrorCode)));
    }

    [Fact]
    public void Collect_mode_never_throws_for_a_failed_check()
    {
        var report = new ValidationReport();

        var exception = Record.Exception(() => report.Check((string?)null, "name").IfNull().IfNullOrEmpty().IfNotEmailAddress());

        Assert.Null(exception);
        Assert.Equal(ErrorCodes.Null, Assert.Single(report.Failures).ErrorCode);
    }

    [Fact]
    public void A_guard_from_a_report_converts_to_the_value()
    {
        var report = new ValidationReport();

        int age = report.Check(-1, "age").IfNegative();
        string name = report.Check("Ada Lovelace", "name").IfNullOrWhiteSpace();

        Assert.Equal(-1, age);
        Assert.Equal("Ada Lovelace", name);
        Assert.Single(report.Failures);
    }
}
