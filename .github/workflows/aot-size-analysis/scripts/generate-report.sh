#!/usr/bin/env bash

set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage: generate-report.sh [options]

Options:
  --platforms <list>           Space-separated platforms to analyze
  --pr-build-url <url>         Pull request build link
  --baseline-build-url <url>   Baseline build link
  --temp-dir <path>            Directory containing extracted artifacts
  --output-file <path>         File that receives step outputs
  -h, --help                   Show this help

Example:
  generate-report.sh \
    --platforms "Linux_x64_AOT Windows_x64_AOT" \
    --pr-build-url "https://dev.azure.com/dnceng-public/public/_build/results?buildId=1569498" \
    --baseline-build-url "https://dev.azure.com/dnceng-public/public/_build/results?buildId=1569822" \
    --temp-dir /tmp/aot-size-analysis \
    --output-file /tmp/generate-report.out
EOF
}

require_value() {
  if [ "$#" -lt 2 ] || [ -z "$2" ]; then
    echo "Missing value for $1." >&2
    print_usage >&2
    exit 2
  fi
}

emit_warning() {
  printf '::warning::%s\n' "$1"
}

emit_output_value() {
  printf '%s=%s\n' "$1" "$2" >> "$output_file"
}

start_group() {
  printf '::group::%s\n' "$1"
}

end_group() {
  printf '::endgroup::\n'
}

platforms=""
pr_build_url=""
baseline_build_url=""
temp_dir=""
output_file=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --platforms)
      require_value "$@"
      platforms="$2"
      shift 2
      ;;
    --pr-build-url)
      require_value "$@"
      pr_build_url="$2"
      shift 2
      ;;
    --baseline-build-url)
      require_value "$@"
      baseline_build_url="$2"
      shift 2
      ;;
    --temp-dir)
      require_value "$@"
      temp_dir="$2"
      shift 2
      ;;
    --output-file)
      require_value "$@"
      output_file="$2"
      shift 2
      ;;
    -h|--help)
      print_usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      print_usage >&2
      exit 2
      ;;
  esac
done

if [ -z "$platforms" ] || [ -z "$pr_build_url" ] || [ -z "$baseline_build_url" ] ||
    [ -z "$temp_dir" ] || [ -z "$output_file" ]; then
  echo "All options except --help are required." >&2
  print_usage >&2
  exit 2
fi

report_file="${temp_dir}/size-report.md"
details_file="${temp_dir}/size-details.md"

# Pass 1: run sizoscope-cli for each platform, collect summary data
declare -A platform_totals
has_any_diff=false

for platform in $platforms; do
  start_group "Analyzing ${platform}"

  pr_mstat=$(find "${temp_dir}/pr/${platform}" -name "dotnet-aot.mstat" -type f -print -quit)
  base_mstat=$(find "${temp_dir}/base/${platform}" -name "dotnet-aot.mstat" -type f -print -quit)

  pr_mstat_size=$(stat --format='%s' "$pr_mstat")
  base_mstat_size=$(stat --format='%s' "$base_mstat")
  echo "Baseline MSTAT: ${base_mstat_size} bytes"
  echo "PR MSTAT: ${pr_mstat_size} bytes"

  diff_file="${temp_dir}/${platform}-diff.md"
  start_time=$(date +%s%N)
  if sizoscope-cli "$base_mstat" "$pr_mstat" --output "$diff_file"; then
    has_any_diff=true

    # Extract the total size difference from the first line
    total_line=$(head -1 "$diff_file")
    # Expected format: "Total accounted size difference: 781.8 kB"
    total_size=$(echo "$total_line" | sed -n 's/^Total accounted size difference: *//p')
    platform_totals["$platform"]="${total_size:-unknown}"
    elapsed_ns=$(($(date +%s%N) - start_time))
    detail_line_count=$(tail -n +2 "$diff_file" | grep -cve '^[[:space:]]*$' || true)
    printf 'Completed in %d.%03d seconds; total accounted size difference: %s; detail lines: %d\n' \
      "$((elapsed_ns / 1000000000))" "$(((elapsed_ns / 1000000) % 1000))" \
      "${total_size:-unknown}" "$detail_line_count"

    # Accumulate per-platform details
    {
      echo "### ${platform}"
      echo ""
      echo "<details>"
      echo "<summary>Size diff details</summary>"
      echo ""
      echo '```'
      cat "$diff_file"
      echo '```'
      echo ""
      echo "</details>"
      echo ""
    } >> "$details_file"
  else
    emit_warning "sizoscope-cli failed for ${platform}."
  fi

  end_group
done

if [ "$has_any_diff" = false ]; then
  echo "::notice::No size diffs were generated across any platform."
  emit_output_value "has_report" "false"
  exit 0
fi

# Pass 2: assemble the final report with summary table first
{
  echo "## 📊 NativeAOT Size Analysis"
  echo ""
  echo "Comparing [PR build](${pr_build_url}) against [baseline build](${baseline_build_url})."
  echo ""
  echo "| Platform | Size Difference |"
  echo "|----------|-----------------|"
  for platform in $platforms; do
    if [ -n "${platform_totals[$platform]+x}" ]; then
      echo "| ${platform} | ${platform_totals[$platform]} |"
    fi
  done
  echo ""
  cat "$details_file"
} > "$report_file"

emit_output_value "has_report" "true"
emit_output_value "report_file" "$report_file"
