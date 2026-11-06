<#
.SYNOPSIS
    Installs Node.js from https://nodejs.org/dist on a Helix agent.
.DESCRIPTION
    Based on dotnet/aspnetcore's eng/helix/content/InstallNode.ps1.
    Downloads and extracts Node.js if not already available on PATH.
.PARAMETER Version
    The version of Node.js to install (e.g., "20.7.0").
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$InstallDir = Join-Path $PSScriptRoot 'nodejs'

if (Get-Command "node.exe" -ErrorAction SilentlyContinue) {
    try {
        $v = & node --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Found node.exe in PATH: $v"
            exit
        } else {
            Write-Host "node.exe found on PATH but not functional, installing fresh copy"
        }
    } catch {
        Write-Host "node.exe found on PATH but not functional, installing fresh copy"
    }
}

if (Test-Path "$InstallDir\node.exe") {
    Write-Host "Node.exe already installed at $InstallDir"
    exit
}

function DownloadAndInstall {
    $nodeFile = "node-v$Version-win-x64"
    $url = "https://nodejs.org/dist/v$Version/$nodeFile.zip"
    $zipPath = Join-Path $env:TEMP "nodejs.zip"

    Write-Host "Downloading Node.js $Version from $url"
    $maxAttempts = 5
    $attempt = 0
    $success = $false

    while ($attempt -lt $maxAttempts -and -not $success) {
        $attempt++
        try {
            Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
            $success = $true
        } catch {
            Write-Host "Download attempt $attempt failed: $_"
            if ($attempt -lt $maxAttempts) {
                Start-Sleep -Seconds 5
            }
        }
    }

    if (-not $success) {
        Write-Error "Failed to download Node.js after $maxAttempts attempts"
        return $false
    }

    Write-Host "Extracting to $InstallDir"
    $tempDir = Join-Path $env:TEMP "nodejs-extract"
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }

    if (Get-Command -Name 'Microsoft.PowerShell.Archive\Expand-Archive' -ErrorAction Ignore) {
        Microsoft.PowerShell.Archive\Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force
    } else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tempDir)
    }

    if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }
    Move-Item (Join-Path $tempDir $nodeFile) $InstallDir

    Remove-Item $zipPath -ErrorAction SilentlyContinue
    Remove-Item $tempDir -Recurse -ErrorAction SilentlyContinue

    if (Test-Path "$InstallDir\node.exe") {
        Write-Host "Node.js $Version installed to $InstallDir"
        return $true
    } else {
        Write-Error "node.exe not found after extraction"
        return $false
    }
}

$result = DownloadAndInstall
if (-not $result) {
    exit 1
}
