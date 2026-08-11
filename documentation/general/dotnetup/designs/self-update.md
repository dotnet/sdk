# Add `Self Update` Command

`dotnetup self update` updates `dotnetup` itself.

The update should be in-place and appear to happen seamlessly from the perspective of a CLI.

`dotnetup update` already updates all of the installs managed by dotnetup. Using `self update` as the key noun matches `dotnetup sdk update` nomenclature. `dotnetup update` will continue to update only the .NET SDK and .NET Runtime installs.

# Trade-offs

On Windows, `dotnetup` can pick one approach:

1. Reboot Approach:

To require a reboot to update & replace. This  simplifies logic as it reduces contention complexity with other running executables and programs. It also provides stronger crash recovery guarantees.

2. File Replace Approach:

To require that absolutely *no* `dotnetup` executable is running during the update process. This likewise provides stronger crash recovery by waiting for every running instance to exit as `dotnetup` can be atomically replaced with a backup. (File Replacement cannot occur on an executable on Windows while said executable is running.) Atomic replacement prevents a partially copied executable, though strict pkill/power-loss durability depends on the kernel.

One fault in this approach is that there can always be a race condition in an executable looking to see if it can run or not, because the executable cannot natively block itself from running whatsoever without elevation. Mutexes, reader/writer locks, or process enumeration may be used to tell the program to exit but this does not provide a full guarantee as the executable itself must be running to respect that and exit.

3. File Rename Approach:

`rename`s are permissible on an executable even while that executable is in use. We can allow other *certain* `dotnetup` processes to run at the same time of `update` (exclusively `dotnetup's telemetry drain process` as well as `dotnetup self update`) and use guards to block others.

Allowing `dotnetup runtime install` to run at the same time as `dotnetup self update` is a poor choice as the old executable may use a different manifest format and crash after the update changes, for example.

To use a rename means we must accept the risk of the app no longer existing in the event of an outage/crash mid-update. Users can still use scripts to re-acquire `dotnetup`.

Existing executables that are permitted to run may change behaviorally based on the updated executable or fail if they don't leverage proper caching of values dependent on the original executable.

## Selected Approach
For `dotnetup`, `3` is the best selection.

For `1`, requiring a reboot would interrupt developers, and dotnetup is a developer tool; a reboot-style approach is best served for system level applications or applications managed by IT.

For `2`, Aspire CLI and RustUp also don't have crash/power outage safe update behavior. With the current telemetry drainer, this also complicates the process structure to `Replace` as a copy process is needed.

Under approach `2`, while `dotnetup` can have a reader/writer lock that tells other `dotnetup` executables not to run, it does not prevent the executable from being invoked and causing a race condition while the newly invoked executable is looking at the mutex and deciding to close itself. This adds complexity and requires retry + busy-waiting logic to verify no executable is running if the `Replace` operation fails.

For `3`, `dotnetup self update` should be safely callable by others at the same time or during an update. IDEs or other tools that want to have unattended upgrades may clobber or compete to update `dotnetup` at the same time. This might be several VS Code extensions, or a window of VS Code and VS Code insiders.

To clarify, this does not mean that the updates to dotnetup itself are executed concurrently as there is no advantage in doing so. Racing update commands would simply observe that there is no update to proceed with and exit gracefully once the update command that won a race let go of its mutex - with approaches `1` or `2`, the racing updaters would see a failure and have to subscribe to a system event and retry. Under approach `3`, they don't have to worry about whether `dotnetup` is already being updated or not.

`dotnetup` is easily and quickly re-installed via the script if an outage occurs.

#### Concurrency Trade-Offs

Another contention is whether to have mutex or inter-process (i.e. several process) aware logic; should `dotnetup` gracefully succeed when multiple updates are attempted at once or simply reject the premise and fail?

`Aspire` and `rustup` are not concurrency safe during update procedures and they also do not block such an action explicitly.

`dotnetup` should be concurrency safe. `dotnetup` should also allow multiple callers to invoke it at the same time to configure/install runtimes, so it should not run an exclusive lock on itself at all times as this would delay progress and other apps unnecessarily.

# Success Criteria

- It's okay for `dotnetup` to no longer exist on the `PATH` or in the `dotnetup` folder in the event of a power-outage or uncontrolled process kill that occurs while `dotnetup self update` is running. Consumers must know how to re-acquire `dotnetup` or ebmed a backup `dotnetup` executable at a base level in the event this occurs.


- It is NOT okay for a power-outage or uncontrolled process kill that occurs while `dotnetup self update` is running to cause a permanent broken state that requires user understanding to remedy outside of re-installing `dotnetup`. e.g. it must not leave behind files with permissions that don't allow deletion, it must not corrupt other files that `dotnetup` depends on or leave them half-complete, including but not limited to a corrupt `dotnetup` executable that fails to load or execute.

- Multiple `dotnetup` processes in general must be able to execute at the same time.

- `dotnetup` may leverage asynchronous code or `await`.

- `self update` must not run if any `non-safe` `dotnetup` process is currently running. e.g. if `dotnetup sdk install` is running, the manifest format may change from one version to another; installing a new version that may edit the manifest format may cause the old `dotnetup` process to fail, and we want an invariant that avoids any such bugs.

- `non-safe` processes must exit immediately if any `self update` is running.

- `self update` does NOT make other `self update` processes fail or exit immediately; other `self update` processes must merely wait for the other update processes to complete and then determine that an update is no longer needed, assuming no release occurs within the time frame of the race.

- At this time, the only 'safe' `dotnetup` processes to have running during `self update` are the `telemetry drain` process, `dotnetup dotnet`, and the `self update` process itself. All other processes are `non-safe`. A process does not know its 'safety' status until `S.CL` parsers or `args` are processed.

- `dotnetup` must not require a reboot to update itself.

- It's ok to ignore a 'rogue' `dotnetup` process and allow them to fail or incur behavioral runtime bugs; e.g. an old `dotnetup` version that does not know about or support any mutex, semaphore, or locks and therefore bypasses the conditional guarantees. This is permissible because `dotnetup` is not yet `stable` or in a fully public `preview`.

# Self Update Broad Approach

## Windows:

#### Final Proposed Update Logic:

Immediately in `dotnetup self update`, `dotnetup` first acquires the already existing `ModifyInstallationStates` mutex (called `dotnetupFolderMutex` below) to prevent manifest/install-state mutations and guard against contending update calls. It will wait a certain amount of time on the mutex and fail if it cannot be acquired with `dotnetupMutexBusy`.

After acquiring `dotnetupFolderMutex`, `dotnetup` must also grab an exclusive file lock on `D/dotnetup.update.lock`. If acquiring the lock or modifying an update artifact fails because a file is in use, dotnetup should query Windows Restart Manager to report the locking process and PID on a best-effort basis and fail. This can be added with a `FileLockDetector`-style helper such as [commit `7fcc618e03f`](https://github.com/dotnet/sdk/commit/7fcc618e03f1520f688fa86bc7ade67aa417e380) via `RmRegisterResources`/`RmGetList` integration.

If `D/dotnetup.update.lock` is acquired, `dotnetup` removes any stale `D/dotnetup.exe.new` and does a best-effort delete of all `tmp/dotnetup/*/` folders, as well as all `dotnetup.exe.old.*`. `tmp/` cleanup is best-effort but failure to delete `dotnetup.exe.old` would cause `InsufficientPermissionsToUpdate`.

The original dotnetup process, say in dotnetup's official install directory, `D/`, assuming that there is a new version of `dotnetup` to update to (it can compare its assembly version with the prescribed `dotnetup` channel version), then downloads a new version of `dotnetup` into a randomly named, current-user-only temporary directory, say `tmp/dotnetup/<random>/dotnetup.exe`. If there is no new version, it would let the mutex and lock go and exit.

After copying the downloaded artifact to `D/dotnetup.exe.new`, dotnetup validates the staged copy.

Backups use transaction-specific names such as `D/dotnetup.exe.old.<transaction-id>` so a locked backup from an older process does not prevent staging a later update. A failure to remove an artifact required by the current transaction returns `InsufficientPermissionsToUpdate` with any available locking-process details.


The state of the file system would be:
```
`D/dotnetup.exe.new` <- replacement file
`D/dotnetup.exe` <- old file

`tmp/dotnetup/<random>/dotnetup.exe` <- swapping throwaway executable
```

While still holding `dotnetupFolderMutex`, `dotnetup.exe` releases its handle to `D/dotnetup.update.lock`, starts `tmp/dotnetup/<random>/dotnetup.exe`, and performs a heartbeat handoff via the hidden command `dotnetup self replacement <tmp folder executable>`. The replacer inherits stdin/stdout/stderr and acquires the `FileShare.None` lock on `D/dotnetup.update.lock` before it responds that it is ready:

```cs
FileStream updateLock = new(
    lockPath,
    FileMode.OpenOrCreate,
    FileAccess.ReadWrite,
    FileShare.None);
```

Once the replacer has acquired `D/dotnetup.update.lock` and responded OK (including the expected PID and an unguessable handoff token), `dotnetup.exe` releases `dotnetupFolderMutex` and exits. The replacer continues holding the update-file lock while it acquires `dotnetupFolderMutex`, so at least one synchronization primitive is held continuously across the handoff. If `dotnetup.exe` fails to get the heartbeat, it fails with `DotnetupReplacerCommunicationFailure` (product error) and tries to kill the `tmp/dotnetup/<random>/dotnetup.exe` process. If the replacer cannot acquire the mutex, it should report that another installation-state operation is in progress.

`tmp/dotnetup/<random>/dotnetup.exe` renames `dotnetup.exe` first to ensure the app had properly exited and nobody restarted it. Executables on Windows can be renamed even while they are running; though one side effect of this, is that the future dotnetup processes will use the newer version to spawn child processes or load external assets - including the short-term telemetry drainer.

`Environment.ProcessPath` should be cached and accessible from a singleton upon `dotnetup` startup as its behavior is undefined [if the executable is renamed or deleted before [ProcessPath] property is first accessed](https://learn.microsoft.com/dotnet/api/system.environment.processpath?view=net-10.0#remarks).

`tmp/dotnetup/<random>/dotnetup.exe` keeps both `D/dotnetup.update.lock` and `dotnetupFolderMutex` while it launches `D/dotnetup.exe --version` in an internal self-update verification mode. This mode disables telemetry and detached child processes and emits only the machine-readable build identity. The replacer waits synchronously with a timeout and succeeds only if the process exits with status `0` and reports the exact expected version/build identity. It then reports success, includes an aka.ms link describing how to install older versions, and releases both locks.

If `tmp/dotnetup/<random>/dotnetup.exe` cannot run `D/dotnetup.exe`, the command does not return `0`, or the reported version does not match the expected version, it will attempt rollback while still holding both locks: delete `D/dotnetup.exe`, rename the current transaction's `D/dotnetup.exe.old.<transaction-id>` back to `D/dotnetup.exe`, and report an error. If it cannot delete `D/dotnetup.exe`, then the new executable may be in use but unresponsive; dotnetup should ask the user to reinstall dotnetup because the update cannot be unwound safely. Otherwise, assuming the hand-off went smoothly, it will release both locks and close.

`dotnetup.exe` (now the new executable) performs best-effort cleanup of prior `tmp/dotnetup/<random>/` directories and `dotnetup.exe.old.*` backups on later launches. Cleanup is age-bounded and never follows symbolic links or reparse points outside the owned update directories. Failure to delete a locked old executable is not an update failure because a process started before `self update` may still be using it.

Abrupt termination can leave temporary directories or old backups behind indefinitely if dotnetup is never run again. Transaction-specific names prevent those stale files from corrupting or unnecessarily blocking a later update; subsequent launches retry cleanup on a best-effort basis.

This prevents breaking apps reliant on `dotnetup` to be running; e.g. when a customer has their machine off for over a month and tries to run dotnetup self update but has some persisted dependent application such as vscode holding the dotnetup executable; other apps can chose how to make this visibly actionable (e.g. close your apps using `dotnetup` when possible.)

Downloading and pre-lock verification may use asynchronous APIs. Once a thread acquires the thread-affine `ScopedMutex`, that thread must not cross an `await` boundary before releasing it; the heartbeat and final replacement critical sections therefore use synchronous waits or a dedicated thread. `FileStream` lock ownership is handle-based and is not thread-affine. The update-file lock bridges the parent/child mutex handoff and prevents another updater from deleting or replacing artifacts belonging to the active transaction.

In the event of a crash/pkill/outage, users can safely run the get-dotnetup scripts to redownload the `dotnetup` executable to the standard `D/` location.

#### Comparisons

`rustup` - Rustup [downloads and launches a separate updater](https://github.com/rust-lang/rustup/blob/main/src/cli/self_update.rs), but its self-update path has no cross-process update lock, so two concurrent self-updates can interfere with the shared updater, installed executable, and proxy links. Its process handoff addresses Windows executable locking, not update serialization or crash-atomic replacement.

`Aspire CLI` - Aspire's archive self-update [renames the running executable to a timestamped backup, copies the extracted new executable to the canonical path, runs `aspire.exe --version`, and rolls back caught failures](https://github.com/microsoft/aspire/blob/main/src/Aspire.Cli/Commands/UpdateCommand.cs). Cleanup is best-effort on later invocations. The replacement sequence has no cross-process update lock, so concurrent self-updates can race over the canonical path and backups. Dotnetup adopts exact-version validation and deferred cleanup, but adds cross-process serialization.

`VS Code` - VS Code's installed Windows updater combines a singleton main process, an [in-process update state machine](https://github.com/microsoft/vscode/blob/main/src/vs/platform/update/electron-main/abstractUpdateService.ts), native application/setup/updating/ready mutexes, staged versioned files, and [Inno Setup](https://github.com/microsoft/vscode/blob/main/build/win32/code.iss). This serializes Windows installers and blocks application startup during the final switch. The statement does not apply uniformly to every distribution: macOS delegates to Electron's updater, while ordinary Linux packages generally delegate installation to the package manager or download page. Dotnetup does not require VS Code's UI state machine or installer framework, but it adopts the narrower invariant that only one self-update transaction may modify its executable at a time.

## Linux:

Linux permits a running executable's pathname to be replaced while the process continues executing the old inode, removing the need for a secondary replacer process. Dotnetup downloads and authenticates the artifact in a secure temporary directory, then copies it to `D/dotnetup.new`, sets the expected executable mode, flushes it to disk, and validates the staged copy. It refuses to update through an unexpected symbolic link and operates on the canonical, dotnetup-owned install path. While holding `dotnetupFolderMutex`, it creates a transaction-specific backup hard link and performs a same-filesystem move over `D/dotnetup`. It runs `D/dotnetup --version` and requires status `0` plus the exact expected version; otherwise it atomically moves the backup path over the canonical path.

A same-directory hard link preserves the old inode before replacement. Both backup and staged paths must be on the same mounted filesystem as the installed executable.
```cs
File.CreateHardLink(backupPath, installedPath);
File.Move(stagedPath, installedPath, overwrite: true);

File.Move(backupPath, installedPath, overwrite: true); // upon failure
```

On a supported local Linux filesystem, the same-filesystem move maps to an atomic namespace replacement: new openers observe either the complete old inode or the complete new inode, while already-running processes continue using the old inode. This does not by itself guarantee persistence across power loss. The implementation flushes the staged file before replacement and, where strict durability is required, synchronizes the containing directory after replacement.

Cross-filesystem `File.Move` may degrade to copy/delete behavior which is why it is avoided.

# Update As a Version Swap Mechanism

Once the releases-index and releases.json files are available, the version to download can be repointed as `dotnetup self install <channel or version>` and use the same semantics as an `update`.

# Implementation

DotnetArchiveDownloader -> rename -> DotnetDownloader

DotnetArchiveDownloader in V1 (preview) can use `ResolveBlobFeedEntry` and use the same unsigned warning and only update off daily channels since that`s what exists. We can show progress and download using everything else we already do.

# Release Stable VS Preview

ResolveManifestEntry will resolve an index of dotnetup releases similar to the .NET release manifest.
The manifest will be signed just like the .NET artifacts manifests, with a detached signature, which will be downloaded as well and be used to validate dotnetup's own executable. We could only have an index but supporting multiple versions or allowing a downgrade/revert will only be possible if we maintain separate indexes. Whether we have a `daily` `preview` `stable` keyed index or a `major.minor` keyed index is not part of this spec.


# Alternatives Considered:

### Content below is not proposed implementation but rather alternatives that we could implement.

Windows also has reboot-delayed renames, but this provides a poor experience for immediate updates as it requires a reboot.

Instead of using 3 process hand-offs, the new copy could never run and the tmp/dotnetup/<random>/dotnetup.exe could rename the new one and report success, but this prevents automatic clean up of tmp/dotnetup/<random>/dotnetup.exe and does not validate that the new executable is actually valid and able to run or not as part of the process.

#### Rejected Alternative: Crash-Safe Replacement

The following `File.Replace` and process-draining design is not part of the final proposed update logic above.

In this alternative, `tmp/dotnetup/<random>/dotnetup.exe` performs an atomic replacement:

```cs
File.Replace(
    sourceFileName: stagedPath,
    destinationFileName: installedPath,
    destinationBackupFileName: backupPath,
    ignoreMetadataErrors: false);
```

This is to prevent a problem in the immediate term if power is lost or the app crashes after renaming the old dotnetup but before renaming the new dotnetup. We did not move forward with this as this requires a hack for telemetry draining as well as waiting for all dotnetup processes to finish before self update can run.

In this alternative, `dotnetup.exe.new` replaces `dotnetup.exe` while the prior executable is preserved as `dotnetup.exe.old`. If the chosen design instead performs separate old-to-backup and new-to-canonical renames, abrupt termination between those operations can leave no executable at the canonical path.

##### Required future behavior for all executables under this alternative:

All future-facing dotnetup executables must at launch grab a shareable file handle (say, always `dotnetup_home/dotnetup.users.lock`) and persist that handle for the entire process lifetime:

```cs
var activityHandle = new FileStream(
    activityPath,
    FileMode.OpenOrCreate,
    FileAccess.ReadWrite,
    FileShare.ReadWrite);
```

`dotnetup self update` must immediately fail if any `dotnetup` executable is currently running; it will detect this by trying to open the same file with exclusivity:

```cs
var exclusiveActivityHandle = new FileStream(
    activityPath,
    FileMode.Open,
    FileAccess.ReadWrite,
    FileShare.None);
```

A file is used over a mutex because a named mutex has only one owner and cannot represent multiple concurrent readers. On Windows, `FileShare` provides mandatory open-sharing semantics between processes. On Unix, .NET implements these modes with advisory `flock`; unsupported locking may be ignored or explicitly disabled, so this alternative requires a platform abstraction and cannot treat `FileShare` as a security boundary on every filesystem.

##### Telemetry drain processes under this alternative:

Until the OTelemetry fixes are complete, `dotnetup` uses a process drainer. The process drainer runs for a while after `dotnetup` exits which may cause confusing behavior when trying to run `dotnetup self update` as the update would be blocked by the telemetry drainer.

Short-term:

Telemetry drain processes must be launched via a temp copy of dotnetup.
