---
name: run-tests
description: >-
  Select and run .NET SDK tests through the repository's diagnostic-preserving test
  entry point. REQUIRED whenever an agent will select, run, or rerun local dotnet/sdk
  tests, including a project, class, method, targeted test, focused test, smallest
  relevant test, or completed-change validation. NEVER invoke when the user says not to
  run tests yet.
license: MIT
---

# Run tests

Use this workflow for every local SDK test execution. Select the smallest project and
filter that cover the changed behavior, then invoke the centralized runner. It streams
detailed output and writes a TRX plus an MSBuild binlog under
`artifacts/log/test-runs/`.

## Choose the execution scope

- For one test, class, or project, invoke `scripts/RunTests.cs` as described below.
- For an area represented in `test/ConditionalTests.props`, expand the scope and invoke
  `scripts/RunTests.cs` once per concrete project.
- When the user explicitly requests the complete repository test suite, explain that it
  is very large and then run `build.cmd -test` on Windows or `./build.sh --test` on
  macOS/Linux. Do not use the complete suite for routine validation.

## Select configured test scopes first

Read `test/ConditionalTests.props` before choosing tests. Its `TriggerPaths` and
`TestProjects` metadata are the repository's source of truth for configured areas; do
not duplicate those mappings in this skill.

For each scope whose `TriggerPaths` match the changed files, expand its project globs
with the same evaluator used by PR validation:

```powershell
.\.dotnet\dotnet.exe run scripts\EvaluateConditionalTestScopes.cs -- `
  --repo-root . `
  --list-test-projects ApiCompat
```

The command writes one `Targeted test project:` line per concrete project. Run each
project separately with the runner below. If a changed file matches
`GlobalTriggerPaths`, the conditional system cannot safely narrow the suite; use broader
validation instead.

## Fall back for common unscoped areas

When no `ConditionalTestScope` covers the changed paths, start with the project that
owns the changed behavior. If a change crosses areas, run each relevant project
separately so a failure identifies the affected area. This table is intentionally
limited to common areas rather than being an exhaustive test-project catalog.

| Change area | Primary test project |
| --- | --- |
| Managed CLI commands, parsing, help, and workloads | `test/dotnet.Tests/dotnet.Tests.csproj` |
| CLI utilities | `test/Microsoft.DotNet.Cli.Utils.Tests/Microsoft.DotNet.Cli.Utils.Tests.csproj` |
| SDK build targets and NETSDK diagnostics | `test/Microsoft.NET.Build.Tests/Microsoft.NET.Build.Tests.csproj` |
| Build task unit behavior | `test/Microsoft.NET.Build.Tasks.Tests/Microsoft.NET.Build.Tasks.Tests.csproj` |
| Publish | `test/Microsoft.NET.Publish.Tests/Microsoft.NET.Publish.Tests.csproj` |
| Pack | `test/Microsoft.NET.Pack.Tests/Microsoft.NET.Pack.Tests.csproj` |
| Restore | `test/Microsoft.NET.Restore.Tests/Microsoft.NET.Restore.Tests.csproj` |
| MSBuild SDK resolution | `test/Microsoft.DotNet.MSBuildSdkResolver.Tests/Microsoft.DotNet.MSBuildSdkResolver.Tests.csproj` |
| Containers | `test/Microsoft.NET.Build.Containers.UnitTests/Microsoft.NET.Build.Containers.UnitTests.csproj` |
| Containers with registry/runtime behavior | `test/Microsoft.NET.Build.Containers.IntegrationTests/Microsoft.NET.Build.Containers.IntegrationTests.csproj` |
| `dotnet watch` | `test/dotnet-watch.Tests/dotnet-watch.Tests.csproj` |
| Static Web Assets | `test/Microsoft.NET.Sdk.StaticWebAssets.Tests/Microsoft.NET.Sdk.StaticWebAssets.Tests.csproj` |
| Web SDK | `test/Microsoft.NET.Sdk.Web.Tests/Microsoft.NET.Sdk.Web.Tests.csproj` |
| Razor SDK | `test/Microsoft.NET.Sdk.Razor.Tests/Microsoft.NET.Sdk.Razor.Tests.csproj` |
| Blazor WebAssembly SDK | `test/Microsoft.NET.Sdk.BlazorWebAssembly.Tests/Microsoft.NET.Sdk.BlazorWebAssembly.Tests.csproj` |

Keep this fallback table limited to areas not represented in
`test/ConditionalTests.props`. Whenever that file changes, reconcile this table: remove
entries for areas that are now configured, and update entries when test-project ownership
changes. Do not duplicate configured mappings here; add or change them in the props file
so local agent selection and PR filtering stay aligned.

Also revisit this table when adding a test project for a substantive new area. Prefer a
`ConditionalTestScope` when reliable trigger paths can be defined. When the area is too
broad for practical conditional filtering, add its primary test project to this table.

## Make the product output current

Tests exercise the SDK under `artifacts/bin/redist/<Configuration>/dotnet`, not only
assemblies built beside the test project.

1. If the redist layout does not exist, run `build.cmd` on Windows or `./build.sh` on
   macOS/Linux.
2. If production code changed, ensure the redist layout contains that change before
   trusting the result:
   - For managed CLI changes covered by `dotnet.Tests`, use **incremental-test** to build
     and deploy the changed assemblies without a full rebuild.
   - For Static Web Assets implementation changes, use
     **validate-static-web-asset-change**.
   - Otherwise rebuild the repository. Building only a test project can leave the SDK
     under test stale even when the test assembly itself is current.
3. If only test code changed, the runner can build the selected test project directly.
4. Tests that do not exercise the assembled SDK, such as NetAnalyzers unit tests, may
   pass `--skip-redist-check`. Use it only when the owning workflow confirms that the
   project does not consume `artifacts/bin/redist`; it does not make stale product bits
   safe to test.

## Run

The runner always performs one incremental build of the selected test project before
execution. This keeps the test assembly current and guarantees that the run reports an
MSBuild binlog path. Pass `--repeat N` to execute the selected tests repeatedly after
that single build. Do not replace the runner with a hand-written `dotnet test`,
`dotnet exec`, or test-application command: those commands can execute zero tests under
the wrong platform or omit the diagnostics needed after a failure.

From the repository root on Windows:

```powershell
.\.dotnet\dotnet.exe scripts\RunTests.cs -- `
  --project test\Microsoft.NET.Build.Tests\Microsoft.NET.Build.Tests.csproj `
  --filter "FullyQualifiedName~GivenThatWeWantToBuildALibrary"
```

On macOS/Linux:

```bash
./.dotnet/dotnet scripts/RunTests.cs -- \
  --project test/Microsoft.NET.Build.Tests/Microsoft.NET.Build.Tests.csproj \
  --filter "FullyQualifiedName~GivenThatWeWantToBuildALibrary"
```

Omit `--filter` to run the whole project. Multi-targeted projects select
`SdkTargetFramework` by default; pass `--framework <TFM>` to choose another supported
target. Use `--configuration Release` when validating a Release redist layout. Unfiltered
project runs can be expensive; do not treat one as the complete repository suite. For
flake checks or performance samples, pass `--repeat N` instead of invoking the runner N
times so the project is built only once.

All supported SDK test projects use MSTest.Sdk/Microsoft.Testing.Platform. The runner
evaluates the selected framework, builds it, then invokes its test application directly.
It only requests a TRX when the project enables the TRX report extension. It prints the
run directory and exact command before execution. At completion it prints the retained
TRX/binlog paths; on failure it also prints failed test names when a TRX is available and
the rerun command.
