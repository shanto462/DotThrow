# Performance

DotThrow is built so that a check that passes costs almost nothing: the guard is a
`ref struct`, the checks measured here allocate nothing, and messages are built only when
a check fails. This
page shows what that means in numbers, next to hand-written code, the .NET built-in throw
helpers, and other libraries.

The benchmarks live in [benchmarks/DotThrow.Benchmarks](../benchmarks/DotThrow.Benchmarks)
and use [BenchmarkDotNet](https://benchmarkdotnet.org/).

## What is measured

**Guard clauses** ([GuardBenchmarks.cs](../benchmarks/DotThrow.Benchmarks/GuardBenchmarks.cs)).
Three checks, each written six ways: a hand-written `if` (the baseline), the .NET built-in
throw helpers, DotThrow, Ardalis.GuardClauses, CommunityToolkit.Diagnostics and Throw.

- An `int` in the range 0 to 150.
- A `string` that is not null, empty or white space, with at most 50 characters.
- A `List<string>` that is not null or empty, with at most 10 items.

Every input is valid, so nothing throws. One check takes about a nanosecond, which is
too short to time alone, so each benchmark checks 1,024 different inputs and reports the
time per input.

**Validation** ([ValidationBenchmarks.cs](../benchmarks/DotThrow.Benchmarks/ValidationBenchmarks.cs)).
A small model with four rules: name not blank and at most 50 characters, a valid email
address, age from 0 to 150, and 1 to 10 tags. It runs with valid input (no failures) and
with invalid input (one failure on each property, so 4 failures). FluentValidation is the
baseline. It uses `RuleLevelCascadeMode = CascadeMode.Stop`, which stops each rule at its
first failure like a DotThrow chain does, so both report the same 4 failures.

All inputs are created once in `[GlobalSetup]`, and the validators once in static fields,
outside the measured code.
`--check` runs every benchmark once and prints its result, to prove that all variants do
the same work (for example, 4,096 failures for 1,024 invalid inputs in every validator).

## Machine and versions

- Apple M4 Pro (12 cores), macOS 27.0.1
- .NET SDK 10.0.401, .NET 10.0.12, Arm64 RyuJIT
- BenchmarkDotNet 0.15.8, default job, in-process toolchain (`--inProcess`)
- DotThrow 2.0.0 (branch `feat/v2`), Ardalis.GuardClauses 5.0.0,
  CommunityToolkit.Diagnostics 8.4.2, FluentValidation 12.1.1, Throw 1.4.0
- Run on 2026-10-10

## Guard clauses

Mean time per check. "Allocated" is memory per check; `-` means none.

| Check | Library | Mean | Ratio | Allocated |
| --- | --- | ---: | ---: | ---: |
| Int in 0..150 | Hand-written `if` | 0.25 ns | 1.00 | - |
| | .NET built-in | 0.30 ns | 1.24 | - |
| | **DotThrow** | **0.61 ns** | **2.47** | **-** |
| | Ardalis.GuardClauses | 0.74 ns | 3.03 | - |
| | CommunityToolkit.Diagnostics | 0.25 ns | 1.01 | - |
| | Throw | 0.51 ns | 2.06 | - |
| String not blank, max 50 | Hand-written `if` | 0.59 ns | 1.00 | - |
| | .NET built-in | 0.62 ns | 1.04 | - |
| | **DotThrow** | **1.62 ns** | **2.73** | **-** |
| | Ardalis.GuardClauses | 2.86 ns | 4.81 | 24 B |
| | CommunityToolkit.Diagnostics | 0.59 ns | 0.99 | - |
| | Throw | 8.68 ns | 14.63 | 96 B |
| List not null or empty, max 10 | Hand-written `if` | 0.37 ns | 1.00 | - |
| | .NET built-in | 0.39 ns | 1.05 | - |
| | **DotThrow** | **1.90 ns** | **5.13** | **-** |
| | Ardalis.GuardClauses | 2.05 ns | 5.55 | - |
| | CommunityToolkit.Diagnostics | 0.36 ns | 0.98 | - |
| | Throw | 0.97 ns | 2.62 | - |

## Validation

Mean time and memory per validated object.

| Input | Library | Mean | Ratio | Allocated |
| --- | --- | ---: | ---: | ---: |
| Valid | FluentValidation | 159.85 ns | 1.00 | 704 B |
| | **DotThrow `Validator<T>`** | **53.82 ns** | **0.34** | **56 B** |
| | **DotThrow `ValidationReport`** | **43.94 ns** | **0.27** | **56 B** |
| Invalid, 4 failures | FluentValidation | 1,672.67 ns | 1.00 | 7,712 B |
| | **DotThrow `Validator<T>`** | **112.80 ns** | **0.07** | **704 B** |
| | **DotThrow `ValidationReport`** | **98.95 ns** | **0.06** | **752 B** |

## Reading the numbers

- Every guard library here checks a value in a few nanoseconds. The differences matter
  only in loops that run millions of times.
- A DotThrow guard chain costs 0.6 to 1.9 ns per check and allocates nothing. That is 2.5
  to 5 times a hand-written `if`. The .NET built-in helpers and CommunityToolkit.Diagnostics
  are as fast as the hand-written code; they are plain static methods without a chain.
- Ardalis.GuardClauses allocated 24 B and Throw allocated 96 B per check in the string
  case. DotThrow allocated nothing in every case.
- For validation, DotThrow's `Validator<T>` was about 3 times faster than FluentValidation
  on valid input and about 15 times faster on invalid input. Both DotThrow variants used
  10 to 13 times less memory.
- A `ValidationReport` costs 56 B even when everything passes: the report and its list.

These numbers come from one machine. Run the benchmarks on yours before you rely on them.

## Run the benchmarks

The numbers above were measured with `--inProcess`. Add it to these commands to run the
same way.

```shell
# Every benchmark (takes about 8 minutes)
dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*"

# Only the guard clauses, with a shorter job
dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*GuardBenchmarks*" --job short

# Check that every variant computes the same result, without measuring
dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --check
```
