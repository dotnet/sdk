# Agent Instructions

Instructions for GitHub Copilot and other AI coding agents working with the .NET SDK repository.

## Overview

This is `dotnet/sdk`, the repository for the core of the .NET SDK. It produces the
`dotnet` CLI driver and the MSBuild tasks, targets, templates, and resolvers that are
shared between the .NET CLI and Visual Studio. The build output is a complete, runnable
`dotnet` installation laid down at `artifacts/bin/redist/<configuration>/dotnet`
(`Debug` by default).

For a high-level project description, build status, and contribution flow, see the
[README](../README.md). For the canonical build/test/debug walkthrough, see the
[Developer Guide](../documentation/project-docs/developer-guide.md).

### What the SDK does

- Provides the `dotnet` command-line driver (`dotnet build`, `restore`, `publish`, `test`,
  `run`, `watch`, etc.).
- Ships the MSBuild logic that turns a `.csproj`/`.fsproj`/`.vbproj` into a build.
- Bundles related toolsets: project/item templates, Razor/Blazor/Web/Static Web Assets
  SDKs, container publishing, file/format/watch tools, API compatibility tooling, and
  workload management.

This repo does **not** own the .NET runtime, the C#/F#/VB compilers, MSBuild itself, NuGet,
or the Visual Studio project system — those are separate repositories that flow in as
dependencies. The full product (`dotnet/dotnet` VMR) composes this repo with those.

### Architecture and major components

The CLI is a generic driver. A `dotnet <command>` is resolved in one of two ways: in-box
commands implemented in `dotnet.dll` (e.g. `build`, `restore`, `test`), or executables named
`dotnet-<command>` found on the `PATH`, including installed global tools (`dotnet foo` runs
`dotnet-foo`). The "build" commands (`build`/`restore`/`publish`/`pack`) are thin CLI
wrappers that invoke MSBuild against the SDK targets.

Major source areas under `src/`:

| Area | Purpose |
| --- | --- |
| `Cli/` | The `dotnet` driver, command implementations, and CLI utilities. `Cli/dotnet` is the entry point. |
| `Tasks/` | MSBuild tasks & targets — the heart of the build logic. `Microsoft.NET.Build.Tasks` is the primary SDK tasks assembly. |
| `Resolvers/` | MSBuild SDK resolvers (how `<Project Sdk="...">` and workloads are located). |
| `RazorSdk/`, `BlazorWasmSdk/`, `WasmSdk/`, `WebSdk/`, `StaticWebAssetsSdk/` | Web/Razor/Blazor/Static Web Assets build SDKs. |
| `Containers/` | `dotnet publish` container image support. |
| `Dotnet.Watch/`, `Dotnet.Format/` | `dotnet watch` and `dotnet format` tools. |
| `Compatibility/` | ApiCompat / GenAPI / package validation tooling. |
| `TemplateEngine/` | CLI glue for the templating engine (engine itself is in `dotnet/templating`). |
| `Workloads/`, `Microsoft.DotNet.TemplateLocator/` | Workload manifests/installation, and locating workload-provided template packs. |
| `Layout/` | Composes the final redist `dotnet` layout. |

### Key files and directories

- `build.cmd` / `build.sh` — primary top-level build entry point (Arcade-based).
  `test.cmd` / `test.sh` and `restore.cmd` / `restore.sh` are thin wrappers that forward
  to it.
- `sdk.slnx` — the full solution. Filtered solutions exist for focused work:
  `cli.slnf`, `tasks.slnf`, `containers.slnf`, `TemplateEngine.slnf`, `source-build.slnf`.
- `global.json` — pins the bootstrap SDK and Arcade versions used to build the repo.
- `Directory.Build.props` / `Directory.Build.targets` / `Directory.Packages.props` — repo-wide
  MSBuild settings and central package version management.
- `eng/` — Arcade build infrastructure, versioning (`eng/Versions.props`), and the
  `dogfood` scripts.
- `artifacts/bin/redist/<configuration>/dotnet` (`Debug` by default) — the built SDK; `.dotnet/dotnet` is the bootstrap SDK.
- `documentation/` — project docs, including the developer guide and area-specific guides.
- `template_feed/` — the in-box project/item templates.
- `test/` — test projects and `test/TestAssets/TestProjects` test inputs.

### Build, test, and dogfood

The top-level `build.cmd`/`build.sh` (and `test.cmd`/`test.sh`) wrap Arcade. The build
script restores and builds the full redist SDK; pass `-test` to also run tests. Common
switches (run `build.cmd -help` for the full list):

| Switch | Effect |
| --- | --- |
| `-c` / `-configuration <Debug\|Release>` | Build configuration (default `Debug`). |
| `-test` (`-t`) | Run tests after building. |
| `-pack` | Build installers/packages (otherwise skipped for speed). |

Arguments not directly supported by the script are passed through to MSBuild (e.g.
`/t:UpdateXlf`, `/bl` for a binlog, `/p:Property=Value`).

Canonical scenarios:

- Build the full redist SDK: `build.cmd` (Windows) or `./build.sh` (Linux/macOS).
  - The script first restores a repo-local .NET SDK to `.dotnet/dotnet`, then builds the SDK.
    Invoke that bootstrap SDK directly as `./.dotnet/dotnet <args>` when you need a `dotnet`
    that resolves against this repo.
  - The built SDK is output to `artifacts/bin/redist/<configuration>/dotnet` (`Debug` by default).
  - The first build is slow; subsequent builds are incremental.
- Run tests: prefer targeted runs — a single test project or test (see the
  [Testing](#testing) section) and the `incremental-test` skill. `build.cmd -test` /
  `./build.sh --test` runs the **entire** suite, which is very large and takes a long time;
  avoid running the full suite for routine local or agent work.
- Release build: `build.cmd -c Release`.
- Run a single test project after a full build:
  `./.dotnet/dotnet test test/dotnet.Tests/dotnet.Tests.csproj --filter "FullyQualifiedName~TestName"`.
  See the [Testing](#testing) section for assembly filtering and more examples.
- Validate changes locally using the SDK you built at
  `artifacts/bin/redist/<configuration>/dotnet` (`Debug` by default).
- For fast inner-loop runs of `dotnet.Tests` without a full rebuild, use the
  `incremental-test` skill.

## Coding Style

- Code should match the style of the file it's in.
- Changes should be minimal to resolve a problem in a clean way.
- User-visible changes to behavior should be considered carefully before committing. They should always be flagged.
- Only edit the files that are necessary to address the specific issue. Do not run `dotnet format` or make formatting changes to additional files.
- Prefer using file-based namespaces for new code.
- Do not allow unused `using` directives to be committed.
- Use `#if NET` blocks for .NET Core specific code, and `#if NETFRAMEWORK` for .NET Framework specific code.

## Testing

- Large changes should always include test changes.
- When creating new test projects in test/TestAssets/TestProjects, always use `$(CurrentTargetFramework)` for the `<TargetFramework>` property instead of hard-coding a specific version like `net8.0`.
- The Skip parameter of the Fact attribute to point to the specific issue link.
- To run tests in this repo:
  - First build (`build.cmd` / `./build.sh`), then load the build environment so a plain `dotnet`
    resolves correctly: dot-source `artifacts\sdk-build-env.ps1` (PowerShell), run
    `artifacts\sdk-build-env.bat` (cmd), or `source artifacts/sdk-build-env.sh` (bash).
  - For MSTest-style projects: `dotnet test path/to/project.csproj --filter "FullyQualifiedName~TestName"`
  - For XUnit test assemblies: `dotnet exec artifacts/bin/redist/Debug/TestAssembly.dll -method "*TestMethodName*"`
  - Examples:
    - `dotnet test test/dotnet.Tests/dotnet.Tests.csproj --filter "Name~ItShowsTheAppropriateMessageToTheUser"`
    - `dotnet exec artifacts/bin/redist/Debug/dotnet.Tests.dll -method "*ItShowsTheAppropriateMessageToTheUser*"`
- For incremental test runs of `dotnet.Tests` (avoids slow full `build.cmd`), use the `incremental-test` skill.
- To test CLI command changes:
  - Build the redist SDK: `./build.sh` from repo root
  - Create a dogfood environment: `source eng/dogfood.sh`
  - Test commands in the dogfood shell (e.g., `dnx --help`, `dotnet tool install --help`)
  - The dogfood script sets up PATH and environment to use the newly built SDK

## Investigating PR validation failures

1. Read the PR and its comments/reviews. Check for references to other PRs or issues where the problem might have already been solved.
2. Use the `ci-analysis` skill (if available) to diagnose build failures.

## Localization

- Avoid modifying .xlf files and instead prompt the user to update them using the `/t:UpdateXlf` target on MSBuild. Correctly automatically modified .xlf files have elements with state `needs-review-translation` or `new`.
- Consider localizing strings in .resx files when possible.
- When adding a new NETSDK error message in `src/Tasks/Common/Resources/Strings.resx`, assign the next available NETSDK code, append the entry at the end of the file, and update the trailing "latest message added" guard comment.

## Documentation

- Do not manually edit files under documentation/manpages/sdk as these are generated based on documentation and should not be manually modified.

## External Dependencies

- Changes that require modifications to the dotnet/templating repository (Microsoft.TemplateEngine packages) should be made directly in that repository, not worked around in this repo.
- The dotnet/templating repository owns the TemplateEngine.Edge, TemplateEngine.Abstractions, and related packages.
- If a change requires updates to template engine behavior or formatting (e.g., DisplayName properties), file an issue in dotnet/templating and make the changes there rather than adding workarounds in this SDK repository.
