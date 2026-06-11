# Agent Instructions for Working on the .NET SDK

This document provides comprehensive instructions for AI agents working on the dotnet/sdk repository, particularly for Static Web Assets related changes.

## Repository Structure

The SDK repository contains multiple components. Key paths for Static Web Assets:

```
src/StaticWebAssetsSdk/
├── Tasks/                          # MSBuild task implementations
│   ├── Data/                       # Data structures (StaticWebAsset, StaticWebAssetEndpoint, etc.)
│   ├── Utils/                      # Utilities (PathTokenizer, OSPath, etc.)
│   └── *.cs                        # Task implementations
├── Targets/                        # MSBuild targets (.targets files)
└── Sdk/                            # SDK props and targets

test/Microsoft.NET.Sdk.StaticWebAssets.Tests/
├── StaticWebAssets/                # Unit tests for tasks
└── *.cs                            # Integration tests
```

## Architecture

For the full technical architecture — data model (`StaticWebAsset`, `StaticWebAssetEndpoint`, `StaticWebAssetGroup`), asset lifecycle, cross-project boundaries, manifests, target execution order, and task/target reference tables — see [Architecture.md](Architecture.md).

## Pipeline Invariants

These invariants must hold at all times. Violating any of them is a bug.

**Collection-level**

- **Asset uniqueness**: Every `StaticWebAsset` is unique by `Identity`. No two assets share the same `Identity` within a given build or publish resolution.
- **Target path uniqueness**: At a given target path, at most one asset per `AssetKind` slot (`Build`, `Publish`, `All`). Multiple assets at the same target path are only allowed if they have distinct `AssetGroups`.
- **Referential integrity**: Every non-primary asset (`Related` or `Alternative`) must point to a valid asset definition via `RelatedAsset`. If an asset is removed, all assets that reference it must also be removed.
- **Endpoint validity**: Every `StaticWebAssetEndpoint` must point to an existing `StaticWebAsset` via `AssetFile`. There must be no endpoints referencing non-existent assets.
- **Endpoint uniqueness**: Endpoints are unique by the `(Route, AssetFile)` pair. No two endpoints share the same route and asset file combination.

**Required properties (every asset must have)**

- `Identity`, `SourceId`, `ContentRoot`, `BasePath`, `RelativePath`, `OriginalItemSpec` — non-empty strings.
- `Fingerprint` (base-36 SHA-256), `Integrity` (base-64 SHA-256) — non-empty strings.
- `FileLength` ≥ 0.
- `LastWriteTime` ≠ `DateTimeOffset.MinValue`.

**Valid values**

- `SourceType` ∈ {`Discovered`, `Computed`, `Project`, `Package`, `Framework`}.
- `AssetKind` ∈ {`Build`, `Publish`, `All`}.
- `AssetMode` ∈ {`All`, `CurrentProject`, `Reference`}.
- `AssetRole` ∈ {`Primary`, `Related`, `Alternative`}.

**Conditional requirements**

- `RelatedAsset` is required when `AssetRole` is `Related` or `Alternative`. Must reference a valid asset `Identity`.
- `AssetTraitName` and `AssetTraitValue` are both required when `AssetRole` is `Alternative`.

**Path normalization**

- `ContentRoot` is always an absolute path ending with a directory separator.
- `BasePath` and `RelativePath` use forward slashes only. `BasePath` is normalized via `StaticWebAsset.Normalize()`, which trims leading and trailing slashes and represents the root as `"/"` when the normalized path is empty. `RelativePath` has no leading or trailing slashes.

**Defaults (applied when not explicitly set)**

- `CopyToOutputDirectory` → `Never`; `CopyToPublishDirectory` → `PreserveNewest`.
- `AssetKind` → `All` (or `Build` when `CopyToPublishDirectory` is `Never`).
- `AssetMode` → `All`; `AssetRole` → `Primary`.

## Development Workflow

For the full development workflow (inner loop, patching, testing, validation), use the **Static Web Assets** agent defined in `.github/agents/static-web-assets-agent.agent.md`. It has the step-by-step process, commands, and test strategy.

**Never modify the system dotnet SDK.** Always use the repo-local redist SDK at `artifacts/bin/redist/{Configuration}/dotnet/` or a freshly downloaded copy for Blazor WASM scenarios.

## Security Considerations When Working on Static Web Assets Features

- Installed NuGet packages can inject and execute arbitrary code as part of the build via MSBuild tasks and targets.

## Performance Considerations When Working on Static Web Assets Features

- `StaticWebAsset` and `StaticWebAssetEndpoint` collections can be very large in real-world projects — a single application can produce thousands of assets, and each asset can have multiple endpoints (fingerprinted, non-fingerprinted, compressed variants). When compressed assets are generated, the endpoint count multiplies further.
- **Avoid O(n²) or worse algorithms** over asset or endpoint collections. Prefer dictionary lookups, hash sets, or single-pass linear scans.
- **Use `Dictionary` or `HashSet` for membership checks** instead of scanning lists with `Contains` or `Any` on identity/path values.
- **Sort once, scan once.** When ordering matters (e.g., parent-before-child for related assets), sort the collection once and process it in a single linear pass rather than doing repeated lookups.
- **Pre-size collections** when the approximate count is known (e.g., `new List<StaticWebAsset>(assets.Length)`).
- Use `OSPath.PathComparer` for all dictionaries and hash sets keyed by file paths. This handles case-insensitive comparison on Windows and case-sensitive on Linux, matching the OS file system behavior.

## Coding Conventions

### Prefer Strongly-Typed Representations

Always convert `ITaskItem` inputs to their strongly-typed representations (`StaticWebAsset`, `StaticWebAssetEndpoint`, `StaticWebAssetGroup`, etc.) at the task boundary, then work with the typed objects throughout the task logic. Do not pass `ITaskItem` into internal helper methods or perform `GetMetadata` calls deep inside the processing pipeline.

```csharp
// Good: convert at boundary, work with typed objects
var asset = StaticWebAsset.FromTaskItem(element);
ProcessAsset(asset);

// Avoid: passing ITaskItem into helpers
ProcessAsset(element); // then calling element.GetMetadata("RelativePath") inside
```

Convert back to `ITaskItem` only when assigning to `[Output]` properties at the end of `Execute`.

### Use Named Constants

Source types, asset kinds, asset modes, and asset roles all have named constants defined as inner classes on `StaticWebAsset` (e.g., `StaticWebAsset.SourceTypes.Package`, `StaticWebAsset.AssetKinds.All`, `StaticWebAsset.AssetModes.CurrentProject`, `StaticWebAsset.AssetRoles.Primary`). Always use these constants instead of string literals.
