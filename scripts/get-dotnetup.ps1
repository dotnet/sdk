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

function Get-OsReleaseValue {
    # Safely reads a key (e.g. ID, VERSION_ID, ID_LIKE) from /etc/os-release.
    # Returns an empty string when the file or key is absent. Written defensively so
    # that StrictMode does not throw on a missing key (notably ID_LIKE, which is optional).
    param([string]$Key)

    if (-not (Test-Path /etc/os-release)) { return "" }

    $match = Select-String -Path /etc/os-release -Pattern "^$Key=" | Select-Object -First 1
    if (-not $match) { return "" }

    return $match.Line -replace "^$Key=", '' -replace '"', ''
}

function Get-IcuInstallInfo {
    # Returns a hashtable with PackageName and InstallCommand for the current Linux distro.
    # Package names and install commands are derived from:
    # https://github.com/dotnet/core/blob/main/release-notes/8.0/os-packages.json
    $result = @{ PackageName = ""; InstallCommand = "" }

    $distroId      = Get-OsReleaseValue 'ID'
    $distroVersion = Get-OsReleaseValue 'VERSION_ID'
    $distroIdLike  = Get-OsReleaseValue 'ID_LIKE'

    switch -Wildcard ($distroId) {
        "ubuntu" {
            $pkg = switch -Wildcard ($distroVersion) {
                "26.*" { "libicu78" }
                "25.*" { "libicu76" }
                "24.*" { "libicu74" }
                "22.*" { "libicu70" }
                "20.*" { "libicu66" }
                default { "libicu-dev" }
            }
            $result.PackageName   = $pkg
            $result.InstallCommand = "sudo apt-get update && sudo apt-get install -y $pkg"
        }
        "debian" {
            $pkg = switch ($distroVersion) {
                { $_ -in "13","sid" } { "libicu76" }
                "12"                  { "libicu72" }
                "11"                  { "libicu67" }
                default               { "libicu-dev" }
            }
            $result.PackageName   = $pkg
            $result.InstallCommand = "sudo apt-get update && sudo apt-get install -y $pkg"
        }
        { $_ -in "fedora","rhel","centos","centos-stream","almalinux","rocky","ol" } {
            $result.PackageName   = "libicu"
            $result.InstallCommand = "sudo dnf install -y libicu"
        }
        "alpine" {
            $result.PackageName   = "icu-libs"
            $result.InstallCommand = "sudo apk add icu-libs"
        }
        { $_ -in "opensuse-leap","opensuse-tumbleweed","sles" } {
            $result.PackageName   = "libicu"
            $result.InstallCommand = "sudo zypper install -y libicu"
        }
        { $_ -in "mariner","azurelinux" } {
            $result.PackageName   = "icu"
            $result.InstallCommand = "sudo tdnf install -y icu"
        }
        default {
            # Probe ID_LIKE for derivative distros
            if ($distroIdLike -match '\b(ubuntu|debian)\b') {
                $result.PackageName   = "libicu-dev"
                $result.InstallCommand = "sudo apt-get update && sudo apt-get install -y libicu-dev"
            } elseif ($distroIdLike -match '\b(rhel|fedora|centos)\b') {
                $result.PackageName   = "libicu"
                $result.InstallCommand = "sudo dnf install -y libicu"
            } elseif ($distroIdLike -match '\bsuse\b') {
                $result.PackageName   = "libicu"
                $result.InstallCommand = "sudo zypper install -y libicu"
            }
        }
    }

    return $result
}

function Test-IcuPresent {
    # ICU is only required on Linux; Windows and macOS include it natively.
    if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Linux)) {
        return $true
    }

    if (-not (Test-Path /etc/os-release)) {
        # No os-release: fall through to ldconfig / filesystem probing below.
        $distroId     = ""
        $distroIdLike = ""
    } else {
        $distroId     = Get-OsReleaseValue 'ID'
        $distroIdLike = Get-OsReleaseValue 'ID_LIKE'
    }

    # Use the native package-manager query for known distros.
    # Package names derived from:
    # https://github.com/dotnet/core/blob/main/release-notes/8.0/os-packages.json
    switch -Wildcard ($distroId) {
        { $_ -in "ubuntu","debian" } {
            $output = & dpkg -l 'libicu[0-9]*' 2>&1 | Out-String
            return $output -match '(?m)^ii\s+libicu\d'
        }
        { $_ -in "fedora","rhel","centos","centos-stream","almalinux","rocky","ol" } {
            $ec = (Start-Process rpm -ArgumentList '-q','libicu' -Wait -PassThru -NoNewWindow).ExitCode
            return $ec -eq 0
        }
        "alpine" {
            $ec = (Start-Process apk -ArgumentList 'info','-e','icu-libs' -Wait -PassThru -NoNewWindow).ExitCode
            return $ec -eq 0
        }
        { $_ -in "opensuse-leap","opensuse-tumbleweed","sles" } {
            $ec = (Start-Process rpm -ArgumentList '-q','libicu' -Wait -PassThru -NoNewWindow).ExitCode
            return $ec -eq 0
        }
        { $_ -in "mariner","azurelinux" } {
            $ec = (Start-Process rpm -ArgumentList '-q','icu' -Wait -PassThru -NoNewWindow).ExitCode
            return $ec -eq 0
        }
        default {
            # Try ID_LIKE for derivative distros
            if ($distroIdLike -match '\b(ubuntu|debian)\b') {
                $output = & dpkg -l 'libicu[0-9]*' 2>&1 | Out-String
                return $output -match '(?m)^ii\s+libicu\d'
            }
            if ($distroIdLike -match '\b(rhel|fedora|centos)\b') {
                $ec = (Start-Process rpm -ArgumentList '-q','libicu' -Wait -PassThru -NoNewWindow).ExitCode
                return $ec -eq 0
            }
            if ($distroIdLike -match '\bsuse\b') {
                $ec = (Start-Process rpm -ArgumentList '-q','libicu' -Wait -PassThru -NoNewWindow).ExitCode
                return $ec -eq 0
            }
            # Fallback: ldconfig cache
            try {
                $ldconfigOutput = & ldconfig -p 2>&1 | Out-String
                if ($ldconfigOutput -match "libicuuc\.so") { return $true }
            } catch { }
            # Fallback: filesystem search
            $searchDirs = @("/usr/lib","/usr/lib64","/usr/local/lib","/usr/local/lib64","/lib","/lib64")
            foreach ($dir in $searchDirs) {
                if (Test-Path $dir) {
                    if (Get-ChildItem -Path $dir -Filter "libicuuc.so.*" -ErrorAction SilentlyContinue) {
                        return $true
                    }
                }
            }
            return $false
        }
    }
}

# --- Main ---

$rid = Get-RuntimeId
Write-Host "Detected runtime: $rid" -ForegroundColor Cyan

# Check ICU libraries (Linux only).
# The .NET runtime requires ICU for globalization support. Check that the libraries
# are present before downloading dotnetup to give a clear, actionable error message.
if ($rid -like "linux*") {
    $icuInfo = Get-IcuInstallInfo
    if (-not (Test-IcuPresent)) {
        Write-Host "Error: ICU libraries are required to run dotnetup but were not found on this system." -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install ICU using your package manager and re-run this script:" -ForegroundColor Red
        if ($icuInfo.InstallCommand) {
            Write-Host "  $($icuInfo.InstallCommand)" -ForegroundColor Red
        } else {
            Write-Host "  Debian/Ubuntu:  sudo apt-get update && sudo apt-get install -y libicu-dev" -ForegroundColor Red
            Write-Host "  Fedora/RHEL:    sudo dnf install -y libicu" -ForegroundColor Red
            Write-Host "  Alpine Linux:   sudo apk add icu-libs" -ForegroundColor Red
        }
        Write-Host ""
        Write-Host "For more information, see: https://aka.ms/dotnet-missing-libicu" -ForegroundColor Red
        exit 1
    }
}

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
    # Compute SHA-512 directly via .NET to avoid relying on Get-FileHash, which is
    # not always resolvable in stripped-down PowerShell hosts (e.g., some CI agents).
    $sha512 = [System.Security.Cryptography.SHA512]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($tempBinary)
        try {
            $hashBytes = $sha512.ComputeHash($stream)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $sha512.Dispose()
    }
    $actual = ([System.BitConverter]::ToString($hashBytes) -replace '-', '').ToLowerInvariant()
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
