# dotnet/sdk Repo Overview

This is the .NET SDK repo.
It contains the `dotnet` CLI, MSBuild tasks/targets, project system SDKs, template engine, workload management, and related tooling.
This repo uses https://github.com/dotnet/arcade for build, test, and pipeline infrastructure.

## Local Development Workflow

Use the `.dotnet/dotnet` executable for `dotnet` CLI commands.
It is automatically acquired during the build process.
Do not modify the SDK version specified in `global.json`.

### Build

- Linux/macOS: `./build.sh`
- Windows: `build.cmd`

Key flags:
- `-bl` - Generate MSBuild binary log (useful for diagnosing build issues)
- `-v <level>` - MSBuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], diag[nostic]
- Extra MSBuild properties can be passed directly with `/p:SomeProperty=value`

The newly built .NET CLI is output to `artifacts/bin/redist/Debug/dotnet/dotnet`.
It can be used to sanity check local changes in a temp directory.

### Test

Do not run the full test suite locally, it takes hours.
Instead, run only the tests relevant to your changes.

After building, run individual test projects:

```bash
# Run all tests in a specific test project
./.dotnet/dotnet test test/<ProjectName>.Tests

# Run a single test by name
./.dotnet/dotnet test test/<ProjectName>.Tests --filter "FullyQualifiedName~TestMethodName"

# Run a single test by class
./.dotnet/dotnet test test/<ProjectName>.Tests --filter "ClassName=Microsoft.DotNet.Cli.SomeTests"
```

Test projects live in `test/<ProjectName>.Tests` and `test/<ProjectName>.IntegrationTests` (with one exception: `NetAnalyzers` tests live in `src/Microsoft.CodeAnalysis.NetAnalyzers/tests`).

## Project Layout

| Feature | Location |
|---|---|
| `dotnet` CLI (entry point & commands) | `src/Cli/` |
| MSBuild tasks & targets | `src/Tasks/` |
| `dotnet watch` tool | `src/Dotnet.Watch/` |
| `dotnet format` tool | `src/Dotnet.Format/` |
| SDK container builds | `src/Containers/` |
| Static web assets pipeline | `src/StaticWebAssetsSdk/` |
| Blazor WebAssembly SDK | `src/BlazorWasmSdk/` |
| Razor SDK | `src/RazorSdk/` |
| Web publish SDK | `src/WebSdk/` |
| WebAssembly SDK (non-Blazor) | `src/WasmSdk/` |
| API compatibility tools (ApiCompat, GenAPI, PackageValidation) | `src/Compatibility/` |
| .NET code analyzers | `src/Microsoft.CodeAnalysis.NetAnalyzers/` |
| Workload management | `src/Workloads/` |
| SDK resolvers | `src/Resolvers/` |
| Template locator | `src/Microsoft.DotNet.TemplateLocator/` |
| CLI tab completions | `src/System.CommandLine.StaticCompletions/` |
| Compilers toolset | `src/Microsoft.Net.Sdk.Compilers.Toolset/` |
| MSI installer support | `src/Microsoft.Win32.Msi/` |
| SDK layout & redistribution | `src/Layout/` |
| Template content | `template_feed/` |

### Meta

| Build infrastructure (Arcade, pipelines, dogfood) | `eng/` |
| Developer documentation | `documentation/` |
| Tests & test assets | `test/` |
| Distributed test orchestration via Helix | `test/UnitTests.proj` |
