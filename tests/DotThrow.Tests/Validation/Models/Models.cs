using System;
using System.Collections.Generic;

namespace DotThrow.Tests.Validation.Models;

// Test models. Every value is a placeholder (Ada Lovelace, ada@example.com).
public sealed class Person
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public int Age { get; set; }

    public int? Height { get; set; }

    public bool IsCompany { get; set; }

    public string? VatNumber { get; set; }

    public Address? Address { get; set; }

    public List<string?> Tags { get; set; } = [];

    public List<Address?> Addresses { get; set; } = [];

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public static Person Valid() => new()
    {
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Age = 36,
        Address = Address.Valid(),
        Tags = ["math"],
        Start = new DateTime(2026, 1, 1),
        End = new DateTime(2026, 1, 2),
    };
}

public sealed class Address
{
    public string? Street { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public static Address Valid() => new() { Street = "1 Example Road", City = "London", PostalCode = "12345" };
}

public sealed class Order
{
    public string? Number { get; set; }

    public decimal Total { get; set; }

    public List<Line?>? Lines { get; set; } = [];

    public Payment? Payment { get; set; }

    public Customer? Customer { get; set; }
}

public sealed class Line
{
    public string? Sku { get; set; }

    public int Quantity { get; set; }

    public bool Active { get; set; } = true;
}

public sealed class Customer
{
    public string? Name { get; set; }

    public Address? Address { get; set; }
}

public abstract class Payment
{
    public decimal Amount { get; set; }
}

public sealed class CardPayment : Payment
{
    public string? CardNumber { get; set; }
}

public sealed class BankPayment : Payment
{
    public string? Iban { get; set; }
}

public sealed class CashPayment : Payment
{
}

public sealed class TreeNode
{
    public string? Name { get; set; }

    public List<TreeNode> Children { get; set; } = [];

    public TreeNode? Parent { get; set; }
}
