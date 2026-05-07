#!/usr/bin/env pwsh
# Benchmark script for PR #54153 - Telemetry perf adjustments
# Measures dotnet restore performance before and after the PR changes.
# Uses the built SDK on the SDK repo's own dotnet.csproj (the CLI project).

param(
    [int]$Runs = 10,
    [int]$WarmupRuns = 2,
    [string]$RepoRoot = "C:\repos\sdk",
    [string]$Configuration = "Release",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Pin environment: telemetry must be ON to measure its cost
$env:DOTNET_CLI_TELEMETRY_OPTOUT = ""
$env:DOTNET_CLI_TELEMETRY_LOG_PATH = ""
$env:DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT = ""
$env:DOTNET_CLI_TELEMETRY_STORAGE_PATH = ""
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_MULTILEVEL_LOOKUP = "0"

$baselineSdkPath = "$RepoRoot\artifacts\bin\redist-baseline"
$prSdkPath = "$RepoRoot\artifacts\bin\redist-pr"
$redistPath = "$RepoRoot\artifacts\bin\redist\$Configuration\dotnet"
$resultsFile = "$RepoRoot\benchmark-results.csv"
$summaryFile = "$RepoRoot\benchmark-summary.txt"
$globalJsonPath = "$RepoRoot\global.json"
$restoreTarget = "$RepoRoot\src\Cli\dotnet\dotnet.csproj"

function Set-SdkPath {
    param([string]$SdkRelativePath)
    $gj = Get-Content $globalJsonPath -Raw | ConvertFrom-Json
    $gj.sdk.paths = @($SdkRelativePath)
    $gj | ConvertTo-Json -Depth 10 | Set-Content $globalJsonPath
}

function Restore-GlobalJson {
    Set-Location $RepoRoot
    git checkout -- global.json 2>&1 | Out-Null
}

function Run-Restore {
    param(
        [string]$DotnetExe,
        [string]$SdkRelativePath,
        [string]$Label,
        [int]$Runs,
        [int]$WarmupRuns
    )

    Write-Host "`n===== Running $Label =====" -ForegroundColor Cyan
    Write-Host "Using SDK: $DotnetExe"
    Write-Host "Restore target: $restoreTarget"
    Write-Host "Warmup runs: $WarmupRuns, Measured runs: $Runs"

    # Point global.json at this SDK
    Set-SdkPath $SdkRelativePath

    # Verify SDK version
    $ver = & $DotnetExe --version 2>&1
    Write-Host "SDK version: $ver"

    # Warmup
    for ($i = 1; $i -le $WarmupRuns; $i++) {
        Write-Host "  Warmup $i/$WarmupRuns..." -ForegroundColor DarkGray
        & $DotnetExe restore $restoreTarget --verbosity quiet 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Warmup restore failed for $Label (exit code $LASTEXITCODE)" }
    }

    $times = @()
    for ($i = 1; $i -le $Runs; $i++) {
        Write-Host "  Run $i/$Runs..." -NoNewline
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        & $DotnetExe restore $restoreTarget --verbosity quiet 2>&1 | Out-Null
        $sw.Stop()
        if ($LASTEXITCODE -ne 0) { throw "Restore failed for $Label run $i (exit code $LASTEXITCODE)" }
        $elapsed = $sw.Elapsed.TotalSeconds
        $times += $elapsed
        Write-Host " $([math]::Round($elapsed, 2))s"

        # Write to CSV
        "$Label,$i,$elapsed" | Out-File -Append -FilePath $resultsFile
    }

    return $times
}

function Get-Stats {
    param([double[]]$Times)
    $sorted = $Times | Sort-Object
    $count = $sorted.Count
    $median = if ($count % 2 -eq 0) {
        ($sorted[$count/2 - 1] + $sorted[$count/2]) / 2
    } else {
        $sorted[([math]::Floor($count/2))]
    }
    return @{
        Mean   = [math]::Round(($Times | Measure-Object -Average).Average, 3)
        Median = [math]::Round($median, 3)
        Min    = [math]::Round(($Times | Measure-Object -Minimum).Minimum, 3)
        Max    = [math]::Round(($Times | Measure-Object -Maximum).Maximum, 3)
        StdDev = [math]::Round([math]::Sqrt(($Times | ForEach-Object { ($_ - ($Times | Measure-Object -Average).Average) * ($_ - ($Times | Measure-Object -Average).Average) } | Measure-Object -Average).Average), 3)
    }
}

if (-not $SkipBuild) {
    # ============================================================
    # Step 1: Build baseline (main branch)
    # ============================================================
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "Step 1: Building BASELINE SDK (main)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green

    Set-Location $RepoRoot
    git checkout main 2>&1
    git pull origin main 2>&1

    Write-Host "Building SDK ($Configuration)..."
    & "$RepoRoot\build.cmd" -configuration $Configuration -restore -build -pack
    if ($LASTEXITCODE -ne 0) { throw "Baseline build failed" }

    # Copy baseline SDK to side-by-side location
    Write-Host "Copying baseline SDK to $baselineSdkPath..."
    if (Test-Path $baselineSdkPath) { Remove-Item -Recurse -Force $baselineSdkPath }
    Copy-Item -Recurse -Force $redistPath $baselineSdkPath

    # ============================================================
    # Step 2: Build PR branch
    # ============================================================
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "Step 2: Building PR SDK (#54153)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green

    # Fetch and checkout PR
    git fetch origin pull/54153/head:pr-54153 2>&1
    git checkout pr-54153 2>&1

    Write-Host "Building SDK ($Configuration)..."
    & "$RepoRoot\build.cmd" -configuration $Configuration -restore -build -pack
    if ($LASTEXITCODE -ne 0) { throw "PR build failed" }

    # Copy PR SDK to side-by-side location
    Write-Host "Copying PR SDK to $prSdkPath..."
    if (Test-Path $prSdkPath) { Remove-Item -Recurse -Force $prSdkPath }
    Copy-Item -Recurse -Force $redistPath $prSdkPath
} else {
    Write-Host "Skipping build (using existing SDK copies)" -ForegroundColor Yellow
    if (-not (Test-Path "$baselineSdkPath\dotnet.exe")) { throw "Baseline SDK not found at $baselineSdkPath" }
    if (-not (Test-Path "$prSdkPath\dotnet.exe")) { throw "PR SDK not found at $prSdkPath" }
}

# ============================================================
# Step 3: Run benchmarks
# ============================================================
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Step 3: Running benchmarks" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Go back to main for a consistent restore target
Set-Location $RepoRoot
git checkout main 2>&1

# Initialize results CSV
"Label,Run,ElapsedSeconds" | Out-File -FilePath $resultsFile

$baselineDotnet = "$baselineSdkPath\dotnet.exe"
$prDotnet = "$prSdkPath\dotnet.exe"

# Run baseline first
$baselineTimes = Run-Restore -DotnetExe $baselineDotnet -SdkRelativePath "artifacts/bin/redist-baseline" -Label "Baseline" -Runs $Runs -WarmupRuns $WarmupRuns

# Run PR
$prTimes = Run-Restore -DotnetExe $prDotnet -SdkRelativePath "artifacts/bin/redist-pr" -Label "PR-54153" -Runs $Runs -WarmupRuns $WarmupRuns

# ============================================================
# Step 4: Compare results
# ============================================================
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Results Summary" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

$baseStats = Get-Stats -Times $baselineTimes
$prStats = Get-Stats -Times $prTimes

$diffMean = [math]::Round($prStats.Mean - $baseStats.Mean, 3)
$diffMedian = [math]::Round($prStats.Median - $baseStats.Median, 3)
$pctMean = if ($baseStats.Mean -ne 0) { [math]::Round(($diffMean / $baseStats.Mean) * 100, 2) } else { 0 }
$pctMedian = if ($baseStats.Median -ne 0) { [math]::Round(($diffMedian / $baseStats.Median) * 100, 2) } else { 0 }

$summary = @"
Benchmark: dotnet restore sdk.sln ($Configuration build)
Runs: $Runs (+ $WarmupRuns warmup)
Telemetry: ON
Environment: Windows, $(hostname)

                  Baseline (main)    PR #54153        Diff         %Change
  Mean (s):       $($baseStats.Mean.ToString().PadRight(19))$($prStats.Mean.ToString().PadRight(17))$($diffMean.ToString().PadRight(13))${pctMean}%
  Median (s):     $($baseStats.Median.ToString().PadRight(19))$($prStats.Median.ToString().PadRight(17))$($diffMedian.ToString().PadRight(13))${pctMedian}%
  Min (s):        $($baseStats.Min.ToString().PadRight(19))$($prStats.Min)
  Max (s):        $($baseStats.Max.ToString().PadRight(19))$($prStats.Max)
  StdDev (s):     $($baseStats.StdDev.ToString().PadRight(19))$($prStats.StdDev)

Raw Baseline times: $($baselineTimes | ForEach-Object { [math]::Round($_, 2) })
Raw PR times:       $($prTimes | ForEach-Object { [math]::Round($_, 2) })

CSV data saved to: $resultsFile
"@

Write-Host $summary
$summary | Out-File -FilePath $summaryFile
Write-Host "`nSummary saved to: $summaryFile" -ForegroundColor Cyan

# Restore global.json to original state
Restore-GlobalJson

# Return to original branch
git checkout - 2>&1
