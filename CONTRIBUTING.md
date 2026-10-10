# Contributing

Thanks for your interest in improving DotThrow. Bug reports, feature ideas, and pull requests are all welcome.

By taking part in this project you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md). To report a security problem, follow [SECURITY.md](SECURITY.md) and do not open a public issue.

## Development setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). `global.json` asks for 10.0.100 or a later 10.0 SDK.
2. Install the [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) and [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) runtimes too, because the tests also run on `net8.0` and `net9.0`. On Windows the tests also run on `net472` and `net481`, which use the .NET Framework 4.8.1 that comes with Windows. If you do not want to install more runtimes, use the [Docker test matrix](#run-the-full-test-matrix-locally-with-docker) instead.
3. For the Native AOT check, install the native toolchain for your system:
   - Linux: `clang` and `zlib1g-dev` (for example `sudo apt-get install clang zlib1g-dev`).
   - macOS: the Xcode command line tools (`xcode-select --install`).
   - Windows: Visual Studio with the "Desktop development with C++" workload.
4. Clone the repository and build it:

   ```sh
   git clone https://github.com/shanto462/DotThrow.git
   cd DotThrow
   dotnet build DotThrow.slnx
   ```

## Repository layout

| Path | What it is |
| ---- | ---------- |
| `src/DotThrow` | The library. Targets `netstandard2.0`, `netstandard2.1`, `net462`, `net8.0`, `net9.0` and `net10.0`. `net462` builds on any OS through the `Microsoft.NETFramework.ReferenceAssemblies` package. |
| `tests/DotThrow.Tests` | xUnit v3 tests. Targets `net8.0`, `net9.0` and `net10.0`, plus `net472` and `net481` on Windows. |
| `samples/DotThrow.Samples` | A console app that shows the API. CI also publishes it with Native AOT. |
| `benchmarks/DotThrow.Benchmarks` | BenchmarkDotNet benchmarks. CI builds them but does not run them. Run them with `dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*"`. |
| `docs` | Documentation. |
| `eng` | Engineering scripts, for example the Docker test matrix (`test-matrix.sh`, `test-matrix.ps1` and `docker/`). |

Package versions live in `Directory.Packages.props` (central package management). Shared build settings live in `Directory.Build.props`. Warnings are errors in every project, so fix a warning instead of suppressing it.

## Checks to run before you open a pull request

CI runs these exact commands. Please run them locally first:

```sh
dotnet format DotThrow.slnx --verify-no-changes
dotnet test tests/DotThrow.Tests/DotThrow.Tests.csproj -c Release
dotnet pack src/DotThrow/DotThrow.csproj -c Release -o dist
dotnet build benchmarks/DotThrow.Benchmarks/DotThrow.Benchmarks.csproj -c Release
```

- If the format check fails, run `dotnet format DotThrow.slnx` to fix the files.
- `dotnet pack` also runs package validation, which catches broken packages and API changes between target frameworks. CI also checks that the package has a `lib/` folder for every target framework of the library.

CI also runs the `net8.0` tests against the `netstandard2.0` and `netstandard2.1` builds of the library, so the code paths that older platforms use are tested too:

```sh
dotnet test tests/DotThrow.Tests/DotThrow.Tests.csproj -c Release -f net8.0 -p:DotThrowAssetTfm=netstandard2.0
dotnet test tests/DotThrow.Tests/DotThrow.Tests.csproj -c Release -f net8.0 -p:DotThrowAssetTfm=netstandard2.1
```

CI also publishes the sample app with Native AOT and runs it. It fails on any trim or AOT warning. The sample project sets `<PublishAot>true</PublishAot>` itself. Do not pass `-p:PublishAot=true` on the command line: it would also apply to the `netstandard2.0` build of the library and fail. CI uses `linux-x64`; use the runtime identifier of your machine, for example `osx-arm64` or `win-x64`:

```sh
dotnet publish samples/DotThrow.Samples/DotThrow.Samples.csproj -c Release -r linux-x64 -warnaserror -o aot
./aot/DotThrow.Samples
```

To see test coverage, run the tests with the coverage collector. CI does the same on Linux and merges the results with [ReportGenerator](https://github.com/danielpalme/ReportGenerator):

```sh
dotnet test tests/DotThrow.Tests/DotThrow.Tests.csproj -c Release --collect "XPlat Code Coverage" --results-directory coverage
```

## Run the full test matrix locally with Docker

You do not need the .NET 8 and .NET 9 runtimes on your machine to run every test leg. With [Docker](https://docs.docker.com/get-started/get-docker/) running (Linux containers), one command runs the tests on `net8.0`, `net9.0` and `net10.0`, plus the `net8.0` tests against the `netstandard2.0` and `netstandard2.1` builds of the library:

```sh
eng/test-matrix.sh                # macOS and Linux
pwsh eng/test-matrix.ps1          # Windows (or any OS with PowerShell 7)
```

It builds a test image with the .NET 10 SDK and the .NET 8 and .NET 9 runtimes (`eng/docker/test.Dockerfile`), mounts the repository read-only, copies it inside the container without `bin` and `obj`, and runs each leg in Release. Your own `bin` and `obj` folders are never used or changed. NuGet packages are cached in the Docker volume `dotthrow-nuget`, so only the first run downloads them. At the end it prints a table with the passed, failed and skipped tests of each leg, and it exits with an error if a leg failed. A leg shows `N/A` when the projects do not support it yet.

Options:

| Bash | PowerShell | What it does |
| ---- | ---------- | ------------ |
| `--legs net8.0,netstandard2.0` | `-Legs net8.0,netstandard2.0` | Run only these legs. |
| `--filter "FullyQualifiedName~Validator"` | `-Filter "FullyQualifiedName~Validator"` | Passed to `dotnet test --filter`. |
| `--rebuild-image` | `-RebuildImage` | Rebuild the test image without the Docker cache. |
| `--platform linux/amd64` | `-Platform linux/amd64` | Run x64 Linux through emulation, for example on an Apple silicon Mac. |

The .NET Framework legs (`net472` and `net481`) run only on Windows, so the Docker matrix does not cover them. CI runs them on `windows-latest`. To remove the image and the cache later, run `docker image rm dotthrow-test:linux-arm64` (or `linux-amd64`) and `docker volume rm dotthrow-nuget`.

On Arm CPUs with SME (Apple M4 and later), .NET 9 inside a Linux VM can crash at random with an illegal instruction (exit code 132). This is a .NET 9 runtime bug, not a DotThrow bug. The script then sets `DOTNET_INTERNAL_ThreadSuspendInjection=0` for the `net9.0` leg, which avoids it, and says so in the summary.

## Commit messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/). Start each commit message, and the pull request title, with a type:

- `feat:` a new feature, for example `feat: add IfNotIpAddress`
- `fix:` a bug fix, for example `fix: report the right count in IfCountEqualTo`
- `perf:` a performance improvement
- `refactor:` a code change with no behavior change
- `test:` tests only
- `docs:` documentation only
- `ci:` CI workflows
- `deps:` dependency updates
- `chore:` anything else

Add `!` after the type for a breaking change, for example `feat!: rename IfCountOutOfRange`.

## Pull request flow

1. Fork the repository and create a branch from `main`, for example `fix/count-message`.
2. Make your change. Add or update tests for it, and add XML docs to new public APIs.
3. If users will notice the change, add a line to `CHANGELOG.md` under `## [Unreleased]`.
4. Run the checks above.
5. Open a pull request to `main` and fill in the template.
6. Wait for review. The maintainer may ask for changes.

## Branch rules

- `main` is protected. All changes go through a pull request.
- A pull request can merge only when the **`CI result`** status check is green. This one check passes only when every CI job passes: format, tests on Linux, macOS and Windows, tests against the `netstandard2.0` and `netstandard2.1` builds, pack with package validation, Native AOT, coverage, and the benchmarks build. CodeQL must also report no new problems.
- Pull requests are merged with squash or rebase only, so the history of `main` stays linear. There are no merge commits.
- Keep pull requests small and focused. One topic per pull request is easier to review.

## Releases

Releases are made by the maintainer:

1. Set the new version in `<Version>` in `src/DotThrow/DotThrow.csproj`.
2. Move the notes under `## [Unreleased]` in `CHANGELOG.md` to a new `## [x.y.z] - YYYY-MM-DD` section, and update the links at the bottom of the file.
3. Merge these changes to `main` through a pull request.
4. Tag the merged commit with `vx.y.z` and push the tag.

The release workflow then checks that the tag matches the project version, runs the tests, packs the package, writes `SHA256SUMS`, and creates a signed build provenance attestation. It pushes the package to NuGet.org with [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), so no long-lived API key is stored anywhere, and creates the GitHub Release with the notes from `CHANGELOG.md`. A version with a dash, for example `2.1.0-preview.1`, is published as a prerelease.

A pull request that changes the release workflow or the build inputs, and a manual run of the workflow, do a dry run: they test and pack but publish nothing.
