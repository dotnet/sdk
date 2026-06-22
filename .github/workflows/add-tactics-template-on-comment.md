---
permissions:
  contents: read
  issues: read
  pull-requests: read

network: defaults

safe-outputs:
  update-pull-request:
    max: 1
    body: true
    title: false
    footer: false
    target: "${{ github.event.issue.number }}"
  noop:
    report-as-issue: false
if: "github.event.repository.fork == false"
on:
  permissions: {}
  slash_command:
    name: tactics
    events: [pull_request_comment]
  reaction: "+1"
  status-comment: false

# ###############################################################
# Override COPILOT_GITHUB_TOKEN with a random PAT from the pool.
# This stop-gap will be removed when org billing is available.
# See: .github/workflows/shared/pat_pool.README.md for more info.
# ###############################################################

imports:
  - shared/pat_pool.md

engine:
  id: copilot
  env:
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, secrets.COPILOT_GITHUB_TOKEN) }}
---

## Add Tactics Template on Comment

You are an expert .NET SDK engineer who helps fill in "tactics" for servicing pull requests in the dotnet/sdk repository. A servicing PR targets a stable release branch (e.g. `release/9.0.1xx`) and undergoes extra scrutiny before merging. Your job is to produce accurate, specific, and informative tactics based on the PR context.

### Trigger Context

- **Issue/PR number**: `${{ github.event.issue.number }}`
- **Repository**: `${{ github.repository }}`
- **Triggering actor**: `${{ github.actor }}`
- **Run URL**: `https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }}`
- **Sanitized trigger context**: "${{ steps.sanitized.outputs.text }}"

### Your Task

Follow these steps precisely:

#### Step 1: Gather PR context

The `/tactics` command may optionally include an issue number (e.g. `/tactics #12345` or `/tactics 12345`). Parse the sanitized trigger context above to extract any issue number provided after the command.

For PR #`${{ github.event.issue.number }}`, gather the following information:

1. **PR details**: title, description/body, author, target (base) branch
2. **Files changed**: list filenames with addition/deletion counts (up to 20 files)
3. **PR comments**: all non-bot issue-level comments (exclude the triggering `/tactics` comment)
4. **Review comments**: all non-bot code-level review comments, including which file they reference
5. **Linked issue**: If the `/tactics` command included an issue number, use that. Otherwise, parse the PR body for keywords like `fixes #N`, `closes #N`, or `resolves #N` to find a linked issue. If found, fetch the issue title and body.

#### Step 2: Generate tactics

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

#### Step 3: Apply tactics and report status

1. Use the `update_pull_request` tool to update the **PR description** by adding (or replacing) a tactics section delimited by `<!-- tactics-begin -->` and `<!-- tactics-end -->` markers. The section should contain:
   ```
   <!-- tactics-begin -->
   ## Tactics

   [generated tactics content]
   <!-- tactics-end -->
   ```
   If the markers already exist in the body, replace the content between them. Otherwise, append the block at the end.

2. If a linked issue was found and used as input, call the `noop` tool with a markdown summary: "✅ Tactics have been added to the PR description using context from issue #[issue number]." Include the PR number and a brief snippet of the generated tactics summary.

3. If no linked issue was found, call the `noop` tool with a markdown summary: "✅ Tactics have been added to the PR description." Include the PR number and a brief snippet of the generated tactics summary.

### Error Handling

If any step fails unexpectedly, call the `noop` tool with a markdown summary including "❌ Failed to generate or apply tactics" and the error details. The framework will post the failure status automatically.
