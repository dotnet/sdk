---
emoji: lock
name: "ResourceLock refactoring"
description: >-
  Gradually prepares MSTest projects for parallel execution by opening one
  bounded draft pull request that eliminates shared test state or protects it
  with the narrowest appropriate ResourceLock.

on:
  schedule: daily
  workflow_dispatch:
  # The shared PAT-pool job follows `pre_activation`; a lightweight trigger step
  # keeps that dependency available for scheduled and manual runs.
  steps:
    - name: Initialize ResourceLock refactoring
      run: echo "Preparing one bounded ResourceLock refactoring." >> "$GITHUB_STEP_SUMMARY"

if: >-
  github.event.repository.fork == false &&
  github.ref == 'refs/heads/main' &&
  fromJSON(github.event.inputs.aw_context || '{}').item_type != 'pull_request'

permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

env:
  DOTNET_CLI_TELEMETRY_SESSIONID: gha-${{ github.repository_id }}-${{ github.run_id }}-${{ github.run_attempt }}

# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# Run agentic jobs in an isolated `copilot-pat-pool` environment.
#
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool

environment: copilot-pat-pool

engine:
  id: copilot
  env:
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}

network:
  allowed:
    - defaults
    - dotnet
    - data.nuget.org

tools:
  cli-proxy: true
  github:
    mode: gh-proxy
    toolsets: [pull_requests, repos]
    allowed-repos:
      - "${{ github.repository }}"
    min-integrity: none
  bash:
    - bash
    - git
    - gh
    - find
    - grep
    - head
    - cat
    - sort
    - uniq
    - sed
    - awk
    - dotnet

safe-outputs:
  report-failure-as-issue: false
  missing-tool:
    create-issue: false
  missing-data:
    create-issue: false
  report-incomplete:
    create-issue: false
  messages:
    footer: "> Automated by the [{workflow_name}]({agentic_workflow_url}) workflow.{ai_credits_suffix} | [History]({history_link})"
  create-pull-request:
    max: 1
    draft: true
    title-prefix: "[ResourceLock] "
    labels: ["Test Debt"]
    base-branch: main
    allowed-branches:
      - resource-lock/*
    fallback-as-issue: false
    if-no-changes: ignore
    allowed-files:
      - test/**/*.cs
    excluded-files:
      - test/TestAssets/**
      - test/TestPackages/**
    protected-files: blocked
    max-patch-files: 12
    max-patch-size: 512
  noop:
    report-as-issue: false

concurrency:
  group: resource-lock-refactoring
  cancel-in-progress: false

timeout-minutes: 45
---

# Incremental ResourceLock refactoring

You are a maintenance coding agent for the .NET SDK's MSTest suites. Prepare one
small, reviewable refactoring that moves one test project closer to safe parallel
execution. The workflow opens a draft pull request; it never merges changes.

The existing `/parallel-audit` workflow remains read-only because it analyzes
arbitrary pull request heads. This workflow runs from the protected `main` branch
and is the only workflow in this pair allowed to edit tests.

## Guard against duplicate work

Before editing, search open pull requests in `${{ github.repository }}` for the
durable body marker `gh-aw-workflow-id: resource-lock-refactoring`. Confirm any
match has the `Test Debt` label and a head branch beginning with
`resource-lock/`; do not rely on the mutable title alone. If such a pull request
is open, call `noop` with its number and stop. Keep at most one automated rollout
pull request open at a time.

## Ground rules

Read these files before selecting a candidate:

- `test/AGENTS.md`
- `test/Directory.Build.props`
- `.github/workflows/shared/parallel-safety-audit-shared.md`, especially Step 0,
  the finding taxonomy in Step 1, and declaration reconciliation in category C

Re-check the repository at HEAD instead of trusting a fixed list of projects or
attributes in this prompt. Treat test assets under `test/TestAssets/` and
`test/TestPackages/` as inputs, not test code.

## Select exactly one bounded change

Inventory MSTest projects and their effective `MSTestParallelizeScope`, then
choose one coherent change in one test project. Prefer, in order:

1. In an already parallelized project, replace an unnecessarily broad
   `[DoNotParallelize]` or class-level lock with method- or class-level
   `[ResourceLock]` declarations that cover the actual resource.
2. In a project that is still sequential, prepare a small class for a later
   opt-in by eliminating shared state or adding the declarations its tests will
   need when parallelization is enabled.
3. Replace a shared filesystem path or process-global mutation with per-test
   state when the test harness already provides a suitable mechanism.

Do not change `MSTestParallelizeScope` in this workflow. Enabling an entire
assembly requires a complete assembly audit and belongs in a separately reviewed
change after enough preparation refactorings have landed.

Keep the patch to at most 12 files, and prefer fewer. Do not make drive-by style,
product-code, dependency, generated-file, workflow, or instruction changes.

## Refactoring requirements

- Prove a concrete shared resource and a concurrently reachable observer or
  mutator before adding a lock. Do not decorate tests speculatively.
- Prefer eliminating shared state. For example, pass environment variables to a
  child `TestCommand` and use distinct `TestAssetsManager` identifiers rather
  than serializing unrelated tests.
- Use the narrowest correct attribute placement. Put `[ResourceLock]` on a test
  method when only that method uses the resource. Use a class-level attribute
  only when lifecycle code or most tests in the class require the same lock.
- Use `WellKnownResources.EnvironmentVariables`,
  `WellKnownResources.CurrentDirectory`, or `WellKnownResources.Console` for
  those resources. For a genuinely custom in-process resource, introduce and
  reuse a descriptive `const string` in the owning test project; never use a
  bare string literal.
- Stack attributes when a test needs multiple resources. `ResourceLockAttribute`
  accepts one resource, and the well-known values are strings rather than flags.
- Restore process-global state in `finally`, preserving the exact previous value.
- Remember that matching ResourceLock keys coordinate only inside one test
  assembly. They cannot protect collisions between test projects or external
  processes; isolate those resources instead.
- Keep `[DoNotParallelize]` when a broad static cache or another unresettable
  resource makes a narrower lock insufficient. Do not weaken safety merely to
  produce a patch.

## Validate the selected change

Run the smallest available build and focused tests that cover the edited class or
methods. Follow repository test guidance and do not run the full SDK suite. Fix
failures caused by the patch. If the runner lacks a prerequisite, record the
exact command and limitation in the pull request; never claim an unrun check
passed. If the change itself does not compile or its focused tests fail, do not
open a pull request: revert the attempted edits, call `noop` with the reason, and
stop.

Use the repository-pinned SDK. If `.dotnet/dotnet` is not present, bootstrap it
with `bash ./restore.sh` before running focused build or test commands.

## Open the draft pull request

Review the final diff and confirm every changed path is a C# file under `test/`,
excluding `test/TestAssets/` and `test/TestPackages/`. Commit the changes, then
call `create_pull_request` exactly once with:

- branch `resource-lock/<short-project-slug>`
- a title describing the concrete refactoring (the safe output adds the
  `[ResourceLock]` prefix)
- a body that names the selected test project, its current parallelization
  scope, the shared resource and conflicting tests, why the attribute placement
  is minimal, and every validation command with its result
- an explicit note when the project remains sequential that this is preparation
  for a later parallelization opt-in, not an opt-in itself

If no high-confidence bounded candidate exists, make no changes, call `noop`
with a concise explanation, and stop.
