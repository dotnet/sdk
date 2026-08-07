---
title: dotnetup env set command
description: Command reference for applying dotnetup environment settings.
ms.topic: reference
ms.date: 08/07/2026
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

| Value | Behavior |
| --- | --- |
| `none` | Do not wire the managed `dotnet`. |
| `shell` | Wire the managed `dotnet` through a shell profile. |
| `everywhere` | Wire the managed `dotnet` through the Windows user environment and shell profile. Windows only. |

When you omit `MODE`, the command reapplies the stored mode. The command
fails if no readable configuration exists.

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
