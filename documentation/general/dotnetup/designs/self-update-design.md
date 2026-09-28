# Self Update

`dotnetup self update [--channel <daily|preview|stable>] [--no-progress]` updates the published NativeAOT `dotnetup`
executable in place. Stage A is implemented; waiting and transparent forwarding of
ordinary commands remain Stage B work. See [Stage A](#stage-a-success-criteria) and
the [version-query contract](self-update-verification.md) for the current behavior and limitations.

The command resolves the latest release for the selected RID from the release channel. When
`--channel` is omitted, [SelfUpdateDefaultChannel](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateDefaultChannel.cs)
derives it from the running build's SemVer prerelease label: no label selects `stable`, a
`preview` label selects `preview`, and any other label (including local development builds)
selects `daily`. This requires daily and preview builds to carry distinct prerelease labels.
`--channel` can select `daily`, `preview`, or `stable` explicitly; `stable` is accepted so the
command is ready when that channel starts publishing. An explicit `--channel` is the only
way to move between channels. See [command usage](../reference/dotnetup.md#self-update).

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

To clarify, this does not mean that updates to dotnetup itself execute concurrently. Racing update commands wait for the update lock, query the installed version again after acquiring both locks, and exit successfully when no update remains to apply.
No-op updates return exit code 0 and report the installed and available versions to
standard error. The diagnostic states whether no newer build is available or the current
same-channel version is newer than the available build.

`dotnetup` is easily and quickly re-installed via the script if an outage occurs.

#### Concurrency Trade-Offs

Another contention is whether to have mutex or inter-process (i.e. several process) aware logic; should `dotnetup` gracefully succeed when multiple updates are attempted at once or simply reject the premise and fail?

`Aspire` and `rustup` are not concurrency safe during update procedures and they also do not block such an action explicitly.

`dotnetup` should be concurrency safe. `dotnetup` should also allow multiple callers to invoke it at the same time to configure/install runtimes, so it should not run an exclusive lock on itself at all times as this would delay progress and other apps unnecessarily.

#### Comparisons

`rustup` - Rustup [downloads and launches a separate updater](https://github.com/rust-lang/rustup/blob/main/src/cli/self_update.rs), but its self-update path has no cross-process update lock, so two concurrent self-updates can interfere with the shared updater, installed executable, and proxy links. Its process handoff addresses Windows executable locking, not update serialization or crash-atomic replacement. Dotnetup needs no handoff at all, because renaming an in-use executable does not require one; see the rejected alternative below.

`Aspire CLI` - Aspire's archive self-update [extracts to a temporary directory, best-effort deletes older backups, renames the running executable to `aspire.exe.old.<unix-timestamp>`, copies the extracted executable to the canonical path, runs `aspire.exe --version`, and on any failure deletes the canonical path and moves the backup back](https://github.com/microsoft/aspire/blob/main/src/Aspire.Cli/Commands/UpdateCommand.cs). Dotnetup adopts Aspire's verification-and-rollback shape but uses `File.Replace` for the Windows switch rather than a rename followed by a copy.

Dotnetup stages on the destination volume and uses `File.Replace`, avoiding a cross-volume copy or deliberately split forward moves without promising a gapless canonical path. Dotnetup uses bounded `--version` queries to identify the installed version and to smoke-test the replacement, including comparison with the selected release. Neither operation authorizes release freshness. Aspire takes no cross-process lock, so concurrent self-updates race over the canonical path and the backups, and nothing stops another Aspire command from running across the replacement — the two problems `U` and `A` exist to solve.

`VS Code` - VS Code's installed Windows updater combines a singleton main process, an [in-process update state machine](https://github.com/microsoft/vscode/blob/main/src/vs/platform/update/electron-main/abstractUpdateService.ts), native application/setup/updating/ready mutexes, staged versioned files, and [Inno Setup](https://github.com/microsoft/vscode/blob/main/build/win32/code.iss). This serializes Windows installers and blocks application startup during the final switch. The statement does not apply uniformly to every distribution: macOS delegates to Electron's updater, while ordinary Linux packages generally delegate installation to the package manager or download page. Dotnetup does not require VS Code's UI state machine or installer framework, but it adopts the narrower invariant that only one self-update transaction may modify its executable at a time.

# Update As a Version Swap Mechanism

Explicit version selection, downgrade commands, and `self install` are future design possibilities, not registered CLI surfaces. The current parser accepts `self update`, `--channel`, and `--no-progress`. Self-update prevents equal-version replacement and downgrades within one semantic channel, while allowing explicit transitions between semantic channels. Use the existing [installation guidance](https://aka.ms/dotnet/dotnetup) for older versions.

# Release Stable VS Preview

Future signed self-update would resolve authenticated version manifests with artifact hashes and a monotonic authorization policy. This is explicitly deferred, not implemented by the current unsigned channel resolver. Signing a manifest alone does not establish freshness or authorize a downgrade; replay protection, version ordering, and explicit downgrade/revert policy need their own design. The existing signed release-manifest loader is future direction only and is unchanged by this implementation.

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
