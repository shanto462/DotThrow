# Performance

DotThrow is built so that a check that passes costs about as much as the `if` you would
write by hand, does not allocate for numbers, strings, dates, enums and in-memory
collections, and does all the expensive work (building a message,
looking up a template) only when a check fails. This page explains how, and shows the
numbers next to hand-written code, the .NET built-in throw helpers, other guard libraries
and FluentValidation.

The benchmarks live in [benchmarks/DotThrow.Benchmarks](../benchmarks/DotThrow.Benchmarks)
and use [BenchmarkDotNet](https://benchmarkdotnet.org/).

- [How DotThrow stays fast](#how-dotthrow-stays-fast)
- [What is measured](#what-is-measured)
- [Machine and versions](#machine-and-versions)
- [Guard clauses](#guard-clauses)
- [Validation](#validation)
- [Single checks](#single-checks)
- [Reading the numbers](#reading-the-numbers)
- [What allocates](#what-allocates)
- [Run the benchmarks](#run-the-benchmarks)

## How DotThrow stays fast

**Guard clauses**

| Technique | Effect |
| --- | --- |
| `Guard<T>` is a `readonly ref struct`, passed by value | A chain never allocates and is never boxed. |
| Checks are `[AggressiveInlining]` with one combined condition | When a check passes, it costs one compare and one branch. |
| The throw is part of the inlined code | The JIT knows that the failure branch never returns. |
| | So it keeps the guard in registers and drops the "is the chain still active" tests of later checks. |
| | A `Throw()` chain compiles to the same instructions as a hand-written `if`. |
| The failure path is a separate, non-inlined method that gets the parts of the guard | Callers do not copy the guard to the stack. |
| Messages are built only on failure | Templates are parsed once and cached per culture; values are formatted straight into a pooled `StringBuilder`, without boxing. |
| Argument mistakes that make every value fail (`min > max`, a negative maximum, a zero divisor) are checked on the failure path | Valid arguments cost nothing extra. |
| `typeof(T) == typeof(X)` specialization | Plain operators for `int`, `long`, `double`, `float` and `decimal`; ordinal string compares; the JIT removes the other branches. |
| Count checks have overloads for `T[]`, `List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>` and the collection interfaces | They read `Count` or `Length` directly, with no interface casts, and a `HashSet<int>` is never enumerated or boxed. A lazy sequence is enumerated only as far as needed. |
| Generic math, `SearchValues<char>`, `Ascii` and span APIs on .NET 8 and later | `IfOdd<T>`, `IfNaN<T>` and the character-class checks use the fastest APIs of each framework; older frameworks use simple loops. |
| DotThrow's own IP address parser, on every framework | `IfNotIPAddress` does not create an `IPAddress` and gives the same answer on every runtime. |
| `params ReadOnlySpan<T>` overloads (.NET 8 and later, C# 13 compilers) | `IfNotOneOf(1, 2, 3)` binds to a span, not an array. On .NET 10 it allocated nothing in our run. |
| Enum data cached per type | The flag checks read a cached mask; `IfNotEnumName` uses a cached `FrozenSet` of names on .NET 8 and later. |
| `[SkipLocalsInit]` | Less stack zeroing. |
| No LINQ, no closures, no boxing on hot paths | |

The JIT output on Arm64 shows the effect. With
`DOTNET_JitDisasm=ChainInt` and full optimization, this method:

```csharp
public static class Disasm
{
    public static int ChainInt(int age) => age.Throw().IfOutOfRange(0, 150);
}
```

compiles to a compare against 150, a branch to the throw, and a return, the same as
`if ((uint)age > 150) throw ...`. The [verify/CoreA.Disasm](../verify/CoreA.Disasm)
project prints this output.

**Validators**

| Technique | Effect |
| --- | --- |
| Rules are built once and published copy-on-write | Validation takes no lock and is thread-safe. |
| The run context is reused per thread, and the failure list is created on the first failure | A valid run of a validator without child validators allocates only the report. |
| Conditions and validator providers are wrapped once, when the rule is built | Nothing is wrapped per run. |
| `ValidateAsync` runs synchronously until a task is really pending | With only synchronous rules it returns a completed task: no `async` state machine. |

## What is measured

**Guard clauses** ([GuardBenchmarks.cs](../benchmarks/DotThrow.Benchmarks/GuardBenchmarks.cs)).
Four checks, each written six ways: a hand-written `if` (the baseline), the .NET built-in
throw helpers, DotThrow, Ardalis.GuardClauses, CommunityToolkit.Diagnostics and Throw.

- An `int` in the range 0 to 150.
- A `string` that is not null, empty or white space, with at most 50 characters.
- A `List<string>` that is not null or empty, with at most 10 items.
- A `HashSet<int>` that is not null or empty, with at most 10 items.

**Validation** ([ValidationBenchmarks.cs](../benchmarks/DotThrow.Benchmarks/ValidationBenchmarks.cs)).
FluentValidation is the baseline of each case.

- A small model with four rules (name, email, age, tags), valid and with 4 failures. DotThrow
  runs it both as a `Validator<T>` and as a `ValidationReport`.
- A nested model: an order with a child validator for the customer, a child validator for
  each of three lines, and a `When` block. Valid, and with 5 failures.
- The small model through `ValidateAsync` (only synchronous rules), and with an extra async
  rule that completes at once.

FluentValidation runs with `RuleLevelCascadeMode = Stop`, which is DotThrow's default, and
its collection rule continues to every item, as DotThrow does, so both libraries report
exactly the same failures.

**Single checks** ([CheckBenchmarks.cs](../benchmarks/DotThrow.Benchmarks/CheckBenchmarks.cs)).
A few DotThrow checks next to the .NET code you would write instead: IPv4 and IPv6 text
against `IPAddress.TryParse`, `IfNotDefined` against `Enum.IsDefined`, and
`IfNotOneOf(1, 2, 3)` against `is 1 or 2 or 3`.

Every input is valid unless the case says otherwise. One check takes about a nanosecond,
which is too short to time alone, so each benchmark runs 1,024 different inputs and
reports the time per input. All inputs are made once in `[GlobalSetup]` and the validators
once in static fields, outside the measured code. `--check` runs every benchmark once and
prints its result, to prove that all variants do the same work (for example, 5,120
failures for 1,024 invalid orders in both libraries).

## Machine and versions

- Apple M4 Pro (12 cores), macOS 27.0.1
- .NET SDK 10.0.401, .NET 10.0.12, Arm64 RyuJIT
- BenchmarkDotNet 0.15.8, in-process toolchain, 1 launch, 8 warm-up and 20 measured
  iterations
- DotThrow 2.0.0 (2.0.1 has the same code), Ardalis.GuardClauses 5.0.0,
  CommunityToolkit.Diagnostics 8.4.2, FluentValidation 12.1.1, Throw 1.4.0
- Run on 2026-10-10. Other work (builds and a Docker test run) used the machine at the
  same time, so absolute times may be a little high; the full reports show the error of
  every number.

## Guard clauses

Mean time per check, with valid input. "Allocated" is memory per check; `-` means none.

| Check | Library | Mean | Ratio | Allocated |
| --- | --- | ---: | ---: | ---: |
| Int in 0..150 | Hand-written `if` | 0.25 ns | 1.00 | - |
| | .NET built-in | 0.32 ns | 1.28 | - |
| | **DotThrow** | **0.27 ns** | **1.09** | **-** |
| | Ardalis.GuardClauses | 0.54 ns | 2.18 | - |
| | CommunityToolkit.Diagnostics | 0.27 ns | 1.08 | - |
| | Throw | 0.56 ns | 2.25 | - |
| String not blank, max 50 | Hand-written `if` | 0.69 ns | 1.00 | - |
| | .NET built-in | 0.73 ns | 1.06 | - |
| | **DotThrow** | **0.70 ns** | **1.02** | **-** |
| | Ardalis.GuardClauses | 3.29 ns | 4.79 | 24 B |
| | CommunityToolkit.Diagnostics | 0.68 ns | 0.99 | - |
| | Throw | 9.39 ns | 13.66 | 96 B |
| List not null or empty, max 10 | Hand-written `if` | 0.41 ns | 1.00 | - |
| | .NET built-in | 0.49 ns | 1.19 | - |
| | **DotThrow** | **0.51 ns** | **1.24** | **-** |
| | Ardalis.GuardClauses | 2.06 ns | 5.03 | - |
| | CommunityToolkit.Diagnostics | 0.44 ns | 1.07 | - |
| | Throw | 3.04 ns | 7.41 | - |
| HashSet&lt;int&gt; not null or empty, max 10 | Hand-written `if` | 0.40 ns | 1.00 | - |
| | .NET built-in | 0.51 ns | 1.26 | - |
| | **DotThrow** | **0.45 ns** | **1.13** | **-** |
| | Ardalis.GuardClauses | 1.46 ns | 3.65 | - |
| | CommunityToolkit.Diagnostics | 1.75 ns | 4.35 | - |
| | Throw | 23.65 ns | 58.90 | 80 B |

The `HashSet<int>` rows come from a second run of that category, after a later
optimization of the count checks (same machine and settings).

## Validation

Mean time and memory per validated object. FluentValidation is the baseline.

| Case | FluentValidation 12.1.1 | DotThrow `Validator<T>` | DotThrow `ValidationReport` |
| --- | ---: | ---: | ---: |
| Small model, valid | 202.31 ns, 704 B | **76.27 ns, 40 B** | **42.95 ns, 40 B** |
| Small model, 4 failures | 1,747.51 ns, 7,712 B | **260.18 ns, 864 B** | **194.44 ns, 912 B** |
| Nested model, valid | 1,322.27 ns, 5,064 B | **224.96 ns, 424 B** | |
| Nested model, 5 failures | 3,902.97 ns, 14,256 B | **561.42 ns, 1,856 B** | |
| Small model, `ValidateAsync`, valid | 298.60 ns, 776 B | **91.55 ns, 112 B** | |
| Small model and an async rule, valid | 462.26 ns, 1,336 B | **114.23 ns, 208 B** | |

## Single checks

Mean time per check, with valid input. The .NET code is the baseline.

| Check | .NET code | DotThrow |
| --- | ---: | ---: |
| IPv4 text | `IPAddress.TryParse`: 16.23 ns, 40 B | `IfNotIPAddress()`: **10.40 ns, 0 B** |
| IPv6 text | `IPAddress.TryParse`: 107.76 ns, 80 B | `IfNotIPAddress()`: **20.90 ns, 0 B** |
| Enum value is defined | `Enum.IsDefined`: 1.04 ns | `IfNotDefined()`: **0.93 ns** |
| Int is one of 1, 2, 3 | `is 1 or 2 or 3`: 0.26 ns | `IfNotOneOf(1, 2, 3)`: **0.35 ns, 0 B** |

## Reading the numbers

- A DotThrow chain costs about the same as the hand-written `if` (0.27, 0.70, 0.51 and
  0.45 ns against 0.25, 0.69, 0.41 and 0.40 ns) and allocated nothing. The .NET built-ins
  and CommunityToolkit.Diagnostics are about as fast for the int, string and list.
  Ardalis.GuardClauses and Throw took 2 to 59 times as long as the hand-written code, and
  allocated in some cases.
- CommunityToolkit.Diagnostics needs a cast to an interface for a `HashSet` and took
  1.75 ns there.
- DotThrow's `Validator<T>` was 2.7 times (small model, valid) to 7 times (nested model
  with failures) faster than FluentValidation, and allocated 6% to 16% of its memory. A
  valid run of the small model allocated only the 40 B report.
- `ValidationReport` is the lightest way to collect errors (43 ns for the small model);
  `Validator<T>` adds rule options, names, groups and child support for about 33 ns.
- `ValidateAsync` with only synchronous rules returns a completed task, so it cost 15 ns
  more than `Validate` here.
- `IfNotIPAddress` checks text 1.6 times (IPv4) and 5.2 times (IPv6) faster than
  `IPAddress.TryParse`, and allocates nothing.

These numbers come from one machine. Run the benchmarks on yours before you rely on them.

## What allocates

Guard chains on numbers, strings, dates, enums and in-memory collections allocate nothing
when the checks pass. These allocate by design:

- `IfNotXml` (the XML reader) and `IfNotUri` and `IfNotHttpUrl` (a `Uri` object);
- regular expression checks (`IfMatches`, `IfNotMatches`);
- `IfHasDuplicates` on a collection with two or more items (a `HashSet`);
- checks on lazy sequences or collections typed as an interface (an enumerator);
- `Uri.IfLoopback` for an IPv6 host;
- `params T[]` calls such as `IfNotOneOf(1, 2, 3)` with a compiler older than C# 13 (the
  array);
- on netstandard2.0, netstandard2.1 and net462 only: the enum checks,
  `IfPrecisionScaleExceeded` and the IP address checks (their bytes).

A failed check allocates its message and, in collect mode, the failure.

## Run the benchmarks

```shell
# Every benchmark (about 17 minutes; these numbers used --inProcess --launchCount 1 --warmupCount 8 --iterationCount 20)
dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*"

# Only the guard clauses, with a shorter job
dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*GuardBenchmarks*" --job short

# Check that every variant computes the same result, without measuring
dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --check
```
