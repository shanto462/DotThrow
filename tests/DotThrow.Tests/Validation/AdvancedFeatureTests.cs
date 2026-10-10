using System;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Testing;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// Validator providers, polymorphic factories, rule selectors, message arguments, OnRuleAdded,
// context-aware options and typed property filters.
public sealed class AdvancedFeatureTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly AddressValidator Strict = new();
    private static readonly Validator<Address> CityOnly = new Validator<Address>().For(x => x.City, c => c.IfNullOrEmpty());

    [Fact]
    public void For_with_a_provider_picks_the_validator_per_object_and_null_skips()
    {
        var validator = new Validator<Person>()
            .For(x => x.Address, x => x.IsCompany ? Strict : (IValidator<Address>?)CityOnly)
            .For(x => x.Address, (x, address) => x.Age > 100 ? null : CityOnly, rule => rule.OverridePropertyName("Second"));

        var address = new Address();
        Assert.Equal(["Address.City", "Second.City"], Names.Of(validator.Validate(new Person { Address = address })));
        Assert.Equal(["Address.Street", "Address.City", "Address.PostalCode", "Second.City"], Names.Of(validator.Validate(new Person { Address = address, IsCompany = true })));
        Assert.Equal(["Address.City"], Names.Of(validator.Validate(new Person { Address = address, Age = 101 })));
        Assert.Equal(typeof(IValidator<Address>), validator.Describe().Rules[0].ChildValidatorType);
    }

    [Fact]
    public async Task ForEach_with_a_provider_picks_per_item_in_sync_and_async_runs()
    {
        var validator = new Validator<Person>()
            .ForEach(x => x.Addresses, (x, address) => address.City == "skip" ? null : Strict)
            .ForEach(x => x.Addresses, x => CityOnly, rule => rule.OverridePropertyName("Again"));

        var person = new Person { Addresses = [new Address { City = "skip" }, new Address { Street = "s", PostalCode = "12345" }] };
        string[] expected = ["Addresses[1].City", "Again[1].City"];

        Assert.Equal(expected, Names.Of(validator.Validate(person)));
        Assert.Equal(expected, Names.Of(await validator.ValidateAsync(person, Token)));
    }

    [Fact]
    public async Task Include_with_a_provider_picks_the_validator_per_object()
    {
        var company = new Validator<Person>().For(x => x.VatNumber, v => v.IfNullOrEmpty());
        var person = new Validator<Person>().For(x => x.Name, n => n.IfNullOrEmpty());
        var validator = new Validator<Person>().Include(x => x.IsCompany ? company : person).Include(x => (Validator<Person>?)null);

        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person())));
        var companyNames = Names.Of(await validator.ValidateAsync(new Person { IsCompany = true }, Token));
        Assert.Equal(["VatNumber"], companyNames);
    }

    [Fact]
    public void Polymorphic_factories_and_untyped_registration()
    {
        var card = new CardPaymentValidator();
        var lenient = new Validator<CardPayment>();
#pragma warning disable CA2263 // The test covers the non-generic Add on purpose.
        var payments = new PolymorphicValidator<Payment>()
            .Add<CardPayment, Order>((order, payment) => order.Total > 100 ? card : lenient)
            .Add<BankPayment>(bank => bank.Amount > 0 ? new Validator<BankPayment>().For(x => x.Iban, i => i.IfNullOrEmpty()) : null)
            .Add(typeof(CashPayment), new Validator<Payment>().Must(p => p.Amount < 50, "Too much cash.", rule => rule.WithName("Amount")));
#pragma warning restore CA2263
        var validator = new Validator<Order>().For(x => x.Payment, payments);

        Assert.True(validator.Validate(new Order { Payment = new CardPayment() }).IsValid);
        Assert.Equal(["Payment.CardNumber", "Payment.Amount"], Names.Of(validator.Validate(new Order { Total = 500, Payment = new CardPayment() })));
        Assert.Equal(["Payment.Iban"], Names.Of(validator.Validate(new Order { Payment = new BankPayment { Amount = 1 } })));
        Assert.True(validator.Validate(new Order { Payment = new BankPayment() }).IsValid);
        Assert.Equal(["Payment.Amount"], Names.Of(validator.Validate(new Order { Payment = new CashPayment { Amount = 60 } })));
        Assert.Throws<ArgumentException>(() => payments.Add(typeof(Person), new Validator<Person>()));
        Assert.Throws<ArgumentException>(() => payments.Add(typeof(CashPayment), new Validator<Person>()));
    }

    private sealed class PathSelector(string prefix) : IRuleSelector
    {
        public bool CanRun(RuleDescriptor rule, string propertyPath, ValidationContext context) =>
            propertyPath.StartsWith(prefix, StringComparison.Ordinal) || rule.Kind == RuleKind.Child;
    }

    [Fact]
    public void Custom_rule_selector_filters_every_rule_including_child_rules()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .For(x => x.Address, new AddressValidator())
            .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.InGroup("contact"));

        var person = new Person { Address = new Address() };
        var options = new ValidationOptions().UseRuleSelector(new PathSelector("Address.C"));

        Assert.Equal(["Address.City"], Names.Of(validator.Validate(person, options)));
        Assert.Equal(["Email"], Names.Of(validator.Validate(person, new ValidationOptions { RuleSelector = new PathSelector("E") })));

        // A custom selector and groups combine with OR, like FluentValidation's composite selector
        // (UseCustomSelector plus IncludeRuleSets("other") still runs Email there).
        Assert.Equal(["Email"], Names.Of(validator.Validate(person, new ValidationOptions { RuleSelector = new PathSelector("E"), Groups = ["other"] })));
        Assert.Empty(validator.Validate(person, new ValidationOptions { RuleSelector = new PathSelector("X"), Groups = ["other"] }).Failures);
    }

    [Fact]
    public void Message_arguments_fill_placeholders_and_are_stored_on_failures()
    {
        var validator = new Validator<Order>()
            .Custom((order, context) =>
            {
                context.AppendArgument("Limit", 2);
                if (order.Lines!.Count > 2)
                {
                    context.AddFailure("Lines", "An order can have at most {Limit} lines, not {Count}.");
                }
            })
            .For(x => x.Number, n => n.IfNullOrEmpty(), rule => rule.WithMessage("{PropertyName} at {PropertyPath} is {PropertyValue}."))
            .ForEach(x => x.Lines, l => l.IfNull(), rule => rule.WithMessage("Line {CollectionIndex} is missing."));

        // Text values are quoted by DotThrow, as in check messages, so the template has no quotes.
        var result = validator.TestValidate(new Order { Number = "", Lines = [new Line(), null, new Line()] });

        result.ShouldHaveValidationErrorFor("Lines").WithErrorMessage("An order can have at most 2 lines, not {Count}.").WithMessageArgument("Limit", 2);
        result.ShouldHaveValidationErrorFor(x => x.Number)
            .WithErrorMessage("Number at Number is ''.")
            .WithMessageArgument("PropertyName", "Number")
            .WithMessageArgument("PropertyPath", "Number")
            .WithMessageArgument("PropertyValue", string.Empty);
        result.ShouldHaveValidationErrorFor("Lines[1]").WithErrorMessage("Line 1 is missing.").WithMessageArgument("CollectionIndex", 1);
        Assert.DoesNotContain("Limit", result.Failures[1].MessageArguments.Keys);
        Assert.Empty(new ValidationFailure("a", "b", "c").MessageArguments);
    }

    [Fact]
    public void Arguments_appended_in_a_condition_stay_with_that_rule()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule
                .When((x, context) => { context.AppendArgument("Hint", "full name"); return true; })
                .WithMessage("Please enter your {Hint}."))
            .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.WithMessage("{Hint} is not set."));

        var report = validator.Validate(new Person());

        Assert.Equal(["Please enter your full name.", "{Hint} is not set."], Names.Messages(report));
        Assert.False(report.Failures[1].MessageArguments.ContainsKey("Hint"));
    }

    [Fact]
    public void Object_level_rule_takes_its_name_from_a_computed_WithName()
    {
        var validator = new Validator<Person>().Must(x => x.Age > 0, "Too young.", rule => rule.WithName(x => x.IsCompany ? "Founded" : "Age"));

        Assert.Equal(["Founded"], Names.Of(validator.Validate(new Person { IsCompany = true })));
    }

    private sealed class WarningOnInternal : Validator<Person>
    {
        public WarningOnInternal()
        {
            For(x => x.Name, n => n.IfNullOrEmpty());
            For(x => x.VatNumber, v => v.IfNullOrEmpty());
            ForEach(x => x.Tags, t => t.IfNullOrEmpty());
        }

        protected override void OnRuleAdded(RuleOptions<Person, object?> rule)
        {
            if (rule.PropertyPath == "VatNumber")
            {
                rule.WithSeverity(Severity.Warning).When(x => x.IsCompany);
            }

            if (rule.Kind == RuleKind.Collection)
            {
                rule.WithState((x, value) => value);
            }
        }
    }

    [Fact]
    public void OnRuleAdded_can_change_every_rule()
    {
        var validator = new WarningOnInternal();

        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person())));
        var report = validator.Validate(new Person { IsCompany = true, Name = "Ada", Tags = [""] });

        Assert.Equal(Severity.Warning, report.Failures[0].Severity);
        Assert.Equal(string.Empty, report.Failures[1].CustomState);
    }

    [Fact]
    public void Context_aware_severity_and_state()
    {
        var validator = new Validator<Person>().For(
            x => x.Name,
            n => n.IfNullOrEmpty(),
            rule => rule
                .WithSeverity((x, name, context) => context.Items.ContainsKey("soft") ? Severity.Info : Severity.Error)
                .WithState((x, name, context) => context.PropertyPath + "|" + x.Age));

        var options = new ValidationOptions();
        options.Items["soft"] = true;
        var failure = Assert.Single(validator.Validate(new Person { Age = 3 }, options).Failures);

        Assert.Equal(Severity.Info, failure.Severity);
        Assert.Equal("|3", failure.CustomState);
    }

    [Fact]
    public async Task Typed_IncludeProperties_and_fluent_options()
    {
        var validator = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrEmpty())
            .For(x => x.Customer, new Validator<Customer>().For(x => x.Name, n => n.IfNullOrEmpty()).For(x => x.Address, new AddressValidator()))
            .For(x => x.Total, t => t.IfNegative(), rule => rule.InGroup("money"));
        var order = new Order { Total = -1, Customer = new Customer { Address = new Address() } };

        Assert.Equal(["Customer.Address.City"], Names.Of(validator.Validate(order, o => o.IncludeProperties(x => x.Customer!.Address!.City))));
        // FluentValidation 12 combines properties and rule sets with OR (CompositeValidatorSelector
        // uses Any): IncludeRulesNotInRuleSet also selects the Customer rule, so the whole child
        // runs. Checked against FluentValidation: Number, Customer.Name, Customer.Address.*, Total.
        var filtered = Names.Of(await validator.ValidateAsync(order, o => o.IncludeProperties(x => x.Number).IncludeProperties("Total").IncludeGroups("money").IncludeDefaultGroup(), Token));
        Assert.Equal(["Number", "Customer.Name", "Customer.Address.Street", "Customer.Address.City", "Customer.Address.PostalCode", "Total"], filtered);
        var propertiesOnly = Names.Of(await validator.ValidateAsync(order, o => o.IncludeProperties(x => x.Number).IncludeProperties("Total"), Token));
        Assert.Equal(["Number", "Total"], propertiesOnly);
        // "*" selects every rule, and OR with the property filter keeps them all (6, as in FluentValidation).
        Assert.Equal(6, validator.Validate(order, o => o.IncludeAllGroups().IncludeProperties(x => x.Customer).IncludeProperties(x => x.Total)).Failures.Count);
        Assert.Equal(5, validator.Validate(order, o => o.IncludeProperties(x => x.Customer).IncludeProperties(x => x.Total)).Failures.Count);
        Assert.Throws<ValidationFailedException>(() => validator.Validate(order, o => o.ThrowOnFailure()));
        validator.TestValidate(order, o => o.IncludeProperties(x => x.Number)).ShouldHaveValidationErrorFor(x => x.Number).Only();
        IValidator<Order> contract = validator;
        Assert.Single(contract.Validate(order, o => o.IncludeProperties(x => x.Number)).Failures);
    }

    [Fact]
    public void Property_filter_without_groups_also_runs_rules_in_groups_like_FluentValidation()
    {
        var validator = new Validator<Order>().For(x => x.Total, t => t.IfNegative(), rule => rule.InGroup("money"));

        Assert.True(validator.Validate(new Order { Total = -1 }).IsValid);
        Assert.Single(validator.Validate(new Order { Total = -1 }, o => o.IncludeProperties(x => x.Total)).Failures);
    }
}

// Global hooks: property chain separator and error code resolver.
[Collection(nameof(GlobalDefaults))]
public sealed class GlobalHookTests : IDisposable
{
    public void Dispose() => ValidatorDefaults.Reset();

    [Fact]
    public void Property_chain_separator_applies_to_paths_prefixes_and_filters()
    {
        ValidatorDefaults.PropertyChainSeparator = "_";
        var validator = new Validator<Order>()
            .For(x => x.Customer!.Name, n => n.IfNullOrEmpty())
            .For(x => x.Customer, new Validator<Customer>().For(x => x.Address, new AddressValidator()));
        var order = new Order { Customer = new Customer { Address = new Address() } };

        Assert.Equal(["Customer_Name", "Customer_Address_Street", "Customer_Address_City", "Customer_Address_PostalCode"], Names.Of(validator.Validate(order)));
        Assert.Equal(["Customer_Address_City"], Names.Of(validator.Validate(order, o => o.IncludeProperties(x => x.Customer!.Address!.City))));
        Assert.Throws<ArgumentException>(() => ValidatorDefaults.PropertyChainSeparator = "");
    }

    [Fact]
    public void Error_code_resolver_changes_codes_of_rules_without_their_own()
    {
        ValidatorDefaults.ErrorCodeResolver = code => "DT_" + code;
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .For(x => x.Email, e => e.IfNullOrEmpty(), rule => rule.WithErrorCode("Mine"));

        Assert.Equal(["DT_Null", "Mine"], Names.Codes(validator.Validate(new Person())));
        var standalone = new ValidationReport();
        standalone.Check((string?)null, "x").IfNull();
        Assert.Equal(ErrorCodes.Null, standalone.Failures[0].ErrorCode);
    }
}
