---
title: dotnetup sdk uninstall command
description: Command reference for removing tracked .NET SDK requirements.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup sdk uninstall command

## Name

`dotnetup sdk uninstall` - Remove an SDK specification and unused files.

## Synopsis

```console
dotnetup sdk uninstall <CHANNEL> [options]
```

## Arguments

`CHANNEL`

The stored SDK channel or exact version to remove. Matching is
case-insensitive. Matching command-line and migration specifications are removed
together. An SDK remains installed if another requirement still needs it.

Repository requirements are not removed by this command. To change or remove
one, update or delete the corresponding `global.json` file.

## Options

| Option | Description |
| --- | --- |
| `--manifest-path <MANIFEST_PATH>` | Use a custom manifest file. |
| `--install-path <INSTALL_PATH>` | Select the installation root. |
| `-?`, `-h`, `--help` | Show command help. |

## Examples

```dotnetcli
dotnetup sdk uninstall latest
dotnetup sdk uninstall 10.0.1xx
dotnetup sdk uninstall preview --install-path .\.dotnet
```
