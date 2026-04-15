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

### Step 2: Check if each commit exists in Azure DevOps

For **each** row, search for the GitHub commit SHA in the corresponding AzDo branch. Make all calls **in parallel**.

Use `dnceng-azure-devop-repo_search_commits` with:
- `project`: `internal`
- `repository`: the AzDo repo name (e.g., `dotnet-sdk` or `dotnet-installer`)
- `version`: the AzDo branch name (e.g., `internal/release/8.0.1xx`)
- `versionType`: `Branch`
- `top`: 1

Do **not** use `commitIds` — that searches the entire repo regardless of branch. Instead, get the latest commit on the specific AzDo branch and compare its commit message or SHA to the GitHub commit. The mirroring service creates merge commits with messages like `Merge commit '<GitHub SHA>'`, so check whether the AzDo head commit references the expected GitHub SHA.

If the AzDo branch head references the GitHub commit SHA, the mirror is **up to date**. If it references an older commit, the mirror is **behind**.

### Step 3: Present results

Display a summary table:

```
| Repo | Branch | Latest GH Commit | Mirrored? | Details |
|---|---|---|---|---|
| dotnet/sdk | release/8.0.1xx | abc1234 "commit msg" | ✅ / ❌ | |
| ... | ... | ... | ... | |
```

For any ❌ entries, also check the latest commit on the AzDo branch to show how far behind it is:
- Use `dnceng-azure-devop-repo_search_commits` with `project`, `repository`, `version` (the AzDo branch name), and `top: 1` to get the most recent AzDo commit.
- Report the AzDo branch's latest commit SHA and date in the Details column.

## Troubleshooting

If the AzDo tools return errors about the project or repository not being found, the `dnceng-azure-devop-*` tools may not be connected to the `dnceng-internal` organization. In that case:
- Verify tool connectivity by listing repos: `dnceng-azure-devop-repo_list_repos_by_project` with `project: internal`.
- If the project is not accessible, inform the user that the AzDo MCP connection may need to be configured for the `dnceng-internal` organization.
