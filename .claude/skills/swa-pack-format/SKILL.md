---
name: swa-pack-format
description: 'Understand and fix Static Web Assets Pack tests. USE FOR: fixing Pack test assertion failures after nupkg content changes, tracing MSBuild conditions to expected nupkg content, updating test assertions for new or changed package layouts, understanding conditional pack logic in .targets files.'
---

# SWA Pack Testing

Pack tests validate that `dotnet pack` produces nupkg files with the correct content (import files, manifests, static assets). The nupkg layout is defined by MSBuild `.targets` files that use conditions to branch on TFM version, project configuration, or feature flags.

## How Pack Content Is Defined

Pack targets live in `src/StaticWebAssetsSdk/Targets/` (primarily `Microsoft.NET.Sdk.StaticWebAssets.Pack.targets`). They populate nupkg content using `<Content>` or `<None>` items with `Pack="true"` and `PackagePath` metadata.

The targets use **MSBuild conditions** to determine which files to include. Common condition axes:

- **Target Framework Version** — different TFMs may produce different nupkg layouts
- **Feature flags** — MSBuild properties that gate new behavior behind conditions
- **Asset presence** — whether the project has scoped CSS, JS modules, etc.

When a condition branches, **each branch produces a different set of nupkg entries**. Test assertions must match the branch that the test's TFM and configuration activate.

## How Pack Tests Work

Pack tests are in `StaticWebAssetsPackIntegrationTest`. They:

1. Create a test project via `CreateAspNetSdkTestAsset()` (optionally overriding the TFM)
2. Run `dotnet pack` against it
3. Assert nupkg contents using helper methods:
   - `NuPkgContainsPatterns(nupkgPath, ...patterns)` — glob matching
   - `NuPkgContain(nupkgPath, ...exactPaths)` — exact path matching

## Fixing Pack Test Assertion Failures

When a pack test fails because the nupkg contains different files than expected:

### Step 1: Identify the test's TFM

Check how the test creates its asset:
```csharp
// Uses the repo's default TFM (current)
var testAsset = ProjectDirectory.CreateAspNetSdkTestAsset(testAssetName);

// Explicitly targets an older TFM
var testAsset = ProjectDirectory.CreateAspNetSdkTestAsset(testAssetName, identifier: "net60")
    .WithProjectChanges(p => { /* sets TargetFramework to net6.0 */ });
```

### Step 2: Trace the condition in Pack.targets

Open `Microsoft.NET.Sdk.StaticWebAssets.Pack.targets` and find the `Condition` attributes on the `<ItemGroup>` elements that populate pack content. Evaluate which branch fires for the test's TFM.

### Step 3: Update assertions to match the active branch

The test assertion must list exactly what the active condition branch puts into the nupkg — no more, no less. Common nupkg paths:
- `build/{file}` — build-time imports and manifests
- `buildMultiTargeting/{file}` — multi-targeting imports
- `buildTransitive/{file}` — transitive dependency imports
- `staticwebassets/**` — the actual asset files

### Step 4: Preserve backward-compat tests

Tests that explicitly target older TFMs exist to verify backward compatibility. Their assertions match the older format and must not be updated to the current format. Only update tests whose TFM activates the new condition branch.

## Intermediate File Assertions

Some tests verify names of intermediate outputs (files generated during the build but not shipped in the nupkg). These file names are also conditioned — trace the `.targets` to find the correct intermediate filename for the test's configuration.

## Key Locations

| What | Where |
|------|-------|
| Pack targets | `src/StaticWebAssetsSdk/Targets/Microsoft.NET.Sdk.StaticWebAssets.Pack.targets` |
| Pack tests | `test/Microsoft.NET.Sdk.StaticWebAssets.Tests/Pack/StaticWebAssetsPackIntegrationTest.cs` |
| Test helpers | `NuPkgContainsPatterns`, `NuPkgContain` in the test utilities |

## General Principle

When you change pack-related `.targets` logic (new conditions, new files, changed paths), find every pack test whose TFM and configuration activate the changed branch, and update its assertions. Leave tests on other branches untouched.
