# Self Update

`dotnetup self update [--no-progress]` updates the published NativeAOT `dotnetup`
executable in place. Stage A is implemented; waiting and transparent forwarding of
ordinary commands remain Stage B work. See [Stage A Status](#stage-a-status) and
the [review guide](#review-guide) for the source map and environment boundaries.

The current command resolves the daily release for the selected RID. It does not
accept a channel or version argument. See [command usage](../reference/dotnetup.md#self-update).

`dotnetup update` already updates all of the installs managed by dotnetup. Using `self update` as the key noun matches `dotnetup sdk update` nomenclature. `dotnetup update` will continue to update only the .NET SDK and .NET Runtime installs.

# Trade-offs

On Windows, `dotnetup` can pick one approach:

1. Reboot Approach:

To require a reboot to update and replace. This simplifies logic because replacement can occur before the executable is loaded, and it provides stronger recovery options through the installer. It is a poor experience for a developer tool.

2. `MoveFileExW` Approach:

`MoveFileExW(stagedPath, installedPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)` can move a staged executable over the canonical path in one call after the canonical executable is no longer running. `MOVEFILE_WRITE_THROUGH` asks Windows not to return until the move has completed on disk, but it does not make the operation an ACID transaction or provide a documented all-or-nothing guarantee across power loss.

Because replacing the still-running destination with `MoveFileExW` cannot be relied upon, this approach requires a temporary replacer process outside `D/`, a handoff from the original process, and draining every process that has the canonical executable open incompatibly. `MoveFileExW` also does not create a backup as part of the move, so preserving the old executable requires a separate operation.

3. `File.Replace` / `ReplaceFileW` Approach:

`File.Replace(stagedPath, installedPath, backupPath)` maps to the Windows `ReplaceFileW` API. It combines replacement of the canonical path and creation of a backup in one operating-system call. Windows permits this operation while the old executable image is running when existing handles allow delete sharing; the running process continues executing the old image while future launches resolve the replacement.

This avoids deliberately splitting the forward replacement into two `File.Move` calls; it does not guarantee an uninterrupted canonical name. Windows measurements observed a brief file-not-found interval even within `File.Replace`, so consumers must retry transient launch failures. It is not an ACID or power-loss-safe transaction: `ReplaceFileW` documents partial failure states, and its `REPLACEFILE_WRITE_THROUGH` flag is unsupported. The staged executable is flushed before replacement, and recovery accounts for the staged, canonical, and backup paths after failure. See [replacement properties](#properties-of-algorithms-1-and-2).


### Cross Update Boundary Trade-Offs

Allowing `dotnetup runtime install` to run across either replacement operation remains unsafe because old code may encounter installation state written by a newer manifest format. The activity gate described below excludes those `non-safe` processes while allowing explicitly `safe` processes to continue running.

Existing safe processes may still change behavior if they resolve paths or load external assets after replacement, so every safe process must cache values derived from its loaded image at startup.

## Selected Approach
For `dotnetup`, `3` is the best selection.

For `1`, requiring a reboot would interrupt developers, and dotnetup is a developer tool; a reboot-style approach is best served for system level applications or applications managed by IT.

For `2`, the temporary replacer and process-draining protocol add a handoff without producing a transactional power-loss guarantee. `MOVEFILE_WRITE_THROUGH` improves completion durability but does not remove the need for an independently created backup or recovery logic.

For `3`, no separate replacer is needed. The original process can call `File.Replace` against its own canonical path, retain the update locks through verification and rollback, and allow explicitly safe processes such as the telemetry drainer to continue executing their already-loaded image. IDEs or other tools may also invoke unattended updates concurrently; the update lock serializes those callers.

To clarify, this does not mean that updates to dotnetup itself execute concurrently. Racing update commands wait for the update lock, re-evaluate the installed identity after acquiring both locks, and exit successfully when no update remains to apply.

`dotnetup` is easily and quickly re-installed via the script if an outage occurs.

#### Concurrency Trade-Offs

Another contention is whether to have mutex or inter-process (i.e. several process) aware logic; should `dotnetup` gracefully succeed when multiple updates are attempted at once or simply reject the premise and fail?

`Aspire` and `rustup` are not concurrency safe during update procedures and they also do not block such an action explicitly.

`dotnetup` should be concurrency safe. `dotnetup` should also allow multiple callers to invoke it at the same time to configure/install runtimes, so it should not run an exclusive lock on itself at all times as this would delay progress and other apps unnecessarily.

# Success Criteria

## Stage A Success Criteria

Stage A reduces initial implementation complexity and scopes bugs to the first set of restrictions. Waiting and transparent re-running of `non-safe` commands are deferred to Stage B; the selected design must preserve the ability to add that behavior without replacing the locking protocol.

- Abrupt termination or power loss may leave the canonical executable unavailable. Consumers must be able to re-acquire it using the [installation scripts](https://aka.ms/dotnet/dotnetup). The protocol does not promise crash-atomic or power-loss-atomic recovery.<br><br>


- Recovery is designed to retain identifiable backups and avoid overwriting unknown files. Self-update does not migrate SDK/runtime installation state. Reinstallation is the fallback when recovery cannot establish a runnable canonical executable; these rules are not a guarantee against arbitrary filesystem damage after power loss.<br><br>

- Multiple `dotnetup` processes in general must be able to execute at the same time.<br><br>

- `dotnetup` may leverage asynchronous code or `await`.<br><br>

- `self update` must not run if any `non-safe` `dotnetup` process is currently running. e.g. if `dotnetup sdk install` is running, the manifest format may change from one version to another; installing a new version that may edit the manifest format may cause the old `dotnetup` process to fail, and we want an invariant that avoids any such bugs.<br><br>

- `non-safe` processes must never execute their command body across a `self update` boundary. In Stage A, a newly started `non-safe` process fails immediately at its gate if a self update is in progress. If its executable was replaced before it passed the gate, it fails and instructs the caller to re-run the command; it must never execute its now-stale command body.<br><br>

- Stage A documentation must warn callers that `dotnetup` commands classified as `non-safe`, including `dotnetup --info`, may fail while a self update is running. Callers are responsible for retrying after the update completes.<br><br>

- `self update` does NOT make other `self update` processes fail or exit immediately; other `self update` processes must merely wait for the other update processes to complete and then determine that an update is no longer needed, assuming no release occurs within the time frame of the race.<br><br>

- At this time, the only 'safe' `dotnetup` processes to have running during `self update` are the `telemetry drain` process, `dotnetup dotnet`, and the `self update` process itself. All other processes are `non-safe`. A process does not know its 'safety' status until `S.CL` parsers or `args` are processed.<br><br>

- `dotnetup` must not require a reboot to update itself.<br><br>

- It's ok to ignore a 'rogue' `dotnetup` process and allow them to fail or incur behavioral runtime bugs; e.g. an old `dotnetup` version that does not know about or support any mutex, semaphore, or locks and therefore bypasses the conditional guarantees. This is permissible because `dotnetup` is not yet `stable` or in a fully public `preview`.

## Stage B Success Criteria

Stage B retains the Stage A safety restrictions and replaces its fail-fast behavior for `non-safe` processes with waiting and transparent re-running by default. Concurrent `self update` calls wait in both stages; that serialization is included in Stage A because it is less complex than transparently re-running arbitrary commands.

- `self update` also does NOT make other `non safe` processes fail or exit immediately unless they are configured to do so. This is because we don't want others who call `dotnetup --info` to have to worry about whether another process is running `self update` or have to write recovery logic for this. However, others may opt in to this behavior if they want minimal latency and would rather defer the task if an update is running.<br><br>

- A `non-safe` process that starts while `self update` is running waits at its gate rather than failing outright by default, so `dotnetup list` and IDE-issued commands do not hard-fail merely because an update is in progress. If the executable was replaced before it passed the gate, it must transparently forward the invocation to the updated executable and return that process's exit code; it must never resume running its own now-stale code. If breaking changes are made to command names themselves, then this will break and that is acceptable.<br><br>

# Self Update Broad Approach

The approach below applies to both stages unless noted. Stage A uses immediate failure at the `non-safe` gate; Stage B adds bounded waiting and forwarding using the same locks and image-identity check.

## Windows:

#### Stage A Update Logic

##### Definitions

Let `D/` be the directory containing the installed dotnetup executable, and let `D/dotnetup.exe` be that executable at its canonical path.

Let `t` be the transaction identifier: a random value unique to a single execution of `dotnetup self update`.

Let `D/dotnetup.exe.new.download` be the download temporary file, `D/dotnetup.exe.new` the hash-validated staged replacement, and `D/dotnetup.exe.old.<t>` the backup of the executable being replaced. All are siblings of `D/dotnetup.exe` on the same volume. [SelfUpdatePaths](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdatePaths.cs) derives these paths from the captured executable path.

Let `A` be the activity lock, the file `D/dotnetup.activity.lock`.

Let `U` be the update lock, the file `D/dotnetup.update.lock`.

`A` and `U` are permanent, zero-length files created on first use and never deleted.

Let `P` be the `dotnetup self update` process that Algorithm 2 outlines.

Let `N` be any `non-safe` dotnetup process.

Let `S` be any `safe` dotnetup process other than `P`: `dotnetup dotnet` and the telemetry drain process. `self update` is classified `safe` as well, but `P` follows the update-lock acquisition protocol rather than the ordinary command gate. Parser-only actions such as help, version, and the hidden identity option do not execute a command body and do not use that gate.

Let `V_channel` be the per-RID build ID resolved from the daily release, and let `V_installed` be the build ID read from the embedded record in `D/dotnetup.exe`. These are the equality tokens defined by the [build-identity contract](build-identity.md), not human-readable versions or filesystem file IDs.

Let `X_U`, `X_A`, `X_N`, and `X_V` be the bounded timeouts defined in rule 3 of Algorithm 1.

`A` and `U` are reader/writer locks rather than flags, so the meaning of each depends on which side is held.

| Lock | Shared ownership means | Exclusive ownership means |
| --- | --- | --- |
| `A` | "a `non-safe` command is running" | "no `non-safe` command is running, and none may start from the current executable" |
| `U` | unused; `U` is only ever opened exclusively | "a self-update transaction or best-effort cleanup owns the update artifacts" |

A shared lock is held via:

```cs
FileStream sharedLock = new(
    lockPath,
    FileMode.OpenOrCreate,
    FileAccess.Read,
    FileShare.Read);
```

An exclusive lock is held via:

```cs
FileStream exclusiveLock = new(
    lockPath,
    FileMode.OpenOrCreate,
    FileAccess.ReadWrite,
    FileShare.None);
```

`FileShare.Delete` is never requested on `A` or `U`.

Lock ownership during command execution and cleanup:

| Participant | Lock | Mode | Held for |
| --- | --- | --- | --- |
| `N` | `A` | shared | from the post-parse gate through command completion and telemetry flush |
| `N` | `U` | exclusive | optional cleanup only, after passing the gate and build-identity check; one nonblocking attempt |
| `P` | `U` | exclusive | acquired transaction through verification/recovery and telemetry flush |
| `P` | `A` | exclusive | acquired transaction through verification/recovery and telemetry flush |
| `S` | `A` | — | never acquired; skips the gate |
| `S` | `U` | none | current safe commands do not perform cleanup |

`A` alone excludes `N` from a transaction, because `P` holds `A` exclusively for the whole transaction and an `N` that has retained `A` shared prevents `P` from ever acquiring it. `U` serializes self-update transactions and cleanup against each other. `N` does not acquire `U` to pass the gate or retain `U` for its command lifetime; its optional cleanup follows step 2.9.

Because `S` holds neither lock, a self update can complete underneath it. `S` must cache image-derived values at startup, particularly the executable path and loaded build ID. Capturing a path does not identify the loaded image: the ID comes from the loaded record, never by reopening that path. See [Program](../../../../src/Installer/dotnetup.Library/Program.cs) and the [loaded-record contract](build-identity.md#access-and-update-gate).

`install`, `uninstall`, `update`, and the other manifest-mutating commands continue to use the `ModifyInstallationStates` mutex for their own critical sections. `P` does not acquire `ModifyInstallationStates`, and no modification to that logic is necessary.

##### Algorithm 1 — Lock acquisition and the `non-safe` gate

[ScopedLockFile](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ScopedLockFile.cs) uses runtime `FileStream` sharing: an acquisition returns a lease, returns null for recognized contention, or propagates another I/O failure. Contention is retried by the coordinator with bounded backoff; there is no additional native `flock` or lock-enforcement probe. These guarantees assume functioning runtime locks on a supported local filesystem, not bounded I/O latency on every filesystem.

**Rule 1 — `P` acquires `U` before `A`.** `U` is taken first so that `P` does not exclude every `N` while waiting out a peer `self update` or cleanup. `N` acquires only `A` at the gate; after passing its build-identity check, it may also hold `U` briefly for cleanup under step 2.9.

**Rule 2 — no hold-and-wait during acquisition.** `P` never blocks on `A` while holding `U`. If the acquisition of `A` fails, `P` releases `U`, backs off, and retries the pair from step 1.1. An `N` waiting at the gate holds neither lock. An `N` that already holds `A` may try to acquire `U` once for cleanup, but skips cleanup immediately if that attempt fails. Cleanup never acquires or waits for additional locks while holding `U`, and releases `U` before continuing command execution.

**Rule 3 — asymmetric timeouts.**

| Timeout | Applies to | Magnitude |
| --- | --- | --- |
| `X_U` | `P` waiting on `U` held by a peer `self update` or cleanup | two minutes of cumulative contention |
| `X_A` | `P` waiting on `A` held by `N` | two seconds of cumulative contention |
| `X_N` | `N` waiting at the gate during a self update (Stage B only) | the length of a typical update |
| `X_V` | `P` waiting for the verification child of step 2.6 | 15 seconds |

Cleanup does not use these retry timeouts: it makes one nonblocking attempt to acquire `U` and skips cleanup if ownership cannot be established.

###### Lock Acquisition for `P`

**1.0 — `P` checks for an available update before acquiring any lock.** `P` resolves `V_channel` and compares it with the build ID read from the canonical executable. If the two are equal, `P` exits successfully without acquiring `U` or `A`.

The canonical identity read is advisory: it is never the basis for replacing or deleting an executable. A failed read, including an observation of the step 2.4 replacement window, falls through to step 1.1 and the authoritative check under both locks. Release resolution is different: a network or release-metadata failure stops the command before either lock is acquired. The resolved release, including `V_channel`, is pinned for the rest of the transaction. See [SelfUpdateWorkflow.Execute](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs).

Reading the canonical build ID without holding `U` is acceptable here and is not in step 2.9, because the two reads gate different actions. Step 2.9 uses the value to delete backups, which is irreversible, so it reads under `U`. Step 1.0 uses the value only to decide whether to continue; a transiently absent or unreadable canonical path cannot authorize replacement or cleanup.

Step 1.0 exists because the common invocation is a poll that finds nothing to do. Without it, every such poll takes `A` exclusively, excludes every `N` on the machine for the duration of the channel lookup, and can fail with `DotnetupBusyWithAnotherCommand` having accomplished nothing. A `self update` run with no network connectivity likewise fails without blocking any other command.

The check is placed before both locks rather than between steps 1.1 and 1.2 deliberately. Resolving `V_channel` is a network operation, so performing it while holding `U` would stretch the interval between acquiring `U` and acquiring `A` from two file opens to a network round trip. More `N` processes would accumulate in that interval, `P` would fail step 1.2 more often and restart the pair under rule 2, and the longer hold on `U` would cause the single nonblocking cleanup attempt of step 2.9 to be skipped more often.

**1.1 — `P` acquires `U` exclusively.** A busy `U` means a peer `self update` or cleanup holds `U`. `P` backs off and retries for up to `X_U`, then re-evaluates whether an update is still required after acquiring both locks. `P` does not fail immediately for contention on `U`; on expiry of `X_U`, `P` fails with `DotnetupBusyWithUpdateOrCleanup` and reports that another update or cleanup holds the lock, with best-effort holder details from step 1.7.

**1.2 — `P` acquires `A` exclusively.** A busy `A` means at least one `N` is running. Per rule 2, `P` releases `U`, backs off, and retries the pair from step 1.1 for up to `X_A`. On expiry of `X_A`, `P` fails with `DotnetupBusyWithAnotherCommand` and reports the locking PID per step 1.7.

###### Lock Acquisition for `N`

**1.3 — `N` passes the gate.** The gate executes in `CommandBase.Execute`, after the parser has determined the safety status of the command and before any command body executes.

`N` acquires `A` shared and holds `A` until `N` exits. In Stage A, if the open fails because `A` is busy, `N` fails immediately, reports that a self update is in progress, and instructs the caller to re-run the command after it completes. In Stage B, `N` instead backs off and retries for up to `X_N`, unless the caller has opted into immediate failure. On expiry of `X_N`, `N` fails and reports that a self update is in progress.

A busy `A` unambiguously means a transaction is in flight, because `A` is only ever held exclusively by `P`; another `N` holding `A` shared does not block this one.

Safety is a property of the command: `CommandBase` classifies every command as `non-safe` by default, and only `dotnetup dotnet`, the telemetry drain, and `self update` override that default. A newly added command is therefore gated unless someone deliberately exempts it.

**1.4 — `N` verifies build identity.** After acquiring `A` shared, `N` compares its cached `DotnetupBuildIdentity.Current`, read from its loaded image, with `V_installed`, read directly from the canonical executable's embedded record while retaining `A`. The [build-identity contract](build-identity.md) defines the record, automatic build generation, and offline reader. This check launches no child and never uses a pathname lookup to identify the loaded image.

The comparison is unconditional, even if acquiring `A` succeeded immediately: replacement may have completed before `N` reached the gate. `N` retains `A` through its command body or forwarding so the canonical build cannot change underneath the check. Missing, malformed, duplicate, unsupported, or unreadable records stop the command; unknown identities never compare equal.

- **Identity matches.** The loaded and installed builds agree, including after rollback to the loaded build. `N` proceeds to the command body.
- **Identity differs.** The loaded build is no longer installed, so `N` must not execute its command body. In Stage A, `N` fails and instructs the caller to re-run the command. In Stage B, `N` forwards per step 1.5.

**1.5 — `N` forwards to the replaced executable (Stage B only).** `N` starts `D/dotnetup.exe` with the original `args`, `UseShellExecute = false`, and no stream redirection, so the child inherits stdin, stdout, and stderr, the working directory of `N`, and the environment of `N` plus an incremented `DOTNETUP_FORWARD_DEPTH`. `N` retains the shared handle on `A` for the lifetime of the child, waits for the child, and returns the exit code of the child via `SetExitCode`.

`D/dotnetup.exe` is resolved as the canonical, dotnetup-owned path and is not followed through an unexpected symbolic link or reparse point. Forwarding is capped at a `DOTNETUP_FORWARD_DEPTH` of 2; beyond that `N` fails rather than hopping again. `N` emits a telemetry event for the forward and does not emit a command-completion event, because the child emits one. Forwarding breaks if the replacement executable renamed or removed the command that `N` was invoked with.

Forwarding is deferred to Stage B. `N` never executes the stale command body of `N` in either stage.

**1.6 — Work permitted before the gate.** Native startup captures the executable path and the build ID from the loaded record, configures language and console output, and parses arguments. Command constructors must not access installation state or start network work. [Program](../../../../src/Installer/dotnetup.Library/Program.cs) creates a [SelfUpdateInvocation](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateInvocation.cs) only for the NativeAOT host with a captured executable path. Managed development/test hosts do not receive an invocation; `self update` rejects them rather than treating the managed `dotnet` host as the executable to replace.

Parsing must not read the manifest, enumerate `D/`, or touch the network.

`N` must not perform cleanup before passing both the gate and the build-identity check, or on a path that fails or forwards instead of executing its command body.

The first-run telemetry notice and its sentinel are not pre-gate work. [CommandBase.Execute](../../../../src/Installer/dotnetup.Library/CommandBase.cs) enters the gate before option telemetry or the command body. Telemetry starts after admission, or in error handling after a gate rejection; failure reporting may therefore write telemetry, but a rejected command never runs its body or cleanup. Parser-only actions have no command gate; the identity action is also telemetry-free. Acquired invocation leases survive command completion, root telemetry completion, and synchronous `FlushTelemetry`, and are disposed only as `Main` returns.

###### Common to `P` and `N`

**1.7 — Reporting the lock holder.** [SelfUpdateLockDiagnostics](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateLockDiagnostics.cs) uses Windows Restart Manager for best-effort process-name/PID details on required lock failures. Optional cleanup does not report contention. The helper verifies process start time before reporting a PID and does not expose command lines or full paths. Missing details, unsupported platforms, or a process exiting during inspection do not change the lock result; there is no Unix holder lookup.

##### Algorithm 2 — The update transaction

Algorithm 2 begins once `P` holds both `U` and `A` per steps 1.1 and 1.2. [SelfUpdateWorkflow](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) transfers acquired leases to the invocation, which retains them through telemetry flush on success or failure. `P` performs replacement and recovery itself; the only replacement-related child is the verification process.

**2.1 — `P` determines whether an update is required.** `P` reads `V_installed` from the canonical executable under both locks and compares it with `V_channel`, not with `P`'s own loaded build ID. If the two identities are equal, the command reports no update needed and exits successfully; the invocation releases acquired locks after telemetry flush.

Step 2.1 is the authoritative check and is performed even when step 1.0 already reported an available update, because a peer `self update` can complete a transaction between step 1.0 and step 1.2. Reading `V_installed` from the canonical executable rather than from the loaded image of `P` is what lets `P` observe that peer's work and exit successfully instead of repeating it.

**2.2 — `P` clears stale artifacts.** `P` validates and removes `D/dotnetup.exe.new` and `D/dotnetup.exe.new.download` if present, and performs best-effort deletion of eligible backups subject to step 2.9. `P` uses its existing ownership of `U` and `A`; it does not reopen or release either lock for cleanup. Backup deletion failures are nonfatal. Unsafe or inaccessible staging files stop the update; the workflow maps I/O/access failures to `InstallFailed` rather than ignoring them.

Backups are named `D/dotnetup.exe.old.<t>` so that a backup still locked by an older process cannot prevent a later transaction from staging.

**2.3 — `P` stages and validates the replacement.** The shared [DotnetDownloader](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetDownloader.cs) writes network bytes or copied cache bytes to `D/dotnetup.exe.new.download`, verifies the pinned SHA-512 hash, and commits those validated bytes to `D/dotnetup.exe.new`. Committing the download is not replacement of the installed executable. Current daily builds are unsigned: the command emits the existing unsigned-source warning and honors the unsigned-download policy, including cache hits. Signed stable self-update metadata is future work, not the current delivery path. Neither staging path is executed.

`P` also reads the staged executable's embedded build ID and requires it to equal `V_channel` before replacement.

After identity validation and, on Unix, setting executable permissions, [SelfUpdateReplacement](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs) flushes the staged file with `FileStream.Flush(flushToDisk: true)` before replacement. This reduces the risk that validated bytes exist only in the system cache, but it does not turn the following replacement into an ACID or power-loss-safe transaction.

Both staging paths are inside `D/` to keep the eventual replacement on the destination volume. Unvalidated bytes remain under `.new.download`; hash-validated bytes become `.new`, and only a staged executable with the expected embedded ID can proceed to replacement. Step 2.2 clears both stale staging names when an update is needed. These protections require a trusted installation directory and stable paths; staging names alone are not a security boundary.

The state of the file system after step 2.3:

```
D/dotnetup.exe.new   <- validated replacement
D/dotnetup.exe       <- installed executable, still running as P
```

**2.4 — `P` replaces the installed executable and creates its backup.** `P` performs one combined replacement:

```cs
File.Replace(
    sourceFileName: stagedPath,
    destinationFileName: installedPath,
    destinationBackupFileName: backupPath,
    ignoreMetadataErrors: false);
```

On Windows, .NET maps this call to `ReplaceFileW`. The operation combines moving the installed executable to `D/dotnetup.exe.old.<t>` and assigning `D/dotnetup.exe` to the staged replacement. `P` continues executing its already-loaded old image. A process launched after the call succeeds resolves the replacement image, while there is no deliberate canonical-path gap between two managed calls.

**2.5 — `P` handles replacement failure.** `ReplaceFileW` is a single API call, not an ACID transaction. In addition to failures that leave all original names intact, Windows documents partial failures. [SelfUpdateReplacement](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs) records original/replacement identities and the backup path before mutation, then inspects the actual paths after failure without following reparse points. It attempts rollback when a verified backup exists and requires the original identity at the canonical path afterward. Recovery never overwrites an unknown canonical executable or deletes recovery artifacts. An unrecoverable failure reports the paths and directs the caller to reinstall.

**2.6 — `P` verifies the replacement.** `P` runs `D/dotnetup.exe --build-identity`, waits synchronously for up to `X_V`, and treats the replacement as valid only if the child exits with status `0` and reports exactly `V_channel`.

`--build-identity` is a hidden **option** on the root command, not a subcommand. Like the built-in `--version`, the action of `--build-identity` runs during `ParseResult.Invoke` and returns before any `CommandBase` is constructed, so `--build-identity` never reaches the gate of step 1.3. That is load-bearing rather than incidental: `P` holds both `A` and `U` exclusively while the child runs, so a gated child would block on its own opens and every transaction would fail. If the verification path ever becomes a subcommand, that subcommand must be classified `safe`.

`--build-identity` writes only `DotnetupBuildIdentity.Current`, read from the loaded record, to stdout. It does not reopen the executable on disk. This is the same ID used by step 1.4; `Parser.Version` remains the human-readable version. `--build-identity` disables telemetry and spawns no detached child processes. This execution check remains necessary even though the gate reads identities offline.

**2.7 — `P` reports success.** `P` prints the installed version and the existing [dotnetup installation link](https://aka.ms/dotnet/dotnetup) for installing older versions. Acquired locks remain held through root telemetry completion and flush, then are released as `Main` returns.

**2.8 — `P` rolls back.** If `P` cannot start `D/dotnetup.exe`, the child does not exit with status `0`, or the reported identity is not `V_channel`, `P` kills the verification child if it is still running and then restores the backup while still holding both locks:

Let `rejectedPath` be `D/dotnetup.exe.old.<t>.rejected`, a transaction-specific sibling path that must not already exist. When the canonical path exists, `P` first renames the rejected executable aside, then renames the original backup back to the canonical path:

```cs
File.Move(
    sourceFileName: installedPath,
    destFileName: rejectedPath,
    overwrite: false);

File.Move(
    sourceFileName: backupPath,
    destFileName: installedPath,
    overwrite: false);
```

Both moves are same-volume renames to unoccupied names; neither copies executable bytes nor overwrites an existing destination. This avoids using the still-running old image as a `File.Replace` replacement source, which has stricter [sharing requirements than the destination](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew#parameters). Running processes can continue executing their images after the renames, but handles that deny delete sharing can still prevent a rename.

The two renames are not atomic together: the canonical path is absent between them, and a crash or power loss can leave it absent. If the canonical path is already absent, `P` skips the first move and attempts only to restore the backup. `P` inspects paths without following unexpected reparse points and never overwrites an unexpected file.

If the first move fails, the rejected executable remains at the canonical path and the backup remains untouched. If the second move fails, the canonical path remains absent and both the backup and rejected executable are retained for recovery. `P` must not delete the backup on either failure; if restoration cannot be completed, `P` reports that dotnetup must be reinstalled.

Successful rollback restores the original executable and its build ID at the canonical path. An `N` loaded from that build takes the identity-matches branch of step 1.4; an `N` loaded from the rejected build instead fails or forwards according to its stage. The rejected executable is left for the best-effort `D/dotnetup.exe.old.*` cleanup in step 2.9.

**2.9 — Deferred cleanup.** On later launches, [SelfUpdateCleanup](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateCleanup.cs) may remove `D/dotnetup.exe.old.*` backups, including rejected executables left by rollback. An `N` attempts cleanup only after acquiring `A` shared and taking the identity-matches branch of step 1.4, retaining `A` throughout cleanup. Current safe commands perform no optional cleanup; `P` performs it only within the locked transaction, and `--build-identity` never performs cleanup.

Cleanup makes one nonblocking attempt to acquire `U` exclusively. If ownership cannot be established, cleanup is skipped without retrying, reporting contention, or failing the command. No participant opens `U` shared. A successful attempt retains `U` through the canonical-build check, enumeration, and deletion, so no self-update transaction can create or use a backup during cleanup. Cleanup never acquires or waits for additional locks, launches a child, or invokes command logic while holding `U`.

Before deleting backups, cleanup reads the canonical executable's embedded build ID under `U` and requires it to match the cleanup process's loaded build ID. If the canonical executable is absent, its record is invalid or unreadable, or the IDs differ, cleanup leaves the backups untouched. This conservative check preserves recovery artifacts when the canonical build cannot be confirmed; acquiring `U` alone does not prove that an earlier transaction completed successfully. The same check applies to backup deletion in step 2.2.

Cleanup checks at most 32 directory entries, accepts only transaction-GUID backup names (optionally ending in `.rejected`), and requires a last-write time at least one day old. It skips failed deletions rather than retrying them and rejects symbolic links or reparse points. It releases `U` as soon as cleanup finishes or is skipped, including on failure, before continuing command execution. The exception is step 2.2, where `P` retains its existing locks for the transaction. Failure to delete a locked backup is not a command or transaction failure, because a process started before the transaction may still be executing that image. These rules also apply to Unix cleanup, subject to the runtime locking compatibility boundary below; cleanup skips failed lock acquisition but does not independently probe lock enforcement.

##### Properties of Algorithms 1 and 2

**The canonical path may be briefly unavailable during step 2.4.** Windows measurements observed file-not-found during `File.Replace`. The [implementation](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs) uses one forward replacement call instead of deliberately splitting it into moves, but neither that code nor the [ReplaceFileW contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew#return-value) guarantees gapless concurrent opens. Measurement timing is not a platform guarantee.

Consumers launching `dotnetup` programmatically should retry transient file-not-found errors with a bounded delay before treating the installation as missing. Retry ordinary commands rejected at the Stage A gate after the update completes. Windows rollback deliberately uses two non-overwriting renames and can also leave a gap. Unix same-filesystem `renameat` provides atomic namespace replacement on supported local filesystems, not power-loss durability; macOS behavior remains unverified in this implementation handoff.

Abrupt termination can leave `D/dotnetup.exe.new` or `D/dotnetup.exe.old.*` behind indefinitely if dotnetup is never run again. Naming each backup with `t` prevents those stale files from corrupting or blocking a later transaction. Step 2.2 clears stale staging files during a later update; backup cleanup in steps 2.2 and 2.9 is opportunistic and runs only when its ownership and canonical-build checks permit it.

A dependent application — a long-running VS Code window, for example — may hold `D/dotnetup.exe.old.<t>` for weeks. Step 2.9 tolerates that rather than failing, and consumers decide how to surface it to the user.

`FileStream` lock ownership is handle-based rather than thread-affine, so `A` and `U` may be held across an `await` and the entire transaction, download included, may be asynchronous. This is why `P` does not use the thread-affine `ScopedMutex`.

After a crash, forced termination, or power loss, use the [get-dotnetup scripts](https://aka.ms/dotnet/dotnetup) to reinstall when the canonical executable is unavailable. Recovery still requires a writable, functioning filesystem; the protocol does not guarantee automatic recovery from every interruption.

#### Comparisons

`rustup` - Rustup [downloads and launches a separate updater](https://github.com/rust-lang/rustup/blob/main/src/cli/self_update.rs), but its self-update path has no cross-process update lock, so two concurrent self-updates can interfere with the shared updater, installed executable, and proxy links. Its process handoff addresses Windows executable locking, not update serialization or crash-atomic replacement. Dotnetup needs no handoff at all, because renaming an in-use executable does not require one; see the rejected alternative below.

`Aspire CLI` - Aspire's archive self-update [extracts to a temporary directory, best-effort deletes older backups, renames the running executable to `aspire.exe.old.<unix-timestamp>`, copies the extracted executable to the canonical path, runs `aspire.exe --version`, and on any failure deletes the canonical path and moves the backup back](https://github.com/microsoft/aspire/blob/main/src/Aspire.Cli/Commands/UpdateCommand.cs). Dotnetup adopts Aspire's verification-and-rollback shape but uses `File.Replace` for the Windows switch rather than a rename followed by a copy.

Three things differ. Dotnetup stages on the destination volume and uses `File.Replace`, avoiding a cross-volume copy or deliberately split forward moves without promising a gapless canonical path. Dotnetup requires the verification child to report exactly `V_channel`, where Aspire requires only exit status `0` and prints whatever version is returned, so a binary that runs but is the wrong build passes Aspire's check. And Aspire takes no cross-process lock, so concurrent self-updates race over the canonical path and the backups, and nothing stops another Aspire command from running across the replacement — the two problems `U` and `A` exist to solve.

`VS Code` - VS Code's installed Windows updater combines a singleton main process, an [in-process update state machine](https://github.com/microsoft/vscode/blob/main/src/vs/platform/update/electron-main/abstractUpdateService.ts), native application/setup/updating/ready mutexes, staged versioned files, and [Inno Setup](https://github.com/microsoft/vscode/blob/main/build/win32/code.iss). This serializes Windows installers and blocks application startup during the final switch. The statement does not apply uniformly to every distribution: macOS delegates to Electron's updater, while ordinary Linux packages generally delegate installation to the package manager or download page. Dotnetup does not require VS Code's UI state machine or installer framework, but it adopts the narrower invariant that only one self-update transaction may modify its executable at a time.

## Linux:

Linux permits the pathname of a running executable to be replaced while the process continues executing the old inode. Algorithm 1 applies unchanged. Algorithm 2 applies with the Windows replacement and failure handling of steps 2.4 and 2.5 replaced by the hard-link-and-move sequence below, and the rollback of step 2.8 replaced by a move of the backup back over the canonical path. Step 1.4 uses the same embedded build-ID format and offline reader as Windows, not device/inode identity.

Let `D/dotnetup` be the installed executable, `D/dotnetup.new` the staged replacement, and `D/dotnetup.old.<t>` the backup.

`P` stages and validates `D/dotnetup.new` per step 2.3, sets the expected executable mode, and flushes `D/dotnetup.new` to disk. `P` refuses to update through an unexpected symbolic link and operates only on the canonical, dotnetup-owned install path. `P` then creates `D/dotnetup.old.<t>` as a hard link to `D/dotnetup` and performs a same-filesystem move of `D/dotnetup.new` over `D/dotnetup`. `P` runs `D/dotnetup --build-identity` per step 2.6; otherwise `P` moves `D/dotnetup.old.<t>` back over `D/dotnetup`. Step 2.9 governs cleanup of `D/dotnetup.old.*` on later launches, including exclusive nonblocking acquisition of `U`.

The Unix forward switch uses `renameat` in a pinned directory, not a managed move that could fall back to copy/delete. Its same-filesystem namespace atomicity is distinct from the Windows `File.Replace` behavior above.

A same-directory hard link preserves the old inode before replacement. Both the backup and the staged path must be on the same mounted filesystem as the installed executable.
The implemented operations are [SelfUpdateFile.CreateBackupUnix and MoveUnix](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateFile.cs), using `linkat` and `renameat` against the pinned directory. Rollback verifies the backup and any occupied canonical identity before restoring it.

On a supported local Linux filesystem, new openers observe either the complete old inode or the complete new inode, while already-running processes continue using the old inode. The implementation flushes the staged file but does not perform a containing-directory durability sync or claim persistence across power loss.

Cross-filesystem `File.Move` may degrade to copy/delete behavior which is why it is avoided.

The rollback move restores the old executable and its build ID. Step 1.4 permits an `N` loaded from that build to proceed and rejects or forwards one loaded from the rejected build, exactly as on Windows.

#### Unix locking caveats

`A` and `U` do not carry the same weight on Unix as on Windows, and Algorithm 1 is correspondingly weaker there.

`FileShare` is mandatory on Windows and enforced by the kernel at `CreateFile`. On Unix, .NET implements `FileShare` with advisory `flock`, which binds only cooperating processes and can be disabled outright by the `System.IO.DisableFileLocking` AppContext switch or the `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` environment variable. Dotnetup accepts the same locking compatibility as the .NET runtime: it uses `FileStream` sharing directly, does not take an additional native `flock`, and does not detect or compensate for disabled or ineffective runtime locking. The concurrency guarantees assume working runtime locking and cooperating processes.

The reason `FileShare.Delete` is never requested does not carry to Unix either. Unlinking an open file is always permitted on Unix, so the hazard the rule exists to prevent — deleting a lock file and recreating it, leaving two processes holding "exclusive" access to different inodes — cannot be prevented by share mode. The mitigation is that `A` and `U` live in a directory owned by the current user and dotnetup never deletes them.

`flock` over NFS is historically unreliable. A `D/` on a network filesystem can silently degrade the gate.

The current diagnostic helper returns no holder details on Unix. It does not parse Linux `/proc/locks` or add a native locking layer.

## macOS:

The macOS implementation selects the same `linkat`/`renameat` flow with platform-specific file flags and metadata handling in [SelfUpdateFile](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateFile.cs). It uses the same embedded build-ID reader and runtime file-sharing locks. macOS execution, APFS behavior, code-signing, and quarantine interactions remain unverified; Linux results are not proof of macOS behavior.

# Update As a Version Swap Mechanism

Version/channel selection, downgrade commands, and `self install` are future design possibilities, not registered CLI surfaces. The current parser accepts only daily `self update` and its `--no-progress` option. Use the existing [installation guidance](https://aka.ms/dotnet/dotnetup) for older versions.

# Implementation

## Stage A Status

Stage A source integration is complete: command registration, the telemetry-free
identity option, NativeAOT invocation lifetime, fail-fast gating, coordinated update,
verified staging, replacement, child verification, rollback, and conservative cleanup
are connected. [Program](../../../../src/Installer/dotnetup.Library/Program.cs),
[CommandBase](../../../../src/Installer/dotnetup.Library/CommandBase.cs), and
[SelfUpdateCommand](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfUpdateCommand.cs)
define the production entry path. Managed development hosts remain invocation-free
and reject self-update.

The [identity tooling](../../../../src/Installer/BuildIdentity/README.md) derives the
embedded equality token from the full Arcade product version and RID. It does not
fingerprint sources or toolchains. Releases with the same version and RID compare
equal, including local rebuilds using the same development version.

[ScopedLockFile](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ScopedLockFile.cs)
returns an acquired disposable lease from `TryAcquireShared` or `TryAcquireExclusive`.
Only contention returns null; other I/O failures propagate. Disposal closes the
handle without deleting the permanent lock file. Commands remain synchronous.
[SelfUpdateCoordinator](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateCoordinator.cs)
acquires `U` before a single attempt at `A`, releases `U` before retrying a busy `A`,
and uses separate cumulative contention budgets, currently two minutes for `U` and
two seconds for `A`. The gate and workflow map failures to `DotnetInstallException`;
`CommandBase` records failure telemetry and the exit code. Windows holder diagnostics
are report-only and cannot change the acquisition result.

Source integration is not feed deployment. [Publishing.props](../../../../eng/Publishing.props)
generates and registers the final binary's `.buildid` sidecar, but live daily
self-update requires a release that deploys the binary, checksum, and matching
sidecar. Their availability at the public daily target is not asserted here.

## Review Guide

| Source | Responsibility |
| --- | --- |
| [DotnetupProcessInfo](../../../../src/Installer/dotnetup.Library/DotnetupProcessInfo.cs), [Program](../../../../src/Installer/dotnetup.Library/Program.cs) | Capture loaded-image values, create native invocation context, and flush telemetry before disposing leases. |
| [Parser](../../../../src/Installer/dotnetup.Library/Parser.cs), [BuildIdentityAction](../../../../src/Installer/dotnetup.Library/BuildIdentityAction.cs), [SelfCommandParser](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfCommandParser.cs) | Register the hidden identity action and public self-update command; identity never constructs a command or starts telemetry. |
| [SelfUpdateInvocation](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateInvocation.cs), [SelfUpdateGate](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateGate.cs), [CommandBase](../../../../src/Installer/dotnetup.Library/CommandBase.cs) | Default commands to unsafe, gate before command work, retain leases, and restrict startup cleanup to admitted unsafe commands. |
| [ScopedLockFile](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ScopedLockFile.cs), [SelfUpdateCoordinator](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateCoordinator.cs), [SelfUpdateLockDiagnostics](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateLockDiagnostics.cs) | Runtime sharing leases, ordered acquisition with separate budgets, and best-effort Windows holder reporting. |
| [SelfUpdateCommand](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfUpdateCommand.cs), [SelfUpdateDownloadProgress](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfUpdateDownloadProgress.cs), [SelfUpdateWorkflow](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) | Host eligibility, daily resolution, progress/warning output, transaction sequencing, and verification-failure recovery. |
| [DotnetDownloader](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetDownloader.cs), [ResolvedDownload](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ResolvedDownload.cs) | Pin release metadata, enforce unsigned policy, validate network/cache bytes, and commit the download to staging. |
| [SelfUpdatePaths](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdatePaths.cs), [SelfUpdateFile](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateFile.cs), [SelfUpdateDirectory](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateDirectory.cs) | Sibling paths, ownership/regular-file checks, no-follow opens, and directory handle lifetime. |
| [SelfUpdateReplacement](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs), [SelfUpdateReplacementState](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacementState.cs), [SelfUpdateVerifier](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateVerifier.cs) | Flushed same-volume replacement, identity-bound recovery, and bounded canonical execution verification with drained pipes. |
| [SelfUpdateCleanup](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateCleanup.cs), [identity tooling](../../../../src/Installer/BuildIdentity/README.md) | Conservative aged-backup deletion and the shared build/runtime identity contract. |

### Environment and Validation Boundaries

- Use a published NativeAOT executable, not the managed library or an ordinary
    managed apphost. The install location must pass ownership and writable-permission
    checks, contain no unexpected links/reparse points, and support same-filesystem
    replacement and working runtime locks. Broadly writable shared temporary
    directories can fail these checks; see [SelfUpdateFile](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateFile.cs).
- Native publishing requires the target platform's linker and SDK/development
    libraries. The Windows handoff resolved the missing libraries using task-local
    official Windows SDK libraries and produced two native versions; native linking
    is not an outstanding source-integration blocker. These are environment inputs,
    not a new machine-wide repository configuration. See the
    [native project](../../../../src/Installer/dotnetup/dotnetup.csproj).
- Reported implementation validation exercised Windows x64 and Linux x64 native
    binaries, including distinct version-derived identities. This is bounded local
    validation, not proof of every RID, filesystem, or live release transaction.
    The reproducible [native test cases](../../../../test/dotnetup.Tests/SelfUpdateNativeTests.cs)
    currently gate their native scenarios to Windows; Linux validation used separate
    native probes. macOS remains unverified.
- The [native fixture](../../../../test/dotnetup.Tests/Utilities/NativeSelfUpdateFiles.cs)
    requires `DOTNETUP_TEST_EXECUTABLE` and `DOTNETUP_TEST_REPLACEMENT` pointing to
    native binaries built with distinct full versions. Same-version/RID rebuilds
    deliberately have equal IDs. Managed reader tests alone do not establish native
    record retention or executable replacement behavior.
- Stage B waiting/forwarding, signed stable self-update, arbitrary version/channel
    selection, and a power-loss-atomic transaction are not implemented contracts.

## Shared Downloads

[DotnetDownloader](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetDownloader.cs)
extends the existing archive pipeline. `DownloadArchiveWithVerification` remains the
`IArchiveDownloader` adapter and retains archive-extension handling.

`ResolveDotnetupDownload(string rid)` resolves the daily shortlink once through
`DailyChannelResolver`, requires the HTTPS `ci.dot.net/public/dotnetup/<version>/dotnetup-<rid>[.exe]`
layout, and fetches the checksum from the corresponding `public-checksums` path. The build-ID
sidecar is the concrete artifact URL plus `.buildid`: exactly 64 lowercase hexadecimal
characters, optionally followed by LF or CRLF, with no BOM. Neither metadata request resolves
another daily shortlink. Missing or malformed metadata fails resolution.

[ResolvedDownload](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ResolvedDownload.cs)
carries `DownloadUri`, `ExpectedHash` (SHA-512), `Rid`, `Version`, `BuildId`, and `IsUnsigned`.
`DownloadWithVerification(ResolvedDownload download, string destinationPath, IProgress<DownloadProgress>? progress = null)`
returns the exact destination path without appending an executable extension. Network bytes
and copied cache bytes are hash-checked before committing to that path. Dotnetup uses the exact
published hash, not the archive pipeline's legacy alternate-hash allowlist.

Preview self-update is daily-only and unsigned. Policy is checked before resolution and before
delivery, including cache hits. [SelfUpdateCommand](../../../../src/Installer/dotnetup.Library/Commands/Self/SelfUpdateCommand.cs)
emits the existing unsigned-source warning and honors `--no-progress`.
[SelfUpdateWorkflow](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs)
requires the staged executable's embedded ID to match `BuildId` before replacement.

# Release Stable VS Preview

Future signed self-update would resolve an index of dotnetup releases similar to the .NET release manifest. This is not the current daily resolver.
The manifest will be signed just like the .NET artifacts manifests, with a detached signature, which will be downloaded as well and be used to validate dotnetup's own executable. We could only have an index but supporting multiple versions or allowing a downgrade/revert will only be possible if we maintain separate indexes. Whether we have a `daily` `preview` `stable` keyed index or a `major.minor` keyed index is not part of this spec.

#### Locking Rationale

**Why two lock files instead of a mutex such as `ModifyInstallationStates`.**
A named mutex has one owning thread rather than shared ownership, so holding a single mutex for the lifetime of each `non-safe` command would serialize those commands. Mutexes do support nonblocking acquisition through [`WaitOne(0)`](https://learn.microsoft.com/dotnet/api/system.threading.waithandle.waitone?view=net-10.0#system-threading-waithandle-waitone(system-int32))

The real reason is a file lock can implement concurrent shared ownership that can survive an `await` without requiring release on the acquiring thread, where mutexes cannot.

**Why `N` acquires only the activity lock at the gate.**
`P` holds `A` exclusively for the entire transaction, so `A` alone is a continuous signal that a transaction is in flight. An `N` that has acquired `A` shared and retained it excludes `P` completely: if `P` is mid-transaction the acquire by `N` fails, and if `P` is between steps 1.1 and 1.2 the acquire by `N` succeeds, after which step 1.2 fails and `P` releases `U` and backs off. There is no interleaving in which both proceed. `N` never releases `A` after passing the gate, including when briefly acquiring and releasing `U` for optional cleanup.

**Why `P` acquires the update lock before the activity lock.**
`U` serializes `P` against peer `self update` processes and cleanup, and `A` excludes `N`. Taking `U` first means `P` only begins excluding every `N` after acquiring ownership of the update artifacts; taking `A` first would hold every `non-safe` command out of the way for the whole of `X_U` while `P` waits on another owner of `U`.

`P` has no reason to open `A` shared first. A shared open succeeds while any number of `non-safe` processes hold `A` shared, so it would answer nothing. Only the exclusive open establishes that no `non-safe` process is running.

**Why optional cleanup introduces no circular wait.** An `N` waiting at the gate holds neither lock. An `N` that already holds `A` never waits for `U`: if `P` owns `U`, cleanup is skipped immediately. If cleanup owns `U`, it performs its bounded work without acquiring additional locks and releases `U` before continuing the command. `P` also releases `U` before retrying a failed acquisition of `A`. Neither participant waits for a lock held by the other while retaining its own. The immediate-skip rule for cleanup, rather than a claim that only `P` can hold two locks, is essential to this reasoning.

**Why the update lock is limited to updates and cleanup.** `U` protects update artifacts, not ordinary command execution. Contention on `U` may mean a peer update or cleanup, and `P` retries either case within `X_U` rather than failing immediately. Once `P` owns `U`, contention on `A` means a `non-safe` command is running and is handled separately by `X_A`. Cleanup is the short-lived exception allowing an admitted `N` to own `U`; current safe commands do not perform optional cleanup. No command holds `U` shared or needs it merely to pass the gate. Diagnostics must not identify every owner of `U` as an updater.

**Why forwarding instead of resuming.** A process that waited at the gate is still executing the old image. Resuming would run pre-update code against post-update state — for example a manifest written in a format the old code does not understand. Forwarding is only legal at the gate precisely because nothing has been mutated and nothing has been written to the console yet.

**Why `FileShare.Delete` is never requested.** A lock file that can be deleted while it is open allows a second process to create a new file at the same path, at which point two processes each hold "exclusive" access to different files. The lock files are permanent fixtures; leaving them behind costs nothing. For the same reason the locks are files under the per-user `D/` rather than `Global\` named objects, which another session can squat.


# Alternatives Considered:

### Content below is not proposed implementation but rather alternatives that we could implement.

Windows also has reboot-delayed renames, but this provides a poor experience for immediate updates as it requires a reboot.

#### Rejected Alternative: `MoveFileExW` from a Separate Replacer Process

An earlier framing called this the "file replace" approach, but the precise operation is a `MoveFileExW` move with `MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH`. Because this operation cannot be relied upon to replace the canonical executable while that destination is running, `P` would copy an updater to a throwaway location, hand the locks to that replacer process `R`, and exit. After incompatible users of `D/dotnetup.exe` drained, `R` would perform the move:

```cs
MoveFileExW(
    stagedPath,
    installedPath,
    MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH);
```

This is a single move of the replacement onto the canonical path, but it does not create the backup; preserving the old executable requires a separate copy, hard link, or rename. `MOVEFILE_WRITE_THROUGH` asks Windows not to return until the move has completed on disk. It improves durability after a successful return, but Microsoft does not document it as an ACID transaction or as an all-or-nothing guarantee across process termination or power loss. Transactional NTFS provided transactional moves, but Microsoft recommends against taking a dependency on TxF because it may not remain available.

The selected `File.Replace` design avoids the temporary process and handoff and creates the backup as part of the same Windows call. It avoids deliberately split forward moves, not all possible canonical-path gaps. It still requires a flushed staged file and explicit handling of `ReplaceFileW`'s documented partial failure states, without promising power-loss atomicity.
