---
description: "Check if public GitHub release branches have been mirrored to internal Azure DevOps repos for dotnet/sdk and dotnet/installer."
---

# Branch Mirror Status Check

Check whether the latest commits on public GitHub release branches have been mirrored to the corresponding `internal/release/*` branches in Azure DevOps.

## Branch Mappings

| GitHub Repo | GitHub Branch | AzDo Project | AzDo Repo | AzDo Branch |
|---|---|---|---|---|
| dotnet/sdk | release/8.0.1xx | internal | dotnet-sdk | internal/release/8.0.1xx |
| dotnet/sdk | release/8.0.4xx | internal | dotnet-sdk | internal/release/8.0.4xx |
| dotnet/sdk | release/9.0.1xx | internal | dotnet-sdk | internal/release/9.0.1xx |
| dotnet/sdk | release/9.0.3xx | internal | dotnet-sdk | internal/release/9.0.3xx |
| dotnet/installer | release/8.0.1xx | internal | dotnet-installer | internal/release/8.0.1xx |
| dotnet/installer | release/8.0.4xx | internal | dotnet-installer | internal/release/8.0.4xx |

## Procedure

### Step 1: Get latest commits from GitHub

For **each** row in the table above, call `github-mcp-server-list_commits` to get the most recent commit on the public branch. Make all calls **in parallel** for efficiency.

Parameters for each call:
- `owner`: the GitHub org (e.g., `dotnet`)
- `repo`: the GitHub repo name (e.g., `sdk` or `installer`)
- `sha`: the branch name (e.g., `release/8.0.1xx`)
- `perPage`: 1 (we only need the latest commit)

Record the **commit SHA** and **commit message** from each response.

### Step 2: Check if each commit exists on the corresponding AzDo branch

For **each** row, verify the GitHub commit SHA is present on the correct AzDo branch. Make all calls **in parallel**.

The AzDo `search_commits` tool name varies by session — look for one matching `*repo_search_commits` that can access the `internal` project in the `dnceng` org. Common names include `dnceng-azure-devop-repo_search_commits` or `azure-devops-repo_search_commits`.

#### Step 2a: Search for merge commits referencing the GitHub SHA

Use `search_commits` with:
- `project`: `internal`
- `repository`: the AzDo repo name (e.g., `dotnet-sdk` or `dotnet-installer`)
- `version`: the AzDo branch name (e.g., `internal/release/8.0.1xx`)
- `versionType`: `Branch`
- `searchText`: the GitHub commit SHA from Step 1
- `top`: 5

This searches the branch history for commits whose message contains the GitHub SHA. The mirroring service typically creates merge commits with messages like `Merge commit '<GitHub SHA>'`, so a match confirms the commit was mirrored to the correct branch.

If a match is found, the mirror is **up to date**.

#### Step 2b: If no match, check for direct push (same SHA on branch)

Sometimes commits are pushed directly to the AzDo branch without a merge commit wrapper, so the SHA is identical but doesn't appear in any commit message. If Step 2a returns no results, get the latest commits on the AzDo branch:

Use `search_commits` with:
- `project`: `internal`
- `repository`: the AzDo repo name
- `version`: the AzDo branch name
- `versionType`: `Branch`
- `top`: 5

Then check if any returned commit's `commitId` exactly matches the GitHub SHA from Step 1. If it does, the mirror is **up to date** (direct push). If not, the mirror is **behind**.

**Important:** Do **not** use `commitIds` alone — that searches the entire repo regardless of branch and does not confirm the commit is on the target branch.

### Step 3: Present results

Display a summary table:

```
| Repo | Branch | Latest GH Commit | Mirrored? | Details |
|---|---|---|---|---|
| dotnet/sdk | release/8.0.1xx | abc1234 "commit msg" | ✅ / ❌ | |
| ... | ... | ... | ... | |
```

For any ❌ entries, also check the latest commit on the AzDo branch to show how far behind it is:
- Use `search_commits` with `project`, `repository`, `version` (the AzDo branch name), and `top: 1` to get the most recent AzDo commit.
- Report the AzDo branch's latest commit SHA and date in the Details column.

## Troubleshooting

If the AzDo tools return errors about the project or repository not being found, the AzDo MCP connection may not be configured for the `dnceng` organization. In that case:
- Try other available `*repo_search_commits` or `*repo_list_repos_by_project` tools to find one that can access `project: internal`.
- If no tool can access the project, inform the user that the AzDo MCP connection may need to be configured for the `dnceng` organization.
