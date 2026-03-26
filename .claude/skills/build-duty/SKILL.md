---
name: build-duty
description: Generate a build duty PR triage report across dotnet/sdk, dotnet/installer, dotnet/templating, and dotnet/dotnet repositories. Use when asked about "build duty", "triage PRs", "build duty report", "merge queue", "dependency PRs", "what PRs need merging", "build duty status", or "check open PRs for build duty".
user-invocable: true
disable-model-invocation: true
---

# Build Duty PR Triage

> **NEVER** use `gh pr merge` or approve PRs automatically. Merging and approval are human-only actions. This skill only generates reports.

## Step 1: Run the Script

```powershell
./.claude/skills/build-duty/scripts/Get-BuildDutyReport.ps1
```

Optionally filter to specific repos:

```powershell
./.claude/skills/build-duty/scripts/Get-BuildDutyReport.ps1 -IncludeRepo sdk,installer
```

The script queries all 4 repos for open PRs from monitored authors (dotnet-maestro[bot], github-actions[bot], vseanreesermsft, dotnet-bot), classifies each PR, and outputs tables grouped by category with human-readable recommendations.

## Step 2: Investigate Failing PRs

For each PR in the "Failing / Blocked" category, use the **ci-analysis** skill to diagnose failures. Determine whether failures are known issues (retry), correlated with PR changes (need fixing), or infrastructure-related (transient).

## Step 3: Generate the Final Report

Synthesize the script output and CI analysis into a markdown report. Read `REPORT_TEMPLATE.md` in this skill directory for the report structure.

## Interpreting Common Patterns

### Cascading Merge Failures
When an inter-branch merge PR (e.g., `release/10.0.1xx => release/10.0.2xx`) is blocked, downstream merges in the chain will also be blocked. Look for the **root cause** at the start of the merge chain.

### Codeflow PRs
PRs titled `[branch] Source code updates from dotnet/dotnet` are automated codeflow from the VMR. These often fail due to package version mismatches (NU1603), breaking changes flowing from other repos, or merge conflicts with concurrent changes.

### Merge Chains
The dotnet/sdk repo has a merge flow: `release/9.0.3xx -> 10.0.1xx -> 10.0.2xx -> 10.0.3xx -> main`. A failure early in the chain blocks everything downstream.

### Branch Lockdown
During servicing windows, release branches are locked. PRs targeting locked branches get the `Branch Lockdown` label automatically.

## Tips

1. Start with "Ready to Merge" -- those are quick wins.
2. For failing PRs, check if multiple PRs share the same failure -- fixing one may unblock others.
3. Merge chain PRs should be merged in order from oldest branch to newest.
4. Stale PRs (>7 days) often indicate a systemic issue.