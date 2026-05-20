#!/usr/bin/env bash
# Determines which test groups should run based on files changed in a PR.
# Outputs AzDO pipeline variables (##vso[task.setvariable ...]) consumed by UnitTests.proj.
#
# Design principles:
#   - Default to running ALL tests (fail-open, never silently skip)
#   - CI builds always run all tests
#   - Codeflow PRs (darc-*) always run all tests
#   - Dependency version changes always run all tests
#   - Only narrow scope for normal human PRs with isolated changes

set -e

# All flags default to true (run everything)
RunAnalyzerTests=true
RunBlazorTests=true
RunSwaTests=true
RunWatchTests=true
RunTemplateTests=true
RunContainerTests=true

SHOULD_FILTER=false

# Manual override: if ForceRunAllTests is set, run everything
if [ "$FORCE_RUN_ALL_TESTS" = "true" ] || [ "$FORCE_RUN_ALL_TESTS" = "True" ]; then
  echo "##[section]ForceRunAllTests parameter set - running all tests"
# Only attempt filtering for PR builds
elif [ "$BUILD_REASON" = "PullRequest" ]; then
  SOURCE_BRANCH="${SYSTEM_PULLREQUEST_SOURCEBRANCH:-}"

  # Strip refs/heads/ prefix if present
  SOURCE_BRANCH="${SOURCE_BRANCH#refs/heads/}"

  # Codeflow PRs (dotnet-maestro) use branch names starting with "darc-"
  if [[ "$SOURCE_BRANCH" == darc-* ]]; then
    echo "##[section]Codeflow PR detected (branch: $SOURCE_BRANCH) - running all tests"
  else
    # Attempt to get the list of changed files
    TARGET_BRANCH="${SYSTEM_PULLREQUEST_TARGETBRANCH:-main}"
    TARGET_BRANCH="${TARGET_BRANCH#refs/heads/}"

    # Fetch the target branch to have a diff base
    git fetch origin "$TARGET_BRANCH" --depth=1 2>/dev/null || true

    # Use two-arg diff (tree comparison) instead of three-dot (merge-base)
    # because AzDO shallow clones lack the history for merge-base computation.
    CHANGED_FILES=$(git diff --name-only "origin/$TARGET_BRANCH" HEAD 2>/dev/null || echo "")

    if [ -z "$CHANGED_FILES" ]; then
      echo "##[warning]Could not determine changed files - running all tests"
    else
      echo "##[section]Changed files in PR:"
      echo "$CHANGED_FILES" | head -50
      if [ "$(echo "$CHANGED_FILES" | wc -l)" -gt 50 ]; then
        echo "  ... ($(echo "$CHANGED_FILES" | wc -l) total files)"
      fi

      # Check for broad-impact changes that require full test runs
      if echo "$CHANGED_FILES" | grep -qE "^eng/Versions\.props$|^eng/Version\.Details\.(xml|props)$"; then
        echo "##[section]Dependency version files changed - running all tests"
      elif echo "$CHANGED_FILES" | grep -qE "^eng/ManualVersions\.props$|^global\.json$"; then
        echo "##[section]Global build configuration changed - running all tests"
      elif echo "$CHANGED_FILES" | grep -qE "^Directory\.Build\.(props|targets)$|^Directory\.Packages\.props$"; then
        echo "##[section]Root build files changed - running all tests"
      else
        # Safe to filter - determine which areas are affected
        SHOULD_FILTER=true
      fi
    fi
  fi
else
  echo "##[section]Non-PR build (Reason: $BUILD_REASON) - running all tests"
fi

if [ "$SHOULD_FILTER" = "true" ]; then
  echo "##[section]Applying path-based test filtering"

  # NetAnalyzers: only when analyzer source or tests change
  if ! echo "$CHANGED_FILES" | grep -qE "^src/Microsoft\.CodeAnalysis\.NetAnalyzers/"; then
    RunAnalyzerTests=false
    echo "  Skipping: NetAnalyzer tests (no changes in src/Microsoft.CodeAnalysis.NetAnalyzers/)"
  fi

  # Blazor WASM: only when Blazor/Wasm SDK source or tests change
  if ! echo "$CHANGED_FILES" | grep -qE "^src/BlazorWasmSdk/|^src/WasmSdk/|^test/Microsoft\.NET\.Sdk\.BlazorWebAssembly"; then
    RunBlazorTests=false
    echo "  Skipping: Blazor WASM tests (no changes in src/BlazorWasmSdk/, src/WasmSdk/)"
  fi

  # Static Web Assets: only when SWA source or tests change
  if ! echo "$CHANGED_FILES" | grep -qE "^src/StaticWebAssetsSdk/|^test/Microsoft\.NET\.Sdk\.StaticWebAssets"; then
    RunSwaTests=false
    echo "  Skipping: Static Web Assets tests (no changes in src/StaticWebAssetsSdk/)"
  fi

  # dotnet-watch: only when watch source or tests change
  if ! echo "$CHANGED_FILES" | grep -qE "^src/Dotnet\.Watch/|^test/dotnet-watch\.|^test/Microsoft\.AspNetCore\.Watch|^test/Microsoft\.DotNet\.HotReload|^test/Microsoft\.Extensions\.DotNetDeltaApplier"; then
    RunWatchTests=false
    echo "  Skipping: dotnet-watch tests (no changes in src/Dotnet.Watch/)"
  fi

  # Template Engine: only when template source or tests change
  if ! echo "$CHANGED_FILES" | grep -qE "^src/TemplateEngine/|^src/Cli/Microsoft\.TemplateEngine|^test/TemplateEngine/|^test/dotnet-new\.|^test/Microsoft\.TemplateEngine|^test/Microsoft\.DotNet\.TemplateLocator|^template_feed/"; then
    RunTemplateTests=false
    echo "  Skipping: Template Engine tests (no changes in src/TemplateEngine/)"
  fi

  # Containers: only when container source or tests change
  if ! echo "$CHANGED_FILES" | grep -qE "^src/Containers/|^test/Microsoft\.NET\.Build\.Containers|^test/containerize\."; then
    RunContainerTests=false
    echo "  Skipping: Container tests (no changes in src/Containers/)"
  fi
fi

# Emit AzDO pipeline variables
echo "##vso[task.setvariable variable=RunAnalyzerTests]$RunAnalyzerTests"
echo "##vso[task.setvariable variable=RunBlazorTests]$RunBlazorTests"
echo "##vso[task.setvariable variable=RunSwaTests]$RunSwaTests"
echo "##vso[task.setvariable variable=RunWatchTests]$RunWatchTests"
echo "##vso[task.setvariable variable=RunTemplateTests]$RunTemplateTests"
echo "##vso[task.setvariable variable=RunContainerTests]$RunContainerTests"

echo ""
echo "##[section]Test execution plan:"
echo "  RunAnalyzerTests:   $RunAnalyzerTests"
echo "  RunBlazorTests:     $RunBlazorTests"
echo "  RunSwaTests:        $RunSwaTests"
echo "  RunWatchTests:      $RunWatchTests"
echo "  RunTemplateTests:   $RunTemplateTests"
echo "  RunContainerTests:  $RunContainerTests"
