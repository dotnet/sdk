---
emoji: "👾"
name: Issue Monster Assigner
description: Assigns one issue to Copilot using an orchestrator-selected base branch
on:
  # `roles: all` bypasses gh-aw's pre_activation membership check. The assigner is only
  # ever dispatched programmatically by the orchestrator, so the workflow_dispatch actor
  # is github-actions[bot] (repo permission `none`); without this, that actor is rejected
  # and every agentic job is skipped. GitHub still enforces its own actions:write
  # requirement to create the dispatch.
  roles: all
  # A no-op activation step forces gh-aw to emit a `pre_activation` job, which the
  # pat_pool import requires (`needs: [pre_activation]`). `roles: all` alone leaves no
  # activation logic, so gh-aw would otherwise omit pre_activation and compilation fails
  # with "job 'pat_pool' depends on non-existent job 'pre_activation'".
  # See shared/pat_pool.README.md "Known Issues".
  steps:
    - name: Force pre_activation job for pat_pool dependency
      run: echo "pre_activation placeholder for PAT pool wiring"
  workflow_dispatch:
    inputs:
      issue_number:
        description: Issue number to assign to Copilot.
        required: true
        type: number
      base_branch:
        description: Base branch for the Copilot pull request.
        required: true
        type: string

permissions:
  contents: read
  copilot-requests: write

sandbox:
  agent:
    sudo: false

# Pin the agent to an explicitly priced model. Under the PAT pool the agent runs
# behind the AWF api-proxy, whose AI-credits pricing check rejects the implicit
# `auto` model ("Model \"auto\" has no AI credits pricing") for pool PATs on
# credits-based billing, skipping the assignment. A named model resolves this.
model: gpt-5.6-luna

# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# Run agentic jobs in an isolated `copilot-pat-pool` environment.
#
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
engine:
  id: copilot
  env:
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}

environment: copilot-pat-pool

imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool

timeout-minutes: 10

tools:
  github: false

safe-outputs:
  assign-to-agent:
    max: 1
    target: "*"
    model: gpt-5.6-sol
    pull-request-repo: "${{ github.repository }}"
    # Copilot branches from this ref and targets it when opening the pull request.
    base-branch: "${{ inputs.base_branch }}"
    # Copilot assignment requires a user token rather than an installation token.
    # The alternative is to startup a copilot session ourselves rather than use assign_to_agent
    # Tentatively this now 'works' without a token but it just adds a UI dialogue to accept the request on the issue which defeats the point of auto-assignment
    github-token: "${{ secrets.ISSUE_MONSTER_ASSIGNMENT_TOKEN }}" # must have issues: write, pr: write, contents: write, actions: write, metdata: read https://github.github.com/gh-aw/reference/copilot-cloud-agent/#using-a-personal-access-token-pat
    allowed: [copilot]
    ignore-if-error: true
  # Pin the threat-detection engine to a capable model. The default detection
  # alias resolves to a small model that false-positively flags gh-aw's own
  # anti-injection preamble as prompt_injection, which under the warn policy
  # aborts the non-reviewable assign_to_agent output and blocks assignment.
  threat-detection:
    engine:
      id: copilot
      model: claude-sonnet-4.6
  noop:
    report-as-issue: false
---

# Issue Monster Assigner

Assign issue `${{ inputs.issue_number }}` to the Copilot coding agent; the workflow already targets `${{ inputs.base_branch }}`, so do not pass `base_branch`, `integrity`, or any other metadata to the tool.

Call exactly once:

```
safeoutputs/assign_to_agent(issue_number=${{ inputs.issue_number }}, agent="copilot")
```

Only assign the issue. Do not read the issue, add comments, or perform any other action.
