# Static Web Assets SDK — Architecture

## 1. What Static Web Assets Solves

Web applications consume static files from multiple sources: the current project's `wwwroot`, class libraries via `ProjectReference`, NuGet packages, and the shared framework. Each source stores files in different physical locations on disk.

Static Web Assets (SWA) creates a **virtual file system** that remaps these files from their original disk locations into a unified view. During development, ASP.NET Core's static file middleware serves files through this virtual layer without knowing their physical paths. At build time, SWA produces a manifest that maps virtual paths to physical locations so the dev server can resolve them. At publish time, SWA copies every asset to its final output location and produces endpoint manifests that configure runtime serving (routes, headers, content negotiation).

The build pipeline is implemented as a set of MSBuild targets and tasks. Targets orchestrate the pipeline phases, tasks contain the logic.

## 2. Static Web Assets

`StaticWebAsset` (`Tasks/Data/StaticWebAsset.cs`) is the core abstraction. It represents a single file tracked through the build and publish pipeline. Every property falls into one of six categories.

### Pipeline Invariants

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
- `BasePath` and `RelativePath` use forward slashes only. `RelativePath` has no leading or trailing slashes. `BasePath` is either `"/"` (representing the empty/root base path) or has no leading or trailing slashes.

**Defaults (applied when not explicitly set)**

- `CopyToOutputDirectory` → `Never`; `CopyToPublishDirectory` → `PreserveNewest`.
- `AssetKind` → `All` (or `Build` when `CopyToPublishDirectory` is `Never`).
- `AssetMode` → `All`; `AssetRole` → `Primary`.

### Identity and Origin

| Property | Type | Description | Invariants |
|----------|------|-------------|------------|
| `Identity` | `string` | Absolute path to the file on disk. Serves as the unique key for the asset. | Required. Defaults to the `FullPath` metadata of the original MSBuild item. |
| `SourceId` | `string` | Identifier of the project or package that owns this asset (project name or NuGet package ID). | Required. Used to group assets by origin and detect conflicts. |
| `SourceType` | `string` | How the asset was discovered. | Required. One of: `Discovered`, `Computed`, `Project`, `Package`, `Framework`. See [Source Types](#source-types). |
| `ContentRoot` | `string` | Absolute path to the root directory containing the asset. | Required. Always ends with a directory separator. For `Discovered` assets this is `wwwroot/`; for `Package` assets it is the package's static web assets folder. |
| `OriginalItemSpec` | `string` | The original MSBuild item spec before normalization. | Required. Fallback when resolving the file on disk. |

### Path and Routing

| Property | Type | Description | Invariants |
|----------|------|-------------|------------|
| `BasePath` | `string` | URL prefix under which the asset is served (e.g., `_content/MyLib`). | Required (may be `/`). Normalized to forward slashes, no leading or trailing slashes. **Ignored when computing target paths** for `Discovered`, `Computed`, and `Framework` assets — these are relative to the project root. |
| `RelativePath` | `string` | Path relative to `ContentRoot`. May contain token expressions. | Required. Normalized to forward slashes. |

**Token expression syntax.** `RelativePath` can embed tokens using the format `#[.{tokenName}]`. The `#` prefix ensures tokens cannot collide with valid file path characters. `[]` delimit the full expression, `{}` delimit the variable name, and a trailing qualifier controls resolution:

| Qualifier | Meaning | Example | File on disk |
|-----------|---------|---------|--------------|
| `?` | Optional — the file on disk is **not** fingerprinted | `app#[.{fingerprint}]?.js` | `app.js` |
| `!` | Preferred — the file on disk **is** fingerprinted | `app#[.{fingerprint}]!.js` | `app.<hash>.js` |
| `~` | File-only — the file on disk **is** fingerprinted, but the token is **excluded from endpoint routes** | `app#[.{fingerprint}]~.js` | `app.<hash>.js` |

`~` is used for package-only fingerprinting: the file inside the nupkg carries the fingerprint in its name, but the endpoint routes served at runtime do not include it.

The only supported token is `fingerprint` (base-36 SHA-256).

**Path computation.** `ComputeTargetPath(prefix, separator)` combines `prefix + BasePath + RelativePath` after normalizing separators. For `Discovered`, `Computed`, and `Framework` assets, `BasePath` is replaced with `""` — these assets live at the project root in the virtual file system. `ComputeLogicalPath()` is equivalent to `ComputeTargetPath("", '/')`.

### Classification

| Property | Type | Description | Invariants |
|----------|------|-------------|------------|
| `AssetKind` | `string` | When the asset is active. | `Build`, `Publish`, or `All`. Defaults to `All` unless `CopyToPublishDirectory` is `Never`, in which case defaults to `Build`. |
| `AssetMode` | `string` | Visibility scope. | `All` (current project + references), `CurrentProject` (owning project only), `Reference` (referencing projects only). Defaults to `All`. |
| `AssetRole` | `string` | Relationship to other assets. | `Primary`, `Related`, or `Alternative`. Defaults to `Primary`. |
| `RelatedAsset` | `string` | Full path to the asset this one is associated with. Forms a chain: an `Alternative` can point to a `Related` asset, which in turn points to a `Primary`. | Required when `AssetRole` is `Related` or `Alternative`. Must reference a valid asset `Identity` in the pipeline. Empty for `Primary`. |
| `AssetTraitName` | `string` | Name of the trait that distinguishes an alternative (e.g., `Content-Encoding`). | Required when `AssetRole` is `Alternative`. |
| `AssetTraitValue` | `string` | Value of the distinguishing trait (e.g., `gzip`, `br`). | Required when `AssetRole` is `Alternative`. |

### Merge Behavior

| Property | Type | Description | Invariants |
|----------|------|-------------|------------|
| `AssetMergeBehavior` | `string` | Conflict resolution when merging assets from multiple TFMs or embedded projects. | `None` (default), `PreferTarget`, `PreferSource`, `Exclude`. |
| `AssetMergeSource` | `string` | Source identifier during merge operations. | Used alongside `AssetMergeBehavior` to determine which copy wins. |

### Copy Behavior

| Property | Type | Description | Invariants |
|----------|------|-------------|------------|
| `CopyToOutputDirectory` | `string` | Whether to copy to the build output directory. | `Never` (default), `PreserveNewest`, `Always`. |
| `CopyToPublishDirectory` | `string` | Whether to copy to the publish output directory. | `PreserveNewest` (default), `Never`, `Always`. When `Never`, `AssetKind` defaults to `Build`. |

### Integrity

| Property | Type | Description | Invariants |
|----------|------|-------------|------------|
| `Fingerprint` | `string` | Base-36 encoded SHA-256 hash of file content. | Required. Used in URL token expressions for cache-busting. |
| `Integrity` | `string` | Base-64 encoded SHA-256 hash of file content. | Required. Used for Subresource Integrity (SRI) in HTTP headers. |
| `FileLength` | `long` | File size in bytes. | Required (≥ 0). Used for `Content-Length` and up-to-date checks. |
| `LastWriteTime` | `DateTimeOffset` | Last modification timestamp. | Required (not `MinValue`). Used for `Last-Modified` and incremental builds. |

### Asset Groups

| Property | Type | Description | Invariants |
|----------|------|-------------|------------|
| `AssetGroups` | `string` | Semicolon-separated `Name=Value` pairs that encode group membership. | Optional. Links this asset to `StaticWebAssetGroup` entries by encoding matching criteria. |

### Source Types

| Value | Origin | `BasePath` in path computation | Typical `ContentRoot` |
|-------|--------|--------------------------------|-----------------------|
| `Discovered` | Found on disk in the current project's `wwwroot` | Ignored (treated as `""`) | `{ProjectDir}/wwwroot/` |
| `Computed` | Generated during build (scoped CSS bundles, JS module manifests) | Ignored (treated as `""`) | `{IntermediateOutputPath}/...` |
| `Project` | From a `ProjectReference`'d library | Used as URL prefix (`_content/{PackageId}`) | Referenced project's content root |
| `Package` | From a NuGet package | Used as URL prefix (`_content/{PackageId}`) | Package's static web assets folder |
| `Framework` | Shared framework files (e.g., Blazor runtime JS). Materialized from package at consume time. | Ignored (treated as `""`) | `{IntermediateOutputPath}/fx/{SourceId}/` |

## 3. Static Web Asset Endpoints

`StaticWebAssetEndpoint` (`Tasks/Data/StaticWebAssetEndpoint.cs`) is the runtime-facing counterpart. It represents a URL route mapped to an asset. During the MSBuild pipeline, endpoints are initially created with `AssetFile = StaticWebAsset.Identity` (an absolute path). When generating the endpoints manifest for runtime, `AssetFile` is rewritten to the asset's virtual path (`StaticWebAsset.ComputeTargetPath`), which is relative to the app's web root. Endpoints never introduce an asset independently; they always refer back to an existing `StaticWebAsset`.

### Core Properties

| Property | Type | Description |
|----------|------|-------------|
| `Route` | `string` | URL path registered in the routing table. Used as the MSBuild item identity. |
| `AssetFile` | `string` | In the pipeline, the absolute path matching `StaticWebAsset.Identity`; in the endpoints manifest/runtime, the asset's virtual path (from `StaticWebAsset.ComputeTargetPath`) relative to the app's web root. Links the endpoint to its underlying asset in the virtual file system. |

### Selectors (Content Negotiation)

`Selectors` is a JSON-serialized array of `StaticWebAssetEndpointSelector` values. Each selector is a request condition that must match for this endpoint to be chosen.

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Request header or attribute to match (e.g., `Content-Encoding`). |
| `Value` | `string` | Expected value (e.g., `gzip`). |
| `Quality` | `string` | Preference weight when multiple selectors match. |

### Response Headers

`ResponseHeaders` is a JSON-serialized array of `StaticWebAssetEndpointResponseHeader` values. These are HTTP response headers included when serving the endpoint.

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Header name (e.g., `Content-Type`, `Cache-Control`, `ETag`). |
| `Value` | `string` | Header value. |

### Endpoint Properties (Metadata)

`EndpointProperties` is a JSON-serialized array of `StaticWebAssetEndpointProperty` values. Build-time metadata carried to the runtime or consumed by downstream tasks.

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Property name (e.g., `fingerprint`, `integrity`, `label`). |
| `Value` | `string` | Property value. |

### Why One Asset Produces Multiple Endpoints

A single asset typically gets at least two endpoints:

1. **Fingerprinted route** — immutable URL with the hash in the path (e.g., `css/app.abc123.css`). Served with `Cache-Control: max-age=31536000, immutable`.
2. **Non-fingerprinted route** — stable URL (e.g., `css/app.css`). Served with `ETag`-based caching.

When compressed alternatives exist, each compressed variant adds further endpoints with `Selectors` for content negotiation (e.g., `Content-Encoding=gzip`).

```
StaticWebAsset (Primary)
├── Identity: D:\project\wwwroot\css\app.css
├── RelativePath: css/app#[.{fingerprint}]!.css
├── Fingerprint: abc123
│
├── Endpoint (fingerprinted)
│   ├── Route: css/app.abc123.css
│   ├── AssetFile: D:\project\wwwroot\css\app.css
│   ├── ResponseHeaders: [Content-Type=text/css, Cache-Control=max-age=31536000,immutable]
│   └── EndpointProperties: [fingerprint=abc123, integrity=sha256-..., label=css/app.css]
│
├── Endpoint (non-fingerprinted)
│   ├── Route: css/app.css
│   ├── AssetFile: D:\project\wwwroot\css\app.css
│   ├── ResponseHeaders: [Content-Type=text/css, ETag="sha256-..."]
│   └── EndpointProperties: [fingerprint=abc123, integrity=sha256-...]
│
└── Alternative Asset (gzip)
    ├── Identity: D:\project\obj\compressed\css\app.css.gz
    ├── AssetRole: Alternative
    ├── RelatedAsset: D:\project\wwwroot\css\app.css
    ├── AssetTraitName: Content-Encoding
    ├── AssetTraitValue: gzip
    │
    └── Endpoint (compressed variant)
        ├── Route: css/app.abc123.css
        ├── AssetFile: D:\project\obj\compressed\css\app.css.gz
        ├── Selectors: [Content-Encoding=gzip]
        └── ResponseHeaders: [Content-Type=text/css, Content-Encoding=gzip, Vary=Content-Encoding]
```

## 4. How Assets Cross Project Boundaries

Assets flow between projects through five source types. Each follows a different discovery and integration path. How a project participates in this flow depends on its **project mode**.

### Project Mode

The MSBuild property `StaticWebAssetProjectMode` controls how a project participates in the SWA pipeline. It determines the base path, asset visibility, compression defaults, and publish layout.

| Mode | Typical project | `StaticWebAssetBasePath` | Set by |
|------|----------------|--------------------------|--------|
| `Default` | Class library (Razor Class Library) | `_content/{PackageId}` | SWA SDK default |
| `Root` | ASP.NET Core web application | `/` | `Microsoft.NET.Sdk.Web` (`Sdk.Server.props`) |
| `SelfContained` | Standalone application | `_content/{PackageId}` | Explicit opt-in |

**Effects of project mode:**

| Aspect | `Default` | `Root` |
|--------|-----------|--------|
| Asset base path | `_content/{PackageId}` (namespaced) | `/` (project root) |
| Asset filtering | Exports assets marked `All` or `Reference` to referencing projects | Includes all referenced assets plus its own; excludes `Reference`-only assets from its own output |
| Publish path prefix | `wwwroot` | `wwwroot/{BasePath}` |
| Build compression | Disabled by default | Enabled by default (`StaticWebAssetBuildCompressAllAssets=true`) |
| Manifest mode | `Default` | `Root` |

The mode is stored in the build manifest's `Mode` field and used at manifest-read time to decide which assets to include in the current project vs. expose as references.

### Discovered — Current Project

Files in the current project's `wwwroot` directory. Scanned by `ResolveProjectStaticWebAssets`. These are the simplest assets: `SourceType=Discovered`, `SourceId` is the current project's package ID, `ContentRoot` is `wwwroot/`.

### Computed — Generated During Build

Created by extension point targets hooked into `GenerateComputedBuildStaticWebAssets` (scoped CSS bundles, JS module manifests, etc.). `SourceType=Computed`, `SourceId` is the current project. These are generated into intermediate output directories.

### Project — Referenced Libraries

Assets from `ProjectReference`'d libraries. The referencing project calls two cross-project MSBuild targets on each reference:

1. **`GetStaticWebAssetsProjectConfiguration`** — returns the referenced project's SWA configuration: `Source` (package ID), `Version`, `GetBuildAssetsTargets`, `GetPublishAssetsTargets`, additional properties to pass during invocation, and manifest path for caching.
2. **`GetCurrentProjectBuildStaticWebAssetItems`** — returns the actual assets, endpoints, and discovery patterns. Results are grouped by `ResultType` metadata (`StaticWebAsset`, `StaticWebAssetEndpoint`, `StaticWebAssetDiscoveryPattern`).

The referencing project sees these with `SourceType=Project` and adds the reference's `BasePath` as a URL prefix (typically `_content/{PackageId}`).

### Package — NuGet Packages

Assets bundled into a nupkg during `dotnet pack`. The pack process (`Pack.targets`) writes assets, endpoints, and discovery patterns into a manifest file inside the package. At consume time, `UpdateExistingPackageStaticWebAssets` (or `ReadPackageAssetsManifest` for the JSON manifest format) reads the package manifest and imports assets with `SourceType=Package`.

The conditional pack logic in `Pack.targets` uses MSBuild conditions to branch on TFM version and feature flags, producing different nupkg layouts (`build/`, `buildMultiTargeting/`, `buildTransitive/`) depending on the branch.

### Framework — Shared Framework Files

A special subset of package assets representing shared framework static files (e.g., Blazor's runtime JavaScript). The flow:

1. **At pack time**: Assets matching a `FrameworkPattern` glob are tagged with `SourceType=Framework` in the package manifest.
2. **At consume time**: Framework assets are **materialized** — physically copied from the package into an intermediate directory (`{IntermediateOutputPath}/fx/{SourceId}/`).
3. **After materialization**: Their metadata is rewritten:
   - `SourceType` → `Discovered`
   - `SourceId` → consuming project's package ID
   - `ContentRoot` → the `fx/` intermediate directory
   - `BasePath` → consuming project's base path
   - `AssetMode` → `CurrentProject`

This materialization makes framework files appear as if they were discovered in the consuming project. The dev server resolves them through the virtual file system like any other discovered asset.

### Embedded — Multi-TFM Inner Builds

When a project targets multiple frameworks (`TargetFrameworks`), each inner build produces its own set of assets. `ResolveEmbeddedProjectsStaticWebAssets` calls each inner build's `GetEmbeddedBuildAssetsTargets` (only for TFMs different from the current build) and merges the results. `AssetMergeBehavior` controls conflict resolution when the same asset exists in multiple TFMs.

### Asset Groups

`StaticWebAssetGroup` (`Tasks/Data/StaticWebAssetGroup.cs`) carries named configuration metadata across project and package boundaries.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Group name. Required. Part of the composite key. |
| `Value` | `string` | Group value. |
| `SourceId` | `string` | Project or package identifier. Required. Part of the composite key. |
| `Deferred` | `bool` | Whether the group is deferred. Defaults to `false`. |

Groups are keyed by `(SourceId, Name)` and stored in a `Dictionary<(string SourceId, string Name), StaticWebAssetGroup>`. They travel alongside assets in manifests and allow a consuming project to read provider-specific settings without coupling to the provider's implementation. An asset's `AssetGroups` property (semicolon-separated `Name=Value` pairs) can reference these groups.

### Discovery Patterns

`StaticWebAssetsDiscoveryPattern` (`Tasks/Data/StaticWebAssetsDiscoveryPattern.cs`) describes a glob pattern for discovering files dynamically at runtime (for the dev server).

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Identifier for the pattern. |
| `Source` | `string` | Package ID origin. |
| `ContentRoot` | `string` | Physical directory root. |
| `BasePath` | `string` | Web-relative path prefix. |
| `Pattern` | `string` | Glob pattern for matching files (e.g., `**`). |

These patterns let the development-time middleware discover files that aren't known at build time (e.g., files added to `wwwroot` after the build).

## 5. Asset Lifecycle — From Disk to Output

### Build Pipeline

Three stages, executed in order:

**Stage 1 — Discovery** (`ResolveCoreStaticWebAssets`): Find existing assets from all sources.
- `ResolveProjectStaticWebAssets` — scan current project's `wwwroot`
- `UpdateExistingPackageStaticWebAssets` — import assets from NuGet packages
- `ResolveEmbeddedProjectsStaticWebAssets` — merge assets from other TFMs (multi-TFM only)
- `ResolveReferencedProjectsStaticWebAssets` — call into referenced projects via configured targets

**Stage 2 — Computation** (`ResolveStaticWebAssetsInputs`): Generate derived assets through the `GenerateComputedBuildStaticWebAssets` extension point.
- Scoped CSS: `.razor.css` files → scope identifiers → CSS rewrite → bundle
- JS Modules: `.razor.js` collocation → JS module initializer manifest
- Other extensions hook in via `GenerateComputedBuildStaticWebAssetsDependsOn`

**Stage 3 — Related Assets** (`ResolveBuildRelatedStaticWebAssets`): Create compressed variants, generate endpoints for all assets, produce manifests.
- Compression: gzip and brotli alternatives with content-negotiation endpoints
- Endpoint generation: map each asset to one or more URL routes with headers and selectors

### Publish Pipeline

Same three-stage structure with publish-specific rules. The build manifest is the starting input.

**Stage 1** (`ResolveCorePublishStaticWebAssets`): Load the build manifest, compute referenced project publish assets, resolve embedded publish assets.

**Stage 2** (`ResolvePublishStaticWebAssets`): Run `GenerateComputedPublishStaticWebAssets` — may produce publish-specific assets (different fingerprinting, different compression settings).

**Stage 3** (`ResolvePublishRelatedStaticWebAssets`): Compress, generate publish endpoints, write the publish manifest.

### Output Copying

After manifests are generated:
- **Build**: `CopyStaticWebAssetsToOutputDirectory` copies assets where `CopyToOutputDirectory` ≠ `Never`.
- **Publish**: `CopyStaticWebAssetsToPublishDirectory` copies assets where `CopyToPublishDirectory` ≠ `Never`.

## 6. Manifests — The Build's Output

Three manifests hand off information between MSBuild (build time) and ASP.NET Core (run time).

### Build Manifest (`staticwebassets.build.json`)

The complete state of the virtual file system. Generated by `GenerateStaticWebAssetsManifest`.

```
{
  "Version": 1,
  "Hash": "<SHA-256 of manifest content>",
  "Source": "<project package ID>",
  "BasePath": "<project base path>",
  "Mode": "Default | Root | SelfContained",
  "ManifestType": "Build",
  "ReferencedProjectsConfiguration": [ ... ],
  "DiscoveryPatterns": [ ... ],
  "Assets": [ ... ],
  "Endpoints": [ ... ]
}
```

- `Mode`: `Default` for libraries, `Root` for the application entry point, `SelfContained` for standalone apps.
- `ReferencedProjectsConfiguration`: metadata for each `ProjectReference` (target names, additional properties). Used by the publish pipeline to call back into referenced projects.
- The build manifest is the input to the publish pipeline.

### Endpoints Manifest (`staticwebassets.endpoints.json`)

Routes mapped to files with headers and selectors. Generated by `GenerateStaticWebAssetEndpointsManifest`.

```
{
  "Version": 1,
  "ManifestType": "Build | Publish",
  "Endpoints": [ ... ]
}
```

Endpoints are filtered to only those pointing to assets present in the manifest. The runtime static file middleware reads this to configure its routing table, response headers, and content-negotiation selectors.

### Development Manifest

A tree-structured manifest consumed by the development-time middleware for the dev server and hot reload. Generated by `GenerateStaticWebAssetsDevelopmentManifest`.

```
{
  "ContentRoots": [ "<absolute paths>" ],
  "Root": {
    "Children": {
      "<segment>": {
        "Children": { ... },
        "Asset": { "ContentRootIndex": 0, "SubPath": "<relative>" },
        "Patterns": [{ "ContentRootIndex": 0, "Pattern": "**", "Depth": 2 }]
      }
    }
  }
}
```

- `ContentRoots`: deduplicated array of base directories. Nodes reference roots by index.
- The tree structure matches URL path segments, enabling efficient prefix-based lookup.
- `Patterns` at each node support runtime file discovery (files added after build).
- Only includes `Build`-kind assets.

## 7. Extension Points

The build pipeline is extensible through `GenerateComputedBuildStaticWebAssets` and `GenerateComputedPublishStaticWebAssets`. Each subsystem hooks into these by adding to `GenerateComputedBuildStaticWebAssetsDependsOn` or `GenerateComputedPublishStaticWebAssetsDependsOn`.

### Scoped CSS (`Microsoft.NET.Sdk.StaticWebAssets.ScopedCss.targets`)

`.razor.css` files → compute unique scope identifiers → rewrite CSS selectors with scope attributes → concatenate into a single bundle (`{ProjectName}.styles.css`). The bundle becomes a `Computed` asset.

### JS Modules (`Microsoft.NET.Sdk.StaticWebAssets.JSModules.targets`)

`.razor.js` files collocated with components → resolve as JS initializer modules → generate a JSON manifest listing all modules. The manifest becomes a `Computed` asset that the Blazor runtime reads at startup.

### Compression (`Microsoft.NET.Sdk.StaticWebAssets.Compression.targets`)

Primary assets → gzip and/or brotli compressed variants. Each compressed file becomes an `Alternative` asset with `AssetTraitName=Content-Encoding` and `AssetTraitValue=gzip|br`. Endpoints for compressed variants include `Selectors` for content negotiation and `Vary: Content-Encoding` response headers.

### Embedded Assets (`Microsoft.NET.Sdk.StaticWebAssets.EmbeddedAssets.targets`)

Multi-TFM projects merging assets from inner builds. Each TFM's inner build exposes assets via `GetCurrentProjectEmbeddedBuildStaticWebAssetItems`. The outer build merges them using `MergeStaticWebAssets`, with `AssetMergeBehavior` resolving conflicts.

## 8. Target Execution Order

### Build Flow

```
StaticWebAssetsPrepareForRun
├── ResolveBuildStaticWebAssets
│   ├── ResolveCoreStaticWebAssets
│   │   ├── UpdateExistingPackageStaticWebAssets
│   │   ├── ResolveProjectStaticWebAssets
│   │   ├── ResolveEmbeddedProjectsStaticWebAssets
│   │   └── ResolveReferencedProjectsStaticWebAssets
│   │       └── ResolveReferencedProjectsStaticWebAssetsConfiguration
│   ├── ResolveStaticWebAssetsInputs
│   │   ├── ResolveCoreStaticWebAssets (already run)
│   │   ├── GenerateComputedBuildStaticWebAssets
│   │   │   └── BundleScopedCssFiles (if ScopedCss enabled)
│   │   ├── ResolveScopedCssAssets (if ScopedCss enabled)
│   │   └── ResolveJSModuleManifestBuildStaticWebAssets (if JSModules enabled)
│   └── ResolveBuildRelatedStaticWebAssets
│       └── ResolveBuildCompressedStaticWebAssets (if Compression enabled)
├── GenerateStaticWebAssetsManifest
└── CopyStaticWebAssetsToOutputDirectory
    └── LoadStaticWebAssetsBuildManifest
```

### Publish Flow

```
StaticWebAssetsPrepareForPublish
├── ResolveStaticWebAssetsConfiguration
├── GenerateStaticWebAssetsPublishManifest
│   └── ResolveAllPublishStaticWebAssets
│       ├── ResolveCorePublishStaticWebAssets
│       │   ├── LoadStaticWebAssetsBuildManifest
│       │   ├── ComputeReferencedProjectsEmbeddedPublishAssets
│       │   └── ComputeReferencedProjectsPublishAssets
│       ├── ResolvePublishStaticWebAssets
│       │   ├── ResolveCorePublishStaticWebAssets (already run)
│       │   └── GenerateComputedPublishStaticWebAssets
│       └── ResolvePublishRelatedStaticWebAssets
│           └── ResolvePublishCompressedStaticWebAssets
└── CopyStaticWebAssetsToPublishDirectory
    ├── StaticWebAssetsPrepareForPublish (already run)
    └── LoadStaticWebAssetsPublishManifest
```

## 9. Task and Target Reference

### Targets by File

| File | Key Targets |
|------|-------------|
| `Microsoft.NET.Sdk.StaticWebAssets.targets` | `StaticWebAssetsPrepareForRun`, `ResolveBuildStaticWebAssets`, `ResolveCoreStaticWebAssets`, `ResolveStaticWebAssetsInputs`, `ResolveBuildRelatedStaticWebAssets`, `ResolveProjectStaticWebAssets`, `UpdateExistingPackageStaticWebAssets`, `GenerateStaticWebAssetsManifest`, `GenerateComputedBuildStaticWebAssets`, `CopyStaticWebAssetsToOutputDirectory`, `ResolveStaticWebAssetsConfiguration`, `LoadStaticWebAssetsBuildManifest` |
| `Microsoft.NET.Sdk.StaticWebAssets.References.targets` | `ResolveReferencedProjectsStaticWebAssetsConfiguration`, `GetStaticWebAssetsProjectConfiguration`, `ResolveReferencedProjectsStaticWebAssets`, `GetCurrentProjectBuildStaticWebAssetItems`, `GetCurrentProjectPublishStaticWebAssetItems` |
| `Microsoft.NET.Sdk.StaticWebAssets.Publish.targets` | `StaticWebAssetsPrepareForPublish`, `GenerateComputedPublishStaticWebAssets`, `GenerateStaticWebAssetsPublishManifest`, `ResolveCorePublishStaticWebAssets`, `ResolvePublishStaticWebAssets`, `ResolveAllPublishStaticWebAssets`, `ComputeReferencedProjectsPublishAssets`, `CopyStaticWebAssetsToPublishDirectory`, `LoadStaticWebAssetsPublishManifest` |
| `Microsoft.NET.Sdk.StaticWebAssets.Compression.targets` | `ResolveBuildCompressedStaticWebAssets`, `GenerateBuildCompressedStaticWebAssets`, `ResolvePublishCompressedStaticWebAssets`, `GeneratePublishCompressedStaticWebAssets` |
| `Microsoft.NET.Sdk.StaticWebAssets.ScopedCss.targets` | `ResolveScopedCssAssets`, `GenerateScopedCssFiles`, `ComputeCssScope`, `BundleScopedCssFiles`, `ResolveBundledCssAssets` |
| `Microsoft.NET.Sdk.StaticWebAssets.JSModules.targets` | `ResolveJSModuleManifestBuildStaticWebAssets`, `GenerateJSModuleManifestBuildStaticWebAssets`, `ResolveJSModuleManifestPublishStaticWebAssets`, `ResolveJsInitializerModuleStaticWebAssets` |
| `Microsoft.NET.Sdk.StaticWebAssets.EmbeddedAssets.targets` | `ResolveEmbeddedProjectsStaticWebAssets`, `ResolveStaticWebAssetsCrossTargetingConfiguration`, `ComputeReferencedProjectsEmbeddedPublishAssets`, `MergeStaticWebAssets` |
| `Microsoft.NET.Sdk.StaticWebAssets.Pack.targets` | Pack-time targets for bundling assets into nupkg (conditional on TFM and feature flags) |

### Tasks by File

| Task | File | Purpose |
|------|------|---------|
| `DefineStaticWebAssets` | `DefineStaticWebAssets.cs` | Create asset definitions from candidates |
| `DefineStaticWebAssetEndpoints` | `DefineStaticWebAssetEndpoints.cs` | Create endpoint definitions |
| `UpdateExternallyDefinedStaticWebAssets` | `UpdateExternallyDefinedStaticWebAssets.cs` | Update imported assets |
| `UpdateStaticWebAssetEndpoints` | `UpdateStaticWebAssetEndpoints.cs` | Update endpoint properties |
| `UpdatePackageStaticWebAssets` | `UpdatePackageStaticWebAssets.cs` | Update package assets |
| `FilterStaticWebAssetEndpoints` | `FilterStaticWebAssetEndpoints.cs` | Filter endpoints by criteria |
| `ComputeReferenceStaticWebAssetItems` | `ComputeReferenceStaticWebAssetItems.cs` | Compute reference assets |
| `ComputeEndpointsForReferenceStaticWebAssets` | `ComputeEndpointsForReferenceStaticWebAssets.cs` | Transform endpoint routes |
| `ComputeStaticWebAssetsForCurrentProject` | `ComputeStaticWebAssetsForCurrentProject.cs` | Filter to current project |
| `ComputeStaticWebAssetsTargetPaths` | `ComputeStaticWebAssetsTargetPaths.cs` | Compute target paths |
| `CollectStaticWebAssetsToCopy` | `CollectStaticWebAssetsToCopy.cs` | Collect assets for copying |
| `MergeStaticWebAssets` | `MergeStaticWebAssets.cs` | Merge assets from multiple TFMs |
| `MergeConfigurationProperties` | `MergeConfigurationProperties.cs` | Merge project configurations |
| `GenerateStaticWebAssetsManifest` | `GenerateStaticWebAssetsManifest.cs` | Generate main manifest |
| `GenerateStaticWebAssetEndpointsManifest` | `GenerateStaticWebAssetEndpointsManifest.cs` | Generate endpoints manifest |
| `GenerateStaticWebAssetsDevelopmentManifest` | `GenerateStaticWebAssetsDevelopmentManifest.cs` | Generate development manifest |
| `ReadStaticWebAssetsManifestFile` | `ReadStaticWebAssetsManifestFile.cs` | Read manifest file |
| `ResolveStaticWebAssetEndpointRoutes` | `ResolveStaticWebAssetEndpointRoutes.cs` | Resolve endpoint routes |
| `ResolveFingerprintedStaticWebAssetEndpointsForAssets` | `ResolveFingerprintedStaticWebAssetEndpointsForAssets.cs` | Resolve fingerprinted endpoints |
| `ResolveStaticWebAssetsEmbeddedProjectConfiguration` | `ResolveStaticWebAssetsEmbeddedProjectConfiguration.cs` | Resolve embedded config |
| `ApplyCompressionNegotiation` | `ApplyCompressionNegotiation.cs` | Apply content negotiation |
| `BrotliCompress` | `Compression/BrotliCompress.cs` | Brotli compression |
| `GZipCompress` | `Compression/GZipCompress.cs` | Gzip compression |
| `DiscoverPrecompressedAssets` | `Compression/DiscoverPrecompressedAssets.cs` | Find pre-compressed assets |
| `ResolveCompressedAssets` | `Compression/ResolveCompressedAssets.cs` | Resolve compression candidates |
| `DiscoverDefaultScopedCssItems` | `DiscoverDefaultScopedCssItems.cs` | Discover scoped CSS |
| `ResolveAllScopedCssAssets` | `ScopedCss/ResolveAllScopedCssAssets.cs` | Resolve all scoped CSS |
| `ApplyCssScopes` | `ScopedCss/ApplyCssScopes.cs` | Apply CSS scopes |
| `ComputeCssScope` | `ScopedCss/ComputeCssScope.cs` | Compute scope identifiers |
| `RewriteCss` | `ScopedCss/RewriteCss.cs` | Rewrite CSS with scopes |
| `ConcatenateCssFiles` | `ScopedCss/ConcatenateCssFiles.cs` | Bundle CSS files |
| `GenerateJsModuleManifest` | `JSModules/GenerateJsModuleManifest.cs` | Generate JS module manifest |
| `ApplyJsModules` | `JSModules/ApplyJsModules.cs` | Apply JS modules to components |
