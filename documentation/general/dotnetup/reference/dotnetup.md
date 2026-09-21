---
title: dotnetup command
description: Command reference for the dotnetup toolchain manager.
ms.topic: reference
ms.date: 08/07/2026
---

# dotnetup command

## Name

`dotnetup` - Install and manage user-level .NET SDKs and runtimes.

## Synopsis

```console
dotnetup [command] [options]
dotnetup
dotnetup --info [--format <text|json>] [--no-list]
dotnetup self update [--channel <daily|preview|stable>] [--no-progress]
```

## Description

`dotnetup` tracks .NET installation requirements and the concrete SDK or
runtime versions that satisfy them. A bare `dotnetup` invocation runs the SDK
install workflow. When interactive input is available and no configuration
exists, it can start first-use onboarding.

## Commands

| Command | Description |
| --- | --- |
| [`sdk`](dotnetup-sdk.md) | Manage .NET SDK installations. |
| [`runtime`](dotnetup-runtime.md) | Manage .NET runtime installations. |
| [`install`](dotnetup-install.md) | Install a component. |
| [`update`](dotnetup-update.md) | Update all tracked components. |
| [`uninstall`](dotnetup-uninstall.md) | Remove a tracked component. |
| [`list`](dotnetup-list.md) | List tracked specifications and installations. |
| [`init`](dotnetup-init.md) | Run interactive setup. |
| [`env`](dotnetup-env.md) | Manage environment configuration. |
| [`dotnet`](dotnetup-dotnet.md) | Run the dotnetup-managed `dotnet`. |
| [`self update`](#self-update) | Update the dotnetup executable itself from a release channel. |

## Options

| Option | Description |
| --- | --- |
| `--info` | Display the dotnetup version, commit, process architecture, runtime identifier, and verified installation information. |
| `--version` | Display the dotnetup version. |
| `--interactive [<true\|false>]` | Allow a bare invocation to wait for user input. The default is `true` in an interactive terminal and `false` in CI or when output is redirected. |
| `-h`, `/h`, `-?`, `/?`, `--help` | Show root help. |

### --info options

| Option | Description |
| --- | --- |
| `--format <text\|json>` | Select text or JSON output. The default is `text`. |
| `--no-list [<true\|false>]` | Omit tracked specifications and installations. Without this option, `--info` verifies installations. |

## Self update

Update the published NativeAOT dotnetup executable in place:

```console
dotnetup self update
dotnetup self update --channel preview
dotnetup self update --no-progress
```

`--channel <daily|preview|stable>` selects the release channel. The default is
`daily`. The `stable` value is accepted in preparation for that channel becoming
available; until then, it reports that no stable build is available.
`--no-progress` disables progress display, not warnings or the result message.
The command resolves the latest build in the selected channel for the runtime
identifier and
reports success without replacing the executable when the installed full version/RID
already matches or when the available build is older on the same semantic channel.
These no-op results return exit code `0` and write the installed and available versions,
plus the reason no update was applied, to standard error. Dotnetup does not persist the channel used to install an
executable, so omitting `--channel` does not infer `preview` or `daily` from the
running executable.
See [SelfCommandParser](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfCommandParser.cs)
and [SelfUpdateCommand](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfUpdateCommand.cs).

Self-update checks the published SHA-512 hash and embedded full version/RID but is
unsigned, emits an unsigned-source warning, and respects the unsigned-download
policy. The selected release must publish the executable and checksum; no identity
sidecar is needed. After replacement, `--version` provides a bounded startup smoke
check. These unsigned checks do not authenticate freshness or prevent downgrades.
Signed version manifests and monotonic authorization are deferred to future stages. See the
[metadata limitations](../designs/version-metadata.md#scope-and-limitations).

The executable must be in a trusted, writable installation directory. Managed
development hosts reject self-update. Other update callers wait for the current
update within a bounded timeout, but ordinary commands, including `--info`, fail
if the activity gate is busy or their loaded build is stale. Retry those commands
after the update completes. Automation should also retry transient file-not-found
launch failures during Windows replacement before concluding that dotnetup is
missing. See [coordination and recovery](../designs/self-update.md#properties-of-algorithms-1-and-2).

Self-update does not update managed SDK/runtime installations; use
[`dotnetup update`](dotnetup-update.md) for those. For older dotnetup versions or
reinstallation after unrecoverable interruption, use the existing
[installation guidance](https://aka.ms/dotnet/dotnetup).

## Examples

Show help and version information:

```dotnetcli
dotnetup --help
dotnetup --version
dotnetup --info
dotnetup --info --format json --no-list
```

Start first-use installation:

```dotnetcli
dotnetup
```

For predictable automation, select an explicit command and channel:

```dotnetcli
dotnetup install latest --interactive false --no-progress
```
