// FluentValidation's SetValidator on a nullable property gives CS8620 (see the main parity file).
#pragma warning disable CS8620, CS8602, CS8603, CS8631
#if NET8_0_OR_GREATER
// FluentValidation 12 targets .NET 8 and later only, so the parity tests do too.
using System;
using System.Linq;
using DotThrow.Tests.Validation.Models;
using FluentValidation;
using FluentValidation.Internal;
using Xunit;
using FV = FluentValidation;
using FVResults = FluentValidation.Results;

namespace DotThrow.Tests.Validation.Parity;

// Parity for the advanced features: validator providers, polymorphic factories, custom
// selectors, the property chain separator, the error code hook, message arguments,
// OnRuleAdded, context-aware options and typed property filters.
[Collection(nameof(GlobalDefaults))]
public sealed class FluentValidationAdvancedParityTests
{
    private static void Same(FVResults.ValidationResult expected, ValidationReport actual)
    {
        Assert.Equal(expected.Errors.Select(e => e.PropertyName), actual.Failures.Select(f => f.Name));
        Assert.Equal(expected.IsValid, actual.IsValid);
    }

    private static InlineValidator<Address> FvStrict()
    {
        var v = new InlineValidator<Address>();
        v.RuleFor(x => x.Street).NotEmpty();
        v.RuleFor(x => x.City).NotEmpty();
        return v;
    }

    private static InlineValidator<Address> FvCityOnly()
    {
        var v = new InlineValidator<Address>();
        v.RuleFor(x => x.City).NotEmpty();
        return v;
    }

    private static Validator<Address> DtStrict() =>
        new Validator<Address>().For(x => x.Street, s => s.IfNullOrWhiteSpace()).For(x => x.City, c => c.IfNullOrWhiteSpace());

    private static Validator<Address> DtCityOnly() => new Validator<Address>().For(x => x.City, c => c.IfNullOrWhiteSpace());

    [Fact]
    public void Validator_providers_for_children_collections_and_includes()
    {
        var fvStrict = FvStrict();
        var fvCity = FvCityOnly();
        var fvCompany = new InlineValidator<Person>();
        fvCompany.RuleFor(x => x.VatNumber).NotEmpty();
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Address).SetValidator(x => x.IsCompany ? fvStrict : fvCity);
        fv.RuleFor(x => x.Address).SetValidator((x, a) => x.Age > 100 ? fvCity : fvStrict).OverridePropertyName("Second");
        fv.RuleForEach(x => x.Addresses).SetValidator((x, a) => a.City == "x" ? fvCity : fvStrict);
        fv.Include(x => x.IsCompany ? fvCompany : new InlineValidator<Person>());

        var dtStrict = DtStrict();
        var dtCity = DtCityOnly();
        var dtCompany = new Validator<Person>().For(x => x.VatNumber, v => v.IfNullOrWhiteSpace());
        var dt = new Validator<Person>()
            .For(x => x.Address, x => x.IsCompany ? dtStrict : dtCity)
            .For(x => x.Address, (x, a) => x.Age > 100 ? dtCity : dtStrict, rule => rule.OverridePropertyName("Second"))
            .ForEach(x => x.Addresses, (x, a) => a.City == "x" ? dtCity : dtStrict)
            .Include(x => x.IsCompany ? dtCompany : new Validator<Person>());

        foreach (var person in new[]
        {
            new Person { Address = new Address(), Addresses = [new Address { City = "x" }, new Address()] },
            new Person { Address = new Address(), IsCompany = true, Age = 101, Addresses = [new Address()] },
        })
        {
            Same(fv.Validate(person), dt.Validate(person));
        }
    }

    [Fact]
    public void Polymorphic_factories_match()
    {
        var fvCard = new InlineValidator<CardPayment>();
        fvCard.RuleFor(x => x.CardNumber).NotEmpty();
        var fvBank = new InlineValidator<BankPayment>();
        fvBank.RuleFor(x => x.Iban).NotEmpty();
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x.Payment).SetInheritanceValidator(v => v
            .Add<CardPayment>((order, card) => order.Total > 100 ? fvCard : new InlineValidator<CardPayment>())
            .Add<BankPayment>(order => fvBank));

        var dtCard = new Validator<CardPayment>().For(x => x.CardNumber, c => c.IfNullOrWhiteSpace());
        var dtBank = new Validator<BankPayment>().For(x => x.Iban, i => i.IfNullOrWhiteSpace());
        var dt = new Validator<Order>().For(x => x.Payment, new PolymorphicValidator<Payment>()
            .Add<CardPayment, Order>((order, card) => order.Total > 100 ? dtCard : new Validator<CardPayment>())
            .Add<BankPayment, Order>((order, _) => dtBank));

        foreach (var order in new[]
        {
            new Order { Payment = new CardPayment() },
            new Order { Total = 500, Payment = new CardPayment() },
            new Order { Payment = new BankPayment() },
        })
        {
            Same(fv.Validate(order), dt.Validate(order));
        }
    }

    private sealed class FvPrefixSelector(string prefix) : IValidatorSelector
    {
        public bool CanExecute(IValidationRule rule, string propertyPath, IValidationContext context) =>
            propertyPath.StartsWith(prefix, StringComparison.Ordinal) || propertyPath == "Customer";
    }

    private sealed class DtPrefixSelector(string prefix) : IRuleSelector
    {
        public bool CanRun(RuleDescriptor rule, string propertyPath, ValidationContext context) =>
            propertyPath.StartsWith(prefix, StringComparison.Ordinal) || propertyPath == "Customer";
    }

    [Theory]
    [InlineData("Customer.A")]
    [InlineData("N")]
    public void Custom_selectors_match(string prefix)
    {
        var fvCustomer = new InlineValidator<Customer>();
        fvCustomer.RuleFor(x => x.Name).NotEmpty();
        fvCustomer.RuleFor(x => x.Address).SetValidator(FvStrict());
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x.Number).NotEmpty();
        fv.RuleFor(x => x.Customer).SetValidator(fvCustomer);
        fv.RuleSet("extra", () => fv.RuleFor(x => x.Total).GreaterThan(0));

        var dt = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrWhiteSpace())
            .For(x => x.Customer, new Validator<Customer>().For(x => x.Name, n => n.IfNullOrWhiteSpace()).For(x => x.Address, DtStrict()))
            .For(x => x.Total, t => t.IfNegativeOrZero(), rule => rule.InGroup("extra"));

        var order = new Order { Customer = new Customer { Address = new Address() } };
        Same(
            fv.Validate(order, o => o.UseCustomSelector(new FvPrefixSelector(prefix))),
            dt.Validate(order, o => o.UseRuleSelector(new DtPrefixSelector(prefix))));
    }

    [Fact]
    public void Property_chain_separator_matches()
    {
        var order = new Order { Customer = new Customer { Address = new Address() } };
        var separator = FV.ValidatorOptions.Global.PropertyChainSeparator;
        try
        {
            // Both libraries read the separator when a rule is added, so build after setting it.
            FV.ValidatorOptions.Global.PropertyChainSeparator = "_";
            ValidatorDefaults.PropertyChainSeparator = "_";
            var fvCustomer = new InlineValidator<Customer>();
            fvCustomer.RuleFor(x => x.Address).SetValidator(FvStrict());
            var fv = new InlineValidator<Order>();
            fv.RuleFor(x => x.Customer.Name).NotEmpty();
            fv.RuleFor(x => x.Customer).SetValidator(fvCustomer);
            var dt = new Validator<Order>()
                .For(x => x.Customer!.Name, n => n.IfNullOrWhiteSpace())
                .For(x => x.Customer, new Validator<Customer>().For(x => x.Address, DtStrict()));

            Same(fv.Validate(order), dt.Validate(order));
        }
        finally
        {
            FV.ValidatorOptions.Global.PropertyChainSeparator = separator;
            ValidatorDefaults.Reset();
        }
    }

    [Fact]
    public void Error_code_hook_applies_to_rules_without_their_own_code()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Name).NotEmpty();
        fv.RuleFor(x => x.Email).NotEmpty().WithErrorCode("Mine");
        var resolver = FV.ValidatorOptions.Global.ErrorCodeResolver;
        try
        {
            FV.ValidatorOptions.Global.ErrorCodeResolver = v => "X_" + v.Name;
            ValidatorDefaults.ErrorCodeResolver = code => "X_" + code;
            var dt = new Validator<Person>()
                .For(x => x.Name, n => n.IfNullOrWhiteSpace())
                .For(x => x.Email, e => e.IfNullOrWhiteSpace(), rule => rule.WithErrorCode("Mine"));

            var expected = fv.Validate(new Person());
            var actual = dt.Validate(new Person());
            Same(expected, actual);
            Assert.All(expected.Errors.Take(1).Concat(expected.Errors.Skip(1)), e => Assert.True(e.ErrorCode.StartsWith("X_", StringComparison.Ordinal) || e.ErrorCode == "Mine"));
            Assert.Equal(expected.Errors.Select(e => e.ErrorCode.StartsWith("X_", StringComparison.Ordinal)), actual.Failures.Select(f => f.ErrorCode.StartsWith("X_", StringComparison.Ordinal)));
            Assert.Equal("Mine", actual.Failures[1].ErrorCode);
        }
        finally
        {
            FV.ValidatorOptions.Global.ErrorCodeResolver = resolver;
            ValidatorDefaults.Reset();
        }
    }

    [Fact]
    public void Message_arguments_and_rule_placeholders_match()
    {
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x).Custom((order, context) =>
        {
            context.MessageFormatter.AppendArgument("Limit", 2);
            context.AddFailure("Lines", "At most {Limit} lines.");
        });
        fv.RuleFor(x => x.Number).NotEmpty().WithMessage("{PropertyName} at {PropertyPath} is '{PropertyValue}'.");
        fv.RuleForEach(x => x.Lines).NotNull().WithMessage("Line {CollectionIndex} is missing.");

        var dt = new Validator<Order>()
            .Custom((order, context) =>
            {
                context.AppendArgument("Limit", 2);
                context.AddFailure("Lines", "At most {Limit} lines.");
            })
            .For(x => x.Number, n => n.IfNullOrEmpty(), rule => rule.WithMessage("{PropertyName} at {PropertyPath} is '{PropertyValue}'."))
            .ForEach(x => x.Lines, l => l.IfNull(), rule => rule.WithMessage("Line {CollectionIndex} is missing."));

        var order = new Order { Number = "", Lines = [new Line(), null] };
        var expected = fv.Validate(order);
        var actual = dt.Validate(order);

        Same(expected, actual);
        Assert.Equal(expected.Errors.Select(e => e.ErrorMessage), actual.Failures.Select(f => f.Message));
        foreach (var key in new[] { "PropertyName", "PropertyValue", "PropertyPath" })
        {
            Assert.Equal(expected.Errors[1].FormattedMessagePlaceholderValues[key], actual.Failures[1].MessageArguments[key]);
        }

        Assert.Equal(expected.Errors[2].FormattedMessagePlaceholderValues["CollectionIndex"], actual.Failures[2].MessageArguments["CollectionIndex"]);
    }

    private sealed class FvHooked : AbstractValidator<Person>
    {
        public FvHooked()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.VatNumber).NotEmpty();
        }

        protected override void OnRuleAdded(IValidationRule<Person> rule)
        {
            if (rule.PropertyName == "VatNumber")
            {
                rule.ApplySharedCondition(context => context.InstanceToValidate.IsCompany);
            }
        }
    }

    private sealed class DtHooked : Validator<Person>
    {
        public DtHooked()
        {
            For(x => x.Name, n => n.IfNullOrWhiteSpace());
            For(x => x.VatNumber, v => v.IfNullOrWhiteSpace());
        }

        protected override void OnRuleAdded(RuleOptions<Person, object?> rule)
        {
            if (rule.PropertyPath == "VatNumber")
            {
                rule.When(x => x.IsCompany);
            }
        }
    }

    [Fact]
    public void OnRuleAdded_matches()
    {
        Same(new FvHooked().Validate(new Person()), new DtHooked().Validate(new Person()));
        Same(new FvHooked().Validate(new Person { IsCompany = true }), new DtHooked().Validate(new Person { IsCompany = true }));
    }

    [Fact]
    public void Context_aware_severity_and_state_match()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Name).NotEmpty()
            .WithSeverity((x, name, context) => context.RootContextData.ContainsKey("soft") ? FV.Severity.Info : FV.Severity.Error)
            .WithState((x, name, context) => x.Age + 1);
        var dt = new Validator<Person>().For(x => x.Name, n => n.IfNullOrWhiteSpace(), rule => rule
            .WithSeverity((x, name, context) => context.Items.ContainsKey("soft") ? DotThrow.Severity.Info : DotThrow.Severity.Error)
            .WithState((x, name, context) => x.Age + 1));

        var person = new Person { Age = 4 };
        var fvContext = new FV.ValidationContext<Person>(person);
        fvContext.RootContextData["soft"] = true;
        var options = new ValidationOptions();
        options.Items["soft"] = true;

        var expected = fv.Validate(fvContext);
        var actual = dt.Validate(person, options);
        Same(expected, actual);
        Assert.Equal(expected.Errors[0].Severity.ToString(), actual.Failures[0].Severity.ToString());
        Assert.Equal(expected.Errors[0].CustomState, actual.Failures[0].CustomState);
    }

    [Fact]
    public void Model_level_rule_with_a_computed_name_matches()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x).Must(x => x.Age > 0).WithName(x => x.IsCompany ? "Founded" : "Age");
        var dt = new Validator<Person>().Must(x => x.Age > 0, configure: rule => rule.WithName(x => x.IsCompany ? "Founded" : "Age"));

        Same(fv.Validate(new Person { IsCompany = true }), dt.Validate(new Person { IsCompany = true }));
        Same(fv.Validate(new Person()), dt.Validate(new Person()));
    }

    [Fact]
    public void Typed_IncludeProperties_matches()
    {
        var fvCustomer = new InlineValidator<Customer>();
        fvCustomer.RuleFor(x => x.Name).NotEmpty();
        fvCustomer.RuleFor(x => x.Address).SetValidator(FvStrict());
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x.Number).NotEmpty();
        fv.RuleFor(x => x.Customer).SetValidator(fvCustomer);

        var dt = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrWhiteSpace())
            .For(x => x.Customer, new Validator<Customer>().For(x => x.Name, n => n.IfNullOrWhiteSpace()).For(x => x.Address, DtStrict()));

        var order = new Order { Customer = new Customer { Address = new Address() } };
        Same(
            fv.Validate(order, o => o.IncludeProperties(x => x.Customer.Address.City, x => x.Number)),
            dt.Validate(order, o => o.IncludeProperties(x => x.Customer!.Address!.City).IncludeProperties(x => x.Number)));
    }
}
#endif
