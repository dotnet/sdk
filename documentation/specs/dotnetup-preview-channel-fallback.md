# dotnetup Preview Channel Fallback Specification

## Overview

This specification defines the behavior of the `dotnetup sdk install --channel preview` command when no preview releases are currently available for any .NET version.

## Background

There is a predictable window each year (typically November through February) where no preview releases exist:
- The current major version (e.g., .NET 10) has reached General Availability (GA)
- The next major version's previews (e.g., .NET 11) haven't started yet

During this window, users requesting the `preview` channel need a reasonable default behavior.

## Design Decisions

### 1. Default Behavior: Automatic Fallback to Latest GA

**Decision**: When `--channel preview` is specified but no preview releases are available, `dotnetup` will automatically install the latest GA version.

**Rationale**:
- Reflects user intention to install the latest/most current version
- Preview users typically want bleeding-edge features; the latest GA is more current than any old preview
- An old preview (e.g., .NET 10 Preview 7 from October) is strictly worse than .NET 10 GA in terms of features, security, and stability
- Semantic expectation: "preview" implies "ahead of stable," not "behind stable"

**Example**:
```bash
$ dotnetup sdk install --channel preview
# When no preview exists:
Installing .NET SDK 10.0.102 to /home/user/.dotnet...
Installed .NET SDK 10.0.102, available via /home/user/.dotnet
```

### 2. Explicit Opt-Out: `--no-fallback` Flag

**Decision**: A `--no-fallback` flag is available to explicitly fail when no preview is available.

**Rationale**:
- Allows CI/CD pipelines to detect when preview builds are unavailable
- Provides control for users who specifically need preview-only behavior
- Follows precedent of other tools with explicit control flags

**Example**:
```bash
$ dotnetup sdk install --channel preview --no-fallback
Error: No version available for channel 'preview'.
No preview releases are currently available. Use --channel latest to install the latest GA version, or remove --no-fallback to allow automatic fallback.
```

### 3. No Warning on Fallback

**Decision**: No warning is emitted when falling back from preview to GA.

**Rationale**:
- The fallback reflects the user's likely intention (install latest)
- Warnings create noise in CI/CD logs
- Users who need strict preview-only behavior can use `--no-fallback`

### 4. No Time-Based Messaging

**Decision**: Do not include messages about when preview releases will become available.

**Rationale**:
- Release dates can change, leading to incorrect information
- Requires annual code updates to maintain accuracy
- Users can check official .NET release schedules if needed

## Implementation Details

### Channel Resolution Logic

The `ChannelVersionResolver.GetLatestPreviewVersion` method:

1. First attempts to find a preview or Go-Live version
2. If none found and `noFallback` is `false` (default):
   - Returns the latest Active (GA) version
3. If none found and `noFallback` is `true`:
   - Returns `null`, causing installation to fail

### Command-Line Interface

```
dotnetup sdk install [channel] [options]

Options:
  --no-fallback    Do not fall back to latest GA version when the requested 
                   channel has no releases available. Causes installation to 
                   fail instead.
```

### Error Messages

When `--no-fallback` is used and no preview is available:
```
Error: No version available for channel 'preview'.
No preview releases are currently available. Use --channel latest to install the latest GA version, or remove --no-fallback to allow automatic fallback.
```

## Scenarios

### Scenario 1: Preview Available
**Input**: `dotnetup sdk install --channel preview`
**Output**: Installs the latest preview version (e.g., .NET 11.0.0-preview.1.12345.67)

### Scenario 2: No Preview Available (Default Behavior)
**Input**: `dotnetup sdk install --channel preview`
**Output**: Installs the latest GA version (e.g., .NET 10.0.102)

### Scenario 3: No Preview Available with `--no-fallback`
**Input**: `dotnetup sdk install --channel preview --no-fallback`
**Output**: Fails with error message explaining no preview is available

### Scenario 4: Explicit GA Request
**Input**: `dotnetup sdk install --channel latest`
**Output**: Always installs the latest GA version regardless of preview availability

## Testing

The implementation includes two tests:

1. **`GetLatestVersionForChannel_Preview_ReturnsLatestPreviewOrFallsBackToGA`**: 
   - Verifies that either a preview version or a GA version is returned
   - Can run year-round

2. **`GetLatestVersionForChannel_PreviewWithNoFallback_ReturnsNullWhenNoPreview`**:
   - Verifies that `noFallback: true` returns `null` when no preview exists
   - Verifies that a preview version is returned when available

## Related

- Original issue: `dotnetup install sdk preview` fails when no preview version is available
- Test file: `test/dotnetup.Tests/ChannelVersionResolverTests.cs`
