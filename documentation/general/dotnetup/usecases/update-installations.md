---
title: Update tracked .NET installations with dotnetup
description: Update rolling SDK and runtime requirements and remove unused versions.
ms.topic: how-to
ms.date: 08/07/2026
---

# Update tracked .NET installations with dotnetup

`dotnetup` stores the requested channel separately from the resolved version.
An update resolves each rolling channel again.

## Choose an update scope

Use one of these commands:

```dotnetcli
# Update all tracked SDK and runtime requirements.
dotnetup update

# Update SDK requirements only.
dotnetup sdk update

# Update SDK and runtime requirements.
dotnetup sdk update --all

# Update runtime requirements only.
dotnetup runtime update
```

Runtime updates include the core .NET and ASP.NET Core runtimes. On Windows,
they also include Windows Desktop runtime requirements.

## Limit the installation root

By default, an update can process requirements for all roots in the selected
manifest. Use `--install-path` to limit the operation:

```dotnetcli
dotnetup update --install-path D:\tools\dotnet
```

## Update repository files

SDK requirements can use `global.json` as their source. To write the newly
resolved SDK version back to each source file, run:

```dotnetcli
dotnetup sdk update --update-global-json
```

Without this option, dotnetup installs the update but does not edit the
repository file.

## Understand update behavior

- Exact SDK and runtime versions are skipped.
- Each requirement is processed independently.
- Processing continues after an individual failure.
- The command reports failure after it processes the remaining requirements.
- Successful updates run garbage collection.
- An installed version remains if another requirement still needs it.

The `--interactive` option is present in the current root and SDK update help.
The update handler does not use it.

## Review the result

Verify tracked state and on-disk installations:

```dotnetcli
dotnetup list
```

For machine-readable output:

```dotnetcli
dotnetup list --format json
```

## See also

- [dotnetup update](../reference/dotnetup-update.md)
- [dotnetup sdk update](../reference/dotnetup-sdk-update.md)
- [dotnetup runtime update](../reference/dotnetup-runtime-update.md)
