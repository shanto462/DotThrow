using System;
using Xunit;

namespace DotThrow.Tests.Guards;

public sealed class GuardCoreTests
{
    [Fact]
    public void Throw_uses_the_argument_expression_as_the_name()
    {
        var request = new SignUpRequest { Email = null };

        var exception = Assert.Throws<ArgumentNullException>(() => request.Email.Throw().IfNull());

        Assert.Equal("request.Email", exception.ParamName);
        Assert.StartsWith("'request.Email' must not be null.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Throw_uses_a_local_variable_name()
    {
        var quantity = -1;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => quantity.Throw().IfNegative());

        Assert.Equal("quantity", exception.ParamName);
        Assert.Equal(-1, exception.ActualValue);
    }

    [Fact]
    public void Throw_uses_a_method_call_expression_as_the_name()
    {
        var exception = Assert.Throws<ArgumentException>(() => EmptyName().Throw().IfNullOrEmpty());

        Assert.Equal("EmptyName()", exception.ParamName);
    }

    [Fact]
    public void Throw_uses_an_explicit_name()
    {
        var guard = 5.Throw("count");

        Assert.Equal("count", guard.Name);
    }

    [Fact]
    public void Throw_with_a_null_name_falls_back_to_value()
    {
        var guard = 5.Throw(paramName: null);
        var report = new ValidationReport();
        var collected = report.Check(5, name: null);

        Assert.Equal("value", guard.Name);
        Assert.Equal("value", collected.Name);
    }

    [Fact]
    public void Check_uses_the_argument_expression_as_the_name()
    {
        var request = new SignUpRequest { Email = "" };
        var report = new ValidationReport();

        report.Check(request.Email).IfNullOrEmpty();

        Assert.Equal("request.Email", Assert.Single(report.Failures).Name);
    }

    [Fact]
    public void Guard_converts_implicitly_to_the_value()
    {
        var name = "Ada Lovelace";

        string checkedName = name.Throw().IfNullOrWhiteSpace().IfLongerThan(50);
        int age = 36.Throw().IfNegative();

        Assert.Same(name, checkedName);
        Assert.Equal(36, age);
    }

    [Fact]
    public void Guard_exposes_value_and_name()
    {
        var guard = 42.Throw("answer");

        Assert.Equal(42, guard.Value);
        Assert.Equal("answer", guard.Name);
        Assert.True(guard.IsActive);
        Assert.False(guard.HasFailed);
    }

    [Fact]
    public void Default_guard_has_default_value_and_no_name()
    {
        var guard = default(Guard<int>);

        Assert.Equal(0, guard.Value);
        Assert.Null(guard.Name);
        Assert.True(guard.IsActive);
    }

    [Fact]
    public void ToString_returns_the_value_text()
    {
        Assert.Equal("42", 42.Throw().ToString());
        Assert.Equal("Ada", "Ada".Throw().ToString());
        Assert.Equal(string.Empty, ((string?)null).Throw().ToString());
    }

    [Fact]
    public void AllowNull_skips_the_rest_of_the_chain_for_null()
    {
        string? middleName = null;

        var guard = middleName.Throw().AllowNull().IfNullOrEmpty().IfLongerThan(2);

        Assert.False(guard.IsActive);
        Assert.False(guard.HasFailed);
        Assert.Null(guard.Value);
    }

    [Fact]
    public void AllowNull_still_checks_a_value_that_is_not_null()
    {
        var middleName = "Augusta";

        var exception = Assert.Throws<ArgumentException>(() => middleName.Throw().AllowNull().IfLongerThan(2));

        Assert.Equal("middleName", exception.ParamName);
    }

    [Fact]
    public void AllowNull_on_a_failed_guard_keeps_it_failed()
    {
        var report = new ValidationReport();

        var guard = report.Check((string?)null, "name").IfNull().AllowNull();

        Assert.True(guard.HasFailed);
        Assert.False(guard.IsActive);
        Assert.Single(report.Failures);
    }

    [Fact]
    public void AllowNull_on_a_value_type_does_nothing()
    {
        var guard = 0.Throw().AllowNull();

        Assert.True(guard.IsActive);
    }

    [Fact]
    public void Nullable_IfNull_unwraps_the_value()
    {
        int? maybeCount = 5;

        int count = maybeCount.Throw().IfNull().IfNegative();
        Guard<int> guard = maybeCount.Throw().IfNull();

        Assert.Equal(5, count);
        Assert.Equal("maybeCount", guard.Name);
        Assert.True(guard.IsActive);
    }

    [Fact]
    public void Nullable_IfNull_throws_for_null()
    {
        int? maybeCount = null;

        var exception = Assert.Throws<ArgumentNullException>(() => maybeCount.Throw().IfNull());

        Assert.Equal("maybeCount", exception.ParamName);
        Assert.StartsWith("'maybeCount' must not be null.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_IfNull_uses_a_custom_message()
    {
        int? maybeCount = null;

        var exception = Assert.Throws<ArgumentNullException>(() => maybeCount.Throw().IfNull("Count is required."));

        Assert.StartsWith("Count is required.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_IfNull_keeps_the_failed_state_in_collect_mode()
    {
        int? maybeCount = null;
        var report = new ValidationReport();

        var guard = report.Check(maybeCount).IfNull().IfZero().IfNegative();

        Assert.True(guard.HasFailed);
        Assert.Equal(0, guard.Value);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(ErrorCodes.Null, failure.ErrorCode);
        Assert.Equal("maybeCount", failure.Name);
    }

    [Fact]
    public void Nullable_IfNull_keeps_the_skipped_state_after_AllowNull()
    {
        int? maybeCount = null;

        var guard = maybeCount.Throw().AllowNull().IfNull().IfZero();

        Assert.False(guard.IsActive);
        Assert.False(guard.HasFailed);
    }

    [Fact]
    public void Nullable_IfNull_on_a_failed_guard_with_a_value_keeps_the_value()
    {
        int? maybeCount = 7;
        var report = new ValidationReport();

        var guard = report.Check(maybeCount).Fail("Earlier", "An earlier failure.").IfNull();

        Assert.True(guard.HasFailed);
        Assert.Equal(7, guard.Value);
        Assert.Single(report.Failures);
    }

    [Fact]
    public void Fail_throws_ArgumentException_in_throw_mode()
    {
        var ticket = "TICKET 12";

        var exception = Assert.Throws<ArgumentException>(() => ticket.Throw().IfNotTicketId());

        Assert.Equal("ticket", exception.ParamName);
        Assert.StartsWith("'ticket' must be a ticket id.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fail_records_the_code_in_collect_mode()
    {
        var report = new ValidationReport();

        report.Check("TICKET 12", "ticket").IfNotTicketId().IfNotTicketId();

        var failure = Assert.Single(report.Failures);
        Assert.Equal("NotTicketId", failure.ErrorCode);
        Assert.Equal("ticket", failure.Name);
    }

    [Fact]
    public void Custom_check_passes_a_valid_value()
    {
        string ticket = "T-1042".Throw().IfNotTicketId();

        Assert.Equal("T-1042", ticket);
    }

    [Fact]
    public void FailOutOfRange_carries_the_actual_value_for_value_types()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => 7.Throw("odd").FailOutOfRange("Odd", "'odd' must be even."));

        Assert.Equal(7, exception.ActualValue);
        Assert.Equal("odd", exception.ParamName);
    }

    [Fact]
    public void FailOutOfRange_hides_the_actual_value_for_reference_types()
    {
        var secret = "hunter2-secret";

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => secret.Throw().FailOutOfRange("Secret", "'secret' is wrong."));

        Assert.Null(exception.ActualValue);
        Assert.DoesNotContain("hunter2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailNull_uses_the_default_or_a_custom_message()
    {
        var defaultMessage = Assert.Throws<ArgumentNullException>(() => "x".Throw("thing").FailNull());
        var customMessage = Assert.Throws<ArgumentNullException>(() => "x".Throw("thing").FailNull("Thing is missing."));

        Assert.StartsWith("'thing' must not be null.", defaultMessage.Message, StringComparison.Ordinal);
        Assert.StartsWith("Thing is missing.", customMessage.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fail_in_collect_mode_returns_a_failed_guard_with_the_same_value()
    {
        var report = new ValidationReport();

        var guard = report.Check(3, "count").FailOutOfRange("TooSmall", "'count' is too small.");

        Assert.True(guard.HasFailed);
        Assert.False(guard.IsActive);
        Assert.Equal(3, guard.Value);
        Assert.Equal("count", guard.Name);
        Assert.Equal("TooSmall", Assert.Single(report.Failures).ErrorCode);
    }

    [Fact]
    public void First_failure_in_throw_mode_stops_the_chain()
    {
        var calls = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => (-1).Throw().IfNegative().If(_ => ++calls > 0));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Inactive_guard_does_not_call_predicates()
    {
        var calls = 0;
        var report = new ValidationReport();

        report.Check(-1, "count").IfNegative().If(_ => ++calls > 0).IfNot(_ => ++calls > 0);
        ((string?)null).Throw().AllowNull().If(_ => ++calls > 0).IfNot(_ => ++calls > 0);

        Assert.Equal(0, calls);
        Assert.Single(report.Failures);
    }

    [Fact]
    public void Each_value_in_collect_mode_reports_its_own_first_failure()
    {
        var report = new ValidationReport();

        report.Check("", "name").IfNullOrEmpty().IfShorterThan(3);
        report.Check(-5, "age").IfNegative().IfLessThan(0);
        report.Check("ada@example.com", "email").IfNullOrWhiteSpace().IfNotEmailAddress();

        Assert.Collection(
            report.Failures,
            f => Assert.Equal(("name", ErrorCodes.Empty), (f.Name, f.ErrorCode)),
            f => Assert.Equal(("age", ErrorCodes.Negative), (f.Name, f.ErrorCode)));
    }

    private static string EmptyName() => string.Empty;

    private sealed class SignUpRequest
    {
        public string? Email { get; set; }
    }
}

/// <summary>A user-defined check, written the way the docs suggest.</summary>
internal static class TicketIdChecks
{
    public static Guard<string> IfNotTicketId(this Guard<string> guard)
    {
        if (guard.IsActive && !IsTicketId(guard.Value))
        {
            return guard.Fail("NotTicketId", $"'{guard.Name}' must be a ticket id.");
        }

        return guard;
    }

    // A ticket id is "T-" followed by one or more digits, for example "T-1042".
    private static bool IsTicketId(string? value)
    {
        if (value is null || value.Length < 3 || value[0] != 'T' || value[1] != '-')
        {
            return false;
        }

        for (var i = 2; i < value.Length; i++)
        {
            if (value[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
