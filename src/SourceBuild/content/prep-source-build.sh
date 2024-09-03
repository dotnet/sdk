#!/usr/bin/env bash

### Usage: $0
###
###   Prepares the environment for a source build by downloading Private.SourceBuilt.Artifacts.*.tar.gz,
###   installing the version of dotnet referenced in global.json,
###   and detecting binaries and removing any non-SB allowed binaries.
###
### Options:
###   --no-artifacts              Exclude the download of the previously source-built artifacts archive
###   --no-bootstrap              Don't replace portable packages in the download source-built artifacts
###   --no-prebuilts              Exclude the download of the prebuilts archive
###   --no-sdk                    Exclude the download of the .NET SDK
###   --artifacts-rid             The RID of the previously source-built artifacts archive to download
###                               Default is centos.9-x64
###   --runtime-source-feed       URL of a remote server or a local directory, from which SDKs and
###                               runtimes can be downloaded
###   --runtime-source-feed-key   Key for accessing the above server, if necessary
###
### Binary-Tooling options:
###   --no-binary-removal         Don't remove non-SB allowed binaries
###   --with-sdk                  Use the SDK in the specified directory
###                               Default is the .NET SDK
###   --with-packages             Specified directory to use as the source feed for packages
###                               Default is the previously source-built artifacts archive.

set -euo pipefail
IFS=$'\n\t'

source="${BASH_SOURCE[0]}"
REPO_ROOT="$( cd -P "$( dirname "$0" )" && pwd )"

function print_help () {
    sed -n '/^### /,/^$/p' "$source" | cut -b 5-
}

# SB prep default arguments
defaultArtifactsRid='centos.9-x64'

# Binary Tooling default arguments
defaultDotnetSdk="$REPO_ROOT/.dotnet"
defaultPackagesDir="$REPO_ROOT/prereqs/packages/previously-source-built"

# SB prep arguments
buildBootstrap=true
downloadArtifacts=true
downloadPrebuilts=true
removeBinaries=true
installDotnet=true
artifactsRid=$defaultArtifactsRid
runtime_source_feed='' # IBM requested these to support s390x scenarios
runtime_source_feed_key='' # IBM requested these to support s390x scenarios

# Binary Tooling arguments
dotnetSdk=$defaultDotnetSdk
packagesDir=$defaultPackagesDir

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
    --artifacts-rid)
      artifactsRid=$2
      ;;
    --runtime-source-feed)
      runtime_source_feed=$2
      shift
      ;;
    --runtime-source-feed-key)
      runtime_source_feed_key=$2
      shift
      ;;
    --no-binary-removal)
      removeBinaries=false
      ;;
    --with-sdk)
      dotnetSdk=$2
      shift
      ;;
    --with-packages)
      packagesDir=$2
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
if [ "$buildBootstrap" == true ] && [ "$installDotnet" == false ] && [ ! -d "$REPO_ROOT/.dotnet" ]; then
  echo "  ERROR: --no-sdk requires --no-bootstrap or a pre-existing .dotnet SDK directory.  Exiting..."
  exit 1
fi

# Check to make sure curl exists to download the archive files
if ! command -v curl &> /dev/null
then
  echo "  ERROR: curl not found.  Exiting..."
  exit 1
fi

# Check if Private.SourceBuilt artifacts archive exists
artifactsBaseFileName="Private.SourceBuilt.Artifacts"
packagesArchiveDir="$REPO_ROOT/prereqs/packages/archive/"
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
if [ "$installDotnet" == true ] && [ -d "$REPO_ROOT/.dotnet" ]; then
  echo "  ./.dotnet SDK directory exists...it will not be installed"
  installDotnet=false;
fi

function DownloadArchive {
  archiveType="$1"
  isRequired="$2"
  artifactsRid="$3"

  packageVersionsPath="$REPO_ROOT/eng/Versions.props"
  notFoundMessage="No source-built $archiveType found to download..."

  echo "  Looking for source-built $archiveType to download..."
  archiveVersionLine=$(grep -m 1 "<PrivateSourceBuilt${archiveType}Version>" "$packageVersionsPath" || :)
  versionPattern="<PrivateSourceBuilt${archiveType}Version>(.*)</PrivateSourceBuilt${archiveType}Version>"
  if [[ $archiveVersionLine =~ $versionPattern ]]; then
    archiveVersion="${BASH_REMATCH[1]}"

    if [ "$archiveType" == "Prebuilts" ]; then
        archiveRid=$defaultArtifactsRid
    else
        archiveRid=$artifactsRid
    fi

    archiveUrl="https://dotnetcli.azureedge.net/source-built-artifacts/assets/Private.SourceBuilt.$archiveType.$archiveVersion.$archiveRid.tar.gz"

    echo "  Downloading source-built $archiveType from $archiveUrl..."
    (cd "$packagesArchiveDir" && curl -f --retry 5 -O "$archiveUrl")
  elif [ "$isRequired" == true ]; then
    echo "  ERROR: $notFoundMessage"
    exit 1
  else
    echo "  $notFoundMessage"
  fi
}

function BootstrapArtifacts {
  DOTNET_SDK_PATH="$REPO_ROOT/.dotnet"

  # Create working directory for running bootstrap project
  workingDir=$(mktemp -d)
  echo "  Building bootstrap previously source-built in $workingDir"

  # Copy bootstrap project to working dir
  cp "$REPO_ROOT/eng/bootstrap/buildBootstrapPreviouslySB.csproj" "$workingDir"

  # Copy NuGet.config from the sdk repo to have the right feeds
  cp "$REPO_ROOT/src/sdk/NuGet.config" "$workingDir"

  # Get PackageVersions.props from existing prev-sb archive
  echo "  Retrieving PackageVersions.props from existing archive"
  sourceBuiltArchive=$(find "$packagesArchiveDir" -maxdepth 1 -name 'Private.SourceBuilt.Artifacts*.tar.gz')
  if [ -f "$sourceBuiltArchive" ]; then
      tar -xzf "$sourceBuiltArchive" -C "$workingDir" PackageVersions.props
  fi

  # Run restore on project to initiate download of bootstrap packages
  "$DOTNET_SDK_PATH/dotnet" restore "$workingDir/buildBootstrapPreviouslySB.csproj" /bl:artifacts/log/prep-bootstrap.binlog /fileLoggerParameters:LogFile=artifacts/log/prep-bootstrap.log /p:ArchiveDir="$packagesArchiveDir" /p:BootstrapOverrideVersionsProps="$REPO_ROOT/eng/bootstrap/OverrideBootstrapVersions.props"

  # Remove working directory
  rm -rf "$workingDir"
}

# Check for the version of dotnet to install
if [ "$installDotnet" == true ]; then
  echo "  Installing dotnet..."
  use_installed_dotnet_cli=false
  (source ./eng/common/tools.sh && InitializeDotNetCli true)
fi

# Read the eng/Versions.props to get the archives to download and download them
if [ "$downloadArtifacts" == true ]; then
  DownloadArchive Artifacts true $artifactsRid
  if [ "$buildBootstrap" == true ]; then
      BootstrapArtifacts
  fi
fi

if [ "$downloadPrebuilts" == true ]; then
  DownloadArchive Prebuilts false $artifactsRid
fi

if [ "$removeBinaries" == true ]; then

  originalPackagesDir=$packagesDir
  # Create working directory for extracking packages
  workingDir=$(mktemp -d)

  # If --with-packages is not passed, unpack PSB artifacts
  if [[ $packagesDir == $defaultPackagesDir ]]; then
    echo "  Extracting previously source-built to $workingDir"
    sourceBuiltArchive=$(find "$packagesArchiveDir" -maxdepth 1 -name 'Private.SourceBuilt.Artifacts*.tar.gz')

    if [ ! -f "$sourceBuiltArchive" ]; then
      echo "  ERROR: Private.SourceBuilt.Artifacts.*.tar.gz does not exist..."\
            "Cannot remove non-SB allowed binaries. Either pass --with-packages or download the artifacts."
      exit 1
    fi

    echo "  Unpacking Private.SourceBuilt.Artifacts.*.tar.gz into $workingDir"
    tar -xzf "$sourceBuiltArchive" -C "$workingDir"

    packagesDir=$workingDir
  fi

  "$REPO_ROOT/eng/detect-binaries.sh" \
  --clean \
  --allowed-binaries-file "$REPO_ROOT/eng/allowed-sb-binaries.txt" \
  --with-packages $packagesDir \
  --with-sdk $dotnetSdk \

  rm -rf "$workingDir"

  packagesDir=$originalPackagesDir
  unset originalPackagesDir
fi
