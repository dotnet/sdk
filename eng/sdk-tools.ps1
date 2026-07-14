# General-purpose shared helpers for the SDK repo's build/test scripts.
# Dot-source this file to reuse the functions below; it defines functions only
# and has no top-level side effects, so it is safe to dot-source multiple times.
#
# This is the repo-owned counterpart to arcade's eng/common/tools.ps1: put shared
# logic that is NOT specific to a single feature here. (eng/common is managed by
# arcade and changes there are overwritten, so it cannot host repo-owned helpers.)

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
