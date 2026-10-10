// The tests register validators by Type on purpose (the non-generic Add).
#pragma warning disable CA2263

using System;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// PolymorphicValidator in every mode: on its own and as a child, sync and async, typed,
// factory and untyped registrations, groups, and the non-generic IValidator members.
public sealed class PolymorphicPathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PolymorphicValidator<Payment> Payments() => new PolymorphicValidator<Payment>()
        .Add(new CardPaymentValidator())
        .Add<BankPayment>(bank => bank.Amount > 0 ? new Validator<BankPayment>().For(x => x.Iban, i => i.IfNullOrEmpty()) : null)
        .Add(typeof(CashPayment), new AsyncCashValidator());

    [Fact]
    public void Runs_alone_and_ignores_types_that_are_not_registered()
    {
        var payments = new PolymorphicValidator<Payment>().Add(new CardPaymentValidator());

        Assert.Equal(["CardNumber", "Amount"], Names.Of(payments.Validate(new CardPayment())));
        Assert.True(payments.Validate(new BankPayment()).IsValid);
    }

    [Fact]
    public async Task Runs_alone_async_for_every_kind_of_registration()
    {
        var payments = Payments();

        var card = await payments.ValidateAsync(new CardPayment(), options: null, Token);
        var bank = await payments.ValidateAsync(new BankPayment { Amount = 1 }, options: null, Token);
        var noBankRules = await payments.ValidateAsync(new BankPayment(), options: null, Token);
        var cash = await payments.ValidateAsync(new CashPayment { Amount = 100 }, options: null, Token);

        Assert.Equal(["CardNumber", "Amount"], Names.Of(card));
        Assert.Equal(["Iban"], Names.Of(bank));
        Assert.True(noBankRules.IsValid);
        Assert.Equal(["Amount"], Names.Of(cash));
    }

    [Fact]
    public async Task Async_run_of_a_type_that_is_not_registered_is_valid()
    {
        var payments = new PolymorphicValidator<Payment>().Add(new CardPaymentValidator());

        var report = await payments.ValidateAsync(new BankPayment(), options: null, Token);

        Assert.True(report.IsValid);
    }

    [Fact]
    public async Task Null_value_alone_throws_or_gives_a_faulted_task()
    {
        var payments = Payments();

        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => payments.Validate(null!, options: null)).ParamName);
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => payments.ValidateAsync(null!, options: null, Token));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public async Task Runs_as_a_child_in_async_runs()
    {
        var validator = new Validator<Order>().For(x => x.Payment, Payments());

        var card = await validator.ValidateAsync(new Order { Payment = new CardPayment() }, Token);
        var bank = await validator.ValidateAsync(new Order { Payment = new BankPayment { Amount = 1 } }, Token);
        var noBankRules = await validator.ValidateAsync(new Order { Payment = new BankPayment() }, Token);
        var cash = await validator.ValidateAsync(new Order { Payment = new CashPayment { Amount = 100 } }, Token);
        var none = await validator.ValidateAsync(new Order(), Token);

        Assert.Equal(["Payment.CardNumber", "Payment.Amount"], Names.Of(card));
        Assert.Equal(["Payment.Iban"], Names.Of(bank));
        Assert.True(noBankRules.IsValid);
        Assert.Equal(["Payment.Amount"], Names.Of(cash));
        Assert.True(none.IsValid);
    }

    [Fact]
    public void Untyped_registration_runs_as_a_child_in_sync_runs()
    {
        var payments = new PolymorphicValidator<Payment>().Add(typeof(CashPayment), new Validator<CashPayment>().Must(x => x.Amount < 50, "Too much cash."));
        var validator = new Validator<Order>().For(x => x.Payment, payments);

        var report = validator.Validate(new Order { Payment = new CashPayment { Amount = 60 } });

        Assert.Equal(["Payment"], Names.Of(report));
    }

    [Fact]
    public void Non_generic_members_validate_objects()
    {
        IValidator payments = new PolymorphicValidator<Payment>().Add(new CardPaymentValidator());

        Assert.False(payments.Validate(new CardPayment(), null).IsValid);
        Assert.Throws<ArgumentNullException>(() => payments.Validate(null!, null));
        Assert.True(payments.CanValidate(typeof(CardPayment)));
        Assert.False(payments.CanValidate(typeof(Person)));
        Assert.Equal("instance", Assert.Throws<ArgumentException>(() => payments.Validate(new Person(), null)).ParamName);
    }

    [Fact]
    public async Task Non_generic_async_member_validates_objects()
    {
        IValidator payments = new PolymorphicValidator<Payment>().Add(new CardPaymentValidator());

        var report = await payments.ValidateAsync(new CardPayment(), null, Token);

        Assert.Equal(["CardNumber", "Amount"], Names.Of(report));
    }

    [Fact]
    public void Root_aware_factory_gets_the_default_root_when_it_runs_alone()
    {
        Order? seen = new();
        var payments = new PolymorphicValidator<Payment>().Add<CardPayment, Order>((order, card) =>
        {
            seen = order;
            return new CardPaymentValidator();
        });

        var report = payments.Validate(new CardPayment(), options: null);

        Assert.Null(seen);
        Assert.False(report.IsValid);
    }

    [Fact]
    public async Task Registration_groups_replace_the_groups_of_the_run()
    {
        var strict = new Validator<BankPayment>()
            .For(x => x.Iban, i => i.IfShorterThan(10), rule => rule.InGroup("strict"))
            .For(x => x.Amount, a => a.IfNegativeOrZero());
        var payments = new PolymorphicValidator<Payment>()
            .Add(strict, "strict")
            .Add<CardPayment>(_ => new CardPaymentValidator(), ValidationGroups.Default)
            .Add(typeof(CashPayment), new Validator<CashPayment>().Must(x => x.Amount < 50, "Too much cash.", rule => rule.InGroup("strict").WithName("Amount")), "strict");
        var options = new ValidationOptions { Properties = ["Iban", "CardNumber", "Amount"] };
        options.Items["user"] = "Ada Lovelace";

        var bank = payments.Validate(new BankPayment { Iban = "DE" }, options);
        var bankAsync = await payments.ValidateAsync(new BankPayment { Iban = "DE" }, options, Token);
        var card = payments.Validate(new CardPayment(), options);
        var cardAsync = await payments.ValidateAsync(new CardPayment(), options, Token);
        var cash = payments.Validate(new CashPayment { Amount = 60 }, options);
        var cashAsync = await payments.ValidateAsync(new CashPayment { Amount = 60 }, options, Token);

        Assert.Equal(["Iban"], Names.Of(bank));
        Assert.Equal(["Iban"], Names.Of(bankAsync));
        Assert.Equal(["CardNumber", "Amount"], Names.Of(card));
        Assert.Equal(["CardNumber", "Amount"], Names.Of(cardAsync));
        Assert.Equal(["Amount"], Names.Of(cash));
        Assert.Equal(["Amount"], Names.Of(cashAsync));
    }

    [Fact]
    public async Task Registration_groups_apply_when_it_runs_as_a_child()
    {
        var strict = new Validator<CardPayment>().For(x => x.CardNumber, c => c.IfNullOrEmpty(), rule => rule.InGroup("strict"));
        var payments = new PolymorphicValidator<Payment>()
            .Add<CardPayment>(_ => strict, "strict")
            .Add(typeof(CashPayment), new AsyncCashValidator(), "strict");
        var validator = new Validator<Order>().For(x => x.Payment, payments);

        var card = await validator.ValidateAsync(new Order { Payment = new CardPayment() }, Token);
        var cash = await validator.ValidateAsync(new Order { Payment = new CashPayment { Amount = 100 } }, Token);

        Assert.Equal(["Payment.CardNumber"], Names.Of(card));
        Assert.Equal(["Payment.Amount"], Names.Of(cash));
    }

    // A hand-written validator with a truly async run, registered without generics.
    private sealed class AsyncCashValidator : IValidator<CashPayment>
    {
        public bool CanValidate(Type type) => typeof(CashPayment).IsAssignableFrom(type);

        public ValidationReport Validate(CashPayment value, ValidationOptions? options) => Check(value);

        public async Task<ValidationReport> ValidateAsync(CashPayment value, ValidationOptions? options, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return Check(value);
        }

        public ValidationReport Validate(object instance, ValidationOptions? options) => Validate((CashPayment)instance, options);

        public Task<ValidationReport> ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken = default) =>
            ValidateAsync((CashPayment)instance, options, cancellationToken);

        private static ValidationReport Check(CashPayment value)
        {
            var report = new ValidationReport();
            report.Check(value.Amount, "Amount").IfGreaterThan(50m);
            return report;
        }
    }
}
