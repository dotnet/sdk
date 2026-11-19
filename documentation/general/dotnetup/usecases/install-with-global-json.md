---
title: Install an SDK for a repository with dotnetup
description: Use global.json to install and track the SDK requirement for a repository.
ms.topic: how-to
ms.date: 08/07/2026
---

# Install an SDK for a repository with dotnetup

When you do not supply an SDK channel, `dotnetup sdk install` searches from
the current directory toward the file system root. It uses the nearest
existing `global.json` file. If that file is unreadable, malformed, or contains
invalid SDK settings, the command fails instead of searching a parent directory.
Comments are allowed. If the file parses but doesn't specify an SDK version,
or if no file exists, it uses `latest` without searching past the nearest file.

## Install the repository requirement

From the repository directory, run:

```dotnetcli
dotnetup sdk install
```

For this `global.json` file, dotnetup tracks the `10.0.1xx` feature band:

```json
{
  "sdk": {
    "version": "10.0.103",
    "rollForward": "latestPatch"
  }
}
```

The `global.json` path is the source of the stored requirement. You can see
the source and installed version with:

```dotnetcli
dotnetup list
```

## Understand roll-forward mapping

For an SDK version such as `10.0.103`, dotnetup maps `rollForward` as follows:

| `rollForward` value | Dotnetup channel |
| --- | --- |
| Omitted, `patch`, or `latestPatch` | `10.0.1xx` |
| `feature` or `latestFeature` | `10.0` |
| `minor` or `latestMinor` | `10` |
| `major` or `latestMajor` | `latest` |
| `disable` | Exact version `10.0.103` |

Dotnetup selects the latest eligible SDK in that scope, with `sdk.version` as
the minimum and `allowPrerelease` controlling preview eligibility. `disable`
requires the exact version currently in the file. Install and update read the
file again, so editing its version or policy changes the requirement.

The non-`latest` policies use their `latest` counterparts for dotnetup management;
the .NET host's selection rules are unchanged. See
[repository SDK rules](../concepts/repositories.md#rollforward-mapping).

## Use `sdk.paths`

If `global.json` contains `sdk.paths`, dotnetup uses the first non-null,
non-whitespace entry when no `--install-path` option is present. It resolves
a relative value from the directory that contains `global.json`.

The install-path precedence is:

1. `--install-path`
2. The first meaningful `sdk.paths` entry
3. The default dotnetup-managed .NET installation root

### The `$host$` sentinel

`$host$` is **not** a literal directory. It selects the default dotnetup-managed
.NET installation root. Dotnetup skips empty, null, or whitespace entries while
looking for the first meaningful entry.

| First meaningful `sdk.paths` entry | Where dotnetup installs |
| --- | --- |
| A relative or absolute path (for example, `.dotnet`) | That path, resolved relative to the directory containing `global.json` |
| `$host$` | The default dotnetup-managed .NET installation root |
| No usable entry | The default dotnetup-managed .NET installation root |

Because `sdk.paths` is ordered, the first meaningful entry wins.
`["$host$", ".dotnet"]` installs to the default installation root, while
`[".dotnet", "$host$"]` installs to `.dotnet`.

## Update `global.json`

After you change `sdk.version` or `rollForward`, run `dotnetup sdk install`
from the repository directory to apply the new requirement. For a registered
repository, `dotnetup sdk update` also reads the current file before resolving
its SDK requirement.

To install the newest version in the derived channel and write that version
back to `global.json`, run:

```dotnetcli
dotnetup sdk install --update-global-json
```

Later, update all tracked repository requirements and their files with:

```dotnetcli
dotnetup sdk update --update-global-json
```

The update changes only `sdk.version`. It preserves the existing formatting,
other properties, and detected text encoding.

## Remove a repository requirement

Remove `sdk.version` from `global.json`, or delete the file if it is no longer
needed. Dotnetup picks up the removal during garbage collection and removes
files only when no remaining requirement needs them.

To use a different SDK instead, update `sdk.version` and run
`dotnetup sdk install` from the repository directory.

`dotnetup sdk uninstall` does not remove repository requirements.

## See also

- [Repository and global.json integration](../concepts/repositories.md)
- [dotnetup sdk install](../reference/dotnetup-sdk-install.md)
- [Update tracked installations](update-installations.md)
