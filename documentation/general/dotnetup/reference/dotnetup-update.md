---
title: dotnetup update command
description: Command reference for updating all tracked dotnetup installations.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup update command

## Name

`dotnetup update` - Update all tracked SDK and runtime channels.

## Synopsis

```console
dotnetup update [options]
```

## Description

The command checks every tracked SDK and runtime specification. It installs a
newer matching version when one is available and skips exact-version
specifications. It then removes unreferenced files.

The command continues after an individual update failure. It returns a
failure after it processes the remaining specifications.

If a tracked `global.json` cannot be read or has invalid SDK settings, the command
warns and updates using its last known requirement, including the minimum SDK
version and prerelease eligibility. This fallback does not itself cause a failure
and never rewrites that file, even with `--update-global-json`. If no complete
requirement was saved, that repository update fails and garbage collection is
skipped for its installation root. See
[repository requirements](../concepts/repositories.md#remove-a-repository-requirement).

This command does not update the dotnetup executable itself. Use
[`dotnetup self update`](dotnetup-self-update.md) for that, optionally with
`--no-progress` to disable progress display.

## Options

| Option | Description |
| --- | --- |
| `--update-global-json` | Update applicable global.json-sourced SDK versions. |
| `--manifest-path <MANIFEST_PATH>` | Use a custom manifest file. |
| `--install-path <INSTALL_PATH>` | Update only the matching installation root. Without this option, update all roots in the manifest. |
| `--interactive [<true\|false>]` | Accepted for compatibility. Update does not use this option. |
| `--no-progress [<true\|false>]` | Disable progress display. |
| `-v`, `--verbosity <normal\|detailed>` | Set output verbosity. The default is `normal`. |
| `-?`, `-h`, `--help` | Show command help. |

## Examples

Update all tracked components:

```dotnetcli
dotnetup update
```

Update one installation root and its repository files:

```dotnetcli
dotnetup update --install-path .\.dotnet --update-global-json
```
