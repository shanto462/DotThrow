## Summary

<!-- What does this pull request change, and why? Link the issue if there is one, for example "Closes #12". -->

## Type of change

- [ ] Bug fix
- [ ] New check or feature
- [ ] Performance improvement
- [ ] Refactor (no behavior change)
- [ ] Documentation
- [ ] CI, build, or dependencies

## How I tested it

<!-- For example: "dotnet test tests/DotThrow.Tests/DotThrow.Tests.csproj -c Release", plus a new test for the case in the issue. -->

## Checklist

- [ ] The PR title follows [Conventional Commits](https://www.conventionalcommits.org/) (for example `feat: add IfNotIpAddress`).
- [ ] `dotnet format DotThrow.slnx --verify-no-changes` passes.
- [ ] `dotnet test tests/DotThrow.Tests/DotThrow.Tests.csproj -c Release` passes (net8.0, net9.0 and net10.0, plus net472 and net481 on Windows).
- [ ] `dotnet test tests/DotThrow.Tests/DotThrow.Tests.csproj -c Release -f net8.0 -p:DotThrowAssetTfm=netstandard2.0` passes, and the same with `netstandard2.1`.
- [ ] `dotnet pack src/DotThrow/DotThrow.csproj -c Release -o dist` passes (this runs package validation).
- [ ] `dotnet publish samples/DotThrow.Samples/DotThrow.Samples.csproj -c Release -r <your-rid> -warnaserror -o aot` passes with no trim or AOT warnings, and `./aot/DotThrow.Samples` runs (CI uses `linux-x64`).
- [ ] `dotnet build benchmarks/DotThrow.Benchmarks/DotThrow.Benchmarks.csproj -c Release` passes.
- [ ] I added or updated tests for the change.
- [ ] New public APIs have XML docs.
- [ ] I updated `CHANGELOG.md` under `## [Unreleased]` if users will notice the change.
