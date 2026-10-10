using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Tests.Validation.Users;

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

/// <summary>
/// A hand-written validator that always reports one failure and records the groups it got.
/// It implements only the interface members, as a mock would.
/// </summary>
public sealed class StubValidator<T> : IValidator<T>
{
    public List<string[]> Groups { get; } = [];

    public bool CanValidate(Type type) => typeof(T).IsAssignableFrom(type);

    public ValidationReport Validate(T value, ValidationOptions? options)
    {
        Groups.Add(options?.Groups is { } groups ? [.. groups] : []);
        return new ValidationReport().Add("Stub", "Stub failure.", "Stub");
    }

    public Task<ValidationReport> ValidateAsync(T value, ValidationOptions? options, CancellationToken cancellationToken = default) =>
        Task.FromResult(Validate(value, options));

    public ValidationReport Validate(object instance, ValidationOptions? options) => Validate((T)instance, options);

    public Task<ValidationReport> ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken = default) =>
        ValidateAsync((T)instance, options, cancellationToken);
}
