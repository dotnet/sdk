# Add 'Self Update' Command

'dotnetup self update' command - public facing command that updates 'dotnetup' itself
matches 'dotnetup sdk update' nomenclature
'dotnetup update' already updates all of the installs managed by dotnetup. We can consider making it also self update dotnetup later.

# Trade-offs

On Windows, applications can pick:

1. To require reboot to update/replace; simplifying logic as other executables cannot be running.

2. To allow crash/power outage safe behavior; to enable this, applications must exclusively allow one executable to run at a time during update, enabling an atomic `Replace` file of the executable.

3. To accept the risk of the app no longer existing in the event of an outage/crash mid-update, but to allow other executables to run simultaneously at the time of update; to enable this, the executable is `renamed`. Open executable behaviors may change based on the updated executable or fail if they don't consider proper caching.

For `dotnetup`, `3` is the best selection.

For `1`, requiring a reboot would interrupt developers, and dotnetup is not that integral to the system.
For `2`, Aspire CLI and RustUp also don't have crash/power outage safe update behavior. With the current telemetry drainer, this also complicates the process structure to `Replace` as a copy process is needed. `dotnetup` is easily and quickly re-installed if this occurs.

Another design contention is whether to have mutex or inter-process (i.e. several process) aware logic for when multiple updates are attempted at once.

`Aspire` and `rustup` are not concurrency safe during update procedures. We argue that `dotnetup` should be, as IDEs or other tools that want to have attended upgrades may clobber or compete to update `dotnetup` at the same time, such as several VS Code extensions, or a window of VS Code and VS Code insiders. `dotnetup` should also allow multiple callers to invoke it at the same time to configure/install runtimes, so it must not run an exclusive lock on itself at all times.

# Self Update Broad Approach

## Windows:

#### Non-Update Code Changes:

#### Update Logic:

The main dotnetup process, say in dotnetup's official install directory, D/, downloads a new version of 'dotnetup'; the new version of dotnetup is downloaded into a user/temp folder - it is verified either via sha (preview) or signature (stable)

Upon verification, 'dotnetup'  holds a mutex for modifying 'dotnetup' folder state ('dotnetupFolderMutex') - this should be the same mutex used for modifying install states/manifests/installs to prevent another running dotnetup process from making breaking changes. Other executables may still be running, so it must also grab an exclusive file lock on 'D/dotnetup.update.lock' and will bail if it cannot. It then deletes any pre-existing 'D/dotnetup.exe.new' or 'D/dotnetup.exe.old' - if we cannot delete them, fail with the exit code / problem as to why, with 'InsufficientPermissionsToUpdate' (user error) pointing out the other executables are in use. The original new dotnetup executable is copied as '/D/dotnetup.exe.new'


The state of the file system would be:
'D/dotnetup.exe.new'
'D/dotnetup.exe'

'tmp/dotnetup.exe'


'dotnetup.exe' lets go of the file while still holding onto the mutex and starts 'tmp/dotnetup.exe' and does a heart-beat hand-off via a hidden command, 'dotnetup self replacement <tmp folder executable>': the replacer exe (/tmp/dotnetup.exe) inherits the pipes to stdin/stdout/stderr. 'tmp/dotnetup.exe' acquires a 'FileShare.None' lock on 'D/dotnetup.update.lock' with 'OpenOrCreate', 'ReadWrite', 'FileShare.None':

```cs
FileStream updateLock = new(
    lockPath,
    FileMode.OpenOrCreate,
    FileAccess.ReadWrite,
    FileShare.None);
```

Once the replacer exe has responded OK (ensure the pid and such match), 'dotnetup.exe' releases 'dotnetupFolderMutex' and exits, upon which 'dotnetupFolderMutex' should be immediately acquired by 'tmp/dotnetup.exe'. If 'dotnetup.exe' fails to get the heart-beat, it fails with a specific error 'DotnetupReplacerCommunicationFailure' type (product error) and tries to kill the 'tmp/dotnetup.exe' process just in case it misbehaved. If 'tmp/dotnetup.exe' cannot acquire the mutex, it should fail mentioning there is another install in place (point to the file).

It renames 'dotnetup.exe' first to ensure the app had properly exited and nobody restarted it. Executables on Windows can be renamed even while they are running; though one side effect of this, is that the future dotnetup processes will use the newer version to spawn child processes or load external assets - including the short-term telemetry drainer.

`Environment.ProcessPath` should be cached and accessible from a singleton upon `dotnetup` startup as its behavior is undefined [if the executable is renamed or deleted before [ProcessPath] property is first accessed](https://learn.microsoft.com/dotnet/api/system.environment.processpath?view=net-10.0#remarks).

'tmp/dotnetup.exe' will release  'D/dotnetup.update.lock' - it will then run a similar heart beat hand off with the new 'D/dotnetup.exe' with a hidden command, 'dotnetup self replaced <temp folder location to delete, aka this executable>' - if it cannot get a response from 'D/dotnetup.exe' stating 'D/dotnetup.exe' got the lock on 'D/dotnetup.update.lock', it will try to delete 'D/dotnetup.exe' and rename 'D/dotnetup.exe.old' back to 'D/dotnetup.exe' and report an error - if it cannot delete 'D/dotnetup.exe' then it must be in use but unresponsive - dotnetup should ask the user to reinstall dotnetup themselves as the new process is not communicating properly but cannot be unwound; otherwise, assuming the hand-off went smoothly, it will release 'dotnetupFolderMutex' and close, in which it should signal to 'dotnetup.exe' (the new executable) that it can acquire 'dotnetupFolderMutex'.

'dotnetup.exe' (now the new executable) will try to delete the 'tmp/dotnetup.exe' and 'dotnetup.exe.old' and report success with it's version binary, along with an aka.ms link to how to install older versions and close, releasing both locks. If it cannot delete 'dotnetup.exe.old' do not treat it as a failure, assume there was a dotnetup process running before 'self update' started and it will close out - the next time self-update runs this can be cleaned up properly, assuming the user isn't in the same situation; if they are, a failure message would then prevent this situation from continuing at the beginning when it'd fail to clean up the 'dotnetup.exe.old' and could point to a specific process-id that's still holding that file. This prevents breaking apps reliant on 'dotnetup' to be running; e.g. when a customer has their machine off for over a month and tries to run dotnetup self update but has some persisted dependent application such as vscode holding the dotnetup executable; other apps can chose how to make this visibly actionable (e.g. close your apps using 'dotnetup' when possible.)

None of this process may be `await`ed as mutexes are not safe to use in an asynchronous context. The point of the `lock` file is to prevent corruption - e.g. if another updater process got the global folder mutex, it still wouldn't be able to actually delete / rename the dotnetup processes because it wouldnt have the file lock. This prevents another update process from deleting the actual valid dotnetup.exe.old at the start of its process.

#### Alternatives Considered:

Windows also has reboot-delayed renames, but this provides a poor experience for immediate updates as it requires a reboot.

Instead of using 3 process hand-offs, the new copy could never run and the tmp/dotnetup.exe could rename the new one and report success, but this prevents automatic clean up of tmp/dotnetup.exe and does not validate that the new executable is actually valid and able to run or not as part of the process.

#### Power/Restart Crash Safe Behavior:

'tmp/dotnetup.exe' does an atomic replacement:

```cs
File.Replace(
    sourceFileName: stagedPath,
    destinationFileName: installedPath,
    destinationBackupFileName: backupPath,
    ignoreMetadataErrors: false);
```

This is to prevent a problem in the immediate term if power is lost or the app crashes after renaming the old dotnetup but before renaming the new dotnetup. We did not move forward with this as this requires a hack for telemetry draining as well as waiting for all dotnetup processes to finish before self update can run.

In the outlined steps above, instead of renaming, 'dotnetup.exe' gets replaced with a backup file of 'dotnetup.exe.old'. 'dotnetup.exe' is replaced by 'dotnetup.exe.new' into 'dotnetup.exe'. If we simply did a rename, then if the app got killed/crashed mid update then no 'dotnnetup' executable would exist.

##### Future `dotnetup` behavior for all executables:

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

##### Future `dotnetup` drain process executables:

Until the OTelemetry fixes are complete, `dotnetup` uses a process drainer. The process drainer runs for a while after `dotnetup` exits which may cause confusing behavior when trying to run `dotnetup self update` as the update would be blocked by the telemetry drainer.

Short-term:

Telemetry drain processes must be launched via a temp copy of dotnetup.

#### Comparisons

'rustup' - It does use a separate updater process but two concurrent self udpates can interfere. This makes mores sense for rustup but less for us as mentioned prior.

'Aspire CLI' - It does not use a separate updater process; it renames itself and copies the old into the new location, runs 'aspire.exe --version' with a redirected output, waits for it, and treats 0 as verification. Cleanup is left for a later invocation. This seems simpler and possibly less bug or process/file contention prone, however,  it is not concurrency safe.

'VS Code' - It is concurrency safe as it has a Node IPC-like Mutex and an in-process state machine from `Idle` to `Checking` to `Downloading` and so forth. We don't need to have this complex of a state layer as we are not managing UI and several instances with many sub host processes, and we don't need to use something as mature as Inno setup as `dotnetup` is a user level dev tool.

(TODO _ can we elminate a 3rd process if we defer deletion of the tmp folder like aspire does )


## Linux:

The running executables can rename themselves which removes the need for a secondary lock file or third process. The global mutex can be held, while the existing executable downloads the new one into temp, verifies it, mvs it, verifies if it responds from D/dotnetup.new,  if so, unlinks/deletes itself, renames the new, and exits.

(TODO: make this more concrete.)

TODO: review this doc with ai to confirm or deny approach

todo: understand code path changes

# Update As a Version Swap Mechanism

Once the releases-index and releases.json files are available, the version to download can be repointed as 'dotnetup self install <channel or version>' and use the same semantics as an 'update'.

# Implementation

DotnetArchiveDownloader -> rename -> DotnetDownloader

DotnetArchiveDownloader in V1 (preview) can use 'ResolveBlobFeedEntry' and use the same unsigned warning and only update off daily channels since that's what exists. We can show progress and download using everything else we already do.

# Release Stable VS Preview

ResolveManifestEntry will resolve an index of dotnetup releases similar to the .NET release manifest.
The manifest will be signed just like the .NET artifacts manifests, with a detached signature, which will be downloaded as well and be used to validate dotnetup's own executable. We could only have an index but supporting multiple versions or allowing a downgrade/revert will only be possible if we maintain separate indexes. Whether we have a 'daily' 'preview' 'stable' keyed index or a 'major.minor' keyed index is not part of this spec.
