# Test image for eng/test-matrix.sh: the .NET 10 SDK plus the .NET 8 and .NET 9
# runtimes in one container, so the tests can run on net8.0, net9.0 and net10.0
# without installing anything on the host.
#
# Every image is pinned by the digest of its multi-arch index (linux/amd64 and
# linux/arm64). Dependabot updates the digests (see .github/dependabot.yml).
# The aspnet images are used for the older runtimes because they contain both
# Microsoft.NETCore.App and Microsoft.AspNetCore.App.

# aspnet:8.0 is .NET 8.0.31 on 2026-10-10
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:1601380775673021a23ea5985669f1daf03719faaa684f839da1752fb48cfc7f AS dotnet8

# aspnet:9.0 is .NET 9.0.20 on 2026-10-10
FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:4d94cf3204df98d321e8dbf8d6d06e0b34fd136aa75c945abf4390ee07eb30dd AS dotnet9

# sdk:10.0 is SDK 10.0.401 with .NET 10.0.12 on 2026-10-10
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa

# Add the shared frameworks next to the ones of the SDK. The dotnet host of the
# SDK image runs apps on any of them.
COPY --from=dotnet8 /usr/share/dotnet/shared/ /usr/share/dotnet/shared/
COPY --from=dotnet9 /usr/share/dotnet/shared/ /usr/share/dotnet/shared/

ENV DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
    DOTNET_CLI_UI_LANGUAGE=en \
    DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=true \
    NUGET_PACKAGES=/nuget/packages

# Run as the image's non-root "app" user. /nuget is the mount point of the NuGet
# cache volume; a new volume takes its owner from this folder.
RUN mkdir -p /work /nuget/packages \
    && chown -R app:app /work /nuget \
    && dotnet --list-runtimes

USER app
WORKDIR /work
