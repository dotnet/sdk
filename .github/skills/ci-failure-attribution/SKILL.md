---
name: ci-failure-attribution
description: >-
  Determine whether CI failures were introduced by a PR or newly tested commits,
  were pre-existing, or stem from external infrastructure. Use when asked whether
  red CI is caused by changes or flakiness, whether a rolling/batched build
  regressed, or why a triggered/manual/scheduled build started failing.
  Investigate adaptively with available MCP tools and focused evidence requests;
  give per-failure attribution, High/Medium/Low confidence, and explicit unknowns.
  Read-only; does not rerun, fix, post, or approve a merge.
license: MIT
compatibility: >-
  Live retrieval requires authenticated read access to the relevant CI and source
  services; supplied evidence can be assessed without it. Prefer Helix MCP for
  Azure DevOps/Helix; use available provider tools or CLI/API calls for other
  services and capability gaps.
---

# CI failure attribution

Evaluate what caused a failure separately from how often it fails. A change can
introduce a flaky failure, and an existing failure can fail every time.

## Investigation approach

Fetch only the data needed to test likely causes for each failure. Use Helix MCP
for Azure DevOps and Helix data. If an MCP tool lacks a needed field or filter,
use a read-only CLI or API call.

Keep the investigation read-only. Do not rerun CI, reproduce failures locally,
edit files, or post to GitHub, even if another skill includes those steps.

## Establish the scope

If the CI provider, Azure DevOps project, repository, or target branch is
unclear, ask the user. Do not assume public CI or `main`.

Check the build metadata to confirm the provider, organization, project,
repository, pipeline, tested commit, and build attempt. For historical PR
builds, get the tested head, merge, and target commits from the build record. Do
not use the PR's current branch state. Read PR comments and reviews for context,
and verify their claims against build data.

List the relevant checks and any failed or canceled jobs and tests. Record which
checks you evaluated and which checks were skipped, pending, or not evaluated.

## Investigate one mechanism at a time

For each failure:

- Find the failing task, test case, assertion, or Helix work item. Read enough
  surrounding log lines to find the first real error before any cleanup or
  missing-artifact errors.
- If the summary shows only a generic work-item error or timeout, fetch the
  child test results, TRX file, console log, binlog, or crash dump.
- Check the changed code, assets, dependencies, or pipeline files at the tested
  commit. Trace how the change can cause the failure.
- Compare the failure with matching runs to see if it existed before the change.
  Confirm that the same test or job actually ran in the comparison build.
- Check Build Analysis reports and linked issues. Confirm that the error text,
  branch, configuration, and dates match.

See [evidence-retrieval.md](references/evidence-retrieval.md) for tool choices
and query pitfalls.

## Choose comparisons and complete the investigation

See [attribution-rubric.md](references/attribution-rubric.md) for rules on
comparing runs, [sample sizes](references/attribution-rubric.md#evidence-sufficiency-and-sampling),
and assigning verdicts:

- **Pull requests:** compare against a target-branch build at the tested base
  commit.
- **Standalone and rolling builds:** compare against the previous matching build
  and the last passing build. Check git history to confirm commit order.
- **Same-SHA scheduled, manual, or repeated runs:** check for changes in
  external resources, pipeline parameters, machine state, or existing code bugs.

Report authentication errors, missing artifacts, pagination limits, and
truncated logs as coverage gaps. Do not recommend a retry or mark all failures
as unrelated while any relevant failure is still unexplained.

When needed, use `ci-analysis`, `helix-investigation`, `pipeline-investigation`,
`binlog-failure-analysis`, `ci-crash-dump`, `source-build-investigation`, or
`known-issue-history` (if available) to inspect existing artifacts. Keep private
logs, code, and IDs inside authorized services. Do not send private data to
public search tools.

## Report

Start the report with the overall summary, the PR or build link, the comparison
commits, and any skipped, pending, or unevaluated checks.

| Failure | Attribution | Behavior | Confidence |
| --- | --- | --- | --- |
| Specific mechanism | PR-related / New-commit-related / Pre-existing / External infrastructure / Inconclusive | Intermittent / Deterministic / Unknown | High / Medium / Low |

For each row:

- Link the relevant logs, artifacts, and source files.
- Explain how the cause produced the failure.
- List the comparison builds you checked, including runs that passed or failed
  differently.
- Note any missing data and the simplest next check.

Group failures into one row only when they share the same root cause, and list
each affected job or test.
