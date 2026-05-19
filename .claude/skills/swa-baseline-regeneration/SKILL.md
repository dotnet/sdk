---
name: swa-baseline-regeneration
description: 'Regenerate Static Web Assets test baselines. USE FOR: fixing "generated manifest should match the expected baseline" errors, updating baseline JSON files after legitimate build output changes, understanding the baseline comparison system.'
---

# SWA Baseline Regeneration

Baseline tests compare generated manifests against stored JSON files. When build output legitimately changes, baselines must be regenerated.

## Error Signature

```
Expected collection to be empty because the generated manifest should match
the expected baseline. If the difference in baselines is expected, please
re-generate the baselines.
```

## Baseline File Location

```
test/Microsoft.NET.Sdk.StaticWebAssets.Tests/StaticWebAssetsBaselines/
```

Two types per test:

| Pattern | Content |
|---------|---------|
| `{TestName}.Build.staticwebassets.json` | Full build manifest (assets, endpoints, discovery patterns) |
| `{TestName}.Build.files.json` | Expected files on disk |
| `{TestName}.Publish.staticwebassets.json` | Publish manifest |
| `{TestName}.Publish.files.json` | Publish file list |

Baselines use template variables for path portability:
- `${ProjectPath}` — test project directory
- `${RestorePath}` — NuGet cache path
- `${Tfm}` — target framework moniker

## How Baselines Work

Baselines are **embedded resources** compiled into the test DLL via `Assembly.GetManifestResourceStream()`. The comparison flow:

1. Test runs a full MSBuild build/publish against a test asset project
2. Loads the generated `staticwebassets.build.json` manifest from intermediate output
3. Calls `AssertManifest(actual, LoadBuildManifest())` — templatizes actual paths (`${ProjectPath}`, etc.) and compares against the embedded baseline
4. Calls `AssertBuildAssets(manifest, outputPath, intermediateOutputPath)` — compares file lists

The templatization is done by `StaticWebAssetsBaselineFactory.ToTemplate()` in `AspNetSdkBaselineTest.cs`.

## Regeneration Procedure

```powershell
# 1. Build the test project to embed current baselines
.\artifacts\bin\redist\Debug\dotnet\dotnet.exe build `
  test\Microsoft.NET.Sdk.StaticWebAssets.Tests\Microsoft.NET.Sdk.StaticWebAssets.Tests.csproj `
  -c Debug --no-restore

# 2. Run affected tests with the regeneration flag
$env:ASPNETCORE_TEST_BASELINES = "true"
.\artifacts\bin\redist\Debug\dotnet\dotnet.exe test `
  artifacts\bin\Microsoft.NET.Sdk.StaticWebAssets.Tests\Debug\net11.0\Microsoft.NET.Sdk.StaticWebAssets.Tests.dll `
  --no-build --filter "FullyQualifiedName~AffectedTestClassName"

# 3. Clear the flag
Remove-Item Env:\ASPNETCORE_TEST_BASELINES

# 4. Rebuild the test project to embed the NEW baselines
.\artifacts\bin\redist\Debug\dotnet\dotnet.exe build `
  test\Microsoft.NET.Sdk.StaticWebAssets.Tests\Microsoft.NET.Sdk.StaticWebAssets.Tests.csproj `
  -c Debug --no-restore

# 5. Validate — run the same tests WITHOUT the flag
.\artifacts\bin\redist\Debug\dotnet\dotnet.exe test `
  artifacts\bin\Microsoft.NET.Sdk.StaticWebAssets.Tests\Debug\net11.0\Microsoft.NET.Sdk.StaticWebAssets.Tests.dll `
  --no-build --filter "FullyQualifiedName~AffectedTestClassName"
```

## Critical: Why Steps 4 and 5 Are Mandatory

Step 2 writes new `.json` files to disk in `StaticWebAssetsBaselines/`, but they are **embedded resources**. The test DLL must be rebuilt (step 4) before the tests can read the updated baselines. Skipping step 4 means step 5 still compares against the old embedded baselines and will fail.

## Alternative: Script-Based Regeneration

The repo includes `src/RazorSdk/update-test-baselines.ps1`:

```powershell
# Regenerate:
dotnet test --no-build -c Release -l "console;verbosity=normal" <TestProject> `
  -e ASPNETCORE_TEST_BASELINES=true --filter AspNetCore=BaselineTest

# Validate:
dotnet test --no-build -c Release -l "console;verbosity=normal" <TestProject> `
  --filter AspNetCore=BaselineTest
```

## Test Classes That Use Baselines

| Class | Test Asset |
|-------|-----------|
| `StaticWebAssetsAppWithPackagesIntegrationTest` | `RazorAppWithPackageAndP2PReference` |
| `JsModulesPackagesIntegrationTest` | `RazorAppWithPackageAndP2PReference` |
| `ScopedCssCompatibilityIntegrationTest` | `RazorAppWithPackageAndP2PReference` |
| `ScopedCssPackageReferences` | `RazorAppWithPackageAndP2PReference` |
| `LegacyStaticWebAssetsV1IntegrationTest` | `RazorAppWithPackageAndP2PReference` |
| `StaticWebAssetsIntegrationTest` | Various |
