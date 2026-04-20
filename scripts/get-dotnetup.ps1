#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downloads the latest dotnetup binary from the dotnetup CI pipeline and installs it locally.

.DESCRIPTION
    Uses the Azure CLI (az) to query the dotnetup CI pipeline in dnceng/internal,
    download the platform-appropriate artifact, and install it to a local directory.

    You must have the Azure CLI installed with the azure-devops extension, and be
    logged in with 'az login' with access to the dnceng/internal project.

.PARAMETER InstallDir
    Directory to install dotnetup into. Defaults to ~/.dotnetup.

.PARAMETER Branch
    The branch to get the latest build from. Defaults to 'release/dnup'.

.PARAMETER RuntimeId
    Override automatic OS/architecture detection with an explicit RID
    (e.g. win-x64, linux-arm64, osx-arm64, linux-musl-x64).

.EXAMPLE
    # Ensure you're logged in, then run:
    az login
    ./get-dotnetup.ps1

.EXAMPLE
    # Override RID and install directory:
    ./get-dotnetup.ps1 -RuntimeId linux-musl-x64 -InstallDir /opt/dotnetup
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $HOME ".dotnetup"),
    [string]$Branch = "release/dnup",
    [string]$RuntimeId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


$AzdoOrg = "https://dev.azure.com/dnceng"
$AzdoProject = "internal"
$PipelineId = 1544

function Get-RuntimeId {
    if ($RuntimeId) {
        return $RuntimeId
    }

    # Detect OS
    if ($IsWindows -or ($env:OS -eq "Windows_NT")) {
        $os = "win"
    }
    elseif ($IsMacOS) {
        $os = "osx"
    }
    elseif ($IsLinux) {
        $os = "linux"

        # Detect musl vs glibc
        $isMusl = $false
        try {
            $lddOutput = & ldd --version 2>&1 | Out-String
            if ($lddOutput -match "musl") {
                $isMusl = $true
            }
        }
        catch { }

        if (-not $isMusl) {
            try {
                $null = & getconf GNU_LIBC_VERSION 2>&1
                # If getconf succeeds, it's glibc
            }
            catch {
                # getconf failed — likely musl
                $isMusl = $true
            }
        }

        if ($isMusl) {
            $os = "linux-musl"
        }
    }
    else {
        throw "Unsupported operating system. Use -RuntimeId to specify a RID manually."
    }

    # Detect architecture
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $archStr = switch ($arch) {
        "X64" { "x64" }
        "Arm64" { "arm64" }
        default { throw "Unsupported architecture: $arch. Use -RuntimeId to specify a RID manually." }
    }

    return "$os-$archStr"
}

# --- Main ---

# Windows PowerShell (5.x) is not supported; require PowerShell 6+ (pwsh).
# The 'PSEdition' property is only present on 5+, so usage on 4 and below will be null,
# so we check that the version isn't Core instead of "version is Desktop"
if ($PSVersionTable.PSEdition -ne "Core") {
    throw @"
This script requires PowerShell 7 (pwsh) and cannot run on Windows PowerShell 5.x.
Install PowerShell 7 from: https://aka.ms/install-powershell
Then re-run this script with 'pwsh' instead of 'powershell'.
"@
}

# Check for Azure CLI
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw @"
Azure CLI (az) is required but was not found on PATH.
Install it from: https://aka.ms/install-az-cli

After installing, log in with:
    az login
"@
}

$rid = Get-RuntimeId
Write-Host "Detected runtime: $rid" -ForegroundColor Cyan

# Get latest successful build for the specified branch
Write-Host "Querying for latest successful build on $Branch..." -ForegroundColor Cyan

$runsJson = az pipelines runs list `
    --pipeline-ids $PipelineId `
    --branch $Branch `
    --status completed `
    --query-order FinishTimeDesc `
    --top 10 `
    --org $AzdoOrg `
    --project $AzdoProject `
    --query "[?result=='succeeded' || result=='partiallySucceeded'] | [0].id" `
    --output tsv

if ($LASTEXITCODE -ne 0) {
    throw @"
Failed to query pipeline runs. Ensure you are logged in and have access to dnceng/internal:

    az login
    az devops configure --defaults organization=$AzdoOrg project=$AzdoProject

Error: $runsJson
"@
}

$runId = ($runsJson | Out-String).Trim()
if (-not $runId) {
    throw "No successful builds found for pipeline $PipelineId on branch $Branch."
}

$buildUrl = "$AzdoOrg/$AzdoProject/_build/results?buildId=$runId"
Write-Host "Found build $runId" -ForegroundColor Green
Write-Host "  $buildUrl" -ForegroundColor DarkGray

# Download the artifact
$artifactName = "dotnetup-standalone-$rid"
Write-Host "Downloading artifact '$artifactName'..." -ForegroundColor Cyan

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "dotnetup-install-$([System.IO.Path]::GetRandomFileName())"

try {
    az pipelines runs artifact download `
        --artifact-name $artifactName `
        --path $tempDir `
        --run-id $runId `
        --org $AzdoOrg `
        --project $AzdoProject 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw @"
Failed to download artifact '$artifactName' for run $runId.
Available RIDs: win-x64, win-arm64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64, osx-x64, osx-arm64
Use -RuntimeId to specify the correct RID.
"@
    }

    # Find the binary in the downloaded contents
    $binaryName = if ($rid -like "win-*") { "dotnetup.exe" } else { "dotnetup" }
    $foundBinary = Get-ChildItem -Path $tempDir -Recurse -Filter $binaryName | Select-Object -First 1
    if (-not $foundBinary) {
        throw "Could not find '$binaryName' in the downloaded artifact. Contents: $(Get-ChildItem -Path $tempDir -Recurse | Select-Object -ExpandProperty FullName)"
    }

    # Install just the binary
    Write-Host "Installing to $InstallDir..." -ForegroundColor Cyan
    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }

    Copy-Item -Path $foundBinary.FullName -Destination $InstallDir -Force

    # Verify the binary is present
    $installedBinary = Join-Path $InstallDir $binaryName
    if (-not (Test-Path $installedBinary)) {
        throw "Installation failed: '$installedBinary' not found after copy."
    }

    # Set executable bit on non-Windows
    if ($rid -notlike "win-*") {
        chmod +x $installedBinary
    }

    Write-Host ""
    Write-Host "dotnetup installed successfully to $InstallDir" -ForegroundColor Green
    Write-Host ""

    # Check if install dir is on PATH
    $pathDirs = $env:PATH -split [System.IO.Path]::PathSeparator
    $onPath = $pathDirs | Where-Object { $_ -eq $InstallDir -or $_ -eq "$InstallDir/" -or $_ -eq "$InstallDir\" }

    if (-not $onPath) {
        Write-Host "To add dotnetup to your PATH, run:" -ForegroundColor Yellow
        Write-Host ""
        if ($rid -like "win-*") {
            Write-Host "  # Current session:" -ForegroundColor DarkGray
            Write-Host "  `$env:PATH = `"$InstallDir;`$env:PATH`""
            Write-Host ""
            Write-Host "  # Permanently (current user):" -ForegroundColor DarkGray
            Write-Host "  [Environment]::SetEnvironmentVariable('PATH', `"$InstallDir;`$([Environment]::GetEnvironmentVariable('PATH', 'User'))`", 'User')"
        }
        else {
            Write-Host "  # Current session:" -ForegroundColor DarkGray
            Write-Host "  export PATH=`"$InstallDir`:`$PATH`""
            Write-Host ""
            Write-Host "  # Permanently (add to your shell profile):" -ForegroundColor DarkGray
            Write-Host "  echo 'export PATH=`"$InstallDir`:`$PATH`"' >> ~/.bashrc"
        }
        Write-Host ""
    }
    else {
        Write-Host "dotnetup is already on your PATH. Run 'dotnetup --help' to get started." -ForegroundColor Green
    }
}
finally {
    # Cleanup temp files
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
}
