#!/usr/bin/env bash

set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage: download-artifacts.sh [options]

Options:
  --azdo-org <name>        Azure DevOps organization
  --azdo-project <name>    Azure DevOps project
  --pr-build-id <id>       Pull request build ID
  --base-build-id <id>     Baseline build ID
  --temp-dir <path>        Directory for downloads and extracted artifacts
  --output-file <path>     File that receives step outputs
  -h, --help               Show this help

Example:
  download-artifacts.sh \
    --azdo-org dnceng-public \
    --azdo-project public \
    --pr-build-id 1569498 \
    --base-build-id 1569822 \
    --temp-dir /tmp/aot-size-analysis \
    --output-file /tmp/download-artifacts.out
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
pr_build_id=""
base_build_id=""
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

if [ -z "$azdo_org" ] || [ -z "$azdo_project" ] || [ -z "$pr_build_id" ] ||
    [ -z "$base_build_id" ] || [ -z "$temp_dir" ] || [ -z "$output_file" ]; then
  echo "All options except --help are required." >&2
  print_usage >&2
  exit 2
fi

azdo_api="https://dev.azure.com/${azdo_org}/${azdo_project}/_apis"
pr_artifacts_file="${temp_dir}/pr-artifacts.json"
base_artifacts_file="${temp_dir}/base-artifacts.json"

curl --fail --silent --show-error --location --retry 3 --max-time 60 \
  "${azdo_api}/build/builds/${pr_build_id}/artifacts?api-version=7.1" \
  --output "$pr_artifacts_file"
curl --fail --silent --show-error --location --retry 3 --max-time 60 \
  "${azdo_api}/build/builds/${base_build_id}/artifacts?api-version=7.1" \
  --output "$base_artifacts_file"

# List artifacts from PR build that match _AotSizeAnalysis
mapfile -t artifact_names < <(
  jq -r '.value[] | select(.name | endswith("_AotSizeAnalysis")) | .name' "$pr_artifacts_file"
)

if [ "${#artifact_names[@]}" -eq 0 ]; then
  emit_warning "No _AotSizeAnalysis artifacts found in PR build ${pr_build_id}."
  emit_output_value "has_artifacts" "false"
  exit 0
fi

echo "Found artifacts: ${artifact_names[*]}"
platforms=""
zip_dir=$(mktemp -d "${temp_dir}/aot-size-zips.XXXXXX")
trap 'rm -rf "$zip_dir"' EXIT
declare -a pair_platforms=() pr_archives=() base_archives=() curl_args=()

# Build one curl invocation containing every complete PR/baseline artifact pair.
for artifact_name in "${artifact_names[@]}"; do
  platform="${artifact_name%_AotSizeAnalysis}"
  pr_download_url=$(jq -r --arg name "$artifact_name" \
    '[.value[] | select(.name == $name) | .resource.downloadUrl][0] // empty' \
    "$pr_artifacts_file")
  base_download_url=$(jq -r --arg name "$artifact_name" \
    '[.value[] | select(.name == $name) | .resource.downloadUrl][0] // empty' \
    "$base_artifacts_file")

  if [ -z "$pr_download_url" ]; then
    emit_warning "PR build does not have a download URL for ${artifact_name}, skipping."
    continue
  fi
  if [ -z "$base_download_url" ]; then
    emit_warning "Baseline build does not have artifact ${artifact_name}, skipping."
    continue
  fi

  pair_index="${#pair_platforms[@]}"
  pr_archive="${zip_dir}/${pair_index}-pr.zip"
  base_archive="${zip_dir}/${pair_index}-base.zip"
  pair_platforms+=("$platform")
  pr_archives+=("$pr_archive")
  base_archives+=("$base_archive")
  curl_args+=(--url "$pr_download_url" --output "$pr_archive")
  curl_args+=(--url "$base_download_url" --output "$base_archive")
done

if [ "${#pair_platforms[@]}" -eq 0 ]; then
  emit_warning "No matching PR and baseline artifact pairs found."
  emit_output_value "has_artifacts" "false"
  exit 0
fi

start_group "Downloading AOT artifact ZIPs"
set +e
curl --fail --show-error --location --retry 3 --max-time 300 \
  --parallel --parallel-immediate --parallel-max 4 --progress-bar --remove-on-error \
  --write-out 'Downloaded %{filename_effective}: %{size_download} bytes in %{time_total}s at %{speed_download} B/s (exit %{exitcode})\n' \
  "${curl_args[@]}"
curl_exit=$?
set -e
end_group

if [ "$curl_exit" -ne 0 ]; then
  emit_warning "One or more artifact downloads failed; continuing with successful pairs."
fi

for pair_index in "${!pair_platforms[@]}"; do
  platform="${pair_platforms[$pair_index]}"
  pr_archive="${pr_archives[$pair_index]}"
  base_archive="${base_archives[$pair_index]}"
  start_group "Extracting ${platform}"
  pr_dir="${temp_dir}/pr/${platform}"
  base_dir="${temp_dir}/base/${platform}"
  mkdir -p "$pr_dir" "$base_dir"

  pair_valid=true
  if [ ! -s "$pr_archive" ]; then
    emit_warning "PR artifact download for ${platform} is missing or empty."
    pair_valid=false
  elif ! unzip -q "$pr_archive" -d "$pr_dir"; then
    emit_warning "Failed to extract PR artifact for ${platform}."
    pair_valid=false
  fi

  if [ ! -s "$base_archive" ]; then
    emit_warning "Baseline artifact download for ${platform} is missing or empty."
    pair_valid=false
  elif ! unzip -q "$base_archive" -d "$base_dir"; then
    emit_warning "Failed to extract baseline artifact for ${platform}."
    pair_valid=false
  fi
  rm -f "$pr_archive" "$base_archive"

  pr_mstat=$(find "$pr_dir" -name "dotnet-aot.mstat" -type f -print -quit)
  base_mstat=$(find "$base_dir" -name "dotnet-aot.mstat" -type f -print -quit)

  if [ "$pair_valid" = true ] && [ -n "$pr_mstat" ] && [ -n "$base_mstat" ]; then
    platforms="${platforms:+${platforms} }${platform}"
  else
    emit_warning "Could not validate an .mstat pair for ${platform}, skipping."
  fi

  end_group
done

if [ -z "$platforms" ]; then
  emit_warning "No matching .mstat file pairs found."
  emit_output_value "has_artifacts" "false"
  exit 0
fi

emit_output_value "has_artifacts" "true"
emit_output_value "platforms" "$(echo "$platforms" | tr ' ' '\n' | sort | tr '\n' ' ')"
