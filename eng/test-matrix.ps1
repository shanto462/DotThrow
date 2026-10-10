#!/usr/bin/env pwsh
<#
.SYNOPSIS
Runs the DotThrow tests on several .NET versions inside Docker. It does the same as
eng/test-matrix.sh and needs Docker with Linux containers.

.DESCRIPTION
One container has the .NET 10 SDK plus the .NET 8 and .NET 9 runtimes
(eng/docker/test.Dockerfile). The repository is mounted read-only and copied inside the
container, so host build output is never used or changed. .NET Framework (net472,
net481) runs only on Windows, in CI.

.PARAMETER Legs
Legs to run: net8.0, net9.0, net10.0, netstandard2.0, netstandard2.1 (default: all).

.PARAMETER Filter
Passed to "dotnet test --filter", for example "FullyQualifiedName~Validator".

.PARAMETER RebuildImage
Rebuild the test image without the Docker cache.

.PARAMETER Platform
Docker platform, for example linux/amd64 (default: the platform of the Docker server).

.EXAMPLE
./eng/test-matrix.ps1 -Legs net8.0,netstandard2.0 -Filter "FullyQualifiedName~Guard"
#>
[CmdletBinding()]
param(
    [string[]] $Legs = @('net8.0', 'net9.0', 'net10.0', 'netstandard2.0', 'netstandard2.1'),
    [string] $Filter = '',
    [switch] $RebuildImage,
    [string] $Platform = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$allLegs = @('net8.0', 'net9.0', 'net10.0', 'netstandard2.0', 'netstandard2.1')
$Legs = @($Legs | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($Legs.Count -eq 0) { throw '-Legs is empty' }
foreach ($leg in $Legs) {
    if ($allLegs -notcontains $leg) { throw "Unknown leg '$leg'. Valid legs: $($allLegs -join ',')" }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'docker is not installed' }
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker is not running. Start Docker Desktop (Linux containers) and try again.' }

if (-not $Platform) { $Platform = (docker version --format '{{.Server.Os}}/{{.Server.Arch}}').Trim() }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$image = "dotthrow-test:$($Platform -replace '/', '-')"
$build = @('build', '--platform', $Platform, '--file', (Join-Path $repoRoot 'eng/docker/test.Dockerfile'), '--tag', $image)

if ($RebuildImage) {
    Write-Host "==> Rebuilding the test image $image"
    docker @build --pull --no-cache $repoRoot
}
else {
    docker image inspect $image *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "==> Using the test image $image (cached)"
        docker @build --quiet $repoRoot | Out-Null
    }
    else {
        Write-Host "==> Building the test image $image (only the first run is slow)"
        docker @build $repoRoot
    }
}
if ($LASTEXITCODE -ne 0) { throw 'docker build failed' }

$legList = $Legs -join ','
$run = @('run', '--rm', '--platform', $Platform,
    '--volume', "${repoRoot}:/src:ro",
    '--mount', 'type=volume,source=dotthrow-nuget,target=/nuget',
    '--env', "LEGS=$legList",
    '--env', "TEST_FILTER=$Filter")
if (-not [Console]::IsInputRedirected -and -not [Console]::IsOutputRedirected) {
    $run += @('--interactive', '--tty')
}

Write-Host "==> Running legs: $($Legs -join ' ') (platform $Platform)"
docker @run $image bash /src/eng/docker/run-tests.sh
exit $LASTEXITCODE
