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

.PARAMETER HangTimeout
Abort a test run that makes no progress for this long (default: 5m).

.PARAMETER RebuildImage
Rebuild the test image without the Docker cache.

.PARAMETER Platform
Docker platform for all legs, for example linux/amd64 (default: the platform of the
Docker server).

.PARAMETER Net9Amd64
Run the net9.0 leg as linux/amd64 through emulation. This is the default on Arm CPUs
with SME but no SVE (Apple M4 and later), where .NET 9 crashes natively.

.PARAMETER Net9Native
Run the net9.0 leg natively even on such CPUs, with the
DOTNET_INTERNAL_ThreadSuspendInjection=0 switch (may still crash).

.EXAMPLE
./eng/test-matrix.ps1 -Legs net8.0,netstandard2.0 -Filter "FullyQualifiedName~Guard"

.EXAMPLE
./eng/test-matrix.ps1 -Net9Native
#>
[CmdletBinding()]
param(
    [string[]] $Legs = @('net8.0', 'net9.0', 'net10.0', 'netstandard2.0', 'netstandard2.1'),
    [string] $Filter = '',
    [string] $HangTimeout = '5m',
    [switch] $RebuildImage,
    [string] $Platform = '',
    [switch] $Net9Amd64,
    [switch] $Net9Native
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

if ($Net9Amd64 -and $Net9Native) { throw 'Use -Net9Amd64 or -Net9Native, not both.' }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Initialize-Image([string] $ImagePlatform) {
    $image = "dotthrow-test:$($ImagePlatform -replace '/', '-')"
    $build = @('build', '--platform', $ImagePlatform, '--file', (Join-Path $repoRoot 'eng/docker/test.Dockerfile'), '--tag', $image)
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
}

# Each container writes its result rows to this folder. The last container prints the
# summary of all of them.
$resultsDir = Join-Path ([System.IO.Path]::GetTempPath()) "dotthrow-matrix-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $resultsDir | Out-Null
if (-not $IsWindows) { chmod 777 $resultsDir }

$ttyArgs = @()
if (-not [Console]::IsInputRedirected -and -not [Console]::IsOutputRedirected) { $ttyArgs = @('--interactive', '--tty') }

function Invoke-Group([string] $GroupPlatform, [string[]] $GroupLegs, [int] $Number, [bool] $Last) {
    $image = "dotthrow-test:$($GroupPlatform -replace '/', '-')"
    Write-Host "==> Running legs: $($GroupLegs -join ' ') (platform $GroupPlatform)"
    $run = @('run', '--rm', '--platform', $GroupPlatform) + $ttyArgs + @(
        '--volume', "${repoRoot}:/src:ro",
        '--volume', "${resultsDir}:/results",
        '--mount', 'type=volume,source=dotthrow-nuget,target=/nuget',
        '--env', "LEGS=$($GroupLegs -join ',')",
        '--env', "TEST_FILTER=$Filter",
        '--env', "HANG_TIMEOUT=$HangTimeout",
        '--env', "ROWS_FILE=/results/$Number.rows",
        '--env', "SUMMARY=$($Last.ToString().ToLowerInvariant())",
        '--env', "NET9_MODE=$net9Mode")
    # Not "return": the output must stream to the console, not into a variable.
    docker @run $image bash /src/eng/docker/run-tests.sh
    if ($LASTEXITCODE -ne 0) { $script:status = $LASTEXITCODE }
}

$script:status = 0
try {
    # How to run the net9.0 leg. .NET 9 crashes on Arm CPUs that have SME but no SVE (see
    # eng/docker/run-tests.sh), so there it runs as x64 through emulation unless
    # -Net9Native is given.
    Initialize-Image $Platform
    $net9Mode = ''
    $net9OnAmd64 = $false
    if ($Legs -contains 'net9.0') {
        if ($Platform -eq 'linux/amd64') { $net9Mode = 'x64 (-Platform linux/amd64)' }
        elseif ($Net9Amd64) { $net9OnAmd64 = $true; $net9Mode = 'x64 through emulation (-Net9Amd64)' }
        elseif ($Net9Native) { $net9Mode = 'native (-Net9Native)' }
        else {
            docker run --rm --platform $Platform "dotthrow-test:$($Platform -replace '/', '-')" bash -c 'grep -qw sme /proc/cpuinfo && ! grep -qw sve /proc/cpuinfo'
            if ($LASTEXITCODE -eq 0) {
                $net9OnAmd64 = $true
                $net9Mode = 'x64 through emulation (default: this CPU has SME but no SVE; -Net9Native to change)'
            }
            else { $net9Mode = 'native' }
        }
        Write-Host "==> net9.0 mode: $net9Mode"
    }

    # Group the legs by Docker platform. The emulated net9.0 leg runs in its own container
    # after the others.
    $nativeLegs = @($Legs | Where-Object { -not ($net9OnAmd64 -and $_ -eq 'net9.0') })
    $amd64Legs = @($Legs | Where-Object { $net9OnAmd64 -and $_ -eq 'net9.0' })
    if ($amd64Legs.Count -gt 0) { Initialize-Image 'linux/amd64' }
    if ($nativeLegs.Count -gt 0) { Invoke-Group $Platform $nativeLegs 1 ($amd64Legs.Count -eq 0) }
    if ($amd64Legs.Count -gt 0) { Invoke-Group 'linux/amd64' $amd64Legs 2 $true }
}
finally {
    Remove-Item -Recurse -Force $resultsDir -ErrorAction SilentlyContinue
}
exit $script:status
