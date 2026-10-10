using System;
using System.Text.RegularExpressions;
using DotThrow.Tests.Support;
using Xunit;

namespace DotThrow.Tests.Guards;

/// <summary>
/// Review fix (ReDoS): a match that runs out of time fails the check with its normal code (fail
/// closed); RegexMatchTimeoutException never escapes.
/// </summary>
public sealed class RegexTimeoutTests
{
    // Catastrophic backtracking: far more than a millisecond without a timeout.
    private const string Evil = "^(a+)+$";
    private static readonly string Input = new string('a', 28) + "!";
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(5);

    [Fact]
    public void A_timed_out_match_fails_both_checks_in_both_modes()
    {
        Check.Fails<ArgumentException, string>(Input, g => g.IfMatches(Evil, Short), ErrorCodes.Matches);
        Check.Fails<ArgumentException, string>(Input, g => g.IfNotMatches(Evil, Short), ErrorCodes.NotMatches);

        var regex = new Regex(Evil, RegexOptions.None, Short);
        Check.Fails<ArgumentException, string>(Input, g => g.IfMatches(regex), ErrorCodes.Matches);
        Check.Fails<ArgumentException, string>(Input, g => g.IfNotMatches(regex), ErrorCodes.NotMatches);
    }

    [Fact]
    public void Timeout_overloads_work_like_the_others_when_the_match_is_fast()
    {
        var timeout = TimeSpan.FromSeconds(1);
        Check.Passes("abc", g => g.IfMatches("^[0-9]+$", timeout));
        Check.Fails<ArgumentException, string>("123", g => g.IfMatches("^[0-9]+$", timeout), ErrorCodes.Matches, "'input' must not match the pattern '^[0-9]+$'.");
        Check.Passes("ABC", g => g.IfNotMatches("^[a-z]+$", timeout, RegexOptions.IgnoreCase));
        Check.Fails<ArgumentException, string>("abc", g => g.IfNotMatches("^[0-9]+$", timeout, message: "digits only: {Pattern}"), ErrorCodes.NotMatches, "digits only: '^[0-9]+$'");
        Check.Fails<ArgumentNullException, string>(null!, g => g.IfMatches("x", timeout), ErrorCodes.Null);
    }

    [Fact]
    public void A_bad_timeout_or_pattern_is_a_programming_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "a".Throw().IfMatches("a", TimeSpan.FromSeconds(-1)));
        Assert.ThrowsAny<ArgumentException>(() => "a".Throw().IfNotMatches("(", TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentNullException>(() => "a".Throw().IfMatches(null!, TimeSpan.FromSeconds(1)));
    }
}
