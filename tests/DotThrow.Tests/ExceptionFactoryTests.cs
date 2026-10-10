using System;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests;

public sealed class ExceptionFactoryTests
{
    public static TheoryData<CheckCase> Failures => Cases.Data(AllCases.Failures());

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failing_check_throws_the_custom_exception(CheckCase check) => check.AssertExceptionFactory();

    [Fact]
    public void Factory_gets_the_name_message_and_code()
    {
        var total = -5m;
        ValidationFailure? seen = null;

        var exception = Assert.Throws<CustomException>(() => total.Throw(f =>
        {
            seen = f;
            return new CustomException(f);
        }).IfNegative());

        Assert.NotNull(seen);
        Assert.Equal("total", seen.Name);
        Assert.Equal("'total' must not be negative.", seen.Message);
        Assert.Equal(ErrorCodes.Negative, seen.ErrorCode);
        Assert.Equal(seen.Message, exception.Message);
    }

    [Fact]
    public void Factory_gets_the_custom_message()
    {
        var exception = Assert.Throws<CustomException>(() => 0.Throw(f => new CustomException(f), "count").IfZero("Count must not be zero."));

        Assert.Equal("Count must not be zero.", exception.Message);
        Assert.Equal(ErrorCodes.Zero, exception.Failure!.ErrorCode);
    }

    [Fact]
    public void Factory_is_not_called_when_every_check_passes()
    {
        var calls = 0;

        int value = 5.Throw(f =>
        {
            calls++;
            return new CustomException(f);
        }).IfNegative().IfGreaterThan(10);

        Assert.Equal(5, value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Factory_returning_null_falls_back_to_the_default_exception()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => (-1).Throw(_ => null!, "count").IfNegative());

        Assert.Equal("count", exception.ParamName);
        Assert.Equal(-1, exception.ActualValue);
    }

    [Fact]
    public void Null_factory_throws_ArgumentNullException()
    {
        Func<ValidationFailure, Exception> factory = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => 1.Throw(factory));

        Assert.Equal("exceptionFactory", exception.ParamName);
    }

    [Fact]
    public void Factory_with_a_null_name_falls_back_to_value()
    {
        var exception = Assert.Throws<CustomException>(() => (-1).Throw(f => new CustomException(f), paramName: null).IfNegative());

        Assert.Equal("value", exception.Failure!.Name);
    }

    [Fact]
    public void Factory_can_build_any_exception_type()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            "".Throw(f => new InvalidOperationException($"{f.ErrorCode}: {f.Message}"), "state").IfNullOrEmpty());

        Assert.Equal("Empty: 'state' must not be empty.", exception.Message);
    }

    [Fact]
    public void Factory_works_with_custom_checks_and_AllowNull()
    {
        string? slug = null;

        slug.Throw(f => new CustomException(f)).AllowNull().IfNotSlug();
        var exception = Assert.Throws<CustomException>(() => "Bad Slug".Throw(f => new CustomException(f), "slug").IfNotSlug());

        Assert.Equal("NotSlug", exception.Failure!.ErrorCode);
    }

    [Fact]
    public void Factory_keeps_the_name_through_nullable_unwrap()
    {
        int? count = -1;

        var exception = Assert.Throws<CustomException>(() => count.Throw(f => new CustomException(f)).IfNull().IfNegative());

        Assert.Equal("count", exception.Failure!.Name);
        Assert.Equal(ErrorCodes.Negative, exception.Failure.ErrorCode);
    }
}
