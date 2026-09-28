# Self Update Broad Approach

The approach below applies to both stages unless noted. Stage A uses immediate failure at the `non-safe` gate; Stage B adds bounded waiting and forwarding using the same locks and loaded-version check. Signed version manifests and monotonic update authorization are deferred to future stages: the current unsigned release metadata, checksums, and startup smoke check do not authenticate freshness or prevent replay.

## Self-update target

`self update` targets the direct NativeAOT `dotnetup` executable that started the current process. Its path is captured from the loaded process at startup, and [SelfUpdatePaths](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdatePaths.cs) derives the canonical executable, locks, staging file, and backups from that location. The command does not resolve a separate expected user-wide or system-wide installation path. Therefore, independently installed copies update themselves in place; a managed development host is not treated as an installed copy and rejects self-update.

## Windows:

#### Stage A Update Logic

<a id="self-update-definitions"></a>

##### Definitions

Let `D/` be the directory containing the installed dotnetup executable, and let `D/dotnetup.exe` be that executable at its canonical path.

Let `t` be the transaction identifier: the first eight hexadecimal characters of a new GUID. An occupied backup path aborts replacement without overwriting the existing file.

Let `D/dotnetup.exe.new.download` be the download temporary file, `D/dotnetup.exe.new` the hash-validated staged replacement, and `D/dotnetup.exe.old.<t>` the backup of the executable being replaced. All are siblings of `D/dotnetup.exe` on the same volume. [SelfUpdatePaths](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdatePaths.cs) derives these paths from the captured executable path.

Let `A` be the activity lock, the file `D/dotnetup.activity.lock`.

Let `U` be the update lock, the file `D/dotnetup.update.lock`.

`A` and `U` are permanent, zero-length files created on first use and never deleted.

Let `P` be the `dotnetup self update` process that Algorithm 2 outlines.

Let `N` be any `non-safe` dotnetup process.

Let `S` be any `safe` dotnetup process other than `P`: `dotnetup dotnet` and the telemetry drain process. `self update` is classified `safe` as well, but `P` follows the update-lock acquisition protocol rather than the ordinary command gate. Parser-only actions such as help and version do not execute a command body and do not use that gate.

Let `V_channel` be the full version resolved from the selected channel for the target RID, and let `V_installed` be the full version reported by `D/dotnetup.exe --version`. The [version-query contract](self-update-verification.md) requires bounded successful execution and valid version output. Equality is local consistency, not content identity or authenticated freshness; builds reporting identical full versions cannot be distinguished. There is no custom embedded version/RID record or identity sidecar.

Let `X_U`, `X_A`, `X_N`, and `X_V` be the bounded timeouts defined in rule 3 of Algorithm 1.

`A` and `U` are reader/writer locks rather than flags, so the meaning of each depends on which side is held.

| Lock | Shared ownership means | Exclusive ownership means |
| --- | --- | --- |
| `A` | "a `non-safe` command is running" | "no `non-safe` command is running, and none may start from the current executable" |
| `U` | unused; `U` is only ever opened exclusively | "a self-update transaction or best-effort cleanup owns the update artifacts" |

`FileShare.Delete` is never requested on `A` or `U`.

Lock ownership during command execution and cleanup:

| Participant | Lock | Mode | Held for |
| --- | --- | --- | --- |
| `N` | `A` | shared | from the post-parse gate through command completion and telemetry flush |
| `N` | `U` | exclusive | optional cleanup only, after passing the gate and version check; one nonblocking attempt |
| `P` | `U` | exclusive | acquired transaction through verification/recovery and telemetry flush |
| `P` | `A` | exclusive | acquired transaction through verification/recovery and telemetry flush |
| `S` | `A` | — | never acquired; skips the gate |
| `S` | `U` | none | current safe commands do not perform cleanup |

`A` alone excludes `N` from a transaction, because `P` holds `A` exclusively for the whole transaction and an `N` that has retained `A` shared prevents `P` from ever acquiring it. `U` serializes self-update transactions and cleanup against each other. `N` does not acquire `U` to pass the gate or retain `U` for its command lifetime; its optional cleanup follows step 2.9.

Because `S` holds neither lock, a self update can complete underneath it. `S` must cache image-derived values at startup, particularly the executable path and loaded version. Capturing a path does not identify the loaded image: the parent's version comes from its loaded assembly's informational version, never by querying that path. See [Program](../../../../src/Installer/dotnetup.Library/Program.cs) and [DotnetupProcessInfo](../../../../src/Installer/dotnetup.Library/DotnetupProcessInfo.cs).

<a id="lock-acquisition-algorithm"></a>

##### Algorithm 1 — Lock acquisition and the `non-safe` gate

**Rule 1 — `P` acquires `U` before `A`.** `U` is taken first so that `P` does not exclude every `N` while waiting out a peer `self update` or cleanup. `N` acquires only `A` at the gate; after passing its version check, it may also hold `U` briefly for cleanup under step 2.9.

**Rule 2 — no hold-and-wait during acquisition.** `P` never blocks on `A` while holding `U`. If the acquisition of `A` fails, `P` releases `U`, backs off, and retries the pair from step 1.1. An `N` waiting at the gate holds neither lock. An `N` that already holds `A` may try to acquire `U` once for cleanup, but skips cleanup immediately if that attempt fails. Cleanup never acquires or waits for additional locks while holding `U`, and releases `U` before continuing command execution.

**Rule 3 — asymmetric timeouts.**

| Timeout | Applies to | Magnitude |
| --- | --- | --- |
| `X_U` | `P` waiting on `U` held by a peer `self update` or cleanup | one minute of cumulative contention |
| `X_A` | `P` waiting on `A` held by `N` | two seconds of cumulative contention |
| `X_N` | `N` waiting at the gate during a self update (Stage B only) | the length of a typical update |
| `X_V` | any installed-version query or the verification child of step 2.6 | 15 seconds |

Cleanup does not use these retry timeouts: it makes one nonblocking attempt to acquire `U` and skips cleanup if ownership cannot be established.

###### Lock Acquisition for `P`

<a id="check-available-update"></a>

**1.0 — `P` checks for an available update before acquiring any lock.** `P` resolves `V_channel` and compares it with the version reported by the canonical executable's `--version` command. For versions in the same semantic channel—the first prerelease identifier, or stable when there is no prerelease identifier—the resolved version must be newer. A version from a different semantic channel is eligible even when its SemVer precedence is lower, so an explicit channel transition is not mistaken for a downgrade. If no update is eligible, `P` exits successfully without acquiring `U` or `A`.

The canonical version query is advisory: it is never the basis for replacing or deleting an executable. A failed query, including an observation of the step 2.4 replacement window, falls through to step 1.1 and the authoritative check under both locks. Release resolution is different: a network or release-metadata failure stops the command before either lock is acquired. The resolved release, including `V_channel`, is pinned for the rest of the transaction. See [SelfUpdateWorkflow.Execute](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs).

Querying the canonical version without holding `U` is acceptable here and is not in step 2.9, because the two queries gate different actions. Step 2.9 uses the value to delete backups, which is irreversible, so it queries under `U`. Step 1.0 uses the value only to decide whether to continue; a transiently absent or unstartable canonical path cannot authorize replacement or cleanup.

Step 1.0 exists because the common invocation is a poll that finds nothing to do. Without it, every such poll takes `A` exclusively, excludes every `N` on the machine for the duration of the channel lookup, and can fail with `DotnetupBusyWithAnotherCommand` having accomplished nothing. A `self update` run with no network connectivity likewise fails without blocking any other command.

The check is placed before both locks rather than between steps 1.1 and 1.2 deliberately. Resolving `V_channel` is a network operation, so performing it while holding `U` would stretch the interval between acquiring `U` and acquiring `A` from two file opens to a network round trip. More `N` processes would accumulate in that interval, `P` would fail step 1.2 more often and restart the pair under rule 2, and the longer hold on `U` would cause the single nonblocking cleanup attempt of step 2.9 to be skipped more often.

**1.1 — `P` acquires `U` exclusively.** A busy `U` means a peer `self update` or cleanup holds `U`. `P` backs off and retries for up to `X_U`, then re-evaluates whether an update is still required after acquiring both locks. `P` does not fail immediately for contention on `U`; on expiry of `X_U`, `P` fails with `DotnetupBusyWithUpdateOrCleanup` and reports that another update or cleanup is busy, with guidance to retry after it completes. Holder identification is deferred under step 1.7.

**1.2 — `P` acquires `A` exclusively.** A busy `A` means at least one `N` is running. Per rule 2, `P` releases `U`, backs off, and retries the pair from step 1.1 for up to `X_A`. On expiry of `X_A`, `P` fails with `DotnetupBusyWithAnotherCommand` and reports that another dotnetup command is running, with guidance to retry after it completes.

###### Lock Acquisition for `N`

<a id="non-safe-command-gate"></a>

**1.3 — `N` passes the gate.** The gate executes in `CommandBase.Execute`, after the parser has determined the safety status of the command and before any command body executes.

`N` acquires `A` shared and holds `A` until `N` exits. In Stage A, if the open fails because `A` is busy, `N` fails immediately, reports that a self update is in progress, and instructs the caller to re-run the command after it completes. In Stage B, `N` instead backs off and retries for up to `X_N`, unless the caller has opted into immediate failure. On expiry of `X_N`, `N` fails and reports that a self update is in progress.

A busy `A` unambiguously means a transaction is in flight, because `A` is only ever held exclusively by `P`; another `N` holding `A` shared does not block this one.

Safety is a property of the command: `CommandBase` classifies every command as `non-safe` by default, and only `dotnetup dotnet`, the telemetry drain, and `self update` override that default. A newly added command is therefore gated unless someone deliberately exempts it.

**1.4 — `N` checks the installed version.** After acquiring `A` shared, `N` runs the canonical executable with `--version` while retaining `A` and compares the result with its own cached loaded assembly version. Comparison is ordinal equality of the full version string, including build metadata. The [version-query contract](self-update-verification.md) defines child startup, encoding, validation, and time limits. The child acquires neither lock and does not perform cleanup.

The comparison is unconditional, even if acquiring `A` succeeded immediately: replacement may have completed before `N` reached the gate. `N` retains `A` through its command body or forwarding so a cooperating updater cannot change the canonical build underneath the check. Failed startup, timeout, nonzero exit, or invalid version output stop the command; an unknown version never compares equal.

- **Identity matches.** The loaded and installed builds agree, including after rollback to the loaded build. `N` proceeds to the command body.
- **Identity differs.** The loaded build is no longer installed, so `N` must not execute its command body. In Stage A, `N` fails and instructs the caller to re-run the command. In Stage B, `N` forwards per step 1.5.

**1.5 — `N` forwards to the replaced executable (Stage B only).** `N` starts `D/dotnetup.exe` with the original `args`, `UseShellExecute = false`, and no stream redirection, so the child inherits stdin, stdout, and stderr, the working directory of `N`, and the environment of `N` plus an incremented `DOTNETUP_FORWARD_DEPTH`. `N` retains the shared handle on `A` for the lifetime of the child, waits for the child, and returns the exit code of the child via `SetExitCode`.

`D/dotnetup.exe` is resolved as the canonical, dotnetup-owned path and is not followed through an unexpected symbolic link or reparse point. Forwarding is capped at a `DOTNETUP_FORWARD_DEPTH` of 2; beyond that `N` fails rather than hopping again. `N` emits a telemetry event for the forward and does not emit a command-completion event, because the child emits one. Forwarding breaks if the replacement executable renamed or removed the command that `N` was invoked with.

Forwarding is deferred to Stage B. `N` never executes the stale command body of `N` in either stage.

<a id="pre-gate-work"></a>

**1.6 — Work permitted before the gate.** Startup captures the executable path and informational version from the loaded assembly. Except for the telemetry drainer, [Program](../../../../src/Installer/dotnetup.Library/Program.cs) starts root telemetry before language and console setup, shows the first-run notice, and parses arguments. Command constructors must not access installation state or start network work. Program creates a [SelfUpdateInvocation](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateInvocation.cs) when [DotnetupProcessInfo.IsDirectExecution](../../../../src/Installer/dotnetup.Library/DotnetupProcessInfo.cs) identifies a directly launched dotnetup executable. The telemetry drainer reuses the same check: the entry assembly must be dotnetup and the captured executable must not be the `dotnet` host. Renamed executables are supported. Execution through `dotnet` and test hosts remains invocation-free and rejects self-update. This check identifies the host, not NativeAOT packaging; a managed dotnetup apphost can pass it. Self-update is supported for the published standalone executable, not for updating a managed deployment's collection of files.

Parsing must not read the manifest, enumerate `D/`, or touch the network.

`N` must not perform cleanup before passing both the gate and the version check, or on a path that fails or forwards instead of executing its command body.

Root telemetry, the first-run telemetry notice, and its sentinel are permitted before the gate. [CommandBase.Execute](../../../../src/Installer/dotnetup.Library/CommandBase.cs) enters the gate before option telemetry or the command body. Program owns the root and catches startup failures, including encoding-scope creation and disposal failures; SelfUpdateInvocation owns coordination and leases only. A rejected command may write telemetry and the notice sentinel, but never runs its body or cleanup. Parser-only actions have no command gate. Acquired invocation leases survive command completion, root telemetry completion, and synchronous `FlushTelemetry`, and are disposed only as `Main` returns.

###### Common to `P` and `N`

<a id="contention-reporting"></a>

**1.7 — Reporting contention; holder identification deferred.** [NonSafeCommandGate](../../../../src/Installer/dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs) and [SelfUpdateWorkflow](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) report the contention category and retry guidance using localized messages and distinct error codes. They do not query operating-system process information or append process names/PIDs. Optional cleanup skips contention silently.

<a id="update-transaction-algorithm"></a>

##### Algorithm 2 — The update transaction

Algorithm 2 begins once `P` holds both `U` and `A` per steps 1.1 and 1.2. [SelfUpdateWorkflow](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) transfers acquired leases to the invocation, which retains them through telemetry flush on success or failure. `P` performs replacement and recovery itself; children only query the installed version or verify startup.

**2.1 — `P` determines whether an update is required.** `P` queries `V_installed` from the canonical executable under both locks and compares it with `V_channel`, not with `P`'s own loaded version. It repeats the semantic-channel and version-ordering check from step 1.0. If no update is eligible, the command reports no update needed and exits successfully; the invocation releases acquired locks after telemetry flush.

Step 2.1 is the authoritative check and is performed even when step 1.0 already reported an available update, because a peer `self update` can complete a transaction between step 1.0 and step 1.2. Reading `V_installed` from the canonical executable rather than from the loaded image of `P` is what lets `P` observe that peer's work and exit successfully instead of repeating it.

**2.2 — `P` clears stale artifacts.** `P` validates and removes `D/dotnetup.exe.new` and `D/dotnetup.exe.new.download` if present, and performs best-effort deletion of eligible backups subject to step 2.9. `P` uses its existing ownership of `U` and `A`; it does not reopen or release either lock for cleanup. Backup deletion failures are nonfatal. Unsafe or inaccessible staging files stop the update; the workflow maps I/O/access failures to `InstallFailed` rather than ignoring them.

Backups are named `D/dotnetup.exe.old.<t>` so that a backup still locked by an older process cannot prevent a later transaction from staging.

<a id="stage-and-validate-replacement"></a>

**2.3 — `P` stages and validates the replacement.** The shared [DotnetDownloader](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetDownloader.cs) writes network bytes or copied cache bytes to `D/dotnetup.exe.new.download`, verifies the pinned SHA-512 hash, and commits those validated bytes to `D/dotnetup.exe.new`. Committing the download is not replacement of the installed executable. Current daily builds are unsigned: the command emits the existing unsigned-source warning and honors the unsigned-download policy, including cache hits. Signed stable self-update metadata is future work, not the current delivery path. Neither staging path is executed.

The artifact RID is selected by the release resolver. There is no embedded version/RID scan. The installed replacement must execute and report `V_channel` during step 2.6; a wrong version or an executable that cannot run triggers rollback.

Both staging paths are inside `D/` to keep the eventual replacement on the destination volume. Unvalidated bytes remain under `.new.download`; hash-validated bytes become `.new`. Step 2.2 clears both stale staging names when an update is needed. These protections require a trusted installation directory and stable paths; staging names alone are not a security boundary.

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

<a id="handle-replacement-failure"></a>

**2.5 — `P` handles replacement failure.** `ReplaceFileW` is a single API call, not an ACID transaction. In addition to failures that leave all original names intact, Windows documents partial failures. [SelfUpdateReplacement](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs) records its transaction paths and replacement progress, then inspects the actual paths after failure without following reparse points. It attempts restoration from the transaction's backup without executing or reading a custom record from either image. An unrecoverable failure retains recovery artifacts, reports the paths, and directs the caller to reinstall.

<a id="smoke-test-replacement"></a>

**2.6 — `P` smoke-tests the replacement.** `P` runs `D/dotnetup.exe --version`, waits synchronously for up to `X_V`, and requires status `0` and nonempty parseable version output matching the selected release's SemVer precedence. A feed version with build metadata must match the full output exactly; otherwise an informational-version source revision suffix is permitted. The loaded-versus-installed comparisons in steps 1.4 and 2.9 always require full string equality. This tests startup and release consistency, not authenticated freshness.

**2.7 — `P` reports success.** `P` prints the installed version and the existing [dotnetup installation link](https://aka.ms/dotnet/dotnetup) for installing older versions. Acquired locks remain held through root telemetry completion and flush, then are released as `Main` returns.

<a id="rollback-replacement"></a>

**2.8 — `P` rolls back.** If `P` cannot start `D/dotnetup.exe`, the child times out, does not exit with status `0`, or reports an invalid or unexpected version, `P` attempts to terminate the verification child if it is still running, then attempts to restore the backup while still holding both locks. A failed kill or termination wait does not suppress rollback. [SelfUpdateVerifier](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateVerifier.cs) preserves that failure in the exception chain, and [SelfUpdateWorkflow](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) retains it when reporting verification or rollback failure. Restoring the canonical path does not establish that the verification child exited; file sharing may permit rollback while it remains alive. The original updater process stays alive throughout recovery. Rollback never needs to query the failed replacement's version.

The two renames are not atomic together: the canonical path is absent between them, and a crash or power loss can leave it absent. If the canonical path is already absent, `P` skips the first move and attempts only to restore the backup. `P` inspects paths without following unexpected reparse points and refuses occupied rollback destinations. It relies on the transaction and locks, not content identity, to identify its canonical candidate and backup.

If the first move fails, the rejected executable remains at the canonical path and the backup remains untouched. If the second move fails, the canonical path remains absent and both the backup and rejected executable are retained for recovery. `P` must not delete the backup on either failure; if restoration cannot be completed, `P` reports that dotnetup must be reinstalled.

Successful rollback restores the original executable at the canonical path. An `N` whose loaded full version matches that executable's `--version` output takes the matching branch of step 1.4; an `N` loaded from a different rejected version instead fails or forwards according to its stage. The rejected executable is left for the best-effort `D/dotnetup.exe.old.*` cleanup in step 2.9.

<a id="deferred-cleanup"></a>

**2.9 — Deferred cleanup.** On later launches, [SelfUpdateCleanup](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateCleanup.cs) may remove `D/dotnetup.exe.old.*` backups, including rejected executables left by rollback. An `N` attempts cleanup only after acquiring `A` shared and taking the version-matches branch of step 1.4, retaining `A` throughout cleanup. Current safe commands perform no optional cleanup; `P` performs it only within the locked transaction, and `--version` never performs cleanup.

Cleanup makes one nonblocking attempt to acquire `U` exclusively. If ownership cannot be established, cleanup is skipped without retrying, reporting contention, or failing the command. No participant opens `U` shared. A successful attempt retains `U` through enumeration, the canonical-version query, and deletion, so no cooperating self-update transaction can create or use a backup during cleanup. Cleanup never acquires or waits for additional locks. Its `--version` child is safe, takes neither lock, and does not recurse into cleanup.

Only when eligible aged backups exist does cleanup query the canonical executable's `--version` under `U` and require ordinal equality with its own loaded full version, including build metadata. If the executable is absent, cannot run, times out, produces invalid output, or reports a different version, cleanup leaves the backups untouched. This conservative check preserves recovery artifacts when the canonical version cannot be confirmed; acquiring `U` alone does not prove that an earlier transaction completed successfully. The same check applies to backup deletion in step 2.2.

##### Properties of Algorithms 1 and 2

**The canonical path may be briefly unavailable during step 2.4.** Windows measurements observed file-not-found during `File.Replace`. The [implementation](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs) uses one forward replacement call instead of deliberately splitting it into moves, but neither that code nor the [ReplaceFileW contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew#return-value) guarantees gapless concurrent opens. Measurement timing is not a platform guarantee.

Consumers launching `dotnetup` programmatically should retry transient file-not-found errors with a bounded delay before treating the installation as missing. Retry ordinary commands rejected at the Stage A gate after the update completes. Windows rollback deliberately uses two non-overwriting renames and can also leave a gap. Unix same-filesystem moves use the runtime's rename behavior on supported local filesystems, not a power-loss-durable transaction; macOS behavior remains unverified in this implementation handoff.

Abrupt termination can leave `D/dotnetup.exe.new` or `D/dotnetup.exe.old.*` behind indefinitely if dotnetup is never run again. Naming each backup with `t` prevents those stale files from corrupting or blocking a later transaction. Step 2.2 clears stale staging files during a later update; backup cleanup in steps 2.2 and 2.9 is opportunistic and runs only when its ownership and canonical-build checks permit it.

A dependent application — a long-running VS Code window, for example — may hold `D/dotnetup.exe.old.<t>` for weeks. Step 2.9 tolerates that rather than failing, and consumers decide how to surface it to the user.

After a crash, forced termination, or power loss, use the [get-dotnetup scripts](https://aka.ms/dotnet/dotnetup) to reinstall when the canonical executable is unavailable. Recovery still requires a writable, functioning filesystem; the protocol does not guarantee automatic recovery from every interruption.

<a id="linux-replacement"></a>

## Linux:

Linux permits the pathname of a running executable to be replaced while the process continues executing the old inode. Algorithm 1 applies unchanged. Algorithm 2 applies with the Windows replacement and failure handling of steps 2.4 and 2.5 replaced by the hard-link-and-move sequence below, and the rollback of step 2.8 replaced by a move of the backup back over the canonical path. Step 1.4 uses the same `--version` query as Windows, not device/inode identity.

Let `D/dotnetup` be the installed executable, `D/dotnetup.new` the staged replacement, and `D/dotnetup.old.<t>` the backup.

`P` stages and validates `D/dotnetup.new` per step 2.3, preserves the installed executable's Unix mode, and flushes `D/dotnetup.new` to disk. `P` rejects unexpected symbolic links observed during pathname validation and operates on the canonical install path. `P` then creates `D/dotnetup.old.<t>` as a hard link to `D/dotnetup` and performs a same-filesystem move of `D/dotnetup.new` over `D/dotnetup`. `P` runs `D/dotnetup --version` per step 2.6; on smoke-check failure `P` moves `D/dotnetup.old.<t>` back over `D/dotnetup`. Step 2.9 governs cleanup of `D/dotnetup.old.*` on later launches, including exclusive nonblocking acquisition of `U`.

A same-directory hard link preserves the old inode before replacement. Both the backup and the staged path must be on the same mounted filesystem as the installed executable.
The implemented operations are in [SelfUpdateReplacement](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs). Rollback validates transaction state and paths before restoring the backup with `File.Move(backupPath, installedPath, overwrite: true)`. It does not execute the rejected candidate or compare embedded records.

On a supported local Linux filesystem, new openers observe either the complete old inode or the complete new inode, while already-running processes continue using the old inode. The implementation flushes the staged file but does not perform a containing-directory durability sync or claim persistence across power loss.

Cross-filesystem `File.Move` may degrade to copy/delete behavior. Keeping all transaction files as siblings in a stable directory avoids that scenario; this is a layout requirement, not a custom native rename guarantee.

The rollback move restores the old executable. Step 1.4 permits an `N` with the matching full loaded version to proceed and rejects or forwards one loaded from a different rejected version, exactly as on Windows.

<a id="unix-locking-caveats"></a>

#### Unix locking caveats

`A` and `U` do not carry the same weight on Unix as on Windows, and Algorithm 1 is correspondingly weaker there.

`FileShare` is mandatory on Windows and enforced by the kernel at `CreateFile`. On Unix, .NET implements `FileShare` with advisory `flock`, which binds only cooperating processes and can be disabled outright by the `System.IO.DisableFileLocking` AppContext switch or the `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` environment variable. Dotnetup accepts the same locking compatibility as the .NET runtime: it uses `FileStream` sharing directly, does not take an additional native `flock`, and does not detect or compensate for disabled or ineffective runtime locking. The concurrency guarantees assume working runtime locking and cooperating processes.

The reason `FileShare.Delete` is never requested does not carry to Unix either. Unlinking an open file is always permitted on Unix, so the hazard the rule exists to prevent — deleting a lock file and recreating it, leaving two processes holding "exclusive" access to different inodes — cannot be prevented by share mode. The mitigation is that `A` and `U` live in a directory owned by the current user and dotnetup never deletes them.

`flock` over NFS is historically unreliable. A `D/` on a network filesystem can silently degrade the gate.
