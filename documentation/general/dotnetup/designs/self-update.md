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

- `non-safe` processes must never execute their command body across a `self update` boundary. A `non-safe` process that starts while `self update` is running waits at its gate rather than failing outright, so `dotnetup list` and IDE-issued commands do not hard-fail during an update. If the executable was replaced while it waited, it must forward the invocation to the updated executable and return that process's exit code; it must never resume running its own now-stale code.

- `self update` does NOT make other `self update` processes fail or exit immediately; other `self update` processes must merely wait for the other update processes to complete and then determine that an update is no longer needed, assuming no release occurs within the time frame of the race.

- At this time, the only 'safe' `dotnetup` processes to have running during `self update` are the `telemetry drain` process, `dotnetup dotnet`, and the `self update` process itself. All other processes are `non-safe`. A process does not know its 'safety' status until `S.CL` parsers or `args` are processed.

- `dotnetup` must not require a reboot to update itself.

- It's ok to ignore a 'rogue' `dotnetup` process and allow them to fail or incur behavioral runtime bugs; e.g. an old `dotnetup` version that does not know about or support any mutex, semaphore, or locks and therefore bypasses the conditional guarantees. This is permissible because `dotnetup` is not yet `stable` or in a fully public `preview`.

# Self Update Broad Approach

## Windows:

#### Final Proposed Update Logic:

##### Locks

Self update is guarded by two lock files in `D/`. Both are permanent, zero-length files created on first use and never deleted.

`install`, `uninstall`, `update`, and the other manifest-mutating commands continue to use the `ModifyInstallationStates` mutex for their own critical sections and no modifications are necessary to this logic.

Each file is a reader/writer lock rather than a flag, so its meaning depends on which side is held.

| File | Shared ownership means | Exclusive ownership means |
| --- | --- | --- |
| `D/dotnetup.activity.lock` | "I am a running `non-safe` command" | "no `non-safe` command transaction is running, and none may start off their existing executable" |
| `D/dotnetup.update.lock` | nothing; it is opened shared only to observe whether an exclusive owner exists | "a self-update transaction is in flight" |

Who holds what, and for how long:

| Participant | Lock | Mode | Held for |
| --- | --- | --- | --- |
| `non-safe` command | `activity` | shared | from just after parse until the process exits |
| `non-safe` command | `update` | shared | a momentary probe, released on the next line |
| `self update` parent | `update` | exclusive | transaction start until it is released for the handoff |
| `self update` parent | `activity` | exclusive | transaction start until the replacer confirms it holds `update`, then released as the parent exits |
| replacer | `update` | exclusive | the handoff until rename, verification, and any rollback have completed |
| replacer | `activity` | — | never acquired |
| `safe` commands, `dotnetup dotnet`, telemetry drain | both | — | never acquired; these skip the gate entirely |

Because `dotnetup dotnet` and the telemetry drain hold nothing, a self update can complete underneath them.

They are `safe` only so long as they resolve any needed process-specific information at startup and globally cache it. In other words, they must cache `Environment.ProcessPath`.

```cs
// shared
new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);

// exclusive
new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
```

`FileShare` sharing semantics mean every open either succeeds or throws `IOException` immediately; no open blocks in the kernel. "Waiting" is always a retry loop with jittered backoff and a bounded timeout. `FileShare.Delete` is never requested on either file.

Three rules govern every participant:

1. The two sides acquire in opposite order, deliberately. A `non-safe` process takes `D/dotnetup.activity.lock` before it touches `D/dotnetup.update.lock`; `self update` takes `D/dotnetup.update.lock` before `D/dotnetup.activity.lock`.
2. While acquiring the pair, no participant blocks on one lock while holding the other. If the second acquisition fails, the first is released, the process backs off, and the pair is retried from the start. Holding a lock while doing work is expected — what is forbidden is holding one and waiting for the other, which is the only way the opposite ordering in rule 1 could deadlock.
3. Timeouts are asymmetric: a `self update` waiting on another `self update` waits generously; a `self update` waiting on a `non-safe` process waits briefly and then fails; a `non-safe` process waiting on a `self update` waits for the length of a typical update and then fails.

The parent-to-replacer handoff is the one deliberate exception to rule 2: the parent holds `D/dotnetup.activity.lock` while it waits for the replacer to acquire `D/dotnetup.update.lock`. That edge is safe only because rule 1 stops a newly launched `non-safe` process at the activity lock, so nothing else can be holding the update lock at that moment.

When an acquisition fails, dotnetup queries Windows Restart Manager to report the locking process and PID on a best-effort basis. This can be added with a `FileLockDetector`-style helper such as [commit `7fcc618e03f`](https://github.com/dotnet/sdk/commit/7fcc618e03f1520f688fa86bc7ade67aa417e380) via `RmRegisterResources`/`RmGetList` integration. The process may exit before it is reported, and failure to identify it does not change the result.

##### The gate (every `non-safe` command)

The gate runs in `CommandBase.Execute`, after the parser has determined the command's safety status and before any command body executes. Safety is a property of the command: `CommandBase` defaults every command to `non-safe`, and only `dotnetup dotnet`, the telemetry drain, and `self update` override that default. A newly added command is therefore gated unless someone deliberately exempts it.

1. Acquire `shared(D/dotnetup.activity.lock)`. Held until the process exits.
2. Acquire `shared(D/dotnetup.update.lock)`, then release it immediately.
3. If either open fails, release whatever is held, back off, and retry from step 1 until the timeout.

Only three things may happen before the gate: console encoding and UI language setup, capturing `Environment.ProcessPath` and the image identity, and parsing. Parsing must not read the manifest, enumerate `D/`, or touch the network — no `System.CommandLine` default-value factory, custom parser, validator, or completion source may do so — because command selection must not depend on state a concurrent update is changing. Reading environment variables, console redirection state, and paths outside `D/` is fine; `--interactive` and `--shell` already resolve their defaults that way, and a self update cannot affect either. The first-run telemetry notice is the only pre-gate write today; it targets the telemetry directory rather than `D/`, and its sentinel keeps it idempotent across a forward.

A process blocked at the gate has therefore touched no installation state and can forward or fail cleanly.

##### Forwarding after a completed update

A process that waited at the gate is still executing the old image, so it must not run its command body if the executable changed while it waited.

At startup each process records the file identity of its own image — the volume serial number and file index from `GetFileInformationByHandle` on the cached `Environment.ProcessPath`. After the gate succeeds, it compares that identity to `D/dotnetup.exe`:

- **Identity matches** — no replacement occurred (including the rollback case, because renaming the backup back preserves the original file identity). The command proceeds normally.
- **Identity differs** : It could forward instead of running. It starts `D/dotnetup.exe` with the original `args`, `UseShellExecute = false` and no stream redirection so the child inherits stdin/stdout/stderr, the same working directory, and the same environment plus an incremented `DOTNETUP_FORWARD_DEPTH`. It keeps its `shared` activity handle for the child's lifetime, waits, and returns the child's exit code via `SetExitCode`. This is subject to breaking if the new executable has a breaking change to the command terminology itself.

`D/dotnetup.exe` is resolved as the canonical, dotnetup-owned path and is not followed through an unexpected symbolic link or reparse point. Forwarding is capped at `DOTNETUP_FORWARD_DEPTH` of 2; beyond that the command fails rather than hopping again. The forwarding process emits a telemetry event for the forward and does not emit a command-completion event, since the child emits its own.

Forwarding is deferred to a later implementation. Until it lands, a process whose image changed while it waited fails and tells the user to re-run the command; it still never executes its own stale command body.

##### `self update` (parent)

1. Acquire `exclusive(D/dotnetup.update.lock)`. Busy means another `self update` is running: back off and retry, then re-evaluate whether an update is still needed. This step never fails the user.
2. Acquire `exclusive(D/dotnetup.activity.lock)`. Busy means a `non-safe` process is running: release the update lock, back off, and retry the pair. On timeout, fail with `dotnetupBusy` and the locking PID.
3. Remove any stale `D/dotnetup.exe.new` and do a best-effort delete of all `tmp/dotnetup/*/` folders and all `dotnetup.exe.old.*`. `tmp/` cleanup is best-effort but failure to delete `dotnetup.exe.old` would cause `InsufficientPermissionsToUpdate`.

The original dotnetup process, say in dotnetup's official install directory, `D/`, assuming that there is a new version of `dotnetup` to update to (it can compare its assembly version with the prescribed `dotnetup` channel version), then downloads a new version of `dotnetup` into a randomly named, current-user-only temporary directory, say `tmp/dotnetup/<random>/dotnetup.exe`. If there is no new version, it releases both locks and exits.

After copying the downloaded artifact to `D/dotnetup.exe.new`, dotnetup validates the staged copy.

Backups use transaction-specific names such as `D/dotnetup.exe.old.<transaction-id>` so a locked backup from an older process does not prevent staging a later update. A failure to remove an artifact required by the current transaction returns `InsufficientPermissionsToUpdate` with any available locking-process details.


The state of the file system would be:
```
`D/dotnetup.exe.new` <- replacement file
`D/dotnetup.exe` <- old file

`tmp/dotnetup/<random>/dotnetup.exe` <- swapping throwaway executable
```

While still holding `exclusive(D/dotnetup.activity.lock)`, `dotnetup.exe` releases its handle to `D/dotnetup.update.lock`, starts `tmp/dotnetup/<random>/dotnetup.exe`, and performs a heartbeat handoff via the hidden command `dotnetup self replacement <tmp folder executable>`. The replacer inherits stdin/stdout/stderr and acquires the `FileShare.None` lock on `D/dotnetup.update.lock` before it responds that it is ready:

```cs
FileStream updateLock = new(
    lockPath,
    FileMode.OpenOrCreate,
    FileAccess.ReadWrite,
    FileShare.None);
```

Once the replacer has acquired `D/dotnetup.update.lock` and responded OK (including the expected PID and an unguessable handoff token), `dotnetup.exe` releases `D/dotnetup.activity.lock` and exits. Because the parent holds the activity lock until the replacer confirms it holds the update lock, at least one of the two locks is held continuously across the handoff. If `dotnetup.exe` fails to get the heartbeat, it fails with `DotnetupReplacerCommunicationFailure` (product error) and tries to kill the `tmp/dotnetup/<random>/dotnetup.exe` process. If the replacer cannot acquire `D/dotnetup.update.lock` within its own timeout, it reports failure and exits without touching any executable.

`tmp/dotnetup/<random>/dotnetup.exe` renames `dotnetup.exe` first to ensure the app had properly exited and nobody restarted it. Executables on Windows can be renamed even while they are running; though one side effect of this, is that the future dotnetup processes will use the newer version to spawn child processes or load external assets - including the short-term telemetry drainer.

`Environment.ProcessPath` should be cached and accessible from a singleton upon `dotnetup` startup as its behavior is undefined [if the executable is renamed or deleted before [ProcessPath] property is first accessed](https://learn.microsoft.com/dotnet/api/system.environment.processpath?view=net-10.0#remarks).

`tmp/dotnetup/<random>/dotnetup.exe` keeps `D/dotnetup.update.lock` while it launches `D/dotnetup.exe --build-identity` to verify the replacement.

`--build-identity` is a hidden **option** on the root command, not a subcommand. Like the built-in `--version`, its action runs during `ParseResult.Invoke` and returns before any `CommandBase` is constructed, so it never reaches the gate. That is load-bearing rather than incidental: the replacer holds `D/dotnetup.update.lock` exclusively while the child runs, so a gated child would block on its own update-lock probe and every update would fail. If the verification path ever becomes a subcommand it must be classified `safe`.

The option reads `AssemblyInformationalVersionAttribute` from the loaded assembly, exactly as `Parser.Version` already does, and writes only that identity to stdout. It must never read the version off disk with `FileVersionInfo` against `Environment.ProcessPath`, which would fail once the executable has been renamed. It disables telemetry and spawns no detached child processes. The replacer waits synchronously with a timeout and succeeds only if the process exits with status `0` and reports the exact expected build identity. It then reports success, includes an aka.ms link describing how to install older versions, and releases the update lock.

If `tmp/dotnetup/<random>/dotnetup.exe` cannot run `D/dotnetup.exe`, the command does not return `0`, or the reported version does not match the expected version, it will attempt rollback while still holding the update lock: delete `D/dotnetup.exe`, rename the current transaction's `D/dotnetup.exe.old.<transaction-id>` back to `D/dotnetup.exe`, and report an error. Rollback restores the original file identity at the canonical path, so a `non-safe` process waiting at its gate resumes normally instead of forwarding. If it cannot delete `D/dotnetup.exe`, then the new executable may be in use but unresponsive; dotnetup should ask the user to reinstall dotnetup because the update cannot be unwound safely. Otherwise, assuming the hand-off went smoothly, it will release the update lock and close.

`dotnetup.exe` (now the new executable) performs best-effort cleanup of prior `tmp/dotnetup/<random>/` directories and `dotnetup.exe.old.*` backups on later launches. Cleanup is age-bounded and never follows symbolic links or reparse points outside the owned update directories. Failure to delete a locked old executable is not an update failure because a process started before `self update` may still be using it.

Abrupt termination can leave temporary directories or old backups behind indefinitely if dotnetup is never run again. Transaction-specific names prevent those stale files from corrupting or unnecessarily blocking a later update; subsequent launches retry cleanup on a best-effort basis.

This prevents breaking apps reliant on `dotnetup` to be running; e.g. when a customer has their machine off for over a month and tries to run dotnetup self update but has some persisted dependent application such as vscode holding the dotnetup executable; other apps can chose how to make this visibly actionable (e.g. close your apps using `dotnetup` when possible.)

`FileStream` lock ownership is handle-based rather than thread-affine, so both locks may be held across an `await` and the whole transaction — download included — can be asynchronous. This is the reason `self update` does not use the thread-affine `ScopedMutex`.

In the event of a crash/pkill/outage, users can safely run the get-dotnetup scripts to redownload the `dotnetup` executable to the standard `D/` location.

#### Locking Rationale

**Why two lock files instead of a mutex such as `ModifyInstallationStates`.**
A named mutex has exactly one owner, so it cannot represent "N `non-safe` processes are alive"; holding it proves only that nobody is inside a critical section at that instant, which does not cover a `dotnetup sdk install` that is downloading an archive. It also has no fail-if-held semantics, so a contending process blocks rather than reacting, and it is thread-affine, so it cannot be held across an `await`. Handle-based `FileShare` locks give all three properties, and the OS closes the handles if a process is killed, so there is no abandoned-state ambiguity to reason about.

**Why a `non-safe` process takes the activity lock first.**
 Mutual exclusion requires that a `non-safe` process, once it has passed its update check, never reaches an instant where it holds neither lock. Retaining the activity lock and then probing the update lock satisfies this. Probing the update lock, releasing it, and only then acquiring the activity lock does not: an updater can acquire both inside that gap, and both sides pass their checks.

Acquiring the update lock, acquiring the activity lock while still holding it, and then releasing the update lock is also correct, but it lets a newly launched `non-safe` process hold the update lock during the parent-to-replacer handoff, where it can knock over the replacer's exclusive acquire — the single most safety-critical open in the design. It also closes a three-party cycle once the `non-safe` side waits rather than fails: the newcomer holds the update lock and waits on the activity lock, the parent holds the activity lock and waits on the replacer, and the replacer waits on the update lock.

**Why `self update` takes them in the reverse order.**
The opposite ordering is what makes the pair of checks race-free: each side retains its own lock before testing the other's, so neither can slip through a gap in the other's sequence. It is also why `self update` has no reason to open the activity lock shared first — a shared open succeeds while any number of `non-safe` processes hold it shared, so it would answer nothing. Only the exclusive open establishes that no `non-safe` process is running.

**Why nobody waits for one lock while holding the other.** Because `non-safe` processes wait rather than fail, hold-and-wait during acquisition would produce a two-party cycle regardless of ordering: the gate would hold the shared activity lock while waiting on the update lock, and the `self update` parent would hold the exclusive update lock while waiting on the activity lock. Releasing everything between retry attempts removes that edge. The handoff is the only place a participant waits while holding, and rule 1 is what keeps it from closing a cycle.

**Why the update lock stays updater-only.** Contention on the update lock means a peer `self update`, which must be waited out and never failed. Contention on the activity lock means a `non-safe` process, which is waited on briefly and then failed. If `non-safe` processes retained the update lock instead of probing it, those two cases would be indistinguishable and the required policies could not both be honored.

**Why forwarding instead of resuming.** A process that waited at the gate is still executing the old image. Resuming would run pre-update code against post-update state — for example a manifest written in a format the old code does not understand. Forwarding is only legal at the gate precisely because nothing has been mutated and nothing has been written to the console yet.

**Why `FileShare.Delete` is never requested.** A lock file that can be deleted while it is open allows a second process to create a new file at the same path, at which point two processes each hold "exclusive" access to different files. The lock files are permanent fixtures; leaving them behind costs nothing. For the same reason the locks are files under the per-user `D/` rather than `Global\` named objects, which another session can squat.

#### Comparisons

`rustup` - Rustup [downloads and launches a separate updater](https://github.com/rust-lang/rustup/blob/main/src/cli/self_update.rs), but its self-update path has no cross-process update lock, so two concurrent self-updates can interfere with the shared updater, installed executable, and proxy links. Its process handoff addresses Windows executable locking, not update serialization or crash-atomic replacement.

`Aspire CLI` - Aspire's archive self-update [renames the running executable to a timestamped backup, copies the extracted new executable to the canonical path, runs `aspire.exe --version`, and rolls back caught failures](https://github.com/microsoft/aspire/blob/main/src/Aspire.Cli/Commands/UpdateCommand.cs). Cleanup is best-effort on later invocations. The replacement sequence has no cross-process update lock, so concurrent self-updates can race over the canonical path and backups. Dotnetup adopts exact-version validation and deferred cleanup, but adds cross-process serialization.

`VS Code` - VS Code's installed Windows updater combines a singleton main process, an [in-process update state machine](https://github.com/microsoft/vscode/blob/main/src/vs/platform/update/electron-main/abstractUpdateService.ts), native application/setup/updating/ready mutexes, staged versioned files, and [Inno Setup](https://github.com/microsoft/vscode/blob/main/build/win32/code.iss). This serializes Windows installers and blocks application startup during the final switch. The statement does not apply uniformly to every distribution: macOS delegates to Electron's updater, while ordinary Linux packages generally delegate installation to the package manager or download page. Dotnetup does not require VS Code's UI state machine or installer framework, but it adopts the narrower invariant that only one self-update transaction may modify its executable at a time.

## Linux:

Linux permits a running executable's pathname to be replaced while the process continues executing the old inode, removing the need for a secondary replacer process. The lock protocol, the gate, and forwarding are identical to Windows; only the replacement step differs. Dotnetup downloads and authenticates the artifact in a secure temporary directory, then copies it to `D/dotnetup.new`, sets the expected executable mode, flushes it to disk, and validates the staged copy. It refuses to update through an unexpected symbolic link and operates on the canonical, dotnetup-owned install path. While holding both locks, it creates a transaction-specific backup hard link and performs a same-filesystem move over `D/dotnetup`. It runs `D/dotnetup --build-identity` and requires status `0` plus the exact expected identity; otherwise it atomically moves the backup path over the canonical path. Because there is no replacer process, the updating process holds both locks from start to finish and the image-identity check uses the device and inode number from `stat`.

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
