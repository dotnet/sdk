---
title: When to use dotnetup
description: Compare dotnetup with other ways to install .NET and decide which method fits your scenario.
ms.topic: conceptual
ms.date: 10/09/2026
---

# When to use dotnetup

> [!IMPORTANT]
> `dotnetup` is in public preview. Its features and behavior might change
> before general availability.

You can install .NET in several ways. `dotnetup` installs and manages .NET
SDKs and runtimes for your user account. This article helps you decide whether
`dotnetup` fits your scenario or another installation method is a better
choice.

## Scenarios that fit dotnetup

Consider `dotnetup` when you want to:

- Install .NET without administrator rights and without a system package
  manager.
- Keep several SDK and runtime versions side by side and update them together.
- Follow a channel, such as `lts`, `10.0`, or `10.0.1xx`, so updates move to
  the newest matching version.
- Install the SDK that a repository's `global.json` file requires.
- Try preview or daily builds without changing the machine-wide installation.
- Use the same commands on Windows, macOS, and Linux.

## Scenarios that fit other methods

Another installation method might be a better choice when you:

- **Need a supported, generally available installation method.** For production
  machines, use the .NET installers, a package manager, or Visual Studio.
- **Develop with Visual Studio.** Visual Studio installs and services its own
  machine-wide copy of .NET. For more information, see
  [Install .NET on Windows](https://learn.microsoft.com/dotnet/core/install/windows).
- **Need machine-wide .NET installations.** On Windows, use Microsoft Update
  or update the package with WinGet. On Linux, use your distribution's package
  manager. On macOS, run the .NET installer again to install a newer version.
- **Manage and remove .NET installations across an organization's Windows
  devices.** Use the
  [.NET Install Manager](https://learn.microsoft.com/dotnet/core/additional-tools/dnim-overview).

## Compare installation methods

| Capability | dotnetup | .NET installers and package managers | dotnet-install scripts |
| --- | --- | --- | --- |
| Installation scope | User | Usually machine | Any folder that you choose |
| Requires administrator rights | No, except to change the system `PATH` on Windows in `everywhere` mode | Depends on the platform and method | No |
| Tracks installations | Yes | Depends on the platform and method | No |
| Updates installations | Yes, with `dotnetup update` | Depends on the platform and method | No. Run the script again. |
| Removes unused installations | Yes | Depends on the platform and method | No |
| Reads `global.json` | Yes | No | Yes, with the `--jsonfile` option |
| Installs daily builds | Yes | No | Yes |
| Support status | Public preview | Generally available | Generally available |

## Use dotnetup with other installations

`dotnetup` doesn't remove or change machine-wide installations. How the two
installations interact depends on the access mode that you choose:

- In `none` and `shell` modes, processes that don't use the dotnetup
  environment configuration continue to use the machine-wide installation.
- In `everywhere` mode on Windows, the dotnetup-managed installation takes
  precedence over the machine-wide installation. Migrate the machine-wide SDKs
  and runtimes that you need into the dotnetup-managed installation.

For more information, see
[dotnetup environment configuration](concepts/environment.md).

## Next steps

- [Get started with dotnetup](README.md)
- [How dotnetup works](concepts/how-dotnetup-works.md)
