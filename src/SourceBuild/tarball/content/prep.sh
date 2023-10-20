#!/usr/bin/env bash
set -euo pipefail
IFS=$'\n\t'

SCRIPT_ROOT="$(cd -P "$( dirname "$0" )" && pwd)"

usage() {
    echo "usage: $0 [options]"
    echo ""
    echo "  Prepares a tarball to be built by downloading Private.SourceBuilt.Artifacts.*.tar.gz and"
    echo "  installing the version of dotnet referenced in global.json"
    echo "options:"
    echo "  --bootstrap    Build a bootstrap version of previously source-built packages archive."
    echo "                 This modifies the downloaded version, replacing portable packages"
    echo "                 with official ms-built packages restored from package feeds."
    echo ""
}

buildBootstrap=false
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
        --bootstrap)
            buildBootstrap=true
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
downloadPrebuilts=true
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

# Check if Private.SourceBuilt prebuilts archive exists
if [ -f $SCRIPT_ROOT/packages/archive/Private.SourceBuilt.Prebuilts.*.tar.gz ]; then
    echo "  Private.SourceBuilt.Prebuilts.*.tar.gz exists...it will not be downloaded"
    downloadPrebuilts=false
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
            echo "  Downloading source-built artifacts from $line..."
            (cd $SCRIPT_ROOT/packages/archive/ && curl --fail --retry 5 -O $line)
        fi
    fi
    if [[ $line == *"Private.SourceBuilt.Prebuilts"* ]]; then
        if [ "$downloadPrebuilts" == "true" ]; then
            echo "  Downloading source-built prebuilts from $line..."
            (cd $SCRIPT_ROOT/packages/archive/ && curl --fail --retry 5 -O $line)
        fi
    fi
done < $SCRIPT_ROOT/packages/archive/archiveArtifacts.txt

# Check for the version of dotnet to install
if [ "$installDotnet" == "true" ]; then
    echo "  Installing dotnet..."
    (source ./eng/common/tools.sh && InitializeDotNetCli true)
fi

# Build bootstrap, if specified
if [ "$buildBootstrap" == "true" ]; then
    DOTNET_SDK_PATH="$SCRIPT_ROOT/.dotnet"

    # Create working directory for running bootstrap project
    workingDir=$(mktemp -d)
    echo "  Building bootstrap previously source-built in $workingDir"

    # Copy bootstrap project to working dir
    cp $SCRIPT_ROOT/eng/bootstrap/buildBootstrapPreviouslySB.csproj $workingDir

    # Copy NuGet.config from the installer repo to have the right feeds
    cp $SCRIPT_ROOT/src/installer/NuGet.config $workingDir

    # Get PackageVersions.props from existing prev-sb archive
    echo "  Retrieving PackageVersions.props from existing archive"
    sourceBuiltArchive=`find $SCRIPT_ROOT/packages/archive -maxdepth 1 -name 'Private.SourceBuilt.Artifacts*.tar.gz'`
    if [ -f "$sourceBuiltArchive" ]; then
        tar -xzf "$sourceBuiltArchive" -C $workingDir PackageVersions.props
    fi

    # Run restore on project to initiate download of bootstrap packages
    $DOTNET_SDK_PATH/dotnet restore $workingDir/buildBootstrapPreviouslySB.csproj /bl:artifacts/prep/bootstrap.binlog /fileLoggerParameters:LogFile=artifacts/prep/bootstrap.log /p:ArchiveDir="$SCRIPT_ROOT/packages/archive/" /p:BootstrapOverrideVersionsProps="$SCRIPT_ROOT/eng/bootstrap/OverrideBootstrapVersions.props"

    # Remove working directory
    rm -rf $workingDir
fi
