#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// Decision D9 follow-up. This file must compile with nullable warnings as errors: a string?
// property gives Guard<string>, and collections of nullable items work with chains and
// child validators, with no CS8619 or CS8620.
public sealed class NullabilityCompileTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Model
    {
        public string? Optional { get; set; }

        public string Required { get; set; } = "x";

        public List<string?> NullableTags { get; set; } = [];

        public List<string> Tags { get; set; } = [];

        public List<Address?> NullableAddresses { get; set; } = [];

        public List<Address> Addresses { get; set; } = [];

        public IEnumerable<int?>? Numbers { get; set; }

        public Address? Address { get; set; }

        public int? Count { get; set; }
    }

    [Fact]
    public async Task Nullable_and_non_nullable_members_compile_without_warnings()
    {
        var address = new AddressValidator();
        var validator = new Validator<Model>()
            .For(x => x.Optional, o => o.AllowNull().IfLongerThan(5))
            .For(x => x.Required, r => r.IfNullOrEmpty())
            .For(x => x.Optional, (o, x) => o.AllowNull().IfLongerThan(x.Required.Length + 10))
            .ForEach(x => x.NullableTags, t => t.IfNullOrEmpty())
            .ForEach(x => x.Tags, t => t.IfNullOrEmpty())
            .ForEach(x => x.NullableAddresses, address)
            .ForEach(x => x.Addresses, address)
            .ForEach(x => x.Numbers, n => n.IfNull().IfNegative())
            .For(x => x.Address, address)
            .For(x => x.Address, a => a.For(y => y.City, c => c.IfNullOrEmpty()))
            .For(x => x.Count, c => c.IfNull().IfNegative())
            .Must(x => x.Optional, o => o.Length > 0)
            .MustAsync(x => x.Optional, (o, _) => Task.FromResult(o.Length > 0));

        Assert.Null(CheckOptional(new Model()));
        var report = await validator.ValidateAsync(new Model { Optional = "abc", Numbers = [1, null], Count = 1 }, Token);

        Assert.Equal(["Numbers[1]"], Names.Of(report));
    }

    private static string? CheckOptional(Model model)
    {
        Guard<string> guard = new ValidationReport().Check(model.Optional);
        return guard.Value;
    }

    [Fact]
    public async Task ValidateAsync_with_null_options_or_groups_compiles()
    {
        var validator = new Validator<Model>();
        IValidator<Model> contract = validator;

        await validator.ValidateAsync(new Model(), null, Token);
        await validator.ValidateAsync(new Model(), groups: null, Token);
        await contract.ValidateAsync(new Model(), null, Token);
        await contract.ValidateAsync(new Model(), Token);
        validator.Validate(new Model(), null);
        contract.Validate(new Model());
        contract.ValidateAndThrow(new Model());
        await contract.ValidateAndThrowAsync(new Model(), Token);
        Assert.True(contract.Validate(new Model(), "a", "b").IsValid);
    }
}
