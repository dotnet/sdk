#!/usr/bin/env bash

### Usage: $0
###
###   Prepares and runs the binary tooling to detect binaries in the VMR. Default behavior is to report any binaries
###   not found in the allowed-binaries file. To remove binaries not specified in the allowed-binaries file, pass --clean.
###
### Options:
###   --clean                    Clean the VMR of binaries not in the specified allowed-binaries file.
###   --allowed-binaries-file    Path to the file containing the list of binaries to be
###                              ignored for either cleaning or validating.
###                              Defaults to eng/allowed-vmr-binaries.txt.
###   --log-level <level>        Set the log level for the binary tooling. Defaults to Debug.
###   --with-packages            Use the specified directory as the packages source feed.
###                              Defaults to online dotnet-public and dotnet-libraries feeds.
###   --with-sdk                 Use the specified directory as the dotnet SDK.
###                              Defaults to .dotnet.

set -euo pipefail
IFS=$'\n\t'

source="${BASH_SOURCE[0]}"
REPO_ROOT="$( cd -P "$( dirname "$0" )/../" && pwd )"
BINARY_TOOL="$REPO_ROOT/eng/tools/BinaryToolKit"

function print_help () {
    sed -n '/^### /,/^$/p' "$source" | cut -b 5-
}

defaultDotnetSdk="$REPO_ROOT/.dotnet"
defaultAllowedBinariesFile="$REPO_ROOT/eng/allowed-vmr-binaries.txt"

# Set default values
allowedBinariesFile=$defaultAllowedBinariesFile
mode='validate'
logLevel='Debug'
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
    --allowed-binaries-file)
      allowedBinariesFile=$2
      shift
      ;;
    --log-level)
      logLevel=$2
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

function ParseBinaryArgs
{
    # Check allowed binaries file
    if [ ! -f "$allowedBinariesFile" ]; then
      echo "ERROR: The specified allowed-binaries file does not exist."
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

function RunBinaryTool
{
  targetDir="$REPO_ROOT"
  outputDir="$REPO_ROOT/artifacts/log/binary-report"
  BinaryToolCommand=""$dotnetSdk/dotnet" run --project "$BINARY_TOOL" -c Release --property:RestoreSources="$restoreSources" "$mode" "$targetDir" -o "$outputDir" -ab "$allowedBinariesFile" -l "$logLevel""

  if [ -n "$packagesDir" ]; then
    BinaryToolCommand=""$BinaryToolCommand" -p CustomPackageVersionsProps="$packagesDir/PackageVersions.props""
  fi

  # Run the Binary Tool
  eval "$BinaryToolCommand"
}

ParseBinaryArgs
RunBinaryTool
