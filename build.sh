#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
REPOROOT="$DIR"
DOTNET_CLI_VERSION="$(cat "$REPOROOT/DotnetCLIVersion.txt")"

CONFIGURATION="Debug"
PLATFORM="Any CPU"
SKIP_BUILD=false
SKIP_TESTS=false

args=( "$@" )

while [[ $# > 0 ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -c|--configuration)
            CONFIGURATION=$2
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        --platform)
            PLATFORM=$2
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            args=( "${args[@]/$1}" )
            ;;  
        --skip-build)
            SKIP_BUILD=true
            args=( "${args[@]/$1}" )
            ;;               
        --help)
            echo "Usage: $0 [--configuration <CONFIGURATION>] [--platform <PLATFORM>] [--help]"
            echo ""
            echo "Options:"
            echo "  --configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
            echo "  --platform <PLATFORM>               Skip checks for pre-reqs in dotnet_install"
            echo "  --help                              Display this help message"
            exit 0
            ;;
        *)
            break
            ;;
    esac

    shift
done

# $args array may have empty elements in it.
# The easiest way to remove them is to cast to string and back to array.
# This will actually break quoted arguments, arguments like
# -test "hello world" will be broken into three arguments instead of two, as it should.
temp="${args[@]}"
args=($temp)

# Set nuget package cache under the repo
[ -z "$NUGET_HTTP_CACHE_PATH" ] && export NUGET_HTTP_CACHE_PATH="$REPOROOT/packages"
[ -z "$NUGET_PACKAGES" ] && export NUGET_PACKAGES="$REPOROOT/packages"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
[ -z "$DOTNET_INSTALL_DIR" ] && export DOTNET_INSTALL_DIR=$REPOROOT/.dotnet_cli
[ -d "$DOTNET_INSTALL_DIR" ] || mkdir -p $DOTNET_INSTALL_DIR

# NuGet depends on HOME and it may not be set. Until it's fixed, we just patch a value in
if [ -z "$HOME" ]; then
    export HOME="$REPOROOT/.home"

    [ ! -d "$HOME" ] || rm -Rf $HOME
    mkdir -p $HOME
fi

if [ ! -f $sdkInstallPath/dotnet ]; then
    # Install a stage 0
    DOTNET_INSTALL_SCRIPT_URL="https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.sh"
    curl -sSL "$DOTNET_INSTALL_SCRIPT_URL" | bash /dev/stdin  --version $DOTNET_CLI_VERSION --verbose
    
    [ -d "$DOTNET_INSTALL_DIR/shared/Microsoft.NETCore.App/1.1.1" ] || curl -sSL "$DOTNET_INSTALL_SCRIPT_URL" | bash /dev/stdin  --channel "Release/1.1.0" --version "1.1.1" --shared-runtime

    # Put stage 0 on the PATH
    export PATH="$DOTNET_INSTALL_DIR:$PATH"

    # Disable first run since we want to control all package sources
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

fi

if [[ "$SKIP_BUILD" = true ]]; then
  exit 0
fi

if [[ "$SKIP_TESTS" = true ]]; then
    dotnet msbuild $REPOROOT/build/build.proj /t:BuildWithoutTesting /m:1 /nologo /p:Configuration=$CONFIGURATION /p:Platform="$PLATFORM" "${args[@]}"
else
    dotnet msbuild $REPOROOT/build/build.proj /m:1 /nologo /p:Configuration=$CONFIGURATION /p:Platform="$PLATFORM" "${args[@]}"
fi
