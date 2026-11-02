#!/usr/bin/env bash

# make NuGet network operations more robust
export NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true
export NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT=6
export NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS=1000

export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

# REM Enable crash dump collection for .NET processes.
export DOTNET_DbgEnableMiniDump=1
export DOTNET_DbgMiniDumpType=4
export DOTNET_DbgMiniDumpName=$HELIX_WORKITEM_UPLOAD_ROOT/coredump.%p
export DOTNET_EnableCrashReport=1

export TestExecutionDirectory=$(realpath "$(mktemp -d "${TMPDIR:-/tmp}"/dotnetSdkTests.XXXXXXXX)")

# Azure Linux 3 is missing zlib-devel, so the linker can't find -lz for NativeAOT.
# Create a symlink in a local directory and add it to LIBRARY_PATH so clang/ld finds it.
if [ -f /etc/azurelinux-release ]; then
    REAL_LIBZ=$(find /usr/lib64 /usr/lib -maxdepth 1 -name "libz.so.1*" ! -type l 2>/dev/null | head -1)
    if [ -n "$REAL_LIBZ" ] && [ ! -f "$(dirname "$REAL_LIBZ")/libz.so" ]; then
        ZLIB_COMPAT_DIR="$TestExecutionDirectory/zlib_compat"
        mkdir -p "$ZLIB_COMPAT_DIR"
        ln -sf "$REAL_LIBZ" "$ZLIB_COMPAT_DIR/libz.so"
        export LIBRARY_PATH="${ZLIB_COMPAT_DIR}${LIBRARY_PATH:+:$LIBRARY_PATH}"
    fi
fi

export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/
mkdir -p $TestExecutionDirectory/Testpackages

export DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory
export DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=$HELIX_CORRELATION_PAYLOAD/r
export DOTNET_SDK_TEST_ASSETS_DIRECTORY=$TestExecutionDirectory/TestAssets
export DOTNET_SDK_TEST_REPO_TEMPLATE_PACKAGES=$TestExecutionDirectory/template_feed
export DOTNET_SDK_TEST_TEMPLATE_SAMPLES_DIR=$TestExecutionDirectory/TemplateSamples

# Call dotnet new so the first run message doesn't interfere with the first test.
dotnet new --debug:ephemeral-hive
