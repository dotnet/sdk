#!/usr/bin/env bash

set -euo pipefail

print_usage() {
  cat <<'EOF'
Usage: find-builds.sh [options]

Options:
  --azdo-org <name>       Azure DevOps organization
  --azdo-project <name>   Azure DevOps project
  --pipeline-id <id>      Azure DevOps pipeline definition ID
  --pr-number <number>    GitHub pull request number
  --pr-head-sha <sha>     GitHub pull request head commit
  --base-branch <name>    GitHub pull request base branch
  --output-file <path>    File that receives step outputs
  -h, --help              Show this help

Example:
  find-builds.sh \
    --azdo-org dnceng-public \
    --azdo-project public \
    --pipeline-id 101 \
    --pr-number 55938 \
    --pr-head-sha 0123456789abcdef \
    --base-branch main \
    --output-file /tmp/find-builds.out
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

azdo_org=""
azdo_project=""
pipeline_id=""
pr_number=""
pr_head_sha=""
base_branch=""
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
    --pipeline-id)
      require_value "$@"
      pipeline_id="$2"
      shift 2
      ;;
    --pr-number)
      require_value "$@"
      pr_number="$2"
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

if [ -z "$azdo_org" ] || [ -z "$azdo_project" ] || [ -z "$pipeline_id" ] ||
    [ -z "$pr_number" ] || [ -z "$pr_head_sha" ] || [ -z "$base_branch" ] ||
    [ -z "$output_file" ]; then
  echo "All options except --help are required." >&2
  print_usage >&2
  exit 2
fi

azdo_api="https://dev.azure.com/${azdo_org}/${azdo_project}/_apis"

get_latest_build_id() {
  local branch="$1"
  local source_sha="${2:-}"

  # Do not filter by result: failed and canceled builds can still contain useful artifacts.
  curl --fail --silent --show-error --location --retry 3 --max-time 60 --get \
    "${azdo_api}/build/builds" \
    --data-urlencode "definitions=${pipeline_id}" \
    --data-urlencode "branchName=${branch}" \
    --data-urlencode "statusFilter=completed" \
    --data-urlencode "queryOrder=queueTimeDescending" \
    --data-urlencode '$top=1' \
    --data-urlencode "api-version=7.1" |
    jq -r --arg source_sha "$source_sha" '
      .value[0]
      | select($source_sha == "" or .triggerInfo["pr.sourceSha"] == $source_sha)
      | .id // empty'
}

# sourceVersion is the synthetic merge commit, so match the PR head through triggerInfo.
pr_build_id=$(get_latest_build_id "refs/pull/${pr_number}/merge" "$pr_head_sha")

if [ -z "$pr_build_id" ]; then
  emit_warning "No completed build found for PR commit ${pr_head_sha}. Ensure the AzDO pipeline has finished."
  emit_output_value "found" "false"
  exit 0
fi
echo "PR build ID: $pr_build_id"

# Find the most recent baseline build on the base branch
base_build_id=$(get_latest_build_id "refs/heads/${base_branch}")

if [ -z "$base_build_id" ]; then
  emit_warning "No completed baseline build found on ${base_branch}."
  emit_output_value "found" "false"
  exit 0
fi
echo "Baseline build ID: $base_build_id"

emit_output_value "found" "true"
emit_output_value "pr_build_id" "$pr_build_id"
emit_output_value "base_build_id" "$base_build_id"
