# Shared helpers for acquiring dotnetup, dot-sourced by both
# eng/configure-toolset.ps1 (bootstrap SDK install) and eng/restore-toolset.ps1
# (test runtime install). This file only defines functions; it has no top-level
# side effects so it is safe to dot-source multiple times.

# Maps a System.Runtime.InteropServices.Architecture enum value to the lowercase
# dotnet RID architecture token (e.g. "x64", "arm64"). Unknown values map to "x64".
function ConvertTo-RidArchitecture([System.Runtime.InteropServices.Architecture]$Architecture) {
    switch ($Architecture) {
        ([System.Runtime.InteropServices.Architecture]::Arm64) { return "arm64" }
        ([System.Runtime.InteropServices.Architecture]::X86) { return "x86" }
        ([System.Runtime.InteropServices.Architecture]::Arm) { return "arm" }
        default { return "x64" }
    }
}

# Detect native OS architecture, which may differ from the process architecture
# (e.g., x64 process running on ARM64 Windows via emulation).
function Get-NativeMachineArchitecture {
    try {
        return ConvertTo-RidArchitecture ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)
    }
    catch {
        # Fallback for environments where RuntimeInformation is unavailable
        return "x64"
    }
}

# Detect the current process architecture, which may differ from the native OS
# architecture when running under emulation (e.g., an x64 process on ARM64).
function Get-ProcessMachineArchitecture {
    try {
        return ConvertTo-RidArchitecture ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)
    }
    catch {
        # Fallback for environments where RuntimeInformation is unavailable
        return "x64"
    }
}

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

# Downloads the public dotnetup installer from aka.ms
# (https://aka.ms/dotnetup/get-dotnetup.ps1) and runs it to install dotnetup into
# $DotnetupDir. Throws on failure so callers can choose how to react.
function Install-DotnetupFromAkaMs([string]$DotnetupDir) {
    # Seed $LASTEXITCODE so strict mode can read it if the script short-circuits
    # without invoking a native process.
    if (-not (Test-Path Variable:LASTEXITCODE)) { $global:LASTEXITCODE = 0 }

    $getDotnetupScript = (Invoke-WebRequest -Uri 'https://aka.ms/dotnetup/get-dotnetup.ps1' -UseBasicParsing).Content
    & ([scriptblock]::Create($getDotnetupScript)) -InstallDir $DotnetupDir
    if ($LASTEXITCODE -ne 0) { throw "get-dotnetup.ps1 exited with code $LASTEXITCODE." }
}
