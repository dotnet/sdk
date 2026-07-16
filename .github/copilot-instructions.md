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

### Repository boundaries and the VMR

An SDK command or build can expose behavior implemented by another .NET repository. Find
the component that defines the behavior before making a change; do not add an SDK
workaround merely because the symptom appears through `dotnet`.

| Repository | Ownership boundary |
| --- | --- |
| [`dotnet/sdk`](https://github.com/dotnet/sdk) | The managed `dotnet` CLI commands and their UX/orchestration; SDK MSBuild tasks, targets, and resolvers; the Template Engine libraries and tools under `src/TemplateEngine`; the `dotnet new` host under `src/Cli/Microsoft.TemplateEngine.Cli`; and common template content under `template_feed`. |
| [`dotnet/runtime`](https://github.com/dotnet/runtime) | CLR and Mono, the base class libraries, the native `dotnet` host/muxer and apphost, runtime and reference packs, and runtime-owned deployment tooling such as NativeAOT and ILLink. SDK publish targets integrate with these artifacts but do not own their implementation. |
| [`dotnet/roslyn`](https://github.com/dotnet/roslyn) | The C# and Visual Basic compilers, compiler server, compiler APIs, and C#/VB compiler behavior such as language diagnostics and code generation. The SDK supplies inputs and ships Roslyn artifacts; SDK-generated defaults and command wiring remain SDK-owned. The F# compiler is in [`dotnet/fsharp`](https://github.com/dotnet/fsharp). |
| [`dotnet/msbuild`](https://github.com/dotnet/msbuild) | The MSBuild engine, evaluation and execution semantics, logging, and core tasks and targets. SDK-specific `Microsoft.NET.*` tasks and targets remain in this repo. |
| [`NuGet/NuGet.Client`](https://github.com/NuGet/NuGet.Client) | NuGet restore, package resolution, protocols, and related MSBuild tasks. SDK CLI wrappers and SDK-specific integration remain in this repo. |
| [`dotnet/project-system`](https://github.com/dotnet/project-system) | Visual Studio-specific project-system behavior. |
| [`dotnet/templating`](https://github.com/dotnet/templating) | Historical home of the Template Engine. Its source and development moved into `dotnet/sdk` in 2026; make current Template Engine changes in this repo. See the [Template Engine overview](../documentation/TemplateEngine/README.md). |
| [`dotnet/dotnet`](https://github.com/dotnet/dotnet) | The Virtual Monolithic Repository (VMR): a synchronized mirror of product repositories plus the infrastructure for building and servicing the integrated .NET product. Product source is mirrored under `src/<repo>`; normal component development still belongs in the owning product repository. |

Do not infer ownership from a diagnostic ID or generated code alone. C# and Visual Basic
compiler diagnostics and compiler-emitted code belong to Roslyn, but analyzers and source
generators belong to the repository that implements them, such as `dotnet/runtime` for
runtime-library generators or `dotnet/sdk` for SDK analyzers.

The standalone SDK build consumes most sibling components as packages or other build
artifacts. Use these files to identify the exact implementation being consumed:

- `eng/Version.Details.xml` records flowed dependency versions and source URI/commit.
  Unified Build backflow commonly records `dotnet/dotnet` as the source for packages from
  several product repositories; that does not transfer ownership to the VMR. When exact
  source matters, inspect `src/source-manifest.json` and the corresponding `src/<repo>`
  directory at the recorded VMR commit, for example `src/runtime`, `src/roslyn`, or
  `src/msbuild`.
- `Directory.Packages.props` centralizes package IDs and their selected versions. Most
  entries reference properties generated in `eng/Version.Details.props`;
  `eng/Versions.props` imports those values and adds manually maintained and
  compatibility-specific version policy. Trace the package or assembly back to its
  product repository instead of changing a version or copying upstream code as a
  substitute for fixing the owner.

The VMR synchronizes product source in both directions through
[code flow](https://github.com/dotnet/dotnet/blob/main/docs/Codeflow-PRs.md): forward flow
moves product-repository changes into the VMR, and backflow returns VMR source changes and
newly built dependencies to product repositories. Do not edit a mirrored VMR `src/*`
directory for an ordinary component fix. Make direct VMR changes only for VMR-owned
infrastructure and pipelines, or when servicing, integration, or cross-repository work is
explicitly coordinated; follow the
[VMR contribution guidance](https://github.com/dotnet/dotnet#contribution).
Source-build is a validation mode, not an ownership boundary: component-specific
source-build behavior belongs with that component, while whole-product source-build
orchestration belongs in the VMR.

For a cross-repository issue:

1. Identify the executable, assembly, task, target, or package that implements the
   behavior, and inspect the version actually consumed by this checkout.
2. Put the semantic fix and its primary tests in the owning repository. Keep only genuine
   SDK orchestration, defaults, compatibility, or integration changes in `dotnet/sdk`.
3. If multiple components must change, keep each change and its tests with its owner, then
   consume the flowed build. Validate the integrated VMR when the issue depends on an
   exact component combination, source-build, or cross-repository target hooks.

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
| `TemplateEngine/` | Template Engine libraries and authoring tools. The `dotnet new` CLI host is under `Cli/Microsoft.TemplateEngine.Cli`. |
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
