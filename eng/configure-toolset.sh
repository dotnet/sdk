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

  # Skip if the pinned SDK is already present in either an externally provided
  # dotnet root or the repo-local one.
  local dotnet_root="$repo_root.dotnet"
  for root in "${DOTNET_INSTALL_DIR:-}" "$dotnet_root"; do
    if [[ -n "$root" && -d "$root/sdk/$dotnet_sdk_version" ]]; then
      echo "Bootstrap SDK '$dotnet_sdk_version' already present at '$root'; skipping dotnetup install."
      return
    fi
  done

  echo "Installing bootstrap SDK '$dotnet_sdk_version' to '$dotnet_root' via dotnetup..."

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

  "$dotnetup_exe" sdk install "$dotnet_sdk_version" \
    --install-path "$dotnet_root" \
    --untracked \
    --set-default-install false \
    --interactive false
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install .NET SDK '$dotnet_sdk_version' to '$dotnet_root' using dotnetup (exit code '$lastexitcode')."
    ExitWithExitCode $lastexitcode
  fi
}

InstallBootstrapSdkWithDotnetup

# Working around issue https://github.com/dotnet/arcade/issues/7327
DisableNativeToolsetInstalls=true
