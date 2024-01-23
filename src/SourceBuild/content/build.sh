#!/usr/bin/env bash

### Usage: $0 [options]
###
### Options:
###   --ci                         Set when running on CI server
###   --clean-while-building       Cleans each repo after building (reduces disk space usage)
###   --configuration              Build configuration [Default: Release]
###   --online                     Build using online sources
###   --poison                     Build with poisoning checks
###   --release-manifest <FILE>    A JSON file, an alternative source of Source Link metadata
###   --run-smoke-test             Don't build; run smoke tests
###   --source-repository <URL>    Source Link repository URL, required when building from tarball
###   --source-version <SHA>       Source Link revision, required when building from tarball
###   --use-mono-runtime           Output uses the mono runtime
###   --with-packages <DIR>        Use the specified directory of previously-built packages
###   --with-sdk <DIR>             Use the SDK in the specified directory for bootstrapping
###
### Use -- to send the remaining arguments to MSBuild

set -euo pipefail
IFS=$'\n\t'

source="${BASH_SOURCE[0]}"
SCRIPT_ROOT="$(cd -P "$( dirname "$0" )" && pwd)"

function print_help () {
    sed -n '/^### /,/^$/p' "$source" | cut -b 5-
}

MSBUILD_ARGUMENTS=("-flp:v=detailed")
MSBUILD_ARGUMENTS=("--tl:off")
# TODO: Make it possible to invoke this script for non source build use cases
# https://github.com/dotnet/source-build/issues/3965
MSBUILD_ARGUMENTS+=("/p:DotNetBuildFromSource=true")
MSBUILD_ARGUMENTS+=("/p:DotNetBuildVertical=false")
CUSTOM_PACKAGES_DIR=''
alternateTarget=false
runningSmokeTests=false
packagesDir="$SCRIPT_ROOT/prereqs/packages/"
packagesArchiveDir="${packagesDir}archive/"
packagesRestoredDir="${packagesDir}restored/"
packagesPreviouslySourceBuiltDir="${packagesDir}previously-source-built/"
CUSTOM_SDK_DIR=''

sourceRepository=''
sourceVersion=''
releaseManifest=''
configuration='Release'

while :; do
  if [ $# -le 0 ]; then
    break
  fi

  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
  case $lowerI in
    --ci)
      MSBUILD_ARGUMENTS+=( "-p:ContinuousIntegrationBuild=true")
      ;;
    --clean-while-building)
      MSBUILD_ARGUMENTS+=( "-p:CleanWhileBuilding=true")
      ;;
    --configuration)
      configuration="$2"
      ;;
    --online)
      MSBUILD_ARGUMENTS+=( "-p:BuildWithOnlineSources=true")
      ;;
    --poison)
      MSBUILD_ARGUMENTS+=( "-p:EnablePoison=true")
      ;;
    --run-smoke-test)
      alternateTarget=true
      runningSmokeTests=true
      MSBUILD_ARGUMENTS+=( "-t:RunSmokeTest" )
      ;;
    --source-repository)
      sourceRepository="$2"
      shift
      ;;
    --source-version)
      sourceVersion="$2"
      shift
      ;;
    --release-manifest)
      releaseManifest="$2"
      shift
      ;;
    --use-mono-runtime)
      MSBUILD_ARGUMENTS+=( "/p:SourceBuildUseMonoRuntime=true" )
      ;;
    --with-packages)
      CUSTOM_PACKAGES_DIR="$(cd -P "$2" && pwd)"
      if [ ! -d "$CUSTOM_PACKAGES_DIR" ]; then
          echo "Custom prviously built packages directory '$CUSTOM_PACKAGES_DIR' does not exist"
          exit 1
      fi
      shift
      ;;
    --with-sdk)
      CUSTOM_SDK_DIR="$(cd -P "$2" && pwd)"
      if [ ! -d "$CUSTOM_SDK_DIR" ]; then
          echo "Custom SDK directory '$CUSTOM_SDK_DIR' does not exist"
          exit 1
      fi
      if [ ! -x "$CUSTOM_SDK_DIR/dotnet" ]; then
          echo "Custom SDK '$CUSTOM_SDK_DIR/dotnet' does not exist or is not executable"
          exit 1
      fi
      shift
      ;;
    --)
      shift
      echo "Detected '--': passing remaining parameters '$@' as build.sh arguments."
      break
      ;;
    '-?'|-h|--help)
      print_help
      exit 0
      ;;
    *)
      echo "Unrecognized argument '$1'"
      print_help
      exit 1
      ;;
  esac
  shift
done

MSBUILD_ARGUMENTS+=("/p:Configuration=$configuration")

# For build purposes, we need to make sure we have all the SourceLink information
if [ "$alternateTarget" != "true" ]; then
  GIT_DIR="$SCRIPT_ROOT/.git"
  if [ -f "$GIT_DIR/index" ]; then # We check for index because if outside of git, we create config and HEAD manually
    if [ -n "$sourceRepository" ] || [ -n "$sourceVersion" ] || [ -n "$releaseManifest" ]; then
      echo "ERROR: Source Link arguments cannot be used in a git repository"
      exit 1
    fi
  else
    if [ -z "$releaseManifest" ]; then
      if [ -z "$sourceRepository" ] || [ -z "$sourceVersion" ]; then
        echo "ERROR: $SCRIPT_ROOT is not a git repository, either --release-manifest or --source-repository and --source-version must be specified"
        exit 1
      fi
    else
      if [ -n "$sourceRepository" ] || [ -n "$sourceVersion" ]; then
        echo "ERROR: --release-manifest cannot be specified together with --source-repository and --source-version"
        exit 1
      fi

      get_property() {
        local json_file_path="$1"
        local property_name="$2"
        grep -oP '(?<="'$property_name'": ")[^"]*' "$json_file_path"
      }

      sourceRepository=$(get_property "$releaseManifest" sourceRepository) \
        || (echo "ERROR: Failed to find sourceRepository in $releaseManifest" && exit 1)
      sourceVersion=$(get_property "$releaseManifest" sourceVersion) \
        || (echo "ERROR: Failed to find sourceVersion in $releaseManifest" && exit 1)

      if [ -z "$sourceRepository" ] || [ -z "$sourceVersion" ]; then
        echo "ERROR: sourceRepository and sourceVersion must be specified in $releaseManifest"
        exit 1
      fi
    fi

    # We need to add "fake" .git/ files when not building from a git repository
    mkdir -p "$GIT_DIR"
    echo '[remote "origin"]' > "$GIT_DIR/config"
    echo "url=\"$sourceRepository\"" >> "$GIT_DIR/config"
    echo "$sourceVersion" > "$GIT_DIR/HEAD"
  fi
fi

if [ "$CUSTOM_PACKAGES_DIR" != "" ]; then
  if [ "$runningSmokeTests" == "true" ]; then
    MSBUILD_ARGUMENTS+=( "-p:CustomSourceBuiltPackagesPath=$CUSTOM_PACKAGES_DIR" )
  else
    MSBUILD_ARGUMENTS+=( "-p:CustomPrebuiltSourceBuiltPackagesPath=$CUSTOM_PACKAGES_DIR" )
  fi
fi

if [ -f "${packagesArchiveDir}archiveArtifacts.txt" ]; then
  ARCHIVE_ERROR=0
  if [ ! -d "$SCRIPT_ROOT/.dotnet" ] && [ "$CUSTOM_SDK_DIR" == "" ]; then
    echo "ERROR: SDK not found at '$SCRIPT_ROOT/.dotnet'. Either run prep.sh to acquire one or specify one via the --with-sdk parameter."
    ARCHIVE_ERROR=1
  fi
  if [ ! -f ${packagesArchiveDir}Private.SourceBuilt.Artifacts*.tar.gz ] && [ "$CUSTOM_PACKAGES_DIR" == "" ]; then
    echo "ERROR: Private.SourceBuilt.Artifacts artifact not found at '$packagesArchiveDir'. Either run prep.sh to acquire it or specify one via the --with-packages parameter."
    ARCHIVE_ERROR=1
  fi
  if [ $ARCHIVE_ERROR == 1 ]; then
    exit 1
  fi
fi

if [ ! -d "$SCRIPT_ROOT/.git" ]; then
  echo "ERROR: $SCRIPT_ROOT is not a git repository. Please run prep.sh add initialize Source Link metadata."
  exit 1
fi

if [ -d "$CUSTOM_SDK_DIR" ]; then
  export SDK_VERSION=$("$CUSTOM_SDK_DIR/dotnet" --version)
  export CLI_ROOT="$CUSTOM_SDK_DIR"
  export _InitializeDotNetCli="$CLI_ROOT/dotnet"
  export DOTNET_INSTALL_DIR="$CLI_ROOT"
  echo "Using custom bootstrap SDK from '$CLI_ROOT', version '$SDK_VERSION'"
else
  sdkLine=$(grep -m 1 'dotnet' "$SCRIPT_ROOT/global.json")
  sdkPattern="\"dotnet\" *: *\"(.*)\""
  if [[ $sdkLine =~ $sdkPattern ]]; then
    export SDK_VERSION=${BASH_REMATCH[1]}
    export CLI_ROOT="$SCRIPT_ROOT/.dotnet"
  fi
fi

packageVersionsPath=''

if [[ "$CUSTOM_PACKAGES_DIR" != "" && -f "$CUSTOM_PACKAGES_DIR/PackageVersions.props" ]]; then
  packageVersionsPath="$CUSTOM_PACKAGES_DIR/PackageVersions.props"
elif [ -d "$packagesArchiveDir" ]; then
  sourceBuiltArchive=$(find "$packagesArchiveDir" -maxdepth 1 -name 'Private.SourceBuilt.Artifacts*.tar.gz')
  if [ -f "${packagesPreviouslySourceBuiltDir}}PackageVersions.props" ]; then
    packageVersionsPath=${packagesPreviouslySourceBuiltDir}PackageVersions.props
  elif [ -f "$sourceBuiltArchive" ]; then
    tar -xzf "$sourceBuiltArchive" -C /tmp PackageVersions.props
    packageVersionsPath=/tmp/PackageVersions.props
  fi
fi

if [ ! -f "$packageVersionsPath" ]; then
  echo "Cannot find PackagesVersions.props.  Debugging info:"
  echo "  Attempted archive path: $packagesArchiveDir"
  echo "  Attempted custom PVP path: $CUSTOM_PACKAGES_DIR/PackageVersions.props"
  exit 1
fi

arcadeSdkLine=$(grep -m 1 'MicrosoftDotNetArcadeSdkVersion' "$packageVersionsPath")
versionPattern="<MicrosoftDotNetArcadeSdkVersion>(.*)</MicrosoftDotNetArcadeSdkVersion>"
if [[ $arcadeSdkLine =~ $versionPattern ]]; then
  export ARCADE_BOOTSTRAP_VERSION=${BASH_REMATCH[1]}

  # Ensure that by default, the bootstrap version of the Arcade SDK is used. Source-build infra
  # projects use bootstrap Arcade SDK, and would fail to find it in the build. The repo
  # projects overwrite this so that they use the source-built Arcade SDK instad.
  export SOURCE_BUILT_SDK_ID_ARCADE=Microsoft.DotNet.Arcade.Sdk
  export SOURCE_BUILT_SDK_VERSION_ARCADE=$ARCADE_BOOTSTRAP_VERSION
  export SOURCE_BUILT_SDK_DIR_ARCADE=$packagesRestoredDir/ArcadeBootstrapPackage/microsoft.dotnet.arcade.sdk/$ARCADE_BOOTSTRAP_VERSION
fi

echo "Found bootstrap SDK $SDK_VERSION, bootstrap Arcade $ARCADE_BOOTSTRAP_VERSION"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUGET_PACKAGES=$packagesRestoredDir/

source $SCRIPT_ROOT/eng/common/native/init-os-and-arch.sh
source $SCRIPT_ROOT/eng/common/native/init-distro-rid.sh
initDistroRidGlobal "$os" "$arch" ""

LogDateStamp=$(date +"%m%d%H%M%S")

"$CLI_ROOT/dotnet" build-server shutdown

if [ "$alternateTarget" == "true" ]; then
  export NUGET_PACKAGES=$NUGET_PACKAGES/smoke-tests
  "$CLI_ROOT/dotnet" msbuild "$SCRIPT_ROOT/build.proj" -bl:"$SCRIPT_ROOT/artifacts/log/$configuration/BuildTests_$LogDateStamp.binlog" -flp:"LogFile=$SCRIPT_ROOT/artifacts/log/$configuration/BuildTests_$LogDateStamp.log" -clp:v=m ${MSBUILD_ARGUMENTS[@]} "$@"
else
  "$CLI_ROOT/dotnet" msbuild "$SCRIPT_ROOT/eng/tools/init-build.proj" -bl:"$SCRIPT_ROOT/artifacts/log/$configuration/BuildMSBuildSdkResolver_$LogDateStamp.binlog" -flp:LogFile="$SCRIPT_ROOT/artifacts/log/$configuration/BuildMSBuildSdkResolver_$LogDateStamp.log" -t:ExtractToolPackage,BuildMSBuildSdkResolver ${MSBUILD_ARGUMENTS[@]} "$@"

  # kill off the MSBuild server so that on future invocations we pick up our custom SDK Resolver
  "$CLI_ROOT/dotnet" build-server shutdown

  "$CLI_ROOT/dotnet" msbuild "$SCRIPT_ROOT/build.proj" -bl:"$SCRIPT_ROOT/artifacts/log/$configuration/Build_$LogDateStamp.binlog" -flp:"LogFile=$SCRIPT_ROOT/artifacts/log/$configuration/Build_$LogDateStamp.log" ${MSBUILD_ARGUMENTS[@]} "$@"
fi
