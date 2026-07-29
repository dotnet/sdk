---
emoji: 🧵
name: "Parallel-safety audit on PR (on open / sync)"
description: >-
  Automatically audits the MSTest tests changed by a non-draft PR for
  parallel-safety when the change touches `test/**`, or the repository-root
  `Directory.Build.props` / `Directory.Build.targets` / `Directory.Packages.props`
  that every test project imports.

# Ported from microsoft/testfx (.github/workflows/parallel-safety-audit.md,
# microsoft/testfx#10252) and adapted to this repository's gh-aw conventions
# (PAT pool, Copilot engine, gh-proxy tooling).
#
# Triggers:
# - pull_request `opened` / `reopened` / `ready_for_review` — audit on the PR's
#   first appearance as a non-draft.
#
# `synchronize` is deliberately NOT included. dotnet/sdk merges ~10 PRs a day and
# roughly 60% of them touch `test/**`; re-auditing on every push would multiply
# that by the commits per PR and burn a pooled Copilot PAT each time, for a report
# that rarely changes between pushes. Use `/parallel-audit` (see
# `parallel-safety-audit-command.md`) to refresh the comment on demand.
#
# NOTE: the trigger filters on `test/**` **only**, not `src/**`. gh-aw ORs the
# `paths` entries, so listing `src/**` would fire a full audit on every
# source-only PR that changes no tests — wasted runs with nothing to audit. A
# PR that changes both a test and production code still matches `test/**` and
# still gets the changed-`src/` list (the extraction step diffs `src/`
# regardless of what triggered the run), so read-set analysis is unaffected.
#
# `paths` cannot be paired with `paths-ignore` for the same event, so
# `test/TestAssets/**` and `test/TestPackages/**` (test *inputs*, not test code)
# cannot be filtered out here; the extraction step excludes them instead, and the
# agent posts nothing when that leaves an empty audit surface.
#
# The repository-root `Directory.Build.props` / `.targets` and
# `Directory.Packages.props` are listed as individual files because
# `test/Directory.Build.props` imports them: an `MSTestParallelizeScope` added
# there opts in **every** test assembly at once without touching anything under
# `test/`. Being single files rather than a `src/**`-style wildcard, they do not
# reintroduce source-only runs.
#
# The companion `/parallel-audit` slash command lives in
# `parallel-safety-audit-command.md`. They must remain separate workflows
# because mixing `slash_command` with other triggers makes gh-aw's activation
# gate always require a command-position match, silently skipping the agent on
# every `pull_request` invocation.
on:
  pull_request:
    types: [opened, reopened, ready_for_review]
    paths:
      - "test/**"
      - "Directory.Build.props"
      - "Directory.Build.targets"
      - "Directory.Packages.props"

# Skip:
# - forks of this repository (`repository.fork` describes the *base* repo, so this
#   only stops the workflow running in someone's fork of dotnet/sdk);
# - pull requests from a forked head repository. GitHub does not expose secrets or
#   environments to `pull_request` runs from forks, so the `copilot-pat-pool`
#   environment would yield no PAT and every such run would fail. Maintainers can
#   still audit a fork PR with `/parallel-audit`, which dispatches on the base repo;
# - draft PRs and OneLocBuild localization check-in PRs (authored by dotnet-bot).
if: >
  github.event.repository.fork == false
  && github.event.pull_request.head.repo.full_name == github.repository
  && github.event.pull_request.draft == false
  && !(
    github.event.pull_request.user.login == 'dotnet-bot'
    && startsWith(github.event.pull_request.title, 'Localized file check-in')
  )

permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

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
  - shared/parallel-safety-audit-shared.md

environment: copilot-pat-pool

engine:
  id: copilot
  env:
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}

# This workflow fires once per PR open / reopen / ready-for-review that touches
# `test/**`. dotnet/sdk sees roughly 10 PRs a day and about 60% of them touch
# tests, so budget for ~10 audits a day plus on-demand `/parallel-audit` runs —
# above the enterprise default of 5K credits.
max-daily-ai-credits: 20K

safe-outputs:
  report-failure-as-issue: false
  messages:
    footer: "> 🤖 **Automated content by GitHub Copilot.** Generated by the [{workflow_name}]({agentic_workflow_url}) workflow.{ai_credits_suffix} · [◷]({history_link})"
  noop:
    report-as-issue: false

concurrency:
  group: parallel-safety-audit-${{ github.event.pull_request.number }}
  cancel-in-progress: true

timeout-minutes: 20
---

<!-- Body provided by shared/parallel-safety-audit-shared.md -->
