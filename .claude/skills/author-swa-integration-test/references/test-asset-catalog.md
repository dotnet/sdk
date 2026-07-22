# SWA Test Asset Catalog

Test assets live under `test/TestAssets/TestProjects/`. Use `CreateAspNetSdkTestAsset("AssetName")` to copy one into an isolated temp directory.

## Choosing an Asset

Pick the simplest asset that has the project shape you need. Prefer reusing an existing asset with runtime modifications over creating a new one.

| Need | Asset |
|------|-------|
| Standalone web app with wwwroot files | RazorSimpleMvc |
| Standalone app with scoped CSS / JS modules | RazorComponentApp |
| P2P reference chain (app → RCL) | RazorAppWithP2PReference |
| P2P + NuGet package references | RazorAppWithPackageAndP2PReference |
| Asset groups (`StaticWebAssetGroupDefinition`) | AssetGroupsSample |
| Framework assets (`StaticWebAssetFrameworkPattern`) | FrameworkAssetsSample |
| Fingerprinting / endpoint manipulation | VanillaWasm |
| Cross-targeting (multiple TFMs) | RazorComponentAppMultitarget |
| Blazor WASM standalone | BlazorWasmMinimal |
| Blazor WASM with RCL | BlazorWasmWithLibrary |
| Blazor WASM hosted behind server | BlazorHosted |
| Legacy component library pack | RazorComponentLibrary |

---

## Standalone Apps

### RazorComponentApp
- **SDK**: Microsoft.NET.Sdk.Web
- **Project**: `ComponentApp.csproj`
- **wwwroot**: No — SWA content comes from scoped CSS (`.razor.css`) and JS module generation
- **Used for**: Scoped CSS, JS modules, endpoints, fingerprinting, core SWA build/publish
- **Test classes**: `ScopedCssIntegrationTests`, `JsModulesIntegrationTest`, `StaticWebAssetEndpointsIntegrationTest`, `StaticWebAssetsFingerprintingTest`, `StaticWebAssetsIntegrationTest`

### RazorSimpleMvc
- **SDK**: Microsoft.NET.Sdk.Web
- **Project**: `SimpleMvc.csproj`
- **wwwroot**: `css/site.css`, `js/SimpleMvc.js`, `.well-known/security.txt`, `.not-copied/test.txt`
- **Used for**: Static files in wwwroot, dot-prefixed folder handling
- **Test classes**: `ScopedCssIntegrationTests`

### RazorMvcWithComponents
- **SDK**: Microsoft.NET.Sdk.Web
- **Project**: `MvcWithComponents.csproj`
- **wwwroot**: No
- **Used for**: MVC + Razor component mixed hosting
- **Test classes**: `ScopedCssIntegrationTests`

---

## Multi-Project (P2P)

### RazorAppWithP2PReference
- **SDK**: Web (app) + Razor (libraries)
- **Projects**: `AppWithP2PReference.csproj` → `ClassLibrary.csproj` → transitive chain, plus `AnotherClassLib.csproj`, `ClassLibraryMvc21.csproj` (netstandard2.0 legacy)
- **wwwroot**: In libraries — `AnotherClassLib/wwwroot/` (css, js), `ClassLibrary/wwwroot/` (js with versioned `.v4.js`)
- **Used for**: P2P asset resolution (direct + transitive), compression, design-time, deferred asset groups
- **Test classes**: `DeferredAssetGroupsIntegrationTest`, `StaticWebAssetsCompressionIntegrationTest`, `StaticWebAssetsDesignTimeTest`, `StaticWebAssetsIntegrationTest`
- **Notes**: Best general-purpose P2P asset. Dynamic modification friendly — add items to `ClassLibrary.csproj` via `WithProjectChanges`.

### RazorAppWithPackageAndP2PReference
- **SDK**: Web (app) + Razor (libraries, some packable)
- **Projects**: 5 projects — app, two P2P libraries, two packable libraries (`PackageVersion=1.0.2`)
- **wwwroot**: In all libraries — css and js files
- **Used for**: Combined P2P + NuGet package asset resolution, pack pipeline, scoped CSS, JS modules, V1 manifest compat
- **Test classes**: `JsModulesIntegrationTest`, `LegacyStaticWebAssetsV1IntegrationTest`, `ScopedCssIntegrationTests`, `StaticWebAssetsIntegrationTest`
- **Notes**: Most complex test asset. Requires `IsolatedNuGetPackageFolderAspNetSdkBaselineTest` base class for pack → restore → build tests.

### RazorComponentAppWithReferenceMultitarget
- **SDK**: Web (app, single TFM) + Web/Library (lib, dual TFM)
- **Projects**: `ComponentApp.csproj` → `RazorComponentLibrary.csproj` (multi-target: server + browser)
- **wwwroot**: No
- **Used for**: Single-TFM app consuming multi-TFM library via P2P
- **Notes**: Not currently referenced by SWA tests.

---

## Package / Pack

### AssetGroupsSample
- **SDK**: Razor (lib, packable) + Web (three consumer variants)
- **Projects**: `IdentityUILib.csproj` (packable), `IdentityUIConsumer.csproj`, `IdentityUIConsumerV4.csproj`, `IdentityUIConsumerV5.csproj`
- **wwwroot**: `IdentityUILib/wwwroot/` — `V4/css/site.css`, `V4/js/site.js`, `V5/css/site.css`, `V5/js/site.js`
- **Used for**: `StaticWebAssetGroupDefinition` feature — group selection via `IncludePattern`, `RelativePathPattern`, `ContentRootSuffix`. Has `StaticWebAssets.Groups.targets` for consumer-side group selection.
- **Test classes**: `AssetGroupsIntegrationTest`
- **Notes**: Three consumer projects test default, V4, and V5 group selection. `StaticWebAssetBasePath=Identity`.

### FrameworkAssetsSample
- **SDK**: Razor (lib, packable) + Web (consumer)
- **Projects**: `FrameworkAssetsLib.csproj` (packable), `FrameworkAssetsConsumer.csproj`
- **wwwroot**: `FrameworkAssetsLib/wwwroot/` — `css/site.css`, `js/framework.js`
- **Used for**: `StaticWebAssetFrameworkPattern` — marking JS files as framework-provided
- **Test classes**: `FrameworkAssetsIntegrationTest`

### RazorComponentLibrary
- **SDK**: Microsoft.NET.Sdk.Razor
- **Project**: `ComponentLibrary.csproj`
- **TFM**: netstandard2.0 (legacy)
- **wwwroot**: No
- **Used for**: Legacy Blazor 3.1 component library pack behavior
- **Test classes**: `StaticWebAssetsPackIntegrationTest`

---

## Blazor WASM

### BlazorWasmMinimal
- **SDK**: Microsoft.NET.Sdk.BlazorWebAssembly
- **Project**: `blazorwasm-minimal.csproj`
- **wwwroot**: `index.html`, `css/app.css`
- **Used for**: Minimal WASM baseline, fingerprinting
- **Test classes**: `StaticWebAssetsFingerprintingTest`

### BlazorWasmWithLibrary
- **SDK**: BlazorWebAssembly (app) + Razor (library)
- **Projects**: `blazorwasm.csproj` → `RazorClassLibrary.csproj`, plus satellite assembly lib
- **wwwroot**: In app (index.html, css, service workers) and library (styles.css, js)
- **Used for**: WASM with RCL, service worker manifest, `LinkBase`, content link patterns, endpoints
- **Test classes**: `StaticWebAssetEndpointsIntegrationTest`

### BlazorHosted
- **SDK**: Web (server host) + BlazorWebAssembly (client) + Razor (library)
- **Projects**: 4 projects — server host → WASM client → RCL + satellite lib
- **wwwroot**: Same structure as BlazorWasmWithLibrary
- **Used for**: WASM hosted behind ASP.NET Core server, endpoint generation for hosted apps
- **Test classes**: `StaticWebAssetEndpointsIntegrationTest`
- **Notes**: RCL has `UseStaticWebAssetsV2=true`.

---

## Special-Purpose

### VanillaWasm
- **SDK**: Microsoft.NET.Sdk.WebAssembly (non-Blazor)
- **Project**: `VanillaWasm.csproj`
- **wwwroot**: `index.html`, `main.js`
- **Used for**: Advanced endpoint manipulation (`FilterStaticWebAssetEndpoints`, `UpdateStaticWebAssetEndpoints`), preload properties, fingerprinting
- **Test classes**: `StaticWebAssetsFingerprintingTest`
- **Notes**: Uses the low-level WebAssembly SDK directly. Has custom MSBuild targets that add preload attributes to endpoints.

### RazorComponentAppMultitarget
- **SDK**: Microsoft.NET.Sdk.Web
- **Project**: `RazorComponentAppMultitarget.csproj`
- **TFM**: Dual — `$(AspNetTestTfm);$(AspNetTestTfm)-browser1.0`
- **wwwroot**: No
- **Used for**: Cross-targeting (TargetFrameworks plural), conditional WASM references
- **Test classes**: `StaticWebAssetsCrossTargetingTests`

---

## Feature Coverage Map

| Feature | Primary Asset |
|---------|--------------|
| Scoped CSS | RazorComponentApp |
| JS modules | RazorComponentApp |
| P2P asset resolution | RazorAppWithP2PReference |
| P2P + package resolution | RazorAppWithPackageAndP2PReference |
| Asset groups | AssetGroupsSample |
| Framework assets | FrameworkAssetsSample |
| Compression | RazorAppWithP2PReference |
| Fingerprinting | VanillaWasm, RazorComponentApp |
| Endpoints | BlazorHosted, RazorComponentApp |
| Cross-targeting | RazorComponentAppMultitarget |
| Blazor WASM | BlazorWasmMinimal |
| Hosted WASM | BlazorHosted |
| Pack pipeline | RazorAppWithPackageAndP2PReference |
| Deferred groups | RazorAppWithP2PReference (via dynamic modification) |
