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

  # Redirect dotnetup data directory under artifacts so build scripts
  # don't read/write the user's home-folder manifest.
  export DOTNET_DOTNETUP_DATA_DIR="$artifacts_dir/.dotnetup"

  # The following shared frameworks are only needed for testing.
  # Set DOTNET_INSTALL_TEST_RUNTIMES=false to skip (e.g. cross-build containers with limited disk).
  if [[ "${DOTNET_INSTALL_TEST_RUNTIMES:-true}" != "false" ]]; then
    InstallDotNetSharedFrameworks "6.0.0" "7.0.0" "8.0.0" "9.0.0" "10.0.0"
  fi

  CreateBuildEnvScript
}

# Installs additional shared frameworks for testing purposes (batched, concurrent)
function InstallDotNetSharedFrameworks {
  local dotnet_root=$DOTNET_INSTALL_DIR
  local versions_to_install=()

  for version in "$@"; do
    local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"
    if [[ ! -d "$fx_dir" ]]; then
      versions_to_install+=("$version")
    fi
  done

  if [[ ${#versions_to_install[@]} -eq 0 ]]; then
    return
  fi

  local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local dotnetup_dir="$script_dir/dotnetup"
  local dotnetup_exe="$dotnetup_dir/dotnetup"

  # Acquire the latest dotnetup daily build using the in-repo install script.
  # build.sh runs under `set -e`, so we have to invoke the script in a way that
  # doesn't trigger errexit; otherwise the script's non-zero exit aborts the
  # whole build before our diagnostic error message can fire.
  if ! "$repo_root/scripts/get-dotnetup.sh" --install-dir "$dotnetup_dir"; then
    echo "Failed to acquire dotnetup."
    ExitWithExitCode 1
  fi

  "$dotnetup_exe" runtime install "${versions_to_install[@]}" --install-path "$dotnet_root" --no-progress --set-default-install false --untracked --interactive false
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to install shared frameworks (${versions_to_install[*]}) to '$dotnet_root' using dotnetup (exit code '$lastexitcode')."
    ExitWithExitCode $lastexitcode
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
