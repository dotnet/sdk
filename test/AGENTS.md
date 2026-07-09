# Test Agent Instructions

Guidance for changes under `test/`.

## Where things live

- **`Microsoft.NET.TestFramework.MSTest`** is the shared MSTest harness — test base
  classes, conditional-test attributes, and assertion helpers that test projects build on.
- Test projects are grouped by area.
- **`test/TestAssets/`** holds inputs, not tests.

## Conventions & gotchas

- **Don't raise parallelism.** MSTest is repo-defaulted to `None` in
  `test/Directory.Build.props` because of concurrency flakiness; a few projects opt
  into `ClassLevel`. Cranking it up causes Helix over-subscription/timeouts.
- **Skips must point to a tracking issue URL** — `[Ignore("https://github.com/dotnet/sdk/issues/N")]`.
- **Verify (approval) snapshots**: `*.verified.*` is checked in; the runner writes a
  git-ignored `*.received.*` on mismatch — promote received → verified when you change
  output intentionally, and never commit `*.received.*`. (See `src/Cli/AGENTS.md` for
  the CLI-specific detail.)
- Helix work-item partitioning is driven by `test/UnitTests.proj` (per-project method
  limits/multipliers) — relevant if a project's tests are unusually slow or numerous.
