using System;
using System.Collections.Generic;
using System.Linq;
using DotThrow.Scanning;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotThrow.DependencyInjection.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void Scanning_registers_the_interface_and_the_class_like_FluentValidation()
    {
        var services = new ServiceCollection().AddValidatorsFromAssemblyContaining<SignUpValidator>();

        AssertRegistered(services, typeof(IValidator<SignUp>), typeof(SignUpValidator), ServiceLifetime.Scoped);
        AssertRegistered(services, typeof(SignUpValidator), typeof(SignUpValidator), ServiceLifetime.Scoped);
        AssertRegistered(services, typeof(IValidator<Order>), typeof(OrderValidator), ServiceLifetime.Scoped);
        AssertRegistered(services, typeof(IValidator<Order>), typeof(OrderLimitValidator), ServiceLifetime.Scoped);
        AssertRegistered(services, typeof(IValidator<Dog>), typeof(PetValidator), ServiceLifetime.Scoped);
        AssertRegistered(services, typeof(IValidator<Cat>), typeof(PetValidator), ServiceLifetime.Scoped);

        // The non-generic IValidator is not registered (FluentValidation does not either).
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IValidator));
        Assert.Single(services, d => d.ServiceType == typeof(PetValidator));
    }

    [Fact]
    public void Scanning_skips_internal_abstract_and_open_generic_validators_by_default()
    {
        var services = new ServiceCollection().AddValidatorsFromAssemblyContaining<SignUpValidator>();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IValidator<Note>));
        Assert.DoesNotContain(services, d => d.ImplementationType is { IsAbstract: true } || d.ImplementationType is { IsGenericTypeDefinition: true });
    }

    [Fact]
    public void Scanning_with_internal_types_registers_internal_validators()
    {
        var services = new ServiceCollection().AddValidatorsFromAssemblyContaining<SignUpValidator>(includeInternalTypes: true);

        AssertRegistered(services, typeof(IValidator<Note>), typeof(NoteValidator), ServiceLifetime.Scoped);
    }

    [Fact]
    public void Scanning_twice_adds_nothing_new()
    {
        var once = new ServiceCollection().AddValidatorsFromAssemblyContaining<SignUpValidator>();
        var twice = new ServiceCollection()
            .AddValidatorsFromAssemblyContaining<SignUpValidator>()
            .AddValidatorsFromAssemblyContaining(typeof(OrderValidator))
            .AddValidatorsFromAssemblies([typeof(SignUp).Assembly, typeof(SignUp).Assembly]);

        Assert.Equal(once.Count, twice.Count);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void Scanning_uses_the_given_lifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection().AddValidatorsFromAssembly(typeof(SignUp).Assembly, lifetime);

        AssertRegistered(services, typeof(IValidator<SignUp>), typeof(SignUpValidator), lifetime);
        AssertRegistered(services, typeof(SignUpValidator), typeof(SignUpValidator), lifetime);
    }

    [Fact]
    public void Scanning_filter_skips_validators()
    {
        var seen = new List<AssemblyScanResult>();
        var services = new ServiceCollection().AddValidatorsFromAssemblies(
            [typeof(SignUp).Assembly],
            filter: result =>
            {
                seen.Add(result);
                return result.ValidatorType != typeof(OrderLimitValidator);
            });

        Assert.Contains(seen, r => r.ValidatorType == typeof(OrderLimitValidator) && r.ModelType == typeof(Order));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(OrderLimitValidator));
        AssertRegistered(services, typeof(IValidator<Order>), typeof(OrderValidator), ServiceLifetime.Scoped);
    }

    [Fact]
    public void Scanned_validators_resolve_with_their_lifetimes()
    {
        using var provider = new ServiceCollection()
            .AddValidatorsFromAssemblyContaining<SignUpValidator>(ServiceLifetime.Singleton, r => r.ModelType == typeof(SignUp))
            .AddValidatorsFromAssemblyContaining<OrderValidator>(ServiceLifetime.Transient, r => r.ModelType == typeof(Order))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        Assert.Same(provider.GetRequiredService<IValidator<SignUp>>(), provider.GetRequiredService<IValidator<SignUp>>());
        Assert.NotSame(provider.GetRequiredService<IValidator<Order>>(), provider.GetRequiredService<IValidator<Order>>());
        Assert.Equal(2, provider.GetServices<IValidator<Order>>().Count());

        // The last registration wins for a single service, as with any DI registration.
        Assert.IsType<OrderLimitValidator>(provider.GetRequiredService<IValidator<Order>>());
        Assert.False(provider.GetRequiredService<IValidator<SignUp>>().Validate(new SignUp(), null).IsValid);
    }

    [Fact]
    public void Scoped_validators_are_shared_in_a_scope()
    {
        using var provider = new ServiceCollection().AddValidatorsFromAssemblyContaining<SignUpValidator>().BuildServiceProvider(validateScopes: true);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var validator = first.ServiceProvider.GetRequiredService<IValidator<SignUp>>();
        Assert.Same(validator, first.ServiceProvider.GetRequiredService<IValidator<SignUp>>());
        Assert.NotSame(validator, second.ServiceProvider.GetRequiredService<IValidator<SignUp>>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IValidator<SignUp>>());
    }

    [Fact]
    public void AddValidator_registers_one_validator_without_scanning()
    {
        var services = new ServiceCollection()
            .AddValidator<SignUp, SignUpValidator>()
            .AddValidator<Order, OrderValidator>(ServiceLifetime.Singleton)
            .AddValidator<Order, OrderValidator>(ServiceLifetime.Singleton);

        AssertRegistered(services, typeof(IValidator<SignUp>), typeof(SignUpValidator), ServiceLifetime.Scoped);
        AssertRegistered(services, typeof(SignUpValidator), typeof(SignUpValidator), ServiceLifetime.Scoped);
        AssertRegistered(services, typeof(IValidator<Order>), typeof(OrderValidator), ServiceLifetime.Singleton);
        Assert.Equal(4, services.Count);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<OrderValidator>(provider.GetRequiredService<IValidator<Order>>());
        Assert.NotNull(provider.GetRequiredService<OrderValidator>());
    }

    [Fact]
    public void AddValidator_with_an_object_registers_a_singleton()
    {
        var inline = new Validator<Note>().For(x => x.Text, text => text.IfNullOrEmpty());
        var other = new Validator<Note>().For(x => x.Text, text => text.IfLongerThan(5));
        var services = new ServiceCollection().AddValidator(inline).AddValidator(other);

        // Two inline validators have the same class; both stay registered.
        Assert.Equal(2, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        using var provider = services.BuildServiceProvider();
        Assert.Equal([inline, other], provider.GetServices<IValidator<Note>>());
    }

    [Fact]
    public void AddValidator_with_a_factory_uses_the_lifetime()
    {
        var created = 0;
        using var provider = new ServiceCollection()
            .AddValidator(_ =>
            {
                created++;
                return new SignUpValidator();
            }, ServiceLifetime.Transient)
            .BuildServiceProvider();

        Assert.IsType<SignUpValidator>(provider.GetRequiredService<IValidator<SignUp>>());
        Assert.IsType<SignUpValidator>(provider.GetRequiredService<IValidator<SignUp>>());
        Assert.Equal(2, created);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        IServiceCollection services = new ServiceCollection();
        IServiceCollection missing = null!;

        Assert.Throws<ArgumentNullException>("services", () => missing.AddValidatorsFromAssembly(typeof(SignUp).Assembly));
        Assert.Throws<ArgumentNullException>("assembly", () => services.AddValidatorsFromAssembly(null!));
        Assert.Throws<ArgumentNullException>("services", () => missing.AddValidatorsFromAssemblies([typeof(SignUp).Assembly]));
        Assert.Throws<ArgumentNullException>("assemblies", () => services.AddValidatorsFromAssemblies(null!));
        Assert.Throws<ArgumentNullException>("type", () => services.AddValidatorsFromAssemblyContaining(null!));
        Assert.Throws<ArgumentNullException>("services", () => missing.AddValidator<SignUp, SignUpValidator>());
        Assert.Throws<ArgumentNullException>("services", () => missing.AddValidator(new SignUpValidator()));
        Assert.Throws<ArgumentNullException>("validator", () => services.AddValidator((IValidator<SignUp>)null!));
        Assert.Throws<ArgumentNullException>("services", () => missing.AddValidator(_ => new SignUpValidator()));
        Assert.Throws<ArgumentNullException>("factory", () => services.AddValidator((Func<IServiceProvider, IValidator<SignUp>>)null!));
    }

    private static void AssertRegistered(IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime) =>
        Assert.Single(services, d => d.ServiceType == serviceType && d.ImplementationType == implementationType && d.Lifetime == lifetime);
}
