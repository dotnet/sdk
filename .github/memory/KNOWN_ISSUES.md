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

## Redist Requires Correct Outer-Build Ordering

**Affected area:** `src/Layout/redist`

**Description:** Some multi-targeted component projects generate SDK content in their
outer build. Referencing only an inner build can race or leave `Sdk.props`/`Sdk.targets`
out of the layout.

**Workaround:** Preserve `ReferenceOutputAssembly="false"` and
`SkipGetTargetFrameworkProperties="true"` on redist build-ordering references unless the
producing project contract changes. See the comments in
[`redist.csproj`](../../src/Layout/redist/redist.csproj).
