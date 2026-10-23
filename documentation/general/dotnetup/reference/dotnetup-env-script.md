---
title: dotnetup env script command
description: Command reference for generating a dotnetup shell activation script.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup env script command

## Name

`dotnetup env script` - Generate shell code that activates dotnetup settings.

## Synopsis

```console
dotnetup env script [options]
```

## Description

Without selection options, the generated script follows the stored
configuration. If no configuration exists, it includes both the managed
`dotnet` and `dotnetup`.

With selection options, it includes only the requested parts.

In `admin` mode, the command remains available for existing shell profiles but
never activates user-level .NET: it does not add the managed .NET root to `PATH`
or set `DOTNET_ROOT`, even with `--dotnet` or `--dotnet-install-path`. Selection
options and the stored `dotnetup`-on-`PATH` setting still determine whether the
script adds `dotnetup` itself. The hidden `print-env-script` compatibility alias
has the same behavior.

Entering `admin` mode does not remove profile entries or rewrite saved user
settings. The preserved profile can activate the saved setup again after leaving
`admin` mode. Existing `PATH` entries and `DOTNET_ROOT` values are not removed;
restart open terminals and review remaining environment settings if they still
select a user-level .NET installation.

## Options

| Option | Description |
| --- | --- |
| `-s`, `--shell [<bash\|zsh\|fish\|pwsh>]` | Select the script syntax. If omitted, detect the current shell. |
| `-d`, `--dotnet-install-path <PATH>` | Use a specific .NET installation root. The default is the default dotnetup-managed installation root. |
| `--dotnet` | Add the selected .NET root to `PATH` and set `DOTNET_ROOT`. |
| `--dotnetup` | Add the directory that contains `dotnetup` to `PATH`. |
| `-?`, `-h`, `--help` | Show command help. |

## Examples

Activate the stored configuration in Bash:

```bash
eval "$(dotnetup env script --shell bash)"
```

Activate only a repository-local .NET installation in PowerShell:

```powershell
dotnetup env script --shell pwsh --dotnet --dotnet-install-path .\.dotnet |
  Invoke-Expression
```

Generate script text without evaluating it:

```dotnetcli
dotnetup env script --shell fish --dotnet --dotnetup
```
