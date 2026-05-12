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

  local native_arch
  native_arch=$(GetNativeMachineArchitecture)

  # On macOS arm64 running under Rosetta 2, uname -m reports x86_64 and
  # Arcade installs the x64 SDK. Reinstall with the native architecture so
  # the dotnet host and shared frameworks match the hardware.
  if [[ "$native_arch" == "arm64" ]] && [[ "$(uname -m)" == "x86_64" ]]; then
    ReadGlobalVersion "dotnet"
    local dotnet_sdk_version=$_ReadGlobalVersion
    echo "Native architecture is arm64 but SDK was installed as x64 (Rosetta 2). Reinstalling for arm64..."
    rm -rf "$DOTNET_INSTALL_DIR/sdk/$dotnet_sdk_version"
    InstallDotNet "$DOTNET_INSTALL_DIR" "$dotnet_sdk_version" "$native_arch" "sdk" "false" $runtime_source_feed $runtime_source_feed_key

    # Remove any cached x64 test runtimes from previous builds
    for fx_version in "6.0.0" "7.0.0" "8.0.0" "9.0.0" "10.0.0"; do
      rm -rf "$DOTNET_INSTALL_DIR/shared/Microsoft.NETCore.App/$fx_version"
    done
  fi

  InstallDotNetSharedFramework "6.0.0" "$native_arch"
  InstallDotNetSharedFramework "7.0.0" "$native_arch"
  InstallDotNetSharedFramework "8.0.0" "$native_arch"
  InstallDotNetSharedFramework "9.0.0" "$native_arch"
  InstallDotNetSharedFramework "10.0.0" "$native_arch"

  CreateBuildEnvScript
}

# Installs additional shared frameworks for testing purposes
function InstallDotNetSharedFramework {
  local version=$1
  local arch=${2:-}
  local dotnet_root=$DOTNET_INSTALL_DIR
  local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"

  if [[ ! -d "$fx_dir" ]]; then
    GetDotNetInstallScript "$dotnet_root"
    local install_script=$_GetDotNetInstallScript

    local install_args=(--version $version --install-dir "$dotnet_root" --runtime "dotnet" --skip-non-versioned-files)
    if [[ -n "$arch" ]]; then
      install_args+=(--architecture "$arch")
    fi

    bash "$install_script" "${install_args[@]}"
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install Shared Framework $version to '$dotnet_root' (exit code '$lastexitcode')."
      ExitWithExitCode $lastexitcode
    fi
  fi
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
