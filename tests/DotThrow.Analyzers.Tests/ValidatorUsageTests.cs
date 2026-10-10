using System.Threading.Tasks;
using DotThrow.Analyzers.CodeFixes;
using DotThrow.Analyzers.Tests.Infrastructure;
using Xunit;

namespace DotThrow.Analyzers.Tests;

public sealed class ValidatorUsageTests
{
    // Validators shared by the tests.
    private const string Validators = """

        public sealed record Order(string Email, string? Coupon, Address Address, string[] Tags);

        public sealed record Address(string City);

        public sealed record Customer(Address Address);

        public sealed class OrderValidator : Validator<Order>
        {
            public OrderValidator()
            {
                For(x => x.Email, email => email.IfNullOrWhiteSpace());
                MustAsync(x => x.Email, (email, token) => Task.FromResult(email.Length > 0), "Taken.");
            }
        }

        public sealed class SyncValidator : Validator<Order>
        {
            public SyncValidator() => For(x => x.Email, email => email.IfNullOrWhiteSpace());
        }

        public class BaseAsyncValidator : Validator<Order>
        {
            public BaseAsyncValidator() => CustomAsync((x, context, token) => Task.CompletedTask);
        }

        public sealed class DerivedValidator : BaseAsyncValidator
        {
            public DerivedValidator() => For(x => x.Email, email => email.IfNull());
        }

        public sealed class GroupedValidator : Validator<Order>
        {
            public GroupedValidator()
            {
                For(x => x.Email, email => email.IfNull());
                Group("checkout", checkout => checkout.MustAsync((x, token) => Task.FromResult(true)));
                MustAsync((x, token) => Task.FromResult(true), configure: rule => rule.InGroup("lookup"));
            }
        }

        public sealed class AddressValidator : Validator<Address>
        {
            public AddressValidator() => MustAsync(x => x.City, (city, token) => Task.FromResult(true));
        }

        public sealed class CustomerValidator : Validator<Customer>
        {
            public CustomerValidator() => For(x => x.Address, new AddressValidator());
        }

        public sealed class IncludingValidator : Validator<Order>
        {
            public IncludingValidator() => Include(new OrderValidator());
        }
        """;

    [Fact]
    public Task DT0004_reports_sync_runs_of_validators_with_async_rules() =>
        Verify.AnalyzerAsync<ValidatorUsageAnalyzer>(
            """
            using System.Threading.Tasks;
            using DotThrow;
            using DotThrow.Testing;

            class Uses
            {
                private static readonly Validator<Order> Inline = new Validator<Order>()
                    .For(x => x.Email, email => email.IfNull())
                    .CustomAsync((x, context, token) => Task.CompletedTask);

                private static Validator<Order> Property { get; } = new Validator<Order>().MustAsync((x, token) => Task.FromResult(true));

                void M(Order order, Customer customer, OrderValidator injected)
                {
                    var validator = new OrderValidator();
                    _ = validator.{|DT0004:Validate|}(order);
                    validator.{|DT0004:ValidateAndThrow|}(order);
                    _ = validator.{|DT0004:TestValidate|}(order);
                    _ = injected.{|DT0004:Validate|}(order);
                    _ = new OrderValidator().{|DT0004:Validate|}(order);
                    _ = new DerivedValidator().{|DT0004:Validate|}(order);
                    _ = Inline.{|DT0004:Validate|}(order);
                    _ = Property.{|DT0004:Validate|}(order);
                    _ = Create().{|DT0004:Validate|}(order);
                    _ = new CustomerValidator().{|DT0004:Validate|}(customer);
                    _ = new IncludingValidator().{|DT0004:Validate|}(order);
                    _ = ((IValidator<Order>)validator).{|DT0004:Validate|}(order);

                    var local = new Validator<Order>();
                    local.For(x => x.Coupon, coupon => coupon.AllowNull().IfLongerThan(10), rule => rule.WhenAsync((x, token) => Task.FromResult(true)));
                    _ = local.{|DT0004:Validate|}(order);

                    var block = new Validator<Order>()
                        .WhenAsync((x, token) => Task.FromResult(true), rules => rules.For(x => x.Email, email => email.IfNull()));
                    _ = block.{|DT0004:Validate|}(order);

                    var each = new Validator<Order>()
                        .ForEach(x => x.Tags, tag => tag.IfNull(), rule => rule.WhereAsync((tag, token) => Task.FromResult(true)));
                    _ = each.{|DT0004:Validate|}(order);

                    var dependent = new Validator<Order>()
                        .For(x => x.Email, email => email.IfNull(), rule => rule
                            .DependentRules(more => more.MustAsync(x => x.Email, (email, token) => Task.FromResult(true))));
                    _ = dependent.{|DT0004:Validate|}(order);
                }

                static Validator<Order> Create() => new Validator<Order>().MustAsync((x, token) => Task.FromResult(true));
            }
            """ + Validators);

    [Fact]
    public Task DT0004_follows_groups() =>
        Verify.AnalyzerAsync<ValidatorUsageAnalyzer>(
            """
            using System.Threading.Tasks;
            using DotThrow;

            class Uses
            {
                void M(Order order, string group)
                {
                    var grouped = new GroupedValidator();
                    _ = grouped.Validate(order);
                    _ = grouped.Validate(order, "other");
                    _ = grouped.Validate(order, ValidationGroups.Default);
                    _ = grouped.{|DT0004:Validate|}(order, "checkout");
                    _ = grouped.{|DT0004:Validate|}(order, "LOOKUP");
                    _ = grouped.{|DT0004:Validate|}(order, ValidationGroups.All);
                    _ = grouped.{|DT0004:Validate|}(order, new[] { "a", "checkout" });

                    // Not constants: the analyzer cannot know.
                    _ = grouped.Validate(order, group);
                    _ = grouped.Validate(order, options => options.IncludeGroups("checkout"));
                    _ = grouped.Validate(order, new ValidationOptions { Groups = new[] { "checkout" } });

                    // An include without groups runs in every run.
                    _ = new IncludingValidator().{|DT0004:Validate|}(order, ValidationGroups.Default);
                }
            }
            """ + Validators);

    [Fact]
    public Task DT0004_ignores_validators_without_async_rules_or_unknown_ones() =>
        Verify.AnalyzerAsync<ValidatorUsageAnalyzer>(
            """
            using System.Threading.Tasks;
            using DotThrow;

            class Uses
            {
                private static readonly Validator<Order> SyncInline = new Validator<Order>().For(x => x.Email, email => email.IfNull());

                async Task M(Order order, IValidator<Order> unknown, Validator<Order> parameter, OrderValidator validator)
                {
                    _ = new SyncValidator().Validate(order);
                    _ = SyncInline.Validate(order);
                    _ = unknown.Validate(order);
                    _ = parameter.Validate(order);
                    _ = await validator.ValidateAsync(order);
                    await validator.ValidateAndThrowAsync(order);

                    // A provider picks the validator at run time.
                    _ = new Validator<Order>().For<Address, AddressValidator>(x => x.Address, x => new AddressValidator()).Validate(order);
                }
            }
            """ + Validators);

    [Fact]
    public Task DT0004_reports_the_async_rule() =>
        Verify.AnalyzerAsync<ValidatorUsageAnalyzer>(
            """
            using System.Threading.Tasks;
            using DotThrow;

            class Uses
            {
                void M(Order order) => _ = new OrderValidator().Validate(order);
            }
            """ + Validators,
            Verify.Diagnostic<ValidatorUsageAnalyzer>("DT0004").WithSpan(6, 53, 6, 61).WithArguments("MustAsync", "Validate", "ValidateAsync"));

    [Fact]
    public Task DT0004_fix_awaits_the_async_method() =>
        Verify.CodeFixAsync<ValidatorUsageAnalyzer, AsyncValidateCodeFix>(
            """
            using System.Threading.Tasks;
            using DotThrow;
            using DotThrow.Testing;

            class Uses
            {
                async Task M(Order order)
                {
                    var validator = new OrderValidator();
                    var report = validator.{|DT0004:Validate|}(order);
                    if (!validator.{|DT0004:Validate|}(order).IsValid)
                    {
                        return;
                    }

                    validator.{|DT0004:ValidateAndThrow|}(order);
                    validator.{|DT0004:ValidateAndThrow|}(order, "default");
                    _ = validator.{|DT0004:Validate|}(order, "default", "other");
                    _ = validator.{|DT0004:TestValidate|}(order);
                }
            }
            """ + Validators,
            """
            using System.Threading.Tasks;
            using DotThrow;
            using DotThrow.Testing;

            class Uses
            {
                async Task M(Order order)
                {
                    var validator = new OrderValidator();
                    var report = await validator.ValidateAsync(order);
                    if (!(await validator.ValidateAsync(order)).IsValid)
                    {
                        return;
                    }

                    await validator.ValidateAndThrowAsync(order);
                    await validator.ValidateAndThrowAsync(order, new[] { "default" });
                    _ = await validator.ValidateAsync(order, new[] { "default", "other" });
                    _ = await validator.TestValidateAsync(order);
                }
            }
            """ + Validators);

    [Fact]
    public Task DT0004_offers_no_fix_outside_async_code_or_for_the_static_form() =>
        Verify.CodeFixAsync<ValidatorUsageAnalyzer, AsyncValidateCodeFix>(
            """
            using System.Threading.Tasks;
            using DotThrow;

            class Uses
            {
                void M(Order order) => _ = new OrderValidator().{|DT0004:Validate|}(order);

                async Task N(Order order)
                {
                    _ = ValidatorExtensions.{|DT0004:Validate|}(new OrderValidator(), order);
                    await Task.Yield();
                }
            }
            """ + Validators,
            """
            using System.Threading.Tasks;
            using DotThrow;

            class Uses
            {
                void M(Order order) => _ = new OrderValidator().{|DT0004:Validate|}(order);

                async Task N(Order order)
                {
                    _ = ValidatorExtensions.{|DT0004:Validate|}(new OrderValidator(), order);
                    await Task.Yield();
                }
            }
            """ + Validators);

    [Fact]
    public Task DT0010_reports_unused_results() =>
        Verify.AnalyzerAsync<ValidatorUsageAnalyzer>(
            """
            using System.Threading.Tasks;
            using DotThrow;
            using DotThrow.Testing;

            class Uses
            {
                void M(Order order, SyncValidator validator, IValidator<Order> any)
                {
                    validator.{|DT0010:Validate|}(order);
                    validator.{|DT0010:Validate|}(order, "checkout");
                    any.{|DT0010:Validate|}(order);
                    validator.{|DT0010:TestValidate|}(order);
                    validator.{|DT0010:ValidateAsync|}(order);
                    validator.{|DT0010:ValidateAndThrowAsync|}(order);
                    any.{|DT0010:ValidateAsync|}(order);

                    // Used, thrown, or discarded on purpose.
                    validator.ValidateAndThrow(order);
                    var report = validator.Validate(order);
                    _ = validator.ValidateAsync(order);
                    validator.Validate(order, options => options.ThrowOnFailure());
                    validator.Validate(order, new ValidationOptions { ThrowOnFailures = true });
                }

                async Task N(Order order, SyncValidator validator)
                {
                    await validator.{|DT0010:ValidateAsync|}(order);
                    await validator.{|DT0010:ValidateAsync|}(order).ConfigureAwait(false);
                    await validator.{|DT0010:ValidateAsync|}(order, "checkout");
                    await validator.{|DT0010:TestValidateAsync|}(order);
                    await validator.ValidateAndThrowAsync(order);
                    var report = await validator.ValidateAsync(order);
                }
            }
            """ + Validators);

    [Fact]
    public Task DT0010_fix_calls_ValidateAndThrow() =>
        Verify.CodeFixAsync<ValidatorUsageAnalyzer, UnusedValidationResultCodeFix>(
            """
            using System.Threading.Tasks;
            using DotThrow;
            using DotThrow.Testing;

            class Uses
            {
                async Task M(Order order, SyncValidator validator)
                {
                    validator.{|DT0010:Validate|}(order);
                    validator.{|DT0010:Validate|}(order, "a", "b");
                    await validator.{|DT0010:ValidateAsync|}(order);
                    await validator.{|DT0010:ValidateAsync|}(order, "single");
                    validator.{|DT0010:TestValidate|}(order);
                }
            }
            """ + Validators,
            """
            using System.Threading.Tasks;
            using DotThrow;
            using DotThrow.Testing;

            class Uses
            {
                async Task M(Order order, SyncValidator validator)
                {
                    validator.ValidateAndThrow(order);
                    validator.ValidateAndThrow(order, "a", "b");
                    await validator.ValidateAndThrowAsync(order);
                    await validator.{|DT0010:ValidateAsync|}(order, "single");
                    validator.{|DT0010:TestValidate|}(order);
                }
            }
            """ + Validators);

    [Fact]
    public Task Top_level_statements() =>
        Verify.CodeFixAsync(() => new DotThrowCodeFixTest<ValidatorUsageAnalyzer, AsyncValidateCodeFix>
        {
            TestState =
            {
                OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication,
                Sources =
                {
                    """
                    using System.Threading.Tasks;
                    using DotThrow;

                    var validator = new OrderValidator();
                    var order = new Order("ada@example.com", null, new Address("London"), []);
                    var report = validator.{|DT0004:Validate|}(order);
                    validator.ValidateAsync(order);
                    await Task.Yield();
                    """ + Validators,
                },
            },
            FixedState =
            {
                OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication,
                Sources =
                {
                    """
                    using System.Threading.Tasks;
                    using DotThrow;

                    var validator = new OrderValidator();
                    var order = new Order("ada@example.com", null, new Address("London"), []);
                    var report = await validator.ValidateAsync(order);
                    validator.ValidateAsync(order);
                    await Task.Yield();
                    """ + Validators,
                },
            },
        });

    [Fact]
    public Task Unawaited_task_in_top_level_statements_without_await() =>
        new DotThrowAnalyzerTest<ValidatorUsageAnalyzer>
        {
            TestState =
            {
                OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication,
                Sources =
                {
                    """
                    using System.Threading.Tasks;
                    using DotThrow;

                    var validator = new SyncValidator();
                    validator.{|DT0010:ValidateAndThrowAsync|}(new Order("ada@example.com", null, new Address("London"), []));
                    """ + Validators,
                },
            },
        }.RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task DT0004_handles_child_groups_and_cycles() =>
        Verify.AnalyzerAsync<ValidatorUsageAnalyzer>(
            """
            using System.Threading.Tasks;
            using DotThrow;

            public sealed record Node(Node[] Children, string Name);

            public sealed class ChildGroupValidator : Validator<Node>
            {
                public ChildGroupValidator() => MustAsync((x, token) => Task.FromResult(true), configure: rule => rule.InGroup("deep"));
            }

            class Uses
            {
                // A validator that refers to itself must not make the analyzer loop.
                private static Validator<Node> Tree => new Validator<Node>().ForEach(x => x.Children, Tree);

                void M(Node node)
                {
                    _ = Tree.Validate(node);

                    var children = new Validator<Node>().ForEach(x => x.Children, new ChildGroupValidator());
                    _ = children.Validate(node);

                    var deep = new Validator<Node>().ForEach(x => x.Children, new ChildGroupValidator(), rule => rule.WithChildGroups("deep"));
                    _ = deep.{|DT0004:Validate|}(node);
                }
            }
            """);

    [Fact]
    public Task DT0010_reports_results_dropped_by_a_lambda() =>
        Verify.AnalyzerAsync<ValidatorUsageAnalyzer>(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using DotThrow;

            class Uses
            {
                void M(List<Order> orders, SyncValidator validator)
                {
                    orders.ForEach(order => validator.{|DT0010:Validate|}(order));
                    Func<Order, ValidationReport> run = order => validator.Validate(order);
                }
            }
            """ + Validators);
}
