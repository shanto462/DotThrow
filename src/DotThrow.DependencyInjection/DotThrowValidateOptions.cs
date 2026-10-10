using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotThrow.DependencyInjection;

/// <summary>
/// Validates an options class with a DotThrow <see cref="IValidator{T}"/>. It is what
/// <c>services.AddOptions&lt;T&gt;().ValidateWithDotThrow()</c> registers; you rarely create it yourself.
/// </summary>
/// <typeparam name="TOptions">The options class.</typeparam>
/// <remarks>
/// Options validation runs synchronously (the options system has no async validation), so the
/// validator must not have async rules: such a validator throws <see cref="AsyncRuleInvokedSynchronouslyException"/>.
/// Every failure fails the options, whatever its <see cref="Severity"/>.
/// </remarks>
public sealed class DotThrowValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IServiceProvider? _services;
    private readonly IValidator<TOptions>? _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotThrowValidateOptions{TOptions}"/> class that
    /// resolves <see cref="IValidator{T}"/> of <typeparamref name="TOptions"/> from
    /// <paramref name="services"/> each time options are validated. The validator is resolved in a
    /// new scope, so scoped validators (the default of assembly scanning) work too.
    /// </summary>
    /// <param name="name">The options name to validate, or <see langword="null"/> to validate every name.</param>
    /// <param name="services">The service provider, usually the root provider.</param>
    public DotThrowValidateOptions(string? name, IServiceProvider services)
    {
        Name = name;
        _services = services.Throw().IfNull().Value;
    }

    /// <summary>Initializes a new instance of the <see cref="DotThrowValidateOptions{TOptions}"/> class with a validator object.</summary>
    /// <param name="name">The options name to validate, or <see langword="null"/> to validate every name.</param>
    /// <param name="validator">The validator.</param>
    public DotThrowValidateOptions(string? name, IValidator<TOptions> validator)
    {
        Name = name;
        _validator = validator.Throw().IfNull().Value;
    }

    /// <summary>Gets the options name this instance validates, or <see langword="null"/> for every name.</summary>
    public string? Name { get; }

    /// <summary>Validates the options instance with the given name.</summary>
    /// <param name="name">The name of the options instance.</param>
    /// <param name="options">The options instance.</param>
    /// <returns>
    /// <see cref="ValidateOptionsResult.Skip"/> for another name, <see cref="ValidateOptionsResult.Success"/>
    /// when every rule passes, or a failure with one message per <see cref="ValidationFailure"/>, for
    /// example <c>"SmtpOptions.Port: 'Port' must be between 1 and 65535."</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">No <see cref="IValidator{T}"/> of <typeparamref name="TOptions"/> is registered.</exception>
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (Name is not null && !string.Equals(Name, name, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        options.Throw().IfNull();
        var report = _validator is not null ? _validator.Validate(options, null) : ValidateFromServices(options);
        return report.IsValid ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(Messages(report));
    }

    private static List<string> Messages(ValidationReport report)
    {
        var typeName = typeof(TOptions).Name;
        var messages = new List<string>(report.Failures.Count);
        foreach (var failure in report.Failures)
        {
            var path = failure.Name.Length == 0 ? typeName : typeName + "." + failure.Name;
            messages.Add(path + ": " + failure.Message);
        }

        return messages;
    }

    private ValidationReport ValidateFromServices(TOptions options)
    {
        using var scope = _services!.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var validator = scope.ServiceProvider.GetService<IValidator<TOptions>>()
            ?? throw new InvalidOperationException(
                $"No IValidator<{typeof(TOptions).Name}> is registered. Register one, for example with " +
                $"services.AddValidator<{typeof(TOptions).Name}, {typeof(TOptions).Name}Validator>(), or pass a validator to ValidateWithDotThrow.");
        return validator.Validate(options, null);
    }
}
