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
    pull-request-repo: "${{ github.repository }}"
    # Copilot branches from this ref and targets it when opening the pull request.
    base-branch: "${{ inputs.base_branch }}"
    # Copilot assignment requires a user token, not an installation token such as GITHUB_TOKEN.
    # GitHub documents Metadata: read plus Actions, Contents, Issues, and Pull requests: read/write.
    github-token: "${{ secrets.ISSUE_MONSTER_ASSIGNMENT_TOKEN }}"
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
