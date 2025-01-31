#!/usr/bin/env bash

# Stop script if unbound variable found (use ${var:-} if intentional)
set -u

# Stop script if command returns non-zero exit code.
# Prevents hidden errors caused by missing error code propagation.
set -e

usage()
{
  echo "Common settings:"
  echo "  --binaryLog                     Create MSBuild binary log (short: -bl)"
  echo "  --configuration <value>         Build configuration: 'Debug' or 'Release' (short: -c)"
  echo "  --rid, --target-rid <value>     Overrides the rid that is produced by the build. e.g. alpine.3.18-arm64, fedora.37-x64, freebsd.13-arm64, ubuntu.19.10-x64"
  echo "  --verbosity <value>             Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic] (short: -v)"
  echo "  --with-system-libs <libs>       Use system versions of these libraries. Combine with a plus. eg brotli+libunwind+rapidjson+zlib"
  echo ""

  echo "Actions:"
  echo "  --clean                         Clean the solution"
  echo "  --help                          Print help and exit (short: -h)"
  echo "  --test                          Run tests (short: -t)"
  echo "  --sign                          Sign the build."
  echo ""

  echo "Source-only settings:"
  echo "  --source-only, --source-build   Source-build the solution (short: -so, -sb)"
  echo "  --online                        Build using online sources"
  echo "  --poison                        Build with poisoning checks"
  echo "  --release-manifest <FILE>       A JSON file, an alternative source of Source Link metadata"
  echo "  --source-repository <URL>       Source Link repository URL, required when building from tarball"
  echo "  --source-version <SHA>          Source Link revision, required when building from tarball"
  echo "  --with-packages <DIR>           Use the specified directory of previously-built packages"
  echo "  --with-sdk <DIR>                Use the SDK in the specified directory for bootstrapping"
  echo ""

  echo "Non-source-only settings:"
  echo "  --build-repo-tests              Build repository tests"

  echo "Advanced settings:"
  echo "  --ci                            Set when running on CI server"
  echo "  --clean-while-building          Cleans each repo after building (reduces disk space usage, short: -cwb)"
  echo "  --excludeCIBinarylog            Don't output binary log (short: -nobl)"
  echo "  --prepareMachine                Prepare machine for CI run, clean up processes after build"
  echo "  --use-mono-runtime              Output uses the mono runtime"
  echo ""
  echo "Command line arguments not listed above are passed thru to msbuild."
  echo "Arguments can also be passed in with a single hyphen."
}

source="${BASH_SOURCE[0]}"

# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

packagesRestoredDir="$scriptroot/.packages/"

# Common settings
binary_log=false
configuration='Release'
verbosity='minimal'

# Actions
clean=false
test=false

# Source-only settings
sourceOnly=false
releaseManifest=''
sourceRepository=''
sourceVersion=''
CUSTOM_PACKAGES_DIR=''
CUSTOM_SDK_DIR=''
packagesDir="$scriptroot/prereqs/packages/"
packagesArchiveDir="${packagesDir}archive/"
packagesPreviouslySourceBuiltDir="${packagesDir}previously-source-built/"

# Advanced settings
ci=false
exclude_ci_binary_log=false
prepare_machine=false
target_rid=
system_libs=

properties=()
while [[ $# > 0 ]]; do
  opt="$(echo "${1/#--/-}" | tr "[:upper:]" "[:lower:]")"
  case "$opt" in
    # Common settings
    -binarylog|-bl)
      binary_log=true
      ;;
    -configuration|-c)
      configuration=$2
      shift
      ;;
    -rid|-target-rid)
      target_rid=$2
      shift
      ;;
    -with-system-libs)
      system_libs=$2
      shift
      ;;
    -verbosity|-v)
      verbosity=$2
      shift
      ;;

    # Actions
    -clean)
      clean=true
      ;;
    -help|-h|-\?|/?)
      usage
      exit 0
      ;;
    -test|-t)
      test=true
      ;;
    -sign)
      properties+=( "/p:Sign=true" )
      ;;

    # Source-only settings
    -source-only|-source-build|-so|-sb)
      sourceOnly=true
      properties+=( "/p:DotNetBuildSourceOnly=true" )
      ;;
    -online)
      properties+=( "/p:DotNetBuildWithOnlineFeeds=true" )
      ;;
    -poison)
      properties+=( "/p:EnablePoison=true" )
      ;;
    -release-manifest)
      releaseManifest="$2"
      shift
      ;;
    -source-repository)
      sourceRepository="$2"
      shift
      ;;
    -source-version)
      sourceVersion="$2"
      shift
      ;;
    -with-packages)
      CUSTOM_PACKAGES_DIR="$(cd -P "$2" && pwd)"
      if [ ! -d "$CUSTOM_PACKAGES_DIR" ]; then
          echo "Custom prviously built packages directory '$CUSTOM_PACKAGES_DIR' does not exist"
          exit 1
      fi
      shift
      ;;
    -with-sdk)
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

    # Advanced settings
    -build-repo-tests)
      properties+=( "/p:DotNetBuildTests=true" )
      ;;
    -ci)
      ci=true
      ;;
    -clean-while-building|-cwb)
      properties+=( "/p:CleanWhileBuilding=true" )
      ;;
    -excludecibinarylog|-nobl)
      exclude_ci_binary_log=true
      ;;
    -preparemachine)
      prepare_machine=true
      ;;
    -use-mono-runtime)
      properties+=( "/p:DotNetBuildUseMonoRuntime=true" )
      ;;
    *)
      properties+=( "$1" )
      ;;
  esac

  shift
done

if [[ "$ci" == true ]]; then
  if [[ "$exclude_ci_binary_log" == false ]]; then
    binary_log=true
  fi
fi

. "$scriptroot/eng/common/tools.sh"

project="$scriptroot/build.proj"
targets="/t:Build"

# This repo uses the VSTest integration instead of the Arcade Test target
if [[ "$test" == true ]]; then
  project="$scriptroot/test/tests.proj"
  targets="$targets;VSTest"
  # Workaround for vstest hangs (https://github.com/microsoft/vstest/issues/5091) [TODO]
  export MSBUILDENSURESTDOUTFORTASKPROCESSES=1
  # Ensure all test projects share stdout (https://github.com/dotnet/source-build/issues/4635#issuecomment-2397464519)
  export MSBUILDDISABLENODEREUSE=1
fi

function Build {
  if [[ "$sourceOnly" != "true" ]]; then

    InitializeToolset

    local bl=""
    if [[ "$binary_log" == true ]]; then
      bl="/bl:\"$log_dir/Build.binlog\""
    fi

    MSBuild --restore \
      $project \
      $targets \
      $bl \
      /p:Configuration=$configuration \
      "${properties[@]}"

    ExitWithExitCode 0

  else
    # Don't use the global nuget cache folder when building source-only which
    # restores prebuilt packages that should never get into the global nuget cache.
    use_global_nuget_cache=false
    GetNuGetPackageCachePath

    if [[ "$test" == true ]]; then
      # When building source-only, use a custom package cache for tests to make prebuilt detection work.
      if [[ "$sourceOnly" == "true" ]]; then
        export NUGET_PACKAGES="${NUGET_PACKAGES}tests"
      fi
    fi

    echo "$NUGET_PACKAGES"

    if [ "$ci" == "true" ]; then
      properties+=( "/p:ContinuousIntegrationBuild=true" )
    fi

    if [ "$test" != "true" ]; then
      initSourceOnlyBinaryLog=""
      if [[ "$binary_log" == true ]]; then
        initSourceOnlyBinaryLog="/bl:\"$log_dir/init-source-only.binlog\""
      fi

      "$CLI_ROOT/dotnet" build-server shutdown --msbuild
      "$CLI_ROOT/dotnet" msbuild "$scriptroot/eng/init-source-only.proj" $initSourceOnlyBinaryLog "${properties[@]}"
      # kill off the MSBuild server so that on future invocations we pick up our custom SDK Resolver
      "$CLI_ROOT/dotnet" build-server shutdown --msbuild
    fi

    # Point MSBuild to the custom SDK resolvers folder, so it will pick up our custom SDK Resolver
    export MSBUILDADDITIONALSDKRESOLVERSFOLDER="$scriptroot/artifacts/toolset/VSSdkResolvers/"

    local bl=""
    if [[ "$binary_log" == true ]]; then
      bl="/bl:\"$log_dir/Build.binlog\""
    fi

    "$CLI_ROOT/dotnet" msbuild --restore "$project" $bl $targets "${properties[@]}"
  fi
}

if [[ "$clean" == true ]]; then
  if [ -d "$artifacts_dir" ]; then
    rm -rf $artifacts_dir
    echo "Artifacts directory deleted."
  fi
  exit 0
fi

# Initialize __DistroRid and __PortableTargetOS
source $scriptroot/eng/common/native/init-os-and-arch.sh
source $scriptroot/eng/common/native/init-distro-rid.sh
initDistroRidGlobal "$os" "$arch" ""
if [[ -n "$target_rid" ]]; then
  properties+=( "/p:TargetRid=$target_rid" )
fi
if [[ -n "$system_libs" ]]; then
  properties+=( "/p:UseSystemLibs=$system_libs" )
fi

# Source-only settings
if [[ "$sourceOnly" == "true" ]]; then
  # For build purposes, we need to make sure we have all the SourceLink information
  if [ "$test" != "true" ]; then
    GIT_DIR="$scriptroot/.git"
    if [ -f "$GIT_DIR/index" ]; then # We check for index because if outside of git, we create config and HEAD manually
      if [ -n "$sourceRepository" ] || [ -n "$sourceVersion" ] || [ -n "$releaseManifest" ]; then
        echo "ERROR: Source Link arguments cannot be used in a git repository"
        exit 1
      fi
    else
      if [ -z "$releaseManifest" ]; then
        if [ -z "$sourceRepository" ] || [ -z "$sourceVersion" ]; then
          echo "ERROR: $scriptroot is not a git repository, either --release-manifest or --source-repository and --source-version must be specified"
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

  # Support custom source built package locations
  if [ "$CUSTOM_PACKAGES_DIR" != "" ]; then
    if [ "$test" == "true" ]; then
      properties+=( "/p:CustomSourceBuiltPackagesPath=$CUSTOM_PACKAGES_DIR" )
    else
      properties+=( "/p:CustomPrebuiltSourceBuiltPackagesPath=$CUSTOM_PACKAGES_DIR" )
    fi
  fi

  if [ ! -d "$scriptroot/.git" ]; then
    echo "ERROR: $scriptroot is not a git repository."
    exit 1
  fi

  # Allow a custom SDK directory to be specified
  if [ -d "$CUSTOM_SDK_DIR" ]; then
    export SDK_VERSION=$("$CUSTOM_SDK_DIR/dotnet" --version)
    export CLI_ROOT="$CUSTOM_SDK_DIR"
    export _InitializeDotNetCli="$CLI_ROOT/dotnet"
    export DOTNET_INSTALL_DIR="$CLI_ROOT"
    echo "Using custom bootstrap SDK from '$CLI_ROOT', version '$SDK_VERSION'"
  else
    sdkLine=$(grep -m 1 'dotnet' "$scriptroot/global.json")
    sdkPattern="\"dotnet\" *: *\"(.*)\""
    if [[ $sdkLine =~ $sdkPattern ]]; then
      export SDK_VERSION=${BASH_REMATCH[1]}
      export CLI_ROOT="$scriptroot/.dotnet"
    fi
  fi

  # Find the Arcade SDK version and set env vars for the msbuild sdk resolver
  packageVersionsPath=''

  if [[ "$CUSTOM_PACKAGES_DIR" != "" && -f "$CUSTOM_PACKAGES_DIR/PackageVersions.props" ]]; then
    packageVersionsPath="$CUSTOM_PACKAGES_DIR/PackageVersions.props"
  elif [ -d "$packagesArchiveDir" ]; then
    sourceBuiltArchive=$(find "$packagesArchiveDir" -maxdepth 1 -name 'Private.SourceBuilt.Artifacts*.tar.gz')
    if [ -f "${packagesPreviouslySourceBuiltDir}PackageVersions.props" ]; then
      packageVersionsPath=${packagesPreviouslySourceBuiltDir}PackageVersions.props
    elif [ -f "$sourceBuiltArchive" ]; then
      tar -xzf "$sourceBuiltArchive" -C /tmp PackageVersions.props
      packageVersionsPath=/tmp/PackageVersions.props
    fi
  fi

  if [ ! -f "$packageVersionsPath" ]; then
    echo "Cannot find PackagesVersions.props.  Debugging info:"
    echo "  Attempted custom PVP path: $CUSTOM_PACKAGES_DIR/PackageVersions.props"
    echo "  Attempted previously-source-built path: ${packagesPreviouslySourceBuiltDir}PackageVersions.props"
    echo "  Attempted archive path: $packagesArchiveDir"
    exit 1
  fi

  # Extract toolset packages

  # Ensure that by default, the bootstrap version of the toolset SDK is used. Source-build infra
  # projects use bootstrap toolset SDKs, and would fail to find it in the build. The repo
  # projects overwrite this so that they use the source-built toolset SDK instad.

  # 1. Microsoft.DotNet.Arcade.Sdk
  arcadeSdkLine=$(grep -m 1 'MicrosoftDotNetArcadeSdkVersion' "$packageVersionsPath")
  arcadeSdkPattern="<MicrosoftDotNetArcadeSdkVersion>(.*)</MicrosoftDotNetArcadeSdkVersion>"
  if [[ $arcadeSdkLine =~ $arcadeSdkPattern ]]; then
    export ARCADE_BOOTSTRAP_VERSION=${BASH_REMATCH[1]}

    export SOURCE_BUILT_SDK_ID_ARCADE=Microsoft.DotNet.Arcade.Sdk
    export SOURCE_BUILT_SDK_VERSION_ARCADE=$ARCADE_BOOTSTRAP_VERSION
    export SOURCE_BUILT_SDK_DIR_ARCADE=$packagesRestoredDir/BootstrapPackages/microsoft.dotnet.arcade.sdk/$ARCADE_BOOTSTRAP_VERSION
  fi

  # 2. Microsoft.Build.NoTargets
  notargetsSdkLine=$(grep -m 1 'Microsoft.Build.NoTargets' "$scriptroot/global.json")
  notargetsSdkPattern="\"Microsoft\.Build\.NoTargets\" *: *\"(.*)\""
  if [[ $notargetsSdkLine =~ $notargetsSdkPattern ]]; then
    export NOTARGETS_BOOTSTRAP_VERSION=${BASH_REMATCH[1]}

    export SOURCE_BUILT_SDK_ID_NOTARGETS=Microsoft.Build.NoTargets
    export SOURCE_BUILT_SDK_VERSION_NOTARGETS=$NOTARGETS_BOOTSTRAP_VERSION
    export SOURCE_BUILT_SDK_DIR_NOTARGETS=$packagesRestoredDir/BootstrapPackages/microsoft.build.notargets/$NOTARGETS_BOOTSTRAP_VERSION
  fi

  # 3. Microsoft.Build.Traversal
  traversalSdkLine=$(grep -m 1 'Microsoft.Build.Traversal' "$scriptroot/global.json")
  traversalSdkPattern="\"Microsoft\.Build\.Traversal\" *: *\"(.*)\""
  if [[ $traversalSdkLine =~ $traversalSdkPattern ]]; then
    export TRAVERSAL_BOOTSTRAP_VERSION=${BASH_REMATCH[1]}

    export SOURCE_BUILT_SDK_ID_TRAVERSAL=Microsoft.Build.Traversal
    export SOURCE_BUILT_SDK_VERSION_TRAVERSAL=$TRAVERSAL_BOOTSTRAP_VERSION
    export SOURCE_BUILT_SDK_DIR_TRAVERSAL=$packagesRestoredDir/BootstrapPackages/microsoft.build.traversal/$TRAVERSAL_BOOTSTRAP_VERSION
  fi

  echo "Found bootstrap versions: SDK $SDK_VERSION, Arcade $ARCADE_BOOTSTRAP_VERSION, NoTargets $NOTARGETS_BOOTSTRAP_VERSION and Traversal $TRAVERSAL_BOOTSTRAP_VERSION"
fi

Build
