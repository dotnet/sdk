#!/usr/bin/env pwsh
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

<#
.SYNOPSIS
    Publishes and runs the dotnet-aot tests as a NativeAOT binary.

.DESCRIPTION
    This script publishes the dotnet-aot.Tests project as a NativeAOT executable
    and runs the resulting native binary. This verifies that the AOT CLI code
    (Parser, DotnetRootResolver, NativeEntryPoint) works correctly when compiled
    ahead-of-time.

    The test project uses xUnit v3 AOT packages (xunit.v3.core.aot) which use
    source generators for test discovery, replacing runtime reflection.

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
$dotnet = [System.IO.Path]::Combine($repoRoot, ".dotnet", "dotnet")
$testProject = Join-Path $PSScriptRoot "dotnet-aot.Tests.csproj"

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
$exeName = if ($RuntimeIdentifier.StartsWith("win")) { "dotnet-aot.Tests.exe" } else { "dotnet-aot.Tests" }
$exePath = Join-Path $publishDir $exeName

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

    Write-Host ""
    Write-Host "Published: $exePath" -ForegroundColor Green
    $size = (Get-Item $exePath -ErrorAction SilentlyContinue).Length
    if ($size) {
        Write-Host "Size:      $([math]::Round($size / 1MB, 1)) MB"
    }
    Write-Host ""
}

# Run
if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: Published binary not found at $exePath" -ForegroundColor Red
    Write-Host "Run without -NoBuild to publish first." -ForegroundColor Yellow
    exit 1
}

Write-Host "Running AOT tests..." -ForegroundColor Yellow
Write-Host ""

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

& $exePath @runArgs
$testExitCode = $LASTEXITCODE

Write-Host ""
if ($testExitCode -eq 0) {
    Write-Host "All AOT tests passed." -ForegroundColor Green
} else {
    Write-Host "AOT tests failed with exit code $testExitCode." -ForegroundColor Red
}

exit $testExitCode
