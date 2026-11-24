---
title: Repository SDK requirements with dotnetup
description: Learn how dotnetup reads global.json and tracks repository SDK requirements.
ms.topic: conceptual
ms.date: 08/07/2026
---

# Repository SDK requirements with dotnetup

`dotnetup` uses `global.json` to associate an SDK requirement with a
repository or directory tree. It searches from the current directory through
each parent directory and reads the nearest existing `global.json`.

## Install from global.json

Run an SDK install command without a channel:

```dotnetcli
dotnetup install
```

If the nearest `global.json` has an `sdk.version`, `dotnetup` derives an
install specification from the version and `rollForward` value. It records
the full path to the file as the specification source. If no `global.json`
exists in the current directory or its parent directories, the command uses
the `latest` channel. Unreadable files, malformed JSON, and invalid SDK settings
are reported as errors. Comments are allowed. `dotnetup` doesn't skip the
nearest file to search parent directories or use the `latest` channel after
an error. If the file parses successfully but doesn't specify an SDK version,
the command uses `latest` without searching parent directories.

## rollForward mapping

`dotnetup` maps `global.json` SDK selection policies to updateable channels:

| `rollForward` value | Dotnetup channel for SDK `10.0.103` |
| --- | --- |
| Omitted, `patch`, or `latestPatch` | `10.0.1xx` |
| `feature` or `latestFeature` | `10.0` |
| `minor` or `latestMinor` | `10` |
| `major` or `latestMajor` | `latest` |
| `disable` | Exactly `10.0.103` |

> **Note:** Dotnetup treats `feature` and `latestFeature` identically: both select
> the latest eligible SDK within the specified major/minor version. Likewise,
> `patch`, `minor`, and `major` behave like their `latest` counterparts. This
> differs from the `dotnet` host's SDK selection rules. Dotnetup does not rewrite
> `rollForward`; the host still applies the original policy when running commands.

Dotnetup always chooses the latest eligible SDK in that scope, never an SDK
older than `sdk.version`. For example, `10.0.103` with an omitted `rollForward`
or `patch` can update to `10.0.105`, but not `10.0.202`. `disable` requires the
exact version currently specified in the file.

`allowPrerelease: false` excludes preview SDKs unless `sdk.version` itself is
a prerelease. When omitted, prerelease SDKs are eligible. This also applies
to repository requirements that map to `latest`; the standalone `latest`
channel continues to select stable releases.

## Installation path from global.json

Dotnetup uses the first non-null, non-whitespace entry in `sdk.paths`. A relative
path is resolved from the directory that contains `global.json`. If the first
meaningful entry is `$host$`, dotnetup uses its default managed installation root.

Installation-path precedence is:

1. `--install-path`.
1. The first meaningful `sdk.paths` entry in the nearest `global.json`, with
   `$host$` selecting the default managed installation root.
1. The default dotnetup-managed .NET installation root.

## Keep repository files current

Pass `--update-global-json` to replace `sdk.version` with the concrete SDK
version that was installed or updated:

```dotnetcli
dotnetup install --update-global-json
dotnetup sdk update --update-global-json
```

Only global.json-sourced SDK specifications are updated by the update
workflow. The modifier preserves the other JSON properties, formatting, and
detected text encoding.

After you change `sdk.version` or `rollForward`, run `dotnetup sdk install`
from the repository directory to apply the new requirement. For a registered
repository, `dotnetup sdk update` also reads the current file before resolving
its SDK requirement.

## Remove a repository requirement

Update `sdk.version` to change a repository's SDK requirement. To remove the
requirement, remove `sdk.version` or delete `global.json` if the file is no longer
needed. Dotnetup picks up these changes during garbage collection and removes
unused installations.

`dotnetup sdk uninstall` removes command-line and migration requirements, not
repository requirements. Use `dotnetup list` to find the `global.json` path.

If `global.json` cannot be read or contains an invalid SDK requirement, dotnetup
reports a warning and uses the last successfully resolved requirement for update
and garbage collection. The saved requirement includes the channel, minimum SDK
version, and prerelease eligibility. `dotnetup list` labels this fallback as
`last known` and shows the read error. Listing does not modify the saved requirement.

Update continues processing all requirements and does not fail solely because a
repository uses this fallback. Even with `--update-global-json`, it does not
rewrite a file that could not be read. A subsequent successful read replaces the
saved requirement. Missing files and files with no SDK version still deactivate
the registration instead of using the fallback.

Older registrations might have only a recorded channel, without the complete
requirement. If such a file cannot be read, update reports a failure for that
repository but continues processing other requirements. Garbage collection skips
that installation root, with a warning, rather than guessing which SDKs are safe
to remove.

## See also

- [Manage repository SDK requirements](../usecases/install-with-global-json.md)
- [`global.json` overview](https://learn.microsoft.com/dotnet/core/tools/global-json)
- [`dotnetup sdk install`](../reference/dotnetup-sdk-install.md)
