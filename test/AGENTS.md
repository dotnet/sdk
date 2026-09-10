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
  into `ClassLevel` or `MethodLevel` after auditing their shared resources. Cranking it
  up without that audit causes Helix over-subscription/timeouts and test interference.
- **In parallelized projects, prefer `[ResourceLock]` over `[DoNotParallelize]`.** In the
  projects that do opt in (`Microsoft.NET.Build.Tests`, `dotnet-watch.Tests`,
  `Microsoft.NET.Build.Containers.UnitTests`, `Microsoft.TemplateEngine.Cli.UnitTests`),
  MSTest's parallel-safety analyzers (MSTEST0073–MSTEST0077) are active, and
  `MSTestAnalysisMode=Recommended` plus `TreatWarningsAsErrors` makes them build errors.
  Fix them in this order:
  1. **Eliminate the shared state** — pass an environment variable to the child process
     via `TestCommand.WithEnvironmentVariable(...)` instead of
     `Environment.SetEnvironmentVariable`, and give each test its own scratch directory
     with a distinct `identifier:` on `TestAssetsManager`.
  2. **Declare `[ResourceLock(...)]`** on the specific tests that must serialize, and
     restore the previous value in a `finally`. A lock serializes only against tests
     declaring the same key. `WellKnownResources` exposes `EnvironmentVariables`,
     `CurrentDirectory` and `Console` as `const string`s (not `[Flags]`), and the
     attribute takes a single resource, so stack it when a test needs several:
     ```csharp
     [ResourceLock(WellKnownResources.EnvironmentVariables)]
     [ResourceLock(WellKnownResources.CurrentDirectory)]
     ```
  3. **`[DoNotParallelize]`** only when the code under test also shares state a lock cannot
     cover (a process-wide static cache, for example) — it defers the whole class to a
     serial tail. Say in a comment why a lock is insufficient.
- **MSTest output is live.** `test/testconfig.json` is copied beside each MSTest
  test executable as `<AssemblyName>.testconfig.json`, so console, trace, and
  `TestContext` output is both captured in the result and shown while the test runs.
- **Use MSTest's built-in combinatorial data support.** Import
  `Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial` for
  `[CombinatorialData]`, `[CombinatorialValues]`, and `[CombinatorialRange]`; do not add
  the third-party `Combinatorial.MSTest` package.
- **Run all local tests through `run-tests`.** It selects and executes the appropriate
  test platform while preserving actionable output and diagnostics.
- **Map new substantive test areas for targeted testing.** Prefer adding a
  `ConditionalTestScope` when reliable trigger paths can be defined. When the area is too
  broad for practical conditional filtering, add its primary project to the fallback
  table in the `run-tests` skill.
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
