# Shared helpers for acquiring dotnetup, dot-sourced by both
# eng/configure-toolset.ps1 (bootstrap SDK install) and eng/restore-toolset.ps1
# (test runtime install). This file only defines functions; it has no top-level
# side effects so it is safe to dot-source multiple times.

# General SDK build helpers (Get-NativeMachineArchitecture, etc.).
. (Join-Path $PSScriptRoot 'sdk-tools.ps1')

# Returns $true when an already-downloaded dotnetup binary at $DotnetupExe is
# recent enough (<24h old) and its architecture matches the native machine, so the
# download can be skipped. Returns $false when dotnetup should be (re)downloaded.
function Test-ShouldUseCachedDotnetup([string]$DotnetupExe) {
    if (-not (Test-Path $DotnetupExe)) {
        return $false
    }

    # Re-download dotnetup at most once every 24 hours to avoid unnecessary network calls.
    $age = (Get-Date) - (Get-Item $DotnetupExe).LastWriteTime
    if ($age.TotalHours -ge 24) {
        return $false
    }
    Write-Host "dotnetup binary is less than 24 hours old; skipping re-download." -ForegroundColor DarkGray

    # dotnetup installs runtimes for its own process architecture, so a cached
    # binary downloaded under emulation (process arch != native arch) would install
    # the wrong runtimes. Re-download when the architectures differ.
    if ((Get-NativeMachineArchitecture) -ne (Get-ProcessMachineArchitecture)) {
        Write-Host "Native architecture differs from process architecture; re-downloading dotnetup for the native architecture." -ForegroundColor DarkGray
        return $false
    }

    return $true
}

# Runs a PowerShell script in a SEPARATE PowerShell process so that an 'exit'
# inside it cannot terminate this build host and bypass the caller's try/catch.
# Uses the current host's own executable to keep pwsh / Windows PowerShell 5.1
# parity. Throws on non-zero exit code.
function Invoke-GetDotnetupScript([string]$ScriptPath, [string]$InstallDir, [string]$ErrorLabel) {
    $psExe = (Get-Process -Id $PID).Path
    if (-not $psExe) {
        $psExeName = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }
        $psExe = Join-Path $PSHOME $psExeName
    }
    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }

    # Temporarily set ErrorActionPreference to Continue so that stderr output
    # from the child process does not become a terminating error (the parent
    # shell inherits 'Stop' from eng/common/tools.ps1). We rely on
    # $LASTEXITCODE for error detection instead.
    $prevEAP = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $psExe -NoProfile -ExecutionPolicy Bypass -File $ScriptPath -InstallDir $InstallDir
    }
    finally {
        $ErrorActionPreference = $prevEAP
    }

    if ($LASTEXITCODE -ne 0) { throw "$ErrorLabel exited with code $LASTEXITCODE." }
}

# Invokes a native command (e.g. the dotnetup executable)
# Returns that process exit code WITHOUT letting a non-zero exit become a terminating error.
# (This covers against $ErrorActionPreference and $PSNativeCommandUseErrorActionPreference)
function Invoke-DotnetupNativeCommand([scriptblock]$Command) {
    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }
    $ErrorActionPreference = 'Continue'
    $PSNativeCommandUseErrorActionPreference = $false
    try {
        # Write command output to the host and prevent it from being returned alongside the exit code 
        & $Command | Out-Host
        return $LASTEXITCODE
    }
    catch {
        Write-Host "dotnetup command failed: $($_.Exception.Message)" -ForegroundColor Yellow
        if ($LASTEXITCODE -ne 0) { return $LASTEXITCODE }
        return 1
    }
}

# Downloads the public dotnetup installer from aka.ms
# (https://aka.ms/dotnet/dotnetup/daily/get-dotnetup.ps1) and runs it to install dotnetup into
# $DotnetupDir. Throws on failure so callers can choose how to react.
#
# If a local get-dotnetup.ps1 script exists in the repo (scripts/get-dotnetup.ps1),
# it is used directly instead of downloading from aka.ms. This supports branches
# (e.g. release/dnup) that carry the script locally and avoids merge conflicts
# when code flows between branches with and without the local script.
function Install-DotnetupFromAkaMs([string]$DotnetupDir) {
    $repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
    $localGetter = Join-Path (Join-Path $repoRoot 'scripts') 'get-dotnetup.ps1'

    # Prefer the repo-local script when available (e.g. on release/dnup).
    if (Test-Path $localGetter) {
        Write-Host "Using local get-dotnetup.ps1 from '$localGetter'." -ForegroundColor DarkGray
        Invoke-GetDotnetupScript -ScriptPath $localGetter -InstallDir $DotnetupDir -ErrorLabel "Local get-dotnetup.ps1"
        return
    }

    $getterUrl = 'https://aka.ms/dotnet/dotnetup/daily/get-dotnetup.ps1'
    $getterScript = Join-Path ([System.IO.Path]::GetTempPath()) ("get-dotnetup-{0}.ps1" -f [System.IO.Path]::GetRandomFileName())

    # Download the installer with retry/backoff. Invoke-WebRequest's built-in
    # -MaximumRetryCount is unavailable on Windows PowerShell 5.1, so retry manually.
    $maxAttempts = 3
    for ($attempt = 1; $true; $attempt++) {
        try {
            Invoke-WebRequest -Uri $getterUrl -OutFile $getterScript -UseBasicParsing
            break
        }
        catch {
            if ($attempt -ge $maxAttempts) {
                throw "Failed to download dotnetup installer from $getterUrl after $maxAttempts attempts: $($_.Exception.Message)"
            }
            $delaySeconds = [Math]::Pow(2, $attempt)
            Write-Host "Download of dotnetup installer failed (attempt $attempt of $maxAttempts): $($_.Exception.Message). Retrying in $delaySeconds seconds..." -ForegroundColor Yellow
            Start-Sleep -Seconds $delaySeconds
        }
    }

    try {
        Invoke-GetDotnetupScript -ScriptPath $getterScript -InstallDir $DotnetupDir -ErrorLabel "get-dotnetup.ps1"
    }
    finally {
        Remove-Item $getterScript -Force -ErrorAction SilentlyContinue
    }
}
