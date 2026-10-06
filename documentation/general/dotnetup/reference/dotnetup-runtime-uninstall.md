---
title: dotnetup runtime uninstall command
description: Command reference for removing tracked .NET runtime requirements.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup runtime uninstall command

## Name

`dotnetup runtime uninstall` - Remove a runtime specification and unused
files.

## Synopsis

```console
dotnetup runtime uninstall <COMPONENT_SPEC> [options]
```

## Arguments

`COMPONENT_SPEC`

The stored runtime requirement to remove. Use
`component@version-or-channel` to select a component. A value without a
component selects the core .NET runtime.

Uninstall accepts one argument and matches the stored spec name. Use
`runtime@11.0` or `11.0` for the core runtime, not `11.0@core`.

## Options

| Option | Description |
| --- | --- |
| `--source <explicit\|globaljson\|all>` | Remove specifications from the selected source. The default is `explicit`. Runtime specifications are normally explicit. |
| `--manifest-path <MANIFEST_PATH>` | Use a custom manifest file. |
| `--install-path <INSTALL_PATH>` | Select the installation root. |
| `--interactive [true\|false]` | Enable confirmation prompts. Defaults to enabled outside CI when output is not redirected. Redirected input alone does not disable confirmation. |
| `--non-interactive` | Proceed without confirmation, even if `--interactive` is enabled. |
| `-?`, `-h`, `--help` | Show command help. |

## Behavior

Uninstall removes matching specifications, then runs garbage collection for the
whole root, including SDKs and other runtime components. Before changing state,
the command warns if requested installations will remain because of other specs,
or if additional installations will be removed. Either warning requires
confirmation in interactive mode: **Y** or **Enter** proceeds; **N** or **Esc**
cancels without removing specs or files. Non-interactive mode prints the warnings
and proceeds without prompting.
The additional-removal warning says "also" only when files for a requested runtime
will be removed too.
The preview ends by identifying each spec that will be removed from tracking,
including its component and source. If a runtime is retained by other specs,
proceeding removes the requested specs but leaves that runtime installed.

## Examples

```dotnetcli
dotnetup runtime uninstall 10.0
dotnetup runtime uninstall aspnetcore@10.0
dotnetup runtime uninstall windowsdesktop@10.0 --install-path .\.dotnet
```
