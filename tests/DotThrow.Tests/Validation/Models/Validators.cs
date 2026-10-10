using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Tests.Validation.Models;

public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.Street, street => street.IfNullOrWhiteSpace());
        For(x => x.City, city => city.IfNullOrWhiteSpace().IfLongerThan(20));
        For(x => x.PostalCode, code => code.IfNullOrEmpty().IfNotMatches("^[0-9]{5}$"));
    }
}

public sealed class LineValidator : Validator<Line>
{
    public LineValidator()
    {
        For(x => x.Sku, sku => sku.IfNullOrWhiteSpace());
        For(x => x.Quantity, quantity => quantity.IfOutOfRange(1, 100));
    }
}

public sealed class CardPaymentValidator : Validator<CardPayment>
{
    public CardPaymentValidator()
    {
        For(x => x.CardNumber, card => card.IfNullOrWhiteSpace());
        For(x => x.Amount, amount => amount.IfNegativeOrZero());
    }
}

public sealed class BankPaymentValidator : Validator<BankPayment>
{
    public BankPaymentValidator()
    {
        For(x => x.Iban, iban => iban.IfNullOrWhiteSpace());
        For(x => x.Iban, iban => iban.IfShorterThan(10), rule => rule.InGroup("strict"));
    }
}

// A hand-written IValidator<T>, the kind a test double or another library provides.
public sealed class StubValidator<T>(string name) : IValidator<T>
{
    public ValidationOptions? LastOptions { get; private set; }

    public bool CanValidate(Type type) => typeof(T).IsAssignableFrom(type);

    public ValidationReport Validate(T value, ValidationOptions? options)
    {
        LastOptions = options;
        return new ValidationReport().Add(name, "Stub failure.", "Stub");
    }

    public Task<ValidationReport> ValidateAsync(T value, ValidationOptions? options, CancellationToken cancellationToken = default) =>
        Task.FromResult(Validate(value, options));

    ValidationReport IValidator.Validate(object instance, ValidationOptions? options) => Validate((T)instance, options);

    Task<ValidationReport> IValidator.ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken) =>
        ValidateAsync((T)instance, options, cancellationToken);
}
