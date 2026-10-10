using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Internal;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// Overloads and members that the scenario tests do not reach: rule option overloads, the
// interface short forms, contexts, descriptors, exceptions and message details.
public sealed class ApiSurfaceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Severity_from_the_object_and_context_conditions_on_rules()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNull(), rule => rule.WithSeverity(x => x.IsCompany ? Severity.Error : Severity.Warning))
            .For(x => x.Email, e => e.IfNull(), rule => rule.Unless((x, context) => context.Items.ContainsKey("skip")));

        var person = validator.Validate(new Person());
        var company = validator.Validate(new Person { IsCompany = true }, o => o.Items["skip"] = true);

        Assert.Equal([Severity.Warning, Severity.Error], person.Failures.Select(f => f.Severity));
        Assert.Equal(Severity.Error, Assert.Single(company.Failures).Severity);
    }

    [Fact]
    public void Dependent_rules_can_be_added_twice_or_be_empty()
    {
        var validator = new Validator<Person>().For(
            x => x.Name,
            n => n.IfNull(),
            rule => rule
                .DependentRules(_ => { })
                .DependentRules(v => v.For(x => x.Name, n => n.IfShorterThan(3)))
                .DependentRules(v => v.For(x => x.Email, e => e.IfNull())));

        Assert.Equal(["Name"], Names.Of(validator.Validate(new Person())));
        Assert.Equal(["Name", "Email"], Names.Of(validator.Validate(new Person { Name = "A" })));
    }

    [Fact]
    public void Include_cycle_through_dependent_rules_is_found()
    {
        var first = new Validator<Person>();
        var second = new Validator<Person>().For(x => x.Name, n => n.IfNull(), rule => rule.DependentRules(v => v.Include(first)));

        Assert.Throws<ArgumentException>(() => first.Include(second));
    }

    [Fact]
    public async Task Interface_short_forms_pass_the_options_on()
    {
        IValidator<Person> validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNull())
            .For(x => x.Email, e => e.IfNull(), rule => rule.InGroup("contact"));

        var configured = await validator.ValidateAsync(new Person(), o => o.IncludeGroups("contact"), Token);
        var group = await validator.ValidateAsync(new Person(), "contact", Token);
        var nullGroup = await validator.ValidateAsync(new Person(), (string)null!, Token);
        var nullGroups = await validator.ValidateAsync(new Person(), (IEnumerable<string>?)null, Token);

        Assert.Equal(["Email"], Names.Of(configured));
        Assert.Equal(["Email"], Names.Of(group));
        Assert.Equal(["Name"], Names.Of(nullGroup));
        Assert.Equal(["Name"], Names.Of(nullGroups));
        await Assert.ThrowsAsync<ValidationFailedException>(() => validator.ValidateAndThrowAsync(new Person(), groups: null, Token));
    }

    [Fact]
    public async Task Validator_group_overloads_take_null_and_any_sequence()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNull())
            .For(x => x.Email, e => e.IfNull(), rule => rule.InGroup("contact"));

        var nullGroup = await validator.ValidateAsync(new Person(), (string)null!, Token);
        var list = await validator.ValidateAsync(new Person(), new List<string> { "contact" }, Token);
        var set = await validator.ValidateAsync(new Person(), new HashSet<string> { "contact", ValidationGroups.Default }, Token);

        Assert.Equal(["Name"], Names.Of(nullGroup));
        Assert.Equal(["Email"], Names.Of(list));
        Assert.Equal(["Name", "Email"], Names.Of(set));
    }

    [Fact]
    public async Task Non_generic_validator_members()
    {
        IValidator validator = new Validator<Person>().For(x => x.Name, n => n.IfNull());

        var report = await validator.ValidateAsync(new Person(), null, Token);

        Assert.Equal(["Name"], Names.Of(report));
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, null));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateAsync(new Address(), null, Token));
    }

    [Fact]
    public void Context_check_names_items_groups_and_arguments()
    {
        string? seenName = null;
        IReadOnlyList<string>? seenGroups = null;
        var validator = new Validator<Person>().Custom(
            (x, context) =>
            {
                seenName = context.Check(x.Age).IfNegative().Name;
                seenGroups = context.Groups;
                context.Check(x.Name, name: null).IfNull();
                context.AppendArgument("Limit", 10);
                context.AppendArgument("Unit", "years");
                context.AddFailure("Age", "'{PropertyName}' must be below {Limit} {Unit}.");
            },
            rule => rule.InGroup("audit"));

        var report = validator.Validate(new Person { Age = -1 }, "audit");

        Assert.Equal("x.Age", seenName);
        Assert.Equal(["audit"], seenGroups);
        Assert.Equal(["x.Age", "value", "Age"], Names.Of(report));
        // Like FluentValidation, AddFailure fills appended arguments but leaves {PropertyName} as written.
        Assert.Equal("'{PropertyName}' must be below 10 years.", report.Failures[2].Message);
    }

    [Fact]
    public void Property_filters_ignore_empty_lists_and_repeated_names()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNull())
            .For(x => x.Email, e => e.IfNull());

        var none = validator.Validate(new Person(), new ValidationOptions { Properties = [] });
        var repeated = validator.Validate(new Person(), new ValidationOptions { Properties = ["Email", "Email", ""] });

        Assert.Equal(["Name", "Email"], Names.Of(none));
        Assert.Equal(["Email"], Names.Of(repeated));
    }

    [Fact]
    public void Rule_messages_without_placeholders_and_with_odd_values()
    {
        var validator = new Validator<Box>()
            .For(x => x.Name, n => n.IfNotNull(), rule => rule.WithMessage("Plain text."))
            .For(x => x.Missing, e => e.IfNull(), rule => rule.WithMessage("Got '{PropertyValue}'."))
            .For(x => x.Content, a => a.IfNotNull(), rule => rule.WithMessage("Got '{PropertyValue}'."));

        var report = validator.Validate(new Box { Name = "Ada", Content = new Nameless() });

        Assert.Equal(["Plain text.", "Got ''.", "Got ''."], Names.Messages(report));
    }

    [Fact]
    public void Async_rule_exception_names_nested_generic_validator_types()
    {
        var validator = new Validator<Dictionary<string, List<int>>>().MustAsync((_, _) => Task.FromResult(true));

        var exception = Assert.Throws<AsyncRuleInvokedSynchronouslyException>(() => validator.Validate([]));

        Assert.Equal(typeof(Validator<Dictionary<string, List<int>>>), exception.ValidatorType);
        Assert.Contains("Validator<Dictionary<String, List<Int32>>>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AsyncRuleInvokedSynchronouslyException_constructors()
    {
        var plain = new AsyncRuleInvokedSynchronouslyException();
        var message = new AsyncRuleInvokedSynchronouslyException("Run it async.");
        var inner = new AsyncRuleInvokedSynchronouslyException("Run it async.", new FormatException());
        var unknown = new AsyncRuleInvokedSynchronouslyException((Type)null!);

        Assert.Contains("ValidateAsync", plain.Message, StringComparison.Ordinal);
        Assert.Equal("Run it async.", message.Message);
        Assert.IsType<FormatException>(inner.InnerException);
        Assert.Null(unknown.ValidatorType);
        Assert.Contains("(unknown)", unknown.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<InvalidOperationException>(plain);
    }

    [Fact]
    public void ValidationFailedException_with_a_message_and_a_null_report()
    {
        var exception = new ValidationFailedException("Order is invalid.", (ValidationReport)null!);

        Assert.Equal("Order is invalid.", exception.Message);
        Assert.Empty(exception.Failures);
    }

    [Fact]
    public void Adding_an_empty_report_changes_nothing()
    {
        var report = new ValidationReport().Add("Name", "Bad.");

        report.AddRange(new ValidationReport(), "Prefix");

        Assert.Equal(["Name"], Names.Of(report));
    }

    [Fact]
    public void Descriptor_members_and_rule_text()
    {
        var descriptor = new Validator<Person>()
            .For(x => x.Name, n => n.IfNull())
            .Must(x => x.Age > 0, "Age.")
            .Describe();

        Assert.Equal(typeof(Validator<Person>), descriptor.ValidatorType);
        Assert.Equal(typeof(Person), descriptor.ModelType);
        Assert.Equal(["Property Name", "Must"], descriptor.Rules.Select(r => r.ToString()));
    }

    [Fact]
    public async Task Task_helpers_turn_exceptions_into_tasks()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        var faulted = TaskHelpers.FromException(new FormatException());
        var canceledTask = TaskHelpers.FromException<int>(new OperationCanceledException(canceled.Token));

        Assert.True(faulted.IsFaulted);
        Assert.True(canceledTask.IsCanceled);
        await Assert.ThrowsAsync<FormatException>(() => faulted);
        Assert.Throws<OperationCanceledException>(() => TaskHelpers.ThrowIfCanceled(canceled.Token));
        TaskHelpers.ThrowIfCanceled(Token);
    }

    public sealed class Box
    {
        public string? Name { get; set; }

        public string? Missing { get; set; }

        public object? Content { get; set; }
    }

    // An object whose text is null, for message formatting.
    private sealed class Nameless
    {
        public override string? ToString() => null;
    }
}
