---
name: author-swa-integration-test
description: 'Author end-to-end Static Web Assets integration tests that exercise MSBuild targets through build, publish, or pack. USE FOR: writing new SWA integration tests, choosing a test asset and base class, dynamically modifying projects at runtime, selecting the right manifest to assert on, verifying the full asset pipeline (primary, compressed, endpoints). DO NOT USE FOR: unit tests of individual tasks (write those directly), baseline regeneration (use swa-baseline-regeneration), troubleshooting failures (use swa-troubleshooting).'
---

# Author SWA Integration Test

## Workflow

Every SWA integration test follows four steps:

1. **Find a test asset** — search the catalog in [references/test-asset-catalog.md](references/test-asset-catalog.md). Prefer reusing an existing asset over creating a new one. If no asset has the right project shape, pick the closest one and modify it at runtime.
2. **Copy and modify** — `CreateAspNetSdkTestAsset()` copies the asset to an isolated temp directory. Use `WithProjectChanges()` for csproj modifications and `File.WriteAllText()` for new files (.targets, .js, .css). Each test gets its own copy — changes never affect other tests.
3. **Execute the action** — build, publish, or pack the project.
4. **Assert on the right manifest** — choose the manifest that answers the question you're asking (see Manifest Selection below).

Read `src/StaticWebAssetsSdk/Architecture.md` for the authoritative reference on asset properties, pipeline invariants, and endpoint rules.

## Choose Base Class

| Scenario | Base Class |
|----------|-----------|
| Build or publish, no baseline comparison | `AspNetSdkTest` |
| Build or publish with baseline JSON comparison | `AspNetSdkBaselineTest` |
| Pack, or pack → restore → build | `IsolatedNuGetPackageFolderAspNetSdkBaselineTest` |

Use `AspNetSdkTest` when you only need to check specific properties on the manifest. Use baseline classes when you need to verify the entire manifest shape matches an expected snapshot.

## Dynamic Project Modification

Never modify the source test asset on disk. The test infrastructure copies assets to a temp directory — modify that copy.

### Modify .csproj via XDocument

```csharp
var projectDirectory = CreateAspNetSdkTestAsset("RazorAppWithP2PReference", identifier: "uniqueId")
    .WithProjectChanges((path, document) =>
    {
        if (Path.GetFileName(path) == "ClassLibrary.csproj")
        {
            document.Root.Add(new XElement("ItemGroup",
                new XElement("StaticWebAssetGroupDefinition",
                    new XAttribute("Include", "MyGroup"),
                    new XAttribute("Value", "v1"))));
            document.Root.Add(new XElement("Import",
                new XAttribute("Project", "Custom.targets")));
        }
    });
```

### Add files at runtime

```csharp
var projectDir = Path.Combine(projectDirectory.TestRoot, "ClassLibrary");
File.WriteAllText(Path.Combine(projectDir, "wwwroot", "app.js"), "console.log('hello');");
File.WriteAllText(Path.Combine(projectDir, "Custom.targets"), "<Project>...</Project>");
```

The `identifier` parameter prevents temp directory collisions when multiple tests reuse the same asset name.

## Manifest Selection

| Manifest | File | What it contains | When to use |
|----------|------|------------------|-------------|
| Build | `staticwebassets.build.json` | All assets — the complete pipeline output | Source of truth for asset properties, roles, source types. Shows the final result of pipeline execution including all variants. |
| Endpoints | `staticwebassets.build.endpoints.json` | Only endpoints surviving group filtering | What the app will "see" at runtime. Use to verify endpoint routes, selectors, response headers, and that group exclusion actually removes endpoints. |
| Publish | `staticwebassets.publish.json` | Assets scoped to publish output | Checking what ships in the published app. |

The build manifest is **unfiltered** — it retains all asset variants so downstream consumers can select at restore time. To verify that a feature *excludes* content, check the **endpoints manifest**, not the build manifest.

### How to load each

```csharp
// Build manifest
var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

// Endpoints manifest
var endpointsPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.endpoints.json");
var endpointsManifest = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(
    File.ReadAllBytes(endpointsPath),
    StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointsManifest);
var endpoints = endpointsManifest?.Endpoints ?? [];
```

## Asserting the Full Pipeline

When a feature adds or modifies assets, verify the full chain — not just one layer.

**Assets in the build manifest:**
- Primary asset: `AssetRole == "Primary"`, correct `RelativePath` and `SourceId`
- Compressed alternatives: `AssetRole == "Alternative"`, `AssetTraitName == "Content-Encoding"`, `AssetTraitValue` is `gzip` or `br`

**Endpoints in the endpoints manifest:**
- Uncompressed endpoint: route matches `RelativePath`, `Selectors.Length == 0`
- Content-negotiated endpoints: same route, `Selectors[0].Name == "Content-Encoding"`
- Direct compressed routes: route ends with `.gz` or `.br`

**For excluded assets:** confirm the build manifest still contains them (unfiltered), but the endpoints manifest has zero matching endpoints.

**For unrelated assets:** verify they're unaffected — existing endpoints for other files still present.

## Test Scope Checklist

Before writing tests, decide which scenarios need coverage. Ask these questions:

- [ ] **Build** — Does the feature affect build-time asset resolution or the build manifest?
- [ ] **Publish** — Does it change what gets published, publish paths, or the publish manifest?
- [ ] **Pack** — Does it affect nupkg layout, the generated `.targets`, or the JSON manifest inside the package?
- [ ] **Pack → Restore → Build** — Does the feature involve cross-package asset flow (e.g., a library consumed via PackageReference)?
- [ ] **P2P reference** — Does it affect how assets propagate between projects via ProjectReference?
- [ ] **Incremental build** — Must the feature survive modify-then-rebuild without a clean? (modify file → rebuild → assert manifest updated)
- [ ] **Group filtering** — Does it involve asset groups, deferred groups, or the FilterStaticWebAssetGroups task?

Not every feature needs all of these. A change to pack layout only needs pack tests. A change to asset resolution during build needs build + possibly P2P tests. Use the checklist to avoid missing a scenario, not to mandate exhaustive coverage.

## Integration Test Patterns

### Build test
```csharp
var build = CreateBuildCommand(projectDirectory, "AppWithP2PReference");
ExecuteCommand(build).Should().Pass();
var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
```

### Publish test
```csharp
var publish = CreatePublishCommand(projectDirectory);
ExecuteCommand(publish).Should().Pass();
// Check files in publish output directory
```

### Pack test
```csharp
var pack = CreatePackCommand(projectDirectory, "LibraryProject");
ExecuteCommand(pack).Should().Pass();
// Open .nupkg as ZipFile, inspect entries
```

### Pack → Restore → Build
```csharp
var pack = CreatePackCommand(projectDirectory, "LibraryProject");
ExecuteCommand(pack).Should().Pass();
var restore = CreateRestoreCommand(projectDirectory, "ConsumerProject");
ExecuteCommand(restore).Should().Pass();
var build = CreateBuildCommand(projectDirectory, "ConsumerProject");
ExecuteCommand(build).Should().Pass();
```

### Incremental build
```csharp
ExecuteCommand(CreateBuildCommand(projectDirectory)).Should().Pass();
File.WriteAllText(Path.Combine(projectDirectory.TestRoot, "wwwroot", "new.js"), "...");
ExecuteCommand(CreateBuildCommand(projectDirectory)).Should().Pass();
// Assert manifest reflects the new file
```

## Reference

- **Architecture**: `src/StaticWebAssetsSdk/Architecture.md` — asset properties, pipeline invariants, endpoint rules
- **Targets**: `src/StaticWebAssetsSdk/Targets/Microsoft.NET.Sdk.StaticWebAssets.targets` — build pipeline
- **Test assets**: `test/TestAssets/TestProjects/` — see [references/test-asset-catalog.md](references/test-asset-catalog.md)
- **Existing tests**: `test/Microsoft.NET.Sdk.StaticWebAssets.Tests/` — follow patterns from neighboring test classes
