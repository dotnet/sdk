#!/usr/bin/env bash

### Usage: $0
###
###   Prepares and runs the file remover tooling to remove files containing non-OSS licenses from the VMR.
###   Default behavior is to removal licenses specifed in the disallowed-sb-license-paths file.
###
### Options:
###   --removal-file        Path to the file containing the list of paths to be removed.
###                         Defaults to eng/disallowed-sb-license-paths.txt.
###   --with-packages       Use the specified directory as the packages source feed.
###                         Defaults to online dotnet-public and dotnet-libraries feeds.
###   --with-sdk            Use the specified directory as the dotnet SDK.
###                         Defaults to .dotnet.

set -euo pipefail
IFS=$'\n\t'

source="${BASH_SOURCE[0]}"
REPO_ROOT="$( cd -P "$( dirname "$0" )/../" && pwd )"
REMOVAL_TOOL="$REPO_ROOT/eng/tools/FileRemover"

function print_help () {
    sed -n '/^### /,/^$/p' "$source" | cut -b 5-
}

defaultDotnetSdk="$REPO_ROOT/.dotnet"
defaultRemovalFile="$REPO_ROOT/eng/disallowed-sb-license-paths.txt"

# Set default values
removalFile=$defaultRemovalFile
propsDir=''
packagesDir=''
restoreSources=''
dotnetSdk=$defaultDotnetSdk

positional_args=()
while :; do
  if [ $# -le 0 ]; then
    break
  fi
  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
  case $lowerI in
    "-?"|-h|--help)
      print_help
      exit 0
      ;;
    --clean)
      mode="clean"
      ;;
    --removal-file)
      allowedBinariesFile=$2
      shift
      ;;
    --with-packages)
        packagesDir=$2
        if [ ! -d "$packagesDir" ]; then
            echo "ERROR: The specified packages directory does not exist."
            exit 1
        elif [ ! -f "$packagesDir/PackageVersions.props" ]; then
            echo "ERROR: The specified packages directory does not contain PackageVersions.props."
            exit 1
        fi
        shift
        ;;
    --with-sdk)
        dotnetSdk=$2
        if [ ! -d "$dotnetSdk" ]; then
            echo "Custom SDK directory '$dotnetSdk' does not exist"
            exit 1
        fi
        if [ ! -x "$dotnetSdk/dotnet" ]; then
            echo "Custom SDK '$dotnetSdk/dotnet' does not exist or is not executable"
            exit 1
        fi
        shift
        ;;
    *)
      positional_args+=("$1")
      ;;
  esac

  shift
done

function ParseRemovalArgs
{
    # Check removal file
    if [ ! -f "$removalFile" ]; then
      echo "ERROR: The specified removal file does not exist."
      exit 1
    fi

    # Check dotnet sdk
    if [ "$dotnetSdk" == "$defaultDotnetSdk" ]; then
        if [ ! -d "$dotnetSdk" ]; then
            . "$REPO_ROOT/eng/common/tools.sh"
            InitializeDotNetCli true
        fi
        else if [ ! -x "$dotnetSdk/dotnet" ]; then
            echo "'$dotnetSdk/dotnet' does not exist or is not executable"
            exit 1
        fi
    fi

    # Check the packages directory
    if [ -z "$packagesDir" ]; then
        # Use dotnet-public and dotnet-libraries feeds as the default packages source feeds
        restoreSources="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json%3Bhttps://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json"
    else
        restoreSources=$(realpath ${packagesDir})
    fi
}

function RunRemovalTool
{
  targetDir="$REPO_ROOT"
  RemovalToolCommand=""$dotnetSdk/dotnet" run --project "$REMOVAL_TOOL" -c Release --property:RestoreSources="$restoreSources" "$targetDir" -rf "$removalFile""

  if [ -n "$packagesDir" ]; then
    RemovalToolCommand=""$RemovalToolCommand" -p CustomPackageVersionsProps="$packagesDir/PackageVersions.props""
  fi

  # Run the Removal Tool
  eval "$RemovalToolCommand"
}

ParseRemovalArgs
RunRemovalTool
