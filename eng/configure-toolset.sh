# We can't use already installed dotnet cli since we need to install additional shared runtimes.
# We could potentially try to find an existing installation that has all the required runtimes,
# but it's unlikely one will be available.

useInstalledDotNetCli="false"

function GetNativeMachineArchitecture {
  if [[ "$(uname)" == "Darwin" ]] && [[ "$(sysctl -n hw.optional.arm64 2>/dev/null)" == "1" ]]; then
    echo "arm64"
    return
  fi
  case "$(uname -m)" in
    arm64|aarch64) echo "arm64" ;;
    amd64|x86_64) echo "x64" ;;
    armv*l) echo "arm" ;;
    i[3-6]86) echo "x86" ;;
    *) echo "x64" ;;
  esac
}

function IsRunningUnderRosettaOnArm64Mac {
  [[ "$(uname)" == "Darwin" && "$(GetNativeMachineArchitecture)" == "arm64" && "$(uname -m)" == "x86_64" ]]
}

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

  # Re-download dotnetup at most once every 24 hours to avoid unnecessary network calls.
  local skip_download=false
  if [[ -f "$dotnetup_exe" ]]; then
    local current_time
    current_time=$(date +%s)
    local file_time
    file_time=$(stat -c %Y "$dotnetup_exe" 2>/dev/null || stat -f %m "$dotnetup_exe" 2>/dev/null || echo 0)
    local age_seconds=$((current_time - file_time))
    if [[ $age_seconds -lt 86400 ]]; then
      echo "dotnetup binary is less than 24 hours old; skipping re-download."
      skip_download=true
    fi
  fi

  if [[ "$skip_download" == true ]] && IsRunningUnderRosettaOnArm64Mac; then
    echo "Running under Rosetta 2 on arm64 macOS; re-downloading dotnetup for the native architecture."
    skip_download=false
  fi

  if [[ "$skip_download" != true ]]; then
    # Acquire the latest dotnetup daily build using the public install script
    # published at aka.ms (https://aka.ms/dotnetup/get-dotnetup.sh). build.sh runs
    # under `set -e`; guard so we can emit a diagnostic.
    local getter_script
    getter_script="$(mktemp)"
    local getter_url="https://aka.ms/dotnetup/get-dotnetup.sh"
    local downloaded=false
    if command -v curl > /dev/null 2>&1; then
      if curl -fsSL --retry 3 "$getter_url" -o "$getter_script"; then downloaded=true; fi
    elif command -v wget > /dev/null 2>&1; then
      if wget -q -O "$getter_script" "$getter_url"; then downloaded=true; fi
    fi
    if [[ "$downloaded" != true ]] || ! bash "$getter_script" --install-dir "$dotnetup_dir"; then
      rm -f "$getter_script"
      Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to acquire dotnetup."
      ExitWithExitCode 1
    fi
    rm -f "$getter_script"
  fi

  # Keep dotnetup's manifest under artifacts instead of the user's home dir.
  export DOTNET_DOTNETUP_DATA_DIR="$artifacts_dir/.dotnetup"

  "$dotnetup_exe" sdk install "${sdk_versions[@]}" \
    --install-path "$dotnet_root" \
    --untracked \
    --set-default-install false \
    --interactive false
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install .NET SDK(s) '${sdk_versions[*]}' to '$dotnet_root' using dotnetup (exit code '$lastexitcode')."
    ExitWithExitCode $lastexitcode
  fi

  # Record the installed SDK so CleanOutStage0ToolsetsAndRuntimes does not
  # later treat this install as stale and wipe it (forcing a build rerun).
  printf '%s' "$dotnet_sdk_version" > "$dotnet_root/.version"
}

InstallBootstrapSdkWithDotnetup

# Working around issue https://github.com/dotnet/arcade/issues/7327
DisableNativeToolsetInstalls=true
