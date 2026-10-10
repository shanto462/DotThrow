using System.Threading.Tasks;
using DotThrow.Analyzers.Tests.Infrastructure;
using Xunit;

namespace DotThrow.Analyzers.Tests;

/// <summary>
/// The analyzers against the netstandard2.0 build of DotThrow, which .NET Framework and older
/// .NET Core apps use. [CallerArgumentExpression] is a polyfill there.
/// </summary>
public sealed class NetStandardTests
{
    [Fact]
    public Task Guard_chain_rules() =>
        new DotThrowAnalyzerTest<GuardChainAnalyzer>
        {
            Target = TestTarget.NetStandard20,
            TestCode = """
                using DotThrow;

                class C
                {
                    void M(string name, int count)
                    {
                        {|DT0001:name.Throw()|};
                        count.Throw().{|DT0009:IfNull()|}.IfNegative();
                        name.Throw().IfNullOrWhiteSpace();
                    }
                }
                """,
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task Redundant_name_with_the_polyfilled_attribute() =>
        new DotThrowAnalyzerTest<RedundantNameAnalyzer>
        {
            Target = TestTarget.NetStandard20,
            TestCode = """
                using DotThrow;

                class C
                {
                    void M(string name) => name.Throw({|DT0003:nameof(name)|}).IfNull();
                }
                """,
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task Manual_guards() =>
        new DotThrowAnalyzerTest<ManualGuardAnalyzer>
        {
            Target = TestTarget.NetStandard20,
            TestCode = """
                using System;

                class C
                {
                    void M(string name, int count)
                    {
                        {|DT0002:if (name == null)|} throw new ArgumentNullException(nameof(name));
                        {|DT0002:if (count < 0 || count > 9)|} throw new ArgumentOutOfRangeException(nameof(count));
                    }
                }
                """,
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task Validator_rules() =>
        new DotThrowAnalyzerTest<ValidatorUsageAnalyzer>
        {
            Target = TestTarget.NetStandard20,
            TestCode = """
                using System.Threading.Tasks;
                using DotThrow;

                class C
                {
                    void M(string value)
                    {
                        var validator = new Validator<string>().MustAsync((x, token) => Task.FromResult(true));
                        _ = validator.{|DT0004:Validate|}(value);
                        validator.{|DT0010:ValidateAsync|}(value);
                    }
                }
                """,
        }.RunAsync(TestContext.Current.CancellationToken);
}
