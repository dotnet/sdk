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
Component     Install spec  Source        Installed version
------------  ------------  ------------  -----------------
SDK           10.0          Command line  10.0.105
SDK           10.0.1xx      Migration     10.0.105
SDK           -             -             9.0.304
.NET Runtime  10.0          Command line  10.0.5

3 install specs; 3 installations (2 SDKs, 1 runtime)
```

The Source column identifies a command-line request, a migration, or the
`global.json` file that supplies the requirement. For `global.json`, the
Install spec column shows the file's current SDK version and any explicitly set
`rollForward` and `allowPrerelease` policies.
The matching installed SDK is the latest eligible version under dotnetup's
[repository SDK rules](../concepts/repositories.md#rollforward-mapping).

SDKs appear first, followed by runtimes, with installed versions sorted newest
first in each group. If several specs use the same installation, its version
appears on each row but counts only once in the summary. Runtimes bundled with
an SDK appear separately only if you also installed them separately. A `-` in
the Install spec and Source columns means no displayed spec uses that installation.

`Not installed` means no matching installation was found. If a `global.json`
file cannot be read or parsed, its row shows `Unavailable` and `Unknown`, with
a warning below the table. Deleted files and files that no longer specify an
SDK version are not shown. An `(invalid)` marker next to a version indicates
that installation verification failed; the warning explains why.

The list covers dotnetup-managed installations, not every SDK on your machine.
For `global.json`, matching is limited to the associated dotnetup installation
directory; additional `sdk.paths` locations are not searched. Multiple roots or
architectures have separate labeled tables.

With `--format json`, output contains two arrays:

- `installSpecs`: tracked channels and versions, with their sources.
- `installations`: installed components and their exact versions.

Property names use camel case. Source values are `Explicit` for command-line
requests, `Migration` for migrations, and `GlobalJson` for repository requirements.

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
