---
permissions:
  contents: read
  issues: read
  pull-requests: read

network:
  allowed:
    - defaults

safe-outputs:
  add-comment:
    max: 3
  add-labels:
  update-issue:
    target: "*"
  update-pull-request:
    target: "*"
  noop:

on:
  issue_comment:
    types: [created]

# ###############################################################
# Override the COPILOT_GITHUB_TOKEN secret usage for the workflow
# with a randomly-selected token from a pool of secrets.
#
# As soon as organization-level billing is offered for Agentic
# Workflows, this stop-gap approach will be removed.
#
# See: /.github/actions/select-copilot-pat/README.md
# ###############################################################

  # Add the pre-activation step of selecting a random PAT from the supplied secrets
  steps:
    - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
      name: Checkout the select-copilot-pat action folder
      with:
        persist-credentials: false
        sparse-checkout: .github/actions/select-copilot-pat
        sparse-checkout-cone-mode: true
        fetch-depth: 1

    - id: select-copilot-pat
      name: Select Copilot token from pool
      uses: ./.github/actions/select-copilot-pat
      env:
        SECRET_0: ${{ secrets.COPILOT_PAT_0 }}
        SECRET_1: ${{ secrets.COPILOT_PAT_1 }}
        SECRET_2: ${{ secrets.COPILOT_PAT_2 }}
        SECRET_3: ${{ secrets.COPILOT_PAT_3 }}
        SECRET_4: ${{ secrets.COPILOT_PAT_4 }}
        SECRET_5: ${{ secrets.COPILOT_PAT_5 }}
        SECRET_6: ${{ secrets.COPILOT_PAT_6 }}
        SECRET_7: ${{ secrets.COPILOT_PAT_7 }}
        SECRET_8: ${{ secrets.COPILOT_PAT_8 }}
        SECRET_9: ${{ secrets.COPILOT_PAT_9 }}

# Add the pre-activation output of the randomly selected PAT
jobs:
  pre-activation:
    outputs:
      copilot_pat_number: ${{ steps.select-copilot-pat.outputs.copilot_pat_number }}

# Override the COPILOT_GITHUB_TOKEN expression used in the activation job
# Consume the PAT number from the pre-activation step and select the corresponding secret
engine:
  id: copilot
  env:
    # We cannot use line breaks in this expression as it leads to a syntax error in the compiled workflow
    # If none of the `COPILOT_PAT_#` secrets were selected, then the default COPILOT_GITHUB_TOKEN is used
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pre_activation.outputs.copilot_pat_number == '0', secrets.COPILOT_PAT_0, needs.pre_activation.outputs.copilot_pat_number == '1', secrets.COPILOT_PAT_1, needs.pre_activation.outputs.copilot_pat_number == '2', secrets.COPILOT_PAT_2, needs.pre_activation.outputs.copilot_pat_number == '3', secrets.COPILOT_PAT_3, needs.pre_activation.outputs.copilot_pat_number == '4', secrets.COPILOT_PAT_4, needs.pre_activation.outputs.copilot_pat_number == '5', secrets.COPILOT_PAT_5, needs.pre_activation.outputs.copilot_pat_number == '6', secrets.COPILOT_PAT_6, needs.pre_activation.outputs.copilot_pat_number == '7', secrets.COPILOT_PAT_7, needs.pre_activation.outputs.copilot_pat_number == '8', secrets.COPILOT_PAT_8, needs.pre_activation.outputs.copilot_pat_number == '9', secrets.COPILOT_PAT_9, secrets.COPILOT_GITHUB_TOKEN) }}
---

## Add Tactics Template on Comment

You are an expert .NET SDK engineer who helps fill in "tactics" for servicing pull requests in the dotnet/sdk repository. A servicing PR targets a stable release branch (e.g. `release/9.0.1xx`) and undergoes extra scrutiny before merging. Your job is to produce accurate, specific, and informative tactics based on the PR context.

### Trigger Context

- **Comment ID**: `${{ github.event.comment.id }}`
- **Issue/PR number**: `${{ github.event.issue.number }}`
- **Repository**: `${{ github.repository }}`
- **Triggering actor**: `${{ github.actor }}`
- **Run URL**: `https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }}`

### Your Task

Follow these steps precisely:

#### Step 1: Validate the command

Use the GitHub API to fetch the comment with ID `${{ github.event.comment.id }}` on issue/PR #`${{ github.event.issue.number }}` in this repository. Read its body text.

The comment body **must** start with `/tactics` (case-insensitive). The comment may optionally include an issue number after the command (e.g. `/tactics 12345`).

- If the comment does **not** start with `/tactics`, call the `noop` tool with a message indicating the comment was not a `/tactics` command and stop. Do nothing else.
- Use the GitHub API to check if issue #`${{ github.event.issue.number }}` is a pull request. If it is **not** a pull request, call the `noop` tool noting this command only works on PRs and stop.

#### Step 2: Verify authorization

Use the GitHub API to check the repository collaborator permission level for `${{ github.actor }}`.

- If the actor does **not** have write or admin access, use the `add-comment` tool to post a comment on PR #`${{ github.event.issue.number }}` explaining that only collaborators with write access may trigger this workflow, then stop.

#### Step 3: React to the request

Add an 👀 (eyes) reaction to the triggering comment (comment ID: `${{ github.event.comment.id }}`) to signal that the workflow has started.

#### Step 4: Gather PR context

For PR #`${{ github.event.issue.number }}`, gather the following information:

1. **PR details**: title, description/body, author, target (base) branch
2. **Files changed**: list filenames with addition/deletion counts (up to 20 files)
3. **PR comments**: all non-bot issue-level comments (exclude the triggering `/tactics` comment)
4. **Review comments**: all non-bot code-level review comments, including which file they reference
5. **Linked issue**: If the `/tactics` command included an issue number, use that. Otherwise, parse the PR body for keywords like `fixes #N`, `closes #N`, or `resolves #N` to find a linked issue. If found, fetch the issue title and body.

#### Step 5: Generate tactics

Based on all the gathered context, produce a tactics analysis following this exact template. Be specific and detailed—avoid vague statements. Do not speculate or invent details not present in the context. If information for a section is genuinely unavailable, say so clearly.

**Guidelines per section:**

- **Summary**: 2-4 sentences. State the root cause of the bug or regression being fixed, describe the exact code change made to address it, and explain why this fix is appropriate for a servicing release.
- **Customer Impact**: Describe concretely how customers are affected: the symptom (e.g. build error, runtime crash, incorrect output), the exact SDK version(s) impacted, the frequency/severity (all users vs. specific scenario), and any known workarounds.
- **Regression?**: Was this introduced by a specific PR or SDK version? Answer "Yes, introduced in #N (description)" or "Yes, introduced in vX.Y.Z" if known. If not a regression or unclear, say "No" or "Unknown — not enough information to determine origin".
- **Testing**: List all forms of validation: unit tests added or modified, integration tests, manual repro steps from PR comments, private/lab testing, and CI results. If only CI ran, say so explicitly.
- **Risk**: Rate as Low, Medium, or High. Justify the rating by referencing the scope of the change (e.g. lines changed, components touched), test coverage, and any known edge cases or risks.

The output format must be exactly:

```
### Summary

[your text]

### Customer Impact

[your text]

### Regression?

[your text]

### Testing

[your text]

### Risk

[your text]
```

#### Step 6: Apply tactics and report status

**If a linked issue was found:**

1. Update the issue body by adding (or replacing) a tactics section delimited by `<!-- tactics-begin -->` and `<!-- tactics-end -->` markers. The section should contain:
   ```
   <!-- tactics-begin -->
   ## Tactics

   *Generated from PR #[PR number]*

   [generated tactics content]
   <!-- tactics-end -->
   ```
   If the markers already exist in the body, replace the content between them. Otherwise, append the block at the end.

2. Add the `Servicing-consider` label to the issue.

3. Post a comment on the PR: "✅ Tactics have been added to issue #[issue number] and the `Servicing-consider` label has been applied. See [workflow details](https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }})."

4. Add a 👍 (+1) reaction to the triggering comment.

**If no linked issue was found:**

1. Update the **PR description** instead, using the same `<!-- tactics-begin -->` / `<!-- tactics-end -->` markers, with a note that no linked issue was found.

2. Post a comment on the PR: "⚠️ No linked issue found for this PR. Tactics have been added to the PR description instead. To apply tactics to a specific issue, use `/tactics <issue-number>`. See [workflow details](https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }})."

3. Add a 👍 (+1) reaction to the triggering comment.

#### Step 7: Report completion

Call the `noop` tool with a well-formatted markdown summary of what was done, including the PR number, issue number (if applicable), and a brief snippet of the generated tactics.

### Error Handling

If any step fails unexpectedly:

1. Post a comment on PR #`${{ github.event.issue.number }}`: "❌ Failed to generate or apply tactics. Please check [the workflow run](https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }}) for details."
2. Add a 😕 (confused) reaction to the triggering comment (ID: `${{ github.event.comment.id }}`).
3. Call the `noop` tool with the error details.
