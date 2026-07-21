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

### Grounding architecture and product claims

Treat this overview as an index, not as independent evidence. In plans, reviews,
root-cause analyses, and AI-facing documentation:

- Verify important architecture, product behavior, and ownership claims against primary
  sources and link the nearest evidence: code or project files for current behavior, and an
  ADR, design document, issue, or PR for decisions and history.
- Prefer repository-relative links for in-repo evidence and link the narrowest durable
  source. Do not cite this overview to support itself.
- When a cited in-repo source is relevant to the task, inspect it before relying on the
  claim; do not assume the link target's contents are already in context.
- Identify inference explicitly and cite its inputs. If sources disagree or evidence is
  incomplete, state the uncertainty instead of turning synthesis into fact; update stale
  context in the same change.

### What the SDK does

- Provides the `dotnet` command-line driver (`dotnet build`, `restore`, `publish`, `test`,
  `run`, `watch`, etc.); see the [managed entry point](../src/Cli/dotnet/Program.cs) and
  [registered command tree](../src/Cli/dotnet/Parser.cs).
- Ships the MSBuild logic that turns a `.csproj`/`.fsproj`/`.vbproj` into a build; see the
  [SDK entry points](../src/Tasks/Microsoft.NET.Build.Tasks/sdk/) and
  [tasks and targets](../src/Tasks/Microsoft.NET.Build.Tasks/).
- Bundles related toolsets: project/item templates, Razor/Blazor/Web/Static Web Assets
  SDKs, container publishing, file/format/watch tools, API compatibility tooling, and
  workload management. The final SDK layout is assembled by the
  [`redist` project](../src/Layout/redist/redist.csproj) and its
  [layout targets](../src/Layout/redist/targets/Directory.Build.targets); in-box project
  and item template sources live in [`template_feed`](../template_feed/).

Do not assume a product change belongs in this repo. The
[runtime](https://github.com/dotnet/runtime),
[C# and Visual Basic compilers](https://github.com/dotnet/roslyn),
[F# compiler](https://github.com/dotnet/fsharp),
[MSBuild](https://github.com/dotnet/msbuild), [NuGet](https://github.com/NuGet/NuGet.Client), and
[Visual Studio project system](https://github.com/dotnet/project-system) are owned in
their linked repositories. The
[dotnet/dotnet VMR](https://github.com/dotnet/dotnet/blob/main/README.md) brings component
source together to build the full .NET SDK.

### Architecture and major components

The managed CLI dispatches commands registered in
[`Parser.cs`](../src/Cli/dotnet/Parser.cs). Unmatched input goes through external command
resolution and then file-based app fallback, as implemented by
[`Program.cs`](../src/Cli/dotnet/Program.cs).

Major source areas under [`src/`](../src/):

| Area | Purpose |
| --- | --- |
| [`Cli/`](../src/Cli/) | The `dotnet` driver, command implementations, and CLI utilities. [`Cli/dotnet/Program.cs`](../src/Cli/dotnet/Program.cs) is the managed entry point. |
| [`Tasks/`](../src/Tasks/) | MSBuild tasks and targets. [`Microsoft.NET.Build.Tasks`](../src/Tasks/Microsoft.NET.Build.Tasks/) contains the primary SDK task assembly and SDK imports. |
| [`Resolvers/`](../src/Resolvers/) | MSBuild SDK resolvers, including SDK and workload resolution. |
| [`RazorSdk/`](../src/RazorSdk/), [`BlazorWasmSdk/`](../src/BlazorWasmSdk/), [`WasmSdk/`](../src/WasmSdk/), [`WebSdk/`](../src/WebSdk/), [`StaticWebAssetsSdk/`](../src/StaticWebAssetsSdk/) | Web, Razor, Blazor, WebAssembly, and Static Web Assets build SDKs. |
| [`Containers/`](../src/Containers/) | `dotnet publish` container image support. |
| [`Dotnet.Watch/`](../src/Dotnet.Watch/), [`Dotnet.Format/`](../src/Dotnet.Format/) | `dotnet watch` and `dotnet format` tools. |
| [`Compatibility/`](../src/Compatibility/) | ApiCompat, GenAPI, API diff, and package validation tooling. |
| [`TemplateEngine/`](../src/TemplateEngine/) | Template engine libraries and authoring/discovery tools; see the [Template Engine overview](../documentation/TemplateEngine/README.md). |
| [`Workloads/`](../src/Workloads/), [`Microsoft.DotNet.TemplateLocator/`](../src/Microsoft.DotNet.TemplateLocator/) | Workload manifests and installation, plus workload-provided template pack location. |
| [`Layout/`](../src/Layout/) | Composes the final `dotnet` layout through [`redist.csproj`](../src/Layout/redist/redist.csproj). |

### Key files and directories

- [`build.cmd`](../build.cmd) / [`build.sh`](../build.sh) — primary top-level build entry
  point (Arcade-based). [`test.cmd`](../test.cmd) / [`test.sh`](../test.sh) and
  [`restore.cmd`](../restore.cmd) / [`restore.sh`](../restore.sh) are thin wrappers that
  forward to it.
- [`sdk.slnx`](../sdk.slnx) — the full solution. Filtered solutions exist for focused work:
  [`cli.slnf`](../cli.slnf), [`tasks.slnf`](../tasks.slnf),
  [`containers.slnf`](../containers.slnf), [`TemplateEngine.slnf`](../TemplateEngine.slnf),
  and [`source-build.slnf`](../source-build.slnf).
- [`global.json`](../global.json) — pins the bootstrap SDK and Arcade versions used to build
  the repo.
- [`Directory.Build.props`](../Directory.Build.props) /
  [`Directory.Build.targets`](../Directory.Build.targets) /
  [`Directory.Packages.props`](../Directory.Packages.props) — repo-wide MSBuild settings and
  central package version management.
- [`eng/`](../eng/) — Arcade build infrastructure, versioning
  ([`eng/Versions.props`](../eng/Versions.props)), and the
  `dogfood` scripts.
- `artifacts/bin/redist/<configuration>/dotnet` (`Debug` by default) — the built SDK;
  `.dotnet/dotnet` is the bootstrap SDK. See the
  [Developer Guide](../documentation/project-docs/developer-guide.md#building).
- [`documentation/`](../documentation/) — project docs, including the developer guide and
  area-specific guides.
- [`template_feed/`](../template_feed/) — the in-box project and item templates.
- [`test/`](../test/) — test projects and
  [`test/TestAssets/TestProjects`](../test/TestAssets/TestProjects/) test inputs.

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

## External Dependencies

Adding or updating a dependency is a repo-wide compatibility and supply-chain change,
not just a project-file edit. Prefer the BCL, existing repository code, or a
dependency already in use. Add a new dependency only at the narrowest necessary scope.

- **Approved feeds:** Use only the restore sources in the root
  [`NuGet.config`](../NuGet.config). For normal repository dependencies, do not add
  ad hoc feeds just to make restore succeed. Do not edit automation-managed feed blocks.
- **Version policy:** Central package management is enabled in
  [`Directory.Build.props`](../Directory.Build.props).
  - Omit `Version` from normal project `PackageReference` items and change the declaration
    in [`Directory.Packages.props`](../Directory.Packages.props) or its imported owner
    instead.
  - Locate the existing `PackageVersion` and update its actual owner: literal versions in
    `Directory.Packages.props` are managed there, property-backed manual versions live in
    [`eng/Versions.props`](../eng/Versions.props) or
    [`eng/ManualVersions.props`](../eng/ManualVersions.props), and packages listed in
    [`eng/dependabot/Packages.props`](../eng/dependabot/Packages.props) are updated by
    Dependabot.
  - For dependencies represented in
    [`eng/Version.Details.xml`](../eng/Version.Details.xml), use the Darc/Maestro
    dependency-flow workflow so the manifest, generated properties, and feeds stay in
    sync; never hand-edit the generated
    [`eng/Version.Details.props`](../eng/Version.Details.props).
- **Security:** Local restores enable NuGet Audit for all dependencies at low severity
  or higher in [`Directory.Build.props`](../Directory.Build.props), using the audit
  source in [`NuGet.config`](../NuGet.config). Treat `NU19xx` findings as actionable:
  update or remove the affected package rather than suppressing the warning or weakening
  audit settings.

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
  - Most test projects require a full redist SDK build first (see the build steps above); `dotnetup.Tests` is the exception and runs without one.
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
