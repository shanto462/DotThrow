# DotThrow for FluentValidation users

DotThrow's `Validator<T>` has the features of FluentValidation 12: property rules, child
validators, collections, conditions, rule sets (groups), cascade, severity, custom state,
async rules, rule-level options, validator providers, polymorphic validators, property
filters, custom selectors, a test helper and an assembly scanner. This page maps every
FluentValidation feature to DotThrow, lists the differences that are on purpose, and
shows how to move a validator over.

The examples assume `using DotThrow;`.

- [The one idea to learn](#the-one-idea-to-learn)
- [Built-in validators](#built-in-validators)
- [Features](#features)
- [Intended differences](#intended-differences)
- [Migrating a validator](#migrating-a-validator)

## The one idea to learn

FluentValidation names say what must be true: `NotEmpty()`, `MaximumLength(50)`.
DotThrow names say what fails: `IfNullOrWhiteSpace()`, `IfLongerThan(50)`. The same
checks work as guard clauses (`name.Throw().IfLongerThan(50)`), where "if longer than 50,
throw" reads naturally. So every built-in validator maps to the check with the opposite
meaning: `GreaterThan(0)` becomes `IfLessThanOrEqualTo(0)`, or simply `IfNegativeOrZero()`.

A rule is a selector and a chain of checks:

```csharp
public sealed class CustomerValidator : Validator<Customer>
{
    public CustomerValidator()
    {
        For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(50));
        For(x => x.Age, age => age.IfOutOfRange(18, 120));
    }
}

public sealed record Customer(string Name, int Age);
```

## Built-in validators

| FluentValidation | DotThrow check | Notes |
| --- | --- | --- |
| `NotNull()` | `IfNull()` | On `int?` and other nullable value types it also unwraps the value. |
| `Null()` | `IfNotNull()` | |
| `NotEmpty()` | `IfEmpty()` | Same meaning for any type. For strings and collections `IfNullOrWhiteSpace()` and `IfNullOrEmpty()` say it more clearly. |
| `Empty()` | `IfNotEmpty()` | |
| `NotEqual(value)` | `IfEqualTo(value)` | |
| `Equal(value)` | `IfNotEqualTo(value)` | Strings take a `StringComparison`: `IfNotEqualTo(value, StringComparison.Ordinal)`. |
| `Equal(x => x.Other)` and other comparisons with a property | `(value, x) => value.IfNotEqualTo(x.Other)` | A chain with two parameters gets the object. |
| `Length(min, max)` | `IfLengthOutOfRange(min, max)` | |
| `Length(n)` | `IfLengthNotEqualTo(n)` | |
| `MinimumLength(n)` | `IfShorterThan(n)` | |
| `MaximumLength(n)` | `IfLongerThan(n)` | |
| `LessThan(v)` | `IfGreaterThanOrEqualTo(v)` | |
| `LessThanOrEqualTo(v)` | `IfGreaterThan(v)` | |
| `GreaterThan(v)` | `IfLessThanOrEqualTo(v)` | `IfNegativeOrZero()` when `v` is zero. |
| `GreaterThanOrEqualTo(v)` | `IfLessThan(v)` | `IfNegative()` when `v` is zero. |
| `InclusiveBetween(a, b)` | `IfOutOfRange(a, b)` | |
| `ExclusiveBetween(a, b)` | `IfOutOfRangeExclusive(a, b)` | |
| `Matches(pattern)` | `IfNotMatches(pattern)` | Also takes a `Regex`, such as one from `[GeneratedRegex]`. |
| `EmailAddress()` | `IfNotEmailAddress()` | Stricter: one `@` with text on both sides, no white space or control characters, no dot at the start or end of either side, no two dots in a row. |
| `CreditCard()` | `IfNotCreditCardNumber()` | Only ASCII digits count, and text without digits fails. |
| `IsInEnum()` | `IfNotDefined()` | A `[Flags]` combination of defined flags passes, as in FluentValidation. |
| `IsEnumName(typeof(E))` | `IfNotEnumName<E>()` | |
| `PrecisionScale(p, s, ignoreTrailingZeros)` | `IfPrecisionScaleExceeded(p, s, ignoreTrailingZeros)` | |
| `Must(v => ...)` | `IfNot(v => ...)` in a chain, or `Must(x => x.P, v => ...)` | `IfNot` fails when the predicate is false, like `Must`. |

DotThrow has about 90 other checks, such as `IfNotSlug`, `IfNotIPAddress`,
`IfWeekend`, `IfOlderThan` and `IfNotHttps`. See [guards.md](guards.md#checks-by-family).

## Features

"Same" means the same behavior, most rows with a test that runs both libraries.
"Equivalent" means the same result through another API shape. "Different" points to
[Intended differences](#intended-differences).

| FluentValidation 12 | DotThrow | |
| --- | --- | --- |
| `AbstractValidator<T>` | `Validator<T>` as a subclass | Same |
| `InlineValidator<T>` | `new Validator<T>().For(...)...` | Equivalent |
| `RuleFor(x => x.P).Check()...` | `For(x => x.P, p => p.IfX()...)` | Same names |
| `RuleFor(x => x).Must(...)` (model level) | `Must(x => ...)`; the failure has no name until `WithName` | Same |
| `Must(v => ...)`, `Must((x, v) => ...)` | `Must(x => x.P, v => ...)`, `Must(x => x.P, (x, v) => ...)`, or `IfNot` in a chain | Same |
| `Must((x, v, context) => ...)` | `Custom((x, context) => ...)` or a two-parameter chain | Equivalent |
| `MustAsync` | `MustAsync(x => ...)`, `MustAsync(x => x.P, (v, ct) => ...)`, `MustAsync(x => x.P, (x, v, ct) => ...)` | Same |
| `Custom`, `CustomAsync` | `Custom((x, context) => ...)`, `CustomAsync((x, context, ct) => ...)`, with `context.Check(...)` | Equivalent |
| `RuleForEach(x => x.Items)` | `ForEach(x => x.Items, item => ...)` | Same names |
| `RuleForEach(...).SetValidator(v)` | `ForEach(x => x.Items, v)` | Same |
| `ChildRules(...)` | `For(x => x.P, p => p.For(...))`, `ForEach(x => x.Items, item => item.For(...))` | Same |
| Nested `RuleForEach(...).ForEach(...)` | inline child rules with `ForEach(r => r, ...)` | Equivalent |
| `Where`, `WhereAsync` | `rule.Where(...)`, `rule.WhereAsync(...)` (indexes stay) | Same |
| `OverrideIndexer(...)` | `rule.WithIndexer((item, index) => ...)` | Same, without the root and collection arguments |
| `{CollectionIndex}` | `{CollectionIndex}` in `WithMessage` | Same |
| `SetValidator(validator)` | `For(x => x.P, validator)` | Same |
| `SetValidator(validator, ruleSets)` | `rule.WithChildGroups(...)` | Same |
| `SetValidator(x => validator)`, `SetValidator((x, v) => validator)` | `For(x => x.P, x => validator)`, `For(x => x.P, (x, p) => validator)`; `null` skips | Same |
| `SetInheritanceValidator(v => v.Add<TDerived>(...))` | `For(x => x.P, new PolymorphicValidator<TBase>().Add(...))` | Same |
| `Include(validator)`, `Include(x => validator)` | `Include(validator)` (a cycle is rejected when you build the validator), `Include(x => validator)` (a cycle stops at `ValidatorDefaults.MaxDepth`) | Same |
| `RuleSet("a,b", () => ...)` | `Group(["a", "b"], v => ...)` or `rule.InGroup("a", "b")` | Same |
| `IncludeRuleSets`, `IncludeAllRuleSets`, `IncludeRulesNotInRuleSet` | groups `"a"`, `ValidationGroups.All`, `ValidationGroups.Default` | Same |
| `RuleSetsExecuted` | `ValidationReport.GroupsExecuted` | Same |
| `IncludeProperties(...)` | `Validate(x, o => o.IncludeProperties(x => x.P))` or paths such as `"Items[].X"`; no expression trees | Same |
| `Validate(x, options => ...)` | `Validate(x, o => ...)` with `IncludeGroups`, `IncludeAllGroups`, `IncludeDefaultGroup`, `IncludeProperties`, `UseRuleSelector`, `ThrowOnFailure` | Same |
| `UseCustomSelector(IValidatorSelector)` | `UseRuleSelector(IRuleSelector)` | Same |
| `ThrowOnFailures()`, `ValidateAndThrow(Async)` | `ThrowOnFailure()`, `ValidateAndThrow(Async)` | Same |
| `RootContextData` | `ValidationOptions.Items`, `ValidationContext.Items` | Same |
| `When`, `Unless`, `WhenAsync`, `UnlessAsync` (rule) | `rule.When(...)` and the others, also with `(x, context) => ...` | Same |
| `ApplyConditionTo.CurrentValidator` | a two-parameter chain with plain C#, or two rules | Equivalent |
| `When(cond, () => ...).Otherwise(() => ...)` | `When(cond, v => ..., otherwise: v => ...)` | Same |
| `DependentRules(() => ...)` | `rule.DependentRules(v => ...)` | Same |
| `Cascade(...)` | `rule.Cascade(...)` | Same; per item for collections ([2](#2-default-rule-cascade-is-stop), [3](#3-stop-on-a-collection-applies-per-item)) |
| `RuleLevelCascadeMode`, `ClassLevelCascadeMode` | `RuleCascade`, `ClassCascade` | Same; default rule cascade is `Stop` ([2](#2-default-rule-cascade-is-stop)) |
| `ValidatorOptions.Global` | `ValidatorDefaults` | Same |
| `PropertyNameResolver`, `DisplayNameResolver` | `ValidatorDefaults.PropertyNameResolver(type, path)`, `DisplayNameResolver(type, path)` | Equivalent: based on paths, no `MemberInfo` |
| `OnFailureCreated` | `ValidatorDefaults.OnFailureCreated(failure)` | Equivalent |
| `ErrorCodeResolver` | `ValidatorDefaults.ErrorCodeResolver(checkCode)` | Different ([9](#9-the-error-code-resolver-gets-the-check-code)) |
| `PropertyChainSeparator` | `ValidatorDefaults.PropertyChainSeparator` | Same |
| `WithMessage(...)` (3 forms) | `rule.WithMessage(...)`, or `message:` on one check | Same |
| `WithName`, `OverridePropertyName` | `rule.WithName(...)`, `rule.OverridePropertyName(...)` | Same |
| `WithErrorCode` | `rule.WithErrorCode(...)` | Same |
| `WithSeverity(...)` (4 forms) | `rule.WithSeverity(...)` (4 forms) | Same |
| `WithState(...)` (3 forms) | `rule.WithState(...)` (3 forms) | Same |
| `Configure(rule => ...)` | the `configure` callback of every rule method | Equivalent |
| `PreValidate` | `protected override bool PreValidate(ValidationContext<T> context)` | Same |
| `RaiseValidationException` | `protected override Exception CreateValidationException(ValidationReport report)` | Equivalent |
| `OnRuleAdded` | `protected override void OnRuleAdded(RuleOptions<T, object?> rule)` | Same |
| `CreateDescriptor()` | `Describe()` | Equivalent |
| `CanValidateInstancesOfType` | `IValidator.CanValidate(Type)` | Same |
| `ValidationContext<T>` | `ValidationContext<T>` (`Instance`, `Items`, `Groups`, `PropertyPath`, `Parent`, `IsAsync`, `Check`, `AddFailure`) | Equivalent |
| `context.AddFailure(...)` (3 forms) | the same 3 forms | Same |
| `context.MessageFormatter.AppendArgument` | `context.AppendArgument(name, value)` | Same |
| `ValidationResult` (`IsValid`, `Errors`, `ToDictionary`, `ToString(separator)`) | `ValidationReport` (`IsValid`, `Failures`, `ToDictionary`, `ToString(separator)`) | Same |
| `ValidationResult.ToString()` | a summary: `Validation failed with N errors: ...` | Different |
| `ValidationFailure` (`PropertyName`, `ErrorMessage`, `ErrorCode`, `Severity`, `AttemptedValue`, `CustomState`, `FormattedMessagePlaceholderValues`) | `ValidationFailure` (`Name`, `Message`, `ErrorCode`, `Severity`, `AttemptedValue`, `CustomState`, `MessageArguments`) | Same |
| `ValidationException.Errors` | `ValidationFailedException.Failures` (and `Report`) | Same |
| `AsyncValidatorInvokedSynchronouslyException` | `AsyncRuleInvokedSynchronouslyException` (both `InvalidOperationException`) | Same |
| `TestHelper` (`TestValidate`, `ShouldHaveValidationErrorFor`, `With...`, `Without...`, `Only`, `ShouldHaveChildValidator`) | `DotThrow.Testing`, same names | Same |
| `AssemblyScanner` | `DotThrow.Scanning.AssemblyScanner` | Same, and it reports every `IValidator<>` a type implements |
| `LanguageManager`, message formatting | `MessageTemplates`, see [localization.md](localization.md) | Equivalent |
| DI extensions (`AddValidatorsFromAssembly`, `AddValidatorsFromAssemblyContaining`) | [`DotThrow.DependencyInjection`](../src/DotThrow.DependencyInjection/README.md), same names | Same |

## Intended differences

Each difference has a test that runs both libraries.

### 1. Null fails content checks

FluentValidation's `MaximumLength` passes a null value; DotThrow's `IfLongerThan`
reports it with the code `Null`. A guard clause must not let null through. To get the
FluentValidation result, start the chain with `AllowNull()`:
`For(x => x.MiddleName, m => m.AllowNull().IfLongerThan(50))`.

### 2. Default rule cascade is Stop

A DotThrow rule stops at its first failed check, so a null value is not reported five
times. Set `RuleCascade = CascadeMode.Continue` on a validator, or
`ValidatorDefaults.RuleCascade = CascadeMode.Continue` once, to get FluentValidation's
default. With Continue, a null value still stops the chain after one `Null` failure.

### 3. Stop on a collection applies per item

With `Stop`, FluentValidation stops a `RuleForEach` after the first item that fails.
DotThrow checks every item and stops each item's chain. To match DotThrow in
FluentValidation, give the collection rule `Cascade(CascadeMode.Continue)`.

### 4. Message names are property paths

The default message says `'PostalCode'`; FluentValidation says `'Postal Code'`. To split
names, set `ValidatorDefaults.DisplayNameResolver`:

```csharp
ValidatorDefaults.DisplayNameResolver = (type, path) =>
    System.Text.RegularExpressions.Regex.Replace(path, "(?<=[a-z])(?=[A-Z])", " ");
```

### 5. ToString

`ValidationReport.ToString()` is a summary with one line per failure.
`ToString(separator)` gives FluentValidation's format. Group names ignore case in both
libraries.

### 6. Inline child rules take the parent's cascade

FluentValidation's `ChildRules` use the global cascade defaults; DotThrow's inline child
rules use the cascade settings of the validator they are in.

### 7. Conditions apply to the whole rule

There is no `ApplyConditionTo.CurrentValidator`. A condition for one check is plain C# in
a two-parameter chain: `(name, x) => { name.IfNullOrEmpty(); if (x.IsCompany) { name.IfLongerThan(20); } }`.

### 8. The assembly scanner reports every interface

A type that implements `IValidator<A>` and `IValidator<B>` gives two results;
FluentValidation reports the first one only.

### 9. The error code resolver gets the check code

DotThrow checks are methods, not validator objects, so
`ValidatorDefaults.ErrorCodeResolver` gets the check's code, such as `"LongerThan"`.

### 10. Polymorphic factories

FluentValidation's root-only factory `Add<TDerived>(x => validator)` is written
`Add<TDerived, TRoot>((x, _) => validator)`. DotThrow also has a value-only form,
`Add<TDerived>(value => validator)`.

### 11. Property filters use the configured separator

`IncludeProperties` matches paths with `ValidatorDefaults.PropertyChainSeparator`;
FluentValidation always uses `"."` there.

## Migrating a validator

A FluentValidation validator:

<!-- snippet: skip -->
```csharp
using FluentValidation;

public class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(x => x.Number).NotEmpty().Matches("^ORD-[0-9]+$");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Please check the email address.");
        RuleFor(x => x.Total).GreaterThan(0).LessThanOrEqualTo(10_000);
        RuleFor(x => x.ShippingAddress).NotNull().SetValidator(new AddressValidator());
        RuleForEach(x => x.Lines).SetValidator(new OrderLineValidator());
        RuleFor(x => x.GiftMessage).NotEmpty().MaximumLength(200).When(x => x.IsGift);

        RuleSet("Admin", () =>
        {
            RuleFor(x => x.InternalNote).MaximumLength(500);
        });
    }
}
```

The same validator in DotThrow:

```csharp
public sealed class OrderValidator : Validator<Order>
{
    public OrderValidator()
    {
        For(x => x.Number, number => number.IfNullOrWhiteSpace().IfNotMatches("^ORD-[0-9]+$"));
        For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress(), rule => rule
            .WithMessage("Please check the email address."));
        For(x => x.Total, total => total.IfNegativeOrZero().IfGreaterThan(10_000m));
        For(x => x.ShippingAddress, address => address.IfNull());
        For(x => x.ShippingAddress, new AddressValidator());
        ForEach(x => x.Lines, new OrderLineValidator());
        For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace().IfLongerThan(200), rule => rule
            .When(x => x.IsGift));

        Group("Admin", admin => admin
            .For(x => x.InternalNote, note => note.AllowNull().IfLongerThan(500)));
    }
}

public sealed class AddressValidator : Validator<Address>
{
    public AddressValidator()
    {
        For(x => x.City, city => city.IfNullOrWhiteSpace());
    }
}

public sealed class OrderLineValidator : Validator<OrderLine>
{
    public OrderLineValidator()
    {
        For(x => x.Quantity, quantity => quantity.IfNegativeOrZero());
    }
}

public sealed record Address(string City);

public sealed record OrderLine(int Quantity);

public sealed record Order(string Number, string Email, decimal Total, Address? ShippingAddress, List<OrderLine> Lines, bool IsGift, string? GiftMessage, string? InternalNote);
```

Steps:

1. Replace the package and `using FluentValidation;` with `using DotThrow;`.
2. Change `AbstractValidator<T>` to `Validator<T>`, `RuleFor(...)` to `For(...)` and
   `RuleForEach(...)` to `ForEach(...)`.
3. Turn each built-in validator into the check with the opposite meaning, using the
   [table above](#built-in-validators).
4. Move rule options (`WithMessage`, `WithName`, `When`, `WithSeverity`, ...) into the
   `configure` callback: `rule => rule.WithMessage(...)`.
5. Turn rule sets into groups: `RuleSet("Admin", () => ...)` becomes
   `Group("Admin", admin => ...)`, and `IncludeRuleSets("Admin")` becomes
   `Validate(x, "Admin")`.
6. Most FluentValidation validators, such as `MaximumLength` and `EmailAddress`, let a
   null value pass; DotThrow's content checks fail it. Where null should pass, add
   `AllowNull()` at the start of the chain.
7. Read results from `report.Failures` (`Name`, `Message`, `ErrorCode`) instead of
   `result.Errors` (`PropertyName`, `ErrorMessage`, `ErrorCode`).
8. In tests, replace `using FluentValidation.TestHelper;` with `using DotThrow.Testing;`.
   The method names are the same.
9. If messages must stay exactly as they were, reword them with
   [message templates](localization.md), and set
   `ValidatorDefaults.DisplayNameResolver` to split property names
   ([difference 4](#4-message-names-are-property-paths)).

Performance: with the same rules on the same machine, DotThrow's `Validator<T>` was 2.7
times (small model, valid) to 7 times (nested model with failures) faster than
FluentValidation 12.1.1, and allocated 6% to 16% of its memory. See
[performance.md](performance.md).
