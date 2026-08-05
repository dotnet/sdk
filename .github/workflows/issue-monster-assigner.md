---
emoji: "👾"
name: Issue Monster Assigner
description: Assigns one issue to Copilot using an orchestrator-selected base branch
on:
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

engine:
  id: copilot

environment: issue-monster

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
