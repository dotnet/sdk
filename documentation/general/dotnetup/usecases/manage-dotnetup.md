---
title: Update or remove dotnetup
description: Check the dotnetup version, update the tool, or safely remove the tool and its managed installations.
ms.topic: how-to
ms.date: 10/09/2026
---

# Update or remove dotnetup

This article describes how to manage the `dotnetup` tool itself. To update or
remove the .NET SDKs and runtimes that `dotnetup` manages, see
[Update installations](update-installations.md).

## Check the dotnetup version

Show the version of `dotnetup`:

```dotnetcli
dotnetup --version
```

To also show the commit, architecture, runtime identifier, and verified
installations, run:

```dotnetcli
dotnetup --info
```

## Update dotnetup

`dotnetup` doesn't have a command that updates itself. Run the download script
again to replace the existing executable.

On macOS or Linux, run:

```bash
curl -fsSL https://aka.ms/dotnetup/get-dotnetup.sh | bash
```

On Windows, run:

```powershell
irm https://aka.ms/dotnetup/get-dotnetup.ps1 | iex
```

The script installs the latest `preview` build by default. If you use the
`daily` build or a custom installation directory, pass the same options that
you used for the first installation.

An update of `dotnetup` doesn't change your .NET installations, environment
configuration, or tracked installation requirements.

## Remove dotnetup

Back up important files before removing `dotnetup`. Installation roots, data
directories, manifest locations, and executable directories can be customized
and might contain files that another tool or repository owns. Don't delete an
entire directory unless you have verified that it is dedicated to `dotnetup`.

### 1. Inventory manifests and installation roots

List the roots in the default manifest:

```dotnetcli
dotnetup list
```

If you used `--manifest-path`, repeat the command for each custom manifest:

```dotnetcli
dotnetup list --manifest-path <MANIFEST_PATH>
```

Record each manifest, checksum file, and installation root. An unqualified
`dotnetup list` doesn't show roots tracked by custom manifests.

### 2. Remove environment configuration

Remove the changes that `dotnetup` made to each configured shell profile:

```dotnetcli
dotnetup env clear --shell <SHELL>
```

Repeat the command for every shell that you configured. If `everywhere` mode
is enabled on Windows, removing the managed root from the system `PATH`
requires elevation even when you are switching to `shell` or `none`.

If you added the executable directory to `PATH` yourself, remove only that
entry. Keep entries that other tools use.

### 3. Back up and remove dotnetup-owned state

Back up each manifest and its `.sha256` checksum before deleting state. Then:

- Remove only installation-root content that you have confirmed `dotnetup`
  owns. Don't delete a shared repository, tools, or application directory.
- Remove each custom manifest and checksum that you no longer need.
- Remove the dotnetup data directory after you have accounted for every root
  and manifest that it contains.

| Operating system | Default data directory |
| --- | --- |
| Windows | `%LOCALAPPDATA%\dotnetup` |
| macOS | `~/Library/Application Support/dotnetup` |
| Linux | `$XDG_DATA_HOME/dotnetup`, or `~/.local/share/dotnetup` when `XDG_DATA_HOME` isn't set |

If you set `DOTNET_DOTNETUP_DATA_DIR`, use that directory instead and remove
the environment variable when it is no longer needed.

### 4. Remove the executable

Delete the `dotnetup` executable from its installation directory. The download
script uses `~/.dotnetup` by default. If that directory contains unrelated
files, remove only the `dotnetup` files.

Open a new terminal to use the updated environment.

## See also

- [How dotnetup works](../concepts/how-dotnetup-works.md)
- [dotnetup environment configuration](../concepts/environment.md)
- [Get started with dotnetup](../README.md)
