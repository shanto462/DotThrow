using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using DotThrow.Dynamic;
using DotThrow.Tests.Dynamic;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Fuzz tests for dynamic rules: <see cref="RuleSetDefinition.FromJson"/> and building a
/// <see cref="DynamicValidator{T}"/> report every problem as a <see cref="RuleDefinitionException"/>
/// and throw nothing else; a validator that builds validates any object without throwing; and
/// <see cref="CheckArguments.Parse{TValue}"/> throws only <see cref="FormatException"/>.
/// </summary>
[Trait(Fuzz.Trait, Fuzz.Category)]
public sealed class DynamicRulesFuzzTests
{
    private static readonly Arbitrary<string?> DefinitionText = Arb.From(
        Gen.Frequency((8, Generators.AsNullable(Generators.RuleDefinitionJson)), (1, Generators.AsNullable(Generators.JsonLike)), (1, Generators.AnyTextGen)),
        Generators.Shrink);

    private static readonly PropertyMap<Sample> Map = Maps.Sample();

    [Fact]
    public void FromJson_throws_only_RuleDefinitionException() =>
        Fuzz.Run(DefinitionText, text =>
        {
            if (text is null)
            {
                Assert.Throws<ArgumentNullException>(() => RuleSetDefinition.FromJson(null!));
                return;
            }

            _ = Read(text);
        });

    // Every definition that reads builds a validator or lists its errors, with both constructors
    // (reflection and PropertyMap); FindErrors and TryUpdate agree with the constructor; and a
    // validator that builds runs on any Sample without throwing.
    [Fact]
    public void Building_and_running_a_validator_throws_only_definition_errors() =>
        Fuzz.Run(
            Arb.From(Gen.Zip(Generators.RuleDefinitionJson, Samples), (pair) => Generators.Shrink(pair.Item1).Select(text => (text ?? string.Empty, pair.Item2))),
            input =>
            {
                var (text, sample) = input;
                if (Read(text) is not { } definition)
                {
                    return;
                }

                foreach (var reflection in new[] { true, false })
                {
                    var errors = Build(definition, reflection, out var validator);
                    if (validator is null)
                    {
                        Assert.NotEmpty(errors);
                        continue;
                    }

                    Assert.Empty(errors);
                    Assert.Empty(validator.FindErrors(definition));
                    Assert.True(validator.TryUpdate(definition, out var updateErrors), string.Join("; ", updateErrors));
                    _ = validator.Describe();
                    Run(validator, sample, text);
                }
            },
            share: 0.25);

    [Fact]
    public void CheckArguments_Parse_throws_only_FormatException() =>
        Fuzz.Run(ArgumentText, text =>
        {
            Parses<int>(text);
            Parses<long>(text);
            Parses<ulong>(text);
            Parses<double>(text);
            Parses<float>(text);
            Parses<decimal>(text);
            Parses<bool>(text);
            Parses<char>(text);
            Parses<DateTime>(text);
            Parses<DateTimeOffset>(text);
            Parses<TimeSpan>(text);
            Parses<Guid>(text);
            Parses<Version>(text);
            Parses<Uri>(text);
            Parses<IPAddress>(text);
            Parses<BigInteger>(text);
            Parses<Status>(text);
            Parses<Access?>(text);
            Parses<int?>(text);
#if NET8_0_OR_GREATER
            Parses<DateOnly>(text);
            Parses<TimeOnly>(text);
            Parses<Half>(text);
            Parses<Int128>(text);
            Parses<UInt128>(text);
#endif
        });

    private static readonly Arbitrary<string> ArgumentText = Arb.From(
        Gen.Frequency(
            (3, Generators.AnyTextGen.Select(text => text ?? string.Empty)),
            (2, Generators.UriLike),
            (2, Gen.Elements("1", "-1", "2147483648", "1.5", "1e400", "2026-05-02", "10:30", "Active", "Read, Write", "::1", "https://a", "1.2.3", "x", " 1 ", "true", "6f9619ff-8b86-d011-b42d-00cf4fc964ff"))),
        text => Generators.Shrink(text).Select(shrunk => shrunk ?? string.Empty));

    private static readonly Gen<Sample> Samples =
        from text in Generators.AnyTextGen
        from number in Gen.Frequency((3, Gen.Choose(-200, 200)), (1, Gen.Elements(int.MinValue, int.MaxValue, 0)))
        from ratio in Gen.Elements(0.5, double.NaN, double.PositiveInfinity, -0.0, 1e300)
        from price in Gen.Elements(10.5m, -1m, 0m, decimal.MaxValue, 1.23456789m)
        from link in Gen.Frequency((1, Gen.Constant<string?>(null)), (3, Generators.AsNullable(Generators.UriLike)))
        from ip in Gen.Elements<IPAddress?>(null, IPAddress.Loopback, IPAddress.IPv6Loopback, IPAddress.Parse("10.0.0.1"), IPAddress.Parse("::ffff:127.0.0.1"))
        from count in Gen.Choose(0, 3)
        from tags in Gen.Frequency((1, Gen.Constant<string?>(null)), (4, Generators.AnyTextGen)).ArrayOf(count)
        from noAddress in Gen.Elements(false, false, true)
        from nullLine in Gen.Elements(false, false, true)
        select new Sample
        {
            Text = text,
            Number = number,
            MaybeNumber = number % 3 == 0 ? null : number,
            Ratio = ratio,
            Single = (float)ratio,
            Price = price,
            Link = Generators.ToUri(link),
            Ip = ip,
            Tags = [.. tags],
            Address = noAddress ? null : new Address { City = text, Zip = null, Floor = number },
            Lines = nullLine ? [null, new Line { Sku = text, Quantity = number }] : [new Line { Sku = text, Quantity = number, Notes = [.. tags] }],
            When = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc).AddDays(number % 10_000),
            Duration = TimeSpan.FromSeconds(number),
            MaybeStatus = number % 2 == 0 ? Status.Closed : null,
            Access = (Access)(number & 7),
            Letter = text is { Length: > 0 } ? text[0] : 'a',
        };

    // The definition, or null when FromJson reports errors (only as RuleDefinitionException).
    private static RuleSetDefinition? Read(string text)
    {
        try
        {
            return RuleSetDefinition.FromJson(text);
        }
        catch (RuleDefinitionException exception)
        {
            Assert.NotEmpty(exception.Errors);
            Assert.All(exception.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error)));
            return null;
        }
#pragma warning disable CA1031 // Any other exception is a bug.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            throw new InvalidOperationException($"FromJson threw {exception.GetType().FullName} for {Contract.Show(text)}.", exception);
        }
    }

    // The errors of building, with the validator when there are none.
    private static IReadOnlyList<string> Build(RuleSetDefinition definition, bool reflection, out DynamicValidator<Sample>? validator)
    {
        validator = null;
        try
        {
            validator = reflection ? new DynamicValidator<Sample>(definition) : new DynamicValidator<Sample>(definition, Map);
            return [];
        }
        catch (RuleDefinitionException exception)
        {
            Assert.All(exception.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error)));
            return exception.Errors;
        }
#pragma warning disable CA1031 // Any other exception is a bug.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            throw new InvalidOperationException($"Building a validator ({(reflection ? "reflection" : "PropertyMap")}) threw {exception.GetType().FullName}.", exception);
        }
    }

    private static void Run(DynamicValidator<Sample> validator, Sample sample, string text)
    {
        try
        {
            var report = validator.Validate(sample);
            _ = report.ToString();
            var grouped = validator.Validate(sample, "a", "b", "default");
            Assert.All(grouped.Failures, failure => Assert.NotNull(failure.Message));
            if (!report.IsValid)
            {
                Assert.Throws<ValidationFailedException>(() => validator.ValidateAndThrow(sample));
            }
        }
        catch (ValidationFailedException)
        {
            throw;
        }
#pragma warning disable CA1031 // Any exception from a validator that built is a bug.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            throw new InvalidOperationException($"Validate threw {exception.GetType().FullName} for the definition {Contract.Show(text)}.", exception);
        }
    }

    private static void Parses<TValue>(string text)
    {
        try
        {
            _ = CheckArguments.Parse<TValue>(text);
        }
        catch (FormatException)
        {
            // The documented exception; any other one fails the property.
        }
    }
}
