#!/usr/bin/env bash

set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage: generate-report.sh [options]

Options:
  --platforms <list>           Space-separated platforms to analyze
  --pr-build-url <url>         Pull request build link
  --baseline-build-url <url>   Baseline build link
  --workflow-run-url <url>     GitHub Actions workflow run containing report artifacts
  --temp-dir <path>            Directory containing extracted artifacts
  --output-file <path>         File that receives step outputs
  -h, --help                   Show this help

Example:
  generate-report.sh \
    --platforms "Linux_x64_AOT Windows_x64_AOT" \
    --pr-build-url "https://dev.azure.com/dnceng-public/public/_build/results?buildId=1569498" \
    --baseline-build-url "https://dev.azure.com/dnceng-public/public/_build/results?buildId=1569822" \
    --workflow-run-url "https://github.com/dotnet/sdk/actions/runs/123456" \
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

escape_html() {
  sed \
    -e 's/&/\&amp;/g' \
    -e 's/</\&lt;/g' \
    -e 's/>/\&gt;/g'
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
workflow_run_url=""
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
    --workflow-run-url)
      require_value "$@"
      workflow_run_url="$2"
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
    [ -z "$workflow_run_url" ] || [ -z "$temp_dir" ] || [ -z "$output_file" ]; then
  echo "All options except --help are required." >&2
  print_usage >&2
  exit 2
fi

report_file="${temp_dir}/size-report.md"
details_file="${temp_dir}/size-details.md"
interactive_report_dir="${temp_dir}/nativeaot-size-report"
html_report_file="${interactive_report_dir}/index.html"
mkdir -p "$interactive_report_dir"
: > "$details_file"

# Pass 1: run sizoscope-cli for each platform, collect summary data
declare -A platform_totals
has_any_diff=false
has_any_treemap=false
has_any_detail_report=false

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

    treemap_file="${interactive_report_dir}/${platform}.svg"
    if pwsh -NoLogo -NoProfile -File "$(dirname "$0")/generate-treemap.ps1" \
        -InputPath "$diff_file" \
        -OutputPath "$treemap_file" \
        -Platform "$platform"; then
      has_any_treemap=true
    else
      emit_warning "Treemap generation failed for ${platform}."
    fi

    pr_scan=$(find "${temp_dir}/pr/${platform}" -name "dotnet-aot.scan.dgml.xml" -type f -print -quit)
    if [ -n "$pr_scan" ]; then
      detail_report_file="${interactive_report_dir}/${platform}-details.html"
      generic_csv_file="${interactive_report_dir}/${platform}-generic-instantiations.csv"
      single_dependency_csv_file="${interactive_report_dir}/${platform}-single-dependency.csv"
      if dotnet run \
          --project "$(dirname "$0")/mstat-report/mstat-report.csproj" \
          --configuration Release \
          --no-restore \
          -- \
          "$pr_mstat" \
          "$pr_scan" \
          "$detail_report_file" \
          "$generic_csv_file" \
          "$single_dependency_csv_file"; then
        has_any_detail_report=true
      else
        emit_warning "Detailed MSTAT report generation failed for ${platform}."
      fi
    else
      emit_warning "No scan dependency graph was found for ${platform}; skipping the detailed report."
    fi

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

if [ "$has_any_treemap" = true ] || [ "$has_any_detail_report" = true ]; then
  {
    cat <<EOF
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>NativeAOT Size Analysis</title>
  <style>
    :root{color-scheme:dark}body{max-width:1500px;margin:auto;padding:24px;background:#0d1117;color:#f0f3f6;font-family:system-ui,sans-serif}
    a{color:#58a6ff}table{border-collapse:collapse}th,td{padding:7px 12px;border:1px solid #36404a;text-align:left}
    details{margin:28px 0}summary{font-size:20px;font-weight:600;cursor:pointer}object{width:100%;margin-top:16px}pre{overflow:auto}
  </style>
</head>
<body>
  <h1>NativeAOT Size Analysis</h1>
  <p>Comparing <a href="$(printf '%s' "$pr_build_url" | escape_html)">PR build</a> against <a href="$(printf '%s' "$baseline_build_url" | escape_html)">baseline build</a>.</p>
  <table><thead><tr><th>Platform</th><th>Size Difference</th></tr></thead><tbody>
EOF
    for platform in $platforms; do
      if [ -n "${platform_totals[$platform]+x}" ]; then
        printf '    <tr><td>%s</td><td>%s</td></tr>\n' \
          "$(printf '%s' "$platform" | escape_html)" \
          "$(printf '%s' "${platform_totals[$platform]}" | escape_html)"
      fi
    done
    printf '  </tbody></table>\n'
    for platform in $platforms; do
      treemap_file="${interactive_report_dir}/${platform}.svg"
      detail_report_file="${interactive_report_dir}/${platform}-details.html"
      diff_file="${temp_dir}/${platform}-diff.md"
      if [ ! -s "$treemap_file" ] && [ ! -s "$detail_report_file" ]; then
        continue
      fi

      escaped_platform=$(printf '%s' "$platform" | escape_html)
      printf '  <details open><summary>%s</summary>\n' "$escaped_platform"
      if [ -s "$detail_report_file" ]; then
        printf '    <p><a href="%s-details.html">Explore members, generic instantiations, and retention paths</a> · <a href="%s-generic-instantiations.csv">Generic CSV</a> · <a href="%s-single-dependency.csv">Single-dependency CSV</a></p>\n' "$escaped_platform" "$escaped_platform" "$escaped_platform"
      fi
      if [ -s "$treemap_file" ]; then
        escaped_file_name=$(printf '%s' "${platform}.svg" | escape_html)
        printf '    <object data="%s" type="image/svg+xml" aria-label="%s NativeAOT size diff"></object>\n' "$escaped_file_name" "$escaped_platform"
      fi
      printf '    <details><summary>Text size diff</summary><pre>'
      escape_html < "$diff_file"
      printf '</pre></details>\n'
      printf '  </details>\n'
    done
    cat <<'EOF'
</body>
</html>
EOF
  } > "$html_report_file"
fi

has_visualizations=false
if [ "$has_any_treemap" = true ] || [ "$has_any_detail_report" = true ]; then
  has_visualizations=true
fi

# Pass 2: assemble the final report with summary table first
{
  echo "## 📊 NativeAOT Size Analysis"
  echo ""
  echo "Comparing [PR build](${pr_build_url}) against [baseline build](${baseline_build_url})."
  echo ""
  if [ "$has_visualizations" = true ]; then
    echo "Download the \`nativeaot-size-report\` artifact from the [workflow run](${workflow_run_url}) for hoverable size-diff treemaps and a member-level explorer with generic-instantiation and retention-path data."
    echo ""
  fi
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
emit_output_value "has_visualizations" "$has_visualizations"
emit_output_value "interactive_report_dir" "$interactive_report_dir"
