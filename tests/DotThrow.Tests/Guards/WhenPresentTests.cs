using System;
using System.Linq;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Guards;

// WhenPresent(): a null value skips the rest of the chain; a value is checked as usual. On a
// Nullable<T> it also unwraps to Guard<T>, so number checks follow directly.
public sealed class WhenPresentTests
{
    [Fact]
    public void Null_reference_skips_the_rest_of_the_chain()
    {
        string? nickname = null;
        var report = new ValidationReport();

        var thrown = nickname.Throw().WhenPresent().IfNullOrEmpty().IfLongerThan(3);
        var collected = report.Check(nickname).WhenPresent().IfShorterThan(2);

        Assert.False(thrown.IsActive);
        Assert.False(thrown.HasFailed);
        Assert.False(collected.IsActive);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Reference_value_is_checked()
    {
        var nickname = "Ada Lovelace";
        var report = new ValidationReport();

        var exception = Assert.Throws<ArgumentException>(() => nickname.Throw().WhenPresent().IfLongerThan(3));
        report.Check(nickname).WhenPresent().IfLongerThan(3);

        Assert.Equal("nickname", exception.ParamName);
        Assert.Equal(ErrorCodes.LongerThan, Assert.Single(report.Failures).ErrorCode);
    }

    [Fact]
    public void Null_nullable_value_skips_and_unwraps()
    {
        int? height = null;
        var report = new ValidationReport();

        Guard<int> thrown = height.Throw().WhenPresent().IfNegative().IfGreaterThan(300);
        Guard<int> collected = report.Check(height).WhenPresent().IfNegative();

        Assert.False(thrown.IsActive);
        Assert.Equal(0, thrown.Value);
        Assert.Equal("height", thrown.Name);
        Assert.False(collected.IsActive);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Nullable_value_is_unwrapped_and_checked()
    {
        int? height = -1;
        var report = new ValidationReport();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => height.Throw().WhenPresent().IfNegative());
        int passed = ((int?)180).Throw().WhenPresent().IfNegative().IfGreaterThan(300);
        report.Check(height).WhenPresent().IfNegative().IfZero();

        Assert.Equal("height", exception.ParamName);
        Assert.Equal(-1, exception.ActualValue);
        Assert.Equal(180, passed);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(("height", ErrorCodes.Negative), (failure.Name, failure.ErrorCode));
    }

    [Fact]
    public void Failed_or_skipped_guards_keep_their_state()
    {
        int? present = 5;
        int? missing = null;
        var report = new ValidationReport();

        var failedPresent = report.Check(present).Fail("Earlier", "Earlier.").WhenPresent().IfZero();
        var failedMissing = report.Check(missing).Fail("Earlier", "Earlier.").WhenPresent().IfZero();
        var skipped = ((string?)null).Throw().AllowNull().WhenPresent();

        Assert.True(failedPresent.HasFailed);
        Assert.Equal(5, failedPresent.Value);
        Assert.True(failedMissing.HasFailed);
        Assert.False(skipped.IsActive);
        Assert.Equal(2, report.Failures.Count);
    }

    [Fact]
    public void WhenPresent_works_inside_validator_rules()
    {
        var validator = new Validator<Person>()
            .For(x => x.Height, h => h.WhenPresent().IfNegative().IfGreaterThan(300))
            .For(x => x.VatNumber, v => v.WhenPresent().IfShorterThan(5));

        Assert.True(validator.Validate(new Person()).IsValid);
        Assert.True(validator.Validate(new Person { Height = 180, VatNumber = "GB12345" }).IsValid);
        var report = validator.Validate(new Person { Height = -1, VatNumber = "GB" });

        Assert.Equal(["Height", "VatNumber"], report.Failures.Select(f => f.Name));
        Assert.Equal([ErrorCodes.Negative, ErrorCodes.ShorterThan], report.Failures.Select(f => f.ErrorCode));
    }
}
