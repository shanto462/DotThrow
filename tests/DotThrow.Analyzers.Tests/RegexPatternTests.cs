using System.Threading.Tasks;
using DotThrow.Analyzers.CodeFixes;
using DotThrow.Analyzers.Tests.Infrastructure;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotThrow.Analyzers.Tests;

public sealed class RegexPatternTests
{
    [Fact]
    public Task DT0005_reports_patterns_that_do_not_parse() =>
        new DotThrowAnalyzerTest<RegexPatternAnalyzer>
        {
            // C# 10: no generator suggestions, so only DT0005 shows.
            LanguageVersion = LanguageVersion.CSharp10,
            TestCode = """
                using System.Text.RegularExpressions;
                using DotThrow;

                class C
                {
                    const string Broken = "(?<name";

                    void M(string code, string pattern)
                    {
                        code.Throw().IfMatches({|DT0005:"[a-"|});
                        code.Throw().IfNotMatches({|DT0005:Broken|});
                        code.Throw().IfNotMatches({|DT0005:"a)"|}, RegexOptions.IgnoreCase, "Bad code.");
                        code.Throw().IfNotMatches(options: RegexOptions.None, pattern: {|DT0005:"*a"|});
                        code.Throw().IfMatches({|DT0005:@"(a)\1"|}, RegexOptions.NonBacktracking);
                        code.Throw().IfMatches({|DT0005:"a"|}, RegexOptions.ECMAScript | RegexOptions.Singleline);

                        // Valid, or not constant.
                        code.Throw().IfMatches("^[a-z]+$");
                        code.Throw().IfNotMatches(@"^\d{5}$", RegexOptions.CultureInvariant);
                        code.Throw().IfMatches(pattern);
                        code.Throw().IfMatches("^a", (RegexOptions)GetOptions());
                        code.Throw().IfMatches(new Regex("^a"));
                    }

                    static int GetOptions() => 0;
                }
                """,
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task DT0006_suggests_the_generator_for_constant_patterns() =>
        Verify.AnalyzerAsync<RegexPatternAnalyzer>(
            """
            using System.Text.RegularExpressions;
            using DotThrow;

            class C
            {
                const string Zip = "^[0-9]{5}$";

                void M(string code, string pattern)
                {
                    code.Throw().IfNotMatches({|DT0006:"^[0-9]{5}$"|});
                    code.Throw().IfMatches({|DT0006:Zip|}, RegexOptions.IgnoreCase);
                    new ValidationReport().Check(code).IfNotMatches({|DT0006:"^[a-z]+$"|});

                    // Not constant, the generator does not compile NonBacktracking, or already a Regex.
                    code.Throw().IfMatches(pattern);
                    code.Throw().IfMatches("^a+$", RegexOptions.NonBacktracking);
                    code.Throw().IfMatches(new Regex("^a"));
                }
            }
            """);

    [Fact]
    public Task Regex_rules_on_netstandard20() =>
        new DotThrowAnalyzerTest<RegexPatternAnalyzer>
        {
            // netstandard2.0 has no [GeneratedRegex], so only invalid patterns are reported.
            Target = TestTarget.NetStandard20,
            TestCode = """
                using DotThrow;

                class C
                {
                    void M(string code)
                    {
                        code.Throw().IfMatches("^[a-z]+$");
                        code.Throw().IfMatches({|DT0005:"[a-"|});
                    }
                }
                """,
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task DT0006_fix_adds_a_generated_regex_method() =>
        Verify.CodeFixAsync<RegexPatternAnalyzer, GeneratedRegexCodeFix>(
            """
            using DotThrow;

            namespace App;

            public sealed class AddressValidator : Validator<Address>
            {
                public AddressValidator()
                {
                    For(x => x.PostalCode, code => code.IfNullOrWhiteSpace().IfNotMatches({|DT0006:"^[0-9]{5}$"|}));
                }
            }

            public sealed record Address(string PostalCode);
            """,
            """
            using System.Text.RegularExpressions;
            using DotThrow;

            namespace App;

            public sealed partial class AddressValidator : Validator<Address>
            {
                public AddressValidator()
                {
                    For(x => x.PostalCode, code => code.IfNullOrWhiteSpace().IfNotMatches(CodeRegex()));
                }

                [GeneratedRegex("^[0-9]{5}$")]
                private static partial Regex {|CS8795:CodeRegex|}();
            }

            public sealed record Address(string PostalCode);
            """);

    [Fact]
    public Task DT0006_fix_all_names_methods_and_keeps_options_and_messages() =>
        Verify.CodeFixAsync<RegexPatternAnalyzer, GeneratedRegexCodeFix>(
            """
            using System.Text.RegularExpressions;
            using DotThrow;

            static class Outer
            {
                class Checks
                {
                    void M(string slug, Request request)
                    {
                        slug.Throw().IfNotMatches({|DT0006:"^[a-z-]+$"|}, RegexOptions.IgnoreCase, "Not a slug.");
                        slug.Throw().IfMatches({|DT0006:"--"|}, message: "No double dashes.");
                        request.Email.Throw().IfNotMatches({|DT0006:"@"|});
                    }

                    static void SlugRegex() { }
                }
            }

            sealed class Request
            {
                public string Email { get; set; } = "";
            }
            """,
            """
            using System.Text.RegularExpressions;
            using DotThrow;

            static partial class Outer
            {
                partial class Checks
                {
                    void M(string slug, Request request)
                    {
                        slug.Throw().IfNotMatches(SlugRegex2(), "Not a slug.");
                        slug.Throw().IfMatches(SlugRegex3(), message: "No double dashes.");
                        request.Email.Throw().IfNotMatches(EmailRegex());
                    }

                    static void SlugRegex() { }

                    [GeneratedRegex("^[a-z-]+$", RegexOptions.IgnoreCase)]
                    private static partial Regex {|CS8795:SlugRegex2|}();

                    [GeneratedRegex("--")]
                    private static partial Regex {|CS8795:SlugRegex3|}();

                    [GeneratedRegex("@")]
                    private static partial Regex {|CS8795:EmailRegex|}();
                }
            }

            sealed class Request
            {
                public string Email { get; set; } = "";
            }
            """);

    [Fact]
    public Task DT0006_fix_is_not_offered_for_local_constants_or_the_static_form() =>
        Verify.CodeFixAsync<RegexPatternAnalyzer, GeneratedRegexCodeFix>(
            """
            using DotThrow;

            class C
            {
                void M(string code)
                {
                    const string Local = "^a+$";
                    code.Throw().IfMatches({|DT0006:Local|});
                    GuardExtensions.IfMatches(code.Throw(), {|DT0006:"^b+$"|});
                }
            }
            """,
            """
            using DotThrow;

            class C
            {
                void M(string code)
                {
                    const string Local = "^a+$";
                    code.Throw().IfMatches({|DT0006:Local|});
                    GuardExtensions.IfMatches(code.Throw(), {|DT0006:"^b+$"|});
                }
            }
            """);
}
