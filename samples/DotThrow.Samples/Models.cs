using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Samples;

/// <summary>A type that checks its own arguments with guard clauses.</summary>
public sealed class Customer
{
    public Customer(string name, string email, int age)
    {
        Name = name.Throw().IfNullOrWhiteSpace().IfLongerThan(100);
        Email = email.Throw().IfNullOrWhiteSpace().IfNotEmailAddress();
        Age = age.Throw().IfOutOfRange(0, 150);
    }

    public string Name { get; }

    public string Email { get; }

    public int Age { get; }
}

public sealed record SignUpRequest(string Name, string Email, string? MiddleName = null, int? Age = null);

public sealed record Address(string Street, string City, string PostalCode);

public sealed record OrderLine(string Sku, int Quantity, decimal Price);

public sealed record Order(string Number, string Email, Address? ShippingAddress, IReadOnlyList<OrderLine> Lines)
{
    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool IsGift { get; init; }

    public string? GiftMessage { get; init; }

    public decimal Total => Lines.Sum(line => line.Quantity * line.Price);
}

[Flags]
public enum FilePermissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
}

/// <summary>An exception from your own domain, thrown instead of ArgumentException.</summary>
public sealed class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; } = "Unknown";
}

/// <summary>Stands in for a database lookup in the async rule sample.</summary>
public sealed class FakeUserStore(params string[] takenEmails)
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        return takenEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }
}
