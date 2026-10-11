using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FsCheck.Fluent;
using Xunit;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Fuzz tests for IfMatches and IfNotMatches: they agree with <see cref="Regex"/>, and a match
/// that times out fails closed. A class of its own, so the slow time-out cases run next to the
/// other fuzz tests instead of after them.
/// </summary>
[Trait(Fuzz.Trait, Fuzz.Category)]
public sealed class RegexFuzzTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    // Without a time out, exactly one of IfMatches and IfNotMatches fails, the same as Regex.IsMatch,
    // with every overload.
    [Fact]
    public void Matches_and_NotMatches_agree_with_Regex() =>
        Fuzz.Run(Arb.From(Gen.Zip(Gen.Elements(Generators.Patterns), Generators.AnyTextGen.Select(text => text ?? string.Empty)), Shrink), input =>
        {
            var (pattern, text) = input;
            var regex = new Regex(pattern, RegexOptions.None, Timeout);
            var expected = regex.IsMatch(text);
            Assert.Equal(expected, Contract.Fails(text, g => g.IfMatches(pattern), ErrorCodes.Matches));
            Assert.Equal(expected, Contract.Fails(text, g => g.IfMatches(pattern, Timeout), ErrorCodes.Matches));
            Assert.Equal(expected, Contract.Fails(text, g => g.IfMatches(regex), ErrorCodes.Matches));
            Assert.Equal(!expected, Contract.Fails(text, g => g.IfNotMatches(pattern), ErrorCodes.NotMatches));
            Assert.Equal(!expected, Contract.Fails(text, g => g.IfNotMatches(pattern, Timeout), ErrorCodes.NotMatches));
            Assert.Equal(!expected, Contract.Fails(text, g => g.IfNotMatches(regex), ErrorCodes.NotMatches));
        });

#if NET7_0_OR_GREATER
    // A match that runs out of time fails the check with its own code (fail closed), and
    // RegexMatchTimeoutException never escapes. Whether a match times out depends on the machine,
    // so each mode is checked on its own: it may fail, and it may pass only when the true answer
    // (from the linear-time NonBacktracking engine) allows it.
    [Fact]
    public void Matches_and_NotMatches_fail_closed_when_the_match_times_out() =>
        Fuzz.Run(Arb.From(Gen.Zip(Gen.Elements(Generators.SlowPatterns), Generators.SlowInput)), input =>
        {
            var (pattern, text) = input;
            var matches = new Regex(pattern, RegexOptions.NonBacktracking).IsMatch(text);
            var timeout = TimeSpan.FromMilliseconds(1);
            var regex = new Regex(pattern, RegexOptions.None, timeout);
            foreach (var outcome in new[]
            {
                Contract.Run(text, g => g.IfMatches(pattern, timeout), ErrorCodes.Matches),
                Contract.Run(text, g => g.IfMatches(regex), ErrorCodes.Matches),
            })
            {
                Assert.True(!matches || (outcome.Thrown && outcome.FromFactory && outcome.Collected), $"IfMatches passed although {pattern} matches {Contract.Show(text)}.");
            }

            foreach (var outcome in new[]
            {
                Contract.Run(text, g => g.IfNotMatches(pattern, timeout), ErrorCodes.NotMatches),
                Contract.Run(text, g => g.IfNotMatches(regex), ErrorCodes.NotMatches),
            })
            {
                Assert.True(matches || (outcome.Thrown && outcome.FromFactory && outcome.Collected), $"IfNotMatches passed although {pattern} does not match {Contract.Show(text)}.");
            }
        },
        share: 0.05);
#endif

    private static IEnumerable<(string, string)> Shrink((string Pattern, string Text) input) =>
        Generators.Shrink(input.Text).Select(text => (input.Pattern, text ?? string.Empty));
}
