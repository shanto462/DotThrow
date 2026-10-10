using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FluentValidation;

namespace DotThrow.Benchmarks;

/// <summary>
/// Validating a small model with four rules, with valid input (no failures) and with
/// invalid input (one failure on each property, so 4 failures). FluentValidation is the
/// baseline. Every validator is built once, outside the measured methods.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ValidationBenchmarks
{
    private const string Valid = "Valid input";
    private const string Invalid = "Invalid input, 4 failures";

    private static readonly SignUpFluentValidator FluentValidator = new();

    private static readonly Validator<SignUp> DotThrowValidator = new Validator<SignUp>()
        .For(x => x.Name, name => name.IfNullOrWhiteSpace().IfLongerThan(50))
        .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress())
        .For(x => x.Age, age => age.IfOutOfRange(0, 150))
        .For(x => x.Tags, tags => tags.IfNullOrEmpty().IfCountGreaterThan(10));

    private SignUp[] _valid = [];
    private SignUp[] _invalid = [];

    [GlobalSetup]
    public void Setup()
    {
        _valid = TestData.ValidSignUps();
        _invalid = TestData.InvalidSignUps();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(Valid)]
    public int ValidFluentValidation() => CountFluentValidation(_valid);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(Valid)]
    public int ValidDotThrowValidator() => CountDotThrowValidator(_valid);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow ValidationReport")]
    [BenchmarkCategory(Valid)]
    public int ValidDotThrowReport() => CountDotThrowReport(_valid);

    [Benchmark(Baseline = true, OperationsPerInvoke = TestData.Count, Description = "FluentValidation")]
    [BenchmarkCategory(Invalid)]
    public int InvalidFluentValidation() => CountFluentValidation(_invalid);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow Validator<T>")]
    [BenchmarkCategory(Invalid)]
    public int InvalidDotThrowValidator() => CountDotThrowValidator(_invalid);

    [Benchmark(OperationsPerInvoke = TestData.Count, Description = "DotThrow ValidationReport")]
    [BenchmarkCategory(Invalid)]
    public int InvalidDotThrowReport() => CountDotThrowReport(_invalid);

    private static int CountFluentValidation(SignUp[] signUps)
    {
        var failures = 0;
        foreach (var signUp in signUps)
        {
            failures += FluentValidator.Validate(signUp).Errors.Count;
        }

        return failures;
    }

    private static int CountDotThrowValidator(SignUp[] signUps)
    {
        var failures = 0;
        foreach (var signUp in signUps)
        {
            failures += DotThrowValidator.Validate(signUp).Failures.Count;
        }

        return failures;
    }

    private static int CountDotThrowReport(SignUp[] signUps)
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
}

/// <summary>
/// The same four rules in FluentValidation. <see cref="CascadeMode.Stop"/> stops each rule at
/// its first failure, which is what a DotThrow chain does, so both report the same failures.
/// </summary>
public sealed class SignUpFluentValidator : AbstractValidator<SignUp>
{
    public SignUpFluentValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).InclusiveBetween(0, 150);
        RuleFor(x => x.Tags).NotEmpty().Must(tags => tags.Count <= 10);
    }
}
