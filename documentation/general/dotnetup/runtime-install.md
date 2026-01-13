# Installation of user runtimes

Installation of .NET Runtime archives is quite similar to that of .NET SDK installation.

It differs in a few ways, which we explore below.

## `global.json` handling

The `sdk` paths feature in [`global.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) is not meant to inform runtime installation.
Therefore, the lookup path and priority list for where `dotnetup` installs the runtime will be slightly different.

Essentially, we just remove `global.json` lookup from the chain of consideration.

## Versions

Runtime versions don't have a feature band. The version parsing handled by the [`Microsoft.Deployment.DotNet.Releases`](https://github.com/dotnet/deployment-tools/tree/main/src/Microsoft.Deployment.DotNet.Releases) library (see [`ChannelVersionResolver`](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ChannelVersionResolver.cs)), however, should account for this. Minimal changes are expected.

## Muxer Handling

The .NET Runtime archives also include `dotnet.exe`. The host replacement logic will be the same as for the .NET SDK archives. However, we can assert that the muxer version is the same as the version of the runtime to be installed, which may reduce overhead.

## Options

There are 3 runtime archives produced: the runtime, aspnetcore runtime, and windows desktop runtime (see [`InstallComponent`](../../../src/Installer/Microsoft.Dotnet.Installation/InstallComponent.cs)).

We could have separate commands:
- `dotnetup runtime install`
- `dotnetup aspnetcoreruntime install`

Or, one command with options:
- `dotnetup runtime install --aspnetcore` (or `-a`)
- `dotnetup runtime install --windowsdesktop` (or `-w`)

To reduce boilerplate code we will go with the option of one command.

## Shared Resources

The .NET SDK install may include the .NET Runtime.
We could decide to include the runtime install in the [`dotnetup` manifest](../../../src/Installer/dotnetup/DotnetupSharedManifest.cs) as a separate install item.
However, to maintain parity with the actions the user has taken, we will avoid doing so. This means uninstalling the SDK will uninstall the runtime, but if the runtime was requested to be installed, it will be ref counted in the manifest as a separate item, so we wouldn't remove it.

What we will do is check `shared/{runtime-type}/{runtime-version}` and `host/fxr/{runtime-version}` in the hive location. We could also query the muxer itself (via [`HostFxrWrapper`](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/HostFxrWrapper.cs)) for a more concrete answer as to if the install exists on disk.

Consider performance (folder check vs muxer invocation) vs accuracy (folder faking via rename) in this decision.

## Optional Defaults

The [.NET Install Script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script) installs the .NET Runtime when you install the ASP.NET Core Runtime.
We will not do this as to allow more granular installations.
