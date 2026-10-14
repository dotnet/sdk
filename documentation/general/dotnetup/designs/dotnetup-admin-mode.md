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

`init` will get a `--interactive false` flag. Under said flag, `--mode <>` must be provided and match `none`, `shell`, `everywhere` or `admin`, depending upon supported OS context.

Defaults remain the same for other options, with migration being true or false depending on the pre-existing default. (`admin` mode will never `migrate`.)

Callers such as C#DK that want integration with VS will need to call `dotnetup init --interactive false --mode admin`.

## Bifurcated Behavior

`dotnetup` will only behave in one mode at a time. We don't want users to be switching back and forth. A command line argument across all commands such as `sdk install` would encourage this behavior, but considering the PATH can only point to one main directory, this split is something we want to direct users away from.

`dotnetup sdk install` for example, will only install to the user folder using archives if the mode is not `admin`. If the mode is `admin`, it will only ever install via `msi`s.

`dotnetup --info`, `dotnetup list` and other commands will only show the installs for the current management mode. They may allude to the fact that other mode installs exist.

`dotnetup update` will not try to update installs from the other management mode, and so on.

## Admin Mode MVP

To rapidly support an emerging C#DK release, `dotnetup` will rollout `admin` support over time per command.

`dotnetup sdk install` and `dotnetup runtime install` will be the only functional commands in `admin` mode.

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

Two `REG_DWORD` values control `admin` mode. They are read from the native registry view, so WOW64 redirection does not split them.

| Value | Written by | Purpose |
| --- | --- | --- |
| `HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup!RequireAdminMode` | IT administrators through Group Policy, Intune, or similar tools | Organizational policy. It sits under the same key as [`BlockUnsignedDownloads`](../signature-verification.md). `dotnetup` never writes or deletes it. |
| `HKLM\SOFTWARE\dotnet\Dotnetup!AdminMode` | `dotnetup init` and `dotnetup env set`, after elevation | Machine setting selected through `dotnetup`. |

`dotnetup` does not write under `HKLM\SOFTWARE\Policies` because that key is reserved for managed policy. If `dotnetup` cleared a policy value when a user left `admin` mode, it could silently remove an IT-deployed setting until the next policy refresh.

The effective mode is resolved in this order:

1. If `RequireAdminMode` exists, it decides the mode. A nonzero value forces `admin` mode, and zero prevents `admin` mode. `init` and `env set` fail with a "managed by your organization" error if the user tries to change the mode, even when elevated.
2. Otherwise, a nonzero `AdminMode` selects `admin` mode.
3. Otherwise, the per-user configuration determines the mode.

When either value selects `admin` mode, the per-user configuration is ignored but not rewritten. Its previous mode therefore applies again if `admin` mode is later disabled. Interactive `init` does not prompt for a mode, first-use onboarding treats the mode as already selected, and telemetry records whether the mode came from policy, the machine setting, or user configuration.

A missing or unreadable value is treated as unset, matching `BlockUnsignedDownloads`. Only administrators can write either value, while standard users can read both.

#### Changing the Mode

Selecting `admin` mode with `init` or `env set` sets `AdminMode` to `1`, and leaving it deletes `AdminMode`. Both operations prompt for UAC before changing any state. If elevation is declined or fails, the command fails and neither the registry nor the per-user configuration changes. This also applies to `dotnetup init --interactive false --mode admin` when called by C#DK.

Elevation reuses the existing hidden-command pattern used for the system `PATH` ([`WindowsPathHelper.StartElevatedProcess`](../../../../src/Installer/dotnetup.Library/WindowsPathHelper.cs)). The elevated command accepts only `set` or `clear` and writes the fixed value. It must not accept registry paths, value names, or data from its arguments. Because that pattern elevates `dotnetup.exe` from a user-writable location, a future alternative is to elevate `%SystemRoot%\System32\reg.exe` with fixed arguments instead.

Because `AdminMode` is machine-wide, selecting or leaving `admin` mode affects every user on the machine. `init` and `env set` must state this before prompting for UAC.

#### Limitations

The registry values constrain only `dotnetup`. They do not remove archive installs, `PATH` entries, or the user-level `DOTNET_ROOT` from earlier modes, although `dotnetup` should warn about them. They also do not prevent users from acquiring .NET through other tools.

When admin mode is initially set via UAC, `BlockUnsignedDownloads` will also be set.

No equivalent exists on other platforms because `admin` mode is Windows-only.

## Command Parity

The initial intent was to reduce UX confusion where commands behave differently across modes. Engineering cost is related; testing and supporting different functionality and expected behavior is costly.

However, management of admin installs behaves much differently. `dotnetup` is not the sole driving force - IT policy exists across many different applications and outlines specific retention, update timeframes, uninstall policies, etc. Other tools may pin or block actions from `dotnetup`. The realm of this should remain in those existing tools.

Therefore, `dotnetup` and its commands will behave differently in `admin` mode, and users will interact with them distinctly as well. See below for more context.

## Channels

Channel semantics remain the same.

`dotnetup sdk install daily`, or `11.0.1xx` would still install the same version regardless of the `mode`. The names for channels will remain the same. What a channel does however will be different.

When switching to admin mode, the existing reg-key IT policy would be set, to block unsigned exes by default.

`daily` blobs exist but are not authenticode signed, so similar warnings and signature validation checks for daily builds would apply to `.exe` installers: https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-win-x64.exe
It admins remain able to block these via policy.


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

### Install Directory with `admin` mode

Environment variables that control the install location will be ignored. The same is true for those that control the 'scratch' directory or download cache directory. The admin install must not be world-writeable.

The `.exe` bundle will manage the destination. Destinations such as `<>\dotnet\x64\dotnet.exe` under emulation will be handled by the installer and not independently verified by `dotnetup`.


# Update, Uninstall & Garbage Collection

Each machine may have it's own [Removal Policy](https://learn.microsoft.com/en-us/dotnet/core/install/windows#choose-when-previous-versions-are-removed) per channel or version. `dotnetup` is not a tool for System Admins, and it will not be cognizant of those policies, as they may conflict with the channel that a user had specified via `dotnetup`. We are not going to reimplement policy semantics that are already implemented elsewhere.

### Update

`/install` in the `.exe` bundle today already respects Removal Policy. The process of `update` should strictly run the `.exe` with `/install` on the latest version per the referenced `dotnetup` semantic channel - e.g. `11.0` or `11.0.1xx`.

Update will require a specific version in `admin` mode. In admin mode, `dotnetup <> update`, outside of `self`, will fail with a deliberate error.

`dotnetup <> update <channel or version>` in `admin` mode will only take one argument and it will not be initially supported when `admin` mode rolls out.

#### Update Channel Definition Parity

This means outside of `admin` mode, `dotnetup <> update` will also need to take a specific version or channel, such that `dotnetup sdk update latest` or `dotnetup sdk update 11.0`. However, this will remain unsupported and blocked until future enhancement.

### Uninstall & Garbage Collection

MSIs reference count dependents; uninstalling an SDK bundle may leave runtimes behind if Visual Studio or other tools still depend on them.
Garbage collection may aggressively try to trim installs managed by other tools. Therefore, GC will be disabled.

To uninstall a system install, the following arguments can be given to the `.exe` bundle: `['/uninstall', '/passive', '/norestart']`.

`dotnetup uninstall` will not initially be supported in `admin` mode.

## Concurrency

Simultaneous commit operations - which in non-admin means 'extract', is not possible with MSI installs. `dotnetup` already defers the extract operation to one sequential step with concurrent downloads. The commit operation will happen sequentially in the same way. Concurrency will be deferred to avoid multi-UAC prompting.

## `global.json`

`global.json` functionality will be very limited and only influence version resolution when it is not provided.

A `global.json` file in the `cwd` influences the behavior of `dotnetup`. `global.json` files may or may not have the same file privilege boundary as a system protected file. If callers want to use `global.json` `cwd` must be applied.

Because UAC is required, the user's intent will be assumed and respected, so `global.json` will still influence the channel/version if no `channel` is provided when running a command such as `dotnetup sdk install` or `dotnetup runtime install`. It will not influence update behavior.

SDK `Paths` functionality as a `global.json` node will not be respected as it is not respected by `admin` installers today, and this behavior should not be allowed to influence system level decisions. It will not be a failure to install due to this to prevent unexpected failure.

## `.exe` Installer Error Handling

The `.exe` invocation will be built around a wrapper after `Authenticode` + releases.json `pcks7` verification that includes a timeout.

https://github.com/dotnet/vscode-dotnet-runtime/blob/f54c68789e1bdfca13e8262045cb41ca6a32aa82/vscode-dotnet-runtime-library/src/Acquisition/WinMacGlobalInstaller.ts#L102 common exit codes are already wrapped into specific user-facing messages that can be translated into `dotnetup` `user` failures. Some of them translate to existing errors (e.g. `disk is full`) and include:

# Result | Handling
0	| Record completion.
3010 | Restart required - do not restart, just communicate it with a specific user failure.
UAC cancellation / 1602	Cancellation | Simply fail.
1618	| Bounded busy retry or actionable failure.
1603 | Policy rejection, invalid payload	Preserve diagnostics; no blanket retry.
Timeout / interrupted parent | Retry Once


## Other Potential Issues - Needs Investigation

Elevation should be minimally held and leverage existing SID connection workloads logic if needed.

Machine locks need deliberate cross-user ACLs.

Do not hold a mutex in the parent while waiting for a privileged child that must acquire it. UpdateWorkflow currently holds its mutex throughout execution.

There is no timeout contract at this time.
