# General-purpose shared helpers for the SDK repo's build/test scripts.
# Dot-source this file to reuse the functions below; it defines functions only
# and has no top-level side effects, so it is safe to dot-source multiple times.
#
# This is the repo-owned counterpart to arcade's eng/common/tools.ps1: put shared
# logic that is NOT specific to a single feature here. (eng/common is managed by
# arcade and changes there are overwritten, so it cannot host repo-owned helpers.)

# Maps a System.Runtime.InteropServices.Architecture enum value to the lowercase
# dotnet RID architecture token (e.g. "x64", "arm64"). Unknown values map to "x64".
#
# The SYNC section below is verified by a test to keep the dotnetup script + dotnet sdk engineering script logic in sync.
# (the get-dotnetup script intentionally does not yet exist in `main` and that script must work standalone)
# BEGIN-SYNC ConvertTo-RidArchitecture (keep identical with scripts/get-dotnetup.ps1)
function ConvertTo-RidArchitecture([System.Runtime.InteropServices.Architecture]$Architecture) {
    switch ($Architecture) {
        ([System.Runtime.InteropServices.Architecture]::Arm64) { return "arm64" }
        ([System.Runtime.InteropServices.Architecture]::X86) { return "x86" }
        ([System.Runtime.InteropServices.Architecture]::Arm) { return "arm" }
        default { return "x64" }
    }
}
# END-SYNC ConvertTo-RidArchitecture

# Detect native OS architecture, which may differ from the process architecture
# (e.g., x64 process running on ARM64 Windows via emulation).
function Get-NativeMachineArchitecture {
    try {
        return ConvertTo-RidArchitecture ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)
    }
    catch {
        # OSArchitecture can throw when a shadowing RuntimeInformation type lacks
        # the property (PSReadLine's polyfill on Windows PowerShell 5.1 under strict mode).
        # PROCESSOR_ARCHITEW6432 reports the native arch under emulation (e.g. an x64 process on ARM64 Windows).
        if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw }
        $procArch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
        switch ($procArch) {
            "ARM64" { return "arm64" }
            "AMD64" { return "x64" }
            "ARM" { return "arm" }
            "x86" { return "x86" }
            default { throw "Unable to determine the native machine architecture: RuntimeInformation.OSArchitecture was unreadable and PROCESSOR_ARCHITEW6432/PROCESSOR_ARCHITECTURE ('$procArch') is empty or unrecognized." }
        }
    }
}

# Detect the current process architecture, which may differ from the native OS
# architecture when running under emulation (e.g., an x64 process on ARM64).
function Get-ProcessMachineArchitecture {
    try {
        return ConvertTo-RidArchitecture ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)
    }
    catch {
        # See Get-NativeMachineArchitecture: the shadowing is Windows-only, so
        # rethrow elsewhere; on Windows fall back to PROCESSOR_ARCHITECTURE, which
        # reflects the (possibly emulated) process arch and cannot be shadowed.
        if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw }
        switch ($env:PROCESSOR_ARCHITECTURE) {
            "ARM64" { return "arm64" }
            "AMD64" { return "x64" }
            "ARM" { return "arm" }
            "x86" { return "x86" }
            default { throw "Unable to determine the process machine architecture: RuntimeInformation.ProcessArchitecture was unreadable and PROCESSOR_ARCHITECTURE ('$env:PROCESSOR_ARCHITECTURE') is empty or unrecognized." }
        }
    }
}
