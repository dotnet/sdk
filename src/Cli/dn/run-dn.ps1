#!/usr/bin/env pwsh
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

<#
.SYNOPSIS
    Build the dn AOT harness layout and run a dotnet command through the Native AOT
    CLI (dotnet-aot) - and optionally the managed fallback - for comparison.

.DESCRIPTION
    Publishes dotnet-aot (NativeAOT) and the dn native host, builds the managed dotnet
    CLI, and assembles them into the dn publish directory (dn + dotnet-aot.dll + the
    managed dotnet.dll + deps). Then runs `dn <Command>` with DOTNET_CLI_ENABLEAOT
    toggled:
      * Aot      DOTNET_CLI_ENABLEAOT=true: the command runs in-process in dotnet-aot.dll.
      * Managed  dn hosts the copied dotnet.dll (same source, JIT-compiled).
      * Compare  runs both and diffs the output (an empty diff means parity).

    DOTNET_ROOT is pointed at the repo-local .dotnet because the publish directory is
    not a full SDK layout.

.PARAMETER Command
    The argument string passed to dn (split on spaces). Example: "--info".

.PARAMETER Mode
    Aot (default), Managed, or Compare.

.PARAMETER Configuration
    Debug (default) or Release.

.PARAMETER Rid
    Runtime identifier. Auto-detected from the host when omitted.

.PARAMETER Layout
    Flat (default) co-locates dotnet-aot with dn. Separated places dotnet-aot (and the managed
    payload) in a sdk\<version>\ subfolder while dn stays in the parent, emulating the deployed muxer
    layout so AppContext.BaseDirectory is no longer the SDK directory.

.PARAMETER SelfLocate
    With -Layout Separated, make dn pass an empty sdk_dir so dotnet-aot resolves the SDK directory by
    self-locating its own module (the fallback path).

.PARAMETER NoBuild
    Skip the publish/build/copy steps and run the already-assembled layout.

.EXAMPLE
    ./run-dn.ps1 -Command "--info"

.EXAMPLE
    ./run-dn.ps1 -Command "--info" -Mode Compare

.EXAMPLE
    ./run-dn.ps1 -Command "workload --info" -NoBuild
#>

[CmdletBinding()]
param(
    [string]$Command = "--info",
    [ValidateSet("Aot", "Managed", "Compare")]
    [string]$Mode = "Aot",
    [ValidateSet("Flat", "Separated")]
    [string]$Layout = "Flat",
    [switch]$SelfLocate,
    [string]$Configuration = "Debug",
    [string]$Rid,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")).Path
$isWin = $IsWindows -or ($env:OS -eq "Windows_NT")
$exeSuffix = if ($isWin) { ".exe" } else { "" }
$dotnet = Join-Path $repoRoot ".dotnet" "dotnet$exeSuffix"
$dnExeName = "dn$exeSuffix"
$aotLibName = if ($isWin) { "dotnet-aot.dll" } elseif ($IsMacOS) { "dotnet-aot.dylib" } else { "dotnet-aot.so" }

if (-not $Rid) {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $os = if ($isWin) { "win" } elseif ($IsMacOS) { "osx" } else { "linux" }
    $Rid = "$os-$arch"
}

Write-Host "Repo root:     $repoRoot"
Write-Host "Configuration: $Configuration"
Write-Host "RID:           $Rid"
Write-Host "Command:       dn $Command"
Write-Host "Mode:          $Mode"
Write-Host ""

function Resolve-PublishPath([string]$relativeGlob) {
    # Globs the TFM folder so paths are not pinned to a specific net version.
    return (Resolve-Path (Join-Path $repoRoot $relativeGlob) -ErrorAction SilentlyContinue |
        Select-Object -First 1).Path
}

if (-not $NoBuild) {
    Write-Host "Publishing dotnet-aot (NativeAOT)..." -ForegroundColor Cyan
    & $dotnet publish (Join-Path $repoRoot "src/Cli/dotnet-aot/dotnet-aot.csproj") -r $Rid -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet-aot publish failed." }

    Write-Host "Publishing dn host..." -ForegroundColor Cyan
    & $dotnet publish (Join-Path $repoRoot "src/Cli/dn/dn.csproj") -r $Rid -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dn publish failed." }

    Write-Host "Building managed dotnet CLI (fallback)..." -ForegroundColor Cyan
    & $dotnet build (Join-Path $repoRoot "src/Cli/dotnet/dotnet.csproj") -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "managed dotnet build failed." }

    $dnPublishDir = Resolve-PublishPath "artifacts/bin/dn/$Configuration/*/$Rid/publish"
    $aotDll = Resolve-PublishPath "artifacts/bin/dotnet-aot/$Configuration/*/$Rid/publish/$aotLibName"
    $managedDir = (Get-ChildItem -Directory (Join-Path $repoRoot "artifacts/bin/dotnet/$Configuration") |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

    if (-not $dnPublishDir) { throw "Could not locate the dn publish directory after build." }
    if (-not $aotDll) { throw "Could not locate $aotLibName after publish." }

    $sdkTargetDir = $dnPublishDir
    if ($Layout -eq "Separated") {
        $sdkTargetDir = Join-Path $dnPublishDir "sdk/11.0.100"
        New-Item -ItemType Directory -Force -Path $sdkTargetDir | Out-Null
        # dotnet-aot must live only in the versioned SDK subfolder, not next to dn.
        Remove-Item (Join-Path $dnPublishDir $aotLibName) -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Assembling layout into $sdkTargetDir ..." -ForegroundColor Cyan
    Copy-Item $aotDll $sdkTargetDir -Force
    Copy-Item (Join-Path $managedDir "*") $sdkTargetDir -Recurse -Force
}

$dnPublishDir = Resolve-PublishPath "artifacts/bin/dn/$Configuration/*/$Rid/publish"
if (-not $dnPublishDir) { throw "dn publish directory not found. Run without -NoBuild first." }

$dnExe = Join-Path $dnPublishDir $dnExeName
if (-not (Test-Path $dnExe)) { throw "dn host not found at '$dnExe'. Run without -NoBuild first." }

$argList = $Command.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
$env:DOTNET_ROOT = (Join-Path $repoRoot ".dotnet")
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

# Emulate the deployed non-flat layout: tell dn to load dotnet-aot from (and pass as sdk_dir) the
# versioned SDK subfolder, optionally forcing the self-locate fallback by blanking sdk_dir.
if ($Layout -eq "Separated") {
    $env:DOTNET_AOT_SDK_DIR = (Join-Path $dnPublishDir "sdk/11.0.100")
    Write-Host "Layout:        Separated (dotnet-aot in $($env:DOTNET_AOT_SDK_DIR))" -ForegroundColor Cyan
}
else {
    Remove-Item Env:\DOTNET_AOT_SDK_DIR -ErrorAction SilentlyContinue
}
if ($SelfLocate) {
    $env:DOTNET_AOT_BLANK_SDKDIR = "1"
    Write-Host "Self-locate:   enabled (dn passes empty sdk_dir; dotnet-aot self-locates)" -ForegroundColor Cyan
}
else {
    Remove-Item Env:\DOTNET_AOT_BLANK_SDKDIR -ErrorAction SilentlyContinue
}

function Invoke-Dn([bool]$enableAot) {
    if ($enableAot) {
        $env:DOTNET_CLI_ENABLEAOT = "true"
    }
    else {
        Remove-Item Env:\DOTNET_CLI_ENABLEAOT -ErrorAction SilentlyContinue
    }
    & $dnExe @argList 2>&1
}

switch ($Mode) {
    "Aot" {
        Write-Host "===== AOT (DOTNET_CLI_ENABLEAOT=true) =====" -ForegroundColor Green
        Invoke-Dn $true
    }
    "Managed" {
        Write-Host "===== Managed (DOTNET_CLI_ENABLEAOT unset) =====" -ForegroundColor Green
        Invoke-Dn $false
    }
    "Compare" {
        $logDir = Join-Path $repoRoot "artifacts/log"
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null

        $aotOut = Invoke-Dn $true
        $managedOut = Invoke-Dn $false
        $aotOut | Set-Content (Join-Path $logDir "dn-aot.txt")
        $managedOut | Set-Content (Join-Path $logDir "dn-managed.txt")

        Write-Host "===== AOT (DOTNET_CLI_ENABLEAOT=true) =====" -ForegroundColor Green
        $aotOut | Write-Output
        Write-Host "===== Managed (fallback) =====" -ForegroundColor Green
        $managedOut | Write-Output

        $diff = Compare-Object $aotOut $managedOut
        Write-Host ""
        if ($diff) {
            Write-Host "DIFFERENCES (AOT vs managed):" -ForegroundColor Yellow
            $diff | Format-Table -AutoSize
        }
        else {
            Write-Host "IDENTICAL: AOT and managed output match line-for-line." -ForegroundColor Green
        }
    }
}
