#!/usr/bin/env bash

install_dependencies() {
  echo "📦 Installing dependencies..."

  if [ -f /etc/os-release ]; then
    echo "🔍 Detected /etc/os-release"
    . /etc/os-release
    echo "🧾 OS Info: ID=$ID, VERSION_ID=$VERSION_ID, PRETTY_NAME=$PRETTY_NAME"

    case "$ID" in
      ubuntu|debian)
        echo "📦 Using apt-get to install dependencies..."
        sudo apt-get update
        sudo apt-get install -y zlib1g-dev libunwind8 clang lld || {
          echo "❌ Failed to install dependencies with apt-get"
          exit 1
        }
        ;;
      centos|rhel)
        echo "📦 Using yum to install dependencies..."
        sudo yum install -y zlib-devel libunwind clang lld || {
          echo "❌ Failed to install dependencies with yum"
          exit 1
        }
        ;;
      fedora)
        echo "📦 Using dnf to install dependencies..."
        sudo dnf install -y zlib-devel libunwind clang lld || {
          echo "❌ Failed to install dependencies with dnf"
          exit 1
        }
        ;;
      alpine)
        echo "📦 Using apk to install dependencies..."
        sudo apk add --no-cache zlib-dev libunwind clang lld || {
          echo "❌ Failed to install dependencies with apk"
          exit 1
        }
        ;;
      *)
        echo "⚠️ Unsupported OS: $ID. Please install dependencies manually."
        ;;
    esac
  else
    echo "⚠️ /etc/os-release not found. Cannot determine OS."
  fi

  echo "✅ Dependency installation complete."
  echo "🔍 Verifying installed tools..."
  command -v clang && clang --version || echo "❌ clang not found"
  command -v gcc && gcc --version || echo "❌ gcc not found"
  command -v lld || echo "❌ lld not found"
}

install_dependencies

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
