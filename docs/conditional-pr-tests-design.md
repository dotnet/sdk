# Conditional PR Test Execution - Design

## Problem Statement

The SDK's PR validation pipeline (definition 101) takes 1-3 hours to complete. Analysis shows:
- ~10 minutes to build the SDK
- ~60+ minutes running tests in Helix (per leg)
- Queue delays add 30-40 minutes on busy days
- Tests are identical across PR and CI — no differentiation

The SDK is broad (analyzers, templates, containers, Blazor/SWA, ILLink, CLI, etc.), but most PRs touch only one area.

## Approach

Run a subset of tests in PR validation based on which source paths changed. Always run full tests in CI and for codeflow/dependency PRs.

## Current Implementation (Proof of Concept)

### Detection Flow

```
PR build triggered
    → detect-test-scope.sh / .ps1 runs
    → Checks: Is this CI? Is this a codeflow PR (darc-* branch)? Did eng/Versions.props change?
        → If yes to any: run ALL tests
    → Otherwise: git diff against target branch, grep for path patterns
    → Sets ##vso[task.setvariable] for each test group
    → Variables passed as /p: properties to MSBuild
    → UnitTests.proj conditionally removes test projects
```

### Files Involved

| File | Role |
|------|------|
| `.vsts-pr.yml` | Pipeline parameter (`runAllTests`) and variable |
| `eng/pipelines/scripts/detect-test-scope.sh` | Path detection (linux/macOS) |
| `eng/pipelines/scripts/detect-test-scope.ps1` | Path detection (Windows) |
| `eng/pipelines/templates/jobs/sdk-build.yml` | Wires detection into build, passes /p: properties |
| `test/UnitTests.proj` | Conditional Remove based on properties |

### Test Groups and Path Mappings

| Group | Estimated CPU (per leg) | Source Paths |
|-------|------------------------|--------------|
| Analyzers | 76 min (70 shards) | `src/RoslynAnalyzers`, `src/NetAnalyzers` |
| Blazor/SWA | 40 min (35 shards) | `src/BlazorWasmSdk`, `src/StaticWebAssetsSdk`, `src/RazorSdk` |
| Templates | 25 min (21 shards) | `src/Cli/dotnet/commands/dotnet-new`, `template_feed` |
| Containers | 12 min (8 shards) | `src/Containers` |
| Watch | 15 min (12 shards) | `src/BuiltInTools/dotnet-watch` |
| ILLink/Publish | 93 min (13 shards) | `src/ILLink`, `src/Cli/dotnet/commands/dotnet-publish` |

## Open Questions / Future Work

### 1. Override Mechanism for PR Builds (TODO)

**Problem**: When a PR build is auto-triggered, how can a developer request a full test run?

**Options considered**:
- ~~Commit message keyword (`[test-all]`)~~ — Rejected: pollutes git history
- **PR description keyword** — Preferred: developer adds e.g. `[test-all]` to the PR body. Script reads via AzDO API using `$(System.PullRequest.PullRequestId)`.
- **`/azp run` style comment** — Ideal UX but AzDO's `/azp run` doesn't support parameter overrides. Would require a custom webhook/bot or Azure Function to parse comments and queue with parameters.
- **Manual re-queue** — Already works via the `runAllTests` pipeline parameter, but only for manual queues (not auto-triggered).

**Decision**: Implement PR description keyword as the primary mechanism. Investigate `/azp`-style comment trigger as a follow-up.

### 2. Configuration-Driven Design (TODO)

**Problem**: Adding a new test scope currently requires touching 3+ files:
1. Detection scripts (`.sh` and `.ps1`) — new path patterns
2. `sdk-build.yml` — new `/p:` property
3. `UnitTests.proj` — new conditional Remove block

This is too many touchpoints and error-prone.

**Desired state**: A single configuration file defines test scopes:
```yaml
# Example: eng/pipelines/test-scopes.yml (strawman)
scopes:
  - name: Analyzers
    paths:
      - src/RoslynAnalyzers/**
      - src/NetAnalyzers/**
    test_assemblies:
      - Microsoft.NET.Build.Analyzers.UnitTests
      - Microsoft.DotNet.ApiCompat.*Tests
      - Microsoft.DotNet.GenAPI.*Tests
      - Microsoft.DotNet.PackageValidation.*Tests

  - name: Containers
    paths:
      - src/Containers/**
    test_assemblies:
      - Microsoft.NET.Build.Containers.*Tests
```

**How this would work**:
- Detection script reads this YAML, iterates scopes, checks paths → outputs a single variable (e.g., `SkippedScopes=Analyzers;Containers`)
- `UnitTests.proj` reads the same YAML (or a generated props file) to determine which assemblies to remove
- `sdk-build.yml` passes a single `/p:SkippedTestScopes=$(SkippedScopes)` — no per-scope properties
- Adding a new scope = one YAML edit

**Decision**: Implement config-driven approach in v2. Current POC validates the concept.

### 3. Platform Coverage Reduction

Currently all 3 platforms (linux, windows, macOS) run identical tests. Could skip macOS in PR for non-platform-specific changes.

### 4. AoT Leg Optimization

AoT legs only run Blazor WASM AoT tests (4 shards). Could skip entirely if Blazor paths aren't touched — requires a separate "Evaluate" job with dependsOn.

## Safety Guarantees

1. Any detection failure → run all tests (fail-open)
2. CI pipeline (`.vsts-ci.yml`) is unchanged → always full coverage
3. Codeflow PRs (darc-* branches) → always full coverage
4. Dependency updates (eng/Versions.props changes) → always full coverage
5. Manual override always available via `runAllTests` parameter

## Impact Estimate

For a typical CLI-only PR, skippable items per leg:
- Analyzers: 70 work items
- Blazor/SWA: 35 work items
- Templates: 21 work items
- Watch: 12 work items
- Containers: 8 work items
- **Total skippable: ~146 of 336 items (43%)**

Expected PR time: 75 min → 40-50 min for targeted PRs (limited by remaining long-pole shards like dotnet.Tests and Build.Tests which always run).
