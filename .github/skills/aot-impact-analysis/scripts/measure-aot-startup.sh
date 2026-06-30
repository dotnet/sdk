#!/usr/bin/env bash
# Measures .NET CLI startup performance for two scenarios using the redist SDK's `dotnet` muxer, and
# (optionally) exports OpenTelemetry spans to a running Aspire Dashboard for a span-level breakdown.
#
# The redist muxer dispatches to the NativeAOT entrypoint when DOTNET_CLI_ENABLEAOT=true and to the
# managed CLI otherwise, so the default before/after compares the *same* built SDK with the AOT path
# off vs. on. Pass --baseline-dotnet to instead compare two different builds with identical env.
#
# Two phases per scenario:
#   1. Timing phase   - runs `dotnet <args>` N times with the OTLP exporter OFF and records
#                       wall-clock (min / median / mean / p95).
#   2. Telemetry phase - when an Aspire Dashboard is reachable, a few exporter-ON runs tagged
#                       OTEL_RESOURCE_ATTRIBUTES="scenario=<label>" so the dashboard captures the
#                       CLI's `dotnet-cli` spans.
#
# The PowerShell sibling Measure-AotStartup.ps1 is the canonical implementation; this is the bash
# port. Sub-second timing needs bash >= 5 (EPOCHREALTIME) or GNU coreutils `gdate` on macOS.
#
# Usage:
#   scripts/measure-aot-startup.sh [--dotnet-path <muxer>] [--baseline-dotnet <muxer>]
#       [--arguments '<args>'] [--iterations N] [--warmup N] [--configuration <cfg>]
#       [--otlp-endpoint <url>] [--dashboard-url <url>] [--output-path <file>]
set -euo pipefail

dotnet_path=""
baseline_dotnet=""
args="--version"
iterations=30
warmup=5
configuration="Release"
otlp_endpoint="http://localhost:4317"
dashboard_url="http://localhost:18888"
output_path=""

usage() { awk 'NR==1{next} /^#/{sub(/^# ?/,"");print;next} {exit}' "$0"; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dotnet-path) dotnet_path="$2"; shift 2;;
    --baseline-dotnet) baseline_dotnet="$2"; shift 2;;
    --arguments) args="$2"; shift 2;;
    --iterations) iterations="$2"; shift 2;;
    --warmup) warmup="$2"; shift 2;;
    --configuration|-c) configuration="$2"; shift 2;;
    --otlp-endpoint) otlp_endpoint="$2"; shift 2;;
    --dashboard-url) dashboard_url="$2"; shift 2;;
    --output-path|-o) output_path="$2"; shift 2;;
    -h|--help) usage; exit 0;;
    *) echo "Unknown option: $1" >&2; usage; exit 1;;
  esac
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

find_repo_root() {
  local dir="$1"
  while [[ -n "$dir" && ! -e "$dir/.git" ]]; do
    local parent; parent="$(dirname "$dir")"
    [[ "$parent" == "$dir" ]] && break
    dir="$parent"
  done
  [[ -e "$dir/.git" ]] || { echo "Could not locate the repository root from $1." >&2; exit 1; }
  ( cd "$dir" && pwd )
}

repo_root="$(find_repo_root "$script_dir")"
[[ -z "$output_path" ]] && output_path="$repo_root/artifacts/aot-startup.md"

resolve_muxer() {
  local explicit="$1"
  if [[ -n "$explicit" ]]; then
    [[ -f "$explicit" ]] || { echo "dotnet muxer not found at $explicit." >&2; exit 1; }
    printf '%s\n' "$explicit"; return
  fi
  local name="dotnet"; [[ "${OS:-}" == "Windows_NT" ]] && name="dotnet.exe"
  local candidate="$repo_root/artifacts/bin/redist/$configuration/dotnet/$name"
  [[ -f "$candidate" ]] || { echo "Redist muxer not found at $candidate. Build the full SDK layout first (build.sh/.cmd), or pass --dotnet-path." >&2; exit 1; }
  printf '%s\n' "$candidate"
}

_now_s() {
  if [[ -n "${EPOCHREALTIME:-}" ]]; then printf '%s' "${EPOCHREALTIME/,/.}"; return; fi
  if command -v gdate >/dev/null 2>&1; then gdate +%s.%N; return; fi
  date +%s.%N 2>/dev/null | sed 's/N$//'
}

primary="$(resolve_muxer "$dotnet_path")"

# Scenarios as parallel arrays (bash 3.2-compatible).
labels=(); exes=(); aot=()
if [[ -n "$baseline_dotnet" ]]; then
  base_mux="$(resolve_muxer "$baseline_dotnet")"
  labels=("base" "pr"); exes=("$base_mux" "$primary"); aot=("true" "true")
  mode="build-vs-build (AOT path on both)"
else
  labels=("managed" "aot"); exes=("$primary" "$primary"); aot=("false" "true")
  mode="managed vs AOT (same SDK)"
fi

echo "Muxer:      $primary"
echo "Command:    dotnet $args"
echo "Mode:       $mode"
echo "Iterations: $iterations (warmup $warmup)"
echo ""

r_min=(); r_med=(); r_mean=(); r_p95=()
for idx in "${!labels[@]}"; do
  label="${labels[$idx]}"; exe="${exes[$idx]}"; aotval="${aot[$idx]}"
  echo "==> Timing scenario '$label'..."
  for ((i=0; i<warmup; i++)); do
    env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_ENABLEAOT="$aotval" "$exe" $args >/dev/null 2>&1 || true
  done
  samples=()
  for ((i=0; i<iterations; i++)); do
    start="$(_now_s)"
    env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_ENABLEAOT="$aotval" "$exe" $args >/dev/null 2>&1 || true
    end="$(_now_s)"
    samples+=("$(awk "BEGIN{printf \"%.3f\", ($end-$start)*1000}")")
  done
  stats="$(printf '%s\n' "${samples[@]}" | sort -n | awk '
    { a[NR]=$1; sum+=$1 }
    function pct(p,  rank,lo,hi){ rank=(p/100)*(n-1)+1; lo=int(rank); hi=lo+(rank>lo?1:0); if(hi>n)hi=n; return a[lo]+(rank-lo)*(a[hi]-a[lo]); }
    END{ n=NR; printf "%.1f %.1f %.1f %.1f", a[1], pct(50), sum/n, pct(95); }')"
  read -r mn md me p9 <<<"$stats"
  r_min+=("$mn"); r_med+=("$md"); r_mean+=("$me"); r_p95+=("$p9")
done

# Telemetry phase.
telemetry_state="none"
otlp_reachable() {
  local url="$1" hp host port
  hp="${url#*://}"; host="${hp%%:*}"; port="${hp##*:}"; port="${port%%/*}"
  (exec 3<>"/dev/tcp/$host/$port") 2>/dev/null && { exec 3>&- 3<&-; return 0; }
  return 1
}
if [[ -n "$otlp_endpoint" ]]; then
  if otlp_reachable "$otlp_endpoint"; then
    for idx in "${!labels[@]}"; do
      label="${labels[$idx]}"; exe="${exes[$idx]}"; aotval="${aot[$idx]}"
      echo "==> Exporting spans for scenario '$label' to $otlp_endpoint..."
      for ((i=0; i<5; i++)); do
        env DOTNET_CLI_TELEMETRY_OPTOUT=0 DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER=1 \
            OTEL_EXPORTER_OTLP_ENDPOINT="$otlp_endpoint" OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
            OTEL_RESOURCE_ATTRIBUTES="scenario=$label" DOTNET_CLI_ENABLEAOT="$aotval" \
            "$exe" $args >/dev/null 2>&1 || true
      done
    done
    telemetry_state="exported"
  else
    telemetry_state="skipped"
  fi
fi

delta_med="$(awk "BEGIN{printf \"%+.1f\", ${r_med[1]} - ${r_med[0]}}")"
pct_med="$(awk "BEGIN{ if(${r_med[0]}>0) printf \"%+.1f\", (${r_med[1]}-${r_med[0]})/${r_med[0]}*100; else printf \"%+.1f\", 0}")"

mkdir -p "$(dirname "$output_path")"
{
  echo "## AOT startup performance"
  echo ""
  echo "Command: \`dotnet $args\` &nbsp;|&nbsp; $iterations iterations ($warmup warmup) &nbsp;|&nbsp; $mode."
  echo "Wall-clock measured with the OTLP exporter off (clean numbers); all times in ms."
  echo ""
  echo "| Scenario | Min | Median | Mean | P95 |"
  echo "| --- | ---: | ---: | ---: | ---: |"
  for idx in "${!labels[@]}"; do
    echo "| ${labels[$idx]} | ${r_min[$idx]} | ${r_med[$idx]} | ${r_mean[$idx]} | ${r_p95[$idx]} |"
  done
  echo ""
  echo "**Median delta (${labels[0]} -> ${labels[1]}): ${delta_med} ms (${pct_med}%).**"
  echo ""
  if [[ "$telemetry_state" == "exported" ]]; then
    echo "Spans exported to the Aspire Dashboard, tagged \`scenario=<label>\`. Inspect with:"
    echo ""
    echo '```'
    echo "aspire otel traces --dashboard-url $dashboard_url --format Table --search scenario:aot"
    echo "aspire otel traces --dashboard-url $dashboard_url --format Json  --search scenario:aot -n 5"
    echo '```'
  elif [[ "$telemetry_state" == "skipped" ]]; then
    echo "_Telemetry phase skipped: no Aspire Dashboard reachable at \`$otlp_endpoint\`. Start one with start-aspire-dashboard.sh._"
  fi
} > "$output_path"

echo ""
echo "Summary written to: $output_path"
for idx in "${!labels[@]}"; do
  echo "  ${labels[$idx]}: min=${r_min[$idx]} median=${r_med[$idx]} mean=${r_mean[$idx]} p95=${r_p95[$idx]} ms"
done
