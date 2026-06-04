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
    local install_script_arch=""
    local native_arch
    native_arch=$(GetNativeMachineArchitecture)
    if [[ -n "${TARGET_ARCHITECTURE:-}" && "$TARGET_ARCHITECTURE" != "$native_arch" ]]; then
      install_script_arch="$TARGET_ARCHITECTURE"
    fi

    local runtime_specs=("6.0" "7.0" "8.0" "9.0" "10.0")
    if [[ -z "$install_script_arch" ]]; then
      # Also install the exact runtime versions that arcade's toolset requires
      # (from Version.Details.props) so tests can target those specific versions.
      local runtime_version
      runtime_version=$(ReadVersionDetailsProperty "MicrosoftNETCoreAppRefPackageVersion")
      local aspnetcore_version
      aspnetcore_version=$(ReadVersionDetailsProperty "MicrosoftAspNetCoreAppRefPackageVersion")
      if [[ -n "$runtime_version" ]]; then
        runtime_specs+=("$runtime_version")
      fi
      if [[ -n "$aspnetcore_version" ]]; then
        runtime_specs+=("aspnetcore@$aspnetcore_version")
      fi
    fi

    InstallDotNetSharedFrameworks "$install_script_arch" "${runtime_specs[@]}"
  fi

  CreateBuildEnvScript
}

function ReadVersionDetailsProperty {
  local property_name=$1
  sed -n "s:.*<$property_name>\([^<]*\)</$property_name>.*:\1:p" "$repo_root/eng/Version.Details.props" | head -n 1
}

# Installs additional shared frameworks for testing purposes.
function InstallDotNetSharedFrameworks {
  local arch=$1
  shift
  local dotnet_root=$DOTNET_INSTALL_DIR
  local specs_to_install=()

  for spec in "$@"; do
    # Accept either a dotnet runtime version/channel or a component@version spec
    # such as aspnetcore@11.0.0-preview.6. Treat major.minor channels as present
    # if any matching patch (e.g. 6.0.36) exists.
    local component="dotnet"
    local version="$spec"
    if [[ "$spec" == *@* ]]; then
      component="${spec%@*}"
      version="${spec#*@}"
    fi

    local shared_framework_name="Microsoft.NETCore.App"
    if [[ "$component" == "aspnetcore" ]]; then
      shared_framework_name="Microsoft.AspNetCore.App"
    elif [[ "$component" == "windowsdesktop" ]]; then
      shared_framework_name="Microsoft.WindowsDesktop.App"
    fi

    if ! compgen -G "$dotnet_root/shared/$shared_framework_name/$version*" > /dev/null; then
      specs_to_install+=("$spec")
    fi
  done

  if [[ ${#specs_to_install[@]} -eq 0 ]]; then
    return
  fi

  # dotnetup installs runtimes for its own process architecture and has no
  # architecture override (InstallerUtilities.GetDefaultInstallArchitecture uses
  # RuntimeInformation.ProcessArchitecture). On a cross-build (e.g. an x64 host
  # producing an arm64 test payload), dotnetup would silently install the host
  # architecture, so the test runtimes would not match the target Helix queue.
  # When a specific architecture is requested, use the dotnet-install script
  # directly since it honors --architecture.
  if [[ -n "$arch" ]]; then
    InstallDotNetSharedFrameworksWithInstallScript "$dotnet_root" "$arch" "${specs_to_install[@]}"
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

  if [[ "$skip_download" == true && -f "$dotnetup_exe" ]]; then
    # dotnetup installs runtimes for its own process architecture, so a cached
    # binary of the wrong architecture (e.g. an x64 dotnetup left on a reused
    # arm64 agent, or one downloaded under Rosetta 2) would install the wrong
    # runtimes. Verify the cached binary's actual architecture against the native
    # architecture and re-download on mismatch rather than trusting uname.
    local native_arch
    native_arch="$(GetNativeMachineArchitecture)"
    local cached_arch=""
    if [[ "$(uname)" == "Darwin" ]]; then
      if file "$dotnetup_exe" 2>/dev/null | grep -q 'arm64'; then
        cached_arch="arm64"
      elif file "$dotnetup_exe" 2>/dev/null | grep -q 'x86_64'; then
        cached_arch="x64"
      fi
    fi
    if [[ -n "$cached_arch" && "$cached_arch" != "$native_arch" ]]; then
      echo "Cached dotnetup architecture ($cached_arch) does not match native architecture ($native_arch); re-downloading."
      skip_download=false
    fi
  fi

  if [[ "$skip_download" != true ]]; then
    # Acquire the latest dotnetup daily build using the public install script
    # published at aka.ms (https://aka.ms/dotnetup/get-dotnetup.sh). build.sh runs
    # under `set -e`; guard so we can emit a diagnostic and fall back on failure.
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
      Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to acquire dotnetup; falling back to dotnet install script."
      InstallDotNetSharedFrameworksWithInstallScript "$dotnet_root" "$arch" "${specs_to_install[@]}"
      return
    fi
    rm -f "$getter_script"
  fi

  local restore_errexit=false
  if [[ $- == *e* ]]; then
    restore_errexit=true
    set +e
  fi
  "$dotnetup_exe" runtime install "${specs_to_install[@]}" --install-path "$dotnet_root" --set-default-install false --untracked --interactive false
  local lastexitcode=$?
  if [[ "$restore_errexit" == true ]]; then
    set -e
  fi

  if [[ $lastexitcode != 0 ]]; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install shared frameworks (${specs_to_install[*]}) to '$dotnet_root' using dotnetup (exit code '$lastexitcode'); falling back to dotnet install script."
    InstallDotNetSharedFrameworksWithInstallScript "$dotnet_root" "$arch" "${specs_to_install[@]}"
  fi
}

function InstallDotNetSharedFrameworksWithInstallScript {
  local dotnet_root=$1
  local arch=$2
  shift 2

  GetDotNetInstallScript "$dotnet_root"
  local install_script=$_GetDotNetInstallScript

  for spec in "$@"; do
    local component="dotnet"
    local install_version="$spec"
    if [[ "$spec" == *@* ]]; then
      component="${spec%@*}"
      install_version="${spec#*@}"
    fi
    # Map dotnetup channel (e.g. "9.0") to the specific version the install
    # script's --version parameter expects (e.g. "9.0.0").
    if [[ "$install_version" =~ ^[0-9]+\.[0-9]+$ ]]; then
      install_version="$install_version.0"
    fi

    local install_args=(--version "$install_version" --install-dir "$dotnet_root" --runtime "$component" --skip-non-versioned-files)
    if [[ -n "$arch" ]]; then
      install_args+=(--architecture "$arch")
    fi

    bash "$install_script" "${install_args[@]}"
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install shared framework spec '$spec' to '$dotnet_root' using dotnet install script for architecture '$arch' (exit code '$lastexitcode')."
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
