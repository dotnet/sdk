<# 
.SYNOPSIS
    Determines which test groups should run based on files changed in a PR.
    Outputs AzDO pipeline variables consumed by UnitTests.proj.

.DESCRIPTION
    Design principles:
      - Default to running ALL tests (fail-open, never silently skip)
      - CI builds always run all tests
      - Codeflow PRs (darc-*) always run all tests
      - Dependency version changes always run all tests
      - Only narrow scope for normal human PRs with isolated changes
#>

$ErrorActionPreference = 'Stop'

# All flags default to true (run everything)
$RunAnalyzerTests = 'true'
$RunBlazorTests = 'true'
$RunSwaTests = 'true'
$RunWatchTests = 'true'
$RunTemplateTests = 'true'
$RunContainerTests = 'true'

$ShouldFilter = $false

# Manual override: if ForceRunAllTests is set, run everything
if ($env:FORCE_RUN_ALL_TESTS -eq 'true' -or $env:FORCE_RUN_ALL_TESTS -eq 'True') {
    Write-Host "##[section]ForceRunAllTests parameter set - running all tests"
}
# Only attempt filtering for PR builds
elseif ($env:BUILD_REASON -eq 'PullRequest') {
    $sourceBranch = $env:SYSTEM_PULLREQUEST_SOURCEBRANCH
    if ($sourceBranch) {
        $sourceBranch = $sourceBranch -replace '^refs/heads/', ''
    }

    # Codeflow PRs (dotnet-maestro) use branch names starting with "darc-"
    if ($sourceBranch -like 'darc-*') {
        Write-Host "##[section]Codeflow PR detected (branch: $sourceBranch) - running all tests"
    }
    else {
        # Attempt to get the list of changed files
        $targetBranch = $env:SYSTEM_PULLREQUEST_TARGETBRANCH
        if (-not $targetBranch) { $targetBranch = 'main' }
        $targetBranch = $targetBranch -replace '^refs/heads/', ''

        # Fetch the target branch to have a merge base for diffing
        & git fetch origin $targetBranch --depth=1 2>$null

        $changedFiles = & git diff --name-only "origin/$targetBranch...HEAD" 2>$null
        if (-not $changedFiles) {
            Write-Host "##[warning]Could not determine changed files - running all tests"
        }
        else {
            $changedFilesList = $changedFiles -split "`n" | Where-Object { $_ }
            Write-Host "##[section]Changed files in PR:"
            $changedFilesList | Select-Object -First 50 | ForEach-Object { Write-Host "  $_" }
            if ($changedFilesList.Count -gt 50) {
                Write-Host "  ... ($($changedFilesList.Count) total files)"
            }

            $changedText = $changedFilesList -join "`n"

            # Check for broad-impact changes that require full test runs
            if ($changedText -match '(?m)^eng/Versions\.props$|^eng/Version\.Details\.(xml|props)$') {
                Write-Host "##[section]Dependency version files changed - running all tests"
            }
            elseif ($changedText -match '(?m)^eng/ManualVersions\.props$|^global\.json$') {
                Write-Host "##[section]Global build configuration changed - running all tests"
            }
            elseif ($changedText -match '(?m)^Directory\.Build\.(props|targets)$|^Directory\.Packages\.props$') {
                Write-Host "##[section]Root build files changed - running all tests"
            }
            else {
                # Safe to filter
                $ShouldFilter = $true
            }
        }
    }
}
else {
    Write-Host "##[section]Non-PR build (Reason: $env:BUILD_REASON) - running all tests"
}

if ($ShouldFilter) {
    Write-Host "##[section]Applying path-based test filtering"

    # NetAnalyzers
    if ($changedText -notmatch 'src/Microsoft\.CodeAnalysis\.NetAnalyzers/') {
        $RunAnalyzerTests = 'false'
        Write-Host "  Skipping: NetAnalyzer tests (no changes in src/Microsoft.CodeAnalysis.NetAnalyzers/)"
    }

    # Blazor WASM
    if ($changedText -notmatch 'src/BlazorWasmSdk/|src/WasmSdk/|test/Microsoft\.NET\.Sdk\.BlazorWebAssembly') {
        $RunBlazorTests = 'false'
        Write-Host "  Skipping: Blazor WASM tests (no changes in src/BlazorWasmSdk/, src/WasmSdk/)"
    }

    # Static Web Assets
    if ($changedText -notmatch 'src/StaticWebAssetsSdk/|test/Microsoft\.NET\.Sdk\.StaticWebAssets') {
        $RunSwaTests = 'false'
        Write-Host "  Skipping: Static Web Assets tests (no changes in src/StaticWebAssetsSdk/)"
    }

    # dotnet-watch
    if ($changedText -notmatch 'src/Dotnet\.Watch/|test/dotnet-watch\.|test/Microsoft\.AspNetCore\.Watch|test/Microsoft\.DotNet\.HotReload|test/Microsoft\.Extensions\.DotNetDeltaApplier') {
        $RunWatchTests = 'false'
        Write-Host "  Skipping: dotnet-watch tests (no changes in src/Dotnet.Watch/)"
    }

    # Template Engine
    if ($changedText -notmatch 'src/TemplateEngine/|src/Cli/Microsoft\.TemplateEngine|test/TemplateEngine/|test/dotnet-new\.|test/Microsoft\.TemplateEngine|test/Microsoft\.DotNet\.TemplateLocator|template_feed/') {
        $RunTemplateTests = 'false'
        Write-Host "  Skipping: Template Engine tests (no changes in src/TemplateEngine/)"
    }

    # Containers
    if ($changedText -notmatch 'src/Containers/|test/Microsoft\.NET\.Build\.Containers|test/containerize\.') {
        $RunContainerTests = 'false'
        Write-Host "  Skipping: Container tests (no changes in src/Containers/)"
    }
}

# Emit AzDO pipeline variables
Write-Host "##vso[task.setvariable variable=RunAnalyzerTests]$RunAnalyzerTests"
Write-Host "##vso[task.setvariable variable=RunBlazorTests]$RunBlazorTests"
Write-Host "##vso[task.setvariable variable=RunSwaTests]$RunSwaTests"
Write-Host "##vso[task.setvariable variable=RunWatchTests]$RunWatchTests"
Write-Host "##vso[task.setvariable variable=RunTemplateTests]$RunTemplateTests"
Write-Host "##vso[task.setvariable variable=RunContainerTests]$RunContainerTests"

Write-Host ""
Write-Host "##[section]Test execution plan:"
Write-Host "  RunAnalyzerTests:   $RunAnalyzerTests"
Write-Host "  RunBlazorTests:     $RunBlazorTests"
Write-Host "  RunSwaTests:        $RunSwaTests"
Write-Host "  RunWatchTests:      $RunWatchTests"
Write-Host "  RunTemplateTests:   $RunTemplateTests"
Write-Host "  RunContainerTests:  $RunContainerTests"
