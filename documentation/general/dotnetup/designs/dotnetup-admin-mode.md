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
      Installs can be managed by other tools alongside dotnetup in a shared system folder.
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

Migrating system installs is meaningless, so the `init` form locks the migration field to "Not supported in admin mode" and never migrates.

## Selecting Admin Mode

`admin` mode is selected with:

```console
dotnetup env set admin
```

This removes dotnetup-managed user-level .NET environment wiring and stores `admin` as the current user's access mode. The separate `--dotnetup-on-path` setting is honored; unrelated environment entries and installed SDKs and runtimes are left alone. Callers such as C# Dev Kit then install the exact SDK they need with `dotnetup sdk install <version> --untracked`.

## Bifurcated Behavior

`dotnetup` will only behave in one mode at a time. We don't want users to be switching back and forth. A command line argument across all commands such as `sdk install` would encourage this behavior, but considering the PATH can only point to one main directory, this split is something we want to direct users away from.

`dotnetup sdk install` for example, will only install to the user folder using archives if the mode is not `admin`. If the mode is `admin`, it will only ever install via `msi`s.

`dotnetup --info` reports the current configuration but does not list system installations in the MVP. `dotnetup list` reports that listing is not yet supported and directs users to `dotnet --list-sdks`, `dotnet --list-runtimes`, or `dotnet --info`.

`dotnetup update` will not try to update installs from the other management mode, and so on.

## Admin Mode MVP

To rapidly support an emerging C#DK release, `dotnetup` will rollout `admin` support over time per command.

`dotnetup sdk install` and `dotnetup runtime install` are the functional install commands in `admin` mode. `init`, `env set`, `env clear`, `env show`, `--info`, and `self update` remain available so users can change or inspect their mode and update `dotnetup`. [`env script`](../reference/dotnetup-env-script.md) and its hidden `print-env-script` compatibility alias also remain available so shell profiles keep working. Every other command, including bare `dotnetup`, fails with `AdminModeCommandNotSupported`; `list` uses an admin-specific unsupported message.

Commands are blocked by default: a command runs in `admin` mode only when its command class declares `admin` support, and that declaration lists the options and arguments it accepts there. Any other option or argument that the user supplies fails with `AdminModeSymbolNotSupported`.

### `dotnetup <> Install` Command Arguments under `admin`:

`--untracked` must also be provided in `admin` mode for installs to the shared system location, at least in the MVP stage. It is not required when global.json `paths` select a local directory; see [`global.json`](#globaljson).

`--install-directory` will remain unsupported as MSI based installation will only ever install to the expected system managed, shared directory. Only a `global.json` `paths` entry can redirect an `admin` mode install to a local directory; see [`global.json`](#globaljson).

`--muxer` related replacement commands or configuration is not controllable for MSI installs so this flag will cause failure if it is applied under `admin` mode, similar to `--install-directory`, meaning `dotnet` must NOT be in use to have `admin` installs succeed.

In general, flags will be blocked in `admin` mode unless they are specifically marked to be under `admin` support for MVP. We may change this architecture later.

Hidden commands that are not impacted by the mode such as the underlying telemetry drainer and self update will still work regardless of the mode.

Rollout for other commands will be integrated later, such as:
`uninstall`, `update`, etc.

### MVP Telemetry

`admin` mode telemetry is separated from the normal queries in the Kusto ADX dashboard because admin installers have different success characteristics. The dashboard access-mode selector defaults to non-admin telemetry and can instead select admin telemetry. Rows created before access-mode telemetry was added are treated as non-admin.

### Mode Storage

`admin`, like the other access modes, is stored in the current user's existing `dotnetup` configuration. Different users can select different access modes. This setting does not grant administrative privileges: the official installer still requests administrator approval when it performs a machine-wide installation.

On Windows, `admin` can be selected from the interactive `dotnetup init` form or with
`dotnetup env set admin`. There is no `init --mode` option.

#### Registry Values (Windows only)

The `BlockUnsignedDownloads` `REG_DWORD` value under `HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup` controls whether unsigned downloads are allowed. A nonzero value blocks them; `0`, a missing value, or an unreadable value allows them. It is read from the native registry view, so WOW64 redirection does not split it.

| Value | Purpose |
| --- | --- |
| [`BlockUnsignedDownloads`](../signature-verification.md) | Blocks downloads that have no detached signature, such as daily builds. It applies in every mode. It does not block `self update` of an Authenticode-signed `dotnetup`, because the replacement must pass Authenticode validation. |

Administrators can set this value through Group Policy, Intune, or similar tools.
dotnetup does not change it when selecting, reapplying, or leaving an access mode.
Admin mode requires signature validation independently of this policy.

#### Changing the Mode

Selecting `admin` mode with `env set` or init applies environment changes and
stores the user setting without changing `BlockUnsignedDownloads`. Leaving
`admin` mode also leaves the policy unchanged.

`env set` without a mode reapplies the stored environment settings. `env clear` is
equivalent to `env set none --dotnetup-on-path false`.
Neither command installs or uninstalls .NET. Init preserves
the configured dotnetup-on-PATH setting, defaulting to `true` on first setup.

An environment or config write failure is reported as a failure, not as successful
setup. Existing Everywhere-mode PATH cleanup can still require elevation.

#### Limitations

Selecting `admin` mode rewrites the dotnetup shell profile block so it calls `env script` with only the parameters for that mode, which do not add the user-level .NET root or set `DOTNET_ROOT`. `env script` has no admin-specific logic and honors the parameters it is given. Existing archive installs remain on disk. Open terminals may retain their previous environment until restarted.

No equivalent exists on other platforms because `admin` mode is Windows-only.

## Command Parity

The initial intent was to reduce UX confusion where commands behave differently across modes. Engineering cost is related; testing and supporting different functionality and expected behavior is costly.

However, management of admin installs behaves much differently. `dotnetup` is not the sole driving force - IT policy exists across many different applications and outlines specific retention, update timeframes, uninstall policies, etc. Other tools may pin or block actions from `dotnetup`. The realm of this should remain in those existing tools.

Therefore, `dotnetup` and its commands will behave differently in `admin` mode, and users will interact with them distinctly as well. See below for more context.

## Channels

Channel names and version selection remain the same, but admin mode requires
signature validation. Unsigned daily or blob-feed builds are blocked even when
`BlockUnsignedDownloads` is `0`, with the user error
"Signature validation is required when using admin mode."
Installers must also pass Authenticode verification chaining to a Microsoft root;
a failure is reported as `SignatureVerificationFailed` and the installer is not run.
Archive installs selected through global.json `sdk.paths` must use signed release
metadata. This admin-mode requirement applies to .NET installs, not dotnetup
itself. `dotnetup self update` is unaffected by access mode: it retains its
existing hash verification, machine-policy checks, and requirement for a
Microsoft-rooted Authenticode signature when the installed executable is signed.


### Channel Differentiators under `admin`
Channels in modes that are not `admin` define specific update and retention policies.

In `admin` mode, channels strictly relate to shorthands for specific versions to manage (and for checks as to whether a version exists) and do not influence other behavior such as update cadence.

## `dotnetup` Manifest Behavior

The dotnetup manifest records install specs (requests for installs) to use for updates and garbage collection, and the actual installations installed in each install root.

For admin mode installs, we will not use the dotnetup manifest to record the installations.  Admin installs can be installed and updated outside of dotnetup so the manifest would not be an accurate source of truth.

In the MVP, we don't record install specs or support updating admin installs.  When we do add that support we will likely record admin install specs in the dotnetup manifest, while using other sources of truth (such as the Registry) to determine what is currently installed.

### Install Lookup

Instead of a manifest, `dotnetup` will need to query the system for the current install state. The cross-process mutex that `dotnetup` holds may not be respected by others, so it does not provide the same guarantees, however it will still be the same implementation as it prevents `dotnetup` from clobbering on itself.

`dotnetup list`, `dotnetup --info` and other commands in `admin` mode will defer to `hostfxr` APIs to avoid duplicated or sharded implementation.

Install checks query `hostfxr_get_dotnet_environment_info` in a short-lived copy of `dotnetup`, using the existing native wrapper and returning SDK and runtime records as JSON rather than parsing `dotnet --list-*` output. The private query runs before normal command startup, without telemetry or first-run output. Loading the system `hostfxr` into the parent `dotnetup` process would lock a file that the installer may need to replace, which can force a restart; waiting for the query process to exit releases those files before installation. Archive and bundle installs share one validator, while retaining archive-specific layout checks and the existing non-Windows archive-validation behavior. If a successfully loaded `hostfxr` lacks that export, the query reports a distinct unavailable capability, not an empty successful enumeration. Validation then requires the muxer and a non-empty directory for the exact requested component/version in the requested root; system enumeration skips that unavailable API only when a muxer and a non-empty recognized component/version directory exist. Missing, misplaced, or unloadable hostfxr libraries and other query failures do not use this fallback. Prepared bundle installs repeat the host check while holding the commit mutex, before running the installer, so a request already satisfied by an earlier batch item or another process is skipped without another installer or UAC prompt.

The implementation is in [`HostFxrQueryProcess`](../../../../src/Installer/dotnetup.Library/HostFxrQueryProcess.cs) and [`InstallationValidator`](../../../../src/Installer/dotnetup.Library/InstallationValidator.cs). The child uses the install root as its working directory and disables multilevel lookup to avoid inheriting the caller's SDK discovery context. [`HostFxrWrapper`](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/HostFxrWrapper.cs) reports a failed Windows library load instead of silently falling back to another `hostfxr`.

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

MSI packages installed by Burn bundles register dependency providers. A parent bundle is registered as a dependent of its chained MSI packages, and installers such as Visual Studio can register their own named dependents. Bundles themselves are not reference counted.

Uninstalling a bundle removes that bundle's dependency registration and requests removal of its MSI packages. A package remains installed while another registered dependent, such as Visual Studio, still requires it. An application that merely directs a user to the standalone .NET installer does not gain an application-specific dependency registration; the dependency relationship is between that installer bundle and its MSI packages.

Garbage collection cannot distinguish a system installation that the user still wants from one that is no longer needed. It could therefore remove installations managed by another bundle or tool. GC will be disabled in `admin` mode.

To uninstall a system install, the following arguments can be given to the `.exe` bundle: `['/uninstall', '/passive', '/norestart']`.

`dotnetup uninstall` will not initially be supported in `admin` mode.

`/uninstall` may not fully remove the install if multiple dependency registrations exist against that install. Through the .exe installer, Burn manages dependents. Visual Studio is another such dependent. When a user installs via the .exe bundle, e.g. when applications prompt a user to install the .NET runtime and they use the .exe installer, another .exe installer later would allow uninstall. There is no separate registered blocking dependent. In the case of VS or another tool installing as a special dependency registration however, it would not be uninstalled.

This means Garbage Collection would intrusively uninstall user's existing installs if it were handled the same way as non-admin mode installs.

Replacement policy is not respected by the `.exe` in terms of muxer or other settings and there currently exists no such control.

## Concurrency

Simultaneous commit operations - which in non-admin means 'extract', are not possible with per-machine MSI installs because the Windows Installer service does not permit them to run concurrently. `dotnetup` already defers the extract operation to one sequential step with concurrent downloads. Installer commit operations will happen sequentially in the same way.

## `global.json`

`global.json` functionality will be very limited: it influences version resolution when no version is provided, and its SDK `paths` can redirect the install to a local directory.

A `global.json` file in the `cwd` influences the behavior of `dotnetup`. `global.json` files may or may not have the same file privilege boundary as a system protected file. If callers want to use `global.json` `cwd` must be applied.

Because UAC is required, the user's intent will be assumed and respected, so `global.json` will still influence the channel/version if no `channel` is provided when running a command such as `dotnetup sdk install` or `dotnetup runtime install`. It will not influence update behavior.

SDK `paths` are not respected by the `admin` installers, which always install to the shared system location, so they must not influence system level decisions. Instead, when the first meaningful `paths` entry is a literal directory outside the system .NET location, `dotnetup` installs into that directory with the same archive mechanism it uses outside `admin` mode, because the repository opted into a local SDK there. These installs require no elevation, are tracked in the dotnetup manifest like any other archive install unless `--untracked` is passed, and require signed release metadata regardless of `BlockUnsignedDownloads`. The access mode remains `admin` for the command gate and telemetry; the `install.mechanism` (`archive`) and `install.path_source` (`globaljson`) telemetry tags identify these installs. When the first entry is `$host$`, or the literal path is the system .NET location, the `.exe` installer installs to the system location as usual.

## `.exe` Installer Error Handling

Before running an installer, `dotnetup` verifies its SHA-512 hash from the signed `releases.json`, opens it so that it cannot be written or deleted until the installer exits, verifies the hash again, and then verifies Authenticode. Unsigned blob-feed builds are blocked before download.

The installer bundles request `asInvoker`, so `dotnetup` starts them without elevation using `/install /quiet /norestart`, and each bundle raises its own UAC prompt. `dotnetup` itself is never elevated to install. Installers run one at a time, so prompts never overlap.

There is no timeout in the MVP. Ctrl+C does not stop a running installer; `dotnetup` waits for it to finish and reports its real result, then starts no further installers in the batch and reports each remaining install as cancelled (`OperationCancelled`).

The [installer runner](../../../../src/Installer/dotnetup.Library/WindowsInstaller/BundleProcessRunner.cs) displays a short cancellation notice above the live progress rows. It overwrites the full width of each notice line so progress-bar fragments do not remain beside the message, including when a narrow terminal wraps it.

https://github.com/dotnet/vscode-dotnet-runtime/blob/f54c68789e1bdfca13e8262045cb41ca6a32aa82/vscode-dotnet-runtime-library/src/Acquisition/WinMacGlobalInstaller.ts#L102 common exit codes are already wrapped into specific user-facing messages that can be translated into `dotnetup` `user` failures. The MVP maps them as follows:

| Result | Handling |
| --- | --- |
| `0` | Success. |
| `3010` | Success. The user must restart to complete the installation; `dotnetup` reports this, does not restart automatically, and skips post-install hostfxr validation. |
| `1641` | Success. A restart has already been initiated; `dotnetup` reports this and skips post-install hostfxr validation. |
| `1602`, `1223` (UAC declined) | Cancelled (`OperationCancelled`). |
| `1618` | Another installation is in progress (`InstallerBusy`). No automatic retry. |
| `740` | The installer requires elevation (`InstallerFailed`). |
| Any other nonzero code, including `1603` | `InstallerFailed`, reporting the exit code. No retry. |
| Installer did not start | `ProcessStartFailed`. |

After exit code `0`, `dotnetup` confirms that the requested version is installed and otherwise fails with `InstallerPostInstallVerificationFailed`. Exit codes `3010` and `1641` skip this validation because pending file replacements may not be completed until the restart.

Future work: a bounded wait for an interrupted parent that confirms the original installation has ended, re-queries the installation state, and retries at most once only if installation is still needed. If the original operation's status cannot be established, report an actionable failure without launching another installer.

## Other Potential Issues - Needs Investigation

Machine locks need deliberate cross-user ACLs.

Do not hold a mutex in the parent while waiting for a privileged child that must acquire it. UpdateWorkflow currently holds its mutex throughout execution.

Installers that need to replace files in use (for example, a running `dotnet` process) can return `3010`.
