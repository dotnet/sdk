# Self-update Algorithm Implementations

This document records implementation-specific details for the
[self-update algorithm](self-update-algorithm.md). Links use semantic Markdown headings so
the algorithm can be reordered or renumbered without changing this document.

## Lock files

See the algorithm's [lock definitions](self-update-algorithm.md#definitions)
and [lock-acquisition protocol](self-update-algorithm.md#lock-acquisition-and-the-non-safe-gate).

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

`install`, `uninstall`, `update`, and the other manifest-mutating commands continue to use the `ModifyInstallationStates` mutex for their own critical sections. `P` does not acquire `ModifyInstallationStates`, and no modification to that logic is necessary.

[ScopedLockFile](../../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/ScopedLockFile.cs) uses runtime `FileStream` sharing: an acquisition returns a lease, returns null for recognized contention, or propagates another I/O failure. The coordinator orchestrates retries using [LockFileRetryPolicy](../../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/LockFileRetryPolicy.cs) for cumulative contention accounting and bounded backoff; there is no additional native `flock` or lock-enforcement probe. These guarantees assume functioning runtime locks on a supported local filesystem, not bounded I/O latency on every filesystem.

`FileStream` lock ownership is handle-based rather than thread-affine, so `A` and `U` may be held across an `await` and the entire transaction, download included, may be asynchronous. This is why `P` does not use the thread-affine `ScopedMutex`.

## Non-safe command gate

See the algorithm's [non-safe command gate](self-update-algorithm.md#non-safe-command-gate).

The gate does not require the canonical executable name: a renamed executable, such as the RID-suffixed `dotnetup-win-x64.exe` served by the download links, runs ordinary commands using the locks in its own directory. Only `self update` requires the canonical name and fails with `DotnetupNonCanonicalExecutableName` otherwise. The gate still rejects links and reparse points in the executable path with `DotnetupUnsupportedInstallLocation`, and reports an installation directory where `A` cannot be opened or created as `PermissionDenied`. Both are user errors with guidance; `DotnetupIdentityUnavailable` remains for failures to query the installed version. See [NonSafeCommandGate](../../../../../src/Installer/dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs).

The gate executes in `CommandBase.Execute`, after the parser has determined the safety status of the command and before any command body executes.

Safety is a property of the command: `CommandBase` classifies every command as `non-safe` by default, and only `dotnetup dotnet`, the telemetry drain, and `self update` override that default. A newly added command is therefore gated unless someone deliberately exempts it.

## Forwarding

See the algorithm's [forwarding behavior](self-update-algorithm.md#forward-to-the-replaced-executable).

`N` starts `D/dotnetup.exe` with `UseShellExecute = false` and no stream redirection, so the child inherits stdin, stdout, and stderr, the working directory of `N`, and the environment of `N` plus an incremented `DOTNETUP_FORWARD_DEPTH`. `N` returns the exit code of the child via `SetExitCode`.

`D/dotnetup.exe` is resolved as the canonical, dotnetup-owned path and is not followed through an unexpected symbolic link or reparse point. Forwarding is capped at a `DOTNETUP_FORWARD_DEPTH` of 2; beyond that `N` fails rather than hopping again. `N` emits a telemetry event for the forward and does not emit a command-completion event, because the child emits one. Forwarding breaks if the replacement executable renamed or removed the command that `N` was invoked with.

## Work before the gate

See the algorithm's [pre-gate restrictions](self-update-algorithm.md#work-permitted-before-the-gate).

Except for the telemetry drainer, [Program](../../../../../src/Installer/dotnetup.Library/Program.cs) starts root telemetry before language and console setup, shows the first-run notice, and parses arguments.

Program creates a [SelfUpdateInvocation](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateInvocation.cs) when [DotnetupProcessInfo.IsDirectExecution](../../../../../src/Installer/dotnetup.Library/DotnetupProcessInfo.cs) identifies a directly launched dotnetup executable. The telemetry drainer reuses the same check: the entry assembly must be dotnetup and the captured executable must not be the `dotnet` host. Renamed executables are supported. Execution through `dotnet` and test hosts remains invocation-free and rejects self-update. This check identifies the host, not NativeAOT packaging; a managed dotnetup apphost can pass it. Self-update is supported for the published standalone executable, not for updating a managed deployment's collection of files.

Root telemetry, the first-run telemetry notice, and its sentinel are permitted before the gate. [CommandBase.Execute](../../../../../src/Installer/dotnetup.Library/CommandBase.cs) enters the gate before option telemetry or the command body. Program owns the root and catches startup failures, including encoding-scope creation and disposal failures; SelfUpdateInvocation owns coordination and leases only. A rejected command may write telemetry and the notice sentinel, but never runs its body or cleanup. Parser-only actions have no command gate. Acquired invocation leases survive command completion, root telemetry completion, and synchronous `FlushTelemetry`, and are disposed only as `Main` returns.

## Contention diagnostics

See the algorithm's [contention reporting behavior](self-update-algorithm.md#reporting-contention).

[NonSafeCommandGate](../../../../../src/Installer/dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs) and [SelfUpdateWorkflow](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) report the contention category and retry guidance using localized messages and distinct error codes.

Best-effort identification of processes using the lock file is deferred to a separate implementation improvement, not a Stage A prerequisite or a requirement for Stage B waiting/forwarding. A future implementation must remain diagnostic-only: missing or stale holder details must not affect lock acquisition, timeout, or recovery decisions. No Restart Manager interop or PID registry is needed in the current implementation.

Holder identification is deferred on all platforms. Dotnetup does not parse Linux `/proc/locks` or add a native locking layer.

## Update transaction lifetime

See the algorithm's [update transaction](self-update-algorithm.md#update-transaction).

[SelfUpdateWorkflow](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) transfers acquired leases to the invocation, which retains them through telemetry flush on success or failure.

`Execute` requires a non-null lease-owner callback. Ownership transfers when that callback returns successfully; if it throws, the workflow disposes the acquired locks. Tests that need locks released when execution ends use the test-only [SelfUpdateTestWorkflow.ExecuteAndReleaseLocks](../../../../../test/dotnetup.Tests/Utilities/SelfUpdateTestWorkflow.cs) wrapper. Production has no optional workflow-scoped lock lifetime.

Acquired locks remain held through root telemetry completion and flush, then are released as `Main` returns.

## Staging and path validation

See the algorithm's [staging and validation behavior](self-update-algorithm.md#stage-and-validate-the-replacement).

The shared [DotnetDownloader](../../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetDownloader.cs) supplies network or cached bytes and verifies the pinned SHA-512 hash before the validated file is committed to its staging path.

The workflow maps I/O and access failures for unsafe or inaccessible staging files to `InstallFailed` rather than ignoring them.

Permissions use ordinary runtime filesystem behavior, as SDK/runtime extraction does in [DotnetArchiveExtractor](../../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetArchiveExtractor.cs). On Windows, new staging files inherit their directory's permissions, and `File.Replace` preserves the installed executable's ACL. On Unix, archive extraction preserves archive modes; the raw dotnetup download has no archive mode, so `SelfUpdateWorkflow` copies the installed executable's mode with `File.GetUnixFileMode` and `File.SetUnixFileMode` rather than assigning a new fixed mode. Self-update does not add an owner whitelist, reject group-writable installs, or rewrite directory permissions.

After hash validation and permission setup, [SelfUpdateReplacement](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs) flushes the staged file with `FileStream.Flush(flushToDisk: true)` before replacement. This reduces the risk that validated bytes exist only in the system cache, but it does not turn the following replacement into an ACID or power-loss-safe transaction.

[SelfUpdatePaths](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdatePaths.cs) owns sibling naming, checked file access, and absence checks. It uses [ExecutablePathResolver](../../../../../src/Installer/dotnetup.Library/ExecutablePathResolver.cs) to resolve the containing directory on Unix, including macOS `/var` aliases, before deriving sibling paths. It preserves the final filename so executable, staged-file, and backup symlinks are still rejected. Windows retains the resolver's normalized-path behavior and reparse-point rejection. `OpenFile` checks directories, devices, and links/reparse points before opening with `FileShare.Read | FileShare.Delete`. `Exists` treats only missing paths as absent, rather than hiding access errors or occupied directories. These checks are not atomic with subsequent operations. Directory handles are not pinned, ownership and hard-link counts are not inspected, and hostile concurrent path substitution is outside this model. The update locks coordinate participating dotnetup processes, not arbitrary filesystem writers.

## Replacement operation

See the algorithm's [replacement behavior](self-update-algorithm.md#replace-the-installed-executable).

[SelfUpdateReplacement](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs) uses one `File.Replace` call:

```cs
File.Replace(
    sourceFileName: stagedPath,
    destinationFileName: installedPath,
    destinationBackupFileName: backupPath,
    ignoreMetadataErrors: false);
```

## Replacement failure recovery

See the algorithm's [replacement failure behavior](self-update-algorithm.md#handle-replacement-failure).

The workflow creates one `SelfUpdateReplacement` instance with the paths and backup name. `Replace()` validates those paths and records progress before mutation. Both immediate failure recovery and later `Rollback()` use that instance's state; there is no global transaction table or recovery state attached to the paths object. A fresh transaction object cannot adopt another transaction's backup. Recovery depends on exclusive lock ownership and a trusted, stable installation directory; it does not detect arbitrary file substitutions by writers ignoring that protocol.

[Replacement recovery tests](../../../../../test/dotnetup.Tests/SelfUpdateReplacementTests.cs) inject failures at the Windows replacement operation inside `Replace`, after validation, flushing, and transaction-state recording. They arrange the documented name states for errors 1175, 1176, and 1177 (with a backup specified), plus an ordinary unchanged-state error, before throwing into the production recovery handler. Additional cases exercise a failure after switching the canonical name and recovery blocked by a locked or missing backup. Assertions cover real file restoration or preservation and retained failure details. These are deterministic state simulations, not a claim that the tests force Windows itself to return every partial-failure code.

## Replacement verification

See the algorithm's [replacement smoke test](self-update-algorithm.md#smoke-test-the-replacement).

The built-in `--version` action runs during `ParseResult.Invoke` and returns before any `CommandBase` is constructed, so it never reaches the gate. That is load-bearing rather than incidental: `P` holds both `A` and `U` exclusively while the child runs, so a gated child would block on its own opens and every transaction would fail. If the verification path ever becomes a subcommand, that subcommand must be classified `safe`.

The verifier disables telemetry and suppresses the first-run banner in the child environment, closes stdin, drains both output streams while retaining bounded stdout/stderr, and retains cancellation and termination timeouts. A private environment request makes dotnetup startup use UTF-8 for this child, matching the parent's decoder even when normal interactive encoding preferences differ. `--version` follows ordinary startup; there is no separate hidden identity action or embedded-record scan.

## Rollback

See the algorithm's [rollback behavior](self-update-algorithm.md#roll-back).

[Workflow tests](../../../../../test/dotnetup.Tests/SelfUpdateWorkflowTests.cs) inject a reported kill error or termination timeout through the existing verification hook and exercise real file rollback and lock retention. This tests the recovery policy without launching an unkillable process; it does not test an operating-system termination failure itself.

[SelfUpdateVerifier](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateVerifier.cs) preserves a failed kill or termination wait in the exception chain, and [SelfUpdateWorkflow](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs) retains it when reporting verification or rollback failure.

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

## Deferred cleanup

See the algorithm's [deferred cleanup behavior](self-update-algorithm.md#deferred-cleanup).

[SelfUpdateCleanup](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateCleanup.cs) implements deferred backup cleanup.

Cleanup checks at most 32 directory entries matching the installed executable's backup prefix, accepts only transaction-GUID backup names (optionally ending in `.rejected`), and requires a last-write time at least seven days old. After validation and before replacement, `P` sets both the installed and staged executables' last-write times to the current UTC update time using `File.SetLastWriteTimeUtc`. The backup and any rejected candidate inherit that time through replacement, hard links, and rollback renames, so retention starts at the update rather than the executable's original modification time. This changes filesystem metadata only, not executable bytes; on Unix other hard links to the same inode also observe the new timestamp. A timestamp-write failure aborts before replacement, though a timestamp already updated is not restored.

Cleanup resolves the containing directory using the same path logic as replacement, skips failed deletions rather than retrying them, and rejects symbolic links or reparse points at the executable, lock, and backup filenames. It releases `U` as soon as cleanup finishes or is skipped, including on failure, before continuing command execution. The exception is cleanup within `P`, where `P` retains its existing locks for the transaction. Failure to delete a locked backup is not a command or transaction failure, because a process started before the transaction may still be executing that image. These rules also apply to Unix cleanup, subject to the runtime locking compatibility boundary; cleanup skips failed lock acquisition but does not independently probe lock enforcement.

##### Properties of Algorithms 1 and 2

**The canonical path may be briefly unavailable during step 2.4.** Windows measurements observed file-not-found during `File.Replace`. The replacement uses one forward call instead of deliberately splitting it into moves, but the [ReplaceFileW contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew#return-value) does not guarantee gapless concurrent opens. Measurement timing is not a platform guarantee.

Consumers launching `dotnetup` programmatically should retry transient file-not-found errors with a bounded delay before treating the installation as missing. Retry ordinary commands rejected at the Stage A gate after the update completes. Windows rollback deliberately uses two non-overwriting renames and can also leave a gap. Unix same-filesystem moves use the runtime's rename behavior on supported local filesystems, not a power-loss-durable transaction; macOS behavior remains unverified in this implementation handoff.

Abrupt termination can leave `D/dotnetup.exe.new` or `D/dotnetup.exe.old.*` behind indefinitely if dotnetup is never run again. Naming each backup with `t` prevents those stale files from corrupting or blocking a later transaction. Step 2.2 clears stale staging files during a later update; backup cleanup in steps 2.2 and 2.9 is opportunistic and runs only when its ownership and canonical-build checks permit it.

A dependent application — a long-running VS Code window, for example — may hold `D/dotnetup.exe.old.<t>` for weeks. Step 2.9 tolerates that rather than failing, and consumers decide how to surface it to the user.

After a crash, forced termination, or power loss, use the [get-dotnetup scripts](https://aka.ms/dotnet/dotnetup) to reinstall when the canonical executable is unavailable. Recovery still requires a writable, functioning filesystem; the protocol does not guarantee automatic recovery from every interruption.

## Unix replacement

See the design's [Linux replacement behavior](self-update-design.md#linux)
and [Unix locking caveats](self-update-design.md#unix-locking-caveats).

The Unix forward switch uses the same managed `File.CreateHardLink` and `File.Move` APIs available to the rest of the installer. No dotnetup-specific `libc` imports or platform-specific native metadata layouts are needed.

The macOS implementation selects the same managed hard-link/move flow in [SelfUpdateReplacement](../../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs). It uses the same `--version` queries and runtime file-sharing locks, and preserves the installed Unix mode. macOS execution, APFS behavior, code-signing, and quarantine interactions remain unverified; Linux results are not proof of macOS behavior.
