# We can't use already installed dotnet cli since we need to install additional shared runtimes.
# We could potentially try to find an existing installation that has all the required runtimes,
# but it's unlikely one will be available.

useInstalledDotNetCli="false"

# Pre-install the bootstrap SDK pinned in global.json using dotnetup into the
# repo-local .dotnet directory that arcade's InitializeDotNetCli will pick up.
#
# Skipped during VMR / source-build (no network) and when --restore was not requested.
function InstallBootstrapSdkWithDotnetup {
  ReadGlobalVersion "dotnet"
  local dotnet_sdk_version=$_ReadGlobalVersion
  if [[ "$restore" != true || "$from_vmr" == true || -z "$dotnet_sdk_version" ]]; then
    return
  fi

  # Collect all SDK versions to install (primary + any additional).
  local sdk_versions=("$dotnet_sdk_version")
  local additional
  additional=$(python3 -c "
import json, sys
with open('$repo_root/global.json') as f:
    g = json.load(f)
for v in g.get('tools', {}).get('additionalDotNetVersions', []):
    if v: print(v)
" 2>/dev/null || true)
  if [[ -n "$additional" ]]; then
    while IFS= read -r ver; do
      sdk_versions+=("$ver")
    done <<< "$additional"
  fi

  # Filter out versions already present in either an externally provided
  # dotnet root or the repo-local one.
  local dotnet_root="$repo_root.dotnet"
  local versions_to_install=()
  for ver in "${sdk_versions[@]}"; do
    local already_installed=false
    for root in "${DOTNET_INSTALL_DIR:-}" "$dotnet_root"; do
      if [[ -n "$root" && -d "$root/sdk/$ver" ]]; then
        echo "Bootstrap SDK '$ver' already present at '$root'; skipping."
        already_installed=true
        break
      fi
    done
    if [[ "$already_installed" != true ]]; then
      versions_to_install+=("$ver")
    fi
  done

  if [[ ${#versions_to_install[@]} -eq 0 ]]; then
    printf '%s' "$dotnet_sdk_version" > "$dotnet_root/.version"
    return
  fi

  echo "Installing SDK(s) '${versions_to_install[*]}' to '$dotnet_root' via dotnetup..."

  local script_dir
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local dotnetup_dir="$script_dir/dotnetup"
  local dotnetup_exe="$dotnetup_dir/dotnetup"

  # build.sh runs under `set -e`; guard so we can emit a diagnostic.
  if ! "$repo_root/scripts/get-dotnetup.sh" --install-dir "$dotnetup_dir"; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to acquire dotnetup."
    ExitWithExitCode 1
  fi

  # Keep dotnetup's manifest under artifacts instead of the user's home dir.
  export DOTNET_DOTNETUP_DATA_DIR="$artifacts_dir/.dotnetup"

  "$dotnetup_exe" sdk install "${versions_to_install[@]}" \
    --install-path "$dotnet_root" \
    --untracked \
    --set-default-install false \
    --interactive false
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install .NET SDK(s) '${versions_to_install[*]}' to '$dotnet_root' using dotnetup (exit code '$lastexitcode')."
    ExitWithExitCode $lastexitcode
  fi

  # Record the installed SDK so CleanOutStage0ToolsetsAndRuntimes does not
  # later treat this install as stale and wipe it (forcing a build rerun).
  printf '%s' "$dotnet_sdk_version" > "$dotnet_root/.version"
}

InstallBootstrapSdkWithDotnetup

# Working around issue https://github.com/dotnet/arcade/issues/7327
DisableNativeToolsetInstalls=true
