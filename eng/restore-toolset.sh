#!/usr/bin/env bash

# Detect native machine architecture, handling macOS Rosetta 2
# where uname -m may report x86_64 on arm64 hardware.
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

function InitializeCustomSDKToolset {
  if [[ "$restore" != true ]]; then
    return
  fi

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them when building in the VMR.
  if [[ $from_vmr == true ]]; then
    return
  fi

  DISTRO=
  MAJOR_VERSION=
  if [ -e /etc/os-release ]; then
      . /etc/os-release
      DISTRO="$ID"
      MAJOR_VERSION="${VERSION_ID:+${VERSION_ID%%.*}}"
  fi

  InitializeDotNetCli true

  # Redirect dotnetup data directory under artifacts so build scripts
  # don't read/write the user's home-folder manifest.
  export DOTNET_DOTNETUP_DATA_DIR="$artifacts_dir/.dotnetup"

  # The following shared frameworks are only needed for testing.
  # Set DOTNET_INSTALL_TEST_RUNTIMES=false to skip (e.g. cross-build containers with limited disk).
  if [[ "${DOTNET_INSTALL_TEST_RUNTIMES:-true}" != "false" ]]; then
    local fallback_arch="${TARGET_ARCHITECTURE:-}"
    local native_arch
    native_arch=$(GetNativeMachineArchitecture)
    if [[ -z "$fallback_arch" && "$native_arch" == "arm64" && "$(uname -m)" == "x86_64" ]]; then
      fallback_arch="$native_arch"
    fi

    InstallDotNetSharedFrameworks "$fallback_arch" "6.0" "7.0" "8.0" "9.0" "10.0"
  fi

  CreateBuildEnvScript
}

# Installs additional shared frameworks for testing purposes.
function InstallDotNetSharedFrameworks {
  local arch=$1
  shift
  local dotnet_root=$DOTNET_INSTALL_DIR
  local versions_to_install=()

  for version in "$@"; do
    # Accept either an exact version or a major.minor channel; treat the
    # framework as present if any matching patch (e.g. 6.0.36) exists.
    if ! compgen -G "$dotnet_root/shared/Microsoft.NETCore.App/$version*" > /dev/null; then
      versions_to_install+=("$version")
    fi
  done

  if [[ ${#versions_to_install[@]} -eq 0 ]]; then
    return
  fi

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

  if [[ "$skip_download" != true ]]; then
    # Acquire the latest dotnetup daily build using the in-repo install script.
    # build.sh runs under `set -e`; guard so we can emit a diagnostic.
    if ! "$repo_root/scripts/get-dotnetup.sh" --install-dir "$dotnetup_dir"; then
      Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to acquire dotnetup; falling back to dotnet install script."
      InstallDotNetSharedFrameworksWithInstallScript "$dotnet_root" "$arch" "${versions_to_install[@]}"
      return
    fi
  fi

  local restore_errexit=false
  if [[ $- == *e* ]]; then
    restore_errexit=true
    set +e
  fi
  "$dotnetup_exe" runtime install "${versions_to_install[@]}" --install-path "$dotnet_root" --set-default-install false --untracked --interactive false
  local lastexitcode=$?
  if [[ "$restore_errexit" == true ]]; then
    set -e
  fi

  if [[ $lastexitcode != 0 ]]; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install shared frameworks (${versions_to_install[*]}) to '$dotnet_root' using dotnetup (exit code '$lastexitcode'); falling back to dotnet install script."
    InstallDotNetSharedFrameworksWithInstallScript "$dotnet_root" "$arch" "${versions_to_install[@]}"
  fi
}

function InstallDotNetSharedFrameworksWithInstallScript {
  local dotnet_root=$1
  local arch=$2
  shift 2

  GetDotNetInstallScript "$dotnet_root"
  local install_script=$_GetDotNetInstallScript

  for version in "$@"; do
    local install_version="$version"
    if [[ "$install_version" =~ ^[0-9]+\.[0-9]+$ ]]; then
      install_version="$install_version.0"
    fi

    local install_args=(--version "$install_version" --install-dir "$dotnet_root" --runtime "dotnet" --skip-non-versioned-files)
    if [[ -n "$arch" ]]; then
      install_args+=(--architecture "$arch")
    fi

    bash "$install_script" "${install_args[@]}"
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install shared framework $version to '$dotnet_root' using dotnet install script for architecture '$arch' (exit code '$lastexitcode')."
      ExitWithExitCode $lastexitcode
    fi
  done
}

function CreateBuildEnvScript {
  mkdir -p $artifacts_dir
  scriptPath="$artifacts_dir/sdk-build-env.sh"
  scriptContents="
#!/usr/bin/env bash

export DOTNET_ROOT=$DOTNET_INSTALL_DIR
export DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$DOTNET_INSTALL_DIR

export PATH=$DOTNET_INSTALL_DIR:\$PATH
export NUGET_PACKAGES=$NUGET_PACKAGES
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0
"

  echo "$scriptContents" > ${scriptPath}
  chmod +x ${scriptPath}
}

# ReadVersionFromJson [json key]
function ReadGlobalVersion {
  local key=$1

  if command -v jq &> /dev/null; then
    _ReadGlobalVersion="$(jq -r ".[] | select(has(\"$key\")) | .\"$key\"" "$global_json_file")"
  elif [[ "$(cat "$global_json_file")" =~ \"$key\"[[:space:]\:]*\"([^\"]+) ]]; then
    _ReadGlobalVersion=${BASH_REMATCH[1]}
  fi

  if [[ -z "$_ReadGlobalVersion" ]]; then
    Write-PipelineTelemetryError -category 'Build' "Error: Cannot find \"$key\" in $global_json_file"
    ExitWithExitCode 1
  fi
}

function CleanOutStage0ToolsetsAndRuntimes {
  ReadGlobalVersion "dotnet"
  local dotnetSdkVersion=$_ReadGlobalVersion
  local dotnetRoot=$DOTNET_INSTALL_DIR
  local versionPath="$dotnetRoot/.version"
  local majorVersion="${dotnetSdkVersion:0:1}"
  local aspnetRuntimePath="$dotnetRoot/shared/Microsoft.AspNetCore.App/$majorVersion.*"
  local coreRuntimePath="$dotnetRoot/shared/Microsoft.NETCore.App/$majorVersion.*"
  local wdRuntimePath="$dotnetRoot/shared/Microsoft.WindowsDesktop.App/$majorVersion.*"
  local sdkPath="$dotnetRoot/sdk/*"

  if [ -f "$versionPath" ]; then
    local lastInstalledSDK=$(cat $versionPath)
    if [[ "$lastInstalledSDK" != "$dotnetSdkVersion" ]]; then
      echo $dotnetSdkVersion > $versionPath
      rm -rf $aspnetRuntimePath
      rm -rf $coreRuntimePath
      rm -rf $wdRuntimePath
      rm -rf $sdkPath
      rm -rf "$dotnetRoot/packs"
      rm -rf "$dotnetRoot/sdk-manifests"
      rm -rf "$dotnetRoot/templates"
      Write-PipelineTelemetryError -category 'Build' "Found old version of SDK, cleaning out folder. Please run build.sh again"
      ExitWithExitCode 1
    fi
  else
    echo $dotnetSdkVersion > $versionPath
  fi
}

InitializeCustomSDKToolset

CleanOutStage0ToolsetsAndRuntimes
