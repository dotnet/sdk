---
title: dotnetup self update command
description: Command reference for updating the dotnetup executable.
ms.topic: reference
ms.date: 10/08/2026
---

# dotnetup self update command

## Name

`dotnetup self update` - Update the dotnetup executable itself from a release channel.

## Synopsis

```console
dotnetup self update [options]
dotnetup self update --update-notifications <true|false>
```

## Description

The command updates the standalone dotnetup executable in place. It downloads
the latest available build for the selected release channel and the current
runtime identifier.

When the installed version is already current, or when the available build is
older on the same channel, the command reports that no update was applied and
returns exit code `0`.

The executable must be in a trusted, writable installation directory. Self-update
isn't supported when dotnetup runs through the `dotnet` host or from a path that
contains a symbolic link, junction, or other reparse point.

Self-update does not update managed SDK/runtime installations; use
[`dotnetup update`](dotnetup-update.md) for those. For older dotnetup versions or
reinstallation after unrecoverable interruption, use the existing
[installation guidance](https://aka.ms/dotnet/dotnetup).

## Options

| Option | Description |
| --- | --- |
| `--channel <daily\|preview\|stable>` | Select the release channel. By default, stable builds use `stable`, preview builds use `preview`, and other prerelease builds use `daily`. |
| `--force` | Install the latest build from the selected channel even when it isn't newer. This option can reinstall or downgrade dotnetup. |
| `--update-notifications <true\|false>` | Enable or disable update notifications without updating dotnetup. This option can't be combined with `--channel` or `--force`. |
| `--no-progress [<true\|false>]` | Disable progress display. |
| `-?`, `-h`, `--help` | Show command help. |

## Update notifications

During an interactive `dotnetup`, `install`, `sdk install`, `runtime install`, `update`,
`sdk update`, or `runtime update`, dotnetup may write a notice to standard output when a
newer build is available on the running build's channel.

`dotnetup self update --update-notifications false` turns the notice off.
`dotnetup self update --update-notifications true` turns the notice back on.

## Examples

Update dotnetup from its default release channel:

```dotnetcli
dotnetup self update
```

Update from the preview channel:

```dotnetcli
dotnetup self update --channel preview
```

Reinstall or downgrade to the latest build in the preview channel:

```dotnetcli
dotnetup self update --channel preview --force
```

Disable update notifications:

```dotnetcli
dotnetup self update --update-notifications false
```

Update without displaying download progress:

```dotnetcli
dotnetup self update --no-progress
```
