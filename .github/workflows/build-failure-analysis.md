---
name: "Build Failure Analysis"
description: >-
  When the Azure Pipelines PR build (`dotnet-sdk-public-ci`) fails, downloads the binary
  logs that build already produced — it does NOT rebuild — and delegates to
  the `build-failure-analyst` agent, which queries the binlogs live via the
  containerized `binlog-mcp` MCP server to identify root causes, post a PR
  comment summarizing them, and attach inline `suggestion` blocks tied to the
  diff.

# This workflow is **advisory**, not gating, and it performs **no build of its
# own**. The SDK's authoritative PR build runs on Azure DevOps
# (dnceng-public/public, pipeline "dotnet-sdk-public-ci", definitionId 101) and publishes
# each build leg's binary logs inside a `<Leg>_Logs_Attempt<N>` pipeline
# artifact (e.g. `Windows_x64_Logs_Attempt1`). When
# that build's GitHub check reports failure, this workflow downloads the
# binlogs from **all** build legs (anonymously — dnceng-public/public is a
# public project) and the agent analyses whichever leg(s) actually contain
# errors. Reusing the binlogs avoids a duplicate build: the analysis pipeline
# only downloads build artifacts (data) and reads them — it does **not** build
# or execute PR code. (gh-aw's generated agent job **does** check out the
# repository — via `actions/checkout` — to load the workflow's own agent
# configuration and, since the `checkout:` block below, the analysed PR head so
# the agent can author a fix commit. The PR tree is only read and edited as
# text; nothing in it is built or executed.)

on:
  # `check_run` fires for every check on a commit, so the `fetch-binlog` job
  # below filters tightly to the rollup `dotnet-sdk-public-ci` build check
  # reporting failure. The pipeline also emits per-leg checks named
  # `dotnet-sdk-public-ci (Build <leg>)`; the exact-name match below
  # deliberately ignores those so the analysis runs once per build, not once
  # per leg.
  check_run:
    types: [completed]
  # Advisory analysis should run for **every** failing PR — including external
  # contributors' PRs, which are the most likely to break the build. Disable
  # gh-aw's default author-association gate (which would otherwise skip
  # non-write-access actors, and on `check_run` the actor is the pipeline app
  # anyway). This is safe here: the workflow only reads a public binlog and
  # posts advisory comments — it never builds or executes PR code. The one
  # write path that touches code (`push-to-pull-request-branch`) is refused
  # outright by gh-aw's handler for fork PRs, so `roles: all` cannot turn an
  # external contribution into a push.
  roles: all
  # Manual entry point for reruns / testing: analyse a specific Azure DevOps
  # build id and post to a specific PR.
  workflow_dispatch:
    inputs:
      ado-build-id:
        description: "Azure DevOps build id to analyze (dnceng-public/public)."
        required: true
        type: string
      pr-number:
        description: "PR number to post the analysis on."
        required: true
        type: string
  # Gate the whole AI pipeline on the fetch job so the agent only runs when a
  # binlog was actually retrieved.
  needs: [fetch-binlog]

# Activate (and run the agent) only when the fetch job retrieved at least one
# binlog. When `check_run` fires for an unrelated / passing check the
# fetch-binlog job is skipped, its output is empty, and this cascades into a
# skipped agent — no AI calls on anything but a real `dotnet-sdk-public-ci`
# failure whose PR targets an in-scope base branch.
if: needs.fetch-binlog.outputs.binlog-found == 'true'

# Least-privilege for the workflow/agent jobs. The agent runs read-only; it
# does NOT post directly. All PR writes (summary comment + inline review
# suggestions + the fix commit) go through gh-aw **safe-outputs**, which the
# compiler emits as a separate `safe_outputs` job granted `pull-requests:
# write` + `issues: write` (and, for `push-to-pull-request-branch`, `contents:
# write`) in the generated lock. Keep `pull-requests: read` here so the AI
# agent job stays least-privilege — do NOT raise it to `write`, that would
# hand PR-write scope to the agent job unnecessarily.
permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

concurrency:
  # Only real `dotnet-sdk-public-ci` check_run events (and manual dispatch for
  # a PR) use a PR/head-scoped group, so a newer analysis supersedes an
  # in-progress one for the same PR. Every OTHER completed check_run on the PR
  # would otherwise land in the same group and — with cancel-in-progress —
  # abort the running real analysis, so those get a unique per-run group that
  # collides with nothing.
  group: ${{ (github.event_name == 'check_run' && github.event.check_run.name == 'dotnet-sdk-public-ci' && format('build-failure-analysis-{0}', github.event.check_run.pull_requests[0].number || github.event.check_run.head_sha)) || (github.event_name == 'workflow_dispatch' && format('build-failure-analysis-{0}', inputs['pr-number'])) || format('build-failure-analysis-run-{0}', github.run_id) }}
  cancel-in-progress: true

timeout-minutes: 30

# The agent job's default checkout uses the event ref, and for `check_run` that
# is the repository's DEFAULT BRANCH — not the pull request. Without this block
# the workspace holds `main`, so an agent asked to fix a PR file would patch the
# wrong revision: `push-to-pull-request-branch` pushes the *file contents* of
# the agent's tree onto the PR branch, so a fix authored against `main` would
# silently revert anything else that changed in that file. `pr-checkout-ref` is
# the PR's head branch for same-repo PRs (attached, so gh-aw can derive the push
# target from `git rev-parse --abbrev-ref HEAD`) and `refs/pull/<n>/head` for
# forks, which gh-aw refuses to push to anyway.
#
# Checking out the PR head does NOT execute PR code: this workflow never builds,
# and the agent's bash allowlist contains no interpreters, package managers or
# build tools — the tree is read and edited as text only.
#
# The PR-head checkout is intentionally shallow (`actions/checkout`'s default
# depth of 1): gh-aw bundles only the commits the agent creates on top of it, so
# no history is needed. Step 6b's loop guard therefore reads the PR's commit
# list through the GitHub tools rather than `git log`, which cannot see the
# branch's history here.
#
# It does, however, put PR-controlled `.github/`, `.agents/` and `AGENTS.md`
# content in the workspace, and the agent reads its playbook from there. gh-aw's
# own base-branch restore (`restore_base_github_folders.sh`) is gated on its
# built-in PR-checkout step, which never fires for `check_run` (that event
# carries no `pull_request` payload), so the second checkout below fetches the
# same agent config from the base branch and a `pre-agent-steps` step copies it
# over the PR's copy before the agent starts. Without that, a fork PR could
# rewrite the analyst's own instructions — `roles: all` lets every fork reach
# this workflow.
checkout:
  - ref: ${{ needs.fetch-binlog.outputs.pr-checkout-ref }}
  - ref: ${{ github.event.repository.default_branch }}
    path: .gh-aw-base-config
    fetch-depth: 1
    # Cone mode (the `actions/checkout` default) materializes every top-level
    # file in addition to the listed directories, which is how the base
    # branch's `AGENTS.md`/`CLAUDE.md`/`GEMINI.md`/`.mcp.json` arrive. The
    # restore step below does not rely on that: it consults the base tree
    # directly, so a sparse-checkout change cannot turn "restore" into "delete".
    sparse-checkout: |
      .github
      .agents

pre-agent-steps:
  - name: Restore agent config from the base branch
    shell: bash
    env:
      BASE_BRANCH: ${{ github.event.repository.default_branch }}
    run: |
      set -euo pipefail
      BASE=".gh-aw-base-config"
      # Mirror gh-aw's restore_base_github_folders.sh: for each agent-config
      # path, prefer the base-branch copy, and delete anything the PR added that
      # the base branch does not have. Root instruction files are handled the
      # same way because the engine auto-loads them.
      for FOLDER in .github .agents; do
        rm -rf "${FOLDER}"
        if [ -d "${BASE}/${FOLDER}" ]; then
          cp -r "${BASE}/${FOLDER}" "${FOLDER}"
          echo "Restored ${FOLDER} from ${BASE_BRANCH}"
        else
          echo "Base branch has no ${FOLDER}; removed the PR's copy"
        fi
      done
      BASE_ROOT_FILES=$(git -C "${BASE}" ls-tree --name-only HEAD)
      for FILE in AGENTS.md CLAUDE.md GEMINI.md .mcp.json; do
        rm -f "${FILE}"
        if [ -f "${BASE}/${FILE}" ]; then
          cp "${BASE}/${FILE}" "${FILE}"
          echo "Restored ${FILE} from ${BASE_BRANCH}"
        elif printf '%s\n' "${BASE_ROOT_FILES}" | grep -qx -- "${FILE}"; then
          # On the base branch but not materialized by the sparse checkout.
          git -C "${BASE}" show "HEAD:${FILE}" > "${FILE}"
          echo "Restored ${FILE} from ${BASE_BRANCH} (via git show)"
        else
          # Genuinely absent on the base branch, so the PR added it: removing
          # it is the intended outcome.
          echo "Base branch has no ${FILE}; removed the PR's copy"
        fi
      done
      rm -rf "${BASE}"
      # gh-aw restores inline sub-agents/skills from the activation artifact in
      # the steps just above; the wipe above would drop them, so replay those
      # restores. They no-op when the workflow defines none (this one does not),
      # and are skipped entirely if a compiler upgrade renames the scripts.
      for SCRIPT in restore_inline_sub_agents.sh restore_inline_skills.sh; do
        if [ -f "${RUNNER_TEMP}/gh-aw/actions/${SCRIPT}" ]; then
          GH_AW_SUB_AGENT_DIR=".github/agents" \
          GH_AW_SUB_AGENT_EXT=".agent.md" \
          GH_AW_SKILL_DIR=".github/skills" \
            bash "${RUNNER_TEMP}/gh-aw/actions/${SCRIPT}"
        fi
      done
      # The restored files differ from the PR head, so leave them staged-free and
      # let git see them as modifications: the agent only ever commits the single
      # source file it fixes, and gh-aw builds its patch from commits, never from
      # the dirty worktree. Fail loudly if that assumption ever breaks.
      git -c core.fileMode=false status --porcelain -- .github .agents AGENTS.md | head -n 20 || true

# `tools:` and `safe-outputs:` are otherwise shared with the slash-command
# workflow via `shared/build-failure-analysis-shared.md`. These additions are
# deliberately declared here, in the automatic workflow only: the command
# workflow stays comment-only and must not be able to push code.
tools:
  # `edit` + the `git add`/`git commit` pair exist only for the
  # `push-to-pull-request-branch` escape hatch (see below and the analyst
  # agent's "Step 6b"). Everything else stays read-only.
  #
  # NOTE: enabling `push-to-pull-request-branch` makes the **compiler** widen the
  # generated shell allowlist on its own with `git branch/checkout/merge/rm/
  # switch` — see the `--allow-tool` list in the compiled lock. Those come from
  # gh-aw, not from the list below, and cannot be removed from here. What matters
  # is that `git push` is not among them: the agent can never write to the
  # remote. The push happens in the `safe_outputs` job, from a bundle of the
  # agent's local commits, filtered by `allowed-files`. The analyst playbook
  # (Step 6b) forbids the agent from using the injected branch/history commands.
  edit:
  bash:
    - "git status:*"
    - "git diff:*"
    - "git log:*"
    - "git rev-parse:*"
    - "git add:*"
    - "git commit:*"

safe-outputs:
  # Escape hatch for the case inline suggestions structurally cannot cover: a
  # `suggestion` block is only accepted by GitHub on lines that are part of the
  # PR diff, so when the root-cause fix lives in a file the PR never touched
  # (the classic dependency-flow break — a flowed package changes an API and the
  # unchanged call sites stop compiling) the analysis could previously only
  # describe the fix and ask a maintainer to commit it by hand. This lets the
  # agent append the fix commit to the PR branch instead.
  #
  # Guardrails, in order of how much they actually protect:
  #   * gh-aw's handler refuses fork PRs outright (the workflow token has no
  #     write access to a fork), so this only ever reaches same-repo branches —
  #     i.e. dependency-flow (`darc-*`) branches and branches from people who
  #     already have write access. It is append-only; force-push is impossible.
  #   * `allowed-files` is an exclusive allowlist: anything outside `src/` and
  #     `test/` is refused by the handler regardless of what the agent produced.
  #     Build infrastructure (`eng/`, `global.json`, `.github/`, `NuGet.config`)
  #     is therefore out of reach, and `protected-files` stays at its default
  #     `blocked` policy on top of that.
  #   * `max: 1` plus the agent's own marker check (see the analyst agent's
  #     "Step 6b") bounds the fail → push → rebuild → fail loop. The push is
  #     made with GITHUB_TOKEN, which does not re-trigger GitHub Actions, but
  #     Azure DevOps' GitHub app *does* rebuild — so the loop guard is the agent
  #     refusing to push twice, not GitHub declining to re-run us.
  # `fallback-as-pull-request: false` keeps a diverged branch from silently
  # turning into a surprise PR (and drops the extra `pull-requests: write`
  # requirement); `check-branch-protection: false` avoids needing
  # `administration: read` just for a pre-flight the platform enforces anyway.
  push-to-pull-request-branch:
    max: 1
    target: "*"
    allowed-files:
      - "src/**"
      - "test/**"
    if-no-changes: "ignore"
    ignore-missing-branch-failure: true
    fallback-as-pull-request: false
    check-branch-protection: false


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
  - shared/build-failure-analysis-fetch.md
  - shared/build-failure-analysis-shared.md

environment: copilot-pat-pool

engine:
  id: copilot
  env:
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}


# Custom job that reuses the binlogs from the failed Azure DevOps build instead
# of rebuilding. It resolves the ADO build id (from the check details URL or
# the dispatch input), verifies the PR targets an in-scope base branch,
# downloads every `<Leg>_Logs_Attempt<N>` artifact, extracts each leg's
# `*.binlog`, and uploads them for the agent job.
# Steps that run in the agent job. Because the top-level `if:` gates activation
# on `needs.fetch-binlog.outputs.binlog-found == 'true'`, these only run once
# binlogs have been retrieved from the failed Azure DevOps build.
steps:
  - name: Download analysis artifact
    uses: actions/download-artifact@v8.0.1
    with:
      name: build-failure-analysis-data
      path: /tmp/binlogs

  - name: Export agent context
    shell: bash
    env:
      GH_AW_BINLOG_FOUND_VALUE: ${{ needs.fetch-binlog.outputs.binlog-found }}
      GH_AW_PR_NUMBER_VALUE: ${{ needs.fetch-binlog.outputs.pr-number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-head-sha }}
      GH_AW_PR_MERGE_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-merge-sha }}
      GH_AW_ADO_BUILD_URL_VALUE: ${{ needs.fetch-binlog.outputs.ado-build-url }}
      GH_AW_MISSING_LEGS_VALUE: ${{ needs.fetch-binlog.outputs.missing-legs }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      # The binlogs are mounted into the binlog-mcp container at
      # `/data/binlogs`. Build the list of in-container binlog paths (one per
      # build leg) that the agent should query. `GH_AW_BINLOG_PATH` is the
      # first entry for tools/prompts that expect a single path.
      BINLOG_DIR="/data/binlogs"
      LIST=""
      if [ "${GH_AW_BINLOG_FOUND_VALUE:-false}" = "true" ] && [ -d /tmp/binlogs ]; then
        for f in /tmp/binlogs/*.binlog; do
          [ -f "$f" ] || continue
          LIST="${LIST}${BINLOG_DIR}/$(basename "$f")"$'\n'
        done
      fi
      FIRST=$(printf '%s' "$LIST" | head -1)
      {
        echo "GH_AW_BUILD_OUTCOME=failure"
        echo "GH_AW_BINLOG_DIR=${BINLOG_DIR}"
        echo "GH_AW_BINLOG_PATH=${FIRST}"
        echo "GH_AW_BINLOG_HOST_PATH=${GH_AW_ADO_BUILD_URL_VALUE}"
        echo "GH_AW_PR_NUMBER=${GH_AW_PR_NUMBER_VALUE}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_PR_MERGE_SHA=${GH_AW_PR_MERGE_SHA_VALUE}"
        echo "GH_AW_MISSING_LEGS=${GH_AW_MISSING_LEGS_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
        echo "GH_AW_BINLOG_LIST<<GH_AW_EOF"
        printf '%s' "$LIST"
        echo "GH_AW_EOF"
      } >> "$GITHUB_ENV"


---

<!--
  Body provided by shared/build-failure-analysis-shared.md.

  All build-failure analysis expertise (binlog parsing, error grouping,
  suggestion authoring) lives in the reusable agent at
  .github/agents/build-failure-analyst.agent.md.
-->
