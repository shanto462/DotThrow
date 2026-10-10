#!/usr/bin/env bash
# Starts the Native AOT build of eng/aot-smoke and sends it valid and invalid requests.
# The CI job "Native AOT" runs it after the publish. Works with the bash 3.2 of macOS.
#
# Usage: eng/aot-smoke/smoke.sh PATH_TO_BINARY
#
# Checks:
#   - the app starts (this also runs the options validation, ValidateOnStart)
#   - a valid request gets 200 and an invalid one gets a 400 application/problem+json with
#     the failing property, for the validator class (/sign-up) and for the dynamic rules
#     from configuration (/orders)
#   - the app log has no exception

set -euo pipefail

[ $# -eq 1 ] || {
  echo "usage: eng/aot-smoke/smoke.sh PATH_TO_BINARY" >&2
  exit 2
}
binary="$1"
url="http://127.0.0.1:${PORT:-5080}"
work="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/dotthrow-aot-smoke.XXXXXX")"
log="$work/app.log"

"$binary" --urls "$url" > "$log" 2>&1 &
pid=$!
cleanup() {
  kill "$pid" 2> /dev/null || true
  wait "$pid" 2> /dev/null || true
  rm -rf "$work"
}
trap cleanup EXIT

ready=false
for _ in $(seq 1 100); do
  if curl -fs -o /dev/null "$url/health"; then
    ready=true
    break
  fi
  if ! kill -0 "$pid" 2> /dev/null; then
    break
  fi
  sleep 0.2
done
if [ "$ready" != true ]; then
  echo "error: the app did not start. Its output:" >&2
  cat "$log" >&2
  exit 1
fi

failures=0
rows=""
# Arguments: name, path, JSON body, expected status, text the body must contain.
check() {
  local body="$work/body" headers="$work/headers" code result
  code="$(curl -s -o "$body" -D "$headers" -w '%{http_code}' \
    -H 'Content-Type: application/json' -d "$3" "$url$2")"
  result=PASS
  if [ "$code" != "$4" ] || ! grep -q "$5" "$body"; then
    result=FAIL
  elif [ "$4" = 400 ] && ! grep -qi '^content-type: application/problem+json' "$headers"; then
    result=FAIL
  fi
  [ "$result" = PASS ] || failures=$((failures + 1))
  echo "$result $1: HTTP $code $(head -c 300 "$body")"
  rows="$rows
| $1 | $4 | $code | $result |"
}

check "valid sign-up" /sign-up '{"name":"Ada Lovelace","email":"ada@example.com"}' 200 'ada@example.com'
check "invalid sign-up" /sign-up '{"name":"","email":"not-an-email"}' 400 '"Email"'
check "valid order" /orders '{"sku":"BOOK-1","quantity":2}' 200 'BOOK-1'
check "invalid order" /orders '{"sku":" ","quantity":500}' 400 '"Quantity"'

if grep -qi 'exception' "$log"; then
  echo "error: the app log has an exception:" >&2
  cat "$log" >&2
  failures=$((failures + 1))
fi

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo '## Native AOT smoke test (ASP.NET Core)'
    echo
    echo '| Request | Expected | Got | Result |'
    echo '| ------- | -------- | --- | ------ |'
    echo "${rows#?}"
  } >> "$GITHUB_STEP_SUMMARY"
fi

if [ "$failures" -gt 0 ]; then
  echo "$failures check(s) failed." >&2
  exit 1
fi
echo "All smoke requests passed."
