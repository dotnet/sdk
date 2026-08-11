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

- `non-safe` processes must never execute their command body across a `self update` boundary. A `non-safe` process that starts while `self update` is running waits at its gate rather than failing outright, so `dotnetup list` and IDE-issued commands do not hard-fail during an update. If the executable was replaced while it waited, it must forward the invocation to the updated executable and return that process's exit code; it must never resume running its own now-stale code. If breaking changes are made to command names themselves, then this will break and that is acceptable.

- `self update` does NOT make other `self update` processes fail or exit immediately; other `self update` processes must merely wait for the other update processes to complete and then determine that an update is no longer needed, assuming no release occurs within the time frame of the race.

- At this time, the only 'safe' `dotnetup` processes to have running during `self update` are the `telemetry drain` process, `dotnetup dotnet`, and the `self update` process itself. All other processes are `non-safe`. A process does not know its 'safety' status until `S.CL` parsers or `args` are processed.

- `dotnetup` must not require a reboot to update itself.

- It's ok to ignore a 'rogue' `dotnetup` process and allow them to fail or incur behavioral runtime bugs; e.g. an old `dotnetup` version that does not know about or support any mutex, semaphore, or locks and therefore bypasses the conditional guarantees. This is permissible because `dotnetup` is not yet `stable` or in a fully public `preview`.

# Self Update Broad Approach

## Windows:

#### Final Proposed Update Logic:

##### Definitions

Let `D/` be the directory containing the installed dotnetup executable, and let `D/dotnetup.exe` be that executable at its canonical path.

Let `T/` be a randomly named, current-user-only temporary directory of the form `tmp/dotnetup/<random>/`, and let `T/dotnetup.exe` be the throwaway executable copied into `T/`.

Let `t` be the transaction identifier: a random value unique to a single execution of `dotnetup self update`, used to name the backup `D/dotnetup.exe.old.<t>`.

Let `A` be the activity lock, the file `D/dotnetup.activity.lock`.

Let `U` be the update lock, the file `D/dotnetup.update.lock`.

`A` and `U` are permanent, zero-length files created on first use and never deleted.

Let `P` be the `dotnetup self update` process that Algorithm 2 outlines.

Let `R` be the replacer process — `T/dotnetup.exe` started by `P` via the hidden command `dotnetup self replacement`.

Let `N` be any `non-safe` dotnetup process.

Let `S` be any `safe` dotnetup process: `dotnetup dotnet`, the telemetry drain process, and `P` itself.

Let `V_channel` be the build identity published by the configured dotnetup channel, and let `V_installed` be the build identity of `D/dotnetup.exe`.

Let `X_U`, `X_A`, `X_N`, and `X_R` be the bounded timeouts defined in rule 3 of Algorithm 1.

`A` and `U` are reader/writer locks rather than flags, so the meaning of each depends on which side is held.

| Lock | Shared ownership means | Exclusive ownership means |
| --- | --- | --- |
| `A` | "a `non-safe` command is running" | "no `non-safe` command is running, and none may start from the current executable" |
| `U` | nothing; `U` is opened shared only to observe whether an exclusive owner exists | "a self-update transaction is in flight" |

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
| `N` | `U` | shared | a momentary probe, released on the next line |
| `P` | `U` | exclusive | start of the transaction until `P` releases `U` for the handoff |
| `P` | `A` | exclusive | start of the transaction until `R` confirms ownership of `U`, then released as `P` exits |
| `R` | `U` | exclusive | the handoff until rename, verification, and any rollback have completed |
| `R` | `A` | — | never acquired |
| `S` | `A` and `U` | — | never acquired; `S` skips the gate entirely |

Because `S` holds neither lock, a self update can complete underneath `S`. `S` is `safe` only so long as `S` resolves every value derived from the dotnetup image at startup and caches it globally — `Environment.ProcessPath` above all, whose behavior is undefined [if the executable is renamed or deleted before the property is first accessed](https://learn.microsoft.com/dotnet/api/system.environment.processpath?view=net-10.0#remarks).

`install`, `uninstall`, `update`, and the other manifest-mutating commands continue to use the `ModifyInstallationStates` mutex for their own critical sections. `P` does not acquire `ModifyInstallationStates`, and no modification to that logic is necessary.

##### Algorithm 1 — Lock acquisition and the `non-safe` gate

Every acquisition of `A` or `U` either succeeds or throws `IOException` immediately; no open blocks in the kernel. "Wait" therefore always denotes a retry loop with jittered backoff bounded by a timeout.

**Rule 1 — opposite ordering.** `N` acquires `A` before `N` touches `U`. `P` acquires `U` before `P` acquires `A`.

**Rule 2 — no hold-and-wait during acquisition.** While acquiring the pair, neither `N` nor `P` blocks on one lock while holding the other. If the second acquisition fails, the first is released, the acquiring process backs off, and the pair is retried from the beginning. Holding a lock while performing work is expected; holding one lock while waiting for the other is forbidden, and is the only way the opposite ordering of rule 1 could deadlock.

**Rule 3 — asymmetric timeouts.**

| Timeout | Applies to | Magnitude |
| --- | --- | --- |
| `X_U` | `P` waiting on `U` held by a peer `self update` | generous |
| `X_A` | `P` waiting on `A` held by `N` | brief |
| `X_N` | `N` waiting at the gate during a self update | the length of a typical update |
| `X_R` | the `P`-to-`R` heartbeat, and `R` waiting on `U` | brief |

**Exception to rule 2.** During step 2.6 of Algorithm 2, `P` holds `A` while `P` waits for `R` to acquire `U`. That edge cannot close a cycle, because rule 1 stops a newly launched `N` at `A`, so no participant other than `R` can be holding `U` at that moment.

###### Lock Acquisition for `P`

**1.1 — `P` acquires `U` exclusively.** A busy `U` means a peer `self update` holds `U`. `P` backs off and retries for up to `X_U`, then re-evaluates whether an update is still required. `P` does not fail the user for contention on `U`; on expiry of `X_U`, `P` fails with `DotnetupBusyWithAnotherUpdate`.

**1.2 — `P` acquires `A` exclusively.** A busy `A` means at least one `N` is running. Per rule 2, `P` releases `U`, backs off, and retries the pair from step 1.1 for up to `X_A`. On expiry of `X_A`, `P` fails with `DotnetupBusyWithAnotherCommand` and reports the locking PID per step 1.7.

###### Lock Acquisition for `N`

**1.3 — `N` passes the gate.** The gate executes in `CommandBase.Execute`, after the parser has determined the safety status of the command and before any command body executes.

1. `N` acquires `A` shared. `N` holds `A` until `N` exits.
2. `N` acquires `U` shared, then releases `U` immediately.
3. If either open throws, `N` releases whatever `N` holds, backs off, and retries from sub-step 1 for up to `X_N`. On expiry of `X_N`, `N` fails and reports that a self update is in progress.

Safety is a property of the command: `CommandBase` classifies every command as `non-safe` by default, and only `dotnetup dotnet`, the telemetry drain, and `self update` override that default. A newly added command is therefore gated unless someone deliberately exempts it.

**1.4 — `N` verifies image identity.** At startup `N` records the file identity of the image of `N` — the volume serial number and file index returned by `GetFileInformationByHandle` on the cached `Environment.ProcessPath`. After the gate succeeds, `N` compares that identity to `D/dotnetup.exe`.

The comparison is unconditional. `N` performs the comparison even when both opens in step 1.3 succeeded on the first attempt, because a transaction that began before `N` launched and completed before `N` reached the gate replaces the image of `N` without `N` ever observing contention.

- **Identity matches.** No replacement occurred, including the rollback case of step 2.11, because renaming the backup back to the canonical path preserves the original file identity. `N` proceeds to the command body.
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

Algorithm 2 begins once `P` holds both `U` and `A` per steps 1.1 and 1.2.

**2.1 — `P` determines whether an update is required.** `P` compares `V_installed` with `V_channel`. If the two identities are equal, `P` releases `A` and `U` and exits successfully.

**2.2 — `P` clears stale artifacts.** `P` deletes `D/dotnetup.exe.new` if present, and performs a best-effort delete of every prior `tmp/dotnetup/<random>/` directory and every `D/dotnetup.exe.old.*` backup. Failure to delete a `tmp/dotnetup/<random>/` directory or a backup belonging to an older transaction is not fatal. Failure to delete an artifact that the current transaction requires returns `InsufficientPermissionsToUpdate` with the locking-process details from step 1.7.

Backups are named `D/dotnetup.exe.old.<t>` so that a backup still locked by an older process cannot prevent a later transaction from staging.

**2.3 — `P` downloads the replacement.** `P` downloads the replacement executable into `T/` as `T/dotnetup.exe`. Preview builds validate the published hash as an integrity check and warn explicitly that the artifact is not authenticated. Stable builds validate signed release metadata against the executable.

**2.4 — `P` stages the replacement.** `P` copies `T/dotnetup.exe` to `D/dotnetup.exe.new` and validates the staged copy again before `D/dotnetup.exe.new` is executed or installed.

The state of the file system after step 2.4:

```
D/dotnetup.exe.new   <- validated replacement
D/dotnetup.exe       <- installed executable, still running as P
T/dotnetup.exe       <- throwaway replacer
```

**2.5 — `P` starts `R`.** While still holding `A` exclusively, `P` releases the handle of `P` on `U`, creates an `AnonymousPipeServerStream` whose client handle is inheritable, and starts `T/dotnetup.exe` via the hidden command `dotnetup self replacement <T/dotnetup.exe>` with that client handle passed on the command line. `R` inherits stdin, stdout, and stderr from `P`.

**2.6 — `R` takes ownership of `U`.** `R` acquires `U` exclusively, then writes the ready message to the inherited pipe handle. If `R` cannot acquire `U` within `X_R`, `R` reports failure and exits without modifying any executable.

The handoff pipe is anonymous, so the identity of `R` is structural rather than checked: there is no pipe name for another process to connect to, and the client handle value is meaningful only inside the process that inherited it. A replacer left behind by an earlier, crashed transaction therefore cannot satisfy the handoff of the current transaction. No handoff token is required, and a token would be weaker — a token passed on the command line of `R` is readable by any process that can enumerate command lines.

Authenticating `R` more strongly is not meaningful. `D/` is writable by the current user, so any process already running as that user can replace `D/dotnetup.exe` directly instead of impersonating `R`.

**2.7 — `P` completes the handoff.** On a valid heartbeat, `P` releases `A` and exits. Because `P` holds `A` until `R` confirms ownership of `U`, at least one of `A` and `U` is held continuously across the handoff. If `P` does not receive the heartbeat within `X_R`, `P` fails with `DotnetupReplacerCommunicationFailure` and attempts to kill `R`.

**2.8 — `R` replaces the executable.** `R` renames `D/dotnetup.exe` to `D/dotnetup.exe.old.<t>` first, which confirms that `P` exited and that no process restarted `D/dotnetup.exe`. `R` then renames `D/dotnetup.exe.new` to `D/dotnetup.exe`. Windows permits renaming an executable that is in use. One consequence is that any dotnetup process started after step 2.8 resolves the replacement image when spawning child processes or loading external assets, including the short-term telemetry drainer.

**2.9 — `R` verifies the replacement.** `R` retains `U` and runs `D/dotnetup.exe --build-identity`. `R` waits synchronously for up to `X_R` and treats the replacement as valid only if the child exits with status `0` and reports exactly `V_channel`.

`--build-identity` is a hidden **option** on the root command, not a subcommand. Like the built-in `--version`, the action of `--build-identity` runs during `ParseResult.Invoke` and returns before any `CommandBase` is constructed, so `--build-identity` never reaches the gate of step 1.3. That is load-bearing rather than incidental: `R` holds `U` exclusively while the child runs, so a gated child would block on its own probe of `U` and every transaction would fail. If the verification path ever becomes a subcommand, that subcommand must be classified `safe`.

`--build-identity` reads `AssemblyInformationalVersionAttribute` from the loaded assembly, exactly as `Parser.Version` already does, and writes only that identity to stdout. `--build-identity` must never read the identity off disk with `FileVersionInfo` against `Environment.ProcessPath`, which fails once the executable has been renamed. `--build-identity` disables telemetry and spawns no detached child processes.

**2.10 — `R` reports success.** `R` prints a success message including an aka.ms link describing how to install older versions, releases `U`, and exits.

**2.11 — `R` rolls back.** If `R` cannot start `D/dotnetup.exe`, the child does not exit with status `0`, or the reported identity is not `V_channel`, `R` attempts rollback while still holding `U`: `R` deletes `D/dotnetup.exe`, then renames `D/dotnetup.exe.old.<t>` back to `D/dotnetup.exe`, then reports an error. Rollback restores the original file identity at the canonical path, so an `N` waiting at the gate takes the identity-matches branch of step 1.4 and proceeds normally instead of forwarding. If `R` cannot delete `D/dotnetup.exe`, the replacement may be in use but unresponsive; `R` reports that dotnetup must be reinstalled, because the transaction cannot be unwound safely.

**2.12 — Deferred cleanup.** `D/dotnetup.exe`, now the replacement, performs best-effort cleanup of prior `tmp/dotnetup/<random>/` directories and `D/dotnetup.exe.old.*` backups on later launches. Cleanup is age-bounded and never follows symbolic links or reparse points outside the update directories that dotnetup owns. Failure to delete a locked backup is not a transaction failure, because a process started before the transaction may still be executing that image.

##### Properties of Algorithms 1 and 2

Abrupt termination can leave `tmp/dotnetup/<random>/` directories or `D/dotnetup.exe.old.*` backups behind indefinitely if dotnetup is never run again. Naming each backup with `t` prevents those stale files from corrupting or blocking a later transaction, and step 2.12 retries cleanup on every later launch.

A dependent application — a long-running VS Code window, for example — may hold `D/dotnetup.exe.old.<t>` for weeks. Step 2.12 tolerates that rather than failing, and consumers decide how to surface it to the user.

`FileStream` lock ownership is handle-based rather than thread-affine, so `A` and `U` may be held across an `await` and the entire transaction, download included, may be asynchronous. This is why `P` does not use the thread-affine `ScopedMutex`.

After a crash, `pkill`, or power loss at any step, running the get-dotnetup scripts restores `D/dotnetup.exe` at the canonical location.

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
