# Installation of user runtimes

Installation of .NET Runtime archives is quite similar to that of .NET SDK installation.

It differs in a few ways, which we explore below.

## `global.json` handling

The `sdk` paths feature in [`global.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) is, in theory, not meant to inform runtime installation.

Essentially, we could remove `global.json` lookup from the chain of consideration when looking up where to install dotnet. However, we suggest that installing the SDK implies the user wants debugging and other features to work based on that .NET SDK. So, we will utilize the same logic and have `global.jsons` `sdk` feature also direct the location and install lookup of the .NET runtime for `dotnetup`, to at least the extent we control. The muxer itself does not respect this, but it does respect `DOTNET_ROOT`, which we can manipulate.

## Versions

Runtime versions don't have a feature band. The version parsing handled by the [`Microsoft.Deployment.DotNet.Releases`](https://github.com/dotnet/deployment-tools/tree/main/src/Microsoft.Deployment.DotNet.Releases) library (see [`ChannelVersionResolver`](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ChannelVersionResolver.cs)), however, should account for this. Minimal changes are expected.

## Muxer Handling

The .NET Runtime archives also include `dotnet.exe`. The host replacement logic will be the same as for the .NET SDK archives. However, we can assert that the muxer version is the same as the version of the runtime to be installed, which may reduce overhead.

## Options

There are 3 runtime archives produced: the runtime, aspnetcore runtime, and windows desktop runtime (see [`InstallComponent`](../../../src/Installer/Microsoft.Dotnet.Installation/InstallComponent.cs)).

We'll allow install with an option as follows:
- `dotnetup runtime install aspnetcore` (or `-a`)
- `dotnetup runtime install windowsdesktop` (or `-w`)

To reduce boilerplate code we will go with the option of one command. Providing no option will install all runtimes.

We will expand this to support multiple tokens, ala `dotnetup runtime install aspnetcore windowsdesktop` to install both in the future.

We will expand this to support `[runtime_type]@version` syntax for explicit or unqualified versions.

## Shared Resources

The .NET SDK install may include the .NET Runtime.
We chose to include the runtime install in the [`dotnetup` manifest](../../../src/Installer/dotnetup/DotnetupSharedManifest.cs) as a separate install item only when the runtime is installed individually, and not as part of the SDK install.

This means uninstalling the SDK will uninstall the runtime, but only if the runtime wasn't separately requested. This is essentially a reference count in the manifest as a separate item.

What we will do is check `shared/{runtime-type}/{runtime-version}` and `host/fxr/{runtime-version}` in the hive location. We could also query the muxer itself (via [`HostFxrWrapper`](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/HostFxrWrapper.cs)) for a more concrete answer as to if the install exists on disk.

Consider performance (folder check vs muxer invocation) vs accuracy (folder faking via rename) in this decision.

## Optional Defaults

The [.NET Install Script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script) installs the .NET Runtime when you install the ASP.NET Core Runtime.
We will do so as well, under the assumption there isn't a use case for a standalone aspnetcore or windowsdesktop runtime without the core runtime.
