---
title: Configure access to dotnetup-managed .NET
description: Configure shell profiles, user environment variables, or command forwarding.
ms.topic: how-to
ms.date: 08/07/2026
---

# Configure access to dotnetup-managed .NET

Installation and environment configuration are separate operations. Select
the access mode that fits your workflow.

## Use command forwarding

The least persistent option is the `none` mode. In this mode, no changes are made to your shell profile or user environment. Instead, `dotnetup dotnet` allows you to run `dotnet` commands via the `dotnetup`-managed installation:

```dotnetcli
dotnetup env set none --dotnetup-on-path true
dotnetup dotnet -- --info
dotnetup dotnet build
```

The managed `dotnet` is not added to your shell `PATH`.

## Configure a shell profile

Use the `shell` mode to write a managed block to your shell profile:

```dotnetcli
dotnetup env set shell --shell pwsh
```

Supported shell values are `bash`, `zsh`, `fish`, and `pwsh`.

Open a new shell after the command finishes, or activate the current shell to apply the changes:

```powershell
dotnetup env script --shell pwsh | Invoke-Expression
```

## Configure the Windows user environment

On Windows, the `everywhere` mode updates the persistent user environment
and the selected shell profile:

```dotnetcli
dotnetup env set everywhere --shell pwsh
```

This mode is not available on Linux or macOS.

## Inspect and correct drift

Show stored settings and compare them with the current environment:

```dotnetcli
dotnetup env show
```

Reapply stored settings:

```dotnetcli
dotnetup env set
```

## Remove environment changes

```dotnetcli
dotnetup env clear
```

This command removes dotnetup environment wiring. It does not uninstall .NET.

## See also

- [Environment configuration](../concepts/environment.md)
- [dotnetup env](../reference/dotnetup-env.md)
