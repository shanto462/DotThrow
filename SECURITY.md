# Security Policy

## Supported versions

Only the latest 2.x release gets security fixes.

| Version | Supported |
| ------- | --------- |
| 2.x     | Yes       |
| 1.x     | No        |

## Reporting a vulnerability

Please do not report security vulnerabilities in public issues, pull requests, or discussions.

Report them privately with GitHub private vulnerability reporting:

https://github.com/shanto462/DotThrow/security/advisories/new

Please include:

- The affected DotThrow version and the target framework of your project.
- What the problem is and what an attacker could do with it.
- Steps to reproduce, for example a minimal code sample and the input that triggers the problem.

## What to expect

- I will confirm that I received your report within 7 days.
- I will keep you updated while I investigate and work on a fix.
- When a fix is released, I will publish a GitHub security advisory and credit you, unless you ask me not to.

## Verifying packages

All packages are built and published only by the release workflow in this repository. It pushes to NuGet.org with trusted publishing, so there is no long-lived NuGet API key that could leak.

Every [GitHub Release](https://github.com/shanto462/DotThrow/releases) includes the `.nupkg` and `.snupkg` of every package (`DotThrow` and its companion packages such as `DotThrow.DependencyInjection`), a `SHA256SUMS` file, and a signed build provenance attestation. To check a package you downloaded from the GitHub Release, use the GitHub CLI. It works the same for every package:

```sh
gh attestation verify DotThrow.2.0.0.nupkg --repo shanto462/DotThrow
```

To check the files against `SHA256SUMS`, put them in the same folder and run:

```sh
sha256sum --check SHA256SUMS        # Linux
shasum -a 256 --check SHA256SUMS    # macOS
```

NuGet.org adds its own repository signature to every package it serves, so a package downloaded from NuGet.org has a different hash than the file on the GitHub Release. To check a package from NuGet.org, verify its signatures instead:

```sh
dotnet nuget verify --all DotThrow.2.0.0.nupkg
```
