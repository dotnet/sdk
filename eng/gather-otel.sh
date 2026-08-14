#!/usr/bin/env bash

set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage: ./eng/gather-otel.sh <dotnet-command-args...>

Runs a dotnet CLI command 10 times (5 non-AOT, then 5 AOT) and exports OTel
telemetry to a collector via OTLP.

Run sequence:
  Runs 1-5   DOTNET_CLI_ENABLEAOT=0  (non-AOT managed CLI)
  Runs 6-10  DOTNET_CLI_ENABLEAOT=1  (AOT native CLI)

Prerequisites:
  - Build the repo first: ./build.sh
  - An OTLP-capable collector must be listening (e.g. the Aspire dashboard
    or a standalone otel-collector).
  - Set OTEL_EXPORTER_OTLP_ENDPOINT to override the default:
      http://127.0.0.1:4317

Each invocation writes separate stdout and stderr log files under:
  artifacts/tmp/otel/runs/<timestamp>/

All commands execute with PWD set to artifacts/bin/redist/Debug.

Examples:
  ./eng/gather-otel.sh dev-certs https
      Runs 'dotnet dev-certs https' 10 times and exports telemetry to
      http://127.0.0.1:4317.

  OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:18889 \
      ./eng/gather-otel.sh dev-certs https
      Same as above but targets a non-default OTLP endpoint.
EOF
}

if [ "$#" -eq 0 ]; then
  print_usage
  exit 1
fi

if [ "${1:-}" = '--help' ] || [ "${1:-}" = '-h' ]; then
  print_usage
  exit 0
fi

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do
  scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$scriptroot/$SOURCE"
done
scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
repo_root="$( cd "$scriptroot/.." && pwd )"
command_working_dir="$repo_root/artifacts/bin/redist/Debug"

dotnet_root="$repo_root/artifacts/bin/redist/Debug/dotnet"
dotnet_exe="$dotnet_root/dotnet"

if [ ! -d "$command_working_dir" ]; then
  echo "Expected command working directory at $command_working_dir. Build the repo first (for example: ./build.sh)." >&2
  exit 1
fi

if [ ! -x "$dotnet_exe" ]; then
  rel_dotnet_exe="$dotnet_exe"
  case "$dotnet_exe" in
    "$PWD"/*) rel_dotnet_exe="${dotnet_exe#$PWD/}" ;;
  esac
  echo "Expected redist dotnet at $rel_dotnet_exe. Build the repo first (for example: ./build.sh)." >&2
  exit 1
fi

otel_state_dir="$repo_root/artifacts/tmp/otel"
mkdir -p "$otel_state_dir"

run_id="$(date +%Y%m%d-%H%M%S)"
run_logs_dir="$otel_state_dir/runs/$run_id"
mkdir -p "$run_logs_dir"

to_abs_path() {
  local p="$1"
  if command -v realpath >/dev/null 2>&1; then
    realpath "$p" 2>/dev/null || echo "$p"
    return
  fi

  if [ -e "$p" ]; then
    (cd "$(dirname "$p")" && printf '%s/%s\n' "$(pwd -P)" "$(basename "$p")")
  else
    echo "$p"
  fi
}

to_relative_path() {
  local p="$1"
  local abs
  abs="$(to_abs_path "$p")"

  if command -v realpath >/dev/null 2>&1; then
    realpath --relative-to="$PWD" "$abs" 2>/dev/null || echo "$abs"
    return
  fi

  case "$abs" in
    "$PWD"/*) echo "${abs#$PWD/}" ;;
    *) echo "$abs" ;;
  esac
}

to_file_uri() {
  local p="$1"
  local abs
  abs="$(to_abs_path "$p")"
  abs="${abs//\\//}"

  local uri
  if [[ "$abs" =~ ^[A-Za-z]:/ ]]; then
    uri="file:///$abs"
  else
    uri="file://$abs"
  fi

  uri="${uri// /%20}"
  echo "$uri"
}

hyperlink_path() {
  local p="$1"
  local uri
  local rel
  uri="$(to_file_uri "$p")"
  rel="$(to_relative_path "$p")"
  printf '\033]8;;%s\a%s\033]8;;\a' "$uri" "$rel"
}

write_vt_progress() {
  local completed="$1"
  local total="$2"
  local label="$3"
  local width=24
  local filled=$((completed * width / total))
  local empty=$((width - filled))
  local bar_filled bar_empty
  bar_filled="$(printf '%*s' "$filled" '' | tr ' ' '#')"
  bar_empty="$(printf '%*s' "$empty" '' | tr ' ' '-')"
  printf '\r\033[2K\033[36m[%s%s]\033[0m %s/%s %s' "$bar_filled" "$bar_empty" "$completed" "$total" "$label"
}

complete_vt_progress_line() {
  printf '\n'
}

failure_modes=()
failure_iterations=()
failure_exit_codes=()
failure_stdout_logs=()
failure_stderr_logs=()

invoke_logged_dotnet_command() {
  local mode="$1"
  local iteration="$2"
  shift 2

  local stdout_log="$run_logs_dir/${mode}-${iteration}.stdout.log"
  local stderr_log="$run_logs_dir/${mode}-${iteration}.stderr.log"

  set +e
  (
    cd "$command_working_dir"
    "$dotnet_exe" "$@"
  ) >"$stdout_log" 2>"$stderr_log"
  local exit_code=$?
  set -e

  if [ "$exit_code" -ne 0 ]; then
    failure_modes+=("$mode")
    failure_iterations+=("$iteration")
    failure_exit_codes+=("$exit_code")
    failure_stdout_logs+=("$stdout_log")
    failure_stderr_logs+=("$stderr_log")
  fi
}

# Ensure telemetry is enabled and OTLP export is active for this process tree.
export DOTNET_CLI_TELEMETRY_OPTOUT=0
export DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER=1

if [ -z "${OTEL_EXPORTER_OTLP_ENDPOINT:-}" ]; then
  export OTEL_EXPORTER_OTLP_ENDPOINT="http://127.0.0.1:4317"
fi

echo "Telemetry env: DOTNET_CLI_TELEMETRY_OPTOUT=$DOTNET_CLI_TELEMETRY_OPTOUT, DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER=$DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER"
echo "OTLP endpoint: $OTEL_EXPORTER_OTLP_ENDPOINT"
echo "Running command: $(hyperlink_path "$dotnet_exe") $*"
echo "Command working directory: $(hyperlink_path "$command_working_dir")"
echo "Command logs directory: $(hyperlink_path "$run_logs_dir")"

total_runs=10
run_index=0

for i in 1 2 3 4 5; do
  run_index=$((run_index + 1))
  write_vt_progress "$run_index" "$total_runs" "non-AOT $i/5"
  export DOTNET_CLI_ENABLEAOT=0
  invoke_logged_dotnet_command "non-aot" "$i" "$@"
done

for i in 1 2 3 4 5; do
  run_index=$((run_index + 1))
  write_vt_progress "$run_index" "$total_runs" "AOT $i/5"
  export DOTNET_CLI_ENABLEAOT=1
  invoke_logged_dotnet_command "aot" "$i" "$@"
done

complete_vt_progress_line

if [ "${#failure_modes[@]}" -eq 0 ]; then
  echo "Completed all runs successfully."
  exit 0
fi

echo
echo "Run summary: ${#failure_modes[@]} command(s) failed."
for idx in "${!failure_modes[@]}"; do
  echo "- ${failure_modes[$idx]} ${failure_iterations[$idx]}/5 failed with exit code ${failure_exit_codes[$idx]}"
  echo "  stdout: $(hyperlink_path "${failure_stdout_logs[$idx]}")"
  echo "  stderr: $(hyperlink_path "${failure_stderr_logs[$idx]}")"
done

exit 1
