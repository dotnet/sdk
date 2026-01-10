# .NET Workload Discovery and Dependency Information CLI Enhancements

**Status:** Proposal  
**Authors:** GitHub Copilot (based on maui-containers repository requirements)  
**Created:** 2026-01-07  
**Updated:** 2026-01-07

## Contents

* [Overview](#overview)
* [Problem Statement](#problem-statement)
* [Goals](#goals)
* [Proposed Solutions](#proposed-solutions)
  * [Enhanced `dotnet workload search version`](#1-enhanced-dotnet-workload-search-version)
  * [New `dotnet workload info` Command](#2-new-dotnet-workload-info-command)
* [Use Cases](#use-cases)
* [Implementation Considerations](#implementation-considerations)
* [Migration Path](#migration-path)
* [Appendices](#appendices)

## Overview

This specification proposes enhancements to the `dotnet workload` CLI to enable programmatic workload discovery and dependency information retrieval:

1. **Extend `dotnet workload search version`** - Add `--sdk-version` and `--latest` flags for cross-SDK queries
2. **Add `dotnet workload info <workload>`** - New command for dependency information with JSON output

## Problem Statement

Container build systems (like [maui-containers](https://github.com/redth/maui-containers)) currently require complex PowerShell scripts to:

1. **Find latest workload versions** for specific .NET SDK versions (currently locked to active SDK)
2. **Extract dependency information** (Android SDK packages, JDK versions, Xcode requirements)
3. **Query across SDK versions** (e.g., build containers for .NET 9.0 while using 10.0 SDK)

### Current Capabilities (Baseline)

`dotnet workload search version` already provides:
- ✅ List workload set versions for the **current** SDK band
- ✅ Get manifest versions for a specific workload version  
- ✅ JSON output support (`--format json`)
- ✅ Reverse lookup (find workload version from manifest versions)

**What's Missing:**
- ❌ Cannot query workload versions for **different** SDK versions
- ❌ No convenient "latest" shorthand (must parse `--take 1`)
- ❌ No access to workload dependency information (Android SDK, JDK, Xcode)

**Recent Enhancement - PR #52329:**

PR [#52329](https://github.com/dotnet/sdk/pull/52329) adds human-readable dependency output to `dotnet workload --info`:

```
[android]
  Installation Source: SDK 10.0.200
  Manifest Version:    36.1.2/10.0.200
  Dependencies (jdk):
     Version:             [17.0,22.0)
     Recommended Version: 17.0.14
  Dependencies (androidsdk):
     - Android SDK Build-Tools 35
         build-tools;35.0.0
     - Android Emulator (optional)
         emulator
         Recommended Version: 35.1.20
```

**Limitations of PR #52329:**
- Only shows dependencies for **installed** workloads
- No JSON output for scripting
- Cannot query for different SDK versions

## Goals

1. **Cross-Version Workload Discovery** - Query workload sets for any SDK version, not just the active one
2. **Machine-Readable Dependency Information** - Expose dependency metadata in JSON format for scripting
3. **Query Dependencies Without Installation** - View workload dependencies without installing the workload
4. **Backward Compatibility** - Maintain existing command behavior
5. **Replace Custom Scripting** - Enable CI/CD pipelines to use native dotnet CLI commands instead of custom PowerShell/NuGet API integration

## Proposed Solutions

### 1. Enhanced `dotnet workload search version`

**Add two new flags to enable cross-SDK queries:**

```
--sdk-version <VERSION>  Query workload versions for the specified SDK version
--latest                 Return only the latest workload version (shorthand for --take 1)
```

#### Examples

```bash
# Get latest workload version for .NET 9.0 (from any SDK)
$ dotnet workload search version --sdk-version 9.0.100 --latest
9.0.308.2

# Get latest workload version for current SDK
$ dotnet workload search version --latest
10.0.101.1

# List top 3 workload versions for .NET 9.0
$ dotnet workload search version --sdk-version 9.0.100 --take 3
9.0.308.2
9.0.308.1
9.0.308

# Get manifests for specific version from different SDK (text)
$ dotnet workload search version 9.0.304 --sdk-version 9.0.100
Workload manifest ID                               Manifest feature band      Manifest Version
------------------------------------------------------------------------------------------------
microsoft.net.sdk.android                          9.0.100                   35.0.105
microsoft.net.sdk.ios                              9.0.100                   18.0.9372
microsoft.net.sdk.maui                             9.0.100                   9.0.10
...

# Get latest workload version with metadata (JSON)
$ dotnet workload search version --sdk-version 9.0.100 --latest --format json
{
  "workloadVersion": "9.0.308.2",
  "sdkVersion": "9.0.100",
  "sdkBand": "9.0.100"
}

# Get manifests for specific version (JSON)
$ dotnet workload search version 9.0.304 --sdk-version 9.0.100 --format json
{
  "workloadVersion": "9.0.304",
  "sdkVersion": "9.0.100",
  "sdkBand": "9.0.100",
  "manifestVersions": {
    "microsoft.net.sdk.android": "35.0.105/9.0.100",
    "microsoft.net.sdk.ios": "18.0.9372/9.0.100",
    "microsoft.net.sdk.maui": "9.0.10/9.0.100"
  }
}
```

#### Command Options

```
Usage:
  dotnet workload search version [<WORKLOAD_VERSION>...] [options]

Arguments:
  <WORKLOAD_VERSION>  Output workload manifest versions for the provided workload version.

Options:
  --sdk-version <VERSION>  Query for specified SDK version (NEW)
  --latest                 Return only latest workload version (NEW)
  --format <format>        Output format: 'json' or 'list' [default: list]
  --take <count>           Number of results to return [default: 5]
  --include-previews       Include prerelease versions [default: False]
  -?, -h, --help           Show command line help.
```

#### Implementation Notes

- When `--sdk-version` is provided, calculate SDK band (round patch to nearest 100)
  - Example: `9.0.304` → band `9.0.300`
- Query NuGet API for `Microsoft.NET.Workloads.{major}.{band}` packages
- Return workload versions compatible with that SDK band
- `--latest` is syntactic sugar for `--take 1` with cleaner output (no array in JSON)
- Enhance JSON output to include SDK version and band metadata
- Backward compatible: existing usage works unchanged

### 2. New `dotnet workload info` Command

Add a new command for detailed workload dependency information. Unlike `dotnet workload --info` which shows installed workloads, this command queries any workload (installed or not) and provides machine-readable JSON output.

**Key Features:**

| Feature | Description |
|---------|-------------|
| Query any workload | Works for both installed and non-installed workloads |
| JSON output | Structured data for scripting and automation |
| Cross-SDK-version queries | Query dependencies for different SDK versions via `--sdk-version` |
| Platform-specific filtering | Filter platform-specific dependencies via `--platform` |

#### Usage

```bash
dotnet workload info <WORKLOAD_ID> [OPTIONS]
```

#### Examples

**Text Output:**
```bash
$ dotnet workload info android
Workload: android
Manifest ID: microsoft.net.sdk.android
Manifest Version: 36.1.2/10.0.100
Workload Set Version: 10.0.101.1 (installed)
SDK Version: 10.0.101
SDK Band: 10.0.100

Dependencies:
  JDK:
    Recommended Version: 17.0.14
    Version Range: [17.0,22.0)
    Major Version: 17
  
  Android SDK Packages:
    build-tools;35.0.0 - Android SDK Build-Tools 35 (Required)
    cmdline-tools;13.0 - Android SDK Command-line Tools (Required)
    emulator - Android Emulator (Optional, Recommended: 35.1.20)
    platforms;android-35 - Android SDK Platform 35 (Required)
    platform-tools - Android SDK Platform-Tools (Required, Recommended: 35.0.2)
```

**JSON Output:**
```bash
$ dotnet workload info android --format json
{
  "workloadId": "android",
  "manifestId": "microsoft.net.sdk.android",
  "manifestVersion": "36.1.2",
  "manifestFeatureBand": "10.0.100",
  "manifestPackageId": "Microsoft.NET.Sdk.Android.Manifest-10.0.100",
  "workloadSetVersion": "10.0.101.1",
  "sdkVersion": "10.0.101",
  "sdkBand": "10.0.100",
  "dependencies": {
    "jdk": {
      "recommendedVersion": "17.0.14",
      "versionRange": "[17.0,22.0)",
      "majorVersion": 17
    },
    "androidsdk": {
      "packages": [
        {
          "id": "build-tools;35.0.0",
          "description": "Android SDK Build-Tools 35",
          "optional": false,
          "recommendedVersion": null
        },
        {
          "id": "emulator",
          "description": "Android Emulator",
          "optional": true,
          "recommendedVersion": "35.1.20"
        },
        {
          "id": "platforms;android-35",
          "description": "Android SDK Platform 35",
          "optional": false,
          "recommendedVersion": null
        },
        {
          "id": "platform-tools",
          "description": "Android SDK Platform-Tools",
          "optional": false,
          "recommendedVersion": "35.0.2"
        }
      ],
      "apiLevel": "35",
      "buildToolsVersion": "35.0.0",
      "cmdLineToolsVersion": "13.0"
    }
  }
}
```

**iOS Workload:**
```bash
$ dotnet workload info ios --format json
{
  "workloadId": "ios",
  "manifestId": "microsoft.net.sdk.ios",
  "manifestVersion": "26.2.10191",
  "manifestFeatureBand": "10.0.100",
  "dependencies": {
    "xcode": {
      "recommendedVersion": "16.2",
      "versionRange": "[16.0,17.0)",
      "majorVersion": 16
    },
    "sdk": {
      "version": "18.2",
      "platform": "iOS"
    }
  }
}
```

**Cross-SDK Query:**
```bash
# Query for specific SDK version (uses latest workload version for that SDK)
$ dotnet workload info android --sdk-version 9.0.300 --format json
{
  "workloadId": "android",
  "manifestVersion": "35.0.105",
  "workloadSetVersion": "9.0.308.2",
  "sdkVersion": "9.0.300",
  "sdkBand": "9.0.300",
  "dependencies": {
    "jdk": {
      "recommendedVersion": "17.0.13",
      "versionRange": "[17.0,22.0)"
    },
    "androidsdk": {
      "packages": [...],
      "apiLevel": "35",
      "buildToolsVersion": "35.0.0"
    }
  }
}
```

**Specific Workload Version Query:**
```bash
# Query for exact workload version (useful for reproducible builds)
$ dotnet workload info android --workload-version 9.0.304 --format json
{
  "workloadId": "android",
  "manifestVersion": "35.0.103",
  "workloadSetVersion": "9.0.304",
  "sdkVersion": "9.0.300",
  "sdkBand": "9.0.300",
  "dependencies": {
    "jdk": {
      "recommendedVersion": "17.0.13",
      "versionRange": "[17.0,22.0)"
    },
    "androidsdk": {
      "packages": [...],
      "apiLevel": "35",
      "buildToolsVersion": "35.0.0"
    }
  }
}
```

**Platform-Specific Query:**
```bash
$ dotnet workload info android --platform linux-arm64 --format json
# System images filtered for linux-arm64 (arm64-v8a)
```

#### Command Options

```
Usage:
  dotnet workload info <WORKLOAD_ID> [options]

Arguments:
  <WORKLOAD_ID>  Workload identifier (e.g., 'android', 'ios', 'maui')

Options:
  --sdk-version <VERSION>       Query for the specified SDK version
  --workload-version <VERSION>  Query for a specific workload version
  --platform <RID>              Filter platform-specific dependencies (e.g., 'linux-x64', 'osx-arm64')
  --format <format>             Output format: 'text' or 'json' [default: text]
  -?, -h, --help                Show command line help.
```

**Version Resolution Logic:**

The command follows a **local-first** approach, defaulting to installed versions:

- **No flags:** Use currently installed workload version for the active SDK
  - Requires workload to be installed
  - No network call needed - reads from local installation
  - Matches actual environment (consistent with `dotnet workload --info`)
  
- **`--sdk-version` only:** 
  - If specified SDK is installed: Use its installed workload version
  - If specified SDK is not installed: Query latest available workload version from NuGet
  - Enables querying other installed SDKs or discovering versions for SDKs not yet installed
  
- **`--workload-version` only:** Use that exact workload version
  - Downloads manifest from NuGet if not cached
  - SDK band is inferred from the workload version
  - Enables querying any historical or future version
  
- **Both `--sdk-version` and `--workload-version`:** Use the specified workload version
  - Validates that the workload version is compatible with the SDK band
  - Error if versions are incompatible

#### Implementation Notes

**Dependency Parsing:**

For Android workloads, `WorkloadDependencies.json` structure is:
```json
{
  "microsoft.net.sdk.android": {
    "jdk": {
      "version": "[17.0,22.0)",
      "recommendedVersion": "17.0.14"
    },
    "androidsdk": {
      "packages": [
        {
          "desc": "Android SDK Build-Tools 35",
          "sdkPackage": {
            "id": "build-tools;35.0.0"
          },
          "optional": "false"
        },
        {
          "desc": "Google APIs System Image",
          "sdkPackage": {
            "id": {
              "win-x64": "system-images;android-35;google_apis;x86_64",
              "linux-x64": "system-images;android-35;google_apis;x86_64",
              "linux-arm64": "system-images;android-35;google_apis;arm64-v8a"
            }
          },
          "optional": "true"
        }
      ]
    }
  }
}
```

For iOS/macOS/tvOS workloads:
```json
{
  "microsoft.net.sdk.ios": {
    "xcode": {
      "version": "[16.0,17.0)",
      "recommendedVersion": "16.2"
    },
    "sdk": {
      "version": "18.2"
    }
  }
}
```

**Implementation Steps:**
1. Resolve workload version:
   - If `--workload-version` specified: Use that version directly (download manifest from NuGet)
   - If `--sdk-version` specified: 
     - Check if SDK is installed locally → use its installed workload version
     - Otherwise → query latest available workload version from NuGet
   - If no flags: Use currently installed workload version from active SDK (requires workload installed)
2. Determine SDK band from workload version (parse workload set package name or local installation)
3. If manifest not available locally: Download manifest package from NuGet (`Microsoft.NET.Sdk.{Workload}.Manifest-{Band}`)
4. Extract `WorkloadManifest.json` and `WorkloadDependencies.json`
5. Parse dependencies and resolve platform-specific package IDs
6. Return structured output in requested format

**Local-First Optimization:**
- When no flags specified: Read directly from installed workload manifests (no network call)
- When `--sdk-version` matches installed SDK: Read from that SDK's manifests (no network call)
- Only download from NuGet when querying non-installed versions

**Caching:**
- Cache downloaded manifest packages in NuGet global packages folder
- Cache is persistent (packages are immutable)
- Reuse existing NuGet package download infrastructure

## Use Cases

### Use Case 1: Container Build Scripts

**Current approach (PowerShell + custom functions):**
```powershell
. ./common-functions.ps1
$latestWorkloadSet = Find-LatestWorkloadSet -DotnetVersion "9.0"
$workloadInfo = Get-WorkloadInfo -DotnetVersion "9.0" -IncludeAndroid
$androidDetails = $workloadInfo.Workloads["Microsoft.NET.Sdk.Android"].Details
$jdkVersion = $androidDetails.JdkMajorVersion
$apiLevel = $androidDetails.ApiLevel
```

**Proposed approach (dotnet CLI):**
```bash
# Get latest workload version for .NET 9.0
WORKLOAD_VERSION=$(dotnet workload search version --sdk-version 9.0.100 --latest)

# Get Android dependency info
ANDROID_INFO=$(dotnet workload info android --sdk-version 9.0.100 --format json)
JDK_VERSION=$(echo $ANDROID_INFO | jq -r '.dependencies.jdk.majorVersion')
API_LEVEL=$(echo $ANDROID_INFO | jq -r '.dependencies.androidSdk.apiLevel')
BUILD_TOOLS=$(echo $ANDROID_INFO | jq -r '.dependencies.androidSdk.buildToolsVersion')
```

### Use Case 2: CI/CD Workload Validation

```bash
# Check if workloads are up-to-date
CURRENT=$(dotnet workload --version)
LATEST=$(dotnet workload search version --latest)

if [ "$CURRENT" != "$LATEST" ]; then
  echo "Workloads out of date: $CURRENT vs $LATEST"
  exit 1
fi
```

### Use Case 3: Multi-SDK Build Matrix

```bash
# Generate build matrix for multiple .NET versions
for SDK in 9.0.100 10.0.100; do
  WORKLOAD=$(dotnet workload search version --sdk-version $SDK --latest)
  echo "SDK $SDK: Workload $WORKLOAD"
done
```

### Use Case 4: Reproducible Builds with Pinned Versions

```bash
# Pin to exact workload version for reproducibility
WORKLOAD_VERSION="9.0.304"

# Get dependency info for that exact version
ANDROID_INFO=$(dotnet workload info android --workload-version $WORKLOAD_VERSION --format json)
JDK_VERSION=$(echo $ANDROID_INFO | jq -r '.dependencies.jdk.majorVersion')
API_LEVEL=$(echo $ANDROID_INFO | jq -r '.dependencies.androidSdk.apiLevel')

# Use in Dockerfile
echo "FROM mcr.microsoft.com/dotnet/sdk:9.0"
echo "RUN dotnet workload install android --version $WORKLOAD_VERSION"
```

### Use Case 5: Dependency Documentation

```bash
# Auto-generate required dependencies
dotnet workload info android --format json | \
  jq -r '.dependencies.androidSdk.packages[] | select(.optional == false) | "- \(.id): \(.description)"'

# Output:
# - build-tools;35.0.0: Android SDK Build-Tools 35
# - cmdline-tools;13.0: Android SDK Command-line Tools
# - platforms;android-35: Android SDK Platform 35
# - platform-tools: Android SDK Platform-Tools
```

## Implementation Considerations

### Caching Strategy

- **API Response Cache**: 1 hour TTL for workload set queries
- **Manifest Package Cache**: Persistent (packages are immutable)
- **Cache Location**: NuGet global packages folder (`~/.nuget/packages/`)
- **Cache Invalidation**: `--no-cache` flag to force fresh queries

### Error Handling

**Example Error Messages:**

```bash
# No workload sets for SDK version
$ dotnet workload search version --sdk-version 7.0.100 --latest
Error: No workload sets found for SDK version 7.0.100.
Workload sets are available starting from .NET 8.0.

# Workload not installed (when no flags specified)
$ dotnet workload info android
Error: Workload 'android' is not installed.
Suggestion: Install with 'dotnet workload install android' or query a specific version with '--workload-version'.

# SDK not installed (when using --sdk-version)
$ dotnet workload info android --sdk-version 9.0.100
Warning: SDK version 9.0.100 is not installed locally. Querying latest available workload version from NuGet.
[... continues with latest available version ...]

# Workload not found
$ dotnet workload info nonexistent
Error: Workload 'nonexistent' not found.
Available workloads: android, ios, maccatalyst, macos, maui, tvos, wasm-tools
Suggestion: Use 'dotnet workload search' to see all available workloads.

# Incompatible SDK and workload versions
$ dotnet workload info android --sdk-version 10.0.100 --workload-version 9.0.304
Error: Workload version 9.0.304 is not compatible with SDK version 10.0.100.
Workload version 9.0.304 requires SDK band 9.0.300.
Suggestion: Use '--sdk-version 9.0.300' or omit to use the currently installed workload version.

# Workload version not found
$ dotnet workload info android --workload-version 9.0.999
Error: Workload version 9.0.999 not found.
Suggestion: Use 'dotnet workload search version --sdk-version 9.0.100' to see available versions.

# Network failure (only when querying non-installed versions)
$ dotnet workload search version --sdk-version 9.0.100 --latest
Error: Failed to query NuGet API for workload versions.
Reason: Network connection failed.
Suggestion: Check your internet connection and try again.
```

**Exit Codes:**
- 0: Success
- 1: Validation error (invalid SDK version, invalid workload ID)
- 2: Not found (workload not found, SDK version not found)
- 3: Network error (NuGet API unreachable, package download failed)

### Performance

**Target Benchmarks:**

| Operation | Target | Notes |
|-----------|--------|-------|
| `search version --latest` (cached) | < 50ms | Local cache read |
| `search version --latest` (uncached) | < 2s | NuGet API query |
| `workload info android` (installed) | < 50ms | Read local manifests, no network |
| `workload info android` (not installed) | < 100ms | Parse cached manifest |
| `workload info android --workload-version X` (uncached) | < 3s | Download + parse |

### Security

- Verify NuGet package signatures before extraction
- Validate package hash against NuGet registry metadata
- Use HTTPS for all NuGet API calls
- Validate JSON structure before parsing
- Prevent path traversal when extracting .nupkg files

### Backward Compatibility

**Compatibility Matrix:**

| Command | Existing Behavior | New Behavior | Breaking? |
|---------|------------------|--------------|-----------|
| `dotnet workload search version` | Returns list for current SDK | Same unless `--sdk-version` specified | No |
| `dotnet workload search version X.Y.Z` | Returns manifests for version | Same | No |
| `dotnet workload search version --format json` | Returns JSON array | Same structure | No |
| `dotnet workload info` | N/A (new command) | Returns workload details | No |

## Migration Path

### Phase 0: Foundation (In Progress)
- ✅ PR [#52329](https://github.com/dotnet/sdk/pull/52329) - Human-readable dependency output in `dotnet workload --info`

### Phase 1: Cross-SDK Queries (Priority: High)
- Implement `--sdk-version` flag for `dotnet workload search version`
- Implement `--latest` flag as shorthand for `--take 1`
- Add SDK band calculation logic
- Enhance JSON output with metadata

### Phase 2: Dependency Information (Priority: High)
- Implement `dotnet workload info <workload>` command
- Parse and expose Android, iOS, macOS, tvOS dependencies
- Support `--sdk-version` for cross-version queries
- Support `--platform` for platform-specific filtering

### Phase 3: Polish (Priority: Medium)
- Performance optimizations
- Enhanced error messages
- Comprehensive testing
- Documentation

## Appendices

### Appendix A: SDK Band Calculation

SDK band calculation algorithm:

```
Given SDK version: X.Y.ZZZ
SDK band = X.Y.(floor(ZZZ / 100) * 100)

Examples:
- 10.0.101 → 10.0.100
- 10.0.199 → 10.0.100
- 10.0.200 → 10.0.200
- 9.0.404 → 9.0.400
```

### Appendix B: Version Format Conversions

**NuGet Package Version to CLI Version:**

Stable versions:
```
NuGet: 9.305.0
CLI:   9.0.305
```

Prerelease versions:
```
NuGet: 10.100.0-rc.1.25458.2
CLI:   10.0.100-rc.1.25458.2
```

Algorithm:
```
Given NuGet version: X.YYY.Z[-prerelease]
CLI version = X.0.YYY[.Z][-prerelease]

Where:
- X = major version
- YYY = patch (becomes third component)
- Z = additional component (only if non-zero)
- prerelease = preserved as-is
```

### Appendix C: Related Documentation

- [.NET Workload Sets](https://learn.microsoft.com/dotnet/core/tools/dotnet-workload-sets)
- [.NET Workload Installation](https://learn.microsoft.com/dotnet/core/tools/dotnet-workload-install)
- [dotnet workload search](https://learn.microsoft.com/dotnet/core/tools/dotnet-workload-search)
- [PR #52329 - Workload Dependencies in --info](https://github.com/dotnet/sdk/pull/52329)
