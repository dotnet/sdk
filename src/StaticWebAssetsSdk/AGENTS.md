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

## Static Web Assets Build and Publish Flow

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

## Static Web Assets Targets

### Main Targets (Microsoft.NET.Sdk.StaticWebAssets.targets)

| Target | Purpose |
|--------|---------|
| `StaticWebAssetsPrepareForRun` | Entry point - orchestrates build-time asset processing |
| `ResolveBuildStaticWebAssets` | Master orchestration for 3-stage asset resolution |
| `ResolveCoreStaticWebAssets` | Stage 1: Discover existing assets from disk |
| `ResolveStaticWebAssetsInputs` | Stage 2: Generate computed assets |
| `ResolveBuildRelatedStaticWebAssets` | Stage 3: Create derived assets (compression) |
| `ResolveProjectStaticWebAssets` | Discover current project's wwwroot files |
| `UpdateExistingPackageStaticWebAssets` | Update NuGet package assets |
| `GenerateStaticWebAssetsManifest` | Generate all manifest files |
| `GenerateComputedBuildStaticWebAssets` | Generate computed assets (hook) |
| `CopyStaticWebAssetsToOutputDirectory` | Copy assets to output |
| `ResolveStaticWebAssetsConfiguration` | Set up paths and properties |
| `AddStaticWebAssetsManifest` | Add manifest to output |
| `AddStaticWebAssetEndpointsBuildManifest` | Add endpoints manifest to output |
| `LoadStaticWebAssetsBuildManifest` | Load manifest for publish |
| `WriteStaticWebAssetsUpToDateCheck` | IDE up-to-date check support |

### Reference Targets (Microsoft.NET.Sdk.StaticWebAssets.References.targets)

| Target | Purpose |
|--------|---------|
| `ResolveReferencedProjectsStaticWebAssetsConfiguration` | Get configuration from project references |
| `GetStaticWebAssetsProjectConfiguration` | Return this project's SWA config |
| `ResolveReferencedProjectsStaticWebAssets` | Get assets from project references |
| `GetCurrentProjectBuildStaticWebAssetItems` | Expose assets to referencing projects |
| `GetCurrentProjectPublishStaticWebAssetItems` | Expose publish assets to references |

### Publish Targets (Microsoft.NET.Sdk.StaticWebAssets.Publish.targets)

| Target | Purpose |
|--------|---------|
| `StaticWebAssetsPrepareForPublish` | Entry point for publish |
| `GenerateComputedPublishStaticWebAssets` | Generate publish-time computed assets |
| `GenerateStaticWebAssetsPublishManifest` | Generate publish manifest |
| `ResolveCorePublishStaticWebAssets` | Resolve core publish assets |
| `ResolvePublishStaticWebAssets` | Resolve all publish assets |
| `ResolveAllPublishStaticWebAssets` | Master orchestration for publish |
| `ComputeReferencedProjectsPublishAssets` | Get publish assets from references |
| `ComputeReferencedStaticWebAssetsPublishManifest` | Compute referenced manifests |
| `CopyStaticWebAssetsToPublishDirectory` | Copy assets to publish output |
| `CopyStaticWebAssetsEndpointsManifest` | Copy endpoints manifest |
| `LoadStaticWebAssetsPublishManifest` | Load publish manifest |

### Compression Targets (Microsoft.NET.Sdk.StaticWebAssets.Compression.targets)

| Target | Purpose |
|--------|---------|
| `ResolveBuildCompressedStaticWebAssets` | Create compressed variants (build) |
| `GenerateBuildCompressedStaticWebAssets` | Generate compressed files (build) |
| `ResolveBuildCompressedStaticWebAssetsConfiguration` | Configure build compression |
| `ResolvePublishCompressedStaticWebAssets` | Create compressed variants (publish) |
| `GeneratePublishCompressedStaticWebAssets` | Generate compressed files (publish) |
| `ResolvePublishCompressedStaticWebAssetsConfiguration` | Configure publish compression |

### Scoped CSS Targets (Microsoft.NET.Sdk.StaticWebAssets.ScopedCss.targets)

| Target | Purpose |
|--------|---------|
| `ResolveScopedCssAssets` | Main scoped CSS processing |
| `GenerateScopedCssFiles` | Generate scoped CSS |
| `ResolveScopedCssInputs` | Discover scoped CSS inputs |
| `ComputeCssScope` | Compute CSS scope identifiers |
| `ResolveScopedCssOutputs` | Set output paths |
| `BundleScopedCssFiles` | Create CSS bundle |
| `ResolveBundledCssAssets` | Resolve bundled CSS |
| `UpdateLegacyPackageScopedCssBundles` | Back-compat for legacy bundles |

### JS Modules Targets (Microsoft.NET.Sdk.StaticWebAssets.JSModules.targets)

| Target | Purpose |
|--------|---------|
| `ResolveJsInitializerModuleStaticWebAssets` | Resolve JS initializer modules |
| `ResolveJSModuleManifestBuildConfiguration` | Configure build JS manifest |
| `GenerateJSModuleManifestBuildStaticWebAssets` | Generate build JS manifest |
| `ResolveJSModuleManifestBuildStaticWebAssets` | Resolve build JS manifest |
| `ResolveJSModuleManifestPublishConfiguration` | Configure publish JS manifest |
| `GenerateJSModuleManifestPublishStaticWebAssets` | Generate publish JS manifest |
| `ResolveJSModuleManifestPublishStaticWebAssets` | Resolve publish JS manifest |
| `ResolveJSModuleStaticWebAssets` | Resolve component JS modules |

### Embedded Assets Targets (Microsoft.NET.Sdk.StaticWebAssets.EmbeddedAssets.targets)

| Target | Purpose |
|--------|---------|
| `GetStaticWebAssetsCrosstargetingProjectConfiguration` | Get embedded project config |
| `ResolveStaticWebAssetsCrossTargetingConfiguration` | Resolve cross-targeting config |
| `ResolveStaticWebAssetsEmbeddingRules` | Resolve embedding rules |
| `ResolveEmbeddedProjectsStaticWebAssets` | Resolve embedded project assets |
| `ResolveEmbeddedConfigurationsPackageAssets` | Resolve embedded package assets |
| `GetEmbeddedReferencedPackageAssets` | Get embedded package assets |
| `ComputeReferencedProjectsEmbeddedPublishAssets` | Compute embedded publish assets |
| `GetCurrentProjectEmbeddedBuildStaticWebAssetItems` | Expose embedded build assets |
| `GetCurrentProjectEmbeddedPublishStaticWebAssetItems` | Expose embedded publish assets |

## Static Web Assets Tasks

| Task | File | Purpose |
|------|------|---------|
| `DefineStaticWebAssets` | DefineStaticWebAssets.cs | Create asset definitions from candidates |
| `DefineStaticWebAssetEndpoints` | DefineStaticWebAssetEndpoints.cs | Create endpoint definitions |
| `UpdateExternallyDefinedStaticWebAssets` | UpdateExternallyDefinedStaticWebAssets.cs | Update imported assets |
| `UpdateStaticWebAssetEndpoints` | UpdateStaticWebAssetEndpoints.cs | Update endpoint properties |
| `UpdatePackageStaticWebAssets` | UpdatePackageStaticWebAssets.cs | Update package assets |
| `FilterStaticWebAssetEndpoints` | FilterStaticWebAssetEndpoints.cs | Filter endpoints by criteria |
| `ComputeReferenceStaticWebAssetItems` | ComputeReferenceStaticWebAssetItems.cs | Compute reference assets |
| `ComputeEndpointsForReferenceStaticWebAssets` | ComputeEndpointsForReferenceStaticWebAssets.cs | Transform endpoint routes |
| `ComputeStaticWebAssetsForCurrentProject` | ComputeStaticWebAssetsForCurrentProject.cs | Filter to current project |
| `ComputeStaticWebAssetsTargetPaths` | ComputeStaticWebAssetsTargetPaths.cs | Compute target paths |
| `CollectStaticWebAssetsToCopy` | CollectStaticWebAssetsToCopy.cs | Collect assets for copying |
| `MergeStaticWebAssets` | MergeStaticWebAssets.cs | Merge assets from multiple TFMs |
| `MergeConfigurationProperties` | MergeConfigurationProperties.cs | Merge project configurations |
| `GenerateStaticWebAssetsManifest` | GenerateStaticWebAssetsManifest.cs | Generate main manifest |
| `GenerateStaticWebAssetEndpointsManifest` | GenerateStaticWebAssetEndpointsManifest.cs | Generate endpoints manifest |
| `GenerateStaticWebAssetsDevelopmentManifest` | GenerateStaticWebAssetsDevelopmentManifest.cs | Generate development manifest |
| `ReadStaticWebAssetsManifestFile` | ReadStaticWebAssetsManifestFile.cs | Read manifest file |
| `ResolveStaticWebAssetEndpointRoutes` | ResolveStaticWebAssetEndpointRoutes.cs | Resolve endpoint routes |
| `ResolveFingerprintedStaticWebAssetEndpointsForAssets` | ResolveFingerprintedStaticWebAssetEndpointsForAssets.cs | Resolve fingerprinted endpoints |
| `ResolveStaticWebAssetsEmbeddedProjectConfiguration` | ResolveStaticWebAssetsEmbeddedProjectConfiguration.cs | Resolve embedded config |
| `ApplyCompressionNegotiation` | ApplyCompressionNegotiation.cs | Apply content negotiation |
| `BrotliCompress` | Compression/BrotliCompress.cs | Brotli compression |
| `GZipCompress` | Compression/GZipCompress.cs | Gzip compression |
| `DiscoverPrecompressedAssets` | Compression/DiscoverPrecompressedAssets.cs | Find pre-compressed assets |
| `ResolveCompressedAssets` | Compression/ResolveCompressedAssets.cs | Resolve compression candidates |
| `DiscoverDefaultScopedCssItems` | DiscoverDefaultScopedCssItems.cs | Discover scoped CSS |
| `ResolveAllScopedCssAssets` | ScopedCss/ResolveAllScopedCssAssets.cs | Resolve all scoped CSS |
| `ApplyCssScopes` | ScopedCss/ApplyCssScopes.cs | Apply CSS scopes |
| `ComputeCssScope` | ScopedCss/ComputeCssScope.cs | Compute scope identifiers |
| `RewriteCss` | ScopedCss/RewriteCss.cs | Rewrite CSS with scopes |
| `ConcatenateCssFiles` | ScopedCss/ConcatenateCssFiles.cs | Bundle CSS files |
| `GenerateJsModuleManifest` | JSModules/GenerateJsModuleManifest.cs | Generate JS module manifest |
| `ApplyJsModules` | JSModules/ApplyJsModules.cs | Apply JS modules to components |

## Core Data Model

The Static Web Assets system is built around two primary data structures: `StaticWebAsset` and `StaticWebAssetEndpoint`. Understanding their properties and relationships is essential for working with the codebase.

### StaticWebAsset

Defined in `Tasks/Data/StaticWebAsset.cs`. Represents a single static file (CSS, JS, image, etc.) that is tracked through the build and publish pipeline. Each asset carries metadata describing where it came from, how it should be served, and how it relates to other assets.

#### Identity and Source Properties

| Property | Type | Description |
|----------|------|-------------|
| `Identity` | `string` | Full path to the file on disk. Used as the unique identifier for the asset. Defaults to the `FullPath` metadata of the original MSBuild item. |
| `SourceId` | `string` | Identifier of the project or package that owns this asset (e.g., project name or NuGet package ID). Used to group assets by origin and detect conflicts between sources. |
| `SourceType` | `string` | How the asset was discovered. One of: `Discovered` (found on disk in wwwroot), `Computed` (generated during build, e.g., scoped CSS bundles), `Project` (from a referenced project), `Package` (from a NuGet package). |
| `ContentRoot` | `string` | Absolute path to the root directory containing the asset (always ends with a directory separator). For wwwroot assets this is the wwwroot folder; for package assets it is the package's staticwebassets folder. |
| `OriginalItemSpec` | `string` | The original MSBuild item spec before normalization. Used as a fallback when resolving the file on disk. |

#### Path and Routing Properties

| Property | Type | Description |
|----------|------|-------------|
| `BasePath` | `string` | URL prefix under which the asset is served (e.g., `_content/MyLib`). For `Discovered` and `Computed` assets this is ignored when computing the target path since they are relative to the project root. Normalized to forward slashes with no leading/trailing slashes. |
| `RelativePath` | `string` | Path relative to the content root. May contain **token expressions** like `#[.{fingerprint}]?` for fingerprinted file names. Normalized to forward slashes. |

**Path Token Syntax:** The `RelativePath` can embed token expressions using the format `#[.{tokenName}]`. The `#` prefix ensures tokens are never confused with valid file paths. `[]` delimit the expression, `{}` delimit variables, `?` marks the expression as optional (non-fingerprinted on disk), and `!` marks it as optional but preferred (fingerprinted on disk). For example, `app#[.{fingerprint}]!.js` means the file on disk is named `app.<hash>.js` but can also be addressed as `app.js`. Currently the only supported token is `fingerprint` (a base-36 SHA-256 hash).

#### Asset Classification Properties

| Property | Type | Description |
|----------|------|-------------|
| `AssetKind` | `string` | When the asset is relevant. `Build` = build only, `Publish` = publish only, `All` = both build and publish. Defaults to `All` unless `CopyToPublishDirectory` is `Never`, in which case defaults to `Build`. |
| `AssetMode` | `string` | Visibility scope. `All` = visible to current project and references, `CurrentProject` = only the owning project, `Reference` = only referencing projects. |
| `AssetRole` | `string` | Relationship to other assets. `Primary` = standalone asset, `Related` = associated with a primary asset (e.g., `.map` file), `Alternative` = variant of a primary asset (e.g., compressed version). |
| `RelatedAsset` | `string` | Full path to the primary asset this asset is related to. Required when `AssetRole` is `Related` or `Alternative`. |
| `AssetTraitName` | `string` | Name of the trait that distinguishes an alternative asset (e.g., `Content-Encoding`). Required for `Alternative` assets. |
| `AssetTraitValue` | `string` | Value of the distinguishing trait (e.g., `gzip`, `br`). Required for `Alternative` assets. |

#### Merge Behavior Properties

| Property | Type | Description |
|----------|------|-------------|
| `AssetMergeBehavior` | `string` | Controls conflict resolution when merging assets from multiple TFMs or embedded projects. Values: `None` (default), `PreferTarget`, `PreferSource`, `Exclude`. |
| `AssetMergeSource` | `string` | Identifies the source of the asset during merge operations, used alongside `AssetMergeBehavior`. |

#### Copy Behavior Properties

| Property | Type | Description |
|----------|------|-------------|
| `CopyToOutputDirectory` | `string` | Whether to copy to the build output directory. Values: `Never` (default), `PreserveNewest`, `Always`. |
| `CopyToPublishDirectory` | `string` | Whether to copy to the publish output directory. Values: `Never`, `PreserveNewest` (default), `Always`. |

#### Integrity and File Properties

| Property | Type | Description |
|----------|------|-------------|
| `Fingerprint` | `string` | Base-36 encoded SHA-256 hash of the file. Used for cache-busting in URL paths via token expressions. |
| `Integrity` | `string` | Base-64 encoded SHA-256 hash of the file. Used for Sub-Resource Integrity (SRI) in HTML tags and HTTP headers. |
| `FileLength` | `long` | Size of the file in bytes. Used for `Content-Length` headers and up-to-date checks. |
| `LastWriteTime` | `DateTimeOffset` | Last modification time of the file. Used for `Last-Modified` headers and incremental build checks. |

### StaticWebAssetEndpoint

Defined in `Tasks/Data/StaticWebAssetEndpoint.cs`. Represents a URL route through which a static web asset can be served at runtime. A single asset can have multiple endpoints (e.g., a fingerprinted URL and a non-fingerprinted URL). Endpoints carry the response headers, content-negotiation selectors, and metadata properties needed by the runtime to serve the asset correctly.

#### Core Properties

| Property | Type | Description |
|----------|------|-------------|
| `Route` | `string` | URL path as it should be registered in the routing table (used as the MSBuild item identity). For example, `_content/MyLib/app.js` or `_content/MyLib/app.abc123.js` for a fingerprinted variant. |
| `AssetFile` | `string` | Path to the physical file on disk, matching the `StaticWebAsset.Identity`. Links the endpoint back to its underlying asset. |

#### Selectors (Content Negotiation)

| Property | Type | Description |
|----------|------|-------------|
| `Selectors` | `StaticWebAssetEndpointSelector[]` | JSON-serialized array of request conditions that must match for this endpoint to be selected. Used for content negotiation (e.g., selecting a gzip-compressed variant when the client sends `Accept-Encoding: gzip`). |

Each `StaticWebAssetEndpointSelector` has:
- **`Name`** – The request header or attribute to match (e.g., `Content-Encoding`).
- **`Value`** – The expected value (e.g., `gzip`).
- **`Quality`** – Preference weight used when multiple selectors match.

#### Response Headers

| Property | Type | Description |
|----------|------|-------------|
| `ResponseHeaders` | `StaticWebAssetEndpointResponseHeader[]` | JSON-serialized array of HTTP response headers to include when serving this endpoint. Typical headers include `Content-Type`, `Cache-Control`, `ETag`, and `Content-Length`. |

Each `StaticWebAssetEndpointResponseHeader` has:
- **`Name`** – Header name (e.g., `Content-Type`).
- **`Value`** – Header value (e.g., `text/css`).

#### Endpoint Properties (Metadata)

| Property | Type | Description |
|----------|------|-------------|
| `EndpointProperties` | `StaticWebAssetEndpointProperty[]` | JSON-serialized array of key-value metadata pairs associated with the endpoint. Used to carry build-time information (e.g., `fingerprint`, `integrity`, `label`) that the runtime or other tasks can consume. |

Each `StaticWebAssetEndpointProperty` has:
- **`Name`** – Property name (e.g., `integrity`, `fingerprint`, `label`).
- **`Value`** – Property value.

### Relationship Between Assets and Endpoints

```
StaticWebAsset (file on disk)
├── Identity: D:\project\wwwroot\css\app.css
├── RelativePath: css/app#[.{fingerprint}]!.css
├── Fingerprint: abc123
│
├── Endpoint 1 (fingerprinted, preferred)
│   ├── Route: css/app.abc123.css
│   ├── AssetFile: D:\project\wwwroot\css\app.css
│   ├── ResponseHeaders: [Content-Type=text/css, Cache-Control=max-age=31536000,immutable]
│   └── EndpointProperties: [fingerprint=abc123, integrity=sha256-..., label=css/app.css]
│
├── Endpoint 2 (non-fingerprinted)
│   ├── Route: css/app.css
│   ├── AssetFile: D:\project\wwwroot\css\app.css
│   ├── ResponseHeaders: [Content-Type=text/css, ETag="abc123"]
│   └── EndpointProperties: [fingerprint=abc123, integrity=sha256-...]
│
└── Alternative Asset (gzip compressed)
    ├── Identity: D:\project\obj\compressed\css\app.css.gz
    ├── AssetRole: Alternative
    ├── RelatedAsset: D:\project\wwwroot\css\app.css
    ├── AssetTraitName: Content-Encoding
    ├── AssetTraitValue: gzip
    │
    └── Endpoint 3 (compressed variant of Endpoint 1)
        ├── Route: css/app.abc123.css
        ├── AssetFile: D:\project\obj\compressed\css\app.css.gz
        ├── Selectors: [Content-Encoding=gzip]
        └── ResponseHeaders: [Content-Type=text/css, Content-Encoding=gzip, Vary=Content-Encoding]
```

## Development Workflow

For detailed instructions on building, patching, and validating changes, see the [validate-static-web-asset-change skill](../../.claude/skills/validate-static-web-asset-change/SKILL.md).

Do an initial build of the entire repo at the beginning following the instructions on the README.md.

### Inner Loop: Make → Patch → Validate

The primary development workflow is an iterative loop focused on fast feedback with a real project:

1. **Make changes** — Edit task code in `src/StaticWebAssetsSdk/Tasks/` (or targets/props files)
2. **Build the tasks** — `dotnet build` in `src/StaticWebAssetsSdk/Tasks`
3. **Patch the SDK** — Copy the built DLL (and any changed `.targets`/`.props` files) into the redist SDK at `artifacts/bin/redist/{Configuration}/dotnet/sdk/{version}/Sdks/Microsoft.NET.Sdk.StaticWebAssets/`
4. **Test with a real project** — Use the redist `dotnet` to `dotnet build` / `dotnet publish` a sample project and verify the behavior
5. **Inspect results** — Check output assets, manifests (`.staticwebassets.runtime.json`, `.staticwebassets.endpoints.json`), and endpoints. Use binary logs (`-bl`) if needed.
6. **Repeat** — Go back to step 1 until the behavior is correct

**Do not write or run integration tests during the inner loop.** Scoped unit tests (run with `--filter "FullyQualifiedName~YourTestClassName"`) are fine for quick validation of task logic.

### After Validation: Unit Tests

Once the change is validated against a real project, write **unit tests** for the affected tasks. Unit tests live in `test/Microsoft.NET.Sdk.StaticWebAssets.Tests/StaticWebAssets/` and are plain test classes — they do **not** extend `AspNetSdkBaselineTest` or any SDK test base class. They test individual task logic in isolation by constructing task inputs and asserting outputs directly.

```powershell
dotnet test test/Microsoft.NET.Sdk.StaticWebAssets.Tests/Microsoft.NET.Sdk.StaticWebAssets.Tests.csproj --filter "FullyQualifiedName~YourTestClassName"
```

Never run all the tests — that takes a very long time. Always use `--filter` to run only the relevant test class.

### Last: Integration Tests

Integration tests extend `AspNetSdkBaselineTest` (or `IsolatedNuGetPackageFolderAspNetSdkBaselineTest`) and run full MSBuild builds against test project assets. They are slow and should **not** be part of the inner development loop. Write or update integration tests only after the change is validated and unit-tested.

When running integration tests locally, only run the specific tests you wrote or modified — at most the tests from the same class using `--filter "FullyQualifiedName~YourIntegrationTestClassName"`. Leave running the full integration test suite to CI.

**Never modify the system dotnet SDK.** Always use the repo-local redist SDK at `artifacts/bin/redist/{Configuration}/dotnet/` or a freshly downloaded copy for Blazor WASM scenarios.
