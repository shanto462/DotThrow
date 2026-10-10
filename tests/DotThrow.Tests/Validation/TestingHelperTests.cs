using System.Threading;
using System.Threading.Tasks;
using DotThrow.Testing;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// DotThrow.Testing, the FluentValidation TestHelper shape.
public sealed class TestingHelperTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Validator<Order> OrderValidator = new Validator<Order>()
        .For(x => x.Number, n => n.IfNullOrEmpty(), rule => rule.WithSeverity(Severity.Warning).WithState(_ => 42))
        .ForEach(x => x.Lines, new LineValidator())
        .For(x => x.Customer, new Validator<Customer>().For(x => x.Address, new AddressValidator()))
        .Must(x => x.Total >= 0, "Total must not be negative.");

    private static Order Bad() => new() { Total = -1, Lines = [new Line { Sku = "a", Quantity = 0 }] };

    [Fact]
    public void ShouldHaveValidationErrorFor_with_conditions_and_Only()
    {
        var result = OrderValidator.TestValidate(Bad());

        result.ShouldHaveValidationErrorFor(x => x.Number)
            .WithErrorCode(ErrorCodes.Null)
            .WithSeverity(Severity.Warning)
            .WithCustomState(42)
            .WithErrorMessage("'Number' must not be null.")
            .WithAttemptedValue(null);
        result.ShouldHaveValidationErrorFor(x => x.Lines![0]!.Quantity).WithErrorCode(ErrorCodes.OutOfRange);
        result.ShouldHaveValidationErrorFor("Lines[0].Quantity");
        result.ShouldHaveValidationErrorFor(string.Empty).WithErrorMessage("Total must not be negative.");
        result.ShouldNotHaveValidationErrorFor(x => x.Customer);
        result.ShouldNotHaveValidationErrorFor("Lines[1].Quantity");
        Assert.Equal(3, result.Failures.Count);

        var only = Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor(x => x.Number).Only());
        Assert.Contains("Total must not be negative.", only.Message, System.StringComparison.Ordinal);
        Assert.Equal(2, only.Failures.Count);
    }

    [Fact]
    public void Failed_assertions_throw_a_framework_agnostic_exception()
    {
        var result = OrderValidator.TestValidate(Bad());

        Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor(x => x.Customer));
        Assert.Throws<ValidationTestException>(() => result.ShouldNotHaveValidationErrorFor(x => x.Number));
        Assert.Throws<ValidationTestException>(() => result.ShouldNotHaveAnyValidationErrors());
        Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor(x => x.Number).WithErrorCode("Other"));
        Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor(x => x.Number).WithoutSeverity(Severity.Warning));
        Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrors().WhenAll(f => f.Severity == Severity.Warning));
        var message = Assert.Throws<ValidationTestException>(() => result.ShouldHaveValidationErrorFor(x => x.Number).WithErrorCode("Other")).Message;
        Assert.Equal("Expected an error code of 'Other'. Actual error code was 'Null'", message);
    }

    [Fact]
    public void Nested_paths_and_valid_objects()
    {
        var order = new Order { Number = "1", Customer = new Customer { Address = new Address { Street = "s", PostalCode = "12345" } } };

        var result = OrderValidator.TestValidate(order);

        result.ShouldHaveValidationErrorFor(x => x.Customer!.Address!.City).Only();
        OrderValidator.TestValidate(new Order { Number = "1" }).ShouldNotHaveAnyValidationErrors();
        result.ShouldHaveValidationErrors().WithoutErrorCode("Nope").WithoutErrorMessage("Nope").WithoutCustomState("Nope");
    }

    [Fact]
    public async Task Async_helpers_and_the_async_rule_hint()
    {
        var validator = new Validator<Order>().MustAsync(async (x, _) => { await Task.Yield(); return x.Number is not null; }, "Number is required.");

        var result = await validator.TestValidateAsync(new Order(), cancellationToken: Token);

        result.ShouldHaveValidationErrorFor(string.Empty);
        var exception = Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => validator.TestValidate(new Order()));
        Assert.Contains("TestValidateAsync", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Groups_and_child_validator_assertions()
    {
        var validator = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrEmpty(), rule => rule.InGroup("strict"))
            .For(x => x.Customer, new Validator<Customer>());

        validator.TestValidate(new Order(), "strict").ShouldHaveValidationErrorFor(x => x.Number);
        validator.TestValidate(new Order()).ShouldNotHaveAnyValidationErrors();
        validator.ShouldHaveChildValidator(x => x.Customer, typeof(Validator<Customer>));
        Assert.Throws<ValidationTestException>(() => validator.ShouldHaveChildValidator(x => x.Customer, typeof(AddressValidator)));
    }
}
