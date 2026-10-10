// API concern (see the test report): Validator.For and ForEach type the chain from the selector,
// so a string? property gives Guard<string?>, while the string checks take Guard<string>.
// Throw() and Check() take T? and do not have this problem. Until the API changes, the
// validator tests turn off the two nullability warnings this causes.
#pragma warning disable CS8619, CS8620

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Tests.Support;

public sealed class User
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public int Age { get; set; }

    public string? Role { get; set; }

    public Address? Address { get; set; }

    public IReadOnlyList<string?>? Tags { get; set; }

    public static User Valid() => new()
    {
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Age = 36,
        Role = "user",
        Address = new Address { City = "London", Zip = "N1 9GU" },
        Tags = ["math", "engines"],
    };
}

public sealed class Shipment
{
    public Address Destination { get; set; } = new();
}

public sealed class Address
{
    public string? City { get; set; }

    public string? Zip { get; set; }
}

public sealed class Order
{
    public decimal Total { get; set; }

    public IReadOnlyList<Line?>? Lines { get; set; }
}

public sealed class Line
{
    public string? Sku { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}

public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.City, city => city.IfNullOrWhiteSpace());
        For(x => x.Zip, zip => zip.IfNullOrEmpty().IfLongerThan(10));
    }
}

public sealed class LineValidator : Validator<Line>
{
    public LineValidator()
    {
        For(x => x.Sku, sku => sku.IfNullOrWhiteSpace());
        For(x => x.Quantity, quantity => quantity.IfNegativeOrZero());
    }
}

/// <summary>The subclass style: rules are added in the constructor.</summary>
public sealed class UserValidator : Validator<User>
{
    public UserValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(50));
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
        For(x => x.Age, age => age.IfNegative().IfGreaterThan(150));
        For(x => x.Address, new AddressValidator());
        ForEach(x => x.Tags, tag => tag.IfNullOrWhiteSpace().IfLongerThan(20));
    }
}

/// <summary>A hand-written validator that always reports one failure and records the groups it got.</summary>
public sealed class StubValidator<T> : IValidator<T>
{
    public List<string[]> Groups { get; } = [];

    public ValidationReport Validate(T value, params string[] groups)
    {
        Groups.Add(groups);
        return new ValidationReport().Add("Stub", "Stub failure.", "Stub");
    }

    public Task<ValidationReport> ValidateAsync(T value, IEnumerable<string>? groups, CancellationToken cancellationToken = default) =>
        Task.FromResult(Validate(value, groups is null ? [] : [.. groups]));
}
