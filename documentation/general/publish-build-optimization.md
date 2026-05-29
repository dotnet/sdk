# Publish Build Optimization

## Overview

When `dotnet publish` runs, it implicitly runs a full `Build` before the publish step.
This is necessary because the SDK cannot assume the previous `dotnet build` used the same
configuration (e.g., the default build configuration is `Debug` while publish defaults to
`Release`), runtime identifier, or other settings.

However, for some publish modes, the full `Build` output (written to `bin\<config>\<tfm>\<rid>\`)
is never used by the publish pipeline. The publish steps read from intermediate outputs
(`obj\`) and resolution items, not from the `bin\` directory. This means the `Build` step
produces artifacts that are confusing to users and automation, as `bin\<config>\<tfm>\<rid>\`
contains a full self-contained managed deployment that is not the intended output.

## How Publish Modes Work Today

### Common Architecture

All publish modes share this flow:

1. **Build** (or equivalent) — produces the IL assembly at `@(IntermediateAssembly)` in `obj\`
2. **ComputeResolvedFilesToPublishList** — collects files to publish from `@(IntermediateAssembly)`,
   `@(RuntimeCopyLocalItems)`, `@(RuntimePackAsset)`, and content items
3. **Post-processing** — mode-specific transformations (ILC, ILLink, crossgen2, bundler)
4. **Copy to PublishDir** — final output written to `bin\<config>\<tfm>\<rid>\publish\`

Key insight: All post-processing steps read from `@(IntermediateAssembly)` (obj) and
`@(ResolvedFileToPublish)` (resolved from NuGet/project references), **never** from the
`Build` output directory.

### PublishAot (Native AOT)

**Status: Optimized (this PR)**

- `IlcCompile` reads `@(IntermediateAssembly)` from `obj\` and produces a native binary
- The full `Build` was running a self-contained deployment to `bin\<config>\<tfm>\<rid>\`,
  including apphost, managed DLLs, deps.json, runtimeconfig.json, and runtime pack files
- **None of these files are used** by the AOT pipeline
- **Optimization**: Replace `Build` with `Compile` (plus resource/satellite targets)
- **Opt-out**: Set `UseAotOptimizedPublish=false` to restore full Build behavior

Target chain for optimized AOT publish:
```
BuildOnlySettings → PrepareForBuild → PrepareResources → Compile → CreateSatelliteAssemblies
```

Where `Compile` includes `ResolveReferences → ResolveProjectReferences → CoreCompile`.

### PublishTrimmed (IL Trimming)

**Status: Not yet optimized — candidate for future optimization**

- `ILLink` (the IL trimmer) processes `@(ResolvedFileToPublish)` items marked with
  `PostprocessAssembly=true`
- These items come from `ComputeResolvedFilesToPublishList` which reads from
  `@(IntermediateAssembly)` (obj) and resolved references
- The `Build` output in `bin\` is not consumed by the trimmer
- The same `Compile`-based optimization would apply here

### PublishReadyToRun (R2R / Crossgen2)

**Status: Not yet optimized — candidate for future optimization**

- `RunCrossgen2` processes `@(ResolvedFileToPublish)` items
- Input assemblies come from resolution, not from `Build` output
- Same optimization opportunity as trimming

### PublishSingleFile (Single-File Bundling)

**Status: Not yet optimized — candidate for future optimization**

- `GenerateSingleFileBundle` bundles `@(ResolvedFileToPublish)` items into one executable
- All inputs come from resolved items and `@(IntermediateAssembly)`
- Same optimization opportunity

### Combined Modes

These modes can be combined (e.g., `PublishAot` implies `PublishTrimmed`). The optimization
applies when the outermost mode is optimized:

| Combination | Optimized? | Notes |
|---|---|---|
| PublishAot (implies trimmed) | ✅ Yes | AOT is the outermost mode |
| PublishTrimmed + PublishSingleFile | ❌ Not yet | Future candidate |
| PublishReadyToRun + PublishSingleFile | ❌ Not yet | Future candidate |
| PublishTrimmed alone | ❌ Not yet | Future candidate |
| PublishReadyToRun alone | ❌ Not yet | Future candidate |

## Breaking Change: AOT Publish Build Optimization

### What Changed

Starting in .NET 10, `dotnet publish` with `PublishAot=true` no longer runs a full `Build`
before publish. Instead, it runs only `Compile` (and resource/satellite assembly targets).

### Impact

- **BeforeBuild / AfterBuild targets**: These will not execute during AOT publish. If you have
  custom targets attached to `BeforeBuild`, `AfterBuild`, or using
  `BeforeTargets="Build"` / `AfterTargets="Build"`, they will not run.
  - **Workaround**: Attach your targets to `BeforeTargets="Publish"` / `AfterTargets="Publish"`,
    or to `BeforeTargets="Compile"` / `AfterTargets="Compile"` instead.
- **PostBuildEvent**: Will not execute during AOT publish (consistent with `--no-build` behavior).
- **Third-party NuGet Build hooks**: Targets from NuGet packages that hook into `Build` will be
  skipped (consistent with `--no-build` behavior).
- **Output directory**: `bin\<config>\<tfm>\<rid>\` will no longer contain managed apphost,
  DLLs, deps.json, runtimeconfig.json, or runtime pack files. Only `native\` and `publish\`
  subdirectories will be present.

### Opt-Out

To restore the previous behavior of running a full `Build` before AOT publish, set:

```xml
<PropertyGroup>
  <UseAotOptimizedPublish>false</UseAotOptimizedPublish>
</PropertyGroup>
```

Or pass it on the command line:

```
dotnet publish /p:UseAotOptimizedPublish=false
```

## Future Work

The same optimization could be applied to `PublishTrimmed`, `PublishReadyToRun`, and
`PublishSingleFile` modes, as they all share the same architecture of reading from
`@(IntermediateAssembly)` and resolved references rather than `Build` output. Each mode
would need:

1. Its own condition check (e.g., `UseTrimmingOptimizedPublish`)
2. The same target chain: `BuildOnlySettings → PrepareForBuild → PrepareResources → Compile → CreateSatelliteAssemblies`
3. Tests verifying no managed artifacts in the output directory
4. Documentation of the breaking change for that mode
