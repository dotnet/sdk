#!/usr/bin/env bash
set -euo pipefail
IFS=$'\n\t'

SCRIPT_ROOT="$(cd -P "$( dirname "$0" )" && pwd)"

usage() {
    echo "usage: $0"
    echo ""
    echo "  Prepares the environment to be built by downloading Private.SourceBuilt.Artifacts.*.tar.gz and"
    echo "  installing the version of dotnet referenced in global.json"
    echo "options:"
    echo "  --no-artifacts              Exclude the download of the previously source-built artifacts archive"
    echo "  --no-bootstrap              Don't replace portable packages in the download source-built artifacts"
    echo "  --no-prebuilts              Exclude the download of the prebuilts archive"
    echo "  --no-sdk                    Exclude the download of the .NET SDK"
    echo "  --source-repository <url>   Source Link repository URL, required when building from tarball"
    echo "  --source-version <sha>      Source Link revision, required when building from tarball"
    echo "  --runtime-source-feed       URL of a remote server or a local directory, from which SDKs and"
    echo "                              runtimes can be downloaded"
    echo "  --runtime-source-feed-key   Key for accessing the above server, if necessary"
    echo ""
}

buildBootstrap=true
downloadArtifacts=true
downloadPrebuilts=true
installDotnet=true
sourceUrl=''
sourceVersion=''
runtime_source_feed='' # IBM requested these to support s390x scenarios
runtime_source_feed_key='' # IBM requested these to support s390x scenarios
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
        --no-bootstrap)
            buildBootstrap=false
            ;;
        --no-artifacts)
            downloadArtifacts=false
            ;;
        --no-prebuilts)
            downloadPrebuilts=false
            ;;
        --no-sdk)
            installDotnet=false
            ;;
        --source-repository)
            sourceUrl="$2"
            shift
            ;;
        --source-version)
            sourceVersion="$2"
            shift
            ;;
        --runtime-source-feed)
            runtime_source_feed=$2
            shift
            ;;
        --runtime-source-feed-key)
            runtime_source_feed_key=$2
            shift
            ;;
        *)
            positional_args+=("$1")
            ;;
    esac

    shift
done

# Attempting to bootstrap without an SDK will fail. So either the --no-sdk flag must be passed
# or a pre-existing .dotnet SDK directory must exist.
if [ "$buildBootstrap" == true ] && [ "$installDotnet" == false ] && [ ! -d "$SCRIPT_ROOT/.dotnet" ]; then
    echo "  ERROR: --no-sdk requires --no-bootstrap or a pre-existing .dotnet SDK directory.  Exiting..."
    exit 1
fi

# Check to make sure curl exists to download the archive files
if ! command -v curl &> /dev/null
then
    echo "  ERROR: curl not found.  Exiting..."
    exit 1
fi

GIT_DIR="$SCRIPT_ROOT/.git"
if [ -f "$GIT_DIR/index" ]; then # We check for index because if outside of git, we create config and HEAD manually
    if [ -n "$sourceUrl" ] || [ -n "$sourceVersion" ]; then
        echo "ERROR: $SCRIPT_ROOT is a git repository, --source-repository and --source-version cannot be used."
        exit 1
    fi
else
    if [ -z "$sourceUrl" ] || [ -z "$sourceVersion" ]; then
      echo "ERROR: $SCRIPT_ROOT is not a git repository, --source-repository and --source-version must be specified."
        exit 1
    fi

    # We need to add "fake" .git/ files when not building from a git repository
    mkdir -p "$GIT_DIR"
    echo '[remote "origin"]' > "$GIT_DIR/config"
    echo "url=\"$sourceUrl\"" >> "$GIT_DIR/config"
    echo "$sourceVersion" > "$GIT_DIR/HEAD"
fi

# Check if Private.SourceBuilt artifacts archive exists
artifactsBaseFileName="Private.SourceBuilt.Artifacts"
packagesArchiveDir="$SCRIPT_ROOT/prereqs/packages/archive/"
if [ "$downloadArtifacts" == true ] && [ -f ${packagesArchiveDir}${artifactsBaseFileName}.*.tar.gz ]; then
    echo "  Private.SourceBuilt.Artifacts.*.tar.gz exists...it will not be downloaded"
    downloadArtifacts=false
fi

# Check if Private.SourceBuilt prebuilts archive exists
prebuiltsBaseFileName="Private.SourceBuilt.Prebuilts"
if [ "$downloadPrebuilts" == true ] && [ -f ${packagesArchiveDir}${prebuiltsBaseFileName}.*.tar.gz ]; then
    echo "  Private.SourceBuilt.Prebuilts.*.tar.gz exists...it will not be downloaded"
    downloadPrebuilts=false
fi

# Check if dotnet is installed
if [ "$installDotnet" == true ] && [ -d "$SCRIPT_ROOT/.dotnet" ]; then
    echo "  ./.dotnet SDK directory exists...it will not be installed"
    installDotnet=false;
fi

function DownloadArchive {
    archiveType="$1"
    isRequired="$2"

    packageVersionsPath="$SCRIPT_ROOT/eng/Versions.props"
    notFoundMessage="No source-built $archiveType found to download..."

    echo "  Looking for source-built $archiveType to download..."
    archiveVersionLine=$(grep -m 1 "<PrivateSourceBuilt${archiveType}Url>" "$packageVersionsPath" || :)
    versionPattern="<PrivateSourceBuilt${archiveType}Url>(.*)</PrivateSourceBuilt${archiveType}Url>"
    if [[ $archiveVersionLine =~ $versionPattern ]]; then
        archiveUrl="${BASH_REMATCH[1]}"
        echo "  Downloading source-built $archiveType from $archiveUrl..."
        (cd "$packagesArchiveDir" && curl --retry 5 -O "$archiveUrl")
    elif [ "$isRequired" == true ]; then
      echo "  ERROR: $notFoundMessage"
      exit 1
    else
      echo "  $notFoundMessage"
    fi
}

function BootstrapArtifacts {
    DOTNET_SDK_PATH="$SCRIPT_ROOT/.dotnet"

    # Create working directory for running bootstrap project
    workingDir=$(mktemp -d)
    echo "  Building bootstrap previously source-built in $workingDir"

    # Copy bootstrap project to working dir
    cp "$SCRIPT_ROOT/eng/bootstrap/buildBootstrapPreviouslySB.csproj" "$workingDir"

    # Copy NuGet.config from the installer repo to have the right feeds
    cp "$SCRIPT_ROOT/src/installer/NuGet.config" "$workingDir"

    # Get PackageVersions.props from existing prev-sb archive
    echo "  Retrieving PackageVersions.props from existing archive"
    sourceBuiltArchive=$(find "$packagesArchiveDir" -maxdepth 1 -name 'Private.SourceBuilt.Artifacts*.tar.gz')
    if [ -f "$sourceBuiltArchive" ]; then
        tar -xzf "$sourceBuiltArchive" -C "$workingDir" PackageVersions.props
    fi

    # Run restore on project to initiate download of bootstrap packages
    "$DOTNET_SDK_PATH/dotnet" restore "$workingDir/buildBootstrapPreviouslySB.csproj" /bl:artifacts/prep/bootstrap.binlog /fileLoggerParameters:LogFile=artifacts/prep/bootstrap.log /p:ArchiveDir="$packagesArchiveDir" /p:BootstrapOverrideVersionsProps="$SCRIPT_ROOT/eng/bootstrap/OverrideBootstrapVersions.props"

    # Remove working directory
    rm -rf "$workingDir"
}

# Check for the version of dotnet to install
if [ "$installDotnet" == true ]; then
    echo "  Installing dotnet..."
    (source ./eng/common/tools.sh && InitializeDotNetCli true)
fi

# Read the eng/Versions.props to get the archives to download and download them
if [ "$downloadArtifacts" == true ]; then
    DownloadArchive Artifacts true
    if [ "$buildBootstrap" == true ]; then
        BootstrapArtifacts
    fi
fi

if [ "$downloadPrebuilts" == true ]; then
    DownloadArchive Prebuilts false
fi
