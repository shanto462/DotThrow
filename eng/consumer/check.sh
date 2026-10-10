#!/usr/bin/env bash
# Builds the consumer app in eng/consumer against packed DotThrow packages with the oldest
# .NET 8 SDK feature band (8.0.1xx, Roslyn 4.8), the way a user on the .NET 8 SDK does.
# The CI job "Consumer (.NET 8 SDK)" runs it after the pack job.
#
# Usage: eng/consumer/check.sh FEED_DIR
#   FEED_DIR  a folder with DotThrow, DotThrow.DependencyInjection and DotThrow.AspNetCore
#             .nupkg files of one version, for example the output of eng/pack.sh
#
# Checks:
#   - the app restores the three packages from FEED_DIR and builds
#   - the analyzers inside the DotThrow package load and report the diagnostics that
#     Seeded.cs provokes (DT0001 and DT0010)
#   - no analyzer load failure (CS8032, CS8034) and no analyzer crash (AD0001). A newer
#     Microsoft.CodeAnalysis reference in the analyzers would cause CS8032 here.
#
# Needs a .NET 8.0.1xx SDK (eng/consumer/global.json). Works with the bash 3.2 of macOS.

set -euo pipefail

expected="DT0001 DT0010"
forbidden="CS8032 CS8034 AD0001"

[ $# -eq 1 ] || {
  echo "usage: eng/consumer/check.sh FEED_DIR" >&2
  exit 2
}
feed="$(cd "$1" && pwd)"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

errors=0
error() {
  if [ "${GITHUB_ACTIONS:-}" = true ]; then echo "::error::$*"; else echo "error: $*" >&2; fi
  errors=$((errors + 1))
}

# Build in a copy outside the repository, so the repository's build settings do not apply
# and nothing is written into the working tree.
work="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/dotthrow-consumer.XXXXXX")"
trap 'rm -rf "$work"' EXIT
cp -R "$here/." "$work/"
rm -f "$work/check.sh"
mkdir -p "$work/feed"
cp "$feed"/*.nupkg "$work/feed/"

core="$(ls "$work"/feed/DotThrow.[0-9]*.nupkg 2> /dev/null | head -n 1 || true)"
[ -n "$core" ] || {
  error "no DotThrow.<version>.nupkg in $feed"
  exit 1
}
version="$(basename "$core" .nupkg)"
version="${version#DotThrow.}"

cd "$work"
sdk="$(dotnet --version)"
echo "==> Building the consumer app with .NET SDK $sdk against DotThrow $version"

status=0
dotnet build Consumer.csproj -c Release --nologo \
  "-p:DotThrowVersion=$version" \
  "-p:RestorePackagesPath=$work/packages" 2>&1 | tee build.log || status=$?

[ "$status" -eq 0 ] || error "the consumer app did not build with SDK $sdk"
case "$sdk" in
  8.0.1*) ;;
  *) error "expected a .NET 8.0.1xx SDK (eng/consumer/global.json), got $sdk" ;;
esac

rows=""
for id in $expected; do
  if grep -q "warning $id" build.log; then
    rows="$rows
| $id | expected | reported |"
  else
    error "the analyzers did not report $id for Seeded.cs, so they may not load with SDK $sdk"
    rows="$rows
| $id | expected | missing |"
  fi
done
for id in $forbidden; do
  if grep -q "$id" build.log; then
    error "$id was reported: $(grep -m 1 "$id" build.log)"
    rows="$rows
| $id | not allowed | reported |"
  else
    rows="$rows
| $id | not allowed | none |"
  fi
done

summary="| Diagnostic | Rule | Result |
| ---------- | ---- | ------ |$rows"
echo
echo "$summary"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "## Consumer build with .NET SDK $sdk (DotThrow $version)"
    echo
    echo "$summary"
  } >> "$GITHUB_STEP_SUMMARY"
fi

if [ "$errors" -gt 0 ]; then
  echo "$errors problem(s) found." >&2
  exit 1
fi
echo
echo "The packages work with .NET SDK $sdk."
