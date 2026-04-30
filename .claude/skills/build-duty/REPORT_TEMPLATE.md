# Report Template

Use this structure for the final build duty triage report:

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

## ⏳ Waiting for CI ({count})

| # | Title | Repo | Target | Age | CI Status |
|---|-------|------|--------|-----|-----------|
| [#2345](url) | Source code updates | dotnet/sdk | main | 1d | 15/17 passed, 2 pending |

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
| Waiting for CI | X |
| Branch Lockdown | X |
| Changes Requested | X |
| Failing/Blocked | X |
| Stale (>7d) | X |
| **Total** | **X** |

---

## 📋 Recommended Actions

1. **Merge:** {count} PRs are ready — review and merge
2. **Wait:** {count} PRs have CI still running — check back later
3. **Retry:** {count} PRs have likely-flaky failures — rerun failed jobs in AzDO (not `/azp run`, which does a full rerun)
4. **Investigate:** {count} PRs have unclassified failures — run CI analysis
5. **Stale:** {count} PRs are >7 days old — escalate if stuck
```
