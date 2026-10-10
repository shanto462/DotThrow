# DotThrow.DependencyInjection

Microsoft.Extensions.DependencyInjection support for [DotThrow](https://github.com/shanto462/DotThrow)
validators:

- Register validators by assembly scanning, with the same methods as FluentValidation.
- Register one validator at a time without reflection (trimming and Native AOT safe).
- Validate options classes with a DotThrow validator, with `ValidateOnStart` and named options.

Targets netstandard2.0, .NET Framework 4.6.2, .NET 8, .NET 9 and .NET 10. It depends on
`DotThrow`, `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options`.

## Install

```shell
dotnet add package DotThrow.DependencyInjection
```

## Register validators

```csharp
using Microsoft.Extensions.DependencyInjection;

// Every public validator class in the assembly, scoped (one per request in web apps).
services.AddValidatorsFromAssemblyContaining<Program>();

// The same, with a lifetime, a filter and internal classes.
services.AddValidatorsFromAssembly(
    typeof(Program).Assembly,
    ServiceLifetime.Singleton,
    filter: result => result.ValidatorType != typeof(LegacyValidator),
    includeInternalTypes: true);

// No scanning: safe for trimming and Native AOT.
services.AddValidator<SignUp, SignUpValidator>();
services.AddValidator(new Validator<Contact>()
    .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress()));
services.AddValidator(sp => new OrderValidator(sp.GetRequiredService<IStockService>()));
```

Each validator class is registered the same way FluentValidation does it: as every
`IValidator<T>` it implements and as the class itself. Then ask for `IValidator<SignUp>` in a
constructor or a minimal API handler.

Assembly scanning uses reflection and is marked `[RequiresUnreferencedCode]`. In trimmed and Native
AOT apps use `AddValidator`.

## Validate options

```csharp
public sealed class SmtpOptions
{
    public string? Host { get; set; }

    public int Port { get; set; }
}

public sealed class SmtpOptionsValidator : Validator<SmtpOptions>
{
    public SmtpOptionsValidator()
    {
        For(x => x.Host, host => host.IfNullOrWhiteSpace());
        For(x => x.Port, port => port.IfOutOfRange(1, 65535));
    }
}

builder.Services.AddValidator<SmtpOptions, SmtpOptionsValidator>();
builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration("Smtp")
    .ValidateWithDotThrow()
    .ValidateOnStart();
```

With `"Port": 0` in the configuration the app does not start:

```text
Microsoft.Extensions.Options.OptionsValidationException: SmtpOptions.Port: 'Port' must be between 1 and 65535.
```

Other forms:

```csharp
// A validator object.
services.AddOptions<SmtpOptions>().ValidateWithDotThrow(new SmtpOptionsValidator());

// Rules written inline.
services.AddOptions<SmtpOptions>("backup")
    .BindConfiguration("BackupSmtp")
    .ValidateWithDotThrow(v => v.For(x => x.Port, port => port.IfOutOfRange(1, 65535)));
```

- Each failure becomes one message, named by its property path (`SmtpOptions.Credentials.UserName`).
- Named options are validated by name. Reloaded configuration is validated again.
- The registered validator may have any lifetime: it is resolved in its own scope.
- Options validation is synchronous, so the validator must not have async rules
  (`MustAsync`, `CustomAsync`); such a validator throws `AsyncRuleInvokedSynchronouslyException`.

## Moving from FluentValidation

`AddValidatorsFromAssembly`, `AddValidatorsFromAssemblies` and `AddValidatorsFromAssemblyContaining`
take the same arguments. The filter gets a `DotThrow.Scanning.AssemblyScanResult` with
`InterfaceType`, `ValidatorType` and `ModelType`. One difference: a class that implements several
`IValidator<T>` interfaces is registered for all of them (FluentValidation registers the first).

## Links

- [DotThrow on GitHub](https://github.com/shanto462/DotThrow)
- [Validation guide](https://github.com/shanto462/DotThrow/blob/main/docs/validation.md)
- [Changelog](https://github.com/shanto462/DotThrow/blob/main/CHANGELOG.md)
- [DotThrow.AspNetCore](https://www.nuget.org/packages/DotThrow.AspNetCore) for minimal API and MVC validation
