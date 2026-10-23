---
title: dotnetup env set command
description: Command reference for applying dotnetup environment settings.
ms.topic: reference
ms.date: 08/11/2026
---

# dotnetup env set command

## Name

`dotnetup env set` - Apply or reapply dotnetup environment settings.

## Synopsis

```console
dotnetup env set [<MODE>] [options]
```

## Arguments

`MODE`

The optional .NET access mode:

| Access mode | Behavior |
| --- | --- |
| `none` | Does not modify these environment variables. Run .NET with `dotnetup dotnet`. |
| `shell` | Modifies the shell profile to set these environment variables. Processes started from that shell use the .NET SDKs and Runtimes installed by dotnetup. |
| `everywhere` | Modifies the system `PATH` and sets the user-level `DOTNET_ROOT` environment variable. Only available on Windows. |
| `admin` | Stores `admin` as the current user's mode, removes dotnetup-managed user-level .NET environment wiring, and runs future installs through the official .NET installers into Program Files, except that a global.json `sdk.paths` directory receives an archive install. Before applying or saving settings, asks for administrator permission to block unsigned downloads if the policy is not already set. Only available on Windows. See [dotnetup admin mode](../designs/dotnetup-admin-mode.md). |

For details about each mode and considerations for `everywhere`, see
[dotnetup environment configuration](../concepts/environment.md).

When you omit `MODE`, the command reapplies the stored mode. The command
fails if no readable configuration exists. Reapplying `admin` mode also
restores its default block on unsigned executable downloads. Selecting another
mode clears that policy first, allowing unsigned downloads by default again.
Cancelled or unsuccessful policy changes leave environment and user configuration
unchanged. This command does not install or uninstall .NET.

The `--dotnetup-on-path` option applies independently in every mode, including
Admin. Unrelated environment entries and installed SDKs and runtimes are preserved.

## Options

| Option | Description |
| --- | --- |
| `--dotnetup-on-path <true\|false>` | Add or remove the directory that contains `dotnetup` from `PATH`. If omitted, preserve the stored value. The first stored configuration defaults to `true`. |
| `-s`, `--shell [<bash\|zsh\|fish\|pwsh>]` | Select the profile shell. If omitted, detect the current shell. |
| `-?`, `-h`, `--help` | Show command help. |

## Examples

Use the managed `dotnet` from Bash:

```dotnetcli
dotnetup env set shell --shell bash
```

Keep only `dotnetup` on `PATH`:

```dotnetcli
dotnetup env set none --dotnetup-on-path true
```

Correct drift by reapplying stored settings:

```dotnetcli
dotnetup env set
```

Configure Admin mode while keeping dotnetup on `PATH`:

```dotnetcli
dotnetup env set admin --dotnetup-on-path true
```
