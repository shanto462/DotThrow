using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.DependencyInjection.Tests;

public sealed class SignUp
{
    public string? Name { get; set; }

    public string? Email { get; set; }
}

public sealed class Order
{
    public int Quantity { get; set; }
}

public sealed class Note
{
    public string? Text { get; set; }
}

public sealed class Dog
{
    public string? Name { get; set; }
}

public sealed class Cat
{
    public string? Name { get; set; }
}

public sealed class SmtpOptions
{
    public string? Host { get; set; }

    public int Port { get; set; }

    public SmtpCredentials? Credentials { get; set; }
}

public sealed class SmtpCredentials
{
    public string? UserName { get; set; }
}

public sealed class SignUpValidator : Validator<SignUp>
{
    public SignUpValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace());
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());
    }
}

public sealed class OrderValidator : Validator<Order>
{
    public OrderValidator()
    {
        For(x => x.Quantity, quantity => quantity.IfLessThan(1));
    }
}

// A second validator for the same model, to check that both are registered.
public sealed class OrderLimitValidator : Validator<Order>
{
    public OrderLimitValidator()
    {
        For(x => x.Quantity, quantity => quantity.IfGreaterThan(100));
    }
}

// Internal: found only with includeInternalTypes.
internal sealed class NoteValidator : Validator<Note>
{
    public NoteValidator()
    {
        For(x => x.Text, text => text.IfNullOrEmpty());
    }
}

// One class that validates two models: registered for both interfaces.
public sealed class PetValidator : IValidator<Dog>, IValidator<Cat>
{
    private static readonly Validator<Dog> Dogs = new Validator<Dog>().For(x => x.Name, name => name.IfNullOrEmpty());
    private static readonly Validator<Cat> Cats = new Validator<Cat>().For(x => x.Name, name => name.IfNullOrEmpty());

    public bool CanValidate(Type type) => type == typeof(Dog) || type == typeof(Cat);

    public ValidationReport Validate(Dog value, ValidationOptions? options) => Dogs.Validate(value, options);

    public ValidationReport Validate(Cat value, ValidationOptions? options) => Cats.Validate(value, options);

    public ValidationReport Validate(object instance, ValidationOptions? options) =>
        instance is Dog dog ? Validate(dog, options) : Validate((Cat)instance, options);

    public Task<ValidationReport> ValidateAsync(Dog value, ValidationOptions? options, CancellationToken cancellationToken = default) => Dogs.ValidateAsync(value, options, cancellationToken);

    public Task<ValidationReport> ValidateAsync(Cat value, ValidationOptions? options, CancellationToken cancellationToken = default) => Cats.ValidateAsync(value, options, cancellationToken);

    public Task<ValidationReport> ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken = default) =>
        instance is Dog dog ? ValidateAsync(dog, options, cancellationToken) : ValidateAsync((Cat)instance, options, cancellationToken);
}

// Never registered: abstract classes and open generic classes cannot be created.
public abstract class BaseNameValidator<T> : Validator<T>
{
}

public sealed class OpenValidator<T> : Validator<T>
{
}

public sealed class SmtpOptionsValidator : Validator<SmtpOptions>
{
    public SmtpOptionsValidator()
    {
        For(x => x.Host, host => host.IfNullOrWhiteSpace());
        For(x => x.Port, port => port.IfOutOfRange(1, 65535));
        For(x => x.Credentials!.UserName, user => user.IfNullOrEmpty(), rule => rule.When(x => x.Credentials is not null));
        Must(x => x.Host != "localhost" || x.Port != 25, "Port 25 on localhost is not allowed.");
    }
}

public sealed class Contact
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public int Age { get; set; }

    public string? Country { get; set; }

    public List<string>? Tags { get; set; }

    public Address? Address { get; set; }
}

public sealed class Address
{
    public string? City { get; set; }
}
