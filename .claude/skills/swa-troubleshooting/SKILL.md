---
name: swa-troubleshooting
description: 'Diagnose and fix common Static Web Assets test and build failures. USE FOR: analyzing CI failures, identifying root cause categories from error messages, fixing metadata propagation bugs, timing/ordering issues in MSBuild targets, test assertion mismatches.'
---

# SWA Troubleshooting

Common failure patterns in Static Web Assets development and their fixes.

## Pattern 1: Metadata Propagation Bugs

**Error signature:**
```
Expected ... to contain "X" but actual path was "Y"
```
Asset paths, content roots, or other metadata don't match expectations.

**Root cause:** An MSBuild task reads metadata from the wrong source. When the data model changes (e.g., a new manifest format, a new metadata field, a different resolution path), tasks that consume that metadata must be updated to read from the new source. A common case is `ContentRoot` — if a task computes it from a file path instead of reading it from item metadata, the result differs.

**Diagnostic approach:**
1. Find the task that produces the mismatched value (search for the property name in `src/StaticWebAssetsSdk/Tasks/`)
2. Trace where the task reads that value — is it from item metadata, a computed path, or a hardcoded convention?
3. Compare against the actual item metadata available at that point in the build (use `-bl` to inspect the binlog)

**Fix:** Update the task to read from the correct metadata source.

## Pattern 2: Target Ordering / DependsOnTargets

**Error signature:**
```
Assets from package 'X' not found in build output
```
Missing assets, empty item groups, or incomplete resolution.

**Root cause:** MSBuild targets that consume data run before the targets that produce it. The `DependsOnTargets` chain has a gap. This is especially common with targets that use `BeforeTargets` (which triggers them early, potentially before their own dependencies are ready).

**Diagnostic approach:**
1. Open the binlog (`-bl`) and find the target that should have produced the missing data
2. Check its execution order relative to the target that consumes it
3. Look for missing `DependsOnTargets` declarations

**Fix:** Add explicit `DependsOnTargets` to ensure data-producing targets complete before data-consuming targets:
```xml
<Target Name="ConsumingTarget"
        DependsOnTargets="ProducerTarget1;ProducerTarget2">
```

**Fix location:** The `.targets` file in `src/StaticWebAssetsSdk/Targets/`, or the `.csproj` of a test asset project.

## Pattern 3: Baseline Drift

**Error signature:**
```
Expected collection to be empty because the generated manifest should match
the expected baseline.
```

**Root cause:** Legitimate build output changed but baseline JSON files were not regenerated.

**Fix:** Use the `swa-baseline-regeneration` skill.

## Pattern 4: Pack Content Mismatch

**Error signature:**
```
Expected nupkg to contain "build\X" but it was not found
```
or unexpected files present in the nupkg.

**Root cause:** Test assertions don't match what the build actually puts into the nupkg for the test's TFM and configuration. Pack targets use MSBuild conditions to determine nupkg content — if a condition branches on TFM version or feature flags, the test must assert the branch its configuration activates.

**Fix:** Open `Microsoft.NET.Sdk.StaticWebAssets.Pack.targets`, find the condition that fires for the test's TFM, and update the test assertions to match what that branch produces. See the Pack Targets section in `AGENTS.md` for the full procedure.

## Pattern 5: WASM/PWA Manifest Failures

**Error signature:**
```
WasmPwaManifestTests ... Expected service-worker-assets.js to contain asset
```

**Root cause:** The Blazor WASM service worker manifest has specific expectations about asset paths and hashes that change when the underlying SWA system changes (asset discovery, fingerprinting, content hashing).

**Fix location:** `test/Microsoft.NET.Sdk.BlazorWebAssembly.Tests/` — the Blazor WASM SDK test project. When SWA infrastructure changes, these tests often need matching updates.

## Pattern 6: Test Assertions Expect Stale Metadata Shape

**Error signature:**
```
Expected asset to have property 'X' with value 'Y'
```

**Root cause:** Integration tests assert specific metadata properties or values on resolved static web assets. When the data model changes (new fields, renamed fields, different default values), those assertions become stale.

**Diagnostic approach:**
1. Check what the test expects vs. what the build actually produces (inspect the test output or use `-bl`)
2. Verify whether the change in metadata is intentional (part of the current feature work)
3. Update assertions to match the new metadata shape

**Fix location:** The integration test class making the assertion (often in `test/Microsoft.NET.Sdk.StaticWebAssets.Tests/`).

## Triage Strategy for CI Failures

When facing multiple CI failures, categorize before fixing:

1. **Count unique test classes** — multiple failures in one class likely share a root cause
2. **Group by error signature** — match against the patterns above
3. **Check TFM** — does the failing test target the current TFM or an older one? Older-TFM tests exercise backward-compat paths
4. **Baseline vs. assertion** — baseline failures need regeneration; assertion failures need code or test changes
5. **Infrastructure vs. code** — timeouts, network errors, and Helix agent issues are not code problems

## AzDO / Helix Navigation

Test results for CI runs live in Azure DevOps pipelines:

1. Pipeline run → Tests tab → filter by "Failed"
2. Group by "Test class" to identify root cause clusters
3. For Helix-distributed tests, the work item log contains the full test output
