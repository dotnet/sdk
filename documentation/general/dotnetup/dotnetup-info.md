# dotnetup --info Command

## Overview

The `--info` option displays diagnostic information about the dotnetup tool, including its version, build architecture, and commit information. This is useful for troubleshooting, bug reports, and verifying the installed version.

## Usage

```bash
# Human-readable output (default)
dotnetup --info

# Machine-readable JSON output
dotnetup --info --json
```

## Output Information

The `--info` command displays the following information:

| Field | Description |
|-------|-------------|
| **Version** | The semantic version of dotnetup (e.g., `10.0.100-preview.1`) |
| **Commit** | The Git commit SHA from which the build was created |
| **Architecture** | The processor architecture of the build (e.g., `x64`, `arm64`) |
| **Runtime Identifier** | The RID this build targets (e.g., `win-x64`, `linux-arm64`) |

## Human-Readable Output

The default output is formatted for human consumption:

```
dotnetup Information:
 Version:      10.0.100-preview.1.25118.1
 Commit:       a1b2c3d4e5
 Architecture: x64
 RID:          win-x64
```

## JSON Output (`--json`)

The `--json` option outputs the information in a machine-readable JSON format, suitable for parsing by scripts and tools:

```json
{
  "version": "10.0.100-preview.1.25118.1",
  "commit": "a1b2c3d4e5",
  "architecture": "x64",
  "rid": "win-x64"
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
    }
  },
  "required": ["version", "commit", "architecture", "rid"]
}
```

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
```

### JSON Output for Scripting

```bash
# PowerShell
$info = dotnetup --info --json | ConvertFrom-Json
Write-Host "Using dotnetup version $($info.version)"

# Bash with jq
version=$(dotnetup --info --json | jq -r '.version')
echo "Using dotnetup version $version"
```

### Version Check in CI

```yaml
# Example GitHub Actions usage
- name: Check dotnetup version
  run: |
    info=$(dotnetup --info --json)
    echo "dotnetup info: $info"
```

## Related Commands

- `dotnet --info` - Similar command for the .NET SDK
- `dotnetup --help` - Display help information
