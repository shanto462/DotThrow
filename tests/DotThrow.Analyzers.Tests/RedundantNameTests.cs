using System.Threading.Tasks;
using DotThrow.Analyzers.CodeFixes;
using DotThrow.Analyzers.Tests.Infrastructure;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotThrow.Analyzers.Tests;

public sealed class RedundantNameTests
{
    private const string Request = """

        public sealed class Request
        {
            public string Email { get; set; } = "";
        }
        """;

    [Fact]
    public Task DT0003_reports_names_the_compiler_fills_in() =>
        Verify.AnalyzerAsync<RedundantNameAnalyzer>(
            """
            using System;
            using DotThrow;

            class C
            {
                void M(string name, Request request, ValidationContext context)
                {
                    name.Throw({|DT0003:nameof(name)|}).IfNull();
                    name.Throw({|DT0003:"name"|}).IfNull();
                    name.Throw({|DT0003:paramName: "name"|}).IfNull();
                    name.Throw(f => new InvalidOperationException(f.Message), {|DT0003:"name"|}).IfNull();
                    request.Email.Throw({|DT0003:"request.Email"|}).IfNullOrEmpty();

                    var report = new ValidationReport();
                    report.Check(name, {|DT0003:nameof(name)|}).IfNull();
                    report.Check(request.Email, {|DT0003:"request.Email"|}).IfNull();
                    context.Check(name, {|DT0003:"name"|}).IfNull();
                }
            }
            """ + Request);

    [Fact]
    public Task DT0003_ignores_names_that_differ_on_purpose() =>
        Verify.AnalyzerAsync<RedundantNameAnalyzer>(
            """
            using DotThrow;

            class C
            {
                void M(string name, Request request)
                {
                    request.Email.Throw("Email").IfNull();
                    request.Email.Throw(nameof(request.Email)).IfNull();
                    name.Throw(Name()).IfNull();
                    name.Throw().IfNull();

                    var report = new ValidationReport();
                    report.Check(name, "Name").IfNull();
                    report.Check(request, r => r.Email).IfNull();
                }

                static string Name() => "name";
            }
            """ + Request);

    [Fact]
    public Task DT0003_needs_CSharp_10() =>
        new DotThrowAnalyzerTest<RedundantNameAnalyzer>
        {
            LanguageVersion = LanguageVersion.CSharp9,
            TestCode = """
                using DotThrow;

                class C
                {
                    void M(string name) => name.Throw(nameof(name)).IfNull();
                }
                """,
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task DT0003_fix_removes_the_argument() =>
        Verify.CodeFixAsync<RedundantNameAnalyzer, RedundantNameCodeFix>(
            """
            using System;
            using DotThrow;

            class C
            {
                void M(string name, int count)
                {
                    name.Throw({|DT0003:nameof(name)|}).IfNull();
                    count.Throw(f => new InvalidOperationException(f.Message), {|DT0003:"count"|}).IfNegative();
                    new ValidationReport().Check(count, {|DT0003:nameof(count)|}).IfNegative();
                }
            }
            """,
            """
            using System;
            using DotThrow;

            class C
            {
                void M(string name, int count)
                {
                    name.Throw().IfNull();
                    count.Throw(f => new InvalidOperationException(f.Message)).IfNegative();
                    new ValidationReport().Check(count).IfNegative();
                }
            }
            """);
}
