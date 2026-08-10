---
title: dotnetup environment configuration
description: Learn how dotnetup configures PATH and DOTNET_ROOT for managed .NET installations.
ms.topic: conceptual
ms.date: 08/07/2026
---

# dotnetup environment configuration

Installing .NET and making the managed `dotnet` command available are
separate operations. The `dotnetup env` commands control `PATH` and
`DOTNET_ROOT` configuration.

## .NET access modes

| Mode | Behavior |
| --- | --- |
| `none` | Does not add the managed `dotnet` to `PATH` or set `DOTNET_ROOT`. Run .NET with `dotnetup dotnet`. |
| `shell` | Writes a managed block to the selected shell profile. Processes started from that shell use the managed `dotnet`. |
| `everywhere` | Configures the Windows user environment and the shell profile so terminals and other user applications use the managed `dotnet`. Windows only. |

The `dotnetup` executable has a separate `PATH` setting. For example, you can
choose `none` for .NET access but keep `dotnetup` on `PATH`.

## Supported shells

Profile and script generation support:

- Bash
- Z shell
- Fish
- Pwsh (PowerShell Core)
- PowerShell

If you do not pass `--shell`, `dotnetup` detects the current shell. Use an
explicit shell when detection is not available or when you want to update a
different profile:

```dotnetcli
dotnetup env set shell --shell zsh
```

## Stored and observed state

`dotnetup.config.json` stores the selected access mode and whether
`dotnetup` should be on `PATH`. `dotnetup env show` compares that
configuration with the current profile and environment. It reports drift if
the observed state does not match.

Reapply the stored configuration to correct drift:

```dotnetcli
dotnetup env set
```

## Current terminal

Profile and Windows environment changes do not rewrite the environment of the
current process. Open a new terminal, source the modified profile, or evaluate
the generated script.

For Bash or Z shell:

```bash
eval "$(dotnetup env script)"
```

For PowerShell:

```powershell
dotnetup env script --shell pwsh | Invoke-Expression
```

`env script` follows the stored configuration when you do not pass selection
options. Use `--dotnet`, `--dotnetup`, or both to select the generated
content.

## Remove environment configuration

Remove all managed environment wiring:

```dotnetcli
dotnetup env clear
```

This command is equivalent to:

```dotnetcli
dotnetup env set none --dotnetup-on-path false
```

It does not uninstall SDKs or runtimes.
