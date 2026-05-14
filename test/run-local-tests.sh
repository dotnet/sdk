#!/bin/bash
# PoC: Run SDK tests locally in parallel instead of using Helix.
# This script provides fine-grained control over parallelism and timing.
#
# Usage:
#   ./test/run-local-tests.sh [--max-parallel N] [--configuration Release]
#
# Prerequisites:
#   - SDK must already be built (run ./build.sh first)
#   - GNU parallel is recommended but not required (falls back to xargs)
#
# This script:
#   1. Discovers the same test projects that UnitTests.proj sends to Helix
#   2. Builds them all
#   3. Runs them in parallel (configurable concurrency)
#   4. Produces TRX results for AzDO consumption
#   5. Reports per-project timing and overall wall-clock time

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Defaults
CONFIGURATION="${CONFIGURATION:-Release}"
MAX_PARALLEL="${MAX_PARALLEL:-$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 8)}"
RESULTS_DIR="$REPO_ROOT/artifacts/TestResults/$CONFIGURATION"
DOTNET="$REPO_ROOT/artifacts/bin/redist/$CONFIGURATION/dotnet/dotnet"
TIMING_LOG="$RESULTS_DIR/timing.log"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --max-parallel) MAX_PARALLEL="$2"; shift 2 ;;
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        --dotnet) DOTNET="$2"; shift 2 ;;
        --help) echo "Usage: $0 [--max-parallel N] [--configuration Release] [--dotnet path]"; exit 0 ;;
        *) echo "Unknown arg: $1"; exit 1 ;;
    esac
done

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║  LOCAL TEST RUN - PoC: No Helix                             ║"
echo "║  Running the SAME tests that Helix would run, but locally.  ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""
echo "Configuration:  $CONFIGURATION"
echo "Max parallel:   $MAX_PARALLEL"
echo "Results dir:    $RESULTS_DIR"
echo "Dotnet path:    $DOTNET"
echo ""

mkdir -p "$RESULTS_DIR"

# Discover test projects (same logic as UnitTests.proj)
echo "Discovering test projects..."
TEST_PROJECTS=()
cd "$REPO_ROOT/test"

# Include *.Tests.csproj (excluding AoT and TestAssets)
while IFS= read -r -d '' proj; do
    TEST_PROJECTS+=("$proj")
done < <(find . -name "*.Tests.csproj" \
    ! -name "*.AoT.Tests.csproj" \
    ! -path "*/TestAssets/*" \
    -print0)

# Include *UnitTests.csproj and *IntegrationTests.csproj (excluding TestAssets)
while IFS= read -r -d '' proj; do
    TEST_PROJECTS+=("$proj")
done < <(find . \( -name "*UnitTests.csproj" -o -name "*IntegrationTests.csproj" \) \
    ! -path "*/TestAssets/*" \
    -print0)

# Include NetAnalyzers tests
while IFS= read -r -d '' proj; do
    TEST_PROJECTS+=("$proj")
done < <(find "$REPO_ROOT/src/Microsoft.CodeAnalysis.NetAnalyzers/tests" -name "*Tests.csproj" -print0 2>/dev/null || true)

# Remove duplicates
mapfile -t TEST_PROJECTS < <(printf '%s\n' "${TEST_PROJECTS[@]}" | sort -u)

# Apply exclusions (same as UnitTests.proj)
EXCLUDED_PATTERNS=(
    "dotnet-MsiInstallation.Tests"
    "dotnet-format.UnitTests"
    "Microsoft.DotNet.ApiCompat.IntegrationTests"
    "Microsoft.DotNet.MSBuildSdkResolver.Tests"
)

FILTERED_PROJECTS=()
for proj in "${TEST_PROJECTS[@]}"; do
    excluded=false
    for pattern in "${EXCLUDED_PATTERNS[@]}"; do
        if [[ "$proj" == *"$pattern"* ]]; then
            excluded=true
            break
        fi
    done
    if [[ "$excluded" == "false" ]]; then
        FILTERED_PROJECTS+=("$proj")
    fi
done

cd "$REPO_ROOT"
echo "Found ${#FILTERED_PROJECTS[@]} test projects to run."
echo ""

# Function to run a single test project and capture timing
run_test() {
    local project="$1"
    local project_name
    project_name=$(basename "$project" .csproj)
    local trx_file="$RESULTS_DIR/${project_name}.trx"
    local log_file="$RESULTS_DIR/${project_name}.log"
    local start_time end_time duration exit_code

    start_time=$(date +%s)

    # Run the test
    "$DOTNET" test "$project" \
        --configuration "$CONFIGURATION" \
        --no-build \
        --results-directory "$RESULTS_DIR" \
        --logger "trx;LogFileName=${project_name}.trx" \
        --logger "console;verbosity=normal" \
        --blame-hang \
        --blame-hang-timeout 60min \
        > "$log_file" 2>&1 || true

    exit_code=$?
    end_time=$(date +%s)
    duration=$((end_time - start_time))

    if [[ $exit_code -eq 0 ]]; then
        echo "✓ PASS  ${project_name} (${duration}s)"
    else
        echo "✗ FAIL  ${project_name} (${duration}s) - see $log_file"
    fi

    echo "${duration}s ${project_name} exit=$exit_code" >> "$TIMING_LOG"
}
export -f run_test
export DOTNET CONFIGURATION RESULTS_DIR TIMING_LOG

# Clear previous timing log
> "$TIMING_LOG"

echo "═══════════════════════════════════════════════════════════════"
echo "  Phase 1: Building all test projects in parallel"
echo "═══════════════════════════════════════════════════════════════"
BUILD_START=$(date +%s)

# Build all projects first (parallel via MSBuild)
"$DOTNET" msbuild "$REPO_ROOT/test/LocalTests.proj" \
    /t:RestoreTestProjects \
    /p:Configuration="$CONFIGURATION" \
    /maxcpucount \
    -nologo -verbosity:minimal || true

# Build using dotnet build on each project via parallel
printf '%s\n' "${FILTERED_PROJECTS[@]}" | \
    xargs -P "$MAX_PARALLEL" -I {} \
    "$DOTNET" build {} --configuration "$CONFIGURATION" --no-restore -nologo -verbosity:minimal 2>/dev/null || true

BUILD_END=$(date +%s)
BUILD_DURATION=$((BUILD_END - BUILD_START))
echo ""
echo "Build phase completed in ${BUILD_DURATION}s"
echo ""

echo "═══════════════════════════════════════════════════════════════"
echo "  Phase 2: Running all tests in parallel (max $MAX_PARALLEL concurrent)"
echo "═══════════════════════════════════════════════════════════════"
TEST_START=$(date +%s)

# Run tests in parallel
if command -v parallel &>/dev/null; then
    # GNU parallel available - best option for controlled parallelism
    printf '%s\n' "${FILTERED_PROJECTS[@]}" | \
        parallel --jobs "$MAX_PARALLEL" --bar run_test {}
else
    # Fallback to xargs
    printf '%s\n' "${FILTERED_PROJECTS[@]}" | \
        xargs -P "$MAX_PARALLEL" -I {} bash -c 'run_test "$@"' _ {}
fi

TEST_END=$(date +%s)
TEST_DURATION=$((TEST_END - TEST_START))

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  RESULTS SUMMARY"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Build wall-clock time:  ${BUILD_DURATION}s ($((BUILD_DURATION / 60))m $((BUILD_DURATION % 60))s)"
echo "Test wall-clock time:   ${TEST_DURATION}s ($((TEST_DURATION / 60))m $((TEST_DURATION % 60))s)"
echo "Total wall-clock time:  $((BUILD_DURATION + TEST_DURATION))s ($(( (BUILD_DURATION + TEST_DURATION) / 60))m $(( (BUILD_DURATION + TEST_DURATION) % 60))s)"
echo "Parallelism:            $MAX_PARALLEL concurrent"
echo "Test projects:          ${#FILTERED_PROJECTS[@]}"
echo ""

# Show timing breakdown (sorted by duration, longest first)
if [[ -f "$TIMING_LOG" ]]; then
    echo "Top 20 longest-running test projects:"
    sort -rn "$TIMING_LOG" | head -20
    echo ""

    # Count pass/fail
    PASS_COUNT=$(grep "exit=0" "$TIMING_LOG" | wc -l)
    FAIL_COUNT=$(grep -v "exit=0" "$TIMING_LOG" | wc -l)
    echo "Passed: $PASS_COUNT  Failed: $FAIL_COUNT"
fi

echo ""
echo "TRX results for AzDO: $RESULTS_DIR/*.trx"
echo ""
echo "To publish in AzDO, add this step to your pipeline:"
echo "  - task: PublishTestResults@2"
echo "    inputs:"
echo "      testResultsFormat: VSTest"
echo "      testResultsFiles: 'artifacts/TestResults/$CONFIGURATION/*.trx'"
echo "      mergeTestResults: true"
