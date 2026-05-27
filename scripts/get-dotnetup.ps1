#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downloads the latest dotnetup daily build and installs it locally.

.DESCRIPTION
    Downloads dotnetup from the public aka.ms shortlinks (e.g.
    https://aka.ms/dotnet/dotnetup/daily/dotnetup-win-x64.exe), verifies the
    SHA-512 checksum, and installs the binary to a local directory.

.PARAMETER InstallDir
    Directory to install dotnetup into. Defaults to ~/.dotnetup.

.PARAMETER Quality
    Build quality to install. Defaults to 'daily'.

.PARAMETER RuntimeId
    Override automatic OS/architecture detection with an explicit RID
    (e.g. win-x64, linux-arm64, osx-arm64, linux-musl-x64).

.EXAMPLE
    ./get-dotnetup.ps1

.EXAMPLE
    ./get-dotnetup.ps1 -RuntimeId linux-musl-x64 -InstallDir /opt/dotnetup
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $HOME ".dotnetup"),
    [string]$Quality = "daily",
    [string]$RuntimeId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BaseUrl = "https://aka.ms/dotnet/dotnetup/$Quality"

function Get-RuntimeId {
    if ($RuntimeId) {
        return $RuntimeId
    }

    # Use RuntimeInformation so this works on both Windows PowerShell 5.1
    # and PowerShell Core ($IsWindows/$IsMacOS/$IsLinux only exist on Core).

    # Detect OS
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Windows)) {
        $os = "win"
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::OSX)) {
        $os = "osx"
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Linux)) {
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

$rid = Get-RuntimeId
Write-Host "Detected runtime: $rid" -ForegroundColor Cyan

$binaryName = if ($rid -like "win-*") { "dotnetup.exe" } else { "dotnetup" }
$fileName = if ($rid -like "win-*") { "dotnetup-$rid.exe" } else { "dotnetup-$rid" }
$downloadUrl = "$BaseUrl/$fileName"
$checksumUrl = "$downloadUrl.sha512"

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "dotnetup-install-$([System.IO.Path]::GetRandomFileName())"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $tempBinary = Join-Path $tempDir $fileName

    function Invoke-WithRetry {
        param([scriptblock]$ScriptBlock, [string]$ActionDescription, [int]$MaxAttempts = 3)
        $attempt = 1
        while ($true) {
            try {
                & $ScriptBlock
                return
            }
            catch {
                if ($attempt -ge $MaxAttempts) {
                    throw "${ActionDescription} failed after $MaxAttempts attempts.`nError: $($_.Exception.Message)"
                }
                $delay = [Math]::Pow(2, $attempt)
                Write-Host "${ActionDescription} failed (attempt $attempt of $MaxAttempts): $($_.Exception.Message). Retrying in $delay seconds..." -ForegroundColor Yellow
                Start-Sleep -Seconds $delay
                $attempt++
            }
        }
    }

    Write-Host "Downloading $downloadUrl" -ForegroundColor Cyan
    try {
        Invoke-WithRetry -ActionDescription "Download from $downloadUrl" -ScriptBlock {
            Invoke-WebRequest -Uri $downloadUrl -OutFile $tempBinary -UseBasicParsing
        }
    }
    catch {
        throw @"
Failed to download dotnetup from $downloadUrl
Available RIDs: win-x64, win-arm64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64, osx-x64, osx-arm64
Use -RuntimeId to specify the correct RID, or -Quality to select a different build quality.

Error: $($_.Exception.Message)
"@
    }

    Write-Host "Verifying SHA-512 checksum..." -ForegroundColor Cyan
    $tempChecksum = Join-Path $tempDir "$fileName.sha512"
    Invoke-WithRetry -ActionDescription "Download checksum from $checksumUrl" -ScriptBlock {
        Invoke-WebRequest -Uri $checksumUrl -OutFile $tempChecksum -UseBasicParsing
    }

    $expected = ((Get-Content $tempChecksum -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    $actual = (Get-FileHash -Path $tempBinary -Algorithm SHA512).Hash.ToLowerInvariant()
    if ($expected -ne $actual) {
        throw "Checksum mismatch.`n  Expected: $expected`n  Actual:   $actual"
    }
    Write-Host "Checksum verified." -ForegroundColor Green

    # Install just the binary (renamed to plain dotnetup[.exe])
    Write-Host "Installing to $InstallDir..." -ForegroundColor Cyan
    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }

    $installedBinary = Join-Path $InstallDir $binaryName
    Copy-Item -Path $tempBinary -Destination $installedBinary -Force

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
