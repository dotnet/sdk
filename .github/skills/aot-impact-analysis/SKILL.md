---
name: aot-impact-analysis
description: >-
  Measure and report the binary-size and startup-performance impact of a NativeAOT enablement change
  to the dotnet CLI. Use when a PR touches src/Cli/dotnet-aot, the AOT entrypoint, or shared CLI code
  the AOT binary compiles, and you need before/after size deltas (sizoscope on dotnet-aot.mstat),
  startup timings through the redist dotnet.exe muxer, OpenTelemetry capture in the Aspire Dashboard,
  or a PR-ready impact summary — "what does this AOT change cost", "size diff for the AOT binary",
  "did startup regress", "profile the AOT entrypoint".
license: MIT
metadata:
  portability: repo-local
---

# AOT impact analysis (size + startup)

Produces the two artifacts an AOT enablement PR needs: a **size impact** report
(NativeAOT binary growth, sizoscope-attributed) and a **startup performance**
report (managed vs AOT, with an OpenTelemetry span breakdown in the Aspire
Dashboard). All scripts are PowerShell, self-locate the repo root, and use the
repo-local SDK (`.dotnet/dotnet.exe`); run them from anywhere in the worktree.

## When to measure what

- **Size** is the common ask and is fully turnkey — run it for every AOT PR. See
  [size-analysis.md](size-analysis.md).
- **Startup** needs a full SDK layout (`build.cmd`/`build.sh`) so the redist
  `dotnet.exe` muxer and the AOT binary exist. Run it when the change could move
  startup — first-run work, new dependencies in the AOT graph, host/bridge
  changes. See [startup-and-telemetry.md](startup-and-telemetry.md).
- **Summarize** by combining the reports into the PR description. See
  [writing-the-summary.md](writing-the-summary.md).

## The rules that always apply

1. **Diff against the fork point.** Baseline at `git merge-base HEAD origin/main`
   so the delta is exactly this change's additive cost — not unrelated drift.
2. **Release + full ILC.** Size is meaningless from a Debug or partial-trim build;
   the scripts default to `-c Release`. This mirrors the `aot-size-analysis` CI
   workflow.
3. **Measure the RIDs CI measures** — **win-x64** and **osx-arm64**. Note which
   you actually ran locally.
4. **Median and P95, not mean,** for the startup headline; mean is skewed by
   GC/JIT/disk-cache outliers.
5. **Size and startup are independent** — report both; neither implies the other.

## Run it

```powershell
# 1. Size (turnkey). Re-run with -Rid osx-arm64 on a Mac.
pwsh .github/skills/aot-impact-analysis/scripts/Measure-AotSize.ps1 -Rid win-x64

# 2. Startup (needs a full build). Start the dashboard in its own terminal first,
#    then measure managed-vs-AOT through the redist muxer.
pwsh .github/skills/aot-impact-analysis/scripts/Start-AspireDashboard.ps1
pwsh .github/skills/aot-impact-analysis/scripts/Measure-AotStartup.ps1 -Arguments '--version'
```

Each script ships in two equivalent forms — PowerShell (`*.ps1`) and bash
(`*.sh`); use whichever fits your shell. The bash equivalents are
`scripts/measure-aot-size.sh`, `scripts/start-aspire-dashboard.sh`, and
`scripts/measure-aot-startup.sh` (e.g. `scripts/measure-aot-size.sh --rid linux-x64`).
Each subskill page documents the parameters, mechanics, and how to read the
output. The size script writes `artifacts/aot-size-<rid>.md`; the startup script
writes `artifacts/aot-startup.md`.

## Reference

- [references/perf-and-otel.md](references/perf-and-otel.md) — OTLP env vars the
  CLI honors, Aspire Dashboard endpoints, the `aspire otel` command surface, and
  AOT before/after gotchas.

## Related skills

The repo's `incremental-test` skill rebuilds and redeploys changed SDK projects
into the redist layout — useful for refreshing the muxer before a startup run. A
consuming repository wires any further cross-references in its overlay.
