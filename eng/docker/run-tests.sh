#!/usr/bin/env bash
# Runs inside the test container (eng/docker/test.Dockerfile). eng/test-matrix.sh and
# eng/test-matrix.ps1 start it; it is not meant to run on the host.
#
# Input (environment variables):
#   LEGS          comma-separated legs, for example "net8.0,netstandard2.0"
#   TEST_FILTER   optional value for "dotnet test --filter"
#   HANG_TIMEOUT  abort a test run that makes no progress for this long (default 5m)
#   ROWS_FILE     where to write this run's result rows (default /work/rows.txt)
#   SUMMARY       "true" (default) prints the summary table of every *.rows file in
#                 /results (when mounted) plus this run's rows; "false" prints nothing
#   NET9_MODE     how the host runs the net9.0 leg and why (shown in the summary)
#
# The repository is mounted read-only at /src. It is copied to /work/repo without
# bin, obj, artifacts and TestResults folders, so no host build output is reused and
# nothing is written back to the host.
#
# Legs: net8.0, net9.0 and net10.0 run every test project under tests/ that targets that
# framework. The asset legs (netstandard2.0, netstandard2.1) run the net8.0 core tests
# against that build of the library. Everything runs in Release, because the
# zero-allocation tests skip in Debug.

set -euo pipefail

src=/src
repo=/work/repo
out=/work/out
legs="${LEGS:-net8.0,net9.0,net10.0,netstandard2.0,netstandard2.1}"
filter="${TEST_FILTER:-}"
hang_timeout="${HANG_TIMEOUT:-5m}"
rows_file="${ROWS_FILE:-/work/rows.txt}"
summary="${SUMMARY:-true}"
net9_mode="${NET9_MODE:-}"
core_tests=tests/DotThrow.Tests/DotThrow.Tests.csproj
lib_project=src/DotThrow/DotThrow.csproj

case "$(uname -m)" in
  aarch64) arch=arm64 ;;
  x86_64) arch=x64 ;;
  *) arch="$(uname -m)" ;;
esac

echo "==> Copying the repository into the container ($arch)"
rm -rf "$repo" "$out"
mkdir -p "$repo" "$out"
: > "$rows_file"
tar -C "$src" \
  --exclude=bin --exclude=obj --exclude=artifacts --exclude=TestResults --exclude=.vs \
  -cf - . | tar -C "$repo" -xf -
cd "$repo"

echo "==> Installed runtimes"
dotnet --list-runtimes | sed -e 's/ \[.*//' -e 's/^/    /'

# Target frameworks of a project. They change as the projects grow, so read them.
tfms_of() {
  local json tfms
  json="$(dotnet msbuild "$1" -getProperty:TargetFrameworks -getProperty:TargetFramework)"
  tfms="$(sed -n 's/^ *"TargetFrameworks": "\(.*\)",\{0,1\}$/\1/p' <<< "$json")"
  [[ -n "$tfms" ]] || tfms="$(sed -n 's/^ *"TargetFramework": "\(.*\)",\{0,1\}$/\1/p' <<< "$json")"
  echo ";$tfms;"
}

test_projects=()
test_tfms=()
for project in tests/*/*.csproj; do
  [[ -f "$project" ]] || continue
  test_projects+=("$project")
  test_tfms+=("$(tfms_of "$project")")
  echo "    $(basename "$project" .csproj): $(tr ';' ' ' <<< "${test_tfms[-1]}")"
done
lib_tfms="$(tfms_of "$lib_project")"
echo "    library: $(tr ';' ' ' <<< "$lib_tfms")"
asset_switch=false
if [[ -f "$core_tests" ]] && grep -q 'DotThrowAssetTfm' "$core_tests"; then
  asset_switch=true
fi

# Counts the test results with one outcome (Passed, Failed, NotExecuted) in a TRX file.
count_outcome() {
  grep -o '<UnitTestResult [^>]*>' "$1" | grep -c "outcome=\"$2\"" || true
}

# .NET 9 on Arm CPUs that have SME but no SVE (Apple M4 and later under OrbStack, and
# similar VMs) dies with SIGILL (exit code 132): while it reads a signal frame it runs
# `rdvl`, an SVE instruction. .NET 8 and .NET 10.0.9+ are not affected, and GitHub-hosted
# runners have no SME. See decision D31 in decisions.md and:
#   https://github.com/dotnet/runtime/issues/122608      (analysis)
#   https://github.com/dotnet/runtime/pull/127398        (the fix)
#   https://github.com/dotnet/runtime/pull/127518        (backport, in .NET 10.0.9 and later)
#   https://github.com/dotnet/runtime/issues/133030      (.NET 9 request, unlikely to ship)
#   https://github.com/orbstack/orbstack/issues/2670     (OrbStack report)
# DOTNET_INTERNAL_ThreadSuspendInjection=0 stops the runtime from sending the signal it
# uses to suspend threads for the GC, which is the common trigger. Limits:
#   - other signals still crash, for example a caught NullReferenceException (SIGSEGV);
#   - a thread in a tight loop without GC polls can then block the GC, so a run can hang.
#     The hang timeout below stops such a run.
# That is not enough for the current tests, so on such CPUs eng/test-matrix.sh runs the
# net9.0 leg as x64 through emulation by default. This switch is used only with
# eng/test-matrix.sh --net9-native.
sme_without_sve=false
if [[ "$arch" == arm64 ]] && grep -qw sme /proc/cpuinfo 2> /dev/null && ! grep -qw sve /proc/cpuinfo 2> /dev/null; then
  sme_without_sve=true
fi

ran=0
failures=0
IFS=',' read -r -a leg_list <<< "${legs// /}"

# Runs one test project for one leg and records a row:
# leg|project|platform|result|passed|failed|skipped|time|note
run_one() {
  local leg="$1" label="$2" project="$3"
  shift 3
  local name args leg_env note start status elapsed trx passed failed skipped result log
  name="$(basename "$project" .csproj)"
  args=("$@")
  if [[ -n "$filter" ]]; then
    args+=(--filter "$filter")
  fi
  leg_env=()
  note=""
  if [[ "$leg" == net9.0 ]]; then
    note="$net9_mode"
    if [[ "$sme_without_sve" == true ]]; then
      leg_env=(DOTNET_INTERNAL_ThreadSuspendInjection=0)
      note="${note:+$note, }ThreadSuspendInjection=0"
    fi
  fi

  echo
  echo "==> $label, $name: ${leg_env[*]:+${leg_env[*]} }dotnet test -c Release ${args[*]}"
  start=$SECONDS
  status=0
  # The legs get separate artifacts folders, so they never share build output.
  log="$out/$leg/$name.log"
  mkdir -p "$out/$leg"
  env "${leg_env[@]}" dotnet test "$project" -c Release "${args[@]}" \
    --artifacts-path "$out/$leg/artifacts" \
    --results-directory "$out/$leg/results/$name" \
    --logger "trx;LogFileName=$name.trx" \
    --blame-hang-timeout "$hang_timeout" --blame-hang-dump-type none \
    --verbosity quiet 2>&1 | tee "$log" || status=$?
  elapsed="$((SECONDS - start))s"
  ran=$((ran + 1))

  passed=- failed=- skipped=-
  trx="$out/$leg/results/$name/$name.trx"
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
    if [[ "$leg" == net9.0 && "$sme_without_sve" == true ]] && grep -qs 'exit code 132' "$log" "$trx"; then
      note="SIGILL from the .NET 9 SME bug; run without --net9-native"
    elif [[ ! -f "$trx" ]]; then
      note="build failed or no tests ran, see the output above"
    elif [[ "$failed" == 0 ]]; then
      note="the run stopped early (crash, or no progress for $hang_timeout), see the output above"
    fi
  fi
  echo "$label|${name#DotThrow.}|$arch|$result|$passed|$failed|$skipped|$elapsed|$note" >> "$rows_file"
}

na() {
  echo "$1|-|$arch|N/A|-|-|-|-|$2" >> "$rows_file"
}

for leg in "${leg_list[@]}"; do
  case "$leg" in
    net8.0 | net9.0 | net10.0)
      any=false
      for i in "${!test_projects[@]}"; do
        if [[ "${test_tfms[$i]}" == *";$leg;"* ]]; then
          any=true
          run_one "$leg" "$leg" "${test_projects[$i]}" -f "$leg"
        fi
      done
      [[ "$any" == true ]] || na "$leg" "no test project targets $leg yet"
      ;;
    netstandard2.0 | netstandard2.1)
      label="$leg asset"
      if [[ "$asset_switch" != true ]]; then
        na "$label" "the core tests have no DotThrowAssetTfm switch yet"
      elif [[ "$lib_tfms" != *";$leg;"* ]]; then
        na "$label" "the library does not target $leg yet"
      else
        run_one "$leg" "$label" "$core_tests" -f net8.0 "-p:DotThrowAssetTfm=$leg"
      fi
      ;;
    *)
      echo "Unknown leg: $leg" >&2
      exit 2
      ;;
  esac
done

# The summary covers this run and, when several containers share /results (for
# example with --net9-amd64), the rows the earlier containers wrote.
if [[ "$summary" == true ]]; then
  all_rows="$(cat /results/*.rows 2> /dev/null || true)"
  if [[ "$rows_file" != /results/* ]]; then
    all_rows="$all_rows"$'\n'"$(cat "$rows_file")"
  fi
  format='%-21s %-26s %-5s %-6s %7s %7s %8s %6s  %s\n'
  echo
  echo "==> Summary"
  # shellcheck disable=SC2059
  printf "$format" "Leg" "Project" "Arch" "Result" "Passed" "Failed" "Skipped" "Time" "Note"
  # shellcheck disable=SC2059
  printf "$format" "---" "-------" "----" "------" "------" "------" "-------" "----" "----"
  total_ran=0
  total_failed=0
  while IFS='|' read -r c_leg c_project c_arch c_result c_passed c_failed c_skipped c_time c_note; do
    [[ -n "$c_leg" ]] || continue
    # shellcheck disable=SC2059
    printf "$format" "$c_leg" "$c_project" "$c_arch" "$c_result" "$c_passed" "$c_failed" "$c_skipped" "$c_time" "$c_note"
    [[ "$c_result" == N/A ]] || total_ran=$((total_ran + 1))
    [[ "$c_result" != FAIL ]] || total_failed=$((total_failed + 1))
  done <<< "$all_rows"
  echo
  if [[ -n "$net9_mode" ]] && grep -q '^net9\.0|' <<< "$all_rows"; then
    echo "net9.0 mode: $net9_mode"
    echo
  fi
  if [[ "$total_ran" -eq 0 ]]; then
    echo "No test run happened." >&2
    exit 1
  fi
  if [[ "$total_failed" -gt 0 ]]; then
    echo "$total_failed test run(s) failed." >&2
    exit 1
  fi
  echo "All test runs passed."
  exit 0
fi

# No summary: the exit code covers only this container's runs.
[[ "$failures" -eq 0 ]] || exit 1
