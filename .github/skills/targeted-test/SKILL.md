---
name: targeted-test
description: >-
  Select and run the smallest relevant .NET SDK tests with live output and retained
  TRX/binlog diagnostics. REQUIRED: invoke before answering any dotnet/sdk request
  containing an explicit deliverable to select, run, or rerun narrow local tests. Use
  the owning workflow for the change, then this skill for test selection and execution.
  Also use to test a completed change, choose tests from changed files, run targeted,
  focused, or smallest relevant tests, or run one project, class, or method, including
  plans and dry runs. NEVER invoke when the user says not to run tests yet. DO NOT USE
  for full suites, end-to-end validation, repo investigations, or review.
license: MIT
---

# Targeted tests

Run the smallest project and filter that cover the changed behavior. This workflow
streams detailed test output and writes a TRX plus an MSBuild binlog under
`artifacts/log/targeted-tests/`.

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

## Run

Start with a build-producing run. The runner builds the selected project by default,
which keeps the test assembly current and produces the binlog needed to diagnose build
failures. Add `--no-build` only after this session built that exact project after the
latest test-code change and confirmed its expected output exists. Do not infer readiness
from another project or an older artifact; when uncertain, omit `--no-build`.

From the repository root on Windows:

```powershell
.\.dotnet\dotnet.exe .github\skills\targeted-test\scripts\RunTargetedTests.cs -- `
  --project test\Microsoft.NET.Build.Tests\Microsoft.NET.Build.Tests.csproj `
  --filter "FullyQualifiedName~GivenThatWeWantToBuildALibrary"
```

On macOS/Linux:

```bash
./.dotnet/dotnet .github/skills/targeted-test/scripts/RunTargetedTests.cs -- \
  --project test/Microsoft.NET.Build.Tests/Microsoft.NET.Build.Tests.csproj \
  --filter "FullyQualifiedName~GivenThatWeWantToBuildALibrary"
```

Omit `--filter` to run the whole project. Use `--configuration Release` when validating
a Release redist layout. A `--no-build` run does not produce a new build binlog.

The runner evaluates the project to select its test platform. It executes MSTest.Sdk
projects through their built Microsoft.Testing.Platform executable and keeps
`dotnet test` for other projects. It prints the exact command before execution. On
failure it also prints failed test names when a TRX is available, the retained
TRX/binlog paths, and the rerun command. Do not replace it with a command that
suppresses console output or discards those artifacts.
