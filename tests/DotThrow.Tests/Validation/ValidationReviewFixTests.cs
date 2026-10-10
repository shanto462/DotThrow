using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Scanning;
using DotThrow.Testing;
using DotThrow.Tests.Validation.Models;
using Xunit;
#if NET8_0_OR_GREATER
using System.Linq;
using FluentValidation;
#endif

namespace DotThrow.Tests.Validation;

// Findings of the blind review of the validation core, one test each.
[Collection(nameof(GlobalDefaults))]
public sealed class ValidationReviewFixTests : IDisposable
{
    public void Dispose() => ValidatorDefaults.Reset();

    private static TreeNode Chain(int length)
    {
        var first = new TreeNode { Name = "n" };
        var current = first;
        for (var i = 1; i < length; i++)
        {
            current.Parent = new TreeNode { Name = "n" };
            current = current.Parent;
        }

        return first;
    }

    [Fact]
    public void Very_deep_graph_on_a_small_stack_fails_cleanly_instead_of_crashing()
    {
        ValidatorDefaults.MaxDepth = 100_000;
        var node = new Validator<TreeNode>();
        node.For(x => x.Name, n => n.IfNullOrEmpty()).For(x => x.Parent, node);
        Exception? caught = null;
        var completed = false;
        var thread = new Thread(
            () =>
            {
                try
                {
                    node.Validate(Chain(20_000));
                    completed = true;
                }
                catch (InvalidOperationException exception)
                {
                    caught = exception;
                }
            },
            maxStackSize: 1024 * 1024);
        thread.Start();
        thread.Join();

        Assert.True(completed || caught is not null);
        if (caught is not null)
        {
            Assert.Contains("stack space", caught.Message, StringComparison.Ordinal);
        }
    }

    private sealed class Wrapper(IValidator<TreeNode> inner) : IValidator<TreeNode>
    {
        public bool CanValidate(Type type) => inner.CanValidate(type);

        public ValidationReport Validate(TreeNode value, ValidationOptions? options) => inner.Validate(value, options);

        public Task<ValidationReport> ValidateAsync(TreeNode value, ValidationOptions? options, CancellationToken cancellationToken = default) =>
            inner.ValidateAsync(value, options, cancellationToken);

        ValidationReport IValidator.Validate(object instance, ValidationOptions? options) => Validate((TreeNode)instance, options);

        Task<ValidationReport> IValidator.ValidateAsync(object instance, ValidationOptions? options, CancellationToken cancellationToken) =>
            ValidateAsync((TreeNode)instance, options, cancellationToken);
    }

    [Fact]
    public void Cycles_through_hand_written_and_untyped_polymorphic_validators_stop_at_max_depth()
    {
        var node = new Validator<TreeNode>();
        node.For(x => x.Parent, new Wrapper(node));
        var loop = new TreeNode { Name = "a" };
        loop.Parent = loop;

        Assert.Contains("deep", Assert.Throws<InvalidOperationException>(() => node.Validate(loop)).Message, StringComparison.Ordinal);

        var poly = new PolymorphicValidator<object>();
        var holder = new Validator<TreeNode>();
        holder.For(x => (object?)x.Parent, poly, rule => rule.OverridePropertyName("Parent"));
#pragma warning disable CA2263 // The test covers the untyped Add on purpose.
        poly.Add(typeof(TreeNode), holder);
#pragma warning restore CA2263

        Assert.Throws<InvalidOperationException>(() => holder.Validate(loop));
    }

    [Fact]
    public void Items_written_by_rules_stay_in_their_run()
    {
        var seenBefore = new List<bool>();
        var validator = new Validator<Person>().Custom((_, context) =>
        {
            seenBefore.Add(context.Items.ContainsKey("seen"));
            context.Items["seen"] = true;
        });
        var options = new ValidationOptions();

        validator.Validate(new Person(), options);
        validator.Validate(new Person(), options);

        Assert.Equal([false, false], seenBefore);
        Assert.False(options.Items.ContainsKey("seen"));
    }

    private sealed class LimitValidator : Validator<Person>
    {
        protected override bool PreValidate(ValidationContext<Person> context)
        {
            context.AppendArgument("Limit", 10);
            context.AddFailure("Name", "At most {Limit}.");
            return false;
        }
    }

    [Fact]
    public void AppendArgument_works_in_PreValidate()
    {
        var failure = Assert.Single(new LimitValidator().Validate(new Person()).Failures);

        Assert.Equal("At most 10.", failure.Message);
        Assert.Equal(10, failure.MessageArguments["Limit"]);
    }

    [Fact]
    public void Non_generic_Validate_of_null_for_a_value_type_throws()
    {
        IValidator validator = new Validator<int>().For(x => x, x => x.IfNegative());

        Assert.Equal("instance", Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, null)).ParamName);
    }

    [Fact]
    public async Task Test_helper_overloads_accept_null_options_and_a_token()
    {
        var validator = new Validator<Person>().For(x => x.Name, n => n.IfNull());

        validator.TestValidate(new Person(), null).ShouldHaveValidationErrorFor(x => x.Name);
        (await validator.TestValidateAsync(new Person(), TestContext.Current.CancellationToken)).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Dependent_rules_take_the_groups_of_their_parent_inside_a_group_block()
    {
        var validator = new Validator<Person>().Group("b", g => g.For(
            x => x.Name,
            n => n.IfNullOrEmpty(),
            rule => rule.InGroup("a").DependentRules(v => v.For(x => x.Email, e => e.IfNullOrEmpty()))));

        var rule = Assert.Single(validator.Describe().Rules);
        Assert.Equal(["a"], rule.Groups);
        Assert.Equal(["a"], Assert.Single(rule.DependentRules).Groups);
        Assert.Equal(["Email"], Names.Of(validator.Validate(new Person { Name = "Ada" }, "a")));
    }

    [Fact]
    public void Null_reference_message_names_the_rule_and_its_parts()
    {
        var validator = new Validator<Person>().For(x => x.Address, new Validator<Address>().Custom((a, context) => context.Items["length"] = a.City!.Length));

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(new Person { Address = new Address() }));

        Assert.Contains("while running the rule for 'x => x.Address'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scanner_skips_dynamic_assemblies()
    {
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DynamicForScan"), AssemblyBuilderAccess.Run);

        var results = new List<AssemblyScanResult>(DotThrow.Scanning.AssemblyScanner.FindValidatorsInAssemblies([dynamicAssembly, typeof(AddressValidator).Assembly]));

        Assert.Contains(results, r => r.ValidatorType == typeof(AddressValidator));
    }

    [Fact]
    public void Must_message_fills_placeholders_like_WithMessage()
    {
        var validator = new Validator<Person>().Must(x => x.Age, age => age > 0, "{PropertyName} must be positive, not {PropertyValue}.");

        Assert.Equal("Age must be positive, not -2.", Assert.Single(validator.Validate(new Person { Age = -2 }).Failures).Message);
    }

    [Fact]
    public void Child_groups_replace_the_whole_selection_inside_the_child()
    {
        var customer = new Validator<Customer>().For(x => x.Name, n => n.IfNullOrEmpty()).For(x => x.Address, new AddressValidator());
        var order = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrEmpty())
            .For(x => x.Customer, customer, rule => rule.WithChildGroups(ValidationGroups.Default));
        var value = new Order { Customer = new Customer { Address = new Address() } };

        // The property filter selects the Customer rule; inside it only the child groups decide.
        Assert.Equal(
            ["Customer.Name", "Customer.Address.Street", "Customer.Address.City", "Customer.Address.PostalCode"],
            Names.Of(order.Validate(value, new ValidationOptions { Properties = ["Customer.Name"] })));
        Assert.Equal(["Number"], Names.Of(order.Validate(value, new ValidationOptions { Properties = ["Number"] })));

        var strict = new Validator<BankPayment>()
            .For(x => x.Iban, i => i.IfShorterThan(10), rule => rule.InGroup("strict"))
            .For(x => x.Amount, a => a.IfNegativeOrZero());
        var payments = new PolymorphicValidator<Payment>().Add(strict, "strict");
        Assert.Equal(["Iban"], Names.Of(payments.Validate(new BankPayment { Iban = "DE" }, new ValidationOptions { Properties = ["Amount"] })));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Child_groups_match_FluentValidation_rule_sets_on_a_child()
    {
        var fvAddress = new InlineValidator<Address>();
        fvAddress.RuleFor(x => x.Street).NotEmpty();
        fvAddress.RuleFor(x => x.City).NotEmpty();
        fvAddress.RuleFor(x => x.PostalCode).NotEmpty();
        var fvCustomer = new InlineValidator<Customer>();
        fvCustomer.RuleFor(x => x.Name).NotEmpty();
        fvCustomer.RuleFor(x => x.Address!).SetValidator(fvAddress);
        var fvStrict = new InlineValidator<BankPayment>();
        fvStrict.RuleSet("strict", () => fvStrict.RuleFor(x => x.Iban).MinimumLength(10));
        fvStrict.RuleFor(x => x.Amount).GreaterThan(0);
        var fvOrder = new InlineValidator<Order>();
        fvOrder.RuleFor(x => x.Number).NotEmpty();
        fvOrder.RuleFor(x => x.Customer!).SetValidator(fvCustomer, "default");
        fvOrder.RuleFor(x => x.Payment).SetInheritanceValidator(v => v.Add(fvStrict, "strict"));

        var customer = new Validator<Customer>().For(x => x.Name, n => n.IfNullOrEmpty()).For(x => x.Address, new AddressValidator());
        var strict = new Validator<BankPayment>()
            .For(x => x.Iban, i => i.IfShorterThan(10), rule => rule.InGroup("strict"))
            .For(x => x.Amount, a => a.IfNegativeOrZero());
        var order = new Validator<Order>()
            .For(x => x.Number, n => n.IfNullOrEmpty())
            .For(x => x.Customer, customer, rule => rule.WithChildGroups(ValidationGroups.Default))
            .For(x => x.Payment, new PolymorphicValidator<Payment>().Add(strict, "strict"));

        var value = new Order { Customer = new Customer { Address = new Address() }, Payment = new BankPayment { Iban = "DE" } };
        string[][] filters = [["Customer.Name"], ["Payment.Iban", "Payment.Amount"], ["Number"], ["Customer.Name", "Payment.Amount"]];
        foreach (var properties in filters)
        {
            var expected = fvOrder.Validate(value, o => o.IncludeProperties(properties)).Errors.Select(e => e.PropertyName);
            Assert.Equal(expected, Names.Of(order.Validate(value, new ValidationOptions { Properties = properties })));
        }
    }
#endif
}
