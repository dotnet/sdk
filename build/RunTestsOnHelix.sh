#!/usr/bin/env bash

install_dependencies() {
  echo "Installing dependencies..."

  if [ -f /etc/os-release ]; then
    . /etc/os-release
    echo "Detected OS: $ID $VERSION_ID"

    case "$ID" in
      centos|rhel)
        sudo dnf install -y epel-release || { echo "Failed to install epel-release"; exit 1; }
        sudo dnf config-manager --set-enabled crb || { echo "Failed to enable CRB repository"; exit 1; }
        sudo dnf install -y zlib-devel libunwind || { echo "Failed to install dependencies"; exit 1; }
        ;;
      fedora)
        sudo dnf install -y clang || { echo "Failed to install clang"; exit 1; }
        ;;
      alpine)
        sudo apk add --no-cache clang || { echo "Failed to install clang"; exit 1; }
        ;;
      *)
        echo "Unsupported OS: $ID. Please install dependencies manually."
        exit 1
        ;;
    esac
  else
    echo "/etc/os-release not found. Cannot determine OS."
    exit 1
  fi

  echo "Dependencies installation complete."
}

install_dependencies

# make NuGet network operations more robust
export NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true
export NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT=6
export NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS=1000

export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

export TestExecutionDirectory=$(realpath "$(mktemp -d "${TMPDIR:-/tmp}"/dotnetSdkTests.XXXXXXXX)")
export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/

export DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory
export DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=$HELIX_CORRELATION_PAYLOAD/r
export DOTNET_SDK_TEST_ASSETS_DIRECTORY=$TestExecutionDirectory/TestAssets

# call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive

# We downloaded a special zip of files to the .nuget folder so add that as a source
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget add source $DOTNET_ROOT/.nuget --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget add source $TestExecutionDirectory/Testpackages --configfile $TestExecutionDirectory/NuGet.config
#Remove feeds not needed for tests
dotnet nuget remove source dotnet6-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet6-internal-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet7-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet7-internal-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source richnav --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source vs-impl --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-tools-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-eng --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
