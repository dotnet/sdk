# 'dotnetup Admin Mode

`admin` mode with `dotnetup` enables management of installs via `.exe bundles` (via `msis`), as opposed to `.tar.gz` and `.zip` install of `.NET.`

`admin` mode is only supported on Windows. Attempting to set `admin` mode on any other platform will fail.

`admin` mode is a side scenario for `dotnetup`: the primary and default experience will be to use the default mode (`shell` on Unix or `everywhere` mode on Windows.) Under these modes, install times will be faster, less disk space will be consumed, and `dotnetup` will be able to provide more coherent and enforced management of the installs.

Tools that manage install policy and integrate into IT Systems for .NET management such as [DNIM](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dnim-overview), MU (Microsoft Update) exist today and `dotnetup` is not meant to replace or align with those tools. `dotnetup` remains a developer focused tool focused on archive based installs. However, we recognize that some users may want to manage their 'admin' controlled installs from a `CLI` that matches their experience using `dotnetup`; some users may also want to keep their management within Visual Studio but engage with the command line to manage other installs.

## Mode Semantics

`admin` was preferred over `system` because it outlines the differentiation; user local paths may be set to take precedence on the system path, so dotnetup can manage system-level installs without needing `admin` mode per se. Of course, `elevation` may be required to modify the system install `PATH`, but that's not necessarily always the case.

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
      Installs are managed by other tools alongside `dotnetup` in a system wide folder.
      Installs may be slower and consume more disk space but can be centrally managed outside of dotnetup.

```

If the 'system' path is already pointed towards somewhere besides an expected program files location `dotnetup` will fail.
The `PATH` will not be managed by `dotnetup` as the[ MSI installer already does this](https://github.com/dotnet/runtime/blob/ede6e05426a1fb94637c8c04cb26a6230bf5ea59/src/installer/pkg/sfx/installers/host.wxs#L47-L73), provided the installs match the native architecture.

`PR`: `Environment.SpecialFolder.ProgramFiles`
`PRx86` : `Environment.SpecialFolder.ProgramFilesX86`

Windows | architecture	| Installer payload	Default installation root
x64	| x64	| `PR`\dotnet
x64	| x86	| `PRx86`\dotnet
Arm64	| Arm64	| `PR`\dotnet
Arm64	| x64	| `PR`\dotnet\x64

Migrating system installs is meaningless and so it will always be 'No.'

## Bifurcated Behavior

`dotnetup` will only behave in one mode at a time.

`dotnetup sdk install` for example, will only install to the user folder using archives if the mode is not `admin`. If the mode is `admin`, it will only ever install via `msi`s.

`dotnetup --info`, `dotnetup list` and other commands will only show the installs for the current management mode. They may allude to the fact that other mode installs exist.

`dotnetup update` will not try to update installs from the other management mode, and so on.

Commands may take in a flag `--mode` to enforce specific mode behavior from the CLI or process invocation, but commands that modify user configuration will block the `--mode` flag.

## Command Parity

The initial intent was to reduce UX confusion where commands behave differently across modes. Engineering cost is related; testing and supporting different functionality and expected behavior is costly.

However, management of admin installs behaves much differently. `dotnetup` is not the sole driving force - IT policy exists across many different applications and outlines specific retention, update timeframes, uninstall policies, etc. Other tools may pin or block actions from `dotnetup`. The realm of this should remain in those existing tools.

Therefore, `dotnetup` and its commands will behave differently in `admin` mode, and users will interact with them distinctly as well.

Arguments such as `--install-directory` will not be allowed in `admin` mode.

## Channels

Channel semantics remain the same to a degree.

`dotnetup sdk install daily` would still install the same version regardless of the `mode`.
The names for channels will remain the same. What a channel does however will be different.

Channels in modes that are not `admin` define specific update and retention policies. In `admin` mode, channels strictly relate to shorthands for specific versions to manage (and for checks as to whether a version exists) and do not influence other behavior such as update cadence.

## `dotnetup` Manifest Behavior

The manifest will be ignored and there will be no `admin` dotnetup manifest.

The dotnetup manifest exists to speed up checks, such as whether or not the install already exists, and to inform `dotnetup` of what is managed on the machine across numerous locations.

Initial designs may consider a separate manifest file for `admin` installs, or `admin` installed baked into the manifest. However, the manifest will never be correct and it will always need revalidation, because other software on the machine may manipulate or change installations or install policy. In addition, update and retention policies outside of `dotnetup` influence `.exe` behavior, so the manifest behavior is not necessary.

### Install Lookup

Instead of a manifest, `dotnetup` will need to query the system for the current install state. The cross-process mutex that `dotnetup` holds may not be respected by others, so it does not provide the same guarantees, however it will still be the same implementation as it prevents `dotnetup` from clobbering on itself.

`dotnetup list`, `dotnetup --info` and other commands in `admin` mode will defer to `hostfxr` APIs to avoid duplicated or sharded implementation.

### Install Directory

Environment variables that control the install location will be ignored. The same is true for those that control the 'scratch' directory or download cache directory. The admin install must not be world-writeable.

The `.exe` bundle will manage the destination. Destinations such as `<>\dotnet\x64\dotnet.exe` under emulation will be handled by the installer and not independently verified by `dotnetup`.


# Update, Uninstall & Garbage Collection

Each machine may have it's own [Removal Policy](https://learn.microsoft.com/en-us/dotnet/core/install/windows#choose-when-previous-versions-are-removed) per channel or version. `dotnetup` is not a tool for System Admins, and it will not be cognizant of those policies, as they may conflict with the channel that a user had specified via `dotnetup`.

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

Simultaneous commit operations - which in non-admin means 'extract', is not possible with MSI installs. `dotnetup` already defers the extract operation to one sequential step with concurrent downloads. The commit operation will happen sequentially in the same way.

## `global.json`

`global.json` functionality will be very limited and only influence version resolution when it is not provided.

A `global.json` file in the `cwd` influences the behavior of `dotnetup`. `global.json` files may or may not have the same file privilege boundary as a system protected file.

Because UAC is required, the user's intent will be assumed and respected, so `global.json` will still influence the channel/version if no `channel` is provided when running a command such as `dotnetup sdk install` or `dotnetup runtime install`. It will not influence update behavior.

SDK `Paths` functionality as a `global.json` node will not be respected as it is not respected by `admin` installers today, and this behavior should not be allowed to influence system level decisions.

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

Elevation should be minimally held and leverage existing SID connection workloads logic.

Machine locks need deliberate cross-user ACLs.

Do not hold a mutex in the parent while waiting for a privileged child that must acquire it. UpdateWorkflow currently holds its mutex throughout execution.

There is no timeout contract at this time.
