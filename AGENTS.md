# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

This is the .NET SDK repository (dotnet/sdk) — the toolchain for building, running, and publishing .NET applications. It contains the `dotnet` CLI, MSBuild tasks/targets, project system SDKs (Web, Razor, Blazor, Containers, StaticWebAssets, Wasm), template engine, workload management, and related tooling.

Uses the Arcade build infrastructure (Microsoft.DotNet.Arcade.Sdk). C# language version is Preview with nullable enabled and warnings as errors.

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

```bash
# All tests via build script
./build.sh --test

# Single test project (after full build + dogfood)
cd test/<ProjectName>.Tests
dotnet test

# Single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Single test by class
dotnet test --filter "ClassName=Microsoft.DotNet.Cli.SomeTests"
```

Test projects live in `test/` with naming conventions `<ProjectName>.Tests` (unit) and `<ProjectName>.IntegrationTests` (integration).

## Project Layout

- **`src/Cli/`** — The `dotnet` CLI entry point and command implementations
- **`src/Tasks/`** — MSBuild tasks (`Microsoft.NET.Build.Tasks`, etc.)
- **`src/StaticWebAssetsSdk/`** — Static web assets build pipeline
- **`src/WebSdk/`**, **`src/RazorSdk/`**, **`src/BlazorWasmSdk/`** — Web project SDKs
- **`src/Containers/`** — Container publishing support
- **`src/Dotnet.Watch/`** — `dotnet watch` tool
- **`src/Resolvers/`** — SDK resolver infrastructure
- **`test/`** — All test projects, test assets (`test/TestAssets/TestProjects/`)
- **`eng/`** — Build infrastructure (Arcade, pipelines, dogfood scripts)
- **`documentation/`** — Developer guide, CLI UX guidelines, snapshot testing docs

Filtered solution files for focused work: `cli.slnf`, `tasks.slnf`, `containers.slnf`, `TemplateEngine.slnf`.

## Key Conventions

- Tests use XUnit. Some tests use the Verify library for snapshot-based assertion (e.g., CLI completion tests in `test/dotnet.Tests/CompletionTests/`).
- The repo builds with a specific preview SDK version pinned in `global.json`, which `build.sh`/`build.cmd` installs automatically. Don't update this manually.
- CI runs on Azure DevOps with tests distributed via Helix. Test orchestration is in `test/UnitTests.proj`.
- Centralized package versioning via `Directory.Packages.props`.
