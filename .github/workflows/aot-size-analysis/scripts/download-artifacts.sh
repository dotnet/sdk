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

download_artifact() {
  local artifacts_file="$1"
  local artifact_name="$2"
  local destination="$3"
  local download_url
  local archive

  download_url=$(jq -r --arg name "$artifact_name" \
    '[.value[] | select(.name == $name) | .resource.downloadUrl][0] // empty' \
    "$artifacts_file")
  if [ -z "$download_url" ]; then
    return 1
  fi

  archive=$(mktemp "${temp_dir}/aot-size-artifact.XXXXXX.zip")
  if ! curl --fail --silent --show-error --location --retry 3 --max-time 300 \
      "$download_url" --output "$archive"; then
    rm -f "$archive"
    return 1
  fi

  if ! unzip -q "$archive" -d "$destination"; then
    rm -f "$archive"
    return 1
  fi

  rm -f "$archive"
}

for artifact_name in "${artifact_names[@]}"; do
  platform="${artifact_name%_AotSizeAnalysis}"
  start_group "Downloading ${platform}"

  pr_dir="${temp_dir}/pr/${platform}"
  base_dir="${temp_dir}/base/${platform}"
  mkdir -p "$pr_dir" "$base_dir"

  if ! download_artifact "$pr_artifacts_file" "$artifact_name" "$pr_dir"; then
    emit_warning "Failed to download ${artifact_name} from PR build, skipping."
    end_group
    continue
  fi

  if ! download_artifact "$base_artifacts_file" "$artifact_name" "$base_dir"; then
    echo "Baseline build does not have artifact ${artifact_name}, skipping."
    end_group
    continue
  fi

  pr_mstat=$(find "$pr_dir" -name "dotnet-aot.mstat" -type f -print -quit)
  base_mstat=$(find "$base_dir" -name "dotnet-aot.mstat" -type f -print -quit)

  if [ -n "$pr_mstat" ] && [ -n "$base_mstat" ]; then
    platforms="${platforms:+${platforms} }${platform}"
  else
    echo "Could not find .mstat files for ${platform}, skipping."
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
