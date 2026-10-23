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
It lets you select an access mode and preserves the configured setting for whether
the `dotnetup` executable is on `PATH` (defaulting to `true` on first setup).
It can also offer to migrate matching native-architecture
system-managed installations. For descriptions of the access modes, see
[dotnetup environment configuration](../concepts/environment.md).

Run this command again to reconfigure dotnetup.

On Windows, the interactive form can select admin mode. Highlighting Admin already
previews configuration-only setup: the SDK-channel and migration fields are hidden,
and the confirmation offers to configure rather than install. Moving the highlight
back or cancelling the edit restores the previous form without changing its selections.
Hidden install-only content leaves blank rows so the other prompts stay in place
while browsing access modes. The form preserves header and section spacing before
removing optional descriptions when adapting to smaller terminals.
Accepting Admin configures the
unsigned-download policy, applies environment settings, saves Admin in the
per-user configuration, and exits without installing .NET. Init also only
configures when changing from saved Admin to another mode. Install afterwards
with an explicit command; Admin installs require
`dotnetup sdk install <version> --untracked`.

Admin mode can also be selected separately with `dotnetup env set admin`.
There is no `--mode` option. For details, see
[dotnetup admin mode](../designs/dotnetup-admin-mode.md).

## Options

| Option | Description |
| --- | --- |
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
```
