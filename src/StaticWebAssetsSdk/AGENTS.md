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

## Triage Heuristic: "Sequence contains more than one element" (and other aggregation crashes)

> **The crash site is never the fix.** A manifest/endpoint task that throws on a collision is the
> *symptom*, not the cause. Work out **why two assets reached the same target-path + `AssetKind` slot**, then
> fix the layer that *should have stopped the second asset from existing*. The most durable fix **prevents the
> surplus asset from being emitted at all**, at its producer — so exactly one asset ever lands on the route —
> rather than letting two land and de-duplicating them downstream. It is **never** the throwing task; but it
> is **also not** automatically "the SDK resolution." Localize where the second asset is *born*, then decide
> whether it should be born conditionally (best) or de-duplicated later (a defensible-but-weaker compensation).

**Symptom.** A Static Web Assets task throws
`InvalidOperationException: Sequence contains more than one element`, or otherwise finds more
than one asset where it expects at most one. The canonical site is
`ChooseNearestAssetKind(group, kind).SingleOrDefault()` in `GenerateStaticWebAssetEndpointsManifest`
(and the identical pattern in `GenerateStaticWebAssetsDevelopmentManifest`).

**What it actually means.** It is a violation of the **target-path uniqueness invariant** documented
above: *at a given target path, at most one asset per `AssetKind` slot (`Build`/`Publish`/`All`)*.
`ChooseNearestAssetKind` is documented to **assume the manifest is already correct** — it deliberately
does not validate or de-duplicate, and yields multiple assets on malformed input precisely so the error
surfaces. So when this throws, **upstream input is malformed**: some producer emitted two assets that
resolve to the same target path + kind.

**The traps (what NOT to do).** Two reflexes both fail here:

1. **Symptom-site softening.** Do not "fix" it at the throw site by replacing `SingleOrDefault()` with
   `First()`, by collapsing/de-duplicating the colliding assets in the consuming task, or by relaxing the
   uniqueness invariant. That masks the real defect and contradicts the task's own contract.
2. **The *unconditional* package remodel.** Do not retype the package's fallback (e.g. model
   `blazor.modules.json` as a `Framework` asset) while still emitting it **unconditionally**. That still
   leaves **two** assets on the route — it just relabels one — so the collision survives.

Both traps were hit on dotnet/sdk#54779 (a symptom-site task "fix" *and* an unconditional `Framework`-asset
remodel) and **both were rejected in review**. **Crucially, the fix that ultimately landed *was* a package
change** (dotnet/aspnetcore#67375, merged) — but a **conditional** one: it makes the package emit its
fallback only when the consumer has no asset of its own, so the second asset never exists. "Change the
package" is not the trap; **changing it *unconditionally*** is. The discriminator is always: *does exactly
one asset end up on the route, or do two?*

**Playbook (do this instead).**

1. **Enumerate every colliding asset** at the offending target path with full provenance —
   `Identity`, `SourceId`, `SourceType`, `AssetKind`, `AssetMode`. Get this from the failing build's
   binlog (search the target path, e.g. `_framework/blazor.modules.json`) or by temporarily logging the
   group contents. You usually have the provenance already; the gap is interpreting it.
2. **Identify the producer** of each colliding asset — which referenced project, NuGet package, or target
   generated it (`SourceType=Package`/`Project`/`Computed`/`Framework` tells you where to look).
3. **Localize the *resolution gap*, not just the producer.** Ask two questions, in order:
   1. **Is the input genuinely malformed?** Is there a duplicate that should *never* exist under any correct
      resolution — e.g. two unrelated sources each emitting a primary asset at one target path with no
      grouping relationship between them? If so, the producer of the spurious asset is at fault.
   2. **Or is the input legitimately grouped, but the surplus variant was emitted anyway?** Assets that
      *intentionally* share a target path do so via **asset groups** (advanced extensibility): a package ships
      a fallback that must be **dropped** once the consumer supplies its own asset for the same path.
      Historically this was modeled with a **deferred** group resolved at **build**, while the build manifest
      deliberately **retained all variants** so transitive consumers could re-resolve against their own graph.
      **Key signal:** if the project **builds** cleanly and only **publish** throws, the build-time resolution
      wasn't reflected at publish — so the variant that *should* have been dropped survives. Two layers can own
      this: the **package** that emits the fallback (it can decline to emit it when the consumer already has an
      asset), and the **SDK** that reloads the build manifest at publish (it can re-apply the build-time
      resolution). The durable fix is the former.
4. **Fix the layer that can stop the second asset from existing.**
   - **A grouped/fallback collision that only manifests at publish** → **preferred: make the package's
     fallback conditional** so exactly one asset is ever produced. The package's build targets run *inside the
     consumer's build* and can see whether the consumer already contributes an asset for the path (e.g.
     `@(_ExistingBuildJSModules) == ''`); gate the fallback on that, and the surplus variant is never emitted —
     no deferred group, no downstream de-duplication, no SDK change. This is what landed for #54779
     (dotnet/aspnetcore#67375). **Acceptable alternative (weaker): carry the build resolution into publish in
     the SDK** — persist the *resolved* (no-longer-`Deferred`) groups into the build manifest and re-apply that
     **unscoped** decision when the manifest is reloaded at publish (dotnet/sdk#54941). It is a real, working
     fix, but it *de-duplicates two assets after the fact* rather than preventing the second one, and it was
     **superseded** by the package fix. Whichever layer you choose, do **NOT** retype the package's asset as a
     `Framework` asset while still emitting it **unconditionally** (it leaves two assets and a redundant
     endpoint), and do **NOT** add a consumer-scoped `Remove`/`SourceId != $(PackageId)` guard in the publish
     targets — at publish the group filter is consumer-scoped (`Source="$(PackageId)"`) and *structurally
     cannot* filter a group owned by a referenced project or package, so a scoped patch is both wrong and
     incomplete.
   - **A genuine duplicate from a referenced project** (no legitimate grouping) → fix that project.
   - **A genuine duplicate emitted by the SDK's own targets** → fix the emitting target.
   In every case, **preserve** the target-path uniqueness invariant and never soften the throwing task.

**Why "blame the producer" is half-right.** Provenance tells you *which* asset collided and *where it came
from*; it does **not** by itself tell you where the fix belongs. The fix belongs where the surplus asset can
be **prevented**. For #54779 that turned out to be the producing package after all — but not because "the
producer is always at fault," and not via an unconditional remodel. The package's targets execute in the
consumer's build, so the package *can* see whether the consumer already owns the asset and decline to emit its
fallback. The earlier belief that "only the SDK can resolve this, because the package can't see the consumer's
graph" was the false premise: at build time, inside the consumer, the package's own targets see exactly the
signal they need (`_ExistingBuildJSModules`). Preventing the asset at its source beats carrying a resolution
forward to delete it later.

**Worked example.** dotnet/sdk#54779 — the `Microsoft.AspNetCore.Components.WebView` package shipped a
fallback `_framework/blazor.modules.json` *unconditionally* (originally via a **deferred** static-web-asset
group). The app *built* fine but failed to *publish* with `Sequence contains more than one element`, because
the fallback survived alongside the app's own modules manifest — two assets on one route. **The fix that
landed is in the package** (dotnet/aspnetcore#67375, merged; backport dotnet/aspnetcore#67401): it removes the
deferred-group machinery and instead materializes the empty (`[]`) fallback as the consumer's own asset
**only when the consumer has no JS modules of its own**, so exactly one asset ever lands on the route and the
collision cannot form — with **no SDK change**. An SDK-side carry-forward (dotnet/sdk#54941) was built and
*empirically verified to work*, but it compensates downstream and was **not merged** (superseded). The earlier
attempts to soften the throwing task and to remodel the package's asset as a `Framework` asset *unconditionally*
were both rejected.

See [Architecture.md](Architecture.md) §"Deferred Groups and `SkipDeferred` Filtering" for the mechanism. A
regression eval for this exact reasoning failure lives in [`evals/`](evals/README.md).

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
