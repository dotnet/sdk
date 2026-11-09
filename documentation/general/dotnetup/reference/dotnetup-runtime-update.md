---
title: dotnetup runtime update command
description: Command reference for updating tracked .NET runtime requirements.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup runtime update command

## Name

`dotnetup runtime update` - Update tracked .NET runtime channels.

## Synopsis

```console
dotnetup runtime update [options]
```

## Description

The command processes the core .NET and ASP.NET Core runtime components. On
Windows, it also processes the Windows Desktop runtime. It skips exact-version
specifications.

The command tries each supported runtime component even if an earlier
component fails.

## Options

| Option | Description |
| --- | --- |
| `--manifest-path <MANIFEST_PATH>` | Use a custom manifest file. |
| `--install-path <INSTALL_PATH>` | Update only the matching installation root. |
| `--no-progress [<true\|false>]` | Disable progress display. Same as `--progress none`. |
| `--progress <auto\|bars\|lines\|none\|json>` | Choose the progress display. The default, `auto`, uses live bars on a terminal and plain lines when output is redirected or in CI. `json` writes [progress events](../usecases/automation.md#progress-events) to stdout and other output to stderr. |
| `-v`, `--verbosity <normal\|detailed>` | Set output verbosity. The default is `normal`. |
| `-?`, `-h`, `--help` | Show command help. |

## Examples

```dotnetcli
dotnetup runtime update
dotnetup runtime update --install-path .\.dotnet --no-progress
```
