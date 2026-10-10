using System.Threading.Tasks;
using DotThrow.Analyzers.CodeFixes;
using DotThrow.Analyzers.Tests.Infrastructure;
using Xunit;

namespace DotThrow.Analyzers.Tests;

public sealed class GuardChainTests
{
    [Fact]
    public Task DT0001_reports_chains_without_checks() =>
        Verify.AnalyzerAsync<GuardChainAnalyzer>(
            """
            using DotThrow;

            class C
            {
                void M(string name, string? middle, ValidationContext context)
                {
                    {|DT0001:name.Throw()|};
                    {|DT0001:name.Throw().AllowNull()|};
                    {|DT0001:name.Throw(f => new System.InvalidOperationException(f.Message))|};
                    _ = {|DT0001:name.Throw()|};

                    var report = new ValidationReport();
                    {|DT0001:report.Check(name)|};
                    {|DT0001:context.Check(middle)|};

                    // The guard converts back to the value, so nothing is checked.
                    string copy = {|DT0001:name.Throw()|};
                    Use({|DT0001:name.Throw().AllowNull()|});
                }

                void Use(string value) { }
            }
            """);

    [Fact]
    public Task DT0001_ignores_chains_with_checks_and_stored_guards() =>
        Verify.AnalyzerAsync<GuardChainAnalyzer>(
            """
            using DotThrow;

            class C
            {
                string Name { get; }

                C(string name, string? middle)
                {
                    Name = name.Throw().IfNullOrWhiteSpace();
                    middle.Throw().AllowNull().IfLongerThan(20);
                    name.Throw().Fail("Code", "Message");

                    var guard = name.Throw();
                    guard.IfNull();

                    var report = new ValidationReport();
                    report.Check(name).IfNullOrEmpty();
                    Check(name.Throw());
                }

                static void Check(Guard<string> guard) => guard.IfNull();
            }
            """);

    [Fact]
    public Task DT0001_removes_a_statement_without_checks() =>
        Verify.CodeFixAsync<GuardChainAnalyzer, GuardChainCodeFix>(
            """
            using DotThrow;

            class C
            {
                void M(string name)
                {
                    {|DT0001:name.Throw()|};
                    {|DT0001:this.Name.Throw().AllowNull()|};
                    System.Console.WriteLine(name);
                }

                string Name { get; } = "";
            }
            """,
            """
            using DotThrow;

            class C
            {
                void M(string name)
                {
                    System.Console.WriteLine(name);
                }

                string Name { get; } = "";
            }
            """);

    [Fact]
    public Task DT0001_keeps_a_statement_with_side_effects() =>
        Verify.CodeFixAsync<GuardChainAnalyzer, GuardChainCodeFix>(
            """
            using DotThrow;

            class C
            {
                void M()
                {
                    {|DT0001:Read().Throw()|};
                }

                static string Read() => "";
            }
            """,
            """
            using DotThrow;

            class C
            {
                void M()
                {
                    {|DT0001:Read().Throw()|};
                }

                static string Read() => "";
            }
            """);

    [Fact]
    public Task DT0001_uses_the_value_directly() =>
        Verify.CodeFixAsync<GuardChainAnalyzer, GuardChainCodeFix>(
            """
            using DotThrow;

            class C
            {
                int Total(int a, int b, string name)
                {
                    string copy = {|DT0001:name.Throw()|};
                    return {|DT0001:(a + b).Throw()|} * 2 + {|DT0001:a.Throw()|};
                }
            }
            """,
            """
            using DotThrow;

            class C
            {
                int Total(int a, int b, string name)
                {
                    string copy = name;
                    return (a + b) * 2 + a;
                }
            }
            """);

    [Fact]
    public Task DT0001_offers_no_fix_for_a_check_with_a_selector() =>
        Verify.CodeFixAsync<GuardChainAnalyzer, GuardChainCodeFix>(
            """
            using DotThrow;

            class C
            {
                void M(Form form)
                {
                    var report = new ValidationReport();
                    {|DT0001:report.Check(form, f => f.Name)|};
                    string name = {|DT0001:report.Check(form, f => f.Name)|};
                }
            }

            class Form
            {
                public string Name { get; set; } = "";
            }
            """,
            """
            using DotThrow;

            class C
            {
                void M(Form form)
                {
                    var report = new ValidationReport();
                    {|DT0001:report.Check(form, f => f.Name)|};
                    string name = {|DT0001:report.Check(form, f => f.Name)|};
                }
            }

            class Form
            {
                public string Name { get; set; } = "";
            }
            """);

    [Fact]
    public Task DT0009_reports_null_checks_on_value_types() =>
        Verify.AnalyzerAsync<GuardChainAnalyzer>(
            """
            using System;
            using DotThrow;

            class C
            {
                void M<T>(int count, int? maybe, Guid id, T any, DateTime when)
                    where T : struct
                {
                    count.Throw().{|DT0009:IfNull()|}.IfNegative();
                    count.Throw().{|DT0009:AllowNull()|}.IfNegative();
                    id.Throw().{|DT0009:IfNotNull()|};
                    any.Throw().{|DT0009:IfNull()|}.IfDefault();
                    maybe.Throw().IfNull().{|DT0009:IfNull()|}.IfNegative();

                    // Nullable values can be null.
                    maybe.Throw().IfNull().IfNegative();
                    maybe.Throw().AllowNull().IfNull().IfNegative();
                    when.Throw().IfDefault();
                }
            }
            """);

    [Fact]
    public Task DT0009_removes_calls_without_effect() =>
        Verify.CodeFixAsync<GuardChainAnalyzer, GuardChainCodeFix>(
            """
            using DotThrow;

            class C
            {
                void M(int count, int other)
                {
                    count.Throw().{|DT0009:IfNull()|}.IfNegative();
                    other.Throw()
                        .{|DT0009:AllowNull()|}
                        .{|DT0009:IfNull()|}
                        .IfZero();
                }
            }
            """,
            """
            using DotThrow;

            class C
            {
                void M(int count, int other)
                {
                    count.Throw().IfNegative();
                    other.Throw()
                        .IfZero();
                }
            }
            """);

    [Fact]
    public Task DT0009_offers_no_fix_for_IfNotNull() =>
        Verify.CodeFixAsync<GuardChainAnalyzer, GuardChainCodeFix>(
            """
            using DotThrow;

            class C
            {
                void M(int count) => count.Throw().{|DT0009:IfNotNull()|};
            }
            """,
            """
            using DotThrow;

            class C
            {
                void M(int count) => count.Throw().{|DT0009:IfNotNull()|};
            }
            """);

    [Fact]
    public Task No_diagnostics_without_DotThrow() =>
        new DotThrowAnalyzerTest<GuardChainAnalyzer>
        {
            TestCode = """
                static class Ext
                {
                    public static int Throw(this string value) => 0;
                }

                class C
                {
                    void M(string name) => name.Throw();
                }
                """,
            Target = TestTarget.NetWithoutDotThrow,
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task Fixes_are_not_offered_where_they_would_break_code() =>
        Verify.CodeFixAsync<GuardChainAnalyzer, GuardChainCodeFix>(
            """
            using System.Collections.Generic;
            using DotThrow;

            record Line(int Quantity);

            class C
            {
                void M(List<string> names, int count)
                {
                    // Removing would take the whole ForEach statement with it.
                    names.ForEach(name => {|DT0001:name.Throw()|});

                    // Removing the last call would leave no check, or no statement.
                    count.Throw().{|DT0009:IfNull()|};
                    _ = new Validator<Line>().For(x => x.Quantity, quantity => quantity.{|DT0009:IfNull()|});
                    GuardExtensions.{|DT0009:IfNull(count.Throw())|}.IfNegative();
                }
            }
            """,
            """
            using System.Collections.Generic;
            using DotThrow;

            record Line(int Quantity);

            class C
            {
                void M(List<string> names, int count)
                {
                    // Removing would take the whole ForEach statement with it.
                    names.ForEach(name => {|DT0001:name.Throw()|});

                    // Removing the last call would leave no check, or no statement.
                    count.Throw().{|DT0009:IfNull()|};
                    _ = new Validator<Line>().For(x => x.Quantity, quantity => quantity.{|DT0009:IfNull()|});
                    GuardExtensions.{|DT0009:IfNull(count.Throw())|}.IfNegative();
                }
            }
            """);
}
