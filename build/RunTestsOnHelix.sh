#!/usr/bin/env bash

# make NuGet network operations more robust
export NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true
export NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT=6
export NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS=1000

export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

# Set DOTNET_HOST_PATH so MSBuild task hosts can locate the dotnet executable.
# Without this, tasks from NuGet packages that use TaskHostFactory (e.g. ComputeWasmBuildAssets
# from WebAssembly SDK, ComputeManagedAssemblies from ILLink) fail with MSB4216 on macOS
# because the task host process cannot find the dotnet host to launch.
export DOTNET_HOST_PATH=$DOTNET_ROOT/dotnet

export TestExecutionDirectory=$(realpath "$(mktemp -d "${TMPDIR:-/tmp}"/dotnetSdkTests.XXXXXXXX)")
export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/

export DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory
export DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=$HELIX_CORRELATION_PAYLOAD/r
export DOTNET_SDK_TEST_ASSETS_DIRECTORY=$TestExecutionDirectory/TestAssets

# call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive

dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget add source $TestExecutionDirectory/Testpackages --configfile $TestExecutionDirectory/NuGet.config
# Remove feeds not needed for tests. Use || true to avoid errors when a source
# doesn't exist (e.g. internal-transport feeds are only present in internal builds).
dotnet nuget remove source dotnet6-transport --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source dotnet6-internal-transport --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source dotnet7-transport --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source dotnet7-internal-transport --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source richnav --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source vs-impl --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source dotnet-libraries-transport --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source dotnet-tools-transport --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source dotnet-libraries --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget remove source dotnet-eng --configfile $TestExecutionDirectory/NuGet.config || true
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config

