---
name: validate-static-web-asset-change
description: Use this skill when you are implementing a change on src\StaticWebAssetsSdk and want to test the behavior locally to validate it works as expected.
---
# Validating Static Web Asset SDK Changes

This document describes how to build, test, and validate changes to the Static Web Assets SDK tasks locally.

## 1. Building the SDK Tasks

To build only the Static Web Assets tasks (fast iteration):

```powershell
cd src/StaticWebAssetsSdk/Tasks
dotnet build
```

The output DLL lands under:
```
artifacts/bin/*/Sdks/Microsoft.NET.Sdk.StaticWebAssets/tasks/*/
```

Use a glob search to find the exact path if needed.

## 2. Determine What Needs to Be Patched

Run `git diff --name-only` (or `git diff --name-only HEAD` for uncommitted changes) and look at which files under `src/StaticWebAssetsSdk/` have changed:

- **`Tasks/**/*.cs`** — The tasks DLL needs to be patched. Build the tasks project first (`dotnet build` in `src/StaticWebAssetsSdk/Tasks`), then copy the resulting `Microsoft.NET.Sdk.StaticWebAssets.Tasks.dll` to the target SDK's matching location under `Sdks/Microsoft.NET.Sdk.StaticWebAssets/tasks/{tfm}/`.
- **`Targets/**/*.targets` or `Targets/**/*.props`** — These files also need to be copied. Copy each changed `.targets` or `.props` file to the matching path under `Sdks/Microsoft.NET.Sdk.StaticWebAssets/targets/` in the target SDK.
- **`Sdk/**/*.props` or `Sdk/**/*.targets`** — Copy to the matching path under `Sdks/Microsoft.NET.Sdk.StaticWebAssets/Sdk/` in the target SDK.

The target SDK location is:
```
{dotnet-root}/sdk/{version}/Sdks/Microsoft.NET.Sdk.StaticWebAssets/
```

## 3. Setting Up a Test SDK

You need an SDK to test your changes against. Do **not** modify your system-installed SDK.

### Option A: Use the Repo Redist SDK (Recommended)

For most projects, use the repo's built-in redist SDK. Build it once from the repo root:

```powershell
.\build.cmd   # Windows
./build.sh    # Linux/macOS
```

This produces a full SDK at `artifacts/bin/redist/{Configuration}/dotnet/`. Set `DOTNET_ROOT` and `PATH` to point to it:

```
DOTNET_ROOT={repo}\artifacts\bin\redist\{Configuration}\dotnet
PATH={repo}\artifacts\bin\redist\{Configuration}\dotnet;{rest-of-PATH}
```

After patching the files there (see Section 2), any `dotnet build` / `dotnet publish` in that shell will use your changes.

### Option B: Download an SDK (Required for Blazor WASM)

The redist SDK does not include workload packs, so **Blazor WebAssembly projects will not work with it**. For WASM scenarios, install a fresh SDK into `artifacts/test-sdk` using the official `dotnet-install` script:

```powershell
# Windows (PowerShell)
& ([scriptblock]::Create((Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1'))) -InstallDir artifacts/test-sdk -Channel 10.0.1xx
```

```bash
# Linux/macOS
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir artifacts/test-sdk --channel 10.0.1xx
```

Adjust `--channel` to match the major version you are working on (e.g., `11.0.1xx`). Then patch and point to it:

```
DOTNET_ROOT={repo}\artifacts\test-sdk
PATH={repo}\artifacts\test-sdk;{rest-of-PATH}
```

## 4. Creating an Isolated Test Project

Sample projects created inside or near the repo must be **isolated** from the repo's MSBuild configuration. Without isolation, the project inherits the repo's Arcade SDK, central package management, custom `global.json` SDK resolution, and internal NuGet feeds — all of which will cause build failures.

Create a folder for your test project and add these sentinel files **before** creating the project itself:

### `Directory.Build.props` (required)

```xml
<Project>
  <!-- Stop MSBuild from walking up to the repo's Directory.Build.props -->
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

MSBuild walks up the directory tree looking for this file. Placing it here stops the walk so the repo root's version (which imports Arcade SDK, enables central package management, etc.) is never found. `ManagePackageVersionsCentrally` must be explicitly set to `false` because the repo enables it globally.

### `Directory.Build.targets` (required)

```xml
<Project>
  <!-- Stop MSBuild from walking up to the repo's Directory.Build.targets -->
</Project>
```

Same traversal-stopping purpose for `.targets`. The repo root's version adds signing, analyzers, and other infrastructure.

### `NuGet.config` (required)

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

The repo's `NuGet.config` references internal Azure DevOps feeds that require authentication. The `<clear />` directive ensures no parent config bleeds through.

### Then create the project

After placing the sentinel files, create your test project in the same folder:

```powershell
dotnet new blazor     # or webapp, classlib, etc.
```

The sentinel files apply to all projects in that folder and its subfolders.

## 5. Testing Your Changes

1. Create or use a sample project that exercises the scenario you changed (e.g., a Blazor app with scoped CSS, a library with static assets, etc.).
2. Build and/or publish the project using the patched SDK:
   ```powershell
   {patched-dotnet} build MyProject.csproj
   {patched-dotnet} publish MyProject.csproj -c Release -o publish
   ```
3. Capture a binary log if you need to debug the build:
   ```powershell
   {patched-dotnet} build MyProject.csproj -bl:build.binlog
   {patched-dotnet} publish MyProject.csproj -bl:publish.binlog
   ```
   If you have an MSBuild binary log MCP server available, use that to inspect the log.

## 6. Quick Validation Checklist

After making a change, verify:

1. **Build the tasks** — `dotnet build` in `src/StaticWebAssetsSdk/Tasks` succeeds
2. **Patch and test locally** — Copy the changed files into a test SDK and build/publish a sample project
3. **Check manifests** — Inspect the generated `.staticwebassets.runtime.json` and `.staticwebassets.endpoints.json` in the output
4. **Check assets** — Verify that the correct assets are included with expected properties (source, content root, relative path, etc.)
5. **Check endpoints** — Verify that routes, response headers, selectors, and endpoint properties are correct
