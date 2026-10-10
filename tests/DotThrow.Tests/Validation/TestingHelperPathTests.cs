using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Scanning;
using DotThrow.Testing;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// The parts of DotThrow.Testing and DotThrow.Scanning that the scenario tests do not reach.
public sealed class TestingHelperPathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Validator<Order> OrderValidator = new Validator<Order>()
        .For(x => x.Number, n => n.IfNullOrEmpty(), rule => rule.WithState(_ => new[] { 1, 2 }))
        .For(x => x.Total, t => t.IfNegative(), rule => rule.InGroup("money"));

    [Fact]
    public void Result_exposes_the_report()
    {
        var result = OrderValidator.TestValidate(new Order(), "money", ValidationGroups.Default);

        Assert.False(result.IsValid);
        Assert.Same(result.Report.Failures, result.Failures);
        Assert.Equal(result.Report.GroupsExecuted, result.GroupsExecuted);
        Assert.Contains("money", result.GroupsExecuted);
    }

    [Fact]
    public void ShouldHaveValidationErrors_fails_for_a_valid_result()
    {
        var result = OrderValidator.TestValidate(new Order { Number = "A-1" });

        var exception = Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrors());

        Assert.Empty(exception.Failures);
    }

    [Fact]
    public void Continuation_lists_matched_and_unmatched_failures()
    {
        var result = OrderValidator.TestValidate(new Order { Total = -1 }, o => o.IncludeAllGroups());

        var failures = result.ShouldHaveValidationErrors();
        var number = failures.WithErrorCode(ErrorCodes.Null);

        Assert.Equal(2, failures.Count());
        Assert.Equal(["Number"], number.MatchedFailures.Select(f => f.Name));
        Assert.Equal(["Total"], number.UnmatchedFailures.Select(f => f.Name));
        Assert.Equal(["Number"], ((IEnumerable)number).Cast<ValidationFailure>().Select(f => f.Name));
        Assert.Throws<ValidationTestException>(() => number.Only());
    }

    [Fact]
    public void Only_on_a_hand_written_continuation_uses_its_unmatched_failures()
    {
        var matched = new ValidationFailure("Name", "Bad.", "Bad");
        var other = new ValidationFailure("Email", "Bad.", "Bad");

        var exception = Assert.Throws<ValidationTestException>(() => new FixedContinuation([matched], [other]).Only());
        new FixedContinuation([matched], []).Only();

        Assert.Equal([other], exception.Failures);
        Assert.Contains("Email: Bad.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_state_can_be_compared_with_a_comparer()
    {
        var result = OrderValidator.TestValidate(new Order());

        result.ShouldHaveValidationErrorFor(x => x.Number).WithCustomState(new[] { 1, 2 }, new SequenceComparer());
        Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor(x => x.Number).WithCustomState(new[] { 1, 2 }));
    }

    [Fact]
    public void Message_argument_that_does_not_match_fails()
    {
        var validator = new Validator<Order>().Custom((x, context) =>
        {
            context.AppendArgument("Limit", 10);
            context.AddFailure("Lines", "At most {Limit} lines.");
        });

        var result = validator.TestValidate(new Order());

        result.ShouldHaveValidationErrorFor("Lines").WithMessageArgument("Limit", 10);
        Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor("Lines").WithMessageArgument("Limit", 11));
        Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor("Lines").WithMessageArgument("Missing", 10));
    }

    [Fact]
    public async Task TestValidateAsync_with_options_set_in_a_callback()
    {
        var result = await OrderValidator.TestValidateAsync(new Order { Number = "A-1", Total = -1 }, o => o.IncludeGroups("money"), Token);

        result.ShouldHaveValidationErrorFor(x => x.Total).Only();
    }

    [Fact]
    public void ValidationTestException_constructors()
    {
        var plain = new ValidationTestException();
        var withMessage = new ValidationTestException("Mismatch.");
        var withInner = new ValidationTestException("Mismatch.", new FormatException());
        var withNullFailures = new ValidationTestException("Mismatch.", (IReadOnlyList<ValidationFailure>)null!);

        Assert.Equal("The validation result did not match.", plain.Message);
        Assert.Empty(plain.Failures);
        Assert.Equal("Mismatch.", withMessage.Message);
        Assert.IsType<FormatException>(withInner.InnerException);
        Assert.Empty(withInner.Failures);
        Assert.Empty(withNullFailures.Failures);
    }

    [Fact]
    public void Scanner_results_enumerate_without_generics_and_print()
    {
        var scanner = AssemblyScanner.FindValidatorsInAssemblyContaining<AddressValidator>();

        var untyped = ((IEnumerable)scanner).Cast<AssemblyScanResult>().ToList();
        var address = untyped.Single(r => r.ValidatorType == typeof(AddressValidator));

        Assert.Equal(scanner.Count(), untyped.Count);
        Assert.Equal("AddressValidator : IValidator<Address>", address.ToString());
    }

    private sealed class SequenceComparer : IEqualityComparer
    {
        public new bool Equals(object? x, object? y) =>
            x is int[] left && y is int[] right ? left.SequenceEqual(right) : object.Equals(x, y);

        public int GetHashCode(object obj) => obj is int[] items ? items.Length : obj.GetHashCode();
    }

    // A continuation from another source than TestValidate, as a custom assertion helper might build.
    private sealed class FixedContinuation(ValidationFailure[] matched, ValidationFailure[] unmatched) : ITestValidationWith
    {
        public IEnumerable<ValidationFailure> MatchedFailures => matched;

        public IEnumerable<ValidationFailure> UnmatchedFailures => unmatched;

        public IEnumerator<ValidationFailure> GetEnumerator() => ((IEnumerable<ValidationFailure>)matched).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
