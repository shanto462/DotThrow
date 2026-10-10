#!/usr/bin/env bash
# Runs inside the test container (eng/docker/test.Dockerfile). eng/test-matrix.sh and
# eng/test-matrix.ps1 start it; it is not meant to run on the host.
#
# Input (environment variables):
#   LEGS         comma-separated legs, for example "net8.0,netstandard2.0"
#   TEST_FILTER  optional value for "dotnet test --filter"
#
# The repository is mounted read-only at /src. It is copied to /work/repo without
# bin, obj, artifacts and TestResults folders, so no host build output is reused and
# nothing is written back to the host.

set -euo pipefail

src=/src
repo=/work/repo
out=/work/out
legs="${LEGS:-net8.0,net9.0,net10.0,netstandard2.0,netstandard2.1}"
filter="${TEST_FILTER:-}"
tests_project=tests/DotThrow.Tests/DotThrow.Tests.csproj
lib_project=src/DotThrow/DotThrow.csproj

echo "==> Copying the repository into the container"
rm -rf "$repo" "$out"
mkdir -p "$repo" "$out"
tar -C "$src" \
  --exclude=bin --exclude=obj --exclude=artifacts --exclude=TestResults --exclude=.vs \
  -cf - . | tar -C "$repo" -xf -
cd "$repo"

echo "==> Installed runtimes"
dotnet --list-runtimes | sed -e 's/ \[.*//' -e 's/^/    /'

# The target frameworks change as the projects grow, so read them from the projects.
tfms_of() {
  dotnet msbuild "$1" -getProperty:TargetFrameworks | tail -n 1 | tr -d '[:space:]'
}
test_tfms=";$(tfms_of "$tests_project");"
lib_tfms=";$(tfms_of "$lib_project");"
asset_switch=false
if grep -q 'DotThrowAssetTfm' "$tests_project"; then
  asset_switch=true
fi
echo "    tests:   ${test_tfms//;/ }"
echo "    library: ${lib_tfms//;/ }"

# Counts the test results with one outcome (Passed, Failed, NotExecuted) in a TRX file.
count_outcome() {
  grep -o '<UnitTestResult [^>]*>' "$1" | grep -c "outcome=\"$2\"" || true
}

# .NET 9 on Arm CPUs with SME (Apple M4 and later) inside a Linux VM crashes at random
# with SIGILL (exit code 132) when it suspends threads for the GC with signals. .NET 8
# and .NET 10 are not affected. Turning the signal injection off avoids the crash.
sme_cpu=false
if [[ "$(uname -m)" == aarch64 ]] && grep -qw sme /proc/cpuinfo 2> /dev/null; then
  sme_cpu=true
fi

rows=()
ran=0
failures=0
IFS=',' read -r -a leg_list <<< "${legs// /}"

for leg in "${leg_list[@]}"; do
  label="$leg"
  case "$leg" in
    net8.0 | net9.0 | net10.0)
      if [[ "$test_tfms" != *";$leg;"* ]]; then
        rows+=("$label|N/A|-|-|-|-|the tests do not target $leg yet")
        continue
      fi
      args=(-f "$leg")
      ;;
    netstandard2.0 | netstandard2.1)
      label="$leg asset"
      if [[ "$asset_switch" != true ]]; then
        rows+=("$label|N/A|-|-|-|-|the tests have no DotThrowAssetTfm switch yet")
        continue
      fi
      if [[ "$lib_tfms" != *";$leg;"* ]]; then
        rows+=("$label|N/A|-|-|-|-|the library does not target $leg yet")
        continue
      fi
      if [[ "$test_tfms" != *";net8.0;"* ]]; then
        rows+=("$label|N/A|-|-|-|-|the tests do not target net8.0")
        continue
      fi
      args=(-f net8.0 "-p:DotThrowAssetTfm=$leg")
      ;;
    *)
      echo "Unknown leg: $leg" >&2
      exit 2
      ;;
  esac
  if [[ -n "$filter" ]]; then
    args+=(--filter "$filter")
  fi

  leg_env=()
  note=""
  if [[ "$leg" == net9.0 && "$sme_cpu" == true ]]; then
    leg_env=(DOTNET_INTERNAL_ThreadSuspendInjection=0)
    note="DOTNET_INTERNAL_ThreadSuspendInjection=0 (.NET 9 SIGILL on SME CPUs)"
  fi

  echo
  echo "==> $label: ${leg_env[*]:+${leg_env[*]} }dotnet test -c Release ${args[*]}"
  start=$SECONDS
  status=0
  # Each leg gets its own artifacts folder, so the legs never share build output.
  env "${leg_env[@]}" dotnet test "$tests_project" -c Release "${args[@]}" \
    --artifacts-path "$out/$leg/artifacts" \
    --results-directory "$out/$leg/results" \
    --logger "trx;LogFileName=$leg.trx" \
    --verbosity quiet || status=$?
  elapsed="$((SECONDS - start))s"
  ran=$((ran + 1))

  passed=- failed=- skipped=-
  trx="$out/$leg/results/$leg.trx"
  if [[ -f "$trx" ]]; then
    passed="$(count_outcome "$trx" Passed)"
    failed="$(count_outcome "$trx" Failed)"
    skipped="$(count_outcome "$trx" NotExecuted)"
  fi
  if [[ "$status" -eq 0 ]]; then
    result=PASS
  else
    result=FAIL
    failures=$((failures + 1))
    if [[ ! -f "$trx" ]]; then
      note="build failed or no tests ran, see the output above"
    elif [[ "$failed" == 0 ]]; then
      note="the test run stopped early (crash?), see the output above"
    fi
  fi
  rows+=("$label|$result|$passed|$failed|$skipped|$elapsed|$note")
done

echo
echo "==> Summary"
printf '%-22s %-6s %7s %7s %8s %6s  %s\n' "Leg" "Result" "Passed" "Failed" "Skipped" "Time" "Note"
printf '%-22s %-6s %7s %7s %8s %6s  %s\n' "---" "------" "------" "------" "-------" "----" "----"
for row in "${rows[@]}"; do
  IFS='|' read -r c_leg c_result c_passed c_failed c_skipped c_time c_note <<< "$row"
  printf '%-22s %-6s %7s %7s %8s %6s  %s\n' \
    "$c_leg" "$c_result" "$c_passed" "$c_failed" "$c_skipped" "$c_time" "$c_note"
done
echo

if [[ "$ran" -eq 0 ]]; then
  echo "No leg ran." >&2
  exit 1
fi
if [[ "$failures" -gt 0 ]]; then
  echo "$failures leg(s) failed." >&2
  exit 1
fi
echo "All legs that ran passed."
