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

### Build and test

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

## Guardrails

These are hard boundaries for agents working in this repo. Treat them as "must not" rules.

### Do not hand-edit generated files

Some files are produced by tooling and are overwritten the next time the build or a
generation step runs. Editing them by hand causes drift and merge conflicts. Never
manually edit:

- **`.xlf` localization files.** Change the source `.resx` strings instead, then
  regenerate the `.xlf` with the `/t:UpdateXlf` MSBuild target. Correctly regenerated
  entries have a state of `needs-review-translation` or `new`. See
  [Localization](../documentation/project-docs/Localization.md) for the full workflow.
- **Generated man pages** under `documentation/manpages/sdk`. These are generated from
  documentation; change the upstream documentation in https://github.com/dotnet/docs instead.
- **Generated workflow lock files** (`.github/workflows/*.lock.yml`).
- More broadly, any file marked `linguist-generated=true` in `.gitattributes`.

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
- To run tests in this repo (after a full build, invoke the repo-local bootstrap SDK directly):
  - For MSTest-style projects: `./.dotnet/dotnet test path/to/project.csproj --filter "FullyQualifiedName~TestName"`
  - To run a built test assembly directly: `./.dotnet/dotnet exec artifacts/bin/redist/Debug/TestAssembly.dll --filter "TestMethodName"`
  - Examples:
    - `./.dotnet/dotnet test test/dotnet.Tests/dotnet.Tests.csproj --filter "Name~ItShowsTheAppropriateMessageToTheUser"`
    - `./.dotnet/dotnet exec artifacts/bin/redist/Debug/dotnet.Tests.dll --filter "ItShowsTheAppropriateMessageToTheUser"`
- For incremental test runs of `dotnet.Tests` (avoids slow full `build.cmd`), use the `incremental-test` skill.

## Investigating PR validation failures

1. Read the PR and its comments/reviews. Check for references to other PRs or issues where the problem might have already been solved.
2. Use the `ci-analysis` skill (if available) to diagnose build failures.

## Localization

- Consider localizing strings in .resx files when possible.
- When adding a new NETSDK error message in `src/Tasks/Common/Resources/Strings.resx`, assign the next available NETSDK code, append the entry at the end of the file, and update the trailing "latest message added" guard comment.

## Keeping AI context and docs in sync

Before you consider a change complete, ask: **does this change require updates to AI context, instructions, docs, commands, tests, or workflow guidance — and if so, make those updates in the same PR.** When your change alters something a contributor-facing or AI-facing artifact describes, update that artifact in the same change rather than leaving it stale. Check whether any of the following now describe out-of-date behavior:

- `.github/copilot-instructions.md` (this file) and any `AGENTS.md` in the affected subdirectories — repository conventions, guardrails, build/test recipes, and architecture claims.
- `.github/skills/*/SKILL.md` and `.github/agents/*.agent.md` — agent workflow guidance and skills.
- `documentation/` — developer guide and area-specific docs.
- Command help/usage text, error messages, and localized `.resx` strings that documentation or instructions reference.

If the change is genuinely internal and unobservable to users, contributors, or agents, no artifact update is needed — but make that a deliberate call, not an oversight.
