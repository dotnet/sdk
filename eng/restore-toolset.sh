#!/usr/bin/env bash

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

  # Build dotnetup if not already present (needs SDK to be installed first)
  EnsureDotnetupBuilt

  InstallDotNetSharedFramework "6.0"
  InstallDotNetSharedFramework "7.0"
  InstallDotNetSharedFramework "8.0"
  InstallDotNetSharedFramework "9.0"

  CreateBuildEnvScript
}

# Builds dotnetup if the executable doesn't exist
function EnsureDotnetupBuilt {
  local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local dotnetup_dir="$script_dir/dotnetup"
  local dotnetup_exe="$dotnetup_dir/dotnetup"

  if [[ ! -f "$dotnetup_exe" ]]; then
    echo "Building dotnetup..."
    local dotnetup_project="$repo_root/src/Installer/dotnetup/dotnetup.csproj"

    # Determine RID based on OS
    local rid
    if [[ "$(uname)" == "Darwin" ]]; then
      if [[ "$(uname -m)" == "arm64" ]]; then
        rid="osx-arm64"
      else
        rid="osx-x64"
      fi
    else
      if [[ "$(uname -m)" == "aarch64" ]]; then
        rid="linux-arm64"
      else
        rid="linux-x64"
      fi
    fi

    "$DOTNET_INSTALL_DIR/dotnet" publish "$dotnetup_project" -c Release -r "$rid" -o "$dotnetup_dir"

    if [[ $? -ne 0 ]]; then
      echo "Failed to build dotnetup."
      ExitWithExitCode 1
    fi

    echo "dotnetup built successfully"
  fi
}

# Installs additional shared frameworks for testing purposes
function InstallDotNetSharedFramework {
  local version=$1
  local dotnet_root=$DOTNET_INSTALL_DIR
  local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"

  if [[ ! -d "$fx_dir" ]]; then
    local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local dotnetup_exe="$script_dir/dotnetup/dotnetup"

    "$dotnetup_exe" runtime install "$version" --install-path "$dotnet_root" --no-progress --set-default-install false
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install Shared Framework $version to '$dotnet_root' using dotnetup (exit code '$lastexitcode')."
      ExitWithExitCode $lastexitcode
    fi
  fi
}

function CreateBuildEnvScript {
  mkdir -p $artifacts_dir
  scriptPath="$artifacts_dir/sdk-build-env.sh"
  scriptContents="
#!/usr/bin/env bash
export DOTNET_MULTILEVEL_LOOKUP=0

export DOTNET_ROOT=$DOTNET_INSTALL_DIR
export DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$DOTNET_INSTALL_DIR

export PATH=$DOTNET_INSTALL_DIR:\$PATH
export NUGET_PACKAGES=$NUGET_PACKAGES
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0
"

  echo "$scriptContents" > ${scriptPath}
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
