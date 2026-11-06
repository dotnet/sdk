# Installation of user runtimes

Installation of .NET Runtime archives is quite similar to that of .NET SDK installation.

It differs in a few ways, which we explore below.

## Command Syntax

The runtime install command follows this syntax:

```bash
dotnetup runtime install <version_or_channel>...
```

### Basic Version Install

When only a version is provided, only the core .NET runtime (`Microsoft.NETCore.App`) is installed:
This runtime has the largest adoption / user base and there's a less distinguishable 'token' identifier.

```bash
dotnetup runtime install 10.0.1    # Installs only .NET runtime 10.0.1
```

```bash
dotnetup runtime install  # Installs latest .NET Core Runtime
```

### Component-Specific Install with `@` Syntax

To install specific runtime components, use the `<component>@<version>` syntax:

```bash
dotnetup runtime install windowsdesktop@10.0.1    # Installs Windows Desktop runtime 10.0.1 (includes core runtime)
dotnetup runtime install aspnetcore@9.0.10        # Installs ASP.NET Core runtime 9.0.10 (includes core runtime)
dotnetup runtime install runtime@10.0.1           # Explicitly installs core runtime 10.0.1
```

### Component Types

| Token | Component | Description |
|-------|-----------|-------------|
| `runtime` | Microsoft.NETCore.App | The core .NET runtime |
| `aspnetcore` | Microsoft.AspNetCore.App | ASP.NET Core runtime (includes core runtime) |
| `windowsdesktop` | Microsoft.WindowsDesktop.App | Windows Desktop runtime for WPF/WinForms (includes core runtime) |

### Multiple Components

Multiple components can be installed in a single command:

```bash
dotnetup runtime install windowsdesktop@10.0.1 aspnetcore@9.0.10
```

This installs both specified components sequentially.

## Versions

Runtime versions don't have a feature band. The version parsing handled by the [`Microsoft.Deployment.DotNet.Releases`](https://github.com/dotnet/deployment-tools/tree/main/src/Microsoft.Deployment.DotNet.Releases) library (see [`ChannelVersionResolver`](https://github.com/dotnet/sdk/blob/release/dnup/src/Installer/Microsoft.Dotnet.Installation/Internal/ChannelVersionResolver.cs)), however, should account for this. Minimal changes are expected.

## Muxer Handling

The .NET Runtime archives also include `dotnet.exe`. The host replacement logic will be the same as for the .NET SDK archives. However, we can assert that the muxer version is the same as the version of the runtime to be installed, which may reduce overhead.

## Runtime Component Selection

There are 3 runtime archives produced: the runtime, aspnetcore runtime, and windows desktop runtime (see [`InstallComponent`](https://github.com/dotnet/sdk/blob/release/dnup/src/Installer/Microsoft.Dotnet.Installation/InstallComponent.cs)).

The component is specified using the `<component>@<version>` syntax described above:

| Command | Effect |
|---------|--------|
| `dotnetup runtime install 10.0.1` | Installs only the core .NET runtime 10.0.1 |
| `dotnetup runtime install runtime@10.0.1` | Explicitly installs the core .NET runtime 10.0.1 |
| `dotnetup runtime install aspnetcore@10.0.1` | Installs ASP.NET Core runtime 10.0.1 (includes core runtime) |
| `dotnetup runtime install windowsdesktop@10.0.1` | Installs Windows Desktop runtime 10.0.1 (does NOT include core runtime) |

**Note:** The `--type` flag is **not** supported. Use the `<component>@<version>` syntax instead.

### Multiple Components

Multiple component specifications can be passed to one command:

```bash
dotnetup runtime install windowsdesktop@10.0.1 aspnetcore@9.0.10
```

## Shared Resources

The .NET SDK install may include the .NET Runtime.

> **Note:** Detailed manifest tracking rules and uninstall strategy will be documented separately in the manifest design document. The rules below are preliminary and subject to change.

### General Principles

1. When installing `aspnetcore` runtime, the core runtime is also installed automatically (as the archives include it)
2. Explicit user actions (install commands) are tracked in the manifest
3. Uninstall operations should not break other installed components

What we will do is check `shared/{runtime-type}/{runtime-version}` and `host/fxr/{runtime-version}` in the hive location. We could also query the muxer itself (via [`HostFxrWrapper`](https://github.com/dotnet/sdk/blob/release/dnup/src/Installer/Microsoft.Dotnet.Installation/Internal/HostFxrWrapper.cs)) for a more concrete answer as to if the install exists on disk.

Consider performance (folder check vs muxer invocation) vs accuracy (folder faking via rename) in this decision.

## Optional Defaults

The [.NET Install Script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script) installs the .NET Runtime when you install the ASP.NET Core Runtime.
We will do so as well, under the assumption there isn't a use case for a standalone aspnetcore or windowsdesktop runtime without the core runtime.
