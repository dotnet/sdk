#!/usr/bin/env pwsh
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

<#
.SYNOPSIS
    Publishes and runs the dotnet-aot tests as a NativeAOT binary.

.DESCRIPTION
    This script publishes the dotnet-aot.Tests project as a NativeAOT executable,
    publishes the dotnet-aot library and dn host, and runs the resulting native
    test binary. This verifies both the Native AOT test closure and end-to-end dn
    integration against a complete SDK installation.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Debug.

.PARAMETER RuntimeIdentifier
    The RID to publish for. Auto-detected if not specified.

.PARAMETER NoBuild
    Skip the publish step and run a previously published binary.

.PARAMETER Trx
    Emit a TRX test report (for CI result publishing). The report is written to
    ResultsDirectory (defaults to <repo>/artifacts/TestResults/<Configuration>).

.PARAMETER ResultsDirectory
    Directory for the TRX report when -Trx is specified.

.EXAMPLE
    ./run-aot-tests.ps1
    ./run-aot-tests.ps1 -Configuration Release
    ./run-aot-tests.ps1 -RuntimeIdentifier linux-x64
#>

param(
    [string]$Configuration = "Debug",
    [string]$RuntimeIdentifier,
    [switch]$NoBuild,
    [switch]$Trx,
    [string]$ResultsDirectory
)

$ErrorActionPreference = "Stop"

# Resolve paths
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..") | Select-Object -ExpandProperty Path
$dotnetName = if ($IsWindows -or $env:OS -eq "Windows_NT") { "dotnet.exe" } else { "dotnet" }
$dotnet = [System.IO.Path]::Combine($repoRoot, ".dotnet", $dotnetName)
$testProject = Join-Path $PSScriptRoot "dotnet-aot.Tests.csproj"
$productProject = [System.IO.Path]::Combine($repoRoot, "src", "Cli", "dotnet-aot", "dotnet-aot.csproj")
$dnProject = [System.IO.Path]::Combine($repoRoot, "src", "Cli", "dn", "dn.csproj")

# Auto-detect RID
if (-not $RuntimeIdentifier) {
    $RuntimeIdentifier = & $dotnet --info 2>$null | Select-String "RID:" | ForEach-Object {
        $_.Line -replace '.*RID:\s*', '' -replace '\s*$', ''
    } | Select-Object -First 1

    if (-not $RuntimeIdentifier) {
        # Fallback: construct from OS and architecture
        $os = if ($IsWindows -or $env:OS -eq "Windows_NT") { "win" }
              elseif ($IsMacOS) { "osx" }
              else { "linux" }
        $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLower()
        $RuntimeIdentifier = "$os-$arch"
    }
}

$publishDir = [System.IO.Path]::Combine($PSScriptRoot, "artifacts", "aot-tests", $Configuration, $RuntimeIdentifier)
$aotPublishDir = [System.IO.Path]::Combine($PSScriptRoot, "artifacts", "dotnet-aot", $Configuration, $RuntimeIdentifier)
$dnPublishDir = [System.IO.Path]::Combine($PSScriptRoot, "artifacts", "dn", $Configuration, $RuntimeIdentifier)
$exeName = if ($RuntimeIdentifier.StartsWith("win")) { "dotnet-aot.Tests.exe" } else { "dotnet-aot.Tests" }
$aotLibraryName = if ($RuntimeIdentifier.StartsWith("win")) { "dotnet-aot.dll" }
                  elseif ($RuntimeIdentifier.StartsWith("osx")) { "libdotnet-aot.dylib" }
                  else { "libdotnet-aot.so" }
$dnName = if ($RuntimeIdentifier.StartsWith("win")) { "dn.exe" } else { "dn" }
$exePath = Join-Path $publishDir $exeName
$aotLibraryPath = Join-Path $aotPublishDir $aotLibraryName
$dnPath = Join-Path $dnPublishDir $dnName

Write-Host "=== dotnet-aot NativeAOT Test Runner ===" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration"
Write-Host "  RID:           $RuntimeIdentifier"
Write-Host "  Publish dir:   $publishDir"
Write-Host ""

# Publish
if (-not $NoBuild) {
    Write-Host "Publishing as NativeAOT..." -ForegroundColor Yellow

    & $dotnet publish $testProject `
        -c $Configuration `
        -r $RuntimeIdentifier `
        -p:PublishAotTests=true `
        -p:PublishDir=$publishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: AOT publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    & $dotnet publish $productProject `
        -c $Configuration `
        -r $RuntimeIdentifier `
        -p:PublishDir=$aotPublishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: dotnet-aot publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    & $dotnet publish $dnProject `
        -c $Configuration `
        -r $RuntimeIdentifier `
        -p:PublishDir=$dnPublishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: dn publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "Published: $exePath" -ForegroundColor Green
    $size = (Get-Item $exePath -ErrorAction SilentlyContinue).Length
    if ($size) {
        Write-Host "Size:      $([math]::Round($size / 1MB, 1)) MB"
    }
    Write-Host ""
}

$managedTestModule = Get-ChildItem ([System.IO.Path]::Combine($repoRoot, "artifacts", "bin", "dotnet-aot.Tests", $Configuration)) `
    -Recurse -Filter "dotnet-aot.Tests.dll" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*$RuntimeIdentifier*" } |
    Select-Object -First 1 -ExpandProperty FullName

# Run
if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: Published binary not found at $exePath" -ForegroundColor Red
    Write-Host "Run without -NoBuild to publish first." -ForegroundColor Yellow
    exit 1
}
if (-not (Test-Path $aotLibraryPath)) {
    Write-Host "ERROR: Published native library not found at $aotLibraryPath" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $dnPath)) {
    Write-Host "ERROR: Published dn host not found at $dnPath" -ForegroundColor Red
    exit 1
}
if (-not $managedTestModule) {
    Write-Host "ERROR: Managed test module not found for Native AOT integration validation." -ForegroundColor Red
    exit 1
}

Write-Host "Running AOT tests..." -ForegroundColor Yellow
Write-Host ""

$sdkDirectory = & $dotnet --info 2>$null | ForEach-Object {
    if ($_ -match '^\s*Base Path:\s*(.+?)\s*$') {
        $Matches[1]
    }
} | Select-Object -First 1

if (-not $sdkDirectory) {
    Write-Host "ERROR: Could not determine the bootstrap SDK directory." -ForegroundColor Red
    exit 1
}

$environment = @{
    DOTNET_AOT_LIBRARY_DIR = $aotPublishDir
    DOTNET_AOT_SDK_DIR = $sdkDirectory
    DOTNET_AOT_TEST_DN_PATH = $dnPath
    DOTNET_AOT_TEST_MANAGED_TEST_MODULE = $managedTestModule
    DOTNET_AOT_TEST_SDK_DIRECTORY = $sdkDirectory
    DOTNET_HOST_PATH = $dotnet
    DOTNET_ROOT = [System.IO.Path]::Combine($repoRoot, ".dotnet")
}
$previousEnvironment = @{}
foreach ($entry in $environment.GetEnumerator()) {
    $previousEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key)
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
}

# When -Trx is set, emit a TRX report (the AOT test binary is a Microsoft.Testing.Platform
# app, so it accepts the --report-trx options) so CI can publish the results.
$runArgs = @()
if ($Trx) {
    if (-not $ResultsDirectory) {
        $ResultsDirectory = [System.IO.Path]::Combine($repoRoot, "artifacts", "TestResults", $Configuration)
    }
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    $runArgs += @("--report-trx", "--report-trx-filename", "dotnet-aot.Tests.trx", "--results-directory", $ResultsDirectory)
}

try {
    & $exePath @runArgs
    $testExitCode = $LASTEXITCODE
}
finally {
    foreach ($entry in $previousEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    }
}

Write-Host ""
if ($testExitCode -eq 0) {
    Write-Host "All AOT tests passed." -ForegroundColor Green
} else {
    Write-Host "AOT tests failed with exit code $testExitCode." -ForegroundColor Red
}

exit $testExitCode
