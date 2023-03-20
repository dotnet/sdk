#!/usr/bin/env bash
set -euo pipefail
IFS=$'\n\t'

usage() {
    echo "usage: $0 [options]"
    echo "options:"
    echo "  --clean-while-building             cleans each repo after building (reduces disk space usage)"
    echo "  --online                           build using online sources"
    echo "  --poison                           build with poisoning checks"
    echo "  --run-smoke-test                   don't build; run smoke tests"
    echo "  --use-mono-runtime                 output uses the mono runtime"
    echo "  --with-packages <dir>              use the specified directory of previously-built packages"
    echo "  --with-sdk <dir>                   use the SDK in the specified directory for bootstrapping"
    echo "use -- to send the remaining arguments to MSBuild"
    echo ""
}

SCRIPT_ROOT="$(cd -P "$( dirname "$0" )" && pwd)"

MSBUILD_ARGUMENTS=("/flp:v=detailed")
CUSTOM_REF_PACKAGES_DIR=''
CUSTOM_PACKAGES_DIR=''
alternateTarget=false
runningSmokeTests=false
CUSTOM_SDK_DIR=''

while :; do
    if [ $# -le 0 ]; then
        break
    fi

    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        --clean-while-building)
            MSBUILD_ARGUMENTS+=( "/p:CleanWhileBuilding=true")
            ;;
        --online)
            MSBUILD_ARGUMENTS+=( "/p:BuildWithOnlineSources=true")
            ;;
        --poison)
            MSBUILD_ARGUMENTS+=( "/p:EnablePoison=true")
            ;;
        --run-smoke-test)
            alternateTarget=true
            runningSmokeTests=true
            MSBUILD_ARGUMENTS+=( "/t:RunSmokeTest" )
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
        -?|-h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unrecognized argument '$1'"
            usage
            exit 1
            ;;
    esac
    shift
done

if [ "$CUSTOM_PACKAGES_DIR" != "" ]; then
  if [ "$runningSmokeTests" == "true" ]; then
    MSBUILD_ARGUMENTS+=( "/p:CustomSourceBuiltPackagesPath=$CUSTOM_PACKAGES_DIR" )
  else
    MSBUILD_ARGUMENTS+=( "/p:CustomPrebuiltSourceBuiltPackagesPath=$CUSTOM_PACKAGES_DIR" )
  fi
fi

if [ -f "$SCRIPT_ROOT/packages/archive/archiveArtifacts.txt" ]; then
  ARCHIVE_ERROR=0
  if [ ! -d "$SCRIPT_ROOT/.dotnet" ] && [ "$CUSTOM_SDK_DIR" == "" ]; then
    echo "ERROR: SDK not found at $SCRIPT_ROOT/.dotnet"
    ARCHIVE_ERROR=1
  fi
  if [ ! -f $SCRIPT_ROOT/packages/archive/Private.SourceBuilt.Artifacts*.tar.gz ] && [ "$CUSTOM_PACKAGES_DIR" == "" ]; then
    echo "ERROR: Private.SourceBuilt.Artifacts artifact not found at $SCRIPT_ROOT/packages/archive/ - Either run prep.sh or pass --with-packages parameter"
    ARCHIVE_ERROR=1
  fi
  if [ $ARCHIVE_ERROR == 1 ]; then
    echo ""
    echo "    Errors detected in tarball. To prep the tarball, run prep.sh while online to install an SDK"
    echo "    and Private.SourceBuilt.Artifacts tarball.  After prepping the tarball, the tarball  can be"
    echo "    built offline.  As an alternative to prepping the tarball, these assets can be provided using"
    echo "    the --with-sdk and --with-packages parameters"
    exit 1
  fi
fi

if [ -d "$CUSTOM_SDK_DIR" ]; then
  export SDK_VERSION=`"$CUSTOM_SDK_DIR/dotnet" --version`
  export CLI_ROOT="$CUSTOM_SDK_DIR"
  export _InitializeDotNetCli="$CLI_ROOT/dotnet"
  export CustomDotNetSdkDir="$CLI_ROOT"
  echo "Using custom bootstrap SDK from '$CLI_ROOT', version '$SDK_VERSION'"
else
  sdkLine=`grep -m 1 'dotnet' "$SCRIPT_ROOT/global.json"`
  sdkPattern="\"dotnet\" *: *\"(.*)\""
  if [[ $sdkLine =~ $sdkPattern ]]; then
    export SDK_VERSION=${BASH_REMATCH[1]}
    export CLI_ROOT="$SCRIPT_ROOT/.dotnet"
  fi
fi

packageVersionsPath=''
restoredPackagesDir="$SCRIPT_ROOT/packages/restored"

if [[ "$CUSTOM_PACKAGES_DIR" != "" && -f "$CUSTOM_PACKAGES_DIR/PackageVersions.props" ]]; then
  packageVersionsPath="$CUSTOM_PACKAGES_DIR/PackageVersions.props"
elif [ -d "$SCRIPT_ROOT/packages/archive" ]; then
  sourceBuiltArchive=`find $SCRIPT_ROOT/packages/archive -maxdepth 1 -name 'Private.SourceBuilt.Artifacts*.tar.gz'`
  if [ -f "$SCRIPT_ROOT/packages/previously-source-built/PackageVersions.props" ]; then
    packageVersionsPath=$SCRIPT_ROOT/packages/previously-source-built/PackageVersions.props
  elif [ -f "$sourceBuiltArchive" ]; then
    tar -xzf "$sourceBuiltArchive" -C /tmp PackageVersions.props
    packageVersionsPath=/tmp/PackageVersions.props
  fi
fi

if [ ! -f "$packageVersionsPath" ]; then
  echo "Cannot find PackagesVersions.props.  Debugging info:"
  echo "  Attempted archive path: $SCRIPT_ROOT/packages/archive"
  echo "  Attempted custom PVP path: $CUSTOM_PACKAGES_DIR/PackageVersions.props"
  exit 1
fi

arcadeSdkLine=`grep -m 1 'MicrosoftDotNetArcadeSdkVersion' "$packageVersionsPath"`
versionPattern="<MicrosoftDotNetArcadeSdkVersion>(.*)</MicrosoftDotNetArcadeSdkVersion>"
if [[ $arcadeSdkLine =~ $versionPattern ]]; then
  export ARCADE_BOOTSTRAP_VERSION=${BASH_REMATCH[1]}

  # Ensure that by default, the bootstrap version of the Arcade SDK is used. Source-build infra
  # projects use bootstrap Arcade SDK, and would fail to find it in the tarball build. The repo
  # projects overwrite this so that they use the source-built Arcade SDK instad.
  export SOURCE_BUILT_SDK_ID_ARCADE=Microsoft.DotNet.Arcade.Sdk
  export SOURCE_BUILT_SDK_VERSION_ARCADE=$ARCADE_BOOTSTRAP_VERSION
  export SOURCE_BUILT_SDK_DIR_ARCADE=$restoredPackagesDir/ArcadeBootstrapPackage/microsoft.dotnet.arcade.sdk/$ARCADE_BOOTSTRAP_VERSION
fi

sourceLinkLine=`grep -m 1 'MicrosoftSourceLinkCommonVersion' "$packageVersionsPath"`
versionPattern="<MicrosoftSourceLinkCommonVersion>(.*)</MicrosoftSourceLinkCommonVersion>"
if [[ $sourceLinkLine =~ $versionPattern ]]; then
  export SOURCE_LINK_BOOTSTRAP_VERSION=${BASH_REMATCH[1]}
fi

echo "Found bootstrap SDK $SDK_VERSION, bootstrap Arcade $ARCADE_BOOTSTRAP_VERSION, bootstrap SourceLink $SOURCE_LINK_BOOTSTRAP_VERSION"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUGET_PACKAGES=$restoredPackagesDir/

LogDateStamp=$(date +"%m%d%H%M%S")

if [ "$alternateTarget" == "true" ]; then
  "$CLI_ROOT/dotnet" $CLI_ROOT/sdk/$SDK_VERSION/MSBuild.dll "$SCRIPT_ROOT/build.proj" /bl:$SCRIPT_ROOT/artifacts/log/Debug/BuildTests_$LogDateStamp.binlog /fileLoggerParameters:LogFile=$SCRIPT_ROOT/artifacts/logs/BuildTests_$LogDateStamp.log /clp:v=m ${MSBUILD_ARGUMENTS[@]} "$@"
else
  $CLI_ROOT/dotnet $CLI_ROOT/sdk/$SDK_VERSION/MSBuild.dll /bl:$SCRIPT_ROOT/artifacts/log/Debug/BuildXPlatTasks_$LogDateStamp.binlog /fileLoggerParameters:LogFile=$SCRIPT_ROOT/artifacts/logs/BuildXPlatTasks_$LogDateStamp.log $SCRIPT_ROOT/tools-local/init-build.proj /t:PrepareOfflineLocalTools ${MSBUILD_ARGUMENTS[@]} "$@"
  # kill off the MSBuild server so that on future invocations we pick up our custom SDK Resolver
  $CLI_ROOT/dotnet build-server shutdown

  $CLI_ROOT/dotnet $CLI_ROOT/sdk/$SDK_VERSION/MSBuild.dll /bl:$SCRIPT_ROOT/artifacts/log/Debug/Build_$LogDateStamp.binlog /fileLoggerParameters:LogFile=$SCRIPT_ROOT/artifacts/logs/Build_$LogDateStamp.log $SCRIPT_ROOT/build.proj ${MSBUILD_ARGUMENTS[@]} "$@"
fi
