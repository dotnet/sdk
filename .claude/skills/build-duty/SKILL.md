---
name: build-duty
description: Generate a build duty PR triage report across dotnet/sdk, dotnet/installer, dotnet/templating, and dotnet/dotnet repositories. Use when asked about "build duty", "triage PRs", "build duty report", "merge queue", "dependency PRs", "what PRs need merging", "build duty status", or "check open PRs for build duty".
---

# Build Duty PR Triage

Monitor and classify pull requests across .NET SDK repositories for build duty engineers. Produces a structured triage report with PR status, age, classification, and failure details.

> 🚨 **NEVER** use `gh pr merge` or approve PRs automatically. Merging and approval are human-only actions. This skill only generates reports.

**Workflow**: Run the script (Step 1) → Read the output + JSON summary (Step 2) → Investigate failing PRs with ci-analysis (Step 3) → Synthesize the final report (Step 4).

## When to Use This Skill

Use this skill when:
- Checking build duty status ("what PRs need merging?", "build duty report")
- Triaging automated PRs across dotnet repos
- Generating a daily build duty triage report
- Checking if dependency update or codeflow PRs are ready to merge
- Asked "what's the merge queue look like?" or "any stuck PRs?"

## Quick Start

```powershell
# Full report across all 4 repos
./.claude/skills/build-duty/scripts/Get-BuildDutyReport.ps1

# Report for specific repos only
./.claude/skills/build-duty/scripts/Get-BuildDutyReport.ps1 -IncludeRepo sdk,installer

# JSON-only output
./.claude/skills/build-duty/scripts/Get-BuildDutyReport.ps1 -OutputJson
```

## Key Parameters

| Parameter | Description |
|-----------|-------------|
| `-IncludeRepo` | Filter to specific repos: `sdk`, `installer`, `templating`, `dotnet` (default: all 4) |
| `-DaysStale` | Days after which a PR is flagged stale (default: 7) |
| `-OutputJson` | Emit only JSON (no human-readable tables) |
| `-Verbose` | Show individual `gh` API calls for debugging |

## What the Script Does

1. **Queries 4 repositories** via `gh` CLI for open PRs from monitored authors:
   - `dotnet-maestro[bot]` — Dependency updates and codeflow PRs
   - `github-actions[bot]` — **Only** inter-branch merge PRs (titles containing "Merge branch"); excludes backport PRs
   - `vseanreesermsft` — Release management PRs
   - `dotnet-bot` — Automated bot PRs

2. **Applies special VMR filtering** for `dotnet/dotnet`: Only includes PRs from `dotnet-maestro[bot]` whose titles reference SDK-owned repos (`dotnet/sdk`, `dotnet/templating`, `dotnet/deployment-tools`, `dotnet/source-build-reference-packages`).

3. **Fetches detailed status** for each PR via GitHub GraphQL:
   - `mergeStateStatus` (CLEAN, BLOCKED, UNSTABLE)
   - `statusCheckRollup` (SUCCESS, FAILURE, PENDING)
   - `reviewDecision` (APPROVED, CHANGES_REQUESTED, REVIEW_REQUIRED)
   - `mergeable` (MERGEABLE, CONFLICTING, UNKNOWN)
   - `changedFiles` count (0 = empty PR with no actual changes)
   - Individual check run results (name, conclusion, status)
   - Labels, age, draft status

4. **Classifies each PR** into categories (see below).

5. **Generates a recommendation** for each PR (see Recommendation Codes below).

6. **Outputs** both human-readable tables and a `[BUILD_DUTY_SUMMARY]` JSON block.

## PR Classification Categories

### ✅ Ready to Merge
PRs where:
- `mergeStateStatus` is `CLEAN` or `UNSTABLE` (non-required checks failing is still mergeable)
- No blocking labels (`DO NOT MERGE`, `Branch Lockdown`)
- Not a draft PR

**Action:** These can be merged immediately by the build duty engineer.

### 🔒 Branch Lockdown
PRs where:
- Has `Branch Lockdown` label
- Expected during servicing windows

**Action:** Queue for merge when lockdown lifts. No investigation needed.

### ⚠️ Changes Requested
PRs where:
- `reviewDecision` is `CHANGES_REQUESTED`
- Reviewer name is included in the report

**Action:** Requires action from PR author or upstream team. Note which reviewer requested changes.

### ❌ Failing / Blocked
PRs where:
- `mergeStateStatus` is `BLOCKED`
- Or `statusCheckRollup` is `FAILURE` or `PENDING`
- Or has `DO NOT MERGE` label

**Action:** Investigate failures. Use the ci-analysis skill for detailed failure information.

### ⏳ Stale (cross-cutting flag)
Any PR older than 7 days (configurable) that does NOT have `Branch Lockdown` label. These may be stuck or forgotten and need attention.

## Recommendation Codes

Each PR gets an automatic recommendation based on its status. The agent should act on these:

| Code | Meaning | Agent Action |
|------|---------|-------------|
| `MERGE_EMPTY_CODEFLOW` | PR from dotnet-bot with 0 changed files — inter-branch codeflow with merge commits | Recommend merging. Completing these PRs reduces churn in the next codeflow PR. |
| `CLOSE_EMPTY_PR` | PR has 0 changed files — no actual code changes after merge/sync | Recommend closing or merging trivially. Provide `gh pr close` command. |
| `FIX_DARC_CONFLICT` | PR from maestro with 0 changed files and a darc merge conflict comment | Flag for manual resolution using `darc vmr resolve-conflict`. Direct the engineer to check the PR comments for step-by-step instructions. |
| `FIX_MERGE_CONFLICTS` | Merge PR has unresolved conflicts | Flag for manual conflict resolution. Cannot be auto-fixed. |
| `RETRY_SINGLE_LEG` | Only 1 CI leg failed out of many (likely flaky, common in templating) | Comment `/azp run` on the PR to trigger a retry. |
| `MERGE` | PR is ready to merge | List as quick win. Do NOT auto-merge — human action only. |
| `WAIT_FOR_LOCKDOWN` | Branch is locked for servicing | No action needed; queue for when lockdown lifts. |
| `ADDRESS_REVIEW` | Changes were requested by a reviewer | Note the reviewer; requires upstream action. |
| `NEEDS_REVIEW` | CI is passing but PR needs review approval | List as needing review. Common for VMR PRs. |
| `INVESTIGATE_FAILURE` | Multiple legs failing or complex failure | Run ci-analysis skill to diagnose. |

## Analysis Workflow

### Step 1: Run the Script

```powershell
./.claude/skills/build-duty/scripts/Get-BuildDutyReport.ps1 -Verbose
```

This produces:
- Human-readable tables grouped by category
- `[BUILD_DUTY_SUMMARY]` JSON block with all PR data

### Step 2: Read the Results

Parse the `[BUILD_DUTY_SUMMARY]` JSON. Key fields:
- `counts` — How many PRs in each category
- `prs.ready` — PRs ready to merge (list these first)
- `prs.blocked` — PRs that need investigation
- `prs.changesRequested` — PRs waiting on reviewers
- `prs.lockdown` — PRs in branch lockdown
- `stalePrs` — PRs flagged as stale

### Step 3: Investigate Failing PRs

For each PR in the `blocked` category, run the ci-analysis skill to get detailed failure information:

```powershell
# For dotnet/sdk PRs
./.claude/skills/ci-analysis/scripts/Get-CIStatus.ps1 -PRNumber <number> -ShowLogs

# For other repos
./.claude/skills/ci-analysis/scripts/Get-CIStatus.ps1 -PRNumber <number> -Repository "dotnet/installer" -ShowLogs
```

From the CI analysis, extract:
- Whether failures are known issues (safe to retry)
- Whether failures correlate with PR changes (need fixing)
- Whether failures are infrastructure-related (transient)

### Step 4: Generate the Final Report

Synthesize the script output and CI analysis into a markdown report. Use this structure:

```markdown
# 🔧 Build Duty Triage Report
**Date:** {today's date}
**Repositories:** dotnet/sdk, dotnet/installer, dotnet/templating, dotnet/dotnet

---

## ✅ Ready to Merge ({count})

| # | Title | Repo | Target | Age |
|---|-------|------|--------|-----|
| [#1234](url) | Update dependencies | dotnet/sdk | main | 2d |

---

## 🔒 Branch Lockdown ({count})

| # | Title | Repo | Target | Age |
|---|-------|------|--------|-----|

---

## ⚠️ Changes Requested ({count})

| # | Title | Repo | Target | Age | Reviewer |
|---|-------|------|--------|-----|----------|

---

## ❌ Failing / Blocked ({count})

| # | Title | Repo | Target | Age | Recommendation | Issue |
|---|-------|------|--------|-----|----------------|-------|
| [#5678](url) | Source code updates | dotnet/sdk | main | 3d | 🔍 Investigate | NU1603: package version mismatch |

---

## 🗑️ Empty PRs — Close or Merge ({count})

PRs with 0 file changes. These typically result from merge conflicts that resolved to no-ops.
- **dotnet-bot PRs (codeflow):** Merge these — they contain merge commits and completing them reduces churn in the next codeflow PR.
- **maestro PRs with darc conflict comment:** Run `darc vmr resolve-conflict` per the instructions in the PR comments.
- **Other empty PRs:** Close with `gh pr close`.

| # | Title | Repo | Recommendation | Command |
|---|-------|------|----------------|---------|
| [#1234](url) | [branch] Source code updates | dotnet/dotnet | Close | `gh pr close 1234 --repo dotnet/dotnet --comment 'Closing: no file changes.'` |
| [#2345](url) | Merge branch X => Y | dotnet/sdk | Merge (codeflow) | Ready to merge — reduces churn in next PR |
| [#3456](url) | [branch] Source code updates | dotnet/sdk | Fix darc conflict | See PR comments for `darc vmr resolve-conflict` instructions |

---

## 🔀 Merge Conflict PRs ({count})

PRs with unresolved merge conflicts that need manual resolution.

| # | Title | Repo | Target |
|---|-------|------|--------|

---

## 📊 Summary

| Category | Count |
|----------|-------|
| Ready to Merge | X |
| Branch Lockdown | X |
| Changes Requested | X |
| Failing/Blocked | X |
| Stale (>7d) | X |
| **Total** | **X** |

---

## 📋 Recommended Actions

1. **Merge:** {count} PRs are ready — review and merge
2. **Retry:** {count} PRs have known-issue failures — retry with `/azp run`
3. **Investigate:** {count} PRs have unclassified failures — run CI analysis
4. **Stale:** {count} PRs are >7 days old — escalate if stuck
```

## Interpreting Common Patterns

### Cascading Merge Failures
When an inter-branch merge PR (e.g., `release/10.0.1xx => release/10.0.2xx`) is blocked, downstream merges in the chain will also be blocked. Look for the **root cause** at the start of the merge chain.

### Codeflow PRs
PRs titled `[branch] Source code updates from dotnet/dotnet` are automated codeflow from the VMR. These often fail due to:
- Package version mismatches (NU1603)
- Breaking changes flowing from other repos
- Merge conflicts with concurrent changes

### Merge Chains
The dotnet/sdk repo has a merge flow: `release/9.0.3xx → 10.0.1xx → 10.0.2xx → 10.0.3xx → main`. A failure early in the chain blocks everything downstream.

### Branch Lockdown
During servicing windows (typically part of each month), release branches are locked. PRs targeting locked branches get the `Branch Lockdown` label automatically.

## Labels Reference

| Label | Meaning |
|-------|---------|
| `DO NOT MERGE` | Explicit block — never merge |
| `Branch Lockdown` | Branch closed for servicing |
| `Area-CodeFlow` | Codeflow/sync PR |

## Tips

1. Start with the "Ready to Merge" category — those are quick wins.
2. For failing PRs, check if multiple PRs share the same failure — fixing one may unblock others.
3. Merge chain PRs (`Merge branch X => Y`) should be merged in order from oldest branch to newest.
4. Stale PRs (>7 days) often indicate a systemic issue — check if the same failure pattern repeats.
5. Use `-IncludeRepo sdk` for a quick check of just the primary repo.
