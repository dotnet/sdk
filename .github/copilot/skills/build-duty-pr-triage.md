# Build Duty PR Triage Skill

## Description
This skill helps the SDK team with daily build duty responsibilities by monitoring and classifying pull requests across monitored repositories.

## Monitored Repositories
- `dotnet/sdk` (primary)
- `dotnet/installer`
- `dotnet/templating`
- `dotnet/dotnet` (VMR - filtered to SDK-owned PRs only)

## Monitored Authors

Only PRs from these authors are tracked:

| Author | Type | Notes |
|--------|------|-------|
| `app/dotnet-maestro` | Bot | Dependency updates, codeflow PRs |
| `app/github-actions` | Bot | **Only** PRs with "Merge branch" in title. Exclude backport PRs. |
| `vseanreesermsft` | User | Release management |
| `dotnet-bot` | Bot | Automated PRs |

### Special: dotnet/dotnet VMR Filtering

For the dotnet/dotnet repo, only include PRs that:
1. Are authored by `dotnet-maestro[bot]}}`
2. Have a title containing one of these SDK-owned repos:
   - `dotnet/sdk`
   - `dotnet/templating`
   - `dotnet/deployment-tools`
   - `dotnet/source-build-reference-packages`

## PR Data Available via GitHub MCP Tools

| Data Point | Method | Notes |
|------------|--------|-------|
| Basic PR info | `get` | title, author, branch, created_at, labels |
| mergeable_state | `get` | "blocked", "clean", "unstable", etc. |
| Reviews | `get_reviews` | APPROVED, CHANGES_REQUESTED, COMMENTED |
| PR Comments | `get_comments` | May contain failure details from Build Analysis |
| Commit status | `get_status` | Aggregate state only (success/pending/failure) |

**Note:** Individual check run details (like "Build Analysis" output) are not directly accessible. Look for failure details in PR comments.

## PR Classification Categories

### 1. ✅ Ready to Merge
PRs that meet all of the following criteria:
- mergeable_state is "clean" or not "blocked"
- Has at least one APPROVED review (no CHANGES_REQUESTED)
- No blocking labels: `DO NOT MERGE`, `Branch Lockdown`
- Not in draft state

### 2. 🔒 Branch Lockdown (Monitoring Only)
PRs that:
- Has `Branch Lockdown` label (branch is closed for servicing fixes)
- These are expected and just need monitoring until lockdown lifts

### 3. ⚠️ Changes Requested
PRs that have:
- At least one review with state = `CHANGES_REQUESTED`
- Requires action from PR author or owning team

### 4. ❌ Failing / Blocked
PRs that have:
- mergeable_state = "blocked" or "unstable"
- Check PR comments for failure details (NU1603 errors, test failures, etc.)
- May have merge conflicts

## Usage

To use this skill, ask Copilot to:

```
@workspace Check the build duty PR status for today
```

or

```
@workspace Triage open PRs in dotnet/sdk for build duty
```

## Workflow Instructions

When performing build duty triage:

1. **Fetch Open PRs**: Query for all open pull requests in the monitored repositories
2. **Check CI Status**: For each PR, determine the status of all required checks
3. **Classify PRs**: Sort PRs into the three categories above
4. **Report Summary**: Provide a formatted summary with:
   - PR number and title
   - Author
   - Target branch
   - Status category
   - Link to the PR
   - Age of the PR (days open)
   - Any blocking issues or notes

## Sample Output Format

```markdown
## Build Duty PR Triage Report - [Date]

### ✅ Ready to Merge (X PRs)
| PR | Title | Target | Age | Reviews | State | Notes |
|----|-------|--------|-----|---------|-------|-------|
| #12345 | Fix build issue | main | 2d | ✅ Approved | clean | Ready to merge |

### 🔒 Branch Lockdown (X PRs)
| PR | Title | Target | Age | Reviews | Notes |
|----|-------|--------|-----|---------|-------|
| #12346 | Feature update | release/9.0.2xx | 1d | ✅ Approved | Branch locked for release |

### ⚠️ Changes Requested (X PRs)
| PR | Title | Target | Age | Reviewer | Notes |
|----|-------|--------|-----|----------|-------|
| #9754 | Source code updates | release/10.0.3xx | 5d | @MiYanni | Changes requested on Jan 22 |

### ❌ Failing / Blocked (X PRs)
| PR | Title | Target | Age | State | Issue |
|----|-------|--------|-----|-------|-------|
| #52585 | Source code updates | release/10.0.3xx | 5d | blocked | NU1603: Microsoft.Deployment.DotNet.Releases version mismatch |

### ⏳ Stale PRs (>7 days, no Branch Lockdown)
| PR | Title | Target | Age | Issue |
|----|-------|--------|-----|-------|
| #52523 | Update dependencies | release/10.0.2xx | 9d ⚠️ | Same NU1603 issue |

### 📊 Summary
| Category | Count |
|----------|-------|
| Ready to merge | X |
| Branch Lockdown | X |
| Changes Requested | X |
| Failing/Blocked | X |
| Stale (>7d) | X |
| **Total** | **X** |
```

## Labels to Watch

### Blocking Labels
- `DO NOT MERGE` - Explicit block, do not merge under any circumstances
- `Branch Lockdown` - Branch is closed for servicing fixes (typically part of each month)

### Area Labels (for routing)
- `Area-*` labels indicate which team owns the change

## Query Examples

Use these GitHub search queries:

```
# All maestro PRs in SDK
is:open is:pr repo:dotnet/sdk author:app/dotnet-maestro

# Merge PRs (then filter for "Merge branch" in title)
is:open is:pr repo:dotnet/sdk author:app/github-actions

# Release management PRs
is:open is:pr repo:dotnet/sdk author:vseanreesermsft

# Bot PRs
is:open is:pr repo:dotnet/sdk author:dotnet-bot

# dotnet/dotnet VMR - then filter titles for SDK-owned repos
is:open is:pr repo:dotnet/dotnet author:app/dotnet-maestro
# Include only if title contains: dotnet/sdk, dotnet/templating, 
# dotnet/deployment-tools, or dotnet/source-build-reference-packages
```

## Age Tracking and Stale PR Flagging

For every PR, calculate and display the age (days since created).

**⚠️ Flag PRs older than 7 days** that do NOT have the `Branch Lockdown` label.
These may be stuck or need attention.

## Additional Checks

When triaging, also consider:
1. **Stale PRs**: PRs open for more than 7 days without activity - these should be flagged with ⚠️
2. **Auto-merge enabled**: PRs with auto-merge that are waiting on checks
3. **Codeflow PRs**: Maestro PRs with "Source code updates" need quick attention
4. **Merge conflicts**: Automated merge PRs may have conflicts to resolve

## Escalation

If you encounter:
- Infrastructure failures affecting multiple PRs → Check Azure DevOps status
- Persistent test failures → Ping the area owners
- Merge conflicts on critical PRs → Notify the PR author
