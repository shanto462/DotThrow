# DotThrow

Guard clauses and validation for .NET, with one set of checks. As a guard clause, a chain
of checks throws the right exception with the parameter name filled in. In collect mode,
the same checks record every error in a report that you can return from an API.
`Validator<T>` has the features of FluentValidation 12.

- More than 110 checks: strings, numbers, dates, collections, dictionaries, enums, URIs,
  IP addresses, streams and types.
- MIT licensed, no dependencies.
- The common checks allocate nothing when they pass.
- Message templates with placeholders, per culture.
- Targets netstandard2.0, netstandard2.1, net462, net8.0, net9.0 and net10.0. Trimming
  and Native AOT safe on .NET 8 and later.

## Install

```shell
dotnet add package DotThrow
```

## Guard clauses

```csharp
using DotThrow;

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
```

`new Customer("Ada Lovelace", "ada@example.com", 200)` throws:

```text
System.ArgumentOutOfRangeException: 'age' must be between 0 and 150. (Parameter 'age')
Actual value was 200.
```

## Collect every error

```csharp
var form = new SignUpForm(Name: "", Email: "ada.example.com", Age: 12);

var report = new ValidationReport();
report.Check(form, f => f.Name).IfNullOrWhiteSpace();
report.Check(form, f => f.Email).IfNullOrWhiteSpace().IfNotEmailAddress();
report.Check(form, f => f.Age).IfLessThan(13, "You must be 13 or older.");

Console.WriteLine(report);

public sealed record SignUpForm(string Name, string Email, int Age);
```

```text
Validation failed with 3 errors:
 - Name: 'Name' must not be empty or white space.
 - Email: 'Email' must be a valid email address.
 - Age: You must be 13 or older.
```

In ASP.NET Core, `Results.ValidationProblem(report.ToDictionary())` turns the report into
a standard 400 response.

## Reusable validators

```csharp
public sealed class SignUpFormValidator : Validator<SignUpForm>
{
    public SignUpFormValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(100));
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress(), rule => rule
            .WithMessage("Please check the email address."));
        For(x => x.Age, age => age.IfLessThan(13, "You must be 13 or older."));
    }
}
```

```csharp
var validator = new SignUpFormValidator();
ValidationReport result = validator.Validate(new SignUpForm("Ada Lovelace", "ada@example.com", 36));
```

Validators also handle nested objects, collections, conditions, groups, severity, async
rules and polymorphic types, and `DotThrow.Testing` has test helpers with the names of
FluentValidation's `TestHelper`.

## Learn more

- [Documentation and source](https://github.com/shanto462/DotThrow)
- [Every check](https://github.com/shanto462/DotThrow/blob/main/docs/guards.md)
- [Validation](https://github.com/shanto462/DotThrow/blob/main/docs/validation.md)
- [DotThrow for FluentValidation users](https://github.com/shanto462/DotThrow/blob/main/docs/fluentvalidation.md)
- [Messages and localization](https://github.com/shanto462/DotThrow/blob/main/docs/localization.md)
- [Writing your own checks](https://github.com/shanto462/DotThrow/blob/main/docs/custom-checks.md)
- [Performance](https://github.com/shanto462/DotThrow/blob/main/docs/performance.md)
- [Migrating from DotThrow 1.x](https://github.com/shanto462/DotThrow/blob/main/docs/migrating-from-v1.md)
- [Release notes](https://github.com/shanto462/DotThrow/blob/main/CHANGELOG.md)
