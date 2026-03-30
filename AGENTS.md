# .NET SDK Repo Instructions

## Overview

This is the .NET SDK repository (dotnet/sdk). It contains the `dotnet` CLI, MSBuild tasks/targets, project system SDKs (Web, Razor, Blazor, Containers, StaticWebAssets, Wasm), template engine, workload management, and related tooling. Uses the Arcade build infrastructure.

## Local Development Workflow

### Building

```bash
./build.sh                    # Linux/macOS (must use bash, not zsh)
build.cmd                     # Windows
```

By default (without `-pack`), the build skips crossgen and installers (`SkipUsingCrossgen=true`, `SkipBuildingInstallers=true`) to speed up inner-loop iteration. Build output goes to `artifacts/bin/redist/<Configuration>/dotnet/`.

Key flags:
- `-c Release` — Release configuration (default: Debug)
- `-pack` — Full build including crossgen and NuGet packages
- `-bl` — Generate MSBuild binary log (useful for diagnosing build issues)
- `-v <level>` — MSBuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], diag[nostic]
- Extra MSBuild properties can be passed directly, e.g. `./build.sh /p:SomeProperty=value`

### Using the Built SDK (Dogfooding)

After building, set up your shell to use the locally-built SDK:

```bash
# macOS/Linux (bash only)
source ./eng/dogfood.sh

# Windows
eng\dogfood.cmd
# or: artifacts\sdk-build-env.bat
```

This puts the built SDK on your PATH so `dotnet build`, `dotnet test`, etc. use your local changes.

### Running Tests

Do **not** run the full test suite locally — it takes hours and is handled by CI (Azure DevOps + Helix). Instead, run only the tests relevant to your changes.

After building, run individual test projects:

```bash
# Run all tests in a specific test project
cd test/<ProjectName>.Tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run a single test by class
dotnet test --filter "ClassName=Microsoft.DotNet.Cli.SomeTests"
```

Test projects live in `test/` with naming conventions `<ProjectName>.Tests` (unit) and `<ProjectName>.IntegrationTests` (integration).

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
| Tests & test assets | `test/` |
| Template content | `template_feed/` |
| Build infrastructure (Arcade, pipelines, dogfood) | `eng/` |
| Developer documentation | `documentation/` |

## Key Conventions

- The repo builds with a specific preview SDK version pinned in `global.json`, which `build.sh`/`build.cmd` install automatically. Don't update this manually.
- CI runs on Azure DevOps with distributed testing via Helix. Test orchestration is in `test/UnitTests.proj`.

