using System;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

public sealed class PolymorphicTests
{
    private static PolymorphicValidator<Payment> Payments() => new PolymorphicValidator<Payment>()
        .Add(new CardPaymentValidator())
        .Add(new BankPaymentValidator(), ValidationGroups.Default, "strict");

    [Fact]
    public void Picks_the_validator_by_runtime_type_and_skips_unknown_types()
    {
        var validator = new Validator<Order>().For(x => x.Payment, Payments());

        Assert.Equal(["Payment.CardNumber", "Payment.Amount"], Names.Of(validator.Validate(new Order { Payment = new CardPayment() })));
        Assert.Equal(["Payment.Iban", "Payment.Iban"], Names.Of(validator.Validate(new Order { Payment = new BankPayment { Amount = 1 } })));
        Assert.True(validator.Validate(new Order { Payment = new CashPayment() }).IsValid);
    }

    [Fact]
    public void Groups_given_to_Add_replace_the_groups_of_the_run()
    {
        var validator = new Validator<Order>().For(x => x.Payment, Payments());

        var report = validator.Validate(new Order { Payment = new BankPayment { Iban = "short" } });

        Assert.Equal(["Payment.Iban"], Names.Of(report));
        Assert.Equal(ErrorCodes.ShorterThan, report.Failures[0].ErrorCode);
        Assert.Contains("strict", report.GroupsExecuted);
    }

    [Fact]
    public void Works_for_collections_and_on_its_own()
    {
        var payments = Payments();
        var validator = new Validator<Order>().ForEach(x => new Payment?[] { x.Payment, new CashPayment(), new CardPayment { CardNumber = "1", Amount = 1 } }, payments, rule => rule.OverridePropertyName("Payments"));

        Assert.Equal(["Payments[0].CardNumber", "Payments[0].Amount"], Names.Of(validator.Validate(new Order { Payment = new CardPayment() })));
        Assert.Equal(["CardNumber", "Amount"], Names.Of(payments.Validate(new CardPayment(), options: null)));
        Assert.True(payments.CanValidate(typeof(CardPayment)));
        Assert.False(payments.CanValidate(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => payments.Validate(null!, options: null));
    }
}
