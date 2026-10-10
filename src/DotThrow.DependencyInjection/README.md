# DotThrow.DependencyInjection

Microsoft.Extensions.DependencyInjection support for [DotThrow](https://github.com/shanto462/DotThrow)
validators:

- Register validators by assembly scanning, with the same methods as FluentValidation.
- Register one validator at a time without reflection (trimming and Native AOT safe).
- Validate options classes with a DotThrow validator, with `ValidateOnStart` and named options.
- Register dynamic validators whose rules live in `appsettings.json` (or any `IConfiguration`) and
  reload when the configuration changes.

Targets netstandard2.0, .NET Framework 4.6.2, .NET 8, .NET 9 and .NET 10. It depends on
`DotThrow`, `Microsoft.Extensions.Configuration.Abstractions`,
`Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`
and `Microsoft.Extensions.Options`.

## Install

```shell
dotnet add package DotThrow.DependencyInjection
```

## Packages

The DotThrow packages share one version:

- [DotThrow](https://www.nuget.org/packages/DotThrow): guard clauses, collect mode, `Validator<T>`, dynamic rules, message translations and analyzers. No dependencies.
- [DotThrow.DependencyInjection](https://www.nuget.org/packages/DotThrow.DependencyInjection) (this package): register validators in dependency injection, validate options classes, and dynamic validators from configuration.
- [DotThrow.AspNetCore](https://www.nuget.org/packages/DotThrow.AspNetCore): validation for minimal API endpoints and MVC actions, and validation problem helpers.

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

When several classes validate the same model, all of them are registered, so
`IEnumerable<IValidator<T>>` gets every one. A single `IValidator<T>` resolves to the last one
registered. Scanning registers in a fixed order: a class after its base classes, then by full type
name. So `StrictOrderValidator : OrderValidator` wins over `OrderValidator`, but between two unrelated
validators the winner only depends on their names. Keep one validator per model, or leave the others
out with the scanning `filter`.

Types that cannot be loaded (an assembly they need is missing) are skipped, so
`AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies())` works.

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

## Dynamic rules from configuration

Rules can live in configuration instead of code, in the shape of `RuleSetDefinition`
(see the [dynamic rules guide](https://github.com/shanto462/DotThrow/blob/main/docs/dynamic-rules.md)):

```json
{
  "Validation": {
    "SignUp": {
      "rules": [
        { "property": "Email", "checks": [ "WhiteSpace", "NotEmailAddress" ] },
        { "property": "Age", "severity": "Warning", "checks": [ { "check": "OutOfRange", "min": 18, "max": 130 } ] }
      ]
    }
  }
}
```

```csharp
// Reflection finds the properties.
builder.Services.AddDynamicValidator<SignUp>(builder.Configuration.GetSection("Validation:SignUp"));

// Trimming and Native AOT: name the properties the rules may check.
builder.Services.AddDynamicValidator(
    "Validation:SignUp",
    new PropertyMap<SignUp>().Add(x => x.Email).Add(x => x.Age),
    o =>
    {
        o.Checks = new CheckRegistry().Add<string>("NotReserved", (email, _) => email.IfOneOf("admin@example.com"));
        o.CodeRules = v => v.For(x => x.Password, p => p.IfNullOrEmpty().IfShorterThan(12));
        o.OnReloadError = (error, services) => services.GetRequiredService<RulesHealth>().Failed(error.Errors);
    });
```

- The validator is a singleton `IValidator<T>` and `DynamicValidator<T>`. One per type: a second
  call for the same type replaces the first.
- When the section changes (for example `reloadOnChange: true` on a JSON file), the rules are read
  again and swapped in one step. Changes to other sections, or saving the same values again, do
  nothing. Rules with errors are not used: the old rules stay, the failure is logged as an error
  through `ILogger<DynamicValidator<T>>`, and `OnReloadError` (if set) gets every error. Without
  logging and without `OnReloadError`, the failure goes to `Trace.TraceError`.
- A definition with errors fails when the validator is first resolved, and also when the host starts
  (`ValidateOnStart`, on by default), with every error and its place, for example
  `Rules[0].Checks[1].Chek (Validation:SignUp:rules:0:checks:1:chek): unknown key (did you mean 'check'?).`
- `AddDynamicValidator<T>(RuleSetDefinition)` registers rules from code, a database or
  `RuleSetDefinition.FromJson`; change them with `DynamicValidator<T>.Update`.
- The section is read without the configuration binder (`RuleSetConfiguration.Read`), so the
  `PropertyMap` overloads are safe for trimming and Native AOT. Configuration stores a JSON `null`
  as an empty string, so empty values read as not set.

## Moving from FluentValidation

`AddValidatorsFromAssembly`, `AddValidatorsFromAssemblies` and `AddValidatorsFromAssemblyContaining`
take the same arguments. The filter gets a `DotThrow.Scanning.AssemblyScanResult` with
`InterfaceType`, `ValidatorType` and `ModelType`. One difference: a class that implements several
`IValidator<T>` interfaces is registered for all of them (FluentValidation registers the first).

## Links

- [DotThrow on GitHub](https://github.com/shanto462/DotThrow)
- [Validation guide](https://github.com/shanto462/DotThrow/blob/main/docs/validation.md)
- [Changelog](https://github.com/shanto462/DotThrow/blob/main/CHANGELOG.md)
