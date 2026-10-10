#!/usr/bin/env bash
# Runs the DotThrow tests on several .NET versions inside Docker, so nothing has to be
# installed on the host. One container has the .NET 10 SDK plus the .NET 8 and .NET 9
# runtimes (eng/docker/test.Dockerfile). The repository is mounted read-only and copied
# inside the container, so host build output is never used or changed.
#
# Legs: net8.0, net9.0, net10.0, and the net8.0 tests against the netstandard2.0 and
# netstandard2.1 builds of the library (netstandard2.0, netstandard2.1).
# .NET Framework (net472, net481) runs only on Windows, in CI.
#
# Works with the bash 3.2 that ships with macOS.

set -euo pipefail

all_legs="net8.0,net9.0,net10.0,netstandard2.0,netstandard2.1"
volume=dotthrow-nuget

usage() {
  cat <<EOF
Usage: eng/test-matrix.sh [options]

Options:
  --legs LIST          Comma-separated legs to run (default: $all_legs)
  --filter EXPR        Passed to "dotnet test --filter", for example "FullyQualifiedName~Validator"
  --rebuild-image      Rebuild the test image without the Docker cache
  --platform PLATFORM  Docker platform, for example linux/amd64 to run x64 through emulation
                       (default: the platform of the Docker server)
  -h, --help           Show this help

Examples:
  eng/test-matrix.sh
  eng/test-matrix.sh --legs net8.0,netstandard2.0 --filter "FullyQualifiedName~Guard"
  eng/test-matrix.sh --platform linux/amd64
EOF
}

die() {
  echo "error: $*" >&2
  exit 2
}

legs="$all_legs"
filter=""
rebuild=false
platform=""

while [ $# -gt 0 ]; do
  case "$1" in
    --legs)
      [ $# -ge 2 ] || die "--legs needs a value"
      legs="$2"
      shift 2
      ;;
    --legs=*)
      legs="${1#*=}"
      shift
      ;;
    --filter)
      [ $# -ge 2 ] || die "--filter needs a value"
      filter="$2"
      shift 2
      ;;
    --filter=*)
      filter="${1#*=}"
      shift
      ;;
    --rebuild-image)
      rebuild=true
      shift
      ;;
    --platform)
      [ $# -ge 2 ] || die "--platform needs a value"
      platform="$2"
      shift 2
      ;;
    --platform=*)
      platform="${1#*=}"
      shift
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      die "unknown option: $1 (see --help)"
      ;;
  esac
done

legs="${legs// /}"
[ -n "$legs" ] || die "--legs is empty"
IFS=',' read -r -a leg_list <<< "$legs"
for leg in "${leg_list[@]}"; do
  case ",$all_legs," in
    *",$leg,"*) ;;
    *) die "unknown leg '$leg'. Valid legs: $all_legs" ;;
  esac
done

command -v docker > /dev/null 2>&1 || die "docker is not installed"
docker info > /dev/null 2>&1 || die "Docker is not running. Start Docker Desktop (or the Docker daemon) and try again."

if [ -z "$platform" ]; then
  platform="$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')"
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
image="dotthrow-test:${platform//\//-}"

build=(docker build --platform "$platform" --file "$repo_root/eng/docker/test.Dockerfile" --tag "$image")
if [ "$rebuild" = true ]; then
  echo "==> Rebuilding the test image $image"
  "${build[@]}" --pull --no-cache "$repo_root"
elif docker image inspect "$image" > /dev/null 2>&1; then
  echo "==> Using the test image $image (cached)"
  "${build[@]}" --quiet "$repo_root" > /dev/null
else
  echo "==> Building the test image $image (only the first run is slow)"
  "${build[@]}" "$repo_root"
fi

run=(docker run --rm --platform "$platform"
  --volume "$repo_root:/src:ro"
  --mount "type=volume,source=$volume,target=/nuget"
  --env "LEGS=$legs"
  --env "TEST_FILTER=$filter")
if [ -t 0 ] && [ -t 1 ]; then
  run+=(--interactive --tty)
fi

echo "==> Running legs: ${legs//,/ } (platform $platform)"
exec "${run[@]}" "$image" bash /src/eng/docker/run-tests.sh
