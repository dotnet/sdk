# dotnetup --info Command

## Overview

The `--info` option displays diagnostic information about the dotnetup tool, including its version, build architecture, commit information, and installed .NET SDKs/runtimes. This is useful for troubleshooting, bug reports, and verifying the installed version.

## Usage

```bash
# Human-readable output (default) - includes installed SDKs
dotnetup --info

# Machine-readable JSON output
dotnetup --info --json

# Skip listing installations (faster)
dotnetup --info --no-list
```

## Options

| Option | Description |
|--------|-------------|
| `--json` | Output information in JSON format |
| `--no-list` | Skip listing installed SDKs (faster startup) |

## Output Information

The `--info` command displays the following information:

| Field | Description |
|-------|-------------|
| **Version** | The semantic version of dotnetup (e.g., `10.0.100-preview.1`) |
| **Commit** | The Git commit SHA from which the build was created |
| **Architecture** | The processor architecture of the build (e.g., `x64`, `arm64`) |
| **Runtime Identifier** | The RID this build targets (e.g., `win-x64`, `linux-arm64`) |
| **Installations** | List of .NET SDKs and runtimes managed by dotnetup (unless `--no-list` is specified) |

## Human-Readable Output

The default output is formatted for human consumption:

```
dotnetup Information:
 Version:      10.0.100-preview.1.25118.1
 Commit:       a1b2c3d4e5
 Architecture: x64
 RID:          win-x64

Installed .NET (managed by dotnetup):

  C:\Users\user\.dotnet
    SDK                 9.0.304      (x64)
    SDK                 10.0.100     (x64)
    Microsoft.NETCore.App  9.0.5     (x64)

Total: 3
```

## JSON Output (`--json`)

The `--json` option outputs the information in a machine-readable JSON format, suitable for parsing by scripts and tools:

```json
{
  "version": "10.0.100-preview.1.25118.1",
  "commit": "a1b2c3d4e5",
  "architecture": "x64",
  "rid": "win-x64",
  "installations": [
    {
      "component": "sdk",
      "version": "9.0.304",
      "installRoot": "C:\\Users\\user\\.dotnet",
      "architecture": "x64"
    },
    {
      "component": "sdk",
      "version": "10.0.100",
      "installRoot": "C:\\Users\\user\\.dotnet",
      "architecture": "x64"
    }
  ]
}
```

### JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "version": {
      "type": "string",
      "description": "The semantic version of dotnetup"
    },
    "commit": {
      "type": "string",
      "description": "The Git commit SHA (abbreviated) from which the build was created"
    },
    "architecture": {
      "type": "string",
      "enum": ["x86", "x64", "arm", "arm64"],
      "description": "The processor architecture of the build"
    },
    "rid": {
      "type": "string",
      "description": "The Runtime Identifier this build targets"
    },
    "installations": {
      "type": "array",
      "description": "List of installed .NET components (null if --no-list specified)",
      "items": {
        "type": "object",
        "properties": {
          "component": { "type": "string", "enum": ["sdk", "runtime", "aspnetcore", "windowsdesktop"] },
          "version": { "type": "string" },
          "installRoot": { "type": "string" },
          "architecture": { "type": "string" }
        }
      }
    }
  },
  "required": ["version", "commit", "architecture", "rid"]
}
```

## Installation Verification

By default, `--info` verifies that each installation in the manifest still exists on disk. This ensures accurate reporting but may be slower. Use `--no-list` to skip this verification for faster output when you only need dotnetup version information.

## Implementation Notes

### Version Information

The version is obtained from the assembly's `AssemblyInformationalVersionAttribute`, which is automatically populated by the .NET SDK build system. This ensures the version stays in sync with the project file without manual updates.

### Commit SHA

The commit SHA is embedded during build via the `SourceRevisionId` MSBuild property, which is included in the `InformationalVersion` attribute in the format `version+commitsha`. This is standard behavior when using SourceLink or when `SourceRevisionId` is set in CI builds.

### Architecture

The architecture is determined at runtime using `System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture`, which accurately reflects the architecture of the running binary.

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |

The `--info` command always succeeds unless there's an unexpected runtime error.

## Examples

### Basic Usage

```bash
$ dotnetup --info
dotnetup Information:
 Version:      10.0.100-preview.1.25118.1
 Commit:       a1b2c3d4e5
 Architecture: x64
 RID:          win-x64

Installed .NET (managed by dotnetup):

  (none)
Total: 0
```

### JSON Output for Scripting

```bash
# PowerShell
$info = dotnetup --info --json | ConvertFrom-Json
Write-Host "Using dotnetup version $($info.version)"
Write-Host "Installed SDKs: $($info.installations.Count)"

# Bash with jq
version=$(dotnetup --info --json | jq -r '.version')
echo "Using dotnetup version $version"
```

### Fast Version Check (Skip Installation List)

```bash
$ dotnetup --info --no-list
dotnetup Information:
 Version:      10.0.100-preview.1.25118.1
 Commit:       a1b2c3d4e5
 Architecture: x64
 RID:          win-x64
```

## Related Commands

- `dotnetup list` - List installed .NET SDKs and runtimes
- `dotnet --info` - Similar command for the .NET SDK
- `dotnetup --help` - Display help information
