using System;
using System.Threading;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// ValidationReport, ValidationFailure and the exception hooks.
public sealed class ReportAndExceptionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void ToString_with_a_separator_joins_messages()
    {
        var report = new ValidationReport().Add("A", "First.").Add("B", "Second.");

        Assert.Equal("First. | Second.", report.ToString(" | "));
        Assert.Equal(string.Empty, new ValidationReport().ToString(", "));
        Assert.StartsWith("Validation failed with 2 errors:", report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToDictionary_groups_messages_by_name()
    {
        var report = new ValidationReport().Add("A", "1.").Add("B", "2.").Add("A", "3.");

        var dictionary = report.ToDictionary();

        Assert.Equal(["1.", "3."], dictionary["A"]);
        Assert.Equal(["2."], dictionary["B"]);
    }

    [Fact]
    public void AddRange_of_the_same_report_doubles_it()
    {
        var report = new ValidationReport().Add("A", "1.");

        report.AddRange(report, "Prefix");

        Assert.Equal(["A", "Prefix.A"], Names.Of(report));
    }

    [Fact]
    public void AddRange_keeps_every_detail_and_joins_index_names()
    {
        var failure = new ValidationFailure("[0]", "m", "c", Severity.Warning, 5, "s");
        var report = new ValidationReport().AddRange(new ValidationReport().Add(failure), "Lines");

        var copy = Assert.Single(report.Failures);
        Assert.Equal("Lines[0]", copy.Name);
        Assert.Equal(Severity.Warning, copy.Severity);
        Assert.Equal(5, copy.AttemptedValue);
        Assert.Equal("s", copy.CustomState);
    }

    [Fact]
    public void Prefixed_copies_rebuild_their_message_arguments()
    {
        var inner = new Validator<Address>().Custom((_, context) =>
        {
            context.AppendArgument("Limit", 3);
            context.AddFailure("City", "At most {Limit}.");
        });
        var report = inner.Validate(new Address());
        _ = report.Failures[0].MessageArguments;

        var copy = new ValidationReport().AddRange(report, "Home").Failures[0];

        Assert.Equal("Home.City", copy.MessageArguments["PropertyPath"]);
        Assert.Equal(3, copy.MessageArguments["Limit"]);
        Assert.Equal("At most 3.", copy.Message);
    }

    [Fact]
    public void Failure_details_are_set_from_rules_and_by_init()
    {
        var failure = new ValidationFailure("Name", "Message.", "Code") { Severity = Severity.Info, AttemptedValue = "x", CustomState = 1 };

        Assert.Equal(Severity.Info, failure.Severity);
        Assert.Equal("Name: Message.", failure.ToString());
        Assert.Equal(Severity.Error, new ValidationFailure("a", "b", "c").Severity);
    }

    [Fact]
    public void Report_Check_with_a_selector_uses_the_property_name()
    {
        var form = new Person { Email = "nope" };
        var report = new ValidationReport();

        report.Check(form, f => f.Email).IfNotEmailAddress();
        report.Check(form.Name).IfNull();

        Assert.Equal(["Email", "form.Name"], Names.Of(report));
        Assert.Equal("nope", report.Failures[0].AttemptedValue);
    }

    [Fact]
    public void Standalone_report_failures_keep_the_attempted_value()
    {
        var report = new ValidationReport();
        report.Check(-5, "count").IfNegative();

        Assert.Equal(-5, Assert.Single(report.Failures).AttemptedValue);
        Assert.Empty(report.GroupsExecuted);
    }

    [Fact]
    public void ThrowOnFailures_uses_CreateValidationException()
    {
        var validator = new CustomExceptionValidator();

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(new Person(), new ValidationOptions { ThrowOnFailures = true }));
        Assert.Equal("1 failures", exception.Message);
        Assert.Throws<InvalidOperationException>(() => validator.ValidateAndThrow(new Person()));
        validator.ValidateAndThrow(new Person { Name = "Ada" });
    }

    [Fact]
    public async System.Threading.Tasks.Task ThrowOnFailures_async_uses_CreateValidationException()
    {
        var validator = new CustomExceptionValidator();

        await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateAsync(new Person(), new ValidationOptions { ThrowOnFailures = true }, Token));
    }

    [Fact]
    public void Child_validators_do_not_throw_on_their_own()
    {
        var validator = new Validator<Order>().For(x => x.Customer, new Validator<Customer>().For(x => x.Name, n => n.IfNull()));

        var exception = Assert.Throws<ValidationFailedException>(() => validator.ValidateAndThrow(new Order { Customer = new Customer() }));

        Assert.Equal(["Customer.Name"], Names.Of(exception.Failures));
        Assert.Same(exception.Report.Failures[0], exception.Failures[0]);
    }

    [Fact]
    public void Warnings_make_the_report_invalid_like_FluentValidation()
    {
        var validator = new Validator<Person>().For(x => x.Name, n => n.IfNull(), rule => rule.WithSeverity(Severity.Warning));

        Assert.False(validator.Validate(new Person()).IsValid);
    }

    private sealed class CustomExceptionValidator : Validator<Person>
    {
        public CustomExceptionValidator() => For(x => x.Name, n => n.IfNullOrEmpty());

        protected override Exception CreateValidationException(ValidationReport report) =>
            new InvalidOperationException($"{report.Failures.Count} failures");
    }
}
