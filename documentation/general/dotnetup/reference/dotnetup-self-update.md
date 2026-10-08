---
title: dotnetup self update command
description: Command reference for updating the dotnetup executable.
ms.topic: reference
ms.date: 10/08/2026
---

# dotnetup self update command

## Name

`dotnetup self update` - Update the dotnetup executable itself from a release channel.

## Synopsis

```console
dotnetup self update [--channel <daily|preview|stable>] [--force] [--no-progress]
dotnetup self update --update-notifications <true|false>
```

## Description

Update the published NativeAOT dotnetup executable in place.

`--channel <daily|preview|stable>` selects the release channel. When omitted, the
channel is derived from the running build's prerelease label: a stable build uses
`stable`, a `preview`-labeled build uses `preview`, and any other prerelease build uses
`daily`. Official daily builds currently carry the same `preview` label as preview builds,
so they also default to `preview`. When that default finds an older build than the one
running, the command suggests `--channel daily`. Pass `--channel` explicitly to switch channels. The `stable` value is accepted in preparation for that channel becoming
available; until then, it reports that no stable build is available.
`--no-progress` disables progress display, not warnings or the result message.
The command resolves the latest build in the selected channel for the runtime
identifier and
reports success without replacing the executable when the installed version
already matches or when the available build is older on the same semantic channel.
These no-op results return exit code `0` and write the installed and available versions,
plus the reason no update was applied, to standard error.
`--force` installs the selected channel's latest build even when it is not newer than
the installed version, which can downgrade or reinstall dotnetup. A forced update writes a
warning before downloading and uses the same download, hash, unsigned-download policy,
replacement, and verification steps as any other update.
See [SelfCommandParser](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfCommandParser.cs)
and [SelfUpdateCommand](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfUpdateCommand.cs).

See the [verification limitations](../designs/self-update/self-update-verification.md).

The executable must be in a trusted, writable installation directory and must be named
`dotnetup` (`dotnetup.exe` on Windows) for self-update. Renamed executables can run
other commands but cannot update themselves. Commands fail with a specific error when
the executable path contains a symbolic link, junction, or other reparse point, or when
dotnetup cannot create its lock files in the installation directory. Running via the
`dotnet` host rejects self-update; supported updates replace the published standalone
executable, not a managed application's collection of files. Other self-update callers wait for the
current update within a bounded timeout. While waiting, the command reports whether another
self-update or another dotnetup command is still running. Ordinary commands, including
`--info`, fail if the activity gate is busy or their loaded build is stale. Retry those
commands after the update completes. Automation should also retry transient file-not-found
launch failures during Windows replacement before concluding that dotnetup is
missing. See [coordination and recovery](../designs/self-update/self-update-algorithm-implementations.md#properties-of-algorithms-1-and-2).

Self-update does not update managed SDK/runtime installations; use
[`dotnetup update`](dotnetup-update.md) for those. For older dotnetup versions or
reinstallation after unrecoverable interruption, use the existing
[installation guidance](https://aka.ms/dotnet/dotnetup).

## Update notifications

During an interactive `dotnetup`, `install`, `sdk install`, `runtime install`, `update`,
`sdk update`, or `runtime update`, dotnetup may write a notice to standard output when a
newer build is available on the running build's channel.

`dotnetup self update --update-notifications false` turns the notice off by setting
`updateNotifications` to `false` in `dotnetup.config.json`. It does not update dotnetup
and cannot be combined with `--channel` or `--force`.
`dotnetup self update --update-notifications true` turns the notice back on.

## Examples

```console
dotnetup self update
dotnetup self update --channel preview
dotnetup self update --channel preview --force
dotnetup self update --no-progress
```
