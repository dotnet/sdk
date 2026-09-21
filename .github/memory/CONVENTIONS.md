---
coverage: Repository-wide code style, framework/build constraints, dependency management, and canonical area convention sources
---

# Conventions

Use this file for repository-wide style that is not already owned by an area
`AGENTS.md`, a skill, or the root guardrails.

## Code Style

From [`.editorconfig`](../../.editorconfig):

- Use spaces, a final newline, and no trailing whitespace. Indent code with 4 spaces and
  Markdown/JSON with 2 spaces.
- C# uses braces, `using` directives outside namespaces, and `System` directives first.
- Use C# keywords rather than BCL type names. The configured preference is explicit types.
- Name constants in PascalCase, private/internal static fields with `s_`, and other
  private/internal fields with `_camelCase`.
- Source files use the standard .NET Foundation MIT header.
- Public API analyzer files are required where the owning project enables that analyzer.

Repository policy:

- Match the style of the existing file and keep changes focused.
- Do not run repository-wide formatting for a focused change.
- Prefer file-scoped namespaces for new C# code and remove unused `using` directives.
- Use `#if NET` for .NET-only code and `#if NETFRAMEWORK` for .NET Framework-only code.
- Follow the existing file when an area has a deliberate older pattern.

## Framework and Build Constraints

- Root builds use the preview C# language version, nullable annotations, warnings as
  errors, implicit usings, central package management, and the Arcade SDK; see
  [`Directory.Build.props`](../../Directory.Build.props).
- Source and test projects targeting current .NET use `$(SdkTargetFramework)`. Test
  assets use `$(CurrentTargetFramework)` because the test harness substitutes it.
- Multi-targeting projects that include .NET Framework follow peer projects in their area.
- Area-specific compatibility constraints belong in the nearest `AGENTS.md`; notably,
  task assemblies and resolvers have .NET Framework host requirements.

## Dependency Management

- Prefer the BCL or an existing repository dependency; add a dependency only at the
  narrowest necessary scope.
- Restore only from sources in [`NuGet.config`](../../NuGet.config). Do not edit
  automation-managed feed blocks or add ad hoc feeds to make restore succeed.
- Omit `Version` from normal `PackageReference` items. Update the existing declaration in
  [`Directory.Packages.props`](../../Directory.Packages.props) or its imported owner:
  property-backed manual versions live in `eng/Versions.props` or
  `eng/ManualVersions.props`, while `eng/dependabot/Packages.props` is Dependabot-owned.
- Dependencies represented in `eng/Version.Details.xml` use Darc/Maestro flow. Never
  hand-edit generated `eng/Version.Details.props`.
- Treat `NU19xx` audit findings as actionable: update or remove the package rather than
  suppressing the warning or weakening audit settings.
- Pin external GitHub Actions and reusable workflows in runtime workflow YAML to full
  40-character commit SHAs, with the tag or branch in a trailing comment. Dependabot
  maintains these pins through [`.github/dependabot.yml`](../dependabot.yml); do not
  hand-edit generated `*.lock.yml` workflows.

## Canonical Convention Owners

| Subject | Canonical source |
| --- | --- |
| Hard guardrails, CI telemetry, and generated files | [Root instructions](../../AGENTS.md#guardrails) |
| Repository build properties and analyzer configuration | [`Directory.Build.props`](../../Directory.Build.props), [`Directory.Build.targets`](../../Directory.Build.targets), [`.editorconfig`](../../.editorconfig) |
| CLI commands and AOT constraints | [`src/Cli/AGENTS.md`](../../src/Cli/AGENTS.md) and CLI skills |
| MSBuild tasks, targets, diagnostics, and framework compatibility | [`src/Tasks/AGENTS.md`](../../src/Tasks/AGENTS.md) |
| SDK resolver hosts, linked sources, interop, and dependency constraints | [`src/Resolvers/AGENTS.md`](../../src/Resolvers/AGENTS.md) |
| Redist composition and bundled-version flow | [`src/Layout/AGENTS.md`](../../src/Layout/AGENTS.md) |
| Test infrastructure, assets, parallelism, snapshots, and Helix | [`test/AGENTS.md`](../../test/AGENTS.md) and [TESTING_STRATEGY.md](TESTING_STRATEGY.md) |
| Repeatable task procedures | `.github/skills/*/SKILL.md` and `.claude/skills/*/SKILL.md` |

Prefer the nearest area guidance over general summaries, and cross-check it against the
linked primary source before relying on it.
