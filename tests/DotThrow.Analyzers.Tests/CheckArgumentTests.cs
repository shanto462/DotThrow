using System.Threading.Tasks;
using DotThrow.Analyzers.CodeFixes;
using DotThrow.Analyzers.Tests.Infrastructure;
using Xunit;

namespace DotThrow.Analyzers.Tests;

public sealed class CheckArgumentTests
{
    [Fact]
    public Task DT0007_reports_constant_arguments_that_are_never_valid() =>
        Verify.AnalyzerAsync<CheckArgumentAnalyzer>(
            """
            using System.Collections.Generic;
            using DotThrow;

            class C
            {
                const int Limit = -5;

                void M(string code, int count, double ratio, decimal price, List<int> items, long size)
                {
                    code.Throw().IfLongerThan({|DT0007:-1|});
                    code.Throw().IfShorterThan({|DT0007:Limit|});
                    code.Throw().IfLengthOutOfRange(5, {|DT0007:2|});
                    code.Throw().IfLengthOutOfRange({|DT0007:-1|}, 2);
                    count.Throw().IfOutOfRange(10, {|DT0007:5|});
                    size.Throw().IfOutOfRange(10L, {|DT0007:5|});
                    ratio.Throw().IfInRange(1.5, {|DT0007:0.5|});
                    count.Throw().IfOutOfRangeExclusive(5, {|DT0007:5|});
                    ratio.Throw().IfGreaterThan({|DT0007:double.NaN|});
                    ratio.Throw().IfOutOfRange({|DT0007:double.NaN|}, 1);
                    count.Throw().IfNotMultipleOf({|DT0007:0|});
                    price.Throw().IfNotMultipleOf({|DT0007:0m|});
                    price.Throw().IfPrecisionScaleExceeded({|DT0007:0|}, 0);
                    price.Throw().IfPrecisionScaleExceeded(4, {|DT0007:5|});
                    items.Throw().IfCountGreaterThan({|DT0007:-2|});
                    items.Throw().IfCountOutOfRange(3, {|DT0007:1|});
                }

                void Fine(string code, int count, int min, int max, double ratio, decimal price)
                {
                    code.Throw().IfLongerThan(0);
                    code.Throw().IfLengthOutOfRange(2, 2);
                    count.Throw().IfOutOfRange(1, 1);
                    count.Throw().IfOutOfRange(min, max);
                    count.Throw().IfOutOfRangeExclusive(1, 3);
                    ratio.Throw().IfGreaterThan(double.PositiveInfinity);
                    count.Throw().IfNotMultipleOf(-1);
                    price.Throw().IfPrecisionScaleExceeded(4, 4);
                    count.Throw().IfOutOfRange(-10, -5);
                }
            }
            """);

    [Fact]
    public Task DT0007_reports_the_problem() =>
        Verify.AnalyzerAsync<CheckArgumentAnalyzer>(
            """
            using DotThrow;

            class C
            {
                void M(int count) => count.Throw().IfOutOfRange(10, 5);
            }
            """,
            Verify.Diagnostic<CheckArgumentAnalyzer>("DT0007")
                .WithSpan(5, 57, 5, 58)
                .WithArguments("The minimum 10 is greater than the maximum 5, so this check always throws"));

    [Fact]
    public Task DT0008_reports_comparisons_with_the_current_time() =>
        Verify.AnalyzerAsync<CheckArgumentAnalyzer>(
            """
            using System;
            using DotThrow;

            class C
            {
                void M(DateTime when, DateTimeOffset moment, DateOnly day, TimeProvider clock, DateTime start)
                {
                    when.Throw().{|DT0008:IfBefore(DateTime.Now)|};
                    when.Throw().{|DT0008:IfAfter(DateTime.Now, "Must not be in the future.")|};
                    moment.Throw().{|DT0008:IfBefore(DateTimeOffset.UtcNow)|};
                    moment.Throw().{|DT0008:IfAfter(DateTimeOffset.Now)|};
                    moment.Throw().{|DT0008:IfBefore(DateTime.UtcNow)|};
                    moment.Throw().{|DT0008:IfBefore(clock.GetUtcNow())|};
                    day.Throw().{|DT0008:IfBefore(DateOnly.FromDateTime(DateTime.Today))|};
                    day.Throw().{|DT0008:IfAfter(DateOnly.FromDateTime(clock.GetLocalNow().DateTime))|};

                    // Not the same meaning, or not the current time.
                    when.Throw().IfBefore(DateTime.UtcNow);
                    when.Throw().IfBefore(start);
                    when.Throw().IfBefore(DateTime.Today);
                    moment.Throw().IfBefore(clock.GetUtcNow().AddDays(1));
                    day.Throw().IfBefore(DateOnly.FromDateTime(start));

                    // The message shows the limit, or the static form.
                    when.Throw().IfBefore(DateTime.Now, "Must be after {ComparisonValue}.");
                    GuardExtensions.IfBefore(when.Throw(), DateTime.Now);
                }
            }
            """);

    [Fact]
    public Task DT0008_fix_uses_IfInPast_and_IfInFuture() =>
        Verify.CodeFixAsync<CheckArgumentAnalyzer, CurrentTimeCodeFix>(
            """
            using System;
            using DotThrow;

            class C
            {
                void M(DateTime when, DateTimeOffset moment, DateOnly day, TimeProvider clock)
                {
                    when.Throw().{|DT0008:IfBefore(DateTime.Now)|};
                    when.Throw().{|DT0008:IfAfter(DateTime.Now, "Must not be in the future.")|};
                    moment.Throw().{|DT0008:IfBefore(limit: DateTimeOffset.UtcNow)|};
                    moment.Throw().{|DT0008:IfBefore(clock.GetUtcNow(), "Too early.")|};
                    day.Throw().{|DT0008:IfAfter(DateOnly.FromDateTime(clock.GetLocalNow().DateTime))|};
                }
            }
            """,
            """
            using System;
            using DotThrow;

            class C
            {
                void M(DateTime when, DateTimeOffset moment, DateOnly day, TimeProvider clock)
                {
                    when.Throw().IfInPast();
                    when.Throw().IfInFuture("Must not be in the future.");
                    moment.Throw().IfInPast();
                    moment.Throw().IfInPast(clock, "Too early.");
                    day.Throw().IfInFuture(clock);
                }
            }
            """);
}
