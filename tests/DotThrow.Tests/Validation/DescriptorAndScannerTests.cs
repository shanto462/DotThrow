using System.Linq;
using System.Threading.Tasks;
using DotThrow.Tests.Validation.Models;
using DotThrow.Scanning;
using Xunit;

namespace DotThrow.Tests.Validation;

public sealed class DescriptorAndScannerTests
{
    [Fact]
    public void Describe_lists_rules_with_their_metadata()
    {
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrEmpty(), rule => rule.WithErrorCode("N").WithSeverity(Severity.Warning).Cascade(CascadeMode.Continue)
                .DependentRules(v => v.For(x => x.Email, e => e.IfNull())))
            .For(x => x.Address, new AddressValidator(), rule => rule.InGroup("a", "b").When(x => x.IsCompany))
            .ForEach(x => x.Tags, t => t.IfNull(), rule => rule.WhereAsync((_, _) => Task.FromResult(true)))
            .Must(x => x.Age > 0)
            .MustAsync((_, _) => Task.FromResult(true))
            .Custom((_, _) => { })
            .Include(new Validator<Person>());

        var descriptor = validator.Describe();

        Assert.Equal(typeof(Person), descriptor.ModelType);
        Assert.Equal(
            [RuleKind.Property, RuleKind.Child, RuleKind.Collection, RuleKind.Must, RuleKind.MustAsync, RuleKind.Custom, RuleKind.Include],
            descriptor.Rules.Select(r => r.Kind));
        var name = descriptor.Rules[0];
        Assert.Equal("Name", name.Path);
        Assert.Equal("x => x.Name", name.Expression);
        Assert.Equal("N", name.ErrorCode);
        Assert.Equal(Severity.Warning, name.Severity);
        Assert.Equal(CascadeMode.Continue, name.Cascade);
        Assert.Equal("Email", Assert.Single(name.DependentRules).Path);
        var address = descriptor.Rules[1];
        Assert.Equal(typeof(AddressValidator), address.ChildValidatorType);
        Assert.Equal(["a", "b"], address.Groups);
        Assert.True(address.HasCondition);
        Assert.True(descriptor.Rules[2].HasAsyncCondition);
        Assert.Equal([ErrorCodes.Must], descriptor.Rules[3].Checks);
        Assert.True(descriptor.Rules[4].IsAsync);
        Assert.True(descriptor.HasAsyncRules);
        Assert.Single(descriptor.GetRules("Email"));
        Assert.False(new AddressValidator().Describe().HasAsyncRules);
    }

    [Fact]
    public void CanValidate_accepts_the_type_and_derived_types()
    {
#pragma warning disable CA1859 // The test covers the non-generic interface on purpose.
        IValidator validator = new Validator<Payment>();
#pragma warning restore CA1859

        Assert.True(validator.CanValidate(typeof(Payment)));
        Assert.True(validator.CanValidate(typeof(CardPayment)));
        Assert.False(validator.CanValidate(typeof(Person)));
    }

    [Fact]
    public void Non_generic_interface_validates_objects()
    {
        IValidator validator = new AddressValidator();

        Assert.False(validator.Validate(new Address(), null).IsValid);
        Assert.Throws<System.ArgumentException>(() => validator.Validate(new Person(), null));
    }

    [Fact]
    public void AssemblyScanner_finds_concrete_validators()
    {
        var results = AssemblyScanner.FindValidatorsInAssemblyContaining<AddressValidator>().ToList();

        Assert.Contains(results, r => r.ValidatorType == typeof(AddressValidator) && r.InterfaceType == typeof(IValidator<Address>) && r.ModelType == typeof(Address));
        Assert.Contains(results, r => r.ValidatorType == typeof(LineValidator));
        Assert.DoesNotContain(results, r => r.ValidatorType.IsGenericTypeDefinition);
        Assert.DoesNotContain(results, r => r.ValidatorType == typeof(Validator<>));

        var withInternal = AssemblyScanner.FindValidatorsInAssemblies([typeof(AddressValidator).Assembly, typeof(AddressValidator).Assembly], includeInternalTypes: true).ToList();
        Assert.Equal(withInternal.Count, withInternal.Distinct().Count());
        Assert.True(withInternal.Count >= results.Count);
        var seen = 0;
#pragma warning disable CA2263 // The test covers the Type overload on purpose.
        AssemblyScanner.FindValidatorsInAssemblyContaining(typeof(Person)).ForEach(_ => seen++);
#pragma warning restore CA2263
        Assert.Equal(results.Count, seen);
    }
}
