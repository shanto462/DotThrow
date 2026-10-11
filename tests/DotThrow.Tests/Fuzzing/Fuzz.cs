using System;
using System.Globalization;
using System.Linq;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.FSharp.Core;

namespace DotThrow.Tests.Fuzzing;

/// <summary>
/// Runs the property-based fuzz tests with FsCheck. Each property gets random inputs from the
/// generators in <see cref="Generators"/>; on a failure FsCheck shrinks the input and throws
/// with the seed, the original input and the shrunk input, so xUnit shows them.
/// </summary>
/// <remarks>
/// <para>
/// A normal test run uses a modest number of cases per property, so the fuzz tests stay fast.
/// Set <c>DOTTHROW_FUZZ_ITERATIONS</c> (for example to <c>50000</c>) for a deep run: every
/// property then runs that many cases, and the slow ones a fixed share of it.
/// </para>
/// <para>
/// To replay a failure, copy the numbers from the line "Replay directly at failing step with
/// (6955468475102380717,9578494888614701779,2)" of the failure message and run the same test
/// with <c>DOTTHROW_FUZZ_REPLAY=6955468475102380717,9578494888614701779,2</c>: the first case
/// is then the failing one. The seed pair after "Falsifiable" works too (without a size); it
/// replays the whole run.
/// </para>
/// </remarks>
internal static class Fuzz
{
    /// <summary>The trait of every fuzz test: <c>--filter "Category=Fuzz"</c> runs only them.</summary>
    public const string Trait = "Category";

    /// <summary>The value of <see cref="Trait"/>.</summary>
    public const string Category = "Fuzz";

    public const string IterationsVariable = "DOTTHROW_FUZZ_ITERATIONS";

    public const string ReplayVariable = "DOTTHROW_FUZZ_REPLAY";

    /// <summary>The cases of a property in a normal test run.</summary>
    public const int NormalCases = 1000;

    private static readonly int? DeepCases = ReadIterations();

    private static readonly Replay? Replayed = ReadReplay();

    /// <summary>
    /// Checks a property on random inputs and throws when it fails. The property fails when it
    /// throws, for example from an xUnit assertion.
    /// </summary>
    /// <param name="input">The inputs.</param>
    /// <param name="property">The property.</param>
    /// <param name="share">
    /// The share of the cases this property runs, for properties that are much slower than the
    /// others (for example 0.1 for one that builds a validator per case).
    /// </param>
    public static void Run<T>(Arbitrary<T> input, Action<T> property, double share = 1)
    {
        var cases = Math.Max(1, (int)((DeepCases ?? NormalCases) * share));
        var config = Config.QuickThrowOnFailure
            .WithMaxTest(cases)
            .WithMaxRejected(cases * 10)
            .WithQuietOnSuccess(true);
        if (Replayed is { } replay)
        {
            config = config.WithReplay(FSharpOption<Replay>.Some(replay));
        }

        FsCheck.Check.One(config, Prop.ForAll(input, property));
    }

    private static int? ReadIterations()
    {
        var text = Environment.GetEnvironmentVariable(IterationsVariable);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"{IterationsVariable} must be a positive whole number, not '{text}'.");
    }

    // "seed,gamma" or "seed,gamma,size", as printed in a failure message (brackets are allowed).
    private static Replay? ReadReplay()
    {
        var text = Environment.GetEnvironmentVariable(ReplayVariable);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text!.Trim('(', ')', ' ').Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length is 2 or 3
            && ulong.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var seed)
            && ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var gamma))
        {
            var size = parts.Length == 3 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? FSharpOption<int>.Some(value)
                : FSharpOption<int>.None;
            return new Replay(new Rnd(seed, gamma), size);
        }

        throw new InvalidOperationException($"{ReplayVariable} must be 'seed,gamma' or 'seed,gamma,size', not '{text}'.");
    }
}
