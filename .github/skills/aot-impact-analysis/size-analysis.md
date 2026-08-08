# Size analysis

Detail for the [aot-impact-analysis](SKILL.md) skill. Covers the before/after
NativeAOT binary-size measurement produced by
[scripts/Measure-AotSize.ps1](scripts/Measure-AotSize.ps1). For the env-var
contract and dashboard mechanics used by the startup half, see
[startup-and-telemetry.md](startup-and-telemetry.md).

## What it measures

The size signal for an AOT enablement is the native `dotnet-aot` shared library
(`dotnet-aot.dll`) and the per-symbol `.mstat` the ILC compiler emits. The skill
diffs this branch against a baseline ref so the delta is exactly the change's
additive cost. This mirrors the repo's `aot-size-analysis` CI workflow, which
diffs the same `.mstat` artifacts with `sizoscope-cli`.

## Run it

```powershell
pwsh .github/skills/aot-impact-analysis/scripts/Measure-AotSize.ps1 -Rid win-x64
```

Bash equivalent (same parameters as `--kebab-case` flags):

```bash
.github/skills/aot-impact-analysis/scripts/measure-aot-size.sh --rid linux-x64
```

Useful parameters:

- `-Rid` / `--rid` — runtime identifier to publish. CI publishes **win-x64** and
  **osx-arm64**; re-run with `osx-arm64` on a Mac for that leg.
- `-Configuration` / `--configuration` — defaults to `Release` (matches CI's
  `buildConfiguration`).
- `-BaseRef` / `--base-ref` — baseline ref; defaults to `git merge-base HEAD
  origin/main` (the fork point), which yields a clean additive diff.
- `-SkipBaseline` / `--skip-baseline` — reuse an already-built baseline worktree
  (faster re-runs).
- `-OutputPath` / `--output-path` — where to write the Markdown report.

## How it works

1. Publishes `src/Cli/dotnet-aot` (Release, full ILC) for the current worktree.
2. Adds a **detached git worktree** at the baseline ref beside the repo and
   publishes the same project there, using the current worktree's repo-local SDK
   (`.dotnet/dotnet.exe`). The baseline restore pulls the baseline package set —
   expect a cold restore on first run.
3. Locates `native/dotnet-aot.mstat` and `native/dotnet-aot.dll` under
   `artifacts/obj` and `artifacts/bin` on both sides.
4. Runs `sizoscope-cli <base.mstat> <pr.mstat> --output <diff>`; the first line is
   `Total accounted size difference: <N> kB`.
5. Writes the report (raw `dotnet-aot.dll` delta in KB + %, the accounted total,
   and the full New/Grown + Removed/Shrunk diff) and removes the temp worktree.

`sizoscope-cli` is installed on demand (`dotnet tool install --global
sizoscope-cli`, from the feeds in the repo's `nuget.config`). If it is not found
after install, open a new shell so `~/.dotnet/tools` is on `PATH`.

## Reading the diff

- The **raw `dotnet-aot.dll` delta** is the headline number a reviewer cares
  about. The **accounted total** from `sizoscope-cli` attributes that delta to
  assemblies/types.
- Equal `+`/`-` `*.resources` rows are **localized satellite assemblies** being
  re-attributed across the two builds; they net to ~zero. This is why the
  accounted total is often below the raw file delta — don't read those rows as
  real growth.
- Map the top real contributors back to the change's features (e.g. a crypto
  assembly to in-process cert generation, registry assemblies to workload
  detection). A contributor that maps to nothing in the diff is worth
  investigating — it may signal an unintended dependency pulled into the AOT
  graph.

Size and startup are independent: a size change does not imply a startup change.
Report both — see [startup-and-telemetry.md](startup-and-telemetry.md).
