#!/usr/bin/env bash
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

# Publishes and runs the dotnet-aot tests as a NativeAOT binary.
# Uses xUnit v3 AOT packages (source-generator-based test discovery).
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

echo "=== dotnet-aot NativeAOT Test Runner ==="
echo "  Configuration: $CONFIGURATION"
echo "  RID:           $RID"
echo "  Publish dir:   $PUBLISH_DIR"
echo ""

# Publish
if [[ "$NO_BUILD" == false ]]; then
    echo "Publishing as NativeAOT..."

    "$DOTNET" publish "$TEST_PROJECT" \
        -c "$CONFIGURATION" \
        -r "$RID" \
        -p:PublishAotTests=true \
        -p:PublishDir="$PUBLISH_DIR"

    echo ""
    echo "Published: $EXE_PATH"
    if [[ -f "$EXE_PATH" ]]; then
        SIZE=$(du -h "$EXE_PATH" | cut -f1)
        echo "Size:      $SIZE"
    fi
    echo ""
fi

# Run
if [[ ! -f "$EXE_PATH" ]]; then
    echo "ERROR: Published binary not found at $EXE_PATH"
    echo "Run without --no-build to publish first."
    exit 1
fi

echo "Running AOT tests..."
echo ""

chmod +x "$EXE_PATH"

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

echo ""
if [[ $TEST_EXIT -eq 0 ]]; then
    echo "All AOT tests passed."
else
    echo "AOT tests failed with exit code $TEST_EXIT."
fi

exit $TEST_EXIT
