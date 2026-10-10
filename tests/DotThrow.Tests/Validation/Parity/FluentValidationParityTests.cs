// FluentValidation's SetValidator on a nullable property (Address?) gives CS8620: the same
// nullability friction that decision D9 removes from DotThrow's For and ForEach.
#pragma warning disable CS8620, CS8602
#if NET8_0_OR_GREATER
// FluentValidation 12 targets .NET 8 and later only, so the parity tests do too.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Validation.Models;
using FluentValidation;
using Xunit;
using FV = FluentValidation;
using FVResults = FluentValidation.Results;

namespace DotThrow.Tests.Validation.Parity;

// Runs the same models through FluentValidation 12 and DotThrow and compares the outcome
// (failure names and order, codes set by the rule, severity, state, attempted value, groups)
// for every feature where DotThrow claims the same behavior. Messages are not compared: the
// wording of the built-in checks differs on purpose. Intended differences have their own tests
// at the end, which assert the difference and the DotThrow way to get the FluentValidation result.
[Collection(nameof(GlobalDefaults))]
public sealed class FluentValidationParityTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void Same(FVResults.ValidationResult expected, ValidationReport actual)
    {
        Assert.Equal(expected.Errors.Select(e => e.PropertyName), actual.Failures.Select(f => f.Name));
        Assert.Equal(expected.IsValid, actual.IsValid);
    }

    private static Order BadOrder() => new()
    {
        Number = "",
        Total = -5,
        Lines = [new Line { Sku = "A-1", Quantity = 0 }, null, new Line { Sku = "", Quantity = 5, Active = false }, new Line { Sku = "", Quantity = 500 }],
        Customer = new Customer { Name = "", Address = new Address { Street = "", City = "London", PostalCode = "x" } },
        Payment = new CardPayment { Amount = 0 },
    };

    private sealed class FvAddress : AbstractValidator<Address>
    {
        public FvAddress()
        {
            RuleFor(x => x.Street).NotEmpty();
            RuleFor(x => x.City).NotEmpty().MaximumLength(20);
            RuleFor(x => x.PostalCode).NotEmpty().Matches("^[0-9]{5}$");
        }
    }

    private sealed class FvLine : AbstractValidator<Line>
    {
        public FvLine()
        {
            RuleFor(x => x.Sku).NotEmpty();
            RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
        }
    }

    private sealed class FvCustomer : AbstractValidator<Customer>
    {
        public FvCustomer()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Address).SetValidator(new FvAddress());
        }
    }

    private static Validator<Customer> DtCustomer() => new Validator<Customer>()
        .For(x => x.Name, n => n.IfNullOrWhiteSpace())
        .For(x => x.Address, new AddressValidator());

    [Fact]
    public void Property_child_collection_and_nested_names_match()
    {
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x.Number).NotEmpty();
        fv.RuleFor(x => x.Total).GreaterThan(0);
        fv.RuleForEach(x => x.Lines).SetValidator(new FvLine());
        fv.RuleFor(x => x.Customer).SetValidator(new FvCustomer());
        fv.RuleFor(x => x).Must(x => x.Total < 1000).WithName("Total");

        var dt = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrWhiteSpace())
            .For(x => x.Total, t => t.IfNegativeOrZero())
            .ForEach(x => x.Lines, new LineValidator())
            .For(x => x.Customer, DtCustomer())
            .Must(x => x.Total < 1000, configure: rule => rule.WithName("Total"));

        var order = BadOrder();
        Same(fv.Validate(order), dt.Validate(order));
        order.Total = 5000;
        Same(fv.Validate(order), dt.Validate(order));
    }

    [Fact]
    public void Collection_of_simple_values_names_items_by_index()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleForEach(x => x.Tags).NotEmpty();
        var dt = new Validator<Person>().ForEach(x => x.Tags, t => t.IfNullOrWhiteSpace());

        var person = new Person { Tags = ["a", "", " ", "b", null] };
        Same(fv.Validate(person), dt.Validate(person));
    }

    [Fact]
    public void Where_keeps_original_indexes_and_the_indexer_can_be_replaced()
    {
        var fv = new InlineValidator<Order>();
        fv.RuleForEach(x => x.Lines).Where(l => l != null && l.Active).SetValidator(new FvLine());
        fv.RuleForEach(x => x.Lines).Where(l => l != null).OverrideIndexer((_, _, line, _) => "<" + line!.Sku + ">").SetValidator(new FvLine());

        var dt = new Validator<Order>()
            .ForEach(x => x.Lines, new LineValidator(), rule => rule.Where(l => l is not null && l.Active))
            .ForEach(x => x.Lines, new LineValidator(), rule => rule.Where(l => l is not null).WithIndexer((line, _) => "<" + line!.Sku + ">"));

        Same(fv.Validate(BadOrder()), dt.Validate(BadOrder()));
    }

    [Fact]
    public void WithName_OverridePropertyName_code_severity_state_and_attempted_value()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Email).NotEmpty().WithName("E-mail").WithErrorCode("EmailRequired").WithSeverity(FV.Severity.Warning).WithState(x => x.Age);
        fv.RuleFor(x => x.Name).NotEmpty().OverridePropertyName("fullName");
        fv.RuleFor(x => x.Age).GreaterThanOrEqualTo(0).WithSeverity(FV.Severity.Info);

        var dt = new Validator<Person>()
            .For(x => x.Email, e => e.IfNullOrWhiteSpace(), rule => rule.WithName("E-mail").WithErrorCode("EmailRequired").WithSeverity(DotThrow.Severity.Warning).WithState(x => x.Age))
            .For(x => x.Name, n => n.IfNullOrWhiteSpace(), rule => rule.OverridePropertyName("fullName"))
            .For(x => x.Age, a => a.IfNegative(), rule => rule.WithSeverity(DotThrow.Severity.Info));

        var person = new Person { Email = "", Name = "", Age = -3 };
        var expected = fv.Validate(person);
        var actual = dt.Validate(person);

        Same(expected, actual);
        for (var i = 0; i < expected.Errors.Count; i++)
        {
            Assert.Equal(expected.Errors[i].Severity.ToString(), actual.Failures[i].Severity.ToString());
            Assert.Equal(expected.Errors[i].CustomState, actual.Failures[i].CustomState);
            Assert.Equal(expected.Errors[i].AttemptedValue, actual.Failures[i].AttemptedValue);
        }

        Assert.Equal("EmailRequired", actual.Failures[0].ErrorCode);
        Assert.Contains("E-mail", expected.Errors[0].ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("E-mail", actual.Failures[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule_cascade_continue_and_stop_and_class_cascade_stop()
    {
        var fv = new InlineValidator<Person> { ClassLevelCascadeMode = FV.CascadeMode.Continue };
        fv.RuleFor(x => x.Name).Cascade(FV.CascadeMode.Continue).MinimumLength(3).Matches("^A");
        fv.RuleFor(x => x.Email).Cascade(FV.CascadeMode.Stop).MinimumLength(3).Matches("@");
        var dt = new Validator<Person>()
            .For(x => x.Name, n => n.IfShorterThan(3).IfNotMatches("^A"), rule => rule.Cascade(DotThrow.CascadeMode.Continue))
            .For(x => x.Email, e => e.IfShorterThan(3).IfNotMatches("@"), rule => rule.Cascade(DotThrow.CascadeMode.Stop));

        var person = new Person { Name = "x", Email = "y" };
        Same(fv.Validate(person), dt.Validate(person));

        fv.ClassLevelCascadeMode = FV.CascadeMode.Stop;
        dt.ClassCascade = DotThrow.CascadeMode.Stop;
        Same(fv.Validate(person), dt.Validate(person));
    }

    [Fact]
    public void Rule_level_conditions_and_When_Otherwise_blocks()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.VatNumber).NotEmpty().When(x => x.IsCompany);
        fv.RuleFor(x => x.Name).NotEmpty().Unless(x => x.Age > 100);
        fv.When(x => x.IsCompany, () => fv.RuleFor(x => x.Email).NotEmpty())
            .Otherwise(() => fv.RuleFor(x => x.Age).GreaterThan(0));

        var dt = new Validator<Person>()
            .For(x => x.VatNumber, v => v.IfNullOrWhiteSpace(), rule => rule.When(x => x.IsCompany))
            .For(x => x.Name, n => n.IfNullOrWhiteSpace(), rule => rule.Unless(x => x.Age > 100))
            .When(x => x.IsCompany, v => v.For(x => x.Email, e => e.IfNullOrWhiteSpace()), otherwise: v => v.For(x => x.Age, a => a.IfNegativeOrZero()));

        foreach (var person in new[] { new Person(), new Person { IsCompany = true }, new Person { Age = 101 }, new Person { IsCompany = true, Age = 101, Name = "Ada" } })
        {
            Same(fv.Validate(person), dt.Validate(person));
        }
    }

    [Fact]
    public void DependentRules_run_only_when_the_parent_passes()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Name).NotEmpty().DependentRules(() =>
        {
            fv.RuleFor(x => x.Email).NotEmpty();
            fv.RuleFor(x => x.Age).GreaterThan(0);
        });
        var dt = new Validator<Person>().For(x => x.Name, n => n.IfNullOrWhiteSpace(), rule => rule.DependentRules(v => v
            .For(x => x.Email, e => e.IfNullOrWhiteSpace())
            .For(x => x.Age, a => a.IfNegativeOrZero())));

        Same(fv.Validate(new Person()), dt.Validate(new Person()));
        Same(fv.Validate(new Person { Name = "Ada" }), dt.Validate(new Person { Name = "Ada" }));
    }

    private static (InlineValidator<Person> Fv, Validator<Person> Dt) Groups()
    {
        var fvAddress = new InlineValidator<Address>();
        fvAddress.RuleFor(x => x.City).NotEmpty();
        fvAddress.RuleSet("shipping", () => fvAddress.RuleFor(x => x.Street).NotEmpty());
        var fvShared = new InlineValidator<Person>();
        fvShared.RuleFor(x => x.VatNumber).NotEmpty();
        fvShared.RuleSet("contact", () => fvShared.RuleFor(x => x.Age).GreaterThan(0));

        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Name).NotEmpty();
        fv.RuleSet("contact, admin", () => fv.RuleFor(x => x.Email).NotEmpty());
        fv.RuleSet("shipping", () =>
        {
            fv.RuleFor(x => x.Address).SetValidator(fvAddress);
            fv.RuleFor(x => x.Address).ChildRules(a => a.RuleFor(y => y.PostalCode).NotEmpty());
        });
        fv.Include(fvShared);

        var dtAddress = new Validator<Address>()
            .For(x => x.City, c => c.IfNullOrWhiteSpace())
            .Group("shipping", v => v.For(x => x.Street, s => s.IfNullOrWhiteSpace()));
        var dtShared = new Validator<Person>()
            .For(x => x.VatNumber, v => v.IfNullOrWhiteSpace())
            .Group("contact", v => v.For(x => x.Age, a => a.IfNegativeOrZero()));
        var dt = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrWhiteSpace())
            .Group(["contact", "admin"], v => v.For(x => x.Email, e => e.IfNullOrWhiteSpace()))
            .Group("shipping", v => v
                .For(x => x.Address, dtAddress)
                .For(x => x.Address, a => a.For(y => y.PostalCode, p => p.IfNullOrWhiteSpace())))
            .Include(dtShared);

        return (fv, dt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("contact")]
    [InlineData("SHIPPING")]
    [InlineData("admin,default")]
    [InlineData("*")]
    [InlineData("missing")]
    public void Groups_match_rule_sets_including_children_and_includes(string list)
    {
        var groups = list.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var (fv, dt) = Groups();
        var person = new Person { Address = new Address() };

        var expected = fv.Validate(person, o => o.IncludeRuleSets(groups));
        var actual = dt.Validate(person, groups);

        Same(expected, actual);
        Assert.Equal(expected.RuleSetsExecuted.Order(StringComparer.OrdinalIgnoreCase), actual.GroupsExecuted.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Default_run_reports_the_default_group()
    {
        var (fv, dt) = Groups();

        Assert.Equal(fv.Validate(new Person()).RuleSetsExecuted, dt.Validate(new Person()).GroupsExecuted);
    }

    [Fact]
    public void Child_rule_set_override_matches_WithChildGroups()
    {
        var fvAddress = new InlineValidator<Address>();
        fvAddress.RuleFor(x => x.City).NotEmpty();
        fvAddress.RuleSet("shipping", () => fvAddress.RuleFor(x => x.Street).NotEmpty());
        var fv = new InlineValidator<Person>();
        fv.RuleSet("shipping", () => fv.RuleFor(x => x.Address).SetValidator(fvAddress, "default", "shipping"));

        var dtAddress = new Validator<Address>()
            .For(x => x.City, c => c.IfNullOrWhiteSpace())
            .For(x => x.Street, s => s.IfNullOrWhiteSpace(), rule => rule.InGroup("shipping"));
        var dt = new Validator<Person>().For(x => x.Address, dtAddress, rule => rule.InGroup("shipping").WithChildGroups("default", "shipping"));

        var person = new Person { Address = new Address() };
        Same(fv.Validate(person, o => o.IncludeRuleSets("shipping")), dt.Validate(person, "shipping"));
    }

    [Theory]
    [InlineData("Number")]
    [InlineData("Customer")]
    [InlineData("Customer.Address")]
    [InlineData("Customer.Address.City")]
    [InlineData("Lines")]
    [InlineData("Lines[].Quantity")]
    [InlineData("Lines[3].Quantity")]
    [InlineData("Total")]
    public void Property_filter_matches_IncludeProperties(string property)
    {
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x.Number).NotEmpty();
        fv.RuleFor(x => x.Total).GreaterThan(0);
        fv.RuleForEach(x => x.Lines).SetValidator(new FvLine());
        fv.RuleFor(x => x.Customer).SetValidator(new FvCustomer());
        fv.RuleFor(x => x).Must(x => x.Total > 1000);

        var dt = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrWhiteSpace())
            .For(x => x.Total, t => t.IfNegativeOrZero())
            .ForEach(x => x.Lines, new LineValidator())
            .For(x => x.Customer, DtCustomer())
            .Must(x => x.Total > 1000);

        Same(fv.Validate(BadOrder(), o => o.IncludeProperties(property)), dt.Validate(BadOrder(), new ValidationOptions { Properties = [property] }));
    }

    [Fact]
    public void Polymorphic_child_validators_match_SetInheritanceValidator()
    {
        var fvCard = new InlineValidator<CardPayment>();
        fvCard.RuleFor(x => x.CardNumber).NotEmpty();
        fvCard.RuleFor(x => x.Amount).GreaterThan(0);
        var fvBank = new InlineValidator<BankPayment>();
        fvBank.RuleFor(x => x.Iban).NotEmpty();
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x.Payment).SetInheritanceValidator(v => v.Add(fvCard).Add(fvBank));

        var dt = new Validator<Order>().For(x => x.Payment, new PolymorphicValidator<Payment>()
            .Add(new CardPaymentValidator())
            .Add(new Validator<BankPayment>().For(x => x.Iban, i => i.IfNullOrWhiteSpace())));

        foreach (Payment payment in new Payment[] { new CardPayment(), new BankPayment(), new CashPayment() })
        {
            var order = new Order { Payment = payment };
            Same(fv.Validate(order), dt.Validate(order));
        }
    }

    [Fact]
    public void Custom_failures_and_PreValidate_get_the_child_path()
    {
        var fvCustomer = new InlineValidator<Customer>();
        fvCustomer.RuleFor(x => x).Custom((c, context) =>
        {
            context.AddFailure("Name", "Name is odd.");
            context.AddFailure("Whole customer is odd.");
        });
        var fv = new InlineValidator<Order>();
        fv.RuleFor(x => x.Customer).SetValidator(fvCustomer);

        var dt = new Validator<Order>().For(x => x.Customer, new Validator<Customer>().Custom((c, context) =>
        {
            context.AddFailure("Name", "Name is odd.");
            context.AddFailure("Whole customer is odd.");
        }));

        var order = new Order { Customer = new Customer() };
        Same(fv.Validate(order), dt.Validate(order));
    }

    [Fact]
    public void Root_context_data_reaches_child_validators()
    {
        var fvAddress = new InlineValidator<Address>();
        fvAddress.RuleFor(x => x.City).Must((_, _, context) => !context.RootContextData.ContainsKey("strict"));
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Address).SetValidator(fvAddress);

        var dt = new Validator<Person>().For(x => x.Address, new Validator<Address>().Custom((_, context) =>
        {
            if (context.Items.ContainsKey("strict"))
            {
                context.AddFailure("City", "Strict.");
            }
        }));

        var person = new Person { Address = new Address() };
        var fvContext = new FV.ValidationContext<Person>(person);
        fvContext.RootContextData["strict"] = true;
        var options = new ValidationOptions();
        options.Items["strict"] = true;

        Same(fv.Validate(fvContext), dt.Validate(person, options));
    }

    [Fact]
    public void ValidateAndThrow_and_ToDictionary_match()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Name).NotEmpty();
        fv.RuleForEach(x => x.Tags).NotEmpty();
        var dt = new Validator<Person>().For(x => x.Name, n => n.IfNullOrWhiteSpace()).ForEach(x => x.Tags, t => t.IfNullOrWhiteSpace());
        var person = new Person { Tags = ["", "a", ""] };

        var expected = Assert.Throws<FV.ValidationException>(() => fv.ValidateAndThrow(person));
        var actual = Assert.Throws<ValidationFailedException>(() => dt.ValidateAndThrow(person));

        Assert.Equal(expected.Errors.Select(e => e.PropertyName), actual.Failures.Select(f => f.Name));
        Assert.Equal(fv.Validate(person).ToDictionary().Keys.Order(), dt.Validate(person).ToDictionary().Keys.Order());
        Assert.Equal(fv.Validate(person).ToDictionary().Values.Select(v => v.Length), dt.Validate(person).ToDictionary().Values.Select(v => v.Length));
    }

    [Fact]
    public async Task Async_rules_match_and_sync_runs_of_async_rules_throw_InvalidOperationException()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Email).MustAsync(async (email, _) => { await Task.Yield(); return !string.IsNullOrEmpty(email); });
        fv.RuleFor(x => x.Name).NotEmpty();
        var dt = new Validator<Person>()
            .MustAsync(x => x.Email, async (email, _) => { await Task.Yield(); return !string.IsNullOrEmpty(email); })
            .For(x => x.Name, n => n.IfNullOrWhiteSpace());

        Same(await fv.ValidateAsync(new Person(), Token), await dt.ValidateAsync(new Person(), Token));
        Assert.IsAssignableFrom<InvalidOperationException>(Assert.ThrowsAny<Exception>(() => fv.Validate(new Person())));
        Assert.IsAssignableFrom<InvalidOperationException>(Assert.ThrowsAny<Exception>(() => dt.Validate(new Person())));
    }

    // Intended difference (decision D6): content checks fail null. FluentValidation's
    // MaximumLength passes null; DotThrow's IfLongerThan reports Null unless AllowNull() is used.
    [Fact]
    public void Difference_null_fails_content_checks_unless_AllowNull()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Email).MaximumLength(5);
        var strict = new Validator<Person>().For(x => x.Email, e => e.IfLongerThan(5));
        var optional = new Validator<Person>().For(x => x.Email, e => e.AllowNull().IfLongerThan(5));

        Assert.True(fv.Validate(new Person()).IsValid);
        Assert.Equal([ErrorCodes.Null], Names.Codes(strict.Validate(new Person())));
        Same(fv.Validate(new Person()), optional.Validate(new Person()));
    }

    // Intended difference: the default rule cascade is Stop (one failure per rule, decision D5);
    // FluentValidation's default is Continue. RuleCascade = Continue gives the same result.
    [Fact]
    public void Difference_default_rule_cascade_is_stop()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleFor(x => x.Name).NotEmpty().MinimumLength(3);
        var dt = new Validator<Person>().For(x => x.Name, n => n.IfNullOrWhiteSpace().IfShorterThan(3));

        var person = new Person { Name = "" };
        Assert.Equal(2, fv.Validate(person).Errors.Count);
        Assert.Single(dt.Validate(person).Failures);

        dt.RuleCascade = DotThrow.CascadeMode.Continue;
        Same(fv.Validate(person), dt.Validate(person));
    }

    // Intended difference: Stop on a collection rule stops the chain of each item in DotThrow;
    // FluentValidation stops the whole collection after the first failed item.
    [Fact]
    public void Difference_stop_on_a_collection_applies_per_item()
    {
        var fv = new InlineValidator<Person>();
        fv.RuleForEach(x => x.Tags).Cascade(FV.CascadeMode.Stop).NotEmpty();
        var dt = new Validator<Person>().ForEach(x => x.Tags, t => t.IfNullOrWhiteSpace(), rule => rule.Cascade(DotThrow.CascadeMode.Stop));

        var person = new Person { Tags = ["", ""] };

        Assert.Single(fv.Validate(person).Errors);
        Assert.Equal(["Tags[0]", "Tags[1]"], Names.Of(dt.Validate(person)));
    }

    // Intended difference: messages name the property by its path ('PostalCode'); FluentValidation
    // splits Pascal case ('Postal Code'). ValidatorDefaults.DisplayNameResolver can change it.
    [Fact]
    public void Difference_message_names_are_paths()
    {
        var fv = new InlineValidator<Address>();
        fv.RuleFor(x => x.PostalCode).NotEmpty();
        var dt = new Validator<Address>().For(x => x.PostalCode, p => p.IfNullOrWhiteSpace());

        Assert.Contains("'Postal Code'", fv.Validate(new Address()).Errors[0].ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("'PostalCode'", dt.Validate(new Address()).Failures[0].Message, StringComparison.Ordinal);
    }
}
#endif
