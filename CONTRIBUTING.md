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
| `src/DotThrow` | The core library. Targets `netstandard2.0`, `netstandard2.1`, `net462`, `net8.0`, `net9.0` and `net10.0`, with no package dependencies. `net462` builds on any OS through the `Microsoft.NETFramework.ReferenceAssemblies` package. |
| `src/DotThrow.DependencyInjection`, `src/DotThrow.AspNetCore` | Companion packages. They ship with the same version as `DotThrow`. |
| `src/DotThrow.Analyzers` | Roslyn analyzers. They ship inside the `DotThrow` package, not as a package of their own. |
| `tests/*` | xUnit v3 tests, one project per package. The core tests target `net8.0`, `net9.0` and `net10.0`, plus `net472` and `net481` on Windows. |
| `samples/DotThrow.Samples` | A console app that shows the API. CI also publishes it with Native AOT. |
| `benchmarks/DotThrow.Benchmarks` | BenchmarkDotNet benchmarks. CI builds them but does not run them. Run them with `dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*"`. |
| `docs` | Documentation. |
| `eng` | Engineering scripts, for example the Docker test matrix (`test-matrix.sh`, `test-matrix.ps1` and `docker/`). |

Package versions live in `Directory.Packages.props` (central package management). Shared build settings live in `Directory.Build.props`. Warnings are errors in every project, so fix a warning instead of suppressing it.

## Checks to run before you open a pull request

CI runs these exact commands. Please run them locally first:

```sh
dotnet format DotThrow.slnx --verify-no-changes
for project in tests/*/*.csproj; do dotnet test "$project" -c Release; done
bash eng/pack.sh dist
dotnet build benchmarks/DotThrow.Benchmarks/DotThrow.Benchmarks.csproj -c Release
```

- If the format check fails, run `dotnet format DotThrow.slnx` to fix the files.
- Always test in Release (`-c Release`). The zero-allocation tests skip in a Debug build.
- In PowerShell, run the tests with `Get-ChildItem tests/*/*.csproj | ForEach-Object { dotnet test $_ -c Release }`. Run `eng/pack.sh` in Git Bash or WSL.
- `eng/pack.sh` packs every packable project under `src/`, which also runs package validation. Then it checks every package: a `lib/` folder for each target framework, a symbol package, one shared version, and no dependencies for the core `DotThrow` package. Read the comment at the top of the script for the details.

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

The same job publishes `eng/aot-smoke`, a minimal API that uses `DotThrow.AspNetCore` and `DotThrow.DependencyInjection` (a validator class, options validation, and dynamic rules with a `PropertyMap`), and sends it valid and invalid requests:

```sh
dotnet publish eng/aot-smoke/AotSmoke.csproj -c Release -r linux-x64 -warnaserror -o aot-web
bash eng/aot-smoke/smoke.sh aot-web/AotSmoke
```

The CI job "Consumer (.NET 8 SDK)" builds `eng/consumer`, a small app that uses the three packages from the pack job, with the oldest .NET 8 SDK feature band (8.0.1xx, Roslyn 4.8). It checks that the analyzers inside the `DotThrow` package load there and report the mistakes seeded in `eng/consumer/Seeded.cs`, with no CS8032, CS8034 or AD0001. With a .NET 8.0.1xx SDK installed, run it after `eng/pack.sh`:

```sh
bash eng/consumer/check.sh dist
```

To see test coverage, run the tests with the coverage collector. CI does the same on Linux and merges the results with [ReportGenerator](https://github.com/danielpalme/ReportGenerator):

```sh
for project in tests/*/*.csproj; do dotnet test "$project" -c Release --collect "XPlat Code Coverage" --results-directory coverage; done
```

## Run the full test matrix locally with Docker

You do not need the .NET 8 and .NET 9 runtimes on your machine to run every test leg. With [Docker](https://docs.docker.com/get-started/get-docker/) running (Linux containers), one command runs every test project under `tests/` on `net8.0`, `net9.0` and `net10.0`, plus the `net8.0` core tests against the `netstandard2.0` and `netstandard2.1` builds of the library:

```sh
eng/test-matrix.sh                # macOS and Linux
pwsh eng/test-matrix.ps1          # Windows (or any OS with PowerShell 7)
```

It builds a test image with the .NET 10 SDK and the .NET 8 and .NET 9 runtimes (`eng/docker/test.Dockerfile`), mounts the repository read-only, copies it inside the container without `bin` and `obj`, and runs each leg in Release. Your own `bin` and `obj` folders are never used or changed. NuGet packages are cached in the Docker volume `dotthrow-nuget`, so only the first run downloads them. At the end it prints one table with the passed, failed and skipped tests of each leg and project, and it exits with an error if a test run failed. A leg shows `N/A` when the projects do not support it yet. A test run that makes no progress for 5 minutes is stopped and counts as failed.

Options:

| Bash | PowerShell | What it does |
| ---- | ---------- | ------------ |
| `--legs net8.0,netstandard2.0` | `-Legs net8.0,netstandard2.0` | Run only these legs. |
| `--filter "FullyQualifiedName~Validator"` | `-Filter "FullyQualifiedName~Validator"` | Passed to `dotnet test --filter`. |
| `--hang-timeout 10m` | `-HangTimeout 10m` | Stop a test run after this long without progress (default `5m`). |
| `--rebuild-image` | `-RebuildImage` | Rebuild the test image without the Docker cache. |
| `--platform linux/amd64` | `-Platform linux/amd64` | Run every leg as x64 Linux through emulation, for example on an Apple silicon Mac. |
| `--net9-amd64` | `-Net9Amd64` | Run the `net9.0` leg as x64 Linux through emulation. This is already the default on CPUs with the .NET 9 problem below. |
| `--net9-native` | `-Net9Native` | Run the `net9.0` leg natively even on those CPUs. See below. |

The .NET Framework legs (`net472` and `net481`) run only on Windows, so the Docker matrix does not cover them. CI runs them on `windows-latest`. To remove the images and the cache later, run `docker image rm dotthrow-test:linux-arm64 dotthrow-test:linux-amd64` and `docker volume rm dotthrow-nuget`.

### .NET 9 on Apple M4 and other Arm CPUs with SME

Some Linux VMs on Arm CPUs with SME but no SVE (for example OrbStack or Docker Desktop on an Apple M4) make .NET 9 crash with an illegal instruction (SIGILL, exit code 132). While .NET 9 reads a signal frame it runs `rdvl`, an SVE instruction. This is a .NET 9 runtime bug, not a DotThrow bug. It is fixed in .NET 10.0.9 and later, a fix for .NET 9 is unlikely, and GitHub-hosted runners are not affected. Details: [dotnet/runtime#122608](https://github.com/dotnet/runtime/issues/122608), [dotnet/runtime#127398](https://github.com/dotnet/runtime/pull/127398), [dotnet/runtime#127518](https://github.com/dotnet/runtime/pull/127518), [dotnet/runtime#133030](https://github.com/dotnet/runtime/issues/133030) and [orbstack/orbstack#2670](https://github.com/orbstack/orbstack/issues/2670).

So on such a CPU the script runs the `net9.0` leg as x64 through emulation by default. It is slower, but the results are correct. The script finds out with a quick check inside the container, and the summary says which mode ran and why.

To try the leg natively anyway, pass `--net9-native`. The script then sets `DOTNET_INTERNAL_ThreadSuspendInjection=0`, which removes the most common trigger (the signal the GC uses to suspend threads). It does not cover other signals, for example a caught `NullReferenceException`, so some tests still crash, and a thread in a tight loop can block the GC (the hang timeout stops such a run).

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
- A pull request can merge only when the **`CI result`** status check is green. This one check passes only when every CI job passes: format, tests on Linux, macOS and Windows, tests against the `netstandard2.0` and `netstandard2.1` builds, pack with package validation and package checks, the .NET 8 SDK consumer build, Native AOT (sample app and ASP.NET Core app), coverage, and the benchmarks build. CodeQL must also report no new problems.
- Pull requests are merged with squash or rebase only, so the history of `main` stays linear. There are no merge commits.
- Keep pull requests small and focused. One topic per pull request is easier to review.

## Releases

Releases are made by the maintainer:

1. Set the new version in `<VersionPrefix>` in `Directory.Build.props`. All packages share this one version. For a prerelease, also set `<VersionSuffix>`, for example `preview.1`.
2. Move the notes under `## [Unreleased]` in `CHANGELOG.md` to a new `## [x.y.z] - YYYY-MM-DD` section, and update the links at the bottom of the file.
3. Merge these changes to `main` through a pull request.
4. Tag the merged commit with `vx.y.z` and push the tag.

The release workflow then checks that the tag matches the version, runs every test project, packs every package with `eng/pack.sh`, writes `SHA256SUMS` for all files, and creates a signed build provenance attestation for them. It pushes the packages to NuGet.org (the core `DotThrow` package first) with [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), so no long-lived API key is stored anywhere, creates the GitHub Release with the notes from `CHANGELOG.md`, and copies the packages to GitHub Packages (`github-packages.yml`), so they are listed on the repository page. NuGet.org stays the place to install from. To copy the packages of an older release, run that workflow by hand with its tag. A version with a dash, for example `2.1.0-preview.1`, is published as a prerelease.

A stable tag (no dash) must point to a commit on `main`; the workflow fails otherwise. A prerelease tag can point to a commit on any branch, so a release candidate can be published from a feature branch before it is merged. Only admins can create `v*` tags (tag ruleset).

A pull request that changes the release workflow or the build inputs, and a manual run of the workflow, do a dry run: they test and pack but publish nothing.

### Rehearse a release with a prerelease

Before the first stable release, and whenever a new package joins the set, rehearse the whole flow with a release candidate. Publishing a new package ID with trusted publishing is the step most likely to fail, and a prerelease does not reach users who install stable versions.

1. Next to `<VersionPrefix>` in `Directory.Build.props`, add `<VersionSuffix>rc.1</VersionSuffix>`. The version is then `2.0.0-rc.1` for every package.
2. Add a `## [2.0.0-rc.1] - YYYY-MM-DD` section to `CHANGELOG.md`. One line is enough, but the release fails without it.
3. Tag the commit `v2.0.0-rc.1` and push the tag. A prerelease tag does not have to be on `main`, so this works from the pull request branch too.
4. Check the result:
   - On NuGet.org, every package has version `2.0.0-rc.1`, marked as prerelease. A version with a dash is always a prerelease on NuGet.
   - Every package has its symbols. `dotnet nuget push` sends the `.snupkg` next to each `.nupkg` on its own, and the job summary lists every pushed package.
   - The GitHub Release `v2.0.0-rc.1` is marked as a pre-release (the workflow passes `--prerelease` for a version with a dash) and has every `.nupkg`, `.snupkg` and `SHA256SUMS`.
   - `gh attestation verify` works for a package from the GitHub Release (see [SECURITY.md](SECURITY.md)).
5. Remove `<VersionSuffix>` again in the next commit, so the branch builds the stable version, and keep the `2.0.0-rc.1` section in `CHANGELOG.md`. For another candidate, set `rc.2` instead.
