#!/usr/bin/env bash
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

# Publishes the NativeAOT test binary, dotnet-aot library, and dn host, then runs
# the full test suite including end-to-end dn integration.
# Build the redist SDK first; the script copies its SDK content into an isolated
# host-native installation and overlays the test-built native library there.
# Test setup and managed fallback use that SDK, not the cross-built host.
# See run-aot-tests.ps1 for detailed documentation.
#
# Usage:
#   ./run-aot-tests.sh [--configuration Debug|Release] [--rid <RID>] [--no-build] \
#                      [--trx] [--results-directory <DIR>]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DOTNET="$REPO_ROOT/.dotnet/dotnet"
TEST_PROJECT="$SCRIPT_DIR/dotnet-aot.Tests.csproj"
PRODUCT_PROJECT="$REPO_ROOT/src/Cli/dotnet-aot/dotnet-aot.csproj"
DN_PROJECT="$REPO_ROOT/src/Cli/dn/dn.csproj"
LAYOUT_PROJECT="$SCRIPT_DIR/PrepareTestSdk.proj"

CONFIGURATION="Debug"
RID=""
NO_BUILD=false
TRX=false
RESULTS_DIRECTORY=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration|-c) CONFIGURATION="$2"; shift 2 ;;
        --rid|-r) RID="$2"; shift 2 ;;
        --no-build) NO_BUILD=true; shift ;;
        --trx) TRX=true; shift ;;
        --results-directory) RESULTS_DIRECTORY="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Auto-detect RID
if [[ -z "$RID" ]]; then
    RID=$("$DOTNET" --info 2>/dev/null | grep "RID:" | head -1 | sed 's/.*RID:\s*//' | tr -d '[:space:]') || true
    if [[ -z "$RID" ]]; then
        OS=$(uname -s | tr '[:upper:]' '[:lower:]')
        ARCH=$(uname -m)
        case "$OS" in
            linux) OS="linux" ;;
            darwin) OS="osx" ;;
            *) OS="linux" ;;
        esac
        case "$ARCH" in
            x86_64|amd64) ARCH="x64" ;;
            aarch64|arm64) ARCH="arm64" ;;
        esac
        RID="$OS-$ARCH"
    fi
fi

PUBLISH_DIR="$SCRIPT_DIR/artifacts/aot-tests/$CONFIGURATION/$RID"
EXE_PATH="$PUBLISH_DIR/dotnet-aot.Tests"
AOT_PUBLISH_DIR="$SCRIPT_DIR/artifacts/dotnet-aot/$CONFIGURATION/$RID"
DN_PUBLISH_DIR="$SCRIPT_DIR/artifacts/dn/$CONFIGURATION/$RID"
case "$RID" in
    win-*) AOT_LIBRARY_NAME="dotnet-aot.dll"; DN_NAME="dn.exe" ;;
    osx-*) AOT_LIBRARY_NAME="libdotnet-aot.dylib"; DN_NAME="dn" ;;
    *) AOT_LIBRARY_NAME="libdotnet-aot.so"; DN_NAME="dn" ;;
esac
AOT_LIBRARY_PATH="$AOT_PUBLISH_DIR/$AOT_LIBRARY_NAME"
DN_PATH="$DN_PUBLISH_DIR/$DN_NAME"
TEST_DOTNET_ROOT="$SCRIPT_DIR/artifacts/test-sdk/$CONFIGURATION/$RID"
TEST_DOTNET="$TEST_DOTNET_ROOT/dotnet"

echo "=== dotnet-aot NativeAOT Test Runner ==="
echo "  Configuration: $CONFIGURATION"
echo "  RID:           $RID"
echo "  Publish dir:   $PUBLISH_DIR"
echo ""

# Publish
if [[ "$NO_BUILD" == false ]]; then
    echo "Publishing as NativeAOT..."

    # Managed module integration also needs the full Build output.
    "$DOTNET" publish "$TEST_PROJECT" \
        -c "$CONFIGURATION" \
        -r "$RID" \
        -p:PublishAotTests=true \
        -p:UseOptimizedPublish=false \
        -p:PublishDir="$PUBLISH_DIR"

    "$DOTNET" publish "$PRODUCT_PROJECT" \
        -c "$CONFIGURATION" \
        -r "$RID" \
        -p:PublishDir="$AOT_PUBLISH_DIR"

    "$DOTNET" publish "$DN_PROJECT" \
        -c "$CONFIGURATION" \
        -r "$RID" \
        -p:PublishDir="$DN_PUBLISH_DIR"

    echo ""
    echo "Published: $EXE_PATH"
    if [[ -f "$EXE_PATH" ]]; then
        SIZE=$(du -h "$EXE_PATH" | cut -f1)
        echo "Size:      $SIZE"
    fi
    echo ""
fi

# Prepare even with --no-build so runtime versions and SDK content cannot go stale.
"$DOTNET" msbuild "$LAYOUT_PROJECT" -restore -t:PrepareTestSdk \
    -p:Configuration="$CONFIGURATION" -p:RuntimeIdentifier="$RID"

# Do not resolve through --info here: the repository global.json selects the bootstrap SDK.
SDK_ASSEMBLIES=("$TEST_DOTNET_ROOT"/sdk/*/dotnet.dll)
if [[ ${#SDK_ASSEMBLIES[@]} -ne 1 || ! -f "${SDK_ASSEMBLIES[0]}" ]]; then
    echo "ERROR: Expected one test SDK in $TEST_DOTNET_ROOT."
    exit 1
fi
SDK_DIRECTORY="$(dirname "${SDK_ASSEMBLIES[0]}")"

# Run
if [[ ! -f "$EXE_PATH" ]]; then
    echo "ERROR: Published binary not found at $EXE_PATH"
    echo "Run without --no-build to publish first."
    exit 1
fi
if [[ ! -f "$AOT_LIBRARY_PATH" ]]; then
    echo "ERROR: Published native library not found at $AOT_LIBRARY_PATH"
    exit 1
fi
if [[ ! -f "$DN_PATH" ]]; then
    echo "ERROR: Published dn host not found at $DN_PATH"
    exit 1
fi

echo "Running AOT tests..."
echo ""

cp "$AOT_LIBRARY_PATH" "$SDK_DIRECTORY/$AOT_LIBRARY_NAME"

export DOTNET_AOT_TEST_SDK_DIRECTORY="$SDK_DIRECTORY"
export DOTNET_AOT_TEST_DN_PATH="$DN_PATH"
MANAGED_TEST_MODULE=$("$DOTNET" msbuild "$TEST_PROJECT" \
    -p:Configuration="$CONFIGURATION" \
    -p:RuntimeIdentifier="$RID" \
    -p:PublishAotTests=true \
    -p:UseOptimizedPublish=false \
    -p:PublishDir="$PUBLISH_DIR" \
    -getProperty:TargetPath)
if [[ ! -f "$MANAGED_TEST_MODULE" ]]; then
    echo "ERROR: Managed test module not found at $MANAGED_TEST_MODULE for Native AOT integration validation."
    exit 1
fi
export DOTNET_AOT_TEST_MANAGED_TEST_MODULE="$MANAGED_TEST_MODULE"
export DOTNET_AOT_TEST_NUGET_CONFIG="$REPO_ROOT/NuGet.config"
export DOTNET_AOT_SDK_DIR="$SDK_DIRECTORY"
export DOTNET_AOT_LIBRARY_DIR="$SDK_DIRECTORY"
export DOTNET_HOST_PATH="$TEST_DOTNET"
export DOTNET_ROOT="$TEST_DOTNET_ROOT"
# Do not leave build servers holding files in the disposable test installation.
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export MSBUILDDISABLENODEREUSE=1

chmod +x "$EXE_PATH"
chmod +x "$DN_PATH"

# When --trx is set, emit a TRX report (the AOT test binary is a Microsoft.Testing.Platform
# app, so it accepts the --report-trx options) so CI can publish the results.
RUN_ARGS=()
if [[ "$TRX" == true ]]; then
    if [[ -z "$RESULTS_DIRECTORY" ]]; then
        RESULTS_DIRECTORY="$REPO_ROOT/artifacts/TestResults/$CONFIGURATION"
    fi
    mkdir -p "$RESULTS_DIRECTORY"
    RUN_ARGS+=(--report-trx --report-trx-filename dotnet-aot.Tests.trx --results-directory "$RESULTS_DIRECTORY")
fi

set +e
"$EXE_PATH" ${RUN_ARGS[@]+"${RUN_ARGS[@]}"}
TEST_EXIT=$?
set -e

"$TEST_DOTNET" exec "$SDK_DIRECTORY/dotnet.dll" build-server shutdown

echo ""
if [[ $TEST_EXIT -eq 0 ]]; then
    echo "All AOT tests passed."
else
    echo "AOT tests failed with exit code $TEST_EXIT."
fi

exit $TEST_EXIT
