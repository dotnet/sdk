---
title: dotnetup init command
description: Command reference for interactive dotnetup setup.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup init command

## Name

`dotnetup init` - Configure dotnetup and install .NET through an interactive
setup flow.

## Synopsis

```console
dotnetup init [options]
```

## Description

The setup flow presents the effective install path and starter SDK channel.
It lets you select an access mode and whether the `dotnetup` executable is on
`PATH`. It can also offer to migrate matching native-architecture
system-managed installations. For descriptions of the access modes, see
[dotnetup environment configuration](../concepts/environment.md).

Run this command again to reconfigure dotnetup.

### Admin mode

On Windows, `--mode admin` switches the machine to `admin` mode, in which
`dotnetup sdk install` and `dotnetup runtime install` run the official .NET
installers into Program Files. Switching into or out of `admin` mode changes a
machine-wide setting for all users and asks for administrator permission. While
`admin` mode is active, `dotnetup init` without `--mode` makes no changes; run
`dotnetup init --mode <none|shell|everywhere>` to leave it. For details, see
[dotnetup admin mode](../designs/dotnetup-admin-mode.md).

## Options

| Option | Description |
| --- | --- |
| `--mode <none\|shell\|everywhere\|admin>` | Select the access mode. Without this option, non-interactive runs keep their existing defaults. `admin` is only available on Windows; it switches this machine to `admin` mode and does not install .NET. |
| `--install-path <INSTALL_PATH>` | Select the installation root. |
| `--manifest-path <MANIFEST_PATH>` | Use a custom manifest file. |
| `--no-progress [<true\|false>]` | Disable progress display. |
| `-s`, `--shell [<bash\|zsh\|fish\|pwsh>]` | Select the profile shell. If omitted, detect the current shell. |
| `-v`, `--verbosity <normal\|detailed>` | Set output verbosity. The default is `normal`. |
| `--require-muxer-update [<true\|false>]` | Fail if the command cannot update the `dotnet` executable. By default, installation continues with a warning. |
| `--interactive [<true\|false>]` | Allow the command to wait for input. |
| `-?`, `-h`, `--help` | Show command help. |

## Examples

```dotnetcli
dotnetup init
dotnetup init --shell bash
dotnetup init --install-path .\.dotnet
dotnetup init --interactive false --mode admin
```
