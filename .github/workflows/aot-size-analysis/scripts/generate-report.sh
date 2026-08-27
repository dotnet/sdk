#!/usr/bin/env bash

set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage: generate-report.sh [options]

Options:
  --azdo-org <name>        Azure DevOps organization
  --azdo-project <name>    Azure DevOps project
  --platforms <list>       Space-separated platforms to analyze
  --pr-build-id <id>       Pull request build ID
  --base-build-id <id>     Baseline build ID
  --pr-head-sha <sha>      GitHub pull request head commit
  --base-branch <name>     GitHub pull request base branch
  --temp-dir <path>        Directory containing extracted artifacts
  --output-file <path>     File that receives step outputs
  -h, --help               Show this help

Example:
  generate-report.sh \
    --azdo-org dnceng-public \
    --azdo-project public \
    --platforms "Linux_x64_AOT Windows_x64_AOT" \
    --pr-build-id 1569498 \
    --base-build-id 1569822 \
    --pr-head-sha 0123456789abcdef \
    --base-branch main \
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

azdo_org=""
azdo_project=""
platforms=""
pr_build_id=""
base_build_id=""
pr_head_sha=""
base_branch=""
temp_dir=""
output_file=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --azdo-org)
      require_value "$@"
      azdo_org="$2"
      shift 2
      ;;
    --azdo-project)
      require_value "$@"
      azdo_project="$2"
      shift 2
      ;;
    --platforms)
      require_value "$@"
      platforms="$2"
      shift 2
      ;;
    --pr-build-id)
      require_value "$@"
      pr_build_id="$2"
      shift 2
      ;;
    --base-build-id)
      require_value "$@"
      base_build_id="$2"
      shift 2
      ;;
    --pr-head-sha)
      require_value "$@"
      pr_head_sha="$2"
      shift 2
      ;;
    --base-branch)
      require_value "$@"
      base_branch="$2"
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

if [ -z "$azdo_org" ] || [ -z "$azdo_project" ] || [ -z "$platforms" ] ||
    [ -z "$pr_build_id" ] || [ -z "$base_build_id" ] || [ -z "$pr_head_sha" ] ||
    [ -z "$base_branch" ] || [ -z "$temp_dir" ] || [ -z "$output_file" ]; then
  echo "All options except --help are required." >&2
  print_usage >&2
  exit 2
fi

report_file="${temp_dir}/size-report.md"
details_file="${temp_dir}/size-details.md"
azdo_build_url="https://dev.azure.com/${azdo_org}/${azdo_project}/_build/results?buildId="

# Pass 1: run sizoscope-cli for each platform, collect summary data
declare -A platform_totals
has_any_diff=false

for platform in $platforms; do
  start_group "Analyzing ${platform}"

  pr_mstat=$(find "${temp_dir}/pr/${platform}" -name "dotnet-aot.mstat" -type f -print -quit)
  base_mstat=$(find "${temp_dir}/base/${platform}" -name "dotnet-aot.mstat" -type f -print -quit)

  diff_file="${temp_dir}/${platform}-diff.md"
  if sizoscope-cli "$base_mstat" "$pr_mstat" --output "$diff_file"; then
    has_any_diff=true

    # Extract the total size difference from the first line
    total_line=$(head -1 "$diff_file")
    # Expected format: "Total accounted size difference: 781.8 kB"
    total_size=$(echo "$total_line" | sed -n 's/^Total accounted size difference: *//p')
    platform_totals["$platform"]="${total_size:-unknown}"

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
  echo "Comparing PR build [\`${pr_head_sha:0:8}\`](${azdo_build_url}${pr_build_id}) against baseline on \`${base_branch}\` ([build](${azdo_build_url}${base_build_id}))."
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
