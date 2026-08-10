# Add `Self Update` Command

`dotnetup self update` command - public facing command that updates `dotnetup` itself
matches `dotnetup sdk update` nomenclature
`dotnetup update` already updates all of the installs managed by dotnetup. We can consider making it also self update dotnetup later.

# Trade-offs

On Windows, applications can pick:

1. To require reboot to update/replace; simplifying logic as other executables cannot be running.

2. To allow crash/power outage safe behavior; to enable this, applications must exclusively allow one executable to run at a time during update, enabling an atomic `Replace` file of the executable.

3. To accept the risk of the app no longer existing in the event of an outage/crash mid-update, but to allow other executables to run simultaneously at the time of update; to enable this, the executable is `renamed`. Open executable behaviors may change based on the updated executable or fail if they don`t consider proper caching.

For `dotnetup`, `3` is the best selection.

For `1`, requiring a reboot would interrupt developers, and dotnetup is not that integral to the system.
For `2`, Aspire CLI and RustUp also don`t have crash/power outage safe update behavior. With the current telemetry drainer, this also complicates the process structure to `Replace` as a copy process is needed. `dotnetup` is easily and quickly re-installed if this occurs.

Another design contention is whether to have mutex or inter-process (i.e. several process) aware logic for when multiple updates are attempted at once.

`Aspire` and `rustup` are not concurrency safe during update procedures. We argue that `dotnetup` should be, as IDEs or other tools that want to have attended upgrades may clobber or compete to update `dotnetup` at the same time, such as several VS Code extensions, or a window of VS Code and VS Code insiders. `dotnetup` should also allow multiple callers to invoke it at the same time to configure/install runtimes, so it must not run an exclusive lock on itself at all times.

# Self Update Broad Approach

## Windows:

#### Final Proposed Update Logic:

The main dotnetup process, say in dotnetup`s official install directory, D/, downloads a new version of `dotnetup`; the new version of dotnetup is downloaded into a user/temp folder - it is verified either via sha (preview) or signature (stable)

Upon verification, `dotnetup` holds a mutex for modifying `dotnetup` folder state (`dotnetupFolderMutex`) - this should be the same mutex used for modifying install states/manifests/installs to prevent another running dotnetup process from making breaking changes. Other executables may still be running, so it must also grab an exclusive file lock on `D/dotnetup.update.lock`. If acquiring the lock or modifying an update artifact fails because a file is in use, dotnetup uses the existing `FileLockDetector`/Windows Restart Manager integration to report the locking process and PID on a best-effort basis. It then deletes any pre-existing `D/dotnetup.exe.new` or `D/dotnetup.exe.old` - if we cannot delete them, fail with the exit code / problem as to why, with `InsufficientPermissionsToUpdate` (user error) pointing out the other executables are in use. The original new dotnetup executable is copied as `/D/dotnetup.exe.new`


The state of the file system would be:
```
`D/dotnetup.exe.new`
`D/dotnetup.exe`

`tmp/dotnetup/<random>/dotnetup.exe`
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

It renames `dotnetup.exe` first to ensure the app had properly exited and nobody restarted it. Executables on Windows can be renamed even while they are running; though one side effect of this, is that the future dotnetup processes will use the newer version to spawn child processes or load external assets - including the short-term telemetry drainer.

`Environment.ProcessPath` should be cached and accessible from a singleton upon `dotnetup` startup as its behavior is undefined [if the executable is renamed or deleted before [ProcessPath] property is first accessed](https://learn.microsoft.com/dotnet/api/system.environment.processpath?view=net-10.0#remarks).

`tmp/dotnetup/<random>/dotnetup.exe` keeps both `D/dotnetup.update.lock` and `dotnetupFolderMutex` while it runs `D/dotnetup.exe --version`. The update succeeds only if the command exits with status `0` and reports the expected version. It then reports success, includes an aka.ms link describing how to install older versions, and releases both locks.

If `tmp/dotnetup/<random>/dotnetup.exe` cannot run `D/dotnetup.exe`, the command does not return `0`, or the reported version does not match the expected version, it will attempt rollback while still holding both locks: delete `D/dotnetup.exe`, rename `D/dotnetup.exe.old` back to `D/dotnetup.exe`, and report an error. If it cannot delete `D/dotnetup.exe`, then the new executable may be in use but unresponsive; dotnetup should ask the user to reinstall dotnetup because the update cannot be unwound safely. Otherwise, assuming the hand-off went smoothly, it will release both locks and close.

`dotnetup.exe` (now the new executable) will try to delete the `tmp/dotnetup/dotnetup.exe` folders the next time it launches. If it cannot delete `dotnetup.exe.old` it will not fail, as there may be dotnetup processes running before `self update` started.

This situation is not ever perpetuating: The next time `dotnetup self update` executed the `old` executable will be replaced, and if it is not replaceable, a failure message would then prevent this situation from repeating itself as updates would be blocked.

This prevents breaking apps reliant on `dotnetup` to be running; e.g. when a customer has their machine off for over a month and tries to run dotnetup self update but has some persisted dependent application such as vscode holding the dotnetup executable; other apps can chose how to make this visibly actionable (e.g. close your apps using `dotnetup` when possible.)

None of this process may be `await`ed as mutexes are not safe to use in an asynchronous context. The point of the `lock` file is to prevent corruption - e.g. if another updater process got the global folder mutex, it still wouldn`t be able to actually delete / rename the dotnetup processes because it wouldnt have the file lock. This prevents another update process from deleting the actual valid dotnetup.exe.old at the start of its process.

#### Comparisons

`rustup` - It does use a separate updater process but two concurrent self udpates can interfere. This makes mores sense for rustup but less for us as mentioned prior.

`Aspire CLI` - It does not use a separate updater process; it renames itself and copies the old into the new location, runs `aspire.exe --version` with a redirected output, waits for it, and treats 0 as verification. Cleanup is left for a later invocation. This seems simpler and possibly less bug or process/file contention prone, however,  it is not concurrency safe. I initially had a design with a 3 process hand off but decided we could take the `--version` validation and later cleanup to reduce complexity.

`VS Code` - It is concurrency safe as it has a Node IPC-like Mutex and an in-process state machine from `Idle` to `Checking` to `Downloading` and so forth. We don`t need to have this complex of a state layer as we are not managing UI and several instances with many sub host processes, and we don`t need to use something as mature as Inno setup as `dotnetup` is a user level dev tool.

## Linux:

The running executables can rename themselves which removes the need for a secondary lock file or third process as no hand-off is needed. The global mutex `dotnetupFolderMutex` can be held, while the existing `dotnetup` executable downloads the new one into the same folder as itself as `dotnetup.new`. `dotnetup` verifies `dotnetup.new`, replaces itself after verification via a `move` operation, and runs the new executable - if `D/dotnetup --version` returns `0` it exits, otherwise it will move back to the existing `inode`:

A hard-link can preserve the old inode before replacement.
```cs
File.CreateHardLink(backupPath, installedPath);
File.Move(stagedPath, installedPath, overwrite: true);

File.Move(backupPath, installedPath, overwrite: true); // upon failure
```

Move is used because it atomically replaces the destination path which reduces risk of breaking states during disruption. The new executable must be downloaded into the dotnetup folder because cross file-system moves may fail or have non-atomic copy behavior.

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

In the outlined steps above, instead of renaming, `dotnetup.exe` gets replaced with a backup file of `dotnetup.exe.old`. `dotnetup.exe` is replaced by `dotnetup.exe.new` into `dotnetup.exe`. If we simply did a rename, then if the app got killed/crashed mid update then no `dotnnetup` executable would exist.

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

```var exclusiveActivityHandle = new FileStream(
    activityPath,
    FileMode.Open,
    FileAccess.ReadWrite,
    FileShare.None);
```

A file is used over a mutex, because the mutex does not allow cross-process sharing with current .NET implementations.

##### Telemetry drain processes under this alternative:

Until the OTelemetry fixes are complete, `dotnetup` uses a process drainer. The process drainer runs for a while after `dotnetup` exits which may cause confusing behavior when trying to run `dotnetup self update` as the update would be blocked by the telemetry drainer.

Short-term:

Telemetry drain processes must be launched via a temp copy of dotnetup.
