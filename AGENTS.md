# Agent Instructions

Instructions for GitHub Copilot and other AI coding agents working with the .NET SDK repository.

## Overview

This is `dotnet/sdk`, the repository for the core of the .NET SDK. It produces the
`dotnet` CLI driver and the MSBuild tasks, targets, templates, and resolvers that are
shared between the .NET CLI and Visual Studio. The build output is a complete, runnable
`dotnet` installation laid down at `artifacts/bin/redist/<configuration>/dotnet`
(`Debug` by default).

For a high-level project description, build status, and contribution flow, see the
[README](README.md). For the canonical build/test/debug walkthrough, see the
[Developer Guide](documentation/project-docs/developer-guide.md).

### Agent orientation and memory

1. Read the [memory index](.github/memory/INDEX.md) first and load other memory files on demand.
2. For non-trivial work, also read [ARCHITECTURE.md](.github/memory/ARCHITECTURE.md) and
   [CONVENTIONS.md](.github/memory/CONVENTIONS.md).
3. Treat memory as orientation; cross-check important claims against linked primary sources.
4. Correct stale memory in the same change and keep the index synchronized.

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

See [ARCHITECTURE.md](.github/memory/ARCHITECTURE.md) for product components and data flow,
[FILE_MAP.md](.github/memory/FILE_MAP.md) for repository locations, and
[API_MAP.md](.github/memory/API_MAP.md) for user-facing and extension surfaces.

### Repository boundaries and the VMR

An SDK command or build can expose behavior implemented by another .NET repository. Find
the component that defines the behavior before making a change; do not add an SDK
workaround merely because the symptom appears through `dotnet`, and do not infer ownership
from a diagnostic ID. See the canonical
[ownership map](.github/memory/ARCHITECTURE.md#ownership-boundaries).

### Build and test

- Build the redist SDK with `build.cmd` on Windows or `./build.sh` on Linux/macOS.
- Add `-test` / `--test` for the full suite and `-pack` / `--pack` for packages/installers;
  avoid these large operations in the routine inner loop.
- Use [`run-tests` skill](.github/skills/run-tests/SKILL.md) for focused validation and
  [`incremental-test` skill](.github/skills/incremental-test/SKILL.md) for supported
  `dotnet.Tests` changes.
- Product tests exercise `artifacts/bin/redist/<configuration>/dotnet`; ensure it contains
  the production change before trusting results.

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
- Run tests through the `run-tests` skill. It selects the appropriate focused, scoped, or
  full-suite workflow and retains actionable diagnostics for focused runs (see
  [Testing](#testing)).
- Release build: `build.cmd -c Release`.
- Validate changes locally using the SDK you built at
  `artifacts/bin/redist/<configuration>/dotnet` (`Debug` by default).

See [TESTING_STRATEGY.md](.github/memory/TESTING_STRATEGY.md) and the
[Developer Guide](documentation/project-docs/developer-guide.md).

## Guardrails

These are hard boundaries for agents working in this repo. Treat them as "must not" rules.

### Flag user-visible behavior

Call out intentional user-visible behavior or contract changes in the final handoff.

### Do not hand-edit generated files

Some files are produced by tooling and are overwritten the next time the build or a
generation step runs. Editing them by hand causes drift and merge conflicts. Never
manually edit:

- **`.xlf` localization files.** Change the source `.resx` strings instead, then
  regenerate the `.xlf` with the `/t:UpdateXlf` MSBuild target. Correctly regenerated
  entries have a state of `needs-review-translation` or `new`. See
  [Localization](documentation/project-docs/Localization.md) for the full workflow.
- **Generated man pages** under `documentation/manpages/sdk`. These are generated from
  documentation; change the upstream documentation in https://github.com/dotnet/docs instead.
- **Generated workflow lock files** (`.github/workflows/*.lock.yml`).
- More broadly, any file marked `linguist-generated=true` in `.gitattributes`.

### Preserve CI telemetry correlation

Set `DOTNET_CLI_TELEMETRY_SESSIONID` in every CI workflow and pipeline entry point. Set
the variable at the workflow or pipeline scope. Job scope is valid for a single-job
workflow. Use the applicable value without changes:

- GitHub Actions:
  `gha-${{ github.repository_id }}-${{ github.run_id }}-${{ github.run_attempt }}`
- Azure DevOps:
  `azdo-$(System.CollectionId)-$(System.TeamProjectId)-$(Build.BuildId)`

When you change shared CI environment variables, preserve this variable. See
the [developer guide](documentation/project-docs/developer-guide.md#ci-workflow-telemetry-correlation)
for the required YAML and the reason for this variable.

## External Dependencies

Adding or updating a dependency is a repo-wide compatibility and supply-chain change.
Follow [CONVENTIONS.md](.github/memory/CONVENTIONS.md#dependency-management).

## Coding Style

Follow [CONVENTIONS.md](.github/memory/CONVENTIONS.md) and the nearest area `AGENTS.md`.

### Target framework properties

Never hardcode the current TFM in a project. See
[CONVENTIONS.md](.github/memory/CONVENTIONS.md#framework-and-build-constraints).

## Testing

- Large changes should always include test changes.
- The Skip parameter of the Fact attribute to point to the specific issue link.
- Use the `run-tests` skill for every local test execution. Choose projects from
  `test/ConditionalTests.props`
  when the changed paths match a configured scope, or use its fallback mappings for
  unscoped common areas. Run one project, class, or method with detailed live output and
  retained TRX/binlog diagnostics.
- For incremental test runs of `dotnet.Tests` (avoids slow full `build.cmd`), use the `incremental-test` skill.
- Follow [`test/AGENTS.md`](test/AGENTS.md) and
  [TESTING_STRATEGY.md](.github/memory/TESTING_STRATEGY.md) for test framework, assets,
  parallelism, conditional scopes, snapshots, and Helix guidance.
- This repo uses conditional test filtering to skip expensive test suites on PRs when
  relevant source files have not changed. When adding new test projects, consider
  registering them as a scope in [`test/ConditionalTests.props`](test/ConditionalTests.props).
  See [`documentation/project-docs/pr-test-filtering.md`](documentation/project-docs/pr-test-filtering.md) for details.

## Investigating PR validation failures

1. Read the PR and its comments/reviews. Check for references to other PRs or issues where the problem might have already been solved.
2. Use the `ci-analysis` skill (if available) to diagnose build failures.

## Keeping AI context and docs in sync

Before completion, run the [`update-docs` skill](.github/skills/update-docs/SKILL.md). It owns the
required checklist for memory, instructions, `AGENTS.md`, skills, agents, contributor
documentation, help, snapshots, and localized resources.

If the change is genuinely internal and unobservable to users, contributors, or agents, no artifact update is needed — but make that a deliberate call, not an oversight.
