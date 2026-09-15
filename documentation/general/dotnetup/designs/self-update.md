# Add `Self Update` Command

`dotnetup self update` updates `dotnetup` itself.

The update should be in-place and appear to happen seamlessly from the perspective of a CLI.

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

This avoids the normal interval in which the canonical path is absent between two `File.Move` calls. It is not an ACID or power-loss-safe transaction: `ReplaceFileW` documents partial failure states, and its `REPLACEFILE_WRITE_THROUGH` flag is unsupported. The staged executable must therefore be flushed before replacement, and recovery must account for the documented arrangements of the staged, canonical, and backup paths after failure.


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

- It's okay for `dotnetup` to no longer exist on the `PATH` or in the `dotnetup` folder in the event of a power-outage or uncontrolled process kill that occurs while `dotnetup self update` is running. Consumers must know how to re-acquire `dotnetup` or ebmed a backup `dotnetup` executable at a base level in the event this occurs.<br><br>


- It is NOT okay for a power-outage or uncontrolled process kill that occurs while `dotnetup self update` is running to cause a permanent broken state that requires user understanding to remedy outside of re-installing `dotnetup`. e.g. it must not leave behind files with permissions that don't allow deletion, it must not corrupt other files that `dotnetup` depends on or leave them half-complete, including but not limited to a corrupt `dotnetup` executable that fails to load or execute.<br><br>

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

#### Final Proposed Update Logic:

##### Definitions

Let `D/` be the directory containing the installed dotnetup executable, and let `D/dotnetup.exe` be that executable at its canonical path.

Let `t` be the transaction identifier: a random value unique to a single execution of `dotnetup self update`.

Let `D/dotnetup.exe.new` be the staged replacement and `D/dotnetup.exe.old.<t>` the backup of the executable being replaced. Both are siblings of `D/dotnetup.exe`, so both are guaranteed to be on the same volume as `D/dotnetup.exe`.

Let `A` be the activity lock, the file `D/dotnetup.activity.lock`.

Let `U` be the update lock, the file `D/dotnetup.update.lock`.

`A` and `U` are permanent, zero-length files created on first use and never deleted.

Let `P` be the `dotnetup self update` process that Algorithm 2 outlines.

Let `N` be any `non-safe` dotnetup process.

Let `S` be any `safe` dotnetup process other than `P`: `dotnetup dotnet` and the telemetry drain process. `self update` is classified `safe` as well, but `P` follows Algorithm 1 rather than the gate.

Let `V_channel` be the per-RID build ID published by the configured dotnetup channel, and let `V_installed` be the build ID read from the embedded record in `D/dotnetup.exe`. These are the equality tokens defined by the [build-identity proposal](build-identity.md), not human-readable versions or filesystem file IDs.

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
| `N` | `A` | shared | from just after parse until `N` exits |
| `N` | `U` | exclusive | optional cleanup only, after passing the gate and build-identity check; one nonblocking attempt |
| `P` | `U` | exclusive | start of the transaction until `P` exits |
| `P` | `A` | exclusive | start of the transaction until `P` exits |
| `S` | `A` | — | never acquired; skips the gate |
| `S` | `U` | exclusive | optional cleanup only; one nonblocking attempt |

`A` alone excludes `N` from a transaction, because `P` holds `A` exclusively for the whole transaction and an `N` that has retained `A` shared prevents `P` from ever acquiring it. `U` serializes self-update transactions and cleanup against each other. `N` does not acquire `U` to pass the gate or retain `U` for its command lifetime; its optional cleanup follows step 2.9.

Because `S` holds neither lock outside optional cleanup, a self update can complete underneath it. `S` is `safe` only so long as `S` resolves every value derived from the dotnetup image at startup and caches it globally — `Environment.ProcessPath` above all, whose behavior is undefined [if the executable is renamed or deleted before the property is first accessed](https://learn.microsoft.com/dotnet/api/system.environment.processpath?view=net-10.0#remarks).

`install`, `uninstall`, `update`, and the other manifest-mutating commands continue to use the `ModifyInstallationStates` mutex for their own critical sections. `P` does not acquire `ModifyInstallationStates`, and no modification to that logic is necessary.

##### Algorithm 1 — Lock acquisition and the `non-safe` gate

Every acquisition of `A` or `U` either succeeds or throws `IOException` immediately; no open blocks in the kernel. "Wait" therefore always denotes a retry loop with jittered backoff bounded by a timeout.

**Rule 1 — `P` acquires `U` before `A`.** `U` is taken first so that `P` does not exclude every `N` while waiting out a peer `self update` or cleanup. `N` acquires only `A` at the gate; after passing its build-identity check, it may also hold `U` briefly for cleanup under step 2.9.

**Rule 2 — no hold-and-wait during acquisition.** `P` never blocks on `A` while holding `U`. If the acquisition of `A` fails, `P` releases `U`, backs off, and retries the pair from step 1.1. An `N` waiting at the gate holds neither lock. An `N` that already holds `A` may try to acquire `U` once for cleanup, but skips cleanup immediately if that attempt fails. Cleanup never acquires or waits for additional locks while holding `U`, and releases `U` before continuing command execution.

**Rule 3 — asymmetric timeouts.**

| Timeout | Applies to | Magnitude |
| --- | --- | --- |
| `X_U` | `P` waiting on `U` held by a peer `self update` or cleanup | generous |
| `X_A` | `P` waiting on `A` held by `N` | brief |
| `X_N` | `N` waiting at the gate during a self update (Stage B only) | the length of a typical update |
| `X_V` | `P` waiting for the verification child of step 2.6 | brief |

Cleanup does not use these retry timeouts: it makes one nonblocking attempt to acquire `U` and skips cleanup if ownership cannot be established.

###### Lock Acquisition for `P`

**1.0 — `P` checks for an available update before acquiring any lock.** `P` resolves `V_channel` and compares it with the build ID read from the canonical executable. If the two are equal, `P` exits successfully without acquiring `U` or `A`.

This check is advisory. It decides only whether acquiring locks is worthwhile, and it is never the basis for replacing or deleting an executable. A read error, a network failure, or an observation of the step 2.4 replacement window does not distinguish the outcome: `P` falls through to step 1.1 and lets step 2.1 decide authoritatively under both locks. `P` may reuse the resolved `V_channel` in step 2.1 rather than resolving it a second time.

Reading the canonical build ID without holding `U` is acceptable here and is not in step 2.9, because the two reads gate different actions. Step 2.9 uses the value to delete backups, which is irreversible, so it reads under `U`. Step 1.0 uses the value only to decide whether to continue, and `File.Replace` is rename-based, so the canonical name always resolves to a complete executable rather than a partially written one.

Step 1.0 exists because the common invocation is a poll that finds nothing to do. Without it, every such poll takes `A` exclusively, excludes every `N` on the machine for the duration of the channel lookup, and can fail with `DotnetupBusyWithAnotherCommand` having accomplished nothing. A `self update` run with no network connectivity likewise fails without blocking any other command.

The check is placed before both locks rather than between steps 1.1 and 1.2 deliberately. Resolving `V_channel` is a network operation, so performing it while holding `U` would stretch the interval between acquiring `U` and acquiring `A` from two file opens to a network round trip. More `N` processes would accumulate in that interval, `P` would fail step 1.2 more often and restart the pair under rule 2, and the longer hold on `U` would cause the single nonblocking cleanup attempt of step 2.9 to be skipped more often.

**1.1 — `P` acquires `U` exclusively.** A busy `U` means a peer `self update` or cleanup holds `U`. `P` backs off and retries for up to `X_U`, then re-evaluates whether an update is still required after acquiring both locks. `P` does not fail immediately for contention on `U`; on expiry of `X_U`, `P` fails with `DotnetupBusyWithUpdateOrCleanup` and reports that another update or cleanup holds the lock, with best-effort holder details from step 1.7.

**1.2 — `P` acquires `A` exclusively.** A busy `A` means at least one `N` is running. Per rule 2, `P` releases `U`, backs off, and retries the pair from step 1.1 for up to `X_A`. On expiry of `X_A`, `P` fails with `DotnetupBusyWithAnotherCommand` and reports the locking PID per step 1.7.

###### Lock Acquisition for `N`

**1.3 — `N` passes the gate.** The gate executes in `CommandBase.Execute`, after the parser has determined the safety status of the command and before any command body executes.

`N` acquires `A` shared and holds `A` until `N` exits. In Stage A, if the open fails because `A` is busy, `N` fails immediately, reports that a self update is in progress, and instructs the caller to re-run the command after it completes. In Stage B, `N` instead backs off and retries for up to `X_N`, unless the caller has opted into immediate failure. On expiry of `X_N`, `N` fails and reports that a self update is in progress.

A busy `A` unambiguously means a transaction is in flight, because `A` is only ever held exclusively by `P`; another `N` holding `A` shared does not block this one.

Safety is a property of the command: `CommandBase` classifies every command as `non-safe` by default, and only `dotnetup dotnet`, the telemetry drain, and `self update` override that default. A newly added command is therefore gated unless someone deliberately exempts it.

**1.4 — `N` verifies build identity.** After acquiring `A` shared, `N` compares `DotnetupBuildIdentity.Current`, read from its loaded image, with `V_installed`, read directly from the canonical executable's embedded record while retaining `A`. The [build-identity proposal](build-identity.md) defines the record, automatic build generation, and offline reader. This check launches no child and never uses a pathname lookup to identify the loaded image.

The comparison is unconditional, even if acquiring `A` succeeded immediately: replacement may have completed before `N` reached the gate. `N` retains `A` through its command body or forwarding so the canonical build cannot change underneath the check. Missing, malformed, duplicate, unsupported, or unreadable records stop the command; unknown identities never compare equal.

- **Identity matches.** The loaded and installed builds agree, including after rollback to the loaded build. `N` proceeds to the command body.
- **Identity differs.** The loaded build is no longer installed, so `N` must not execute its command body. In Stage A, `N` fails and instructs the caller to re-run the command. In Stage B, `N` forwards per step 1.5.

**1.5 — `N` forwards to the replaced executable (Stage B only).** `N` starts `D/dotnetup.exe` with the original `args`, `UseShellExecute = false`, and no stream redirection, so the child inherits stdin, stdout, and stderr, the working directory of `N`, and the environment of `N` plus an incremented `DOTNETUP_FORWARD_DEPTH`. `N` retains the shared handle on `A` for the lifetime of the child, waits for the child, and returns the exit code of the child via `SetExitCode`.

`D/dotnetup.exe` is resolved as the canonical, dotnetup-owned path and is not followed through an unexpected symbolic link or reparse point. Forwarding is capped at a `DOTNETUP_FORWARD_DEPTH` of 2; beyond that `N` fails rather than hopping again. `N` emits a telemetry event for the forward and does not emit a command-completion event, because the child emits one. Forwarding breaks if the replacement executable renamed or removed the command that `N` was invoked with.

Forwarding is deferred to Stage B. `N` never executes the stale command body of `N` in either stage.

**1.6 — Work permitted before the gate.** Exactly three things may execute before the gate: console encoding and UI language setup, capture of `Environment.ProcessPath` and the build ID from the loaded record (not a file lookup), and parsing.

Parsing must not read the manifest, enumerate `D/`, or touch the network.

`N` must not perform cleanup before passing both the gate and the build-identity check, or on a path that fails or forwards instead of executing its command body.

The first-run telemetry notice is the only pre-gate write. That notice targets the telemetry directory rather than `D/`, and its sentinel keeps the notice idempotent across a forward. An `N` blocked at the gate has therefore touched no installation state and can forward or fail cleanly.

###### Common to `P` and `N`

**1.7 — Reporting the lock holder.** When a required acquisition fails, dotnetup queries Windows Restart Manager to report the locking process and PID on a best-effort basis. Optional cleanup skips this reporting when it cannot acquire `U`. This requires a `FileLockDetector`-style helper such as [commit `7fcc618e03f`](https://github.com/dotnet/sdk/commit/7fcc618e03f1520f688fa86bc7ade67aa417e380) via `RmRegisterResources`/`RmGetList` integration. The reported process may exit before the report is produced, and failure to identify the reported process does not change the outcome of the acquisition.

##### Algorithm 2 — The update transaction

Algorithm 2 begins once `P` holds both `U` and `A` per steps 1.1 and 1.2. `P` holds both locks until `P` exits, and `P` performs every step itself; no second process participates in the replacement.

**2.1 — `P` determines whether an update is required.** `P` reads `V_installed` from the canonical executable under both locks and compares it with `V_channel`, not with `P`'s own loaded build ID. If the two identities are equal, `P` releases `A` and `U` and exits successfully.

Step 2.1 is the authoritative check and is performed even when step 1.0 already reported an available update, because a peer `self update` can complete a transaction between step 1.0 and step 1.2. Reading `V_installed` from the canonical executable rather than from the loaded image of `P` is what lets `P` observe that peer's work and exit successfully instead of repeating it.

**2.2 — `P` clears stale artifacts.** `P` deletes `D/dotnetup.exe.new` if present, and performs best-effort deletion of eligible `D/dotnetup.exe.old.*` backups, subject to the canonical-build check and cleanup limits in step 2.9. `P` uses its existing ownership of `U` and `A`; it does not reopen `U` or release either lock for cleanup. Failure to delete a backup belonging to an older transaction is not fatal. Failure to delete `D/dotnetup.exe.new` returns `InsufficientPermissionsToUpdate` with the locking-process details from step 1.7.

Backups are named `D/dotnetup.exe.old.<t>` so that a backup still locked by an older process cannot prevent a later transaction from staging.

**2.3 — `P` stages and validates the replacement.** `P` downloads the replacement executable directly to `D/dotnetup.exe.new` and validates the staged file in place. Preview builds validate the published hash as an integrity check and warn explicitly that the artifact is not authenticated. Stable builds validate signed release metadata against the executable. `P` neither executes `D/dotnetup.exe.new` nor renames `D/dotnetup.exe.new` over any path until validation succeeds.

`P` also reads the staged executable's embedded build ID and requires it to equal `V_channel` before replacement.

After validation, `P` flushes the staged file with `FileStream.Flush(flushToDisk: true)` before replacement. This reduces the risk that validated bytes exist only in the system cache, but it does not turn the following replacement into an ACID or power-loss-safe transaction.

`D/dotnetup.exe.new` is staged inside `D/` because `ReplaceFileW` requires the replacement, replaced, and backup files to reside on the same volume. Unvalidated bytes sitting briefly in `D/` are not reachable: nothing resolves the `.new` suffix, and step 2.2 removes a stale `D/dotnetup.exe.new` at the start of every transaction.

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

**2.5 — `P` handles replacement failure.** `ReplaceFileW` is a single API call, not an ACID transaction. In addition to failures that leave all original names intact, Windows documents partial failures in which the old executable has moved to the backup name or the replacement has inherited metadata without taking the canonical name. If `File.Replace` throws, `P` retains both locks, classifies the native error, inspects the current transaction's three paths without following reparse points, and restores `D/dotnetup.exe.old.<t>` to the canonical path when that is both necessary and safe. If it cannot establish a runnable canonical executable, it reports that dotnetup must be reinstalled.

**2.6 — `P` verifies the replacement.** `P` runs `D/dotnetup.exe --build-identity`, waits synchronously for up to `X_V`, and treats the replacement as valid only if the child exits with status `0` and reports exactly `V_channel`.

`--build-identity` is a hidden **option** on the root command, not a subcommand. Like the built-in `--version`, the action of `--build-identity` runs during `ParseResult.Invoke` and returns before any `CommandBase` is constructed, so `--build-identity` never reaches the gate of step 1.3. That is load-bearing rather than incidental: `P` holds both `A` and `U` exclusively while the child runs, so a gated child would block on its own opens and every transaction would fail. If the verification path ever becomes a subcommand, that subcommand must be classified `safe`.

`--build-identity` writes only `DotnetupBuildIdentity.Current`, read from the loaded record, to stdout. It does not reopen the executable on disk. This is the same ID used by step 1.4; `Parser.Version` remains the human-readable version. `--build-identity` disables telemetry and spawns no detached child processes. This execution check remains necessary even though the gate reads identities offline.

**2.7 — `P` reports success.** `P` prints a success message including an aka.ms link describing how to install older versions, releases `A` and `U`, and exits.

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

Successful rollback restores the original executable and its build ID at the canonical path. An `N` loaded from that build takes the identity-matches branch of step 1.4; an `N` loaded from the rejected build instead fails or forwards according to its stage. The rejected executable is left for the best-effort `D/dotnetup.exe.old.*` cleanup in step 2.8.

**2.9 — Deferred cleanup.** On later launches, dotnetup may perform best-effort cleanup of `D/dotnetup.exe.old.*` backups, including rejected executables left by rollback. An `N` attempts cleanup only after acquiring `A` shared and taking the identity-matches branch of step 1.4. It retains `A` throughout cleanup. Safe commands other than `P` may also attempt cleanup without acquiring `A`; the `--build-identity` verification path never performs cleanup.

Cleanup makes one nonblocking attempt to acquire `U` exclusively. If ownership cannot be established, cleanup is skipped without retrying, reporting contention, or failing the command. No participant opens `U` shared. A successful attempt retains `U` through the canonical-build check, enumeration, and deletion, so no self-update transaction can create or use a backup during cleanup. Cleanup never acquires or waits for additional locks, launches a child, or invokes command logic while holding `U`.

Before deleting backups, cleanup reads the canonical executable's embedded build ID under `U` and requires it to match the cleanup process's loaded build ID. If the canonical executable is absent, its record is invalid or unreadable, or the IDs differ, cleanup leaves the backups untouched. This conservative check preserves recovery artifacts when the canonical build cannot be confirmed; acquiring `U` alone does not prove that an earlier transaction completed successfully. The same check applies to backup deletion in step 2.2.

Cleanup applies an age threshold and a finite per-launch work budget to enumeration and deletion, skips failed deletions rather than retrying them, and never follows symbolic links or reparse points outside `D/`. It releases `U` as soon as cleanup finishes or is skipped, including on failure, before continuing command execution. The exception is step 2.2, where `P` retains its existing locks for the transaction. Failure to delete a locked backup is not a command or transaction failure, because a process started before the transaction may still be executing that image. These rules also apply to Unix cleanup, subject to the Unix locking caveats below; cleanup must be skipped if lock enforcement cannot be established.

##### Properties of Algorithms 1 and 2

**The canonical path briefly does not exist during step 2.4.** `File.Replace` moves the destination aside before moving the source into place, so for roughly one millisecond `D/dotnetup.exe` cannot be opened and a process launching it observes a file-not-found error. Measured on Windows across five runs, the window is 0.8 ms to 1.3 ms; a pair of separate renames measures 1.0 ms to 1.6 ms, so the choice of primitive does not remove it.

The window cannot be closed on Windows while dotnetup is running. The only gapless primitive is `File.Move` with `overwrite: true`, and that fails with `UnauthorizedAccessException` against a path that has concurrent openers. Consumers that launch `dotnetup` programmatically — an IDE extension polling for updates, for example — should retry once on file-not-found rather than treating the first failure as a missing installation. Linux and macOS have no such window, because `rename(2)` over an existing path is atomic and the destination name resolves to either the old or the new inode at every instant.

Abrupt termination can leave `D/dotnetup.exe.new` or `D/dotnetup.exe.old.*` behind indefinitely if dotnetup is never run again. Naming each backup with `t` prevents those stale files from corrupting or blocking a later transaction. Step 2.2 clears stale staging files during a later update; backup cleanup in steps 2.2 and 2.9 is opportunistic and runs only when its ownership and canonical-build checks permit it.

A dependent application — a long-running VS Code window, for example — may hold `D/dotnetup.exe.old.<t>` for weeks. Step 2.9 tolerates that rather than failing, and consumers decide how to surface it to the user.

`FileStream` lock ownership is handle-based rather than thread-affine, so `A` and `U` may be held across an `await` and the entire transaction, download included, may be asynchronous. This is why `P` does not use the thread-affine `ScopedMutex`.

After a crash, `pkill`, or power loss at any step, running the get-dotnetup scripts restores `D/dotnetup.exe` at the canonical location.

#### Comparisons

`rustup` - Rustup [downloads and launches a separate updater](https://github.com/rust-lang/rustup/blob/main/src/cli/self_update.rs), but its self-update path has no cross-process update lock, so two concurrent self-updates can interfere with the shared updater, installed executable, and proxy links. Its process handoff addresses Windows executable locking, not update serialization or crash-atomic replacement. Dotnetup needs no handoff at all, because renaming an in-use executable does not require one; see the rejected alternative below.

`Aspire CLI` - Aspire's archive self-update [extracts to a temporary directory, best-effort deletes older backups, renames the running executable to `aspire.exe.old.<unix-timestamp>`, copies the extracted executable to the canonical path, runs `aspire.exe --version`, and on any failure deletes the canonical path and moves the backup back](https://github.com/microsoft/aspire/blob/main/src/Aspire.Cli/Commands/UpdateCommand.cs). Dotnetup adopts Aspire's verification-and-rollback shape but uses `File.Replace` for the Windows switch rather than a rename followed by a copy.

Three things differ. Dotnetup stages on the destination volume and uses `File.Replace`, avoiding both a cross-volume copy and the normal canonical-path gap between separate operations. Dotnetup requires the verification child to report exactly `V_channel`, where Aspire requires only exit status `0` and prints whatever version is returned, so a binary that runs but is the wrong build passes Aspire's check. And Aspire takes no cross-process lock, so concurrent self-updates race over the canonical path and the backups, and nothing stops another Aspire command from running across the replacement — the two problems `U` and `A` exist to solve.

`VS Code` - VS Code's installed Windows updater combines a singleton main process, an [in-process update state machine](https://github.com/microsoft/vscode/blob/main/src/vs/platform/update/electron-main/abstractUpdateService.ts), native application/setup/updating/ready mutexes, staged versioned files, and [Inno Setup](https://github.com/microsoft/vscode/blob/main/build/win32/code.iss). This serializes Windows installers and blocks application startup during the final switch. The statement does not apply uniformly to every distribution: macOS delegates to Electron's updater, while ordinary Linux packages generally delegate installation to the package manager or download page. Dotnetup does not require VS Code's UI state machine or installer framework, but it adopts the narrower invariant that only one self-update transaction may modify its executable at a time.

## Linux:

Linux permits the pathname of a running executable to be replaced while the process continues executing the old inode. Algorithm 1 applies unchanged. Algorithm 2 applies with the Windows replacement and failure handling of steps 2.4 and 2.5 replaced by the hard-link-and-move sequence below, and the rollback of step 2.8 replaced by a move of the backup back over the canonical path. Step 1.4 uses the same embedded build-ID format and offline reader as Windows, not device/inode identity.

Let `D/dotnetup` be the installed executable, `D/dotnetup.new` the staged replacement, and `D/dotnetup.old.<t>` the backup.

`P` stages and validates `D/dotnetup.new` per step 2.3, sets the expected executable mode, and flushes `D/dotnetup.new` to disk. `P` refuses to update through an unexpected symbolic link and operates only on the canonical, dotnetup-owned install path. `P` then creates `D/dotnetup.old.<t>` as a hard link to `D/dotnetup` and performs a same-filesystem move of `D/dotnetup.new` over `D/dotnetup`. `P` runs `D/dotnetup --build-identity` per step 2.6; otherwise `P` moves `D/dotnetup.old.<t>` back over `D/dotnetup`. Step 2.9 governs cleanup of `D/dotnetup.old.*` on later launches, including exclusive nonblocking acquisition of `U`.

Like the selected Windows `File.Replace` operation, the Linux move is a single namespace replacement rather than a pair of renames, so it has no normal window in which no executable exists at the canonical path.

A same-directory hard link preserves the old inode before replacement. Both the backup and the staged path must be on the same mounted filesystem as the installed executable.
```cs
File.CreateHardLink(backupPath, installedPath);
File.Move(stagedPath, installedPath, overwrite: true);

File.Move(backupPath, installedPath, overwrite: true); // upon failure
```

On a supported local Linux filesystem, the same-filesystem move maps to an atomic namespace replacement: new openers observe either the complete old inode or the complete new inode, while already-running processes continue using the old inode. This does not by itself guarantee persistence across power loss. The implementation flushes the staged file before replacement and, where strict durability is required, synchronizes the containing directory after replacement.

Cross-filesystem `File.Move` may degrade to copy/delete behavior which is why it is avoided.

The rollback move restores the old executable and its build ID. Step 1.4 permits an `N` loaded from that build to proceed and rejects or forwards one loaded from the rejected build, exactly as on Windows.

#### Unix locking caveats

`A` and `U` do not carry the same weight on Unix as on Windows, and Algorithm 1 is correspondingly weaker there.

`FileShare` is mandatory on Windows and enforced by the kernel at `CreateFile`. On Unix, .NET implements `FileShare` with advisory `flock`, which binds only cooperating processes and can be disabled outright by the `System.IO.DisableFileLocking` AppContext switch or the `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` environment variable. An environment variable can therefore turn the entire gate into a no-op. An implementation that wants the guarantee should take the `flock` explicitly rather than relying on the implicit behavior of `FileStream`.

The reason `FileShare.Delete` is never requested does not carry to Unix either. Unlinking an open file is always permitted on Unix, so the hazard the rule exists to prevent — deleting a lock file and recreating it, leaving two processes holding "exclusive" access to different inodes — cannot be prevented by share mode. The mitigation is that `A` and `U` live in a directory owned by the current user and dotnetup never deletes them.

`flock` over NFS is historically unreliable. A `D/` on a network filesystem can silently degrade the gate.

Windows Restart Manager has no Unix equivalent, so step 1.7 degrades. Linux can parse `/proc/locks`, which lists `FLOCK` holders with PID; macOS has no comparable interface.

## macOS:

macOS follows the Linux replacement flow: the pathname of a running executable may be replaced, and `File.CreateHardLink` and a same-filesystem `File.Move` behave as described above. Step 1.4 uses the same embedded build-ID format and offline reader as Windows and Linux. The Unix locking caveats apply, except that `/proc/locks` does not exist, so step 1.7 cannot report a lock holder at all.

# Update As a Version Swap Mechanism

Once the releases-index and releases.json files are available, the version to download can be repointed as `dotnetup self install <channel or version>` and use the same semantics as an `update`.

# Implementation

DotnetArchiveDownloader -> rename -> DotnetDownloader

DotnetArchiveDownloader in V1 (preview) can use `ResolveBlobFeedEntry` and use the same unsigned warning and only update off daily channels since that`s what exists. We can show progress and download using everything else we already do.

# Release Stable VS Preview

ResolveManifestEntry will resolve an index of dotnetup releases similar to the .NET release manifest.
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

**Why the update lock is limited to updates and cleanup.** `U` protects update artifacts, not ordinary command execution. Contention on `U` may mean a peer update or cleanup, and `P` retries either case within `X_U` rather than failing immediately. Once `P` owns `U`, contention on `A` means a `non-safe` command is running and is handled separately by `X_A`. Cleanup is the short-lived exception allowing `N` or a safe command to own `U`; no command holds `U` shared or needs it merely to pass the gate. Diagnostics must not identify every owner of `U` as an updater.

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

The selected `File.Replace` design avoids the temporary process and handoff, creates the backup as part of the same Windows call, and removes the normal canonical-path gap. It still requires a flushed staged file and explicit handling of `ReplaceFileW`'s documented partial failure states.
