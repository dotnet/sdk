---
title: How dotnetup works
description: Learn about dotnetup installation roots, components, install specifications, installations, and state files.
ms.topic: conceptual
ms.date: 08/07/2026
---

# How dotnetup works

`dotnetup` tracks what you request separately from the SDK and runtime files
that satisfy each request. Several requests can share the same files, and
`dotnetup` removes files only after no tracked request needs them.

## Installation roots

A **.NET installation root** is a directory where `dotnetup` manages SDKs and
runtimes for one architecture.

The current CLI installs for the architecture of the running `dotnetup`
process. It does not have an architecture option.

The default dotnetup-managed .NET installation root is the `dotnet`
subdirectory of the dotnetup data directory:

| Platform | Default installation root |
| --- | --- |
| Windows | `%LOCALAPPDATA%\dotnetup\dotnet` |
| macOS | `~/Library/Application Support/dotnetup/dotnet` |
| Linux | `$XDG_DATA_HOME/dotnetup/dotnet`, or `~/.local/share/dotnetup/dotnet` when `XDG_DATA_HOME` is not set |

Use `--install-path` to select another installation root. An explicit install path takes
precedence over a path from `global.json`, which takes precedence over the
default installation root.

`dotnetup` does not write to a system-managed .NET directory, such as
`Program Files\dotnet` or `/usr/share/dotnet`.

## Components

`dotnetup` manages these component types:

| Component | Runtime specification name | Installed content |
| --- | --- | --- |
| .NET SDK | Not applicable | SDK, host, runtime, targeting packs, and related SDK content |
| .NET runtime | `runtime` | `Microsoft.NETCore.App` runtime |
| ASP.NET Core runtime | `aspnetcore` | `Microsoft.AspNetCore.App` runtime |
| Windows Desktop runtime | `windowsdesktop` | `Microsoft.WindowsDesktop.App` runtime on Windows |

The ASP.NET Core aliases `aspnet` and the Windows Desktop alias `desktop` are
accepted in runtime component specifications.

## Tracked requirements

A tracked requirement combines a component with a channel or exact version.
For example, an SDK requirement for `10.0.1xx` means "keep the latest SDK in
the 10.0.1xx feature band."

For more information about supported channels and versions, see
[Channels and versions](channels.md).

Requirements can come from:

- A channel or version that you supply on the command line.
- An SDK requirement that `dotnetup` derives from a `global.json` file.

An exact version is pinned, so update commands don't advance it. A channel can
resolve to a newer version during an update.

## Shared installations

Two requirements can resolve to the same installed SDK or runtime. They can
also share host, runtime, and targeting-pack files.

Uninstall commands first remove matching tracked requirements. Cleanup then
keeps the installed versions needed by the remaining requirements and removes
only files that no remaining requirement needs.

## Tracked and untracked installs

By default, an install command records its specification and result in the
manifest. A tracked install can be listed, updated, and removed by
`dotnetup`.

The `--untracked` option installs files without recording them. `dotnetup`
does not list, update, or remove those files. Use this option only when another
process owns the installation. Use a separate root that no tracked dotnetup
installation uses. Cleanup can remove untracked component directories inside
a tracked root.

To prevent accidental mixing, a tracked install fails if the target contains
an existing .NET installation that is not in the selected manifest. Select a
new empty directory, or remove the existing installation if you own it. Use
`--untracked` only with a separate root.

## State files

The dotnetup data directory contains these user-level state files:

| File | Purpose |
| --- | --- |
| `dotnetup_manifest.json` | Tracks installation roots, install specifications, installations, and shared subcomponents. |
| `dotnetup_manifest.json.sha256` | Detects changes to manifest content that dotnetup did not write. |
| `dotnetup.config.json` | Stores the .NET access mode and whether the `dotnetup` directory is on `PATH`. |

Do not edit these files. Use `dotnetup install`, `update`, `uninstall`, and
`env` commands to change the related state.

The `DOTNET_DOTNETUP_DATA_DIR` environment variable changes the data
directory. The `--manifest-path` option changes only the manifest used by one
command. It does not change the configuration file or default installation root.

## Concurrent operations

Install, update, uninstall, list, and cleanup workflows coordinate access to
shared installation state. If another installation-changing command is
running, a command can wait for it to finish. This shared process lock also
applies when commands use different manifests.

## See also

- [Channels and versions](channels.md)
- [Repository SDK requirements](repositories.md)
- [Environment configuration](environment.md)
- [`dotnetup list`](../reference/dotnetup-list.md)
