---
coverage: Persistent repository gotchas, compatibility traps, and documented workarounds
---

# Known Issues

These are durable development gotchas, not a list of open product bugs. Follow linked
area guidance when it is more specific.

## Product Tests Can Exercise a Stale SDK

**Affected area:** [`test/`](../../test/), `artifacts/bin/redist/`

**Description:** Most integration tests exercise the SDK in the redist layout. Building
only a test project can leave production assemblies or targets in that layout stale.

**Workaround:** Use the [`run-tests` skill](../skills/run-tests/SKILL.md), which owns the
product-layout freshness decision and invokes area-specific deployment workflows where
applicable.

## A Reused App Tree Can Serve Stale WebAssembly Runtime Assets

**Affected area:** Manual validation of `dotnet watch` / Blazor WebAssembly against a
locally built SDK

**Description:** Building the same sample app with successive
`artifacts/bin/redist/<configuration>/dotnet` builds without cleaning can leave `obj` and
`bin` holding runtime assets from an earlier SDK. Static Web Assets incrementality keeps
the stale copies, and the app can then boot with a runtime whose
`System.Reflection.Metadata.MetadataUpdater.IsSupported` is `false`, so Hot Reload silently
applies nothing even though the sources, the SDK, and the generated modules are correct.

**Workaround:** Delete the sample's `obj` and `bin` after rebuilding the redist SDK.
Confirm the app really supports updates by reading
`getDotnetRuntime(0).getConfig()` and calling
`window.Blazor._internal.getApplyUpdateCapabilities()`: an empty capability string means no
agent was created.

## Windows Builds Can Exceed Legacy Path Limits

**Affected area:** Full builds and generated intermediates on Windows

**Description:** Deep generated paths can cause misleading missing-resource failures.

**Workaround:** Enable Windows long paths and run `git config core.longpaths true`, then
retry. See the [Developer Guide](../../documentation/project-docs/developer-guide.md#building).

## Helix Uses a Different Filesystem Layout

**Affected area:** Tests that assume repository-relative paths or undeclared runtime files

**Description:** Helix publishes tests as tools and separates the SDK, work-item payload,
and correlation payload. A test can pass locally but fail when it depends on the checkout
layout, a machine-installed dependency, or an environment variable not propagated to the
Helix runner.

**Workaround:** Use `SdkTestContext` paths, deploy extra runtime files through
`TestExecutionDirectoryFiles`, and reproduce with the local Helix layout described in
[`repro-helix-failure.md`](../../documentation/project-docs/repro-helix-failure.md).

## Test Parallelism Is Disabled by Default

**Affected area:** MSTest projects under [`test/`](../../test/)

**Description:** Shared environment variables, current directory, console state, static
caches, and scratch paths have caused broad concurrency flakiness. Repository defaults
therefore set `MSTestParallelizeScope=None`.

**Workaround:** Do not raise parallelism globally. In projects that deliberately opt in,
eliminate shared state first, then use a narrow `[ResourceLock]`; reserve
`[DoNotParallelize]` for state a resource lock cannot cover. See
[`test/AGENTS.md`](../../test/AGENTS.md#conventions--gotchas).

## Resolver Code Runs in Two Hosts and Links Shared Sources

**Affected area:** `src/Resolvers`

**Description:** Resolver projects run in .NET MSBuild and Visual Studio/.NET Framework.
Several components are compiled from linked sources rather than referenced assemblies,
and dependencies are constrained by MSBuild binding redirects.

**Workaround:** Exercise both target-framework paths, keep hostfxr interop compatible,
and coordinate dependency changes with MSBuild. See
[`src/Resolvers/AGENTS.md`](../../src/Resolvers/AGENTS.md#conventions--gotchas).

## Generated Files Are Easy to Edit Accidentally

**Affected area:** `.xlf`, `.github/workflows/*.lock.yml`, generated man pages, Verify snapshots

**Description:** Manual edits drift from their source or are overwritten. Verify also
creates `*.received.*` files on mismatch that must not be committed.

**Workaround:** Edit `.resx` and regenerate XLF; change manpage content in `dotnet/docs`;
regenerate workflow locks through their owning workflow; inspect received snapshots and
promote only intentional output to `*.verified.*`. See the
[root generated-file guardrails](../../AGENTS.md#do-not-hand-edit-generated-files)
and [`snapshot-based-testing.md`](../../documentation/project-docs/snapshot-based-testing.md).

## `gh aw compile` Must Run on Linux or macOS, Never Windows

**Affected area:** gh-aw workflows with a custom `safe-outputs.jobs` entry alongside
`safe-outputs.threat-detection` (e.g. `.github/workflows/stale-reference-interpret.md`)

**Description:** gh-aw's secret-redaction validation for the generated token-usage
artifact path uses Go's `filepath.Join`, which emits the host OS's path separator.
Compiling on Windows produces backslash paths (e.g.
`\tmp\gh-aw\sandbox\firewall\logs\api-proxy-logs\token-usage.jsonl`) that do not match
the forward-slash runtime paths the redaction check expects, so `gh aw compile` fails
with "artifact paths not covered by secret redaction" whenever threat detection and a
custom `safe-outputs.jobs` entry are combined. Compiling the identical workflow source
on Linux (forward-slash `filepath.Join`) succeeds. This is the root cause behind
[github/gh-aw#62458](https://github.com/github/gh-aw/issues/62458); an unmerged
upstream fix ([github/gh-aw#62484](https://github.com/github/gh-aw/pull/62484))
replaces `filepath.Join` with the OS-independent `path.Join`.

A prior workaround in this repository (moving the recorder out of
`safe-outputs.jobs` into a plain top-level job) avoided the compile failure, but was
later found to be unnecessary and unrelated to a separate first-request HTTP 400 issue
that was happening at the same time (see below). Several Copilot CLI version pins
(1.0.85, 1.0.83, 1.0.80) were also tried and discarded chasing that unrelated symptom;
none were the real fix for either issue. Both the safe-outputs restructuring and the
version pins were reverted once each issue's actual, independent root cause was
confirmed.

**Workaround:** Always run `gh aw compile` for this workflow on Linux or macOS (e.g.
WSL on a Windows dev machine), never native Windows PowerShell/cmd. Do not remove the
real custom `safe-outputs.jobs` design or add a Copilot CLI version pin to work around
this; both are unnecessary once compilation happens on a POSIX host. Revisit once a
released gh-aw version includes the `path.Join` fix.

## Pinning an Unsupported Copilot Model Causes an Immediate HTTP 400

**Affected area:** gh-aw workflows using `engine.model` with the `copilot` engine
(e.g. `.github/workflows/stale-reference-interpret.md`)

**Description:** Pinning `engine.model` to `gpt-5.6-luna` caused every agent request
to fail immediately (~1.2s, 0 tokens consumed) with
`failureClass=http_400_response_error` / `isHTTP400ResponseError=true`, and the
harness did not retry ("persistent request validation/state failure"). This was
initially conflated with the unrelated Windows-compile issue above and with the
discarded `safe-outputs.jobs` restructuring, because all three were being debugged
around the same time. Removing the `model:` pin (falling back to the CLI's `'auto'`
default via `COPILOT_MODEL: ${{ vars.GH_AW_MODEL_AGENT_COPILOT ||
vars.GH_AW_DEFAULT_MODEL_COPILOT || 'auto' }}`) resolved the failure in a live
end-to-end test run.

**Workaround:** Do not pin `engine.model` for the `copilot` engine to a model ID that
has not been confirmed to work end-to-end for the target repository/entitlement. Prefer
leaving it unset (CLI default) unless a specific model has been verified.

## Redist Requires Correct Outer-Build Ordering

**Affected area:** `src/Layout/redist`

**Description:** Some multi-targeted component projects generate SDK content in their
outer build. Referencing only an inner build can race or leave `Sdk.props`/`Sdk.targets`
out of the layout.

**Workaround:** Preserve `ReferenceOutputAssembly="false"` and
`SkipGetTargetFrameworkProperties="true"` on redist build-ordering references unless the
producing project contract changes. See the comments in
[`redist.csproj`](../../src/Layout/redist/redist.csproj).
