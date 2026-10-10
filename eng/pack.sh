#!/usr/bin/env bash
# Packs every packable project under src/ in Release and checks the packages.
# The CI pack job and the release workflow run it. You can run it locally too:
#
#   eng/pack.sh dist
#   eng/pack.sh dist -p:ContinuousIntegrationBuild=true
#
# Usage: eng/pack.sh OUTPUT_DIR [extra "dotnet pack" arguments...]
#
# Environment:
#   EXPECTED_VERSION  fail unless every package has this version (the release tag)
#   SKIP_PACK         "true" only checks the packages already in OUTPUT_DIR (no pack)
#
# Checks:
#   - every packable project has a .nupkg, and a .snupkg when IncludeSymbols is true
#   - every package has lib/<tfm>/<AssemblyName>.dll for each target framework
#   - all packages share one version (lockstep, set in Directory.Build.props)
#   - the core DotThrow package has no dependencies (decision D21)
#   - the core package has exactly the analyzer files below in analyzers/, and no other
#     package has an analyzers/ folder
#
# Works with the bash 3.2 that ships with macOS.

set -euo pipefail

core_id=DotThrow
# Every file the core package must have under analyzers/, and nothing else.
core_analyzer_files="analyzers/dotnet/cs/DotThrow.Analyzers.CodeFixes.dll
analyzers/dotnet/cs/DotThrow.Analyzers.dll"

die() {
  echo "error: $*" >&2
  exit 2
}

errors=0
error() {
  if [ "${GITHUB_ACTIONS:-}" = true ]; then echo "::error::$*"; else echo "error: $*" >&2; fi
  errors=$((errors + 1))
}

[ $# -ge 1 ] || die "usage: eng/pack.sh OUTPUT_DIR [dotnet pack arguments...]"
out="$1"
shift

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$out"
out="$(cd "$out" && pwd)"
cd "$repo_root"

# Reads one property from the JSON that "dotnet msbuild -getProperty:A -getProperty:B" prints.
prop() {
  sed -n "s/^ *\"$1\": \"\(.*\)\",\{0,1\}\$/\1/p" <<< "$2"
}

# Find the packable projects. Bash 3.2 has no associative arrays, so use parallel arrays.
projects=()
ids=()
assemblies=()
frameworks=()
versions=()
symbols=()
for project in src/*/*.csproj; do
  [ -f "$project" ] || continue
  json="$(dotnet msbuild "$project" \
    -getProperty:IsPackable -getProperty:PackageId -getProperty:AssemblyName \
    -getProperty:TargetFrameworks -getProperty:TargetFramework \
    -getProperty:Version -getProperty:IncludeSymbols)"
  [ "$(prop IsPackable "$json")" = true ] || continue
  tfms="$(prop TargetFrameworks "$json")"
  [ -n "$tfms" ] || tfms="$(prop TargetFramework "$json")"
  projects+=("$project")
  ids+=("$(prop PackageId "$json")")
  assemblies+=("$(prop AssemblyName "$json")")
  frameworks+=("$tfms")
  versions+=("$(prop Version "$json")")
  symbols+=("$(prop IncludeSymbols "$json")")
done
[ ${#projects[@]} -gt 0 ] || die "no packable projects under src/"

# Lockstep: one version for all packages.
version="${versions[0]}"
for v in "${versions[@]}"; do
  [ "$v" = "$version" ] || error "packages have different versions (${versions[*]}); set the version only in Directory.Build.props"
done
if [ -n "${EXPECTED_VERSION:-}" ] && [ "$version" != "$EXPECTED_VERSION" ]; then
  error "package version $version does not match the expected version $EXPECTED_VERSION"
fi
[ "$errors" -eq 0 ] || exit 1

if [ "${SKIP_PACK:-}" = true ]; then
  echo "==> Checking the packages in $out (SKIP_PACK=true), version $version"
else
  echo "==> Packing ${#projects[@]} project(s), version $version"
  for project in "${projects[@]}"; do
    echo "    $project"
    dotnet pack "$project" -c Release -o "$out" ${1+"$@"}
  done
fi

# Check each package and build the summary.
summary="| Package | Version | Target frameworks | Dependencies | Symbols |
| ------- | ------- | ----------------- | ------------ | ------- |"
i=0
while [ "$i" -lt ${#projects[@]} ]; do
  id="${ids[$i]}"
  assembly="${assemblies[$i]}"
  tfms="${frameworks[$i]}"
  nupkg="$out/$id.$version.nupkg"
  snupkg="$out/$id.$version.snupkg"
  i=$((i + 1))

  if [ ! -f "$nupkg" ]; then
    error "$id: $nupkg was not created"
    continue
  fi
  symbol_state=no
  if [ "${symbols[$((i - 1))]}" = true ]; then
    if [ -f "$snupkg" ]; then symbol_state=yes; else error "$id: $snupkg was not created"; fi
  fi

  # Read the listing once: "unzip | grep -q" can fail under pipefail when grep exits early.
  listing="$(unzip -l "$nupkg")"
  for tfm in ${tfms//;/ }; do
    grep -q " lib/$tfm/$assembly.dll\$" <<< "$listing" || error "$id: the package has no lib/$tfm/$assembly.dll"
  done

  nuspec="$(unzip -p "$nupkg" "$id.nuspec")"
  dependencies="$(grep -o '<dependency id="[^"]*"' <<< "$nuspec" | sed 's/.*id="\(.*\)"/\1/' | sort -u | tr '\n' ',' || true)"
  dependencies="${dependencies%,}"
  dependencies="${dependencies//,/, }"

  # The file names under analyzers/ (folder entries end with "/" and are left out).
  analyzer_files="$(awk '$4 ~ /^analyzers\// && $4 !~ /\/$/ { print $4 }' <<< "$listing" | sort)"
  if [ "$id" = "$core_id" ]; then
    [ -z "$dependencies" ] || error "$id must not have dependencies (decision D21). Found: $dependencies"
    if [ "$analyzer_files" != "$core_analyzer_files" ]; then
      error "$id: analyzers/ must contain exactly: $(tr '\n' ' ' <<< "$core_analyzer_files")Found: $(tr '\n' ' ' <<< "${analyzer_files:-nothing}")"
    fi
  elif [ -n "$analyzer_files" ]; then
    error "$id: only the $core_id package may contain analyzers. Found: $(tr '\n' ' ' <<< "$analyzer_files")"
  fi

  summary="$summary
| $id | $version | ${tfms//;/, } | ${dependencies:-none} | $symbol_state |"
done

echo
echo "$summary"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo '## NuGet packages'
    echo
    echo "$summary"
  } >> "$GITHUB_STEP_SUMMARY"
fi

if [ "$errors" -gt 0 ]; then
  echo "$errors problem(s) found." >&2
  exit 1
fi
echo
echo "All packages are valid."
