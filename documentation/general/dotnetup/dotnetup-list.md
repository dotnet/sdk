# dotnetup list Command

## Overview

The `list` command displays all .NET SDKs and runtimes that are managed by dotnetup. This provides visibility into what dotnetup has installed and is tracking in its manifest.

## Usage

```bash
# Human-readable output (default)
dotnetup list

# Machine-readable JSON output
dotnetup list --json

# Skip verification (faster, reads manifest only)
dotnetup list --no-verify
```

## Options

| Option | Description |
|--------|-------------|
| `--json` | Output list in JSON format |
| `--no-verify` | Skip verifying each installation exists on disk (faster) |

## Output Information

For each installation, the following information is displayed:

| Field | Description |
|-------|-------------|
| **Component** | Type of installation (SDK, Runtime, ASP.NET, Desktop) |
| **Version** | The specific version installed |
| **Install Root** | The directory where .NET is installed |
| **Architecture** | The processor architecture (x64, arm64, etc.) |

## Human-Readable Output

The default output groups installations by install root:

```
Installed .NET (managed by dotnetup):

  C:\Users\user\.dotnet
    .NET SDK     9.0.304              (x64)
    .NET SDK     10.0.100             (x64)
    Runtime      9.0.5                (x64)

  C:\Program Files\dotnet
    .NET SDK     8.0.400              (x64)

Total: 4
```

When no installations are found:

```
Installed .NET (managed by dotnetup):

  (none)
Total: 0
```

## JSON Output (`--json`)

The `--json` option outputs the list in a machine-readable JSON format:

```json
{
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
    },
    {
      "component": "runtime",
      "version": "9.0.5",
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
    "installations": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "component": {
            "type": "string",
            "enum": ["sdk", "runtime", "aspnetcore", "windowsdesktop"],
            "description": "The type of .NET component"
          },
          "version": {
            "type": "string",
            "description": "The version of the installation"
          },
          "installRoot": {
            "type": "string",
            "description": "The root directory of the .NET installation"
          },
          "architecture": {
            "type": "string",
            "enum": ["x86", "x64", "arm", "arm64"],
            "description": "The processor architecture"
          }
        },
        "required": ["component", "version", "installRoot", "architecture"]
      }
    }
  },
  "required": ["installations"]
}
```

## Verification (Default Behavior)

By default, `dotnetup list` verifies each installation exists on disk before displaying it. This ensures accuracy, especially after manual file system changes.

When verification is enabled (default):
- Each installation is checked to confirm the files exist on disk
- Invalid entries (deleted or corrupted installations) are removed from the manifest
- Only valid installations are displayed

Use `--no-verify` to skip verification and read directly from the manifest (faster but may show stale entries):

```bash
dotnetup list --no-verify
```

## Component Types

| Component | Display Name | Description |
|-----------|--------------|-------------|
| `sdk` | .NET SDK | .NET SDK for building applications |
| `runtime` | dotnet (runtime) | .NET Runtime (Microsoft.NETCore.App) |
| `aspnetcore` | aspnet (runtime) | ASP.NET Core Runtime |
| `windowsdesktop` | windowsdesktop (runtime) | Windows Desktop Runtime (WPF/WinForms) |

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |

## Examples

### List All Installations

```bash
$ dotnetup list
Installed .NET (managed by dotnetup):

  C:\Users\user\.dotnet
    .NET SDK      10.0.100             (x64)

Total: 1
```

### JSON Output for Scripting

```bash
# PowerShell - count SDK installations
$list = dotnetup list --json | ConvertFrom-Json
$sdkCount = ($list.installations | Where-Object { $_.component -eq 'sdk' }).Count
Write-Host "You have $sdkCount SDK(s) installed"

# Bash with jq - get all versions
dotnetup list --json | jq -r '.installations[].version'
```

### Skip Verification for Speed

```bash
# Skip verification when you trust the manifest is accurate
$ dotnetup list --no-verify
```
