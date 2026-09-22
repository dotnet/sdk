# Dotnetup Self-Update Verification

Self-update obtains the installed version by running `dotnetup --version`.
There is no custom embedded version/RID record, build-identity digest, identity
sidecar, or record-generation/publish-validation tool. Normal assembly version
information remains: [Parser.Version](../../../../src/Installer/dotnetup.Library/Parser.cs)
reads the loaded library's informational version without launching a process.

## Version queries and startup verification

[SelfUpdateVerifier](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateVerifier.cs)
runs the canonical executable with `--version`, disables telemetry and the first-run
banner, closes redirected stdin, and drains stdout and stderr concurrently. A query
requires exit code zero and nonempty, parseable version output within 15 seconds.
Output capture and termination waits are bounded.

The private verification invocation requests UTF-8 from
[Program](../../../../src/Installer/dotnetup.Library/Program.cs), and the parent
decodes UTF-8. This does not rely on inherited console code pages or the normal
`DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING` preference. Normal interactive invocations
retain their existing encoding policy.

The built-in version action executes normal startup and command-line handling but
bypasses the command gate, acquires neither self-update lock, and does not trigger
cleanup. The parent can therefore run it while holding either or both locks.

[SelfUpdateWorkflow](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateWorkflow.cs)
uses version queries before and after acquiring the update locks to determine whether
an update is needed. After replacement it requires the reported full version to match
the selected release. This checks both startup and release consistency; an exit code
alone is not sufficient. Staged files are not executed.

## Coordination and recovery

[NonSafeCommandGate](../../../../src/Installer/dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs)
holds the shared activity lock while comparing the installed executable's reported
version with the parent's loaded version. Equality is ordinal equality of the full
version, including build metadata, not just SemVer precedence.

[SelfUpdateCleanup](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateCleanup.cs)
queries the installed version only when eligible aged backups exist. It retains the
exclusive update lock through the query and deletion, and skips cleanup if the child
fails, times out, produces invalid output, or reports a different full version.
The query child does not acquire the parent's locks or recursively clean up.

[SelfUpdateReplacement](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateReplacement.cs)
uses transaction state, its retained backup, and the existing lock contract for
recovery. Rollback neither executes the rejected candidate nor needs its version.
The parent holds both locks through replacement, verification, and recovery.
These guarantees require cooperating processes and stable paths in a trusted
installation directory; they do not identify arbitrary external file substitutions.
See the [self-update design](self-update.md) for partial failures and platform behavior.

## Scope and limitations

Distinct builds with identical full version strings cannot be distinguished by a
version query. Version equality is not executable content identity.

The current channel and checksum metadata are unsigned. HTTPS pinning, SHA-512
checking, version ordering, and reported-version comparisons detect consistency
errors but do not authenticate release freshness or prevent replay of release
metadata. Signed release metadata bound to artifact hashes, and authenticated update
authorization, are future work. The existing signed release-manifest loader is not
wired into self-update.
