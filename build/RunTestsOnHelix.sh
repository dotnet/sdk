#!/usr/bin/env bash

# make NuGet network operations more robust
export NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true
export NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT=6
export NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS=1000

export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

export TestExecutionDirectory=$(pwd)/testExecutionDirectory
mkdir $TestExecutionDirectory
export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/

# call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive

# We downloaded a special zip of files to the .nuget folder so add that as a source
dotnet new nugetconfig -o $TestExecutionDirectory
dotnet nuget add source $DOTNET_ROOT/.nuget --configfile $TestExecutionDirectory/nuget.config
dotnet nuget list source --configfile $TestExecutionDirectory/nuget.config

cp $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/testAsset.props ./
export TestPackagesRoot=$(pwd)/Assets/TestPackages
find $TestPackagesRoot -name *.csproj
find . -name *.csproj
dotnet build ./Assets/TestPackages/Microsoft.NET.TestPackages.csproj /t:Build -p:VersionPropsIsImported=false
find $TestPackagesRoot -name *.nupkg
cp $TestPackagesRoot/TestPackages/. $TestExecutionDirectory/TestPackages -R
