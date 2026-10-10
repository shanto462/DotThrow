#if NET8_0_OR_GREATER
using System.Threading;
using DotThrow.Tests.Support;
using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Performance;

// A valid sync run allocates only the report. Measured on the current thread after warm-up.
public sealed class ValidatorAllocationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Validator<Person> PersonValidator = new Validator<Person>()
        .For(x => x.Name, n => n.IfNullOrWhiteSpace().IfLongerThan(50))
        .For(x => x.Email, e => e.IfNullOrWhiteSpace().IfNotEmailAddress())
        .For(x => x.Age, a => a.IfOutOfRange(0, 150))
        .For(x => x.End, (end, x) => end.IfBefore(x.Start))
        .ForEach(x => x.Tags, t => t.IfNullOrEmpty())
        .For(x => x.Address, new AddressValidator());

    [Fact]
    public void Valid_sync_run_allocates_only_the_report_and_the_child_context()
    {
        Allocations.SkipUnlessOptimized();
        var person = Person.Valid();
        var allocated = Allocations.Measure(() => Assert.True(PersonValidator.Validate(person).IsValid), iterations: 1, allowedPerCall: 40 + 200);

        Assert.True(allocated <= 40 + 200, $"Allocated {allocated} bytes.");
    }

    [Fact]
    public void Valid_sync_run_without_children_allocates_only_the_report()
    {
        Allocations.SkipUnlessOptimized();
        var validator = new Validator<Person>()
            .For(x => x.Name, n => n.IfNullOrWhiteSpace().IfLongerThan(50))
            .For(x => x.Age, a => a.IfOutOfRange(0, 150))
            .ForEach(x => x.Tags, t => t.IfNullOrEmpty())
            .Must(x => x.Age < 200);
        var person = Person.Valid();
        var allocated = Allocations.Measure(() => validator.Validate(person), iterations: 1, allowedPerCall: 40);

        Assert.True(allocated <= 40, $"Allocated {allocated} bytes.");
    }

    [Fact]
    public void Valid_async_run_of_sync_rules_allocates_the_report_and_one_task()
    {
        Allocations.SkipUnlessOptimized();
        var validator = new Validator<Person>().For(x => x.Name, n => n.IfNullOrWhiteSpace());
        var person = Person.Valid();
        var allocated = Allocations.Measure(() => Assert.True(validator.ValidateAsync(person, Token).IsCompletedSuccessfully), iterations: 1, allowedPerCall: 40 + 80);

        Assert.True(allocated <= 40 + 80, $"Allocated {allocated} bytes.");
    }
}
#endif
