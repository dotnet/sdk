#!/usr/bin/env bash

# Shared dotnetup acquisition helpers (architecture detection, cache freshness, download).
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/dotnetup-shared.sh"

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

# Maps a dotnetup component (aspnetcore/windowsdesktop/dotnet) to the name of
# its shared-framework folder under <dotnet root>/shared.
function GetSharedFrameworkName {
  local component=$1
  case "$component" in
    aspnetcore) echo "Microsoft.AspNetCore.App" ;;
    windowsdesktop) echo "Microsoft.WindowsDesktop.App" ;;
    *) echo "Microsoft.NETCore.App" ;;
  esac
}

# Returns the shared-framework directory for a component
# (e.g. <dotnet root>/shared/Microsoft.AspNetCore.App).
function GetSharedFrameworkPath {
  local dotnet_root=$1
  local component=$2
  echo "$dotnet_root/shared/$(GetSharedFrameworkName "$component")"
}

# Returns success (0) if a shared framework matching $version (a major.minor
# channel such as 6.0 or an exact version) is already present for $component.
function IsSharedFrameworkInstalled {
  local dotnet_root=$1
  local component=$2
  local version=$3
  local fx_root
  fx_root="$(GetSharedFrameworkPath "$dotnet_root" "$component")"

  # Only a major.minor channel (e.g. 6.0) should match any patch via a glob. An
  # exact version must match an exact folder so that, for example, 8.0.1 does not
  # spuriously match an installed 8.0.10.
  if [[ "$version" =~ ^[0-9]+\.[0-9]+$ ]]; then
    compgen -G "$fx_root/$version*" > /dev/null 2>&1
  else
    [[ -d "$fx_root/$version" ]]
  fi
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

    if ! IsSharedFrameworkInstalled "$dotnet_root" "$component" "$version"; then
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

  if ! ShouldUseCachedDotnetup "$dotnetup_exe"; then
    if ! AcquireDotnetup "$dotnetup_dir"; then
      Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to acquire dotnetup; falling back to dotnet install script."
      InstallDotNetSharedFrameworksWithInstallScript "$dotnet_root" "$arch" "${specs_to_install[@]}"
      return
    fi
  fi

  RunWithoutErrexit "$dotnetup_exe" runtime install "${specs_to_install[@]}" --install-path "$dotnet_root" --set-default-install false --untracked --interactive false
  local lastexitcode=$_RunWithoutErrexit

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
    local version="$spec"
    if [[ "$spec" == *@* ]]; then
      component="${spec%@*}"
      version="${spec#*@}"
    fi
    # Map dotnetup channel (e.g. "9.0") to the specific version the install
    # script's --version parameter expects (e.g. "9.0.0").
    local install_version="$version"
    if [[ "$install_version" =~ ^[0-9]+\.[0-9]+$ ]]; then
      install_version="$install_version.0"
    fi

    local install_args=(--version "$install_version" --install-dir "$dotnet_root" --runtime "$component" --skip-non-versioned-files)
    if [[ -n "$arch" ]]; then
      install_args+=(--architecture "$arch")
    fi

    # Disable errexit around the install-script call so the exit-code and filesystem checks below always run.
    RunWithoutErrexit bash "$install_script" "${install_args[@]}"
    local lastexitcode=$_RunWithoutErrexit

    # Ensure the download was actually successful to some degree.
    local framework_installed=false
    if IsSharedFrameworkInstalled "$dotnet_root" "$component" "$version"; then
      framework_installed=true
    fi

    # Promote a false success (exit 0 but nothing on disk) to a real failure.
    if [[ $lastexitcode == 0 && "$framework_installed" != true ]]; then
      lastexitcode=1
    fi

    if [[ $lastexitcode != 0 ]]; then
      local architecture_message=""
      if [[ -n "$arch" ]]; then
        architecture_message=" for architecture '$arch'"
      fi
      echo "Failed to install shared framework spec '$spec' to '$dotnet_root' using dotnet install script${architecture_message} (exit code '$lastexitcode', installed '$framework_installed')."
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
  local majorVersion="${dotnetSdkVersion%%.*}"
  local aspnetRuntimePath="$(GetSharedFrameworkPath "$dotnetRoot" aspnetcore)/$majorVersion.*"
  local coreRuntimePath="$(GetSharedFrameworkPath "$dotnetRoot" dotnet)/$majorVersion.*"
  local wdRuntimePath="$(GetSharedFrameworkPath "$dotnetRoot" windowsdesktop)/$majorVersion.*"
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
