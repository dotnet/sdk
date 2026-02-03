# Installation of user runtimes

Installation of .NET Runtime archives is quite similar to that of .NET SDK installation.

It differs in a few ways, which we explore below.

## `global.json` handling

The `sdk` paths feature in [`global.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) is, in theory, not meant to inform runtime installation.

Essentially, we could remove `global.json` lookup from the chain of consideration when looking up where to install dotnet. However, we suggest that installing the SDK implies the user wants debugging and other features to work based on that .NET SDK. So, we will utilize the same logic and have `global.jsons` `sdk` feature also direct the location and install lookup of the .NET runtime for `dotnetup`, to at least the extent we control. The muxer itself does not respect this, but it does respect `DOTNET_ROOT`, which we can manipulate; admittedly, this may only be realistic for `dotnetup dotnet` or commands where we control the starting process, and we shouldn't set the entire user environment block to point to a repo specific location.

## Versions

Runtime versions don't have a feature band. The version parsing handled by the [`Microsoft.Deployment.DotNet.Releases`](https://github.com/dotnet/deployment-tools/tree/main/src/Microsoft.Deployment.DotNet.Releases) library (see [`ChannelVersionResolver`](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ChannelVersionResolver.cs)), however, should account for this. Minimal changes are expected.

## Muxer Handling

The .NET Runtime archives also include `dotnet.exe`. The host replacement logic will be the same as for the .NET SDK archives. However, we can assert that the muxer version is the same as the version of the runtime to be installed, which may reduce overhead.

## Options

There are 3 runtime archives produced: the runtime, aspnetcore runtime, and windows desktop runtime (see [`InstallComponent`](../../../src/Installer/Microsoft.Dotnet.Installation/InstallComponent.cs)).

We'll allow install with an option as follows:
- `dotnetup runtime install core` (or `-c`) - Installs the .NET Runtime only
- `dotnetup runtime install aspnetcore` (or `-a`) - Installs the ASP.NET Core Runtime (includes core runtime)
- `dotnetup runtime install windowsdesktop` (or `-w`) - Installs the Windows Desktop Runtime (includes core runtime)

To reduce boilerplate code we will go with the option of one command. Providing no option will install all runtimes.

We will expand this to support multiple tokens, ala `dotnetup runtime install aspnetcore windowsdesktop` to install both in the future.

We will expand this to support `[runtime_type]@version` syntax for explicit or unqualified versions.

## Shared Resources

The .NET SDK install may include the .NET Runtime.

### Manifest Tracking Rules

**Core runtime is tracked in the manifest if and only if:**
1. It was explicitly installed via `dotnetup runtime install core`
2. NOT when it comes bundled with `aspnetcore` or `windowsdesktop` archive installations

This distinction is important because:
- The ASP.NET Core archive includes `Microsoft.NETCore.App` files on disk
- The SDK archive includes `Microsoft.NETCore.App` files on disk
- We don't want to double-track the same runtime from multiple sources

**Example manifest entries:**
```json
{
  "installs": [
    { "component": "SDK", "version": "9.0.100" },
    { "component": "ASPNETCore", "version": "9.0.12" }
  ]
}
```
Note: Core runtime 9.0.12 exists on disk (from ASP.NET Core archive) but is NOT tracked because it wasn't explicitly installed.

### Uninstall Strategy

When uninstalling a runtime component, we must be careful not to delete shared files that other components depend on.

**Uninstall rules for `shared/Microsoft.NETCore.App/{version}`:**

1. **Check manifest for same major.minor version:**
   - Query all manifest entries with the same major.minor (e.g., 9.0.x)
   - Include: SDK, Runtime, ASPNETCore, WindowsDesktop components

2. **SDK version correlation:**
   - SDK 9.0.1xx typically bundles runtime 9.0.x
   - Before deleting core runtime files, check if any SDK with matching major.minor exists
   - If SDK exists, do NOT delete `Microsoft.NETCore.App` files

3. **Delete core runtime only if:**
   - No SDK with same major.minor exists in manifest
   - No other runtime component (ASPNETCore) with same version exists in manifest
   - The core runtime was explicitly uninstalled (installed via `core` option)

**Uninstall rules for `shared/Microsoft.AspNetCore.App/{version}`:**
- Safe to delete if the ASPNETCore component is being uninstalled
- Check if any SDK with same major.minor exists (SDKs may include ASP.NET Core)

**Uninstall rules for `shared/Microsoft.WindowsDesktop.App/{version}`:**
- Safe to delete if the WindowsDesktop component is being uninstalled
- Windows Desktop is standalone (no dependencies from SDK or other runtimes)

**Example uninstall scenarios:**

| Manifest State | Uninstall Command | Files Deleted |
|----------------|-------------------|---------------|
| SDK:9.0.100, ASPNETCore:9.0.12 | `uninstall aspnetcore 9.0` | Only `Microsoft.AspNetCore.App/9.0.12` |
| Runtime:9.0.12, ASPNETCore:9.0.12 | `uninstall aspnetcore 9.0` | Only `Microsoft.AspNetCore.App/9.0.12` |
| Runtime:9.0.12 (only) | `uninstall core 9.0` | `Microsoft.NETCore.App/9.0.12` and host files |
| SDK:9.0.100 (only) | `uninstall sdk 9.0` | SDK files, but NOT runtime files (may break other apps) |

This ensures uninstalling the SDK will not uninstall runtimes that are tracked in the manifest. Runtimes must be explicitly uninstalled via `dotnetup runtime uninstall`.

What we will do is check `shared/{runtime-type}/{runtime-version}` and `host/fxr/{runtime-version}` in the hive location. We could also query the muxer itself (via [`HostFxrWrapper`](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/HostFxrWrapper.cs)) for a more concrete answer as to if the install exists on disk.

Consider performance (folder check vs muxer invocation) vs accuracy (folder faking via rename) in this decision.

## Optional Defaults

The [.NET Install Script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script) installs the .NET Runtime when you install the ASP.NET Core Runtime.
We will do so as well, under the assumption there isn't a use case for a standalone aspnetcore or windowsdesktop runtime without the core runtime.
