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
case-insensitive.

Uninstall accepts one argument. It matches the stored spec name, not the
version that another spec currently resolves to. For example, if only `latest`
is tracked, `uninstall 11.0` does not remove that spec even when it selects an
11.0 SDK. A single channel can match multiple installed versions.

## Options

| Option | Description |
| --- | --- |
| `--source <explicit\|globaljson\|all>` | Remove specifications from the selected source. The default is `explicit`. |
| `--manifest-path <MANIFEST_PATH>` | Use a custom manifest file. |
| `--install-path <INSTALL_PATH>` | Select the installation root. |
| `--interactive [true\|false]` | Enable confirmation prompts. Defaults to enabled outside CI when output is not redirected. Redirected input alone does not disable confirmation. |
| `--non-interactive` | Proceed without confirmation, even if `--interactive` is enabled. |
| `-?`, `-h`, `--help` | Show command help. |

## Behavior

Uninstall removes matching specifications, then runs garbage collection for the
whole root. Another specification can keep a requested SDK installed, and
unreferenced installations outside the requested channel can also be removed.
Before changing state, the command lists requested installations that will remain
(and the specs keeping them installed) and additional installations that will be
removed. When either warning applies in interactive mode, press **Y** or **Enter**
to proceed, or **N** or **Esc** to cancel without removing specs or files.
Warnings are still printed in non-interactive mode, but no confirmation is required.
The preview ends by identifying each spec that will be removed from tracking,
including its component and source. Retained versions stay installed after
proceeding. Removing a spec sourced from `global.json` does not delete that file.

## Examples

```dotnetcli
dotnetup sdk uninstall latest
dotnetup sdk uninstall 10.0.1xx --source globaljson
dotnetup sdk uninstall preview --source all --install-path .\.dotnet
```
