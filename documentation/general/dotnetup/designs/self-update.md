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

- It's okay for `dotnetup` to no longer exist on the `PATH` or in the `dotnetup` folder in the event of a power-outage or uncontrolled process kill that occurs while `dotnetup self update` is running. Consumers must know how to re-acquire `dotnetup` or ebmed a backup `dotnetup` executable at a base level in the event this occurs.<br><br>


- It is NOT okay for a power-outage or uncontrolled process kill that occurs while `dotnetup self update` is running to cause a permanent broken state that requires user understanding to remedy outside of re-installing `dotnetup`. e.g. it must not leave behind files with permissions that don't allow deletion, it must not corrupt other files that `dotnetup` depends on or leave them half-complete, including but not limited to a corrupt `dotnetup` executable that fails to load or execute.<br><br>

- Multiple `dotnetup` processes in general must be able to execute at the same time.<br><br>

- `dotnetup` may leverage asynchronous code or `await`.<br><br>

- `self update` must not run if any `non-safe` `dotnetup` process is currently running. e.g. if `dotnetup sdk install` is running, the manifest format may change from one version to another; installing a new version that may edit the manifest format may cause the old `dotnetup` process to fail, and we want an invariant that avoids any such bugs.<br><br>

- `non-safe` processes must never execute their command body across a `self update` boundary. A `non-safe` process that starts while `self update` is running waits at its gate rather than failing outright, so `dotnetup list` and IDE-issued commands do not hard-fail during an update. If the executable was replaced while it waited, it must forward the invocation to the updated executable and return that process's exit code; it must never resume running its own now-stale code. If breaking changes are made to command names themselves, then this will break and that is acceptable.<br><br>

- `self update` does NOT make other `self update` processes fail or exit immediately; other `self update` processes must merely wait for the other update processes to complete and then determine that an update is no longer needed, assuming no release occurs within the time frame of the race.<br><br>

- `self update` also does NOT make other `non safe` processes fail or exit immediately unless they are configured to do so. This is because we don't want others who call `dotnetup --info` to have to worry about whether another process is running `self update` or have to write recovery logic for this. However, others may opt in to this behavior if they want minimal latency and would rather defer the task if an update is running.<br><br>

- At this time, the only 'safe' `dotnetup` processes to have running during `self update` are the `telemetry drain` process, `dotnetup dotnet`, and the `self update` process itself. All other processes are `non-safe`. A process does not know its 'safety' status until `S.CL` parsers or `args` are processed.<br><br>

- `dotnetup` must not require a reboot to update itself.<br><br>

- It's ok to ignore a 'rogue' `dotnetup` process and allow them to fail or incur behavioral runtime bugs; e.g. an old `dotnetup` version that does not know about or support any mutex, semaphore, or locks and therefore bypasses the conditional guarantees. This is permissible because `dotnetup` is not yet `stable` or in a fully public `preview`.

# Self Update Broad Approach

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

Let `S` be any `safe` dotnetup process: `dotnetup dotnet`, the telemetry drain process, and `P` itself.

Let `V_channel` be the build identity published by the configured dotnetup channel, and let `V_installed` be the build identity of `D/dotnetup.exe`.

Let `X_U`, `X_A`, `X_N`, and `X_V` be the bounded timeouts defined in rule 3 of Algorithm 1.

`A` and `U` are reader/writer locks rather than flags, so the meaning of each depends on which side is held.

| Lock | Shared ownership means | Exclusive ownership means |
| --- | --- | --- |
| `A` | "a `non-safe` command is running" | "no `non-safe` command is running, and none may start from the current executable" |
| `U` | unused; `U` is only ever opened exclusively | "a self-update transaction is in flight" |

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

Ownership across a complete transaction:

| Participant | Lock | Mode | Held for |
| --- | --- | --- | --- |
| `N` | `A` | shared | from just after parse until `N` exits |
| `N` | `U` | — | never acquired |
| `P` | `U` | exclusive | start of the transaction until `P` exits |
| `P` | `A` | exclusive | start of the transaction until `P` exits |
| `S` | `A` and `U` | — | never acquired; `S` skips the gate entirely |

`A` alone excludes `N` from a transaction, because `P` holds `A` exclusively for the whole transaction and an `N` that has retained `A` shared prevents `P` from ever acquiring it. `U` serializes `P` against peer `self update` processes and is never touched by `N`.

Because `S` holds neither lock, a self update can complete underneath `S`. `S` is `safe` only so long as `S` resolves every value derived from the dotnetup image at startup and caches it globally — `Environment.ProcessPath` above all, whose behavior is undefined [if the executable is renamed or deleted before the property is first accessed](https://learn.microsoft.com/dotnet/api/system.environment.processpath?view=net-10.0#remarks).

`install`, `uninstall`, `update`, and the other manifest-mutating commands continue to use the `ModifyInstallationStates` mutex for their own critical sections. `P` does not acquire `ModifyInstallationStates`, and no modification to that logic is necessary.

##### Algorithm 1 — Lock acquisition and the `non-safe` gate

Every acquisition of `A` or `U` either succeeds or throws `IOException` immediately; no open blocks in the kernel. "Wait" therefore always denotes a retry loop with jittered backoff bounded by a timeout.

**Rule 1 — `P` acquires `U` before `A`.** `N` acquires only `A`, so `P` is the sole participant that holds two locks. `U` is taken first so that `P` does not exclude every `N` while waiting out a peer `self update`.

**Rule 2 — no hold-and-wait during acquisition.** `P` never blocks on `A` while holding `U`. If the acquisition of `A` fails, `P` releases `U`, backs off, and retries the pair from step 1.1. Holding a lock while performing work is expected; holding one lock while waiting for the other is forbidden, and is the only way this design could deadlock. Rule 2 is vacuous for `N`, which performs a single acquisition and therefore holds nothing while waiting.

**Rule 3 — asymmetric timeouts.**

| Timeout | Applies to | Magnitude |
| --- | --- | --- |
| `X_U` | `P` waiting on `U` held by a peer `self update` | generous |
| `X_A` | `P` waiting on `A` held by `N` | brief |
| `X_N` | `N` waiting at the gate during a self update | the length of a typical update |
| `X_V` | `P` waiting for the verification child of step 2.6 | brief |

###### Lock Acquisition for `P`

**1.1 — `P` acquires `U` exclusively.** A busy `U` means a peer `self update` holds `U`. `P` backs off and retries for up to `X_U`, then re-evaluates whether an update is still required. `P` does not fail the user for contention on `U`; on expiry of `X_U`, `P` fails with `DotnetupBusyWithAnotherUpdate`.

**1.2 — `P` acquires `A` exclusively.** A busy `A` means at least one `N` is running. Per rule 2, `P` releases `U`, backs off, and retries the pair from step 1.1 for up to `X_A`. On expiry of `X_A`, `P` fails with `DotnetupBusyWithAnotherCommand` and reports the locking PID per step 1.7.

###### Lock Acquisition for `N`

**1.3 — `N` passes the gate.** The gate executes in `CommandBase.Execute`, after the parser has determined the safety status of the command and before any command body executes.

`N` acquires `A` shared and holds `A` until `N` exits. If the open throws, `N` backs off and retries for up to `X_N`. On expiry of `X_N`, `N` fails and reports that a self update is in progress.

A busy `A` unambiguously means a transaction is in flight, because `A` is only ever held exclusively by `P`; another `N` holding `A` shared does not block this one.

Safety is a property of the command: `CommandBase` classifies every command as `non-safe` by default, and only `dotnetup dotnet`, the telemetry drain, and `self update` override that default. A newly added command is therefore gated unless someone deliberately exempts it.

**1.4 — `N` verifies image identity.** At startup `N` records the file identity of the image of `N` — the volume serial number and file index returned by `GetFileInformationByHandle` on the cached `Environment.ProcessPath`. After the gate succeeds, `N` compares that identity to `D/dotnetup.exe`.

The comparison is unconditional. `N` performs the comparison even when the acquisition in step 1.3 succeeded on the first attempt, because a transaction that began before `N` launched and completed before `N` reached the gate replaces the image of `N` without `N` ever observing contention.

- **Identity matches.** No replacement occurred, including the rollback case of step 2.8, because renaming the backup back to the canonical path preserves the original file identity. `N` proceeds to the command body.
- **Identity differs.** `D/dotnetup.exe` was replaced while `N` waited, so the image of `N` is stale and `N` must not execute the command body of `N`. `N` forwards per step 1.5.

**1.5 — `N` forwards to the replaced executable.** `N` starts `D/dotnetup.exe` with the original `args`, `UseShellExecute = false`, and no stream redirection, so the child inherits stdin, stdout, and stderr, the working directory of `N`, and the environment of `N` plus an incremented `DOTNETUP_FORWARD_DEPTH`. `N` retains the shared handle on `A` for the lifetime of the child, waits for the child, and returns the exit code of the child via `SetExitCode`.

`D/dotnetup.exe` is resolved as the canonical, dotnetup-owned path and is not followed through an unexpected symbolic link or reparse point. Forwarding is capped at a `DOTNETUP_FORWARD_DEPTH` of 2; beyond that `N` fails rather than hopping again. `N` emits a telemetry event for the forward and does not emit a command-completion event, because the child emits one. Forwarding breaks if the replacement executable renamed or removed the command that `N` was invoked with.

Forwarding is deferred to a later implementation. Until forwarding lands, an `N` whose image identity differs fails and instructs the user to re-run the command. `N` never executes the stale command body of `N` in either case.

**1.6 — Work permitted before the gate.** Exactly three things may execute before the gate: console encoding and UI language setup, capture of `Environment.ProcessPath` and the image identity, and parsing.

Parsing must not read the manifest, enumerate `D/`, or touch the network.

The first-run telemetry notice is the only pre-gate write. That notice targets the telemetry directory rather than `D/`, and its sentinel keeps the notice idempotent across a forward. An `N` blocked at the gate has therefore touched no installation state and can forward or fail cleanly.

###### Common to `P` and `N`

**1.7 — Reporting the lock holder.** When an acquisition fails, dotnetup queries Windows Restart Manager to report the locking process and PID on a best-effort basis. This requires a `FileLockDetector`-style helper such as [commit `7fcc618e03f`](https://github.com/dotnet/sdk/commit/7fcc618e03f1520f688fa86bc7ade67aa417e380) via `RmRegisterResources`/`RmGetList` integration. The reported process may exit before the report is produced, and failure to identify the reported process does not change the outcome of the acquisition.

##### Algorithm 2 — The update transaction

Algorithm 2 begins once `P` holds both `U` and `A` per steps 1.1 and 1.2. `P` holds both locks until `P` exits, and `P` performs every step itself; no second process participates in the replacement.

**2.1 — `P` determines whether an update is required.** `P` compares `V_installed` with `V_channel`. If the two identities are equal, `P` releases `A` and `U` and exits successfully.

**2.2 — `P` clears stale artifacts.** `P` deletes `D/dotnetup.exe.new` if present, and performs a best-effort delete of every `D/dotnetup.exe.old.*` backup. Failure to delete a backup belonging to an older transaction is not fatal. Failure to delete `D/dotnetup.exe.new` returns `InsufficientPermissionsToUpdate` with the locking-process details from step 1.7.

Backups are named `D/dotnetup.exe.old.<t>` so that a backup still locked by an older process cannot prevent a later transaction from staging.

**2.3 — `P` stages and validates the replacement.** `P` downloads the replacement executable directly to `D/dotnetup.exe.new` and validates the staged file in place. Preview builds validate the published hash as an integrity check and warn explicitly that the artifact is not authenticated. Stable builds validate signed release metadata against the executable. `P` neither executes `D/dotnetup.exe.new` nor renames `D/dotnetup.exe.new` over any path until validation succeeds.

`D/dotnetup.exe.new` is staged inside `D/` rather than in a temporary directory because step 2.5 requires a same-volume rename, and a temporary directory is frequently on a different volume. Unvalidated bytes sitting briefly in `D/` are not reachable: nothing resolves the `.new` suffix, and step 2.2 removes a stale `D/dotnetup.exe.new` at the start of every transaction.

The state of the file system after step 2.3:

```
D/dotnetup.exe.new   <- validated replacement
D/dotnetup.exe       <- installed executable, still running as P
```

**2.4 — `P` backs up the installed executable.** `P` renames `D/dotnetup.exe` to `D/dotnetup.exe.old.<t>`. Windows permits renaming an executable that is in use, including the image that `P` is itself executing.

**2.5 — `P` installs the replacement.** `P` renames `D/dotnetup.exe.new` to `D/dotnetup.exe`. Any dotnetup process started after step 2.5 resolves the replacement image, including child processes and external assets that a dotnetup process loads, and including the short-term telemetry drainer.

A crash between steps 2.4 and 2.5 leaves no executable at the canonical path. The success criteria permit that outcome, and the recovery is to re-run the get-dotnetup scripts.

**2.6 — `P` verifies the replacement.** `P` runs `D/dotnetup.exe --build-identity`, waits synchronously for up to `X_V`, and treats the replacement as valid only if the child exits with status `0` and reports exactly `V_channel`.

`--build-identity` is a hidden **option** on the root command, not a subcommand. Like the built-in `--version`, the action of `--build-identity` runs during `ParseResult.Invoke` and returns before any `CommandBase` is constructed, so `--build-identity` never reaches the gate of step 1.3. That is load-bearing rather than incidental: `P` holds both `A` and `U` exclusively while the child runs, so a gated child would block on its own opens and every transaction would fail. If the verification path ever becomes a subcommand, that subcommand must be classified `safe`.

`--build-identity` reads `AssemblyInformationalVersionAttribute` from the loaded assembly, exactly as `Parser.Version` already does, and writes only that identity to stdout. `--build-identity` must never read the identity off disk with `FileVersionInfo` against `Environment.ProcessPath`, which fails once the executable has been renamed. `--build-identity` disables telemetry and spawns no detached child processes.

**2.7 — `P` reports success.** `P` prints a success message including an aka.ms link describing how to install older versions, releases `A` and `U`, and exits.

**2.8 — `P` rolls back.** If `P` cannot start `D/dotnetup.exe`, the child does not exit with status `0`, or the reported identity is not `V_channel`, `P` attempts rollback while still holding both locks: `P` deletes `D/dotnetup.exe`, then renames `D/dotnetup.exe.old.<t>` back to `D/dotnetup.exe`, then reports an error. Rollback restores the original file identity at the canonical path, so an `N` waiting at the gate takes the identity-matches branch of step 1.4 and proceeds normally instead of forwarding. If `P` cannot delete `D/dotnetup.exe`, the replacement may be in use but unresponsive; `P` reports that dotnetup must be reinstalled, because the transaction cannot be unwound safely.

**2.9 — Deferred cleanup.** `D/dotnetup.exe`, now the replacement, performs best-effort cleanup of `D/dotnetup.exe.old.*` backups on later launches. Cleanup is age-bounded and never follows symbolic links or reparse points outside `D/`. Failure to delete a locked backup is not a transaction failure, because a process started before the transaction may still be executing that image.

##### Properties of Algorithms 1 and 2

Abrupt termination can leave `D/dotnetup.exe.new` or `D/dotnetup.exe.old.*` behind indefinitely if dotnetup is never run again. Naming each backup with `t` prevents those stale files from corrupting or blocking a later transaction, and steps 2.2 and 2.9 retry cleanup on every later launch.

A dependent application — a long-running VS Code window, for example — may hold `D/dotnetup.exe.old.<t>` for weeks. Step 2.9 tolerates that rather than failing, and consumers decide how to surface it to the user.

`FileStream` lock ownership is handle-based rather than thread-affine, so `A` and `U` may be held across an `await` and the entire transaction, download included, may be asynchronous. This is why `P` does not use the thread-affine `ScopedMutex`.

After a crash, `pkill`, or power loss at any step, running the get-dotnetup scripts restores `D/dotnetup.exe` at the canonical location.

#### Comparisons

`rustup` - Rustup [downloads and launches a separate updater](https://github.com/rust-lang/rustup/blob/main/src/cli/self_update.rs), but its self-update path has no cross-process update lock, so two concurrent self-updates can interfere with the shared updater, installed executable, and proxy links. Its process handoff addresses Windows executable locking, not update serialization or crash-atomic replacement. Dotnetup needs no handoff at all, because renaming an in-use executable does not require one; see the rejected alternative below.

`Aspire CLI` - Aspire's archive self-update [extracts to a temporary directory, best-effort deletes older backups, renames the running executable to `aspire.exe.old.<unix-timestamp>`, copies the extracted executable to the canonical path, runs `aspire.exe --version`, and on any failure deletes the canonical path and moves the backup back](https://github.com/microsoft/aspire/blob/main/src/Aspire.Cli/Commands/UpdateCommand.cs). Steps 2.4 through 2.9 are that sequence, and dotnetup adopts it deliberately rather than inventing one.

Three things differ. Dotnetup renames a same-directory staged file where Aspire copies from a temporary directory, so dotnetup cannot leave a truncated executable at the canonical path and cannot fail on a cross-volume copy. Dotnetup requires the verification child to report exactly `V_channel`, where Aspire requires only exit status `0` and prints whatever version is returned, so a binary that runs but is the wrong build passes Aspire's check. And Aspire takes no cross-process lock, so concurrent self-updates race over the canonical path and the backups, and nothing stops another Aspire command from running across the replacement — the two problems `U` and `A` exist to solve.

`VS Code` - VS Code's installed Windows updater combines a singleton main process, an [in-process update state machine](https://github.com/microsoft/vscode/blob/main/src/vs/platform/update/electron-main/abstractUpdateService.ts), native application/setup/updating/ready mutexes, staged versioned files, and [Inno Setup](https://github.com/microsoft/vscode/blob/main/build/win32/code.iss). This serializes Windows installers and blocks application startup during the final switch. The statement does not apply uniformly to every distribution: macOS delegates to Electron's updater, while ordinary Linux packages generally delegate installation to the package manager or download page. Dotnetup does not require VS Code's UI state machine or installer framework, but it adopts the narrower invariant that only one self-update transaction may modify its executable at a time.

## Linux:

Linux permits the pathname of a running executable to be replaced while the process continues executing the old inode. Algorithm 1 applies unchanged. Algorithm 2 applies with steps 2.4 and 2.5 replaced by the hard-link-and-move sequence below, and the rollback of step 2.8 replaced by a move of the backup back over the canonical path. The image identity of step 1.4 is the device and inode number reported by `stat` rather than the volume serial number and file index.

Let `D/dotnetup` be the installed executable, `D/dotnetup.new` the staged replacement, and `D/dotnetup.old.<t>` the backup.

`P` stages and validates `D/dotnetup.new` per step 2.3, sets the expected executable mode, and flushes `D/dotnetup.new` to disk. `P` refuses to update through an unexpected symbolic link and operates only on the canonical, dotnetup-owned install path. `P` then creates `D/dotnetup.old.<t>` as a hard link to `D/dotnetup` and performs a same-filesystem move of `D/dotnetup.new` over `D/dotnetup`. `P` runs `D/dotnetup --build-identity` per step 2.6; otherwise `P` moves `D/dotnetup.old.<t>` back over `D/dotnetup`. Step 2.9 cleans up `D/dotnetup.old.*` on later launches.

Because the move is a namespace replacement rather than a pair of renames, Linux has no window equivalent to the gap between steps 2.4 and 2.5 in which no executable exists at the canonical path.

A same-directory hard link preserves the old inode before replacement. Both the backup and the staged path must be on the same mounted filesystem as the installed executable.
```cs
File.CreateHardLink(backupPath, installedPath);
File.Move(stagedPath, installedPath, overwrite: true);

File.Move(backupPath, installedPath, overwrite: true); // upon failure
```

On a supported local Linux filesystem, the same-filesystem move maps to an atomic namespace replacement: new openers observe either the complete old inode or the complete new inode, while already-running processes continue using the old inode. This does not by itself guarantee persistence across power loss. The implementation flushes the staged file before replacement and, where strict durability is required, synchronizes the containing directory after replacement.

Cross-filesystem `File.Move` may degrade to copy/delete behavior which is why it is avoided.

Because the rollback move restores the original inode at the canonical path, an `N` waiting at the gate takes the identity-matches branch of step 1.4 exactly as on Windows.

#### Unix locking caveats

`A` and `U` do not carry the same weight on Unix as on Windows, and Algorithm 1 is correspondingly weaker there.

`FileShare` is mandatory on Windows and enforced by the kernel at `CreateFile`. On Unix, .NET implements `FileShare` with advisory `flock`, which binds only cooperating processes and can be disabled outright by the `System.IO.DisableFileLocking` AppContext switch or the `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` environment variable. An environment variable can therefore turn the entire gate into a no-op. An implementation that wants the guarantee should take the `flock` explicitly rather than relying on the implicit behavior of `FileStream`.

The reason `FileShare.Delete` is never requested does not carry to Unix either. Unlinking an open file is always permitted on Unix, so the hazard the rule exists to prevent — deleting a lock file and recreating it, leaving two processes holding "exclusive" access to different inodes — cannot be prevented by share mode. The mitigation is that `A` and `U` live in a directory owned by the current user and dotnetup never deletes them.

`flock` over NFS is historically unreliable. A `D/` on a network filesystem can silently degrade the gate.

Windows Restart Manager has no Unix equivalent, so step 1.7 degrades. Linux can parse `/proc/locks`, which lists `FLOCK` holders with PID; macOS has no comparable interface.

## macOS:

macOS follows the Linux replacement flow: the pathname of a running executable may be replaced, `File.CreateHardLink` and a same-filesystem `File.Move` behave as described above, and the image identity is the device and inode number from `stat`. The Unix locking caveats apply, except that `/proc/locks` does not exist, so step 1.7 cannot report a lock holder at all.

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
A named mutex has exactly one owner, so it cannot represent "N `non-safe` processes are alive"; holding it proves only that nobody is inside a critical section at that instant, which does not cover a `dotnetup sdk install` that is downloading an archive. It also has no fail-if-held semantics, so a contending process blocks rather than reacting, and it is thread-affine, so it cannot be held across an `await`. Handle-based `FileShare` locks give all three properties, and the OS closes the handles if a process is killed, so there is no abandoned-state ambiguity to reason about.

**Why `N` acquires only the activity lock.**
`P` holds `A` exclusively for the entire transaction, so `A` alone is a continuous signal that a transaction is in flight. An `N` that has acquired `A` shared and retained it excludes `P` completely: if `P` is mid-transaction the acquire by `N` fails, and if `P` is between steps 1.1 and 1.2 the acquire by `N` succeeds, after which step 1.2 fails and `P` releases `U` and backs off. There is no interleaving in which both proceed, and because `N` performs a single acquisition there is no window in which `N` holds nothing after having passed a check.

**Why `P` acquires the update lock before the activity lock.**
`U` serializes `P` against peer `self update` processes and `A` excludes `N`. Taking `U` first means `P` only begins excluding every `N` after `P` has won the race against peers; taking `A` first would hold every `non-safe` command out of the way for the whole of `X_U` while `P` waits on a peer that may itself be performing a long download.

`P` has no reason to open `A` shared first. A shared open succeeds while any number of `non-safe` processes hold `A` shared, so it would answer nothing. Only the exclusive open establishes that no `non-safe` process is running.

**Why nobody waits for one lock while holding the other.** `P` is the only participant that holds two locks. If `P` waited on `A` while holding `U`, a two-party cycle would form as soon as `non-safe` processes wait rather than fail: `N` holds `A` and waits for the transaction to end, while `P` holds `U` and waits for `A`. Releasing `U` between retry attempts removes that edge. The ability of `N` to wait comes from this rule, not from any acquisition ordering.

**Why the update lock stays updater-only.** Contention on `U` means a peer `self update`, which must be waited out and never failed. Contention on `A` means a `non-safe` process, which is waited on briefly and then failed. Keeping `N` off `U` entirely means the two cases can never be confused, and it removes any chance that a momentary open by `N` spuriously fails the acquire by `P` at step 1.1.

**Why forwarding instead of resuming.** A process that waited at the gate is still executing the old image. Resuming would run pre-update code against post-update state — for example a manifest written in a format the old code does not understand. Forwarding is only legal at the gate precisely because nothing has been mutated and nothing has been written to the console yet.

**Why `FileShare.Delete` is never requested.** A lock file that can be deleted while it is open allows a second process to create a new file at the same path, at which point two processes each hold "exclusive" access to different files. The lock files are permanent fixtures; leaving them behind costs nothing. For the same reason the locks are files under the per-user `D/` rather than `Global\` named objects, which another session can squat.


# Alternatives Considered:

### Content below is not proposed implementation but rather alternatives that we could implement.

Windows also has reboot-delayed renames, but this provides a poor experience for immediate updates as it requires a reboot.

#### Rejected Alternative: A Separate Replacer Process

In this alternative, `P` copies the validated executable to a throwaway location, starts it as a replacer process `R`, hands `U` over to `R` through an anonymous pipe while `P` retains `A`, and lets `R` perform the rename, verification, and rollback after `P` exits. The anonymous pipe makes the identity of `R` structural: there is no pipe name for another process to connect to, and the client handle value is meaningful only inside the process that inherited it.

This is not the proposed design, because `R` performs nothing that `P` cannot. Windows permits renaming an executable that is in use, including the image the renaming process is itself executing, so `P` can rename `D/dotnetup.exe` to the backup, install the replacement, and spawn the verification child without ever exiting. Aspire's self-update takes the same in-process shape.

The replacer structure costs a throwaway copy and its temporary directory, a handoff pipe and heartbeat, the `dotnetup self replacement` hidden command, an additional timeout, an exception to rule 2 of Algorithm 1 for the span in which `P` holds `A` while waiting on `R`, and a divergence between the Windows and Unix flows. It buys no additional crash safety: a crash between the backup rename and the install rename leaves no executable at the canonical path under either structure.

#### Rejected Alternative: Crash-Safe Replacement

The following `File.Replace` and process-draining design is not part of the final proposed update logic above.

In this alternative, `P` replaces steps 2.4 and 2.5 with a single atomic replacement:

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
