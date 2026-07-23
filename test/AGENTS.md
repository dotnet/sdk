# Test Agent Instructions

Guidance for changes under `test/`.

## Where things live

- **`Microsoft.NET.TestFramework.MSTest`** is the shared MSTest harness — test base
  classes, conditional-test attributes, and assertion helpers that test projects build on.
- Test projects are grouped by area.
- **`test/TestAssets/`** holds inputs, not tests.

## Conventions & gotchas

- **Derive from `SdkTest`** (in `Microsoft.NET.TestFramework.MSTest`). This gives you
  `TestAssetsManager`, `Log` (wired to MSTest's `TestContext`), and
  `BinLogArgument(...)` for binlog paths collected by Helix.
- **Use `SdkTestContext.Current` for paths** — never hardcode paths or manually discover
  locations at runtime (e.g. walking up directories):

  | Need | API |
  | --- | --- |
  | `dotnet` executable (the built SDK) | `SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath` |
  | Repo root (nullable in Helix) | `SdkTestContext.Current.ToolsetUnderTest.RepoRoot` or `SdkTestContext.GetRepoRoot()` |
  | Test execution directory | `SdkTestContext.Current.TestExecutionDirectory` |
  | Test assets root | `SdkTestContext.Current.TestAssetsDirectory` (or `TestAssetsManager` from base class) |

  **Never** use `.dotnet/dotnet` or `Process.Start("dotnet", ...)` without going through
  `DotNetHostPath` — this ensures the test exercises the built SDK, not a globally
  installed one.
- **Test asset placement.** Put test inputs (projects, packages, workloads, etc.) in
  `test/TestAssets/`. They are automatically deployed to Helix via `test/UnitTests.proj`.
- **Don't raise parallelism.** MSTest is repo-defaulted to `None` in
  `test/Directory.Build.props` because of concurrency flakiness; a few projects opt
  into `ClassLevel`. Cranking it up causes Helix over-subscription/timeouts.
- **MSTest output is live in local runs.** `test/testconfig.json` is copied beside each
  MSTest test executable as `<AssemblyName>.testconfig.json`, so console, trace, and
  `TestContext` output is both captured in the result and shown while the test runs.
  CI keeps MSTest's default result capture because live capture can block high-volume
  parallel test runs when output is redirected through Helix.
- **Skips must point to a tracking issue URL** — `[Ignore("https://github.com/dotnet/sdk/issues/N")]`.
- **Verify (approval) snapshots**: `*.verified.*` is checked in; the runner writes a
  `*.received.*` on mismatch — promote received → verified when you change output
  intentionally, and never commit `*.received.*` (only some snapshot dirs git-ignore
  them). (See `src/Cli/AGENTS.md` for the CLI-specific detail.)
- Helix work-item partitioning is driven by `test/UnitTests.proj` (per-project method
  limits/multipliers) — relevant if a project's tests are unusually slow or numerous.

## Helix deployment

CI runs SDK tests on Helix machines where the repo layout differs from a local dev box.
The conventions above (SdkTest, SdkTestContext, test asset placement) handle most cases.
This section covers the additional Helix-specific concerns.

### Deploying extra files to the test execution directory

If a test needs files at runtime beyond the test assembly and test assets (scripts,
`.props`/`.targets` files, etc.), add them to `TestExecutionDirectoryFiles` in
`test/UnitTests.proj`:

```xml
<TestExecutionDirectoryFiles Include="$(RepoRoot)path\to\file.targets">
  <DestinationFolder>relative/subfolder/</DestinationFolder>
</TestExecutionDirectoryFiles>
```

Files listed here are copied to `TestExecutionDirectoryFiles\` inside the Helix payload
and become available under `SdkTestContext.Current.TestExecutionDirectory`.

### Validate locally in a simulated Helix layout

Consider validating in a simulated Helix layout when your tests reference paths, use
test assets, or add new Helix payload (`TestExecutionDirectoryFiles`). See
[Repro Helix Failure](../documentation/project-docs/repro-helix-failure.md) for the
steps to create a local Helix test layout and run tests against it.
