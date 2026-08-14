# We can't use already installed dotnet cli since we need to install additional shared runtimes.
# We could potentially try to find an existing installation that has all the required runtimes,
# but it's unlikely one will be available.

useInstalledDotNetCli="false"

# Shared dotnetup acquisition helpers (architecture detection, cache freshness, download).
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/dotnetup-shared.sh"

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
  # Extract additionalDotNetVersions from global.json using grep/sed (no python dependency).
  # Matches lines like:  "10.0.100-preview.1.12345"  inside the array.
  local in_block=false
  while IFS= read -r line; do
    if [[ "$line" == *"\"additionalDotNetVersions\""* ]]; then
      in_block=true
      continue
    fi
    if [[ "$in_block" == true ]]; then
      if [[ "$line" == *"]"* ]]; then
        break
      fi
      local ver
      ver=$(echo "$line" | sed -n 's/.*"\([^"]*\)".*/\1/p')
      if [[ -n "$ver" ]]; then
        sdk_versions+=("$ver")
      fi
    fi
  done < "$repo_root/global.json"

  local dotnet_root="$repo_root.dotnet"

  echo "Installing SDK(s) '${sdk_versions[*]}' to '$dotnet_root' via dotnetup..."

  local script_dir
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local dotnetup_dir="$script_dir/dotnetup"
  local dotnetup_exe="$dotnetup_dir/dotnetup"

  if ! ShouldUseCachedDotnetup "$dotnetup_exe"; then
    if ! AcquireDotnetup "$dotnetup_dir"; then
      Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to acquire dotnetup. Will fall back to standard dotnet-install script."
      return
    fi
  fi

  # Keep dotnetup's manifest under artifacts instead of the user's home dir.
  export DOTNET_DOTNETUP_DATA_DIR="$artifacts_dir/.dotnetup"

  RunWithoutErrexit "$dotnetup_exe" sdk install "${sdk_versions[@]}" \
    --install-path "$dotnet_root" \
    --untracked \
    --set-default-install false \
    --interactive false
  local lastexitcode=$_RunWithoutErrexit

  if [[ $lastexitcode != 0 ]]; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install .NET SDK(s) '${sdk_versions[*]}' to '$dotnet_root' using dotnetup (exit code '$lastexitcode'). Will fall back to standard dotnet-install script."
    return
  fi

  # Record the installed SDK so CleanOutStage0ToolsetsAndRuntimes does not
  # later treat this install as stale and wipe it (forcing a build rerun).
  printf '%s' "$dotnet_sdk_version" > "$dotnet_root/.version"
}

InstallBootstrapSdkWithDotnetup

# Working around issue https://github.com/dotnet/arcade/issues/7327
DisableNativeToolsetInstalls=true
