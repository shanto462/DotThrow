using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FluentValidation;
using FvCascadeMode = FluentValidation.CascadeMode;

namespace DotThrow.Benchmarks;

/// <summary>
/// DotThrow validators next to FluentValidation validators with the same rules. FluentValidation
/// is the baseline of every category. Every validator is built once, in a static field, outside
/// the measured methods. FluentValidation stops each rule at its first failure
/// (<c>RuleLevelCascadeMode = Stop</c>), which is DotThrow's default, so both report the same
/// failures; <c>--check</c> prints the failure counts to prove it.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ValidationBenchmarks
{
    private const string SmallValid = "Small model, valid";
    private const string SmallInvalid = "Small model, 4 failures";
    private const string NestedValid = "Nested model, valid";
    private const string NestedInvalid = "Nested model, 5 failures";
    private const string AsyncApi = "Small model, ValidateAsync, valid";
    private const string AsyncRule = "Small model + async rule, valid";

    private static readonly Task<bool> Done = Task.FromResult(true);

    private static readonly SignUpFluentValidator FluentSignUp = new();
    private static readonly SignUpAsyncFluentValidator FluentSignUpAsync = new();
    private static readonly OrderFluentValidator FluentOrder = new();

    private static readonly Validator<SignUp> DotThrowSignUp = new Validator<SignUp>()
        .For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(50))
        .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress())
        .For(x => x.Age, age => age.IfOutOfRange(0, 150))
        .For(x => x.Tags, tags => tags.IfNullOrEmpty().IfCountGreaterThan(10));

    private static readonly Validator<SignUp> DotThrowSignUpAsync = new Validator<SignUp>()
        .Include(DotThrowSignUp)
        .MustAsync(x => x.Email, (email, cancellationToken) => Done);

    private static readonly Validator<Customer> DotThrowCustomer = new Validator<Customer>()
        .For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(50))
        .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress());

    private static readonly Validator<OrderLine> DotThrowLine = new Validator<OrderLine>()
        .For(x => x.Sku, sku => sku.IfNullOrWhiteSpace().IfLongerThan(20))
        .For(x => x.Quantity, quantity => quantity.IfOutOfRange(1, 100))
        .For(x => x.Price, price => price.IfNegative());

    private static readonly Validator<Order> DotThrowOrder = new Validator<Order>()
        .For(x => x.Number, number => number.IfNullOrWhiteSpace().IfNotStartsWith("ORD-"))
        .For(x => x.Customer, customer => customer.IfNull())
        .For(x => x.Customer, DotThrowCustomer)
        .For(x => x.Lines, lines => lines.IfNullOrEmpty())
        .ForEach(x => x.Lines, DotThrowLine)
        .When(x => x.IsGift, gift => gift.For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace()));

    private SignUp[] _validSignUps = [];
    private SignUp[] _invalidSignUps = [];
    private Order[] _validOrders = [];
    private Order[] _invalidOrders = [];

    [GlobalSetup]
    public void Setup()
    {
        _validSignUps = TestData.ValidSignUps();
        _invalidSignUps = TestData.InvalidSignUps();
        _validOrders = TestData.ValidOrders();
        _invalidOrders = TestData.InvalidOrders();
    }

    // ---- small model -------------------------------------------------------------------

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(SmallValid)]
    public int SmallValidFluentValidation() => CountFluent(_validSignUps);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(SmallValid)]
    public int SmallValidDotThrowValidator() => CountDotThrow(_validSignUps);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow ValidationReport")]
    [BenchmarkCategory(SmallValid)]
    public int SmallValidDotThrowReport() => CountReport(_validSignUps);

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(SmallInvalid)]
    public int SmallInvalidFluentValidation() => CountFluent(_invalidSignUps);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(SmallInvalid)]
    public int SmallInvalidDotThrowValidator() => CountDotThrow(_invalidSignUps);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow ValidationReport")]
    [BenchmarkCategory(SmallInvalid)]
    public int SmallInvalidDotThrowReport() => CountReport(_invalidSignUps);

    // ---- nested model: child validator, collection of child validators, When block -----

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(NestedValid)]
    public int NestedValidFluentValidation() => CountFluent(_validOrders);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(NestedValid)]
    public int NestedValidDotThrowValidator() => CountDotThrow(_validOrders);

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(NestedInvalid)]
    public int NestedInvalidFluentValidation() => CountFluent(_invalidOrders);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(NestedInvalid)]
    public int NestedInvalidDotThrowValidator() => CountDotThrow(_invalidOrders);

    // ---- async -------------------------------------------------------------------------

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(AsyncApi)]
    public async Task<int> AsyncApiFluentValidation()
    {
        var failures = 0;
        foreach (var signUp in _validSignUps)
        {
            failures += (await FluentSignUp.ValidateAsync(signUp).ConfigureAwait(false)).Errors.Count;
        }

        return failures;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(AsyncApi)]
    public async Task<int> AsyncApiDotThrowValidator()
    {
        var failures = 0;
        foreach (var signUp in _validSignUps)
        {
            failures += (await DotThrowSignUp.ValidateAsync(signUp).ConfigureAwait(false)).Failures.Count;
        }

        return failures;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(AsyncRule)]
    public async Task<int> AsyncRuleFluentValidation()
    {
        var failures = 0;
        foreach (var signUp in _validSignUps)
        {
            failures += (await FluentSignUpAsync.ValidateAsync(signUp).ConfigureAwait(false)).Errors.Count;
        }

        return failures;
    }

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(AsyncRule)]
    public async Task<int> AsyncRuleDotThrowValidator()
    {
        var failures = 0;
        foreach (var signUp in _validSignUps)
        {
            failures += (await DotThrowSignUpAsync.ValidateAsync(signUp).ConfigureAwait(false)).Failures.Count;
        }

        return failures;
    }

    private static int CountFluent(SignUp[] signUps)
    {
        var failures = 0;
        foreach (var signUp in signUps)
        {
            failures += FluentSignUp.Validate(signUp).Errors.Count;
        }

        return failures;
    }

    private static int CountFluent(Order[] orders)
    {
        var failures = 0;
        foreach (var order in orders)
        {
            failures += FluentOrder.Validate(order).Errors.Count;
        }

        return failures;
    }

    private static int CountDotThrow(SignUp[] signUps)
    {
        var failures = 0;
        foreach (var signUp in signUps)
        {
            failures += DotThrowSignUp.Validate(signUp).Failures.Count;
        }

        return failures;
    }

    private static int CountDotThrow(Order[] orders)
    {
        var failures = 0;
        foreach (var order in orders)
        {
            failures += DotThrowOrder.Validate(order).Failures.Count;
        }

        return failures;
    }

    private static int CountReport(SignUp[] signUps)
    {
        var failures = 0;
        foreach (var signUp in signUps)
        {
            var report = new ValidationReport();
            report.Check(signUp.Name).IfNullOrWhiteSpace().IfLongerThan(50);
            report.Check(signUp.Email).IfNullOrWhiteSpace().IfNotEmailAddress();
            report.Check(signUp.Age).IfOutOfRange(0, 150);
            report.Check(signUp.Tags).IfNullOrEmpty().IfCountGreaterThan(10);
            failures += report.Failures.Count;
        }

        return failures;
    }

    private sealed class SignUpFluentValidator : AbstractValidator<SignUp>
    {
        public SignUpFluentValidator()
        {
            RuleLevelCascadeMode = FvCascadeMode.Stop;
            RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Age).InclusiveBetween(0, 150);
            RuleFor(x => x.Tags).NotEmpty().Must(tags => tags.Count <= 10);
        }
    }

    private sealed class SignUpAsyncFluentValidator : AbstractValidator<SignUp>
    {
        public SignUpAsyncFluentValidator()
        {
            Include(new SignUpFluentValidator());
            RuleFor(x => x.Email).MustAsync((email, cancellationToken) => Done);
        }
    }

    private sealed class CustomerFluentValidator : AbstractValidator<Customer>
    {
        public CustomerFluentValidator()
        {
            RuleLevelCascadeMode = FvCascadeMode.Stop;
            RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    private sealed class OrderLineFluentValidator : AbstractValidator<OrderLine>
    {
        public OrderLineFluentValidator()
        {
            RuleLevelCascadeMode = FvCascadeMode.Stop;
            RuleFor(x => x.Sku).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0m);
        }
    }

    private sealed class OrderFluentValidator : AbstractValidator<Order>
    {
        public OrderFluentValidator()
        {
            RuleLevelCascadeMode = FvCascadeMode.Stop;
            RuleFor(x => x.Number).NotEmpty().Must(number => number.StartsWith("ORD-", StringComparison.Ordinal));
            RuleFor(x => x.Customer).NotNull().SetValidator(new CustomerFluentValidator()!);
            RuleFor(x => x.Lines).NotEmpty();
            // FluentValidation's Stop on a collection rule stops at the first failed item;
            // DotThrow stops per item. Continue here, so both check every line.
            RuleForEach(x => x.Lines).Cascade(FvCascadeMode.Continue).SetValidator(new OrderLineFluentValidator());
            When(x => x.IsGift, () => RuleFor(x => x.GiftMessage).NotEmpty());
        }
    }
}
