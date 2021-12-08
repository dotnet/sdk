function InitializeCustomSDKToolset {
  if [[ "$restore" != true ]]; then
    return
  fi

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if [[ "${DotNetBuildFromSource:-}" == "true" ]]; then
    return
  fi

  InitializeDotNetCli true
  if [[ "$DISTRO" != "ubuntu" || "$MAJOR_VERSION" -le 16 ]]; then
    InstallDotNetSharedFramework "1.0.5"
    InstallDotNetSharedFramework "1.1.2"
  fi
  InstallDotNetSharedFramework "2.1.0"
  InstallDotNetSharedFramework "2.2.8"
}

# Installs additional shared frameworks for testing purposes
function InstallDotNetSharedFramework {
  local version=$1
  local dotnet_root=$DOTNET_INSTALL_DIR 
  local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"

  if [[ ! -d "$fx_dir" ]]; then
    GetDotNetInstallScript "$dotnet_root"
    local install_script=$_GetDotNetInstallScript

    bash "$install_script" --version $version --install-dir "$dotnet_root" --runtime "dotnet"
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install Shared Framework $version to '$dotnet_root' (exit code '$lastexitcode')."
      ExitWithExitCode $lastexitcode
    fi
  fi
}

InitializeCustomSDKToolset
