---
title: dotnetup overview
description: Learn how dotnetup installs and manages user-level .NET SDKs and runtimes.
ms.topic: overview
ms.date: 08/07/2026
---

# dotnetup overview

`dotnetup` is a cross-platform toolchain manager for user-level .NET
installations. It installs .NET SDKs and runtimes without writing to a
system-managed .NET directory. It also tracks the installation requirements
that you select so that it can update or remove the related files later.

Use `dotnetup` to:

- Install stable, preview, daily, or exact .NET versions.
- Keep multiple SDK and runtime requirements in one managed installation.
- Install the SDK required by the nearest `global.json` file.
- Update tracked channels without changing exact-version requirements.
- Configure how terminals and applications find the managed `dotnet`.
- Run the managed `dotnet` without changing `PATH`.

## Start

Run the interactive setup:

```dotnetcli
dotnetup init
```

The setup asks which SDK channel to install and how you want to access the
managed `dotnet` command.

For a concise, noninteractive setup, install an SDK and configure the current
shell:

```dotnetcli
dotnetup install latest
dotnetup env set shell
dotnetup list
dotnet --version
```

If you do not want to change your shell profile, use the forwarding command:

```dotnetcli
dotnetup install latest
dotnetup env set none
dotnetup dotnet --version
```

## How dotnetup manages .NET

An installation starts with an **install specification**. The specification
identifies a component, such as the .NET SDK, and a channel or exact version.
`dotnetup` resolves that specification to a concrete version and installs it
in a managed **hive**.

The manifest records both the specification and the concrete installation.
More than one specification can refer to the same files. When you uninstall a
specification, `dotnetup` removes files only when no remaining specification
requires them.

For more information, see [How dotnetup works](concepts/how-dotnetup-works.md).

## Choose what to read next

| Goal | Article |
| --- | --- |
| Understand channels, hives, and tracking | [How dotnetup works](concepts/how-dotnetup-works.md) |
| Use a repository's `global.json` | [Manage repository SDK requirements](usecases/install-with-global-json.md) |
| Install SDKs and runtimes | [Install .NET components](usecases/install-components.md) |
| Update or remove installations | [Update installations](usecases/update-installations.md) |
| Configure `PATH` and `DOTNET_ROOT` | [Manage the dotnetup environment](usecases/manage-environment.md) |
| Try preview or daily builds | [Use preview and daily builds](usecases/try-daily-builds.md) |
| Look up command syntax | [dotnetup command reference](reference/dotnetup.md) |

## Availability

This documentation describes the current in-repository implementation of
`dotnetup`. Distribution instructions can differ for each internal release.
After the `dotnetup` executable is available, run `dotnetup --version` to
confirm the version and `dotnetup --help` to see its command surface.
