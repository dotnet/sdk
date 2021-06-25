#!/usr/bin/env bash
set -euo pipefail
IFS=$'\n\t'

SCRIPT_ROOT="$(cd -P "$( dirname "$0" )" && pwd)"

usage() {
    echo "usage: $0"
    echo ""
    echo "  Prepares a tarball to be built by downloading Private.SourceBuilt.Artifacts.*.tar.gz and"
    echo "  installing the version of dotnet referenced in global.json"
    echo ""
}

positional_args=()
while :; do
    if [ $# -le 0 ]; then
        break
    fi
    lowerI="$(echo "$1" | awk '{print tolower($0)}')"
    case $lowerI in
        "-?"|-h|--help)
            usage
            exit 0
            ;;
        *)
            positional_args+=("$1")
            ;;
    esac

    shift
done

# Check for the archive text file which describes the location of the archive files to download
if [ ! -f $SCRIPT_ROOT/packages/archive/archiveArtifacts.txt ]; then
    echo "  ERROR: $SCRIPT_ROOT/packages/archive/archiveArtifacts.txt does not exist.  Cannot determine which archives to download.  Exiting..."
    exit -1
fi

downloadArtifacts=true
installDotnet=true

# Check to make sure curl exists to download the archive files
if ! command -v curl &> /dev/null
then
    echo "  ERROR: curl not found.  Exiting..."
    exit -1
fi

# Check if Private.SourceBuilt artifacts archive exists
if [ -f $SCRIPT_ROOT/packages/archive/Private.SourceBuilt.Artifacts.*.tar.gz ]; then
    echo "  Private.SourceBuilt.Artifacts.*.tar.gz exists...it will not be downloaded"
    downloadArtifacts=false
fi

# Check if dotnet is installed
if [ -d $SCRIPT_ROOT/.dotnet ]; then
    echo "  ./.dotnet SDK directory exists...it will not be installed"
    installDotnet=false;
fi

# Read the archive text file to get the archives to download and download them
while read -r line; do
    if [[ $line == *"Private.SourceBuilt.Artifacts"* ]]; then
        if [ "$downloadArtifacts" == "true" ]; then
            echo "  Downloading source-built artifacts..."
            (cd $SCRIPT_ROOT/packages/archive/ && curl -O $line)
        fi
    fi
done < $SCRIPT_ROOT/packages/archive/archiveArtifacts.txt

# Check for the version of dotnet to install
if [ "$installDotnet" == "true" ]; then
    echo "  Installing dotnet..."
    (source ./eng/common/tools.sh && InitializeDotNetCli true)
fi
