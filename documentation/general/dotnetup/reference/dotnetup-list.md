---
title: dotnetup list command
description: Command reference for listing and verifying dotnetup installations.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup list command

## Name

`dotnetup list` - List tracked .NET requirements and installations.

## Synopsis

```console
dotnetup list [options]
```

## Description

Lists the SDKs and runtimes managed by dotnetup. Each install spec is a requested
channel or version, shown alongside the installed version that satisfies it:

```text
C:\dotnet (x64)
Component     Installed version  Install spec  Source
------------  -----------------  ------------  ---------
.NET SDK      10.0.105           10.0          Command
.NET SDK      10.0.105           10.0.1xx      Command
.NET SDK      9.0.304            -             -
.NET Runtime  10.0.5             10.0          Command

3 install specs; 3 installations (2 SDKs, 1 runtime)
```

The Source column identifies an install command request or the `global.json` file
associated with a repository requirement. The Install spec column shows the
channel or version recorded in the manifest. Matching uses the latest installed
version that satisfies that recorded channel; listing does not reread global.json
or change install, update, uninstall, or garbage-collection behavior.

SDKs appear first, followed by runtimes, with installed versions sorted newest
first in each group. If several specs use the same installation, its version
appears on each row but counts only once in the summary. Runtimes bundled with
an SDK appear separately only if you also installed them separately. A `-` in
the Install spec and Source columns means no displayed spec uses that installation.

`Not installed` means no matching installation was found. An `(invalid)` marker next to a version indicates
that installation verification failed; the warning explains why.

The list covers dotnetup-managed installations, not every SDK on your machine.
For `global.json`, matching is limited to the associated dotnetup installation
directory; additional `sdk.paths` locations are not searched. Multiple roots or
architectures have separate labeled tables. Each table shows its installation
directory and architecture, even when there is only one root.

With `--format json`, output contains two arrays:

- `installSpecs`: tracked channels and versions, with their sources.
- `installations`: installed components and their exact versions.

Property names use camel case. Source values are `Explicit` for command-line
requests and `GlobalJson` for repository requirements.

## Options

| Option | Description |
| --- | --- |
| `--format <text\|json>` | Select text or JSON output. The default is `text`. |
| `--no-verify [<true\|false>]` | Do not validate each recorded installation on disk. |
| `--manifest-path <MANIFEST_PATH>` | Use a custom manifest file. |
| `--install-path <INSTALL_PATH>` | Show only the matching installation root. |
| `-?`, `-h`, `--help` | Show command help. |

## Examples

List and verify all tracked installations:

```dotnetcli
dotnetup list
```

Create JSON output without file validation:

```dotnetcli
dotnetup list --format json --no-verify
```
