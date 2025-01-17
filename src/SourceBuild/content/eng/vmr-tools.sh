#!/usr/bin/env bash

### Usage: $0
###
###   Prepares and runs the VMR's tools.
###
### Options:
###   --tool <tool>              Name of VMR tool (name of a project in the eng/tools directory).
###   --list                     List of available tools to run.
###   --help                     Show this help message. Use '--help advanced' to only show advanced options.
###
##! Advanced Options:
##!   --with-packages <path>     Use the specified directory as the packages source feed for tooling.
##!                              Defaults to online dotnet-public and dotnet-libraries feeds.
##!   --with-sdk <path>          Use the specified directory as the dotnet SDK for tooling.
##!                              Defaults to .dotnet.
##!
##!   Command line arguments not listed above are passed thru to 'dotnet run' for tooling.

set -euo pipefail
IFS=$'\n\t'

source="${BASH_SOURCE[0]}"
REPO_ROOT="$( cd -P "$( dirname "$0" )/../" && pwd )"
TOOL_ROOT="$REPO_ROOT/eng/tools/"

defaultDotnetSdk="$REPO_ROOT/.dotnet"
mapfile -t availableTools < <(find "$TOOL_ROOT" -name '*.csproj' | grep -v tasks | xargs -n 1 basename | sed 's/.csproj//' | sort)

# Set default values
tool=''
toolArgs=''
propsDir=''
packagesDir=''
restoreSources=''
dotnetSdk=$defaultDotnetSdk

function print_help () {
  local advanced=${1:-default_value}
  if [ "$advanced" == "advanced" ]; then
    sed -n '/^##! /,/^$/p' "$source" | cut -b 5-
  else
    sed -n '/^### /,/^$/p' "$source" | cut -b 5-
  fi
  exit 0
}

if [ $# -eq 0 ]; then
    print_help
fi

toolArgs=()
while :; do
  if [ $# -le 0 ]; then
    break
  fi
  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
  case $lowerI in
    "-?"|-h|--help)
      print_help ${2:-default_value}
      ;;
    --tool)
      tool=$(find $TOOL_ROOT -name $2 -type d)
      if ! [[ " ${availableTools[@]} " =~ " ${2} " ]]; then
        echo "ERROR: The specified tool '$2' is not available in '$TOOL_ROOT'. Use --list to see available tools."
        exit 1
      fi
      shift
      ;;
    --list)
        echo "Available tools:"
        for tool in "${availableTools[@]}"; do
            echo "    $tool"
        done
        exit 0
        ;;
    --with-packages)
      packagesDir=$2
      if [ ! -d "$packagesDir" ]; then
        echo "ERROR: The specified packages directory '$packagesDir' does not exist."
        exit 1
      elif [ ! -f "$packagesDir/PackageVersions.props" ]; then
        echo "ERROR: The specified packages directory '$packagesDir' does not contain PackageVersions.props."
        exit 1
      fi
      shift
      ;;
    --with-sdk)
      dotnetSdk=$2
      if [ ! -d "$dotnetSdk" ]; then
        echo "ERROR: Custom SDK directory '$dotnetSdk' does not exist"
        exit 1
      fi
      if [ ! -x "$dotnetSdk/dotnet" ]; then
        echo "ERROR: Custom SDK '$dotnetSdk/dotnet' does not exist or is not executable"
        exit 1
      fi
      shift
      ;;
    *)
      if [ -n "$1" ]; then
        toolArgs+=("$1")
      fi
      ;;
  esac

  shift
done

function ValidateArgs
{
  # Check that a tool was specified
  if [ -z "$tool" ]; then
    echo "ERROR: --tool is required. Use --list to see available tools."
    exit 1
  fi

  # Initialize the dotnet SDK if needed
  if [ "$dotnetSdk" == "$defaultDotnetSdk" ]; then
    if [ ! -d "$dotnetSdk" ]; then
      . "$REPO_ROOT/eng/common/tools.sh"
      InitializeDotNetCli true
    fi
  fi

  # Set the restore sources
  if [ -z "$packagesDir" ]; then
    # Use dotnet-public and dotnet-libraries feeds as the default packages source feeds
    restoreSources="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json%3Bhttps://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json"
  else
    restoreSources=$(realpath "$packagesDir")
  fi
}

function RunTool
{
  if [ -n "$packagesDir" ]; then
    customVersionsPropsPath=$(realpath -m "$packagesDir/PackageVersions.props")
    toolArgs+=("-p" "CustomPackageVersionsProps=$customVersionsPropsPath")
  fi

  "$dotnetSdk/dotnet" run --project "$tool" -c Release --property:RestoreSources="$restoreSources" "${toolArgs[@]}"
}

ValidateArgs
RunTool
