namespace DotThrow.Samples;

// A validator can be a subclass, which is handy for dependency injection...
public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.Street, street => street.IfNullOrWhiteSpace().IfLongerThan(100));
        For(x => x.City, city => city.IfNullOrWhiteSpace());
        For(x => x.PostalCode, code => code.IfNullOrWhiteSpace().IfNotMatches("^[0-9]{5}$"));
    }
}

public sealed class OrderLineValidator : Validator<OrderLine>
{
    public OrderLineValidator()
    {
        // IfNotSku is a custom check (CustomChecks.cs).
        For(x => x.Sku, sku => sku.IfNullOrWhiteSpace().IfNotSku());
        For(x => x.Quantity, quantity => quantity.IfOutOfRange(1, 100));
        For(x => x.Price, price => price.IfNegative().IfPrecisionScaleExceeded(9, 2));
    }
}

public static class OrderRules
{
    // ...or built inline, as here. Build a validator once and reuse it: it is thread-safe.
    public static Validator<Order> Create() => new Validator<Order>()
        .For(x => x.Number, number => number.IfNullOrWhiteSpace().IfNotStartsWith("ORD-"))
        .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress())
        .For(x => x.ShippingAddress, address => address.IfNull())
        .For(x => x.ShippingAddress, new AddressValidator())
        .For(x => x.Lines, lines => lines.IfNullOrEmpty().IfCountGreaterThan(20))
        .ForEach(x => x.Lines, new OrderLineValidator())
        .ForEach(x => x.Tags, tag => tag.IfNullOrWhiteSpace().IfLongerThan(10))
        .For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace(), rule => rule.When(x => x.IsGift))
        .Must(x => x.Total, total => total <= 10_000m, "The order total must not be more than 10000.");
}
