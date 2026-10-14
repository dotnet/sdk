# `dotnetup` Admin Mode

`admin` mode with `dotnetup` enables management of installs via `.exe bundles` (via `msis`), as opposed to `.tar.gz` and `.zip` install of `.NET.`

`admin` mode is only supported on Windows. Attempting to set `admin` mode on any other platform will fail.

`admin` mode is a side scenario for `dotnetup`: the primary and default experience will be to use the default mode (`shell` on Unix or `everywhere` mode on Windows.) Under these modes, install times will be faster, less disk space will be consumed, and `dotnetup` will be able to provide more coherent and enforced management of the installs.

### Why Admin Mode

Tools that manage install policy and integrate into IT Systems for .NET management such as [DNIM](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dnim-overview), MU (Microsoft Update), and others, exist today. `dotnetup` is not meant to replace or align with those tools. `dotnetup` remains a developer focused tool focused on archive based installs.

However, we recognize that some users may need Visual Studio integration alongside VS Code, and Visual Studio uses `admin` type installers. Some users may depend on updates to the .NET runtime used by applications across the system that must be managed outside of `dotnetup`. This is the decision that C#DK and other extensions made, to prefer smoother integration with VS at the cost of install reliability, speed, and disk space.

Users also may want to manage their 'admin' controlled installs from a `CLI` that matches their experience using `dotnetup`.

## Mode Semantics

`admin` was preferred over `system` because it outlines the differentiation; user local paths may be set to take precedence on the system path, so dotnetup can manage system-level installs without needing `admin` mode per se.

Of course, `elevation` may be required to modify the system install `PATH`, but that's not necessarily always the case.

### Mode Communication

The shorthand form will say:
```
dotnetup will install .NET SDKs and runtimes in C:\Users\noahgilson\AppData\Local\dotnetup\dotnet.

  SDK Channel              latest
    Determines which version of .NET to install and how it stays updated — 'latest', 'lts', 'preview', 'daily', or a
    version like '10.0'.

  Access mode
    Controls where the dotnet you install is available. Change it later with 'dotnetup env set'.
    None
      Run .NET with 'dotnetup dotnet'. dotnet isn't added to your PATH, so your existing installs are unaffected.
    Shell
      Configure your shell profile so shell sessions use dotnetup's .NET installs.
    Everywhere  (default)
      Modify the system PATH so all apps use dotnetup's .NET installs (requires elevation).
    Admin
      Installs are managed by other tools alongside `dotnetup` in a shared system folder.
      Installation is slower and consumes more disk space but supports external central IT management.

```

If the 'system' path is already pointed towards somewhere besides an expected program files location `dotnetup` will fail.

The `PATH` will not be managed by `dotnetup` as the[ MSI installer already does this](https://github.com/dotnet/runtime/blob/ede6e05426a1fb94637c8c04cb26a6230bf5ea59/src/installer/pkg/sfx/installers/host.wxs#L47-L73), provided the installs match the native architecture.

#### Required & Expected Installation Paths

`PR`: `Environment.SpecialFolder.ProgramFiles`
`PRx86` : `Environment.SpecialFolder.ProgramFilesX86`

| Windows architecture | Installer payload | Default installation root |
| --- | --- | --- |
| x64 | x64 | `PR\dotnet` |
| x64 | x86 | `PRx86\dotnet` |
| Arm64 | Arm64 | `PR\dotnet` |
| Arm64 | x64 | `PR\dotnet\x64` |

Migrating system installs is meaningless and so it will always be 'No.'

## dotnetup `init`

`init` gets a `--mode <MODE>` option that accepts `none`, `shell`, `everywhere`, or `admin`, depending upon supported OS context. `--mode` is optional, including with `--interactive false`; without it, non-interactive `init` keeps its existing defaults.

`init --mode admin` only switches the machine to `admin` mode. It does not install .NET or configure the environment. While `admin` mode is active, `init` without `--mode` makes no changes and explains how to leave `admin` mode; `init --mode <other mode>` disables `admin` mode first, then runs the normal setup with that mode.

Defaults remain the same for other options, with migration being true or false depending on the pre-existing default. (`admin` mode will never `migrate`.)

Callers such as C#DK that want integration with VS will need to call `dotnetup init --interactive false --mode admin`, then install with `dotnetup sdk install <version> --untracked`.

## Bifurcated Behavior

`dotnetup` will only behave in one mode at a time. We don't want users to be switching back and forth. A command line argument across all commands such as `sdk install` would encourage this behavior, but considering the PATH can only point to one main directory, this split is something we want to direct users away from.

`dotnetup sdk install` for example, will only install to the user folder using archives if the mode is not `admin`. If the mode is `admin`, it will only ever install via `msi`s.

`dotnetup --info`, `dotnetup list` and other commands will only show the installs for the current management mode. They may allude to the fact that other mode installs exist.

`dotnetup update` will not try to update installs from the other management mode, and so on.

## Admin Mode MVP

To rapidly support an emerging C#DK release, `dotnetup` will rollout `admin` support over time per command.

`dotnetup sdk install` and `dotnetup runtime install` will be the only functional install commands in `admin` mode. `init`, `env set`, and `self update` remain available so that users can change modes and update `dotnetup`. Every other command, including bare `dotnetup`, fails with `AdminModeCommandNotSupported`.

Commands are blocked by default: a command runs in `admin` mode only when its command class declares `admin` support, and that declaration lists the options and arguments it accepts there. Any other option or argument that the user supplies fails with `AdminModeSymbolNotSupported`.

### `dotnetup <> Install` Command Arguments under `admin`:

`--untracked` must also be provided in `admin` mode, at least in the MVP stage.

`--install-directory` will remain unsupported as MSI based installation will only ever install to the expected system managed, shared directory.

`--muxer` related replacement commands or configuration is not controllable for MSI installs so this flag will cause failure if it is applied under `admin` mode, similar to `--install-directory`, meaning `dotnet` must NOT be in use to have `admin` installs succeed.

In general, flags will be blocked in `admin` mode unless they are specifically marked to be under `admin` support for MVP. We may change this architecture later.

Hidden commands that are not impacted by the mode such as the underlying telemetry drainer and self update will still work regardless of the mode.

Rollout for other commands will be integrated later, such as:
`uninstall`, `update`, etc.

### MVP Telemetry

`admin` mode telemetry should not be part of the queries in the kusto ADX dashboard. `admin` installers have lower install success rates, which we don't want to distract from `dotnetup`. The ADX dashboard must be able to differentiate via a flag across all telemetry whether it is in `admin` mode or not - likely `mode` can be part of all auto-ingested telemetry properties, though it may also be `unknown` before `init`.

### Mode Storage

On Windows, `admin` mode is a machine-wide setting stored in admin-only `HKLM` registry values. Changing it requires elevation. The other modes (`none`, `shell`, and `everywhere`) remain per-user preferences in the existing `dotnetup` configuration under the user's data directory.

The per-user configuration cannot enforce `admin` mode. An unelevated user can replace the configuration file or its containing directory, or redirect the data directory with `DOTNET_DOTNETUP_DATA_DIR`. ACL-protecting the file, or a subdirectory of the user profile, would not help because the user controls the parent directory and can rename the protected item, then create a replacement. A protected directory under `%ProgramData%` would be enforceable, but it would duplicate the registry settings below without Group Policy or Intune support.

#### Registry Values (Windows only)

Two independent `REG_DWORD` values under `HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup` control `admin` mode. Both use the same structure: `1` (or any nonzero value) enables the behavior, and `0`, a missing value, or an unreadable value disables it. They are read from the native registry view, so WOW64 redirection does not split them.

| Value | Purpose |
| --- | --- |
| `AdminMode` | Selects `admin` mode for every user on the machine. |
| [`BlockUnsignedDownloads`](../signature-verification.md) | Blocks downloads that have no detached signature, such as daily builds. It applies in every mode. It does not block `self update` of an Authenticode-signed `dotnetup`, whose replacement must itself be Authenticode-signed. |

IT administrators can set either value through Group Policy, Intune, or similar tools. `dotnetup init` and `dotnetup env set` also write them after elevation, as described below. Keeping the values separate lets IT block unsigned downloads outside `admin` mode, or allow them inside it.

The effective mode is resolved in this order:

1. If `AdminMode` is nonzero, the mode is `admin`.
2. Otherwise, the per-user configuration determines the mode. The per-user configuration can never select `admin` mode; a hand-edited `admin` value is treated as unset.

When `AdminMode` selects `admin` mode, the per-user configuration is ignored but not rewritten. Its previous mode therefore applies again if `admin` mode is later disabled. Interactive `init` does not prompt for a mode, first-use onboarding treats the mode as already selected, and telemetry records whether the mode came from policy or user configuration.

Only administrators can write either value, while standard users can read both.

#### Changing the Mode

Selecting `admin` mode with `init` or `env set` sets `AdminMode` and `BlockUnsignedDownloads` to `1`. Leaving `admin` mode sets `AdminMode` to `0` and leaves `BlockUnsignedDownloads` unchanged, because IT may manage it independently. Both operations prompt for UAC before changing any state, and neither prompts when `AdminMode` already has the requested value or `dotnetup` is already elevated. If elevation is declined or fails, or the value does not change afterwards (for example, because Group Policy reapplies it), the command fails and neither the registry nor the per-user configuration changes. This also applies to `dotnetup init --interactive false --mode admin` when called by C#DK.

Elevation reuses the existing hidden-command pattern used for the system `PATH` ([`WindowsPathHelper.StartElevatedProcess`](../../../../src/Installer/dotnetup.Library/WindowsPathHelper.cs)). The elevated command accepts only the fixed `enableadminmode` and `disableadminmode` operations. It must not accept registry paths, value names, or data from its arguments. Because that pattern elevates `dotnetup.exe` from a user-writable location, a future alternative is to elevate `%SystemRoot%\System32\reg.exe` with fixed arguments instead.

Because `AdminMode` is machine-wide, selecting or leaving `admin` mode affects every user on the machine. `init` and `env set` state this before prompting for UAC.

Leaving `admin` mode happens only right before the newly selected mode is applied. If `init` exits without changes (the user exits the form, or a local `global.json` sets `sdk.paths`), or runs as a dry run, `admin` mode stays enabled. Selecting `admin` mode while it is already active changes nothing and does not prompt.

#### Limitations

The registry values constrain only `dotnetup`. They do not remove archive installs, `PATH` entries, or the user-level `DOTNET_ROOT` from earlier modes, although `dotnetup` should warn about them. They also do not prevent users from acquiring .NET through other tools.

No equivalent exists on other platforms because `admin` mode is Windows-only.

## Command Parity

The initial intent was to reduce UX confusion where commands behave differently across modes. Engineering cost is related; testing and supporting different functionality and expected behavior is costly.

However, management of admin installs behaves much differently. `dotnetup` is not the sole driving force - IT policy exists across many different applications and outlines specific retention, update timeframes, uninstall policies, etc. Other tools may pin or block actions from `dotnetup`. The realm of this should remain in those existing tools.

Therefore, `dotnetup` and its commands will behave differently in `admin` mode, and users will interact with them distinctly as well. See below for more context.

## Channels

Channel semantics remain the same.

`dotnetup sdk install daily`, or `11.0.1xx` would still install the same version regardless of the `mode`. The names for channels will remain the same. What a channel does however will be different.

Enabling `admin` mode also sets `BlockUnsignedDownloads`, so unsigned installers are blocked by default.

`daily` blobs exist but are not Authenticode signed (for example, https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-win-x64.exe). When `BlockUnsignedDownloads` is `0`, `admin` mode installs these `.exe` installers from the blob feed after verifying their published SHA-512 hash and printing the same unsigned-download warning used for archives. Authenticode is not checked for them. Installers resolved from the signed `releases.json` must always pass Authenticode verification chaining to a Microsoft root. IT administrators remain able to block unsigned installers via policy.


### Channel Differentiators under `admin`
Channels in modes that are not `admin` define specific update and retention policies.

In `admin` mode, channels strictly relate to shorthands for specific versions to manage (and for checks as to whether a version exists) and do not influence other behavior such as update cadence.

## `dotnetup` Manifest Behavior

The manifest will be ignored and there will be no `admin` dotnetup manifest.

The dotnetup manifest exists to speed up checks, such as whether or not the install already exists, and to inform `dotnetup` of what is managed on the machine across numerous locations, and what to update.

Initial designs may consider a separate manifest file for `admin` installs, or `admin` installed baked into the manifest. However, the manifest will never be correct and it will always need revalidation, because other software on the machine may manipulate or change installations or install policy. In addition, update and retention policies outside of `dotnetup` influence `.exe` behavior.


### Future Manifest Behavior

We may add later support for an `admin` based manifest to allow updating everything tracked by `dotnetup` to provide a more similar experience.

### Install Lookup

Instead of a manifest, `dotnetup` will need to query the system for the current install state. The cross-process mutex that `dotnetup` holds may not be respected by others, so it does not provide the same guarantees, however it will still be the same implementation as it prevents `dotnetup` from clobbering on itself.

`dotnetup list`, `dotnetup --info` and other commands in `admin` mode will defer to `hostfxr` APIs to avoid duplicated or sharded implementation.

Install checks query `hostfxr` through the install root's `dotnet` executable (`--list-sdks` and `--list-runtimes`) in a child process. Loading the system `hostfxr` into `dotnetup` itself would lock a file that the installer may need to replace, which can force a restart.

### Install Directory with `admin` mode

Environment variables that control the install location will be ignored. The same is true for those that control the 'scratch' directory or download cache directory. The admin install must not be world-writeable.

The `.exe` bundle will manage the destination. Destinations such as `<>\dotnet\x64\dotnet.exe` under emulation will be handled by the installer and not independently verified by `dotnetup`.


# Update, Uninstall & Garbage Collection

Each machine may have it's own [Removal Policy](https://learn.microsoft.com/en-us/dotnet/core/install/windows#choose-when-previous-versions-are-removed) per channel or version. `dotnetup` is not a tool for System Admins, and it will not be cognizant of those policies, as they may conflict with the channel that a user had specified via `dotnetup`. We are not going to reimplement policy semantics that are already implemented elsewhere.

### Update

`/install` in the `.exe` bundle today already respects Removal Policy. The process of `update` should strictly run the `.exe` with `/install` on the latest version per the referenced `dotnetup` semantic channel - e.g. `11.0` or `11.0.1xx`.

Note that `/install` updats across patch versions; so in the runtime, `X.Y.xx` and in the sdk, `X.Y.Zxx` - we respect other update patterns, e.g. `latest` which is cross-major.minor, or channels akin to roll forward values in global.json that update differently, so running `/install` to update and clean up won't always be correct.

Per the owner of the `.exe`, we know that update is the default system behavior and would clean up older patch versions.

Update will require a specific version in `admin` mode. In admin mode, `dotnetup <> update`, outside of `self`, will fail with a deliberate error.

`dotnetup <> update <channel or version>` in `admin` mode will only take one argument and it will not be initially supported when `admin` mode rolls out.

#### Update Channel Definition Parity

This means outside of `admin` mode, `dotnetup <> update` will also need to take a specific version or channel, such that `dotnetup sdk update latest` or `dotnetup sdk update 11.0`. However, this will remain unsupported and blocked until future enhancement.

### Uninstall & Garbage Collection

MSIs reference count dependents; uninstalling an SDK bundle may leave runtimes behind if Visual Studio or other tools still depend on them.
Garbage collection may aggressively try to trim installs managed by other tools. Therefore, GC will be disabled.

To uninstall a system install, the following arguments can be given to the `.exe` bundle: `['/uninstall', '/passive', '/norestart']`.

`dotnetup uninstall` will not initially be supported in `admin` mode.

`/uninstall` respects reference counts but only VS and a few others respect it - e.g. when tools like PowerToys give the user a link to download the Runtime, that is not reference counted, so garbage collection as it is for non-`admin` modes would remove what users had already requested if the installs are not respected from other sources.

We also know `/uninstall` on the `.exe` may return `0` even if the install was not uninstalled if other references remain so we must make our own hostfxr API check afterward to see if something was actually uninstalled or not.

Replacement policy is not respected by the `.exe` in terms of muxer or other settings and there currently exists no such control.

## Concurrency

Simultaneous commit operations - which in non-admin means 'extract', is not possible with MSI installs. `dotnetup` already defers the extract operation to one sequential step with concurrent downloads. The commit operation will happen sequentially in the same way. Concurrency will be deferred to avoid multi-UAC prompting.

## `global.json`

`global.json` functionality will be very limited and only influence version resolution when it is not provided.

A `global.json` file in the `cwd` influences the behavior of `dotnetup`. `global.json` files may or may not have the same file privilege boundary as a system protected file. If callers want to use `global.json` `cwd` must be applied.

Because UAC is required, the user's intent will be assumed and respected, so `global.json` will still influence the channel/version if no `channel` is provided when running a command such as `dotnetup sdk install` or `dotnetup runtime install`. It will not influence update behavior.

SDK `Paths` functionality as a `global.json` node will not be respected as it is not respected by `admin` installers today, and this behavior should not be allowed to influence system level decisions. It will not be a failure to install due to this to prevent unexpected failure.

## `.exe` Installer Error Handling

Before running an installer, `dotnetup` verifies its SHA-512 hash (from the signed `releases.json`, or from the blob feed for allowed unsigned builds), opens it so that it cannot be written or deleted until the installer exits, verifies the hash again, and then verifies Authenticode for signed sources.

The installer bundles request `asInvoker`, so `dotnetup` starts them without elevation using `/install /quiet /norestart`, and each bundle raises its own UAC prompt. `dotnetup` itself is never elevated to install. Installers run one at a time, so prompts never overlap.

There is no timeout in the MVP. Ctrl+C does not stop a running installer; `dotnetup` waits for it to finish and reports its real result, then starts no further installers in the batch and reports each remaining install as cancelled (`OperationCancelled`).

https://github.com/dotnet/vscode-dotnet-runtime/blob/f54c68789e1bdfca13e8262045cb41ca6a32aa82/vscode-dotnet-runtime-library/src/Acquisition/WinMacGlobalInstaller.ts#L102 common exit codes are already wrapped into specific user-facing messages that can be translated into `dotnetup` `user` failures. The MVP maps them as follows:

| Result | Handling |
| --- | --- |
| `0` | Success. |
| `3010`, `1641` | Success. A restart is required; `dotnetup` reports this and does not restart. |
| `1602`, `1223` (UAC declined) | Cancelled (`OperationCancelled`). |
| `1618` | Another installation is in progress (`InstallerBusy`). No automatic retry. |
| `740` | The installer requires elevation (`InstallerFailed`). |
| Any other nonzero code, including `1603` | `InstallerFailed`, reporting the exit code. No retry. |
| Installer did not start | `ProcessStartFailed`. |

After a successful exit, `dotnetup` confirms that the requested version is installed and otherwise fails with `InstallerPostInstallVerificationFailed`.

Future work: a bounded wait for an interrupted parent that confirms the original installation has ended, re-queries the installation state, and retries at most once only if installation is still needed. If the original operation's status cannot be established, report an actionable failure without launching another installer.

## Other Potential Issues - Needs Investigation

Machine locks need deliberate cross-user ACLs.

Do not hold a mutex in the parent while waiting for a privileged child that must acquire it. UpdateWorkflow currently holds its mutex throughout execution.

Installers that need to replace files in use (for example, a running `dotnet` process) can return `3010`.
