#!/usr/bin/env bash
# Runs the DotThrow tests on several .NET versions inside Docker, so nothing has to be
# installed on the host. One container has the .NET 10 SDK plus the .NET 8 and .NET 9
# runtimes (eng/docker/test.Dockerfile). The repository is mounted read-only and copied
# inside the container, so host build output is never used or changed.
#
# Legs: net8.0, net9.0 and net10.0 run every test project under tests/ that targets the
# framework. netstandard2.0 and netstandard2.1 run the net8.0 core tests against that
# build of the library. .NET Framework (net472, net481) runs only on Windows, in CI.
#
# Works with the bash 3.2 that ships with macOS.

set -euo pipefail

all_legs="net8.0,net9.0,net10.0,netstandard2.0,netstandard2.1"
volume=dotthrow-nuget

usage() {
  cat <<EOF
Usage: eng/test-matrix.sh [options]

Options:
  --legs LIST           Comma-separated legs to run (default: $all_legs)
  --filter EXPR         Passed to "dotnet test --filter", for example "FullyQualifiedName~Validator"
  --hang-timeout TIME   Abort a test run that makes no progress for this long (default: 5m)
  --rebuild-image       Rebuild the test image without the Docker cache
  --platform PLATFORM   Docker platform for all legs, for example linux/amd64 to run x64
                        through emulation (default: the platform of the Docker server)
  --net9-amd64          Run the net9.0 leg as linux/amd64 through emulation. This is the
                        default on Arm CPUs with SME but no SVE (Apple M4 and later), where
                        .NET 9 crashes natively.
  --net9-native         Run the net9.0 leg natively even on such CPUs, with the
                        DOTNET_INTERNAL_ThreadSuspendInjection=0 switch (may still crash)
  -h, --help            Show this help

Examples:
  eng/test-matrix.sh
  eng/test-matrix.sh --legs net8.0,netstandard2.0 --filter "FullyQualifiedName~Guard"
  eng/test-matrix.sh --net9-native
EOF
}

die() {
  echo "error: $*" >&2
  exit 2
}

legs="$all_legs"
filter=""
hang_timeout=5m
rebuild=false
platform=""
net9=auto

while [ $# -gt 0 ]; do
  case "$1" in
    --legs | --filter | --hang-timeout | --platform)
      [ $# -ge 2 ] || die "$1 needs a value"
      case "$1" in
        --legs) legs="$2" ;;
        --filter) filter="$2" ;;
        --hang-timeout) hang_timeout="$2" ;;
        --platform) platform="$2" ;;
      esac
      shift 2
      ;;
    --legs=*) legs="${1#*=}" && shift ;;
    --filter=*) filter="${1#*=}" && shift ;;
    --hang-timeout=*) hang_timeout="${1#*=}" && shift ;;
    --platform=*) platform="${1#*=}" && shift ;;
    --rebuild-image) rebuild=true && shift ;;
    --net9-amd64) net9=amd64 && shift ;;
    --net9-native) net9=native && shift ;;
    -h | --help)
      usage
      exit 0
      ;;
    *) die "unknown option: $1 (see --help)" ;;
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

ensure_image() {
  local image="dotthrow-test:${1//\//-}"
  local build=(docker build --platform "$1" --file "$repo_root/eng/docker/test.Dockerfile" --tag "$image")
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
}

# Each container writes its result rows to this folder. The last container prints the
# summary of all of them. The container user is not the host user, so the folder must be
# writable for everyone.
results_dir="$(mktemp -d "${TMPDIR:-/tmp}/dotthrow-matrix.XXXXXX")"
chmod 777 "$results_dir"
trap 'rm -rf "$results_dir"' EXIT

tty_args=()
if [ -t 0 ] && [ -t 1 ]; then
  tty_args=(--interactive --tty)
fi

# Runs one container. Arguments: platform, legs, group number, "true" if it is the last.
run_group() {
  local image="dotthrow-test:${1//\//-}"
  echo "==> Running legs: ${2//,/ } (platform $1)"
  docker run --rm --platform "$1" ${tty_args[@]+"${tty_args[@]}"} \
    --volume "$repo_root:/src:ro" \
    --volume "$results_dir:/results" \
    --mount "type=volume,source=$volume,target=/nuget" \
    --env "LEGS=$2" \
    --env "TEST_FILTER=$filter" \
    --env "HANG_TIMEOUT=$hang_timeout" \
    --env "ROWS_FILE=/results/$3.rows" \
    --env "SUMMARY=$4" \
    --env "NET9_MODE=$net9_mode" \
    "$image" bash /src/eng/docker/run-tests.sh
}

# How to run the net9.0 leg. .NET 9 crashes on Arm CPUs that have SME but no SVE (see
# the comment in eng/docker/run-tests.sh), so there it runs as x64 through emulation
# unless --net9-native is given.
case ",$legs," in
  *,net9.0,*) has_net9=true ;;
  *) has_net9=false ;;
esac
ensure_image "$platform"
net9_mode=""
net9_amd64=false
if [ "$has_net9" = true ]; then
  if [ "$platform" = linux/amd64 ]; then
    net9_mode="x64 (--platform linux/amd64)"
  elif [ "$net9" = amd64 ]; then
    net9_amd64=true
    net9_mode="x64 through emulation (--net9-amd64)"
  elif [ "$net9" = native ]; then
    net9_mode="native (--net9-native)"
  elif docker run --rm --platform "$platform" "dotthrow-test:${platform//\//-}" \
    bash -c 'grep -qw sme /proc/cpuinfo && ! grep -qw sve /proc/cpuinfo'; then
    net9_amd64=true
    net9_mode="x64 through emulation (default: this CPU has SME but no SVE; --net9-native to change)"
  else
    net9_mode="native"
  fi
  echo "==> net9.0 mode: $net9_mode"
fi

# Group the legs by Docker platform. The emulated net9.0 leg runs in its own container
# after the others.
native_legs=""
amd64_legs=""
for leg in "${leg_list[@]}"; do
  if [ "$leg" = net9.0 ] && [ "$net9_amd64" = true ]; then
    amd64_legs="net9.0"
  else
    native_legs="${native_legs:+$native_legs,}$leg"
  fi
done

status=0
if [ -n "$amd64_legs" ]; then
  ensure_image linux/amd64
fi
if [ -n "$native_legs" ]; then
  last=true
  [ -z "$amd64_legs" ] || last=false
  run_group "$platform" "$native_legs" 1 "$last" || status=$?
fi
if [ -n "$amd64_legs" ]; then
  run_group linux/amd64 "$amd64_legs" 2 true || status=$?
fi
exit "$status"
