# Focused evidence retrieval

## Match the tool to the question

| Question | Starting point |
| --- | --- |
| Which build, commit, and pipeline failed? | Read PR checks from the source provider, or call `azdo_build` for an Azure DevOps build URL. |
| Where did the build fail? | Call `azdo_timeline`, `azdo_search_timeline`, or `azdo_evidence_plan`. |
| What is the error message? | Call `azdo_search_log` or `azdo_log` with the log ID from the timeline. Fetch more lines if the log tail is truncated. |
| Which test case failed? | Call `azdo_test_runs` and `azdo_test_results`. Fetch child results or the TRX file if the summary omits them. |
| Which Helix work item failed, and why? | Call `azdo_helix_jobs`, `helix_status`, and `helix_work_item`, then use `helix_logs`, `helix_search`, or `helix_files`. |
| Is there a binlog, dump, or test attachment? | Call `azdo_artifacts`, `azdo_test_attachments`, `helix_files`, or `helix_find_files`, and download only the needed file. |
| Did this test or job fail without these changes? | Call `azdo_builds` filtered by pipeline, branch, and date, then check the specific test or job in a matching build. |
| Does a Known Build Error (KBE) cover this failure? | Read the Build Analysis check report. `azdo_build_analysis` only reads issue links from build tags and timeline entries. |
| What code or build inputs changed? | Use GitHub or Azure Repos commit and diff APIs at the tested SHAs. Fetch raw Azure Build or Run JSON if summary fields are missing. |

If an MCP tool restricts the organization or project, or lacks a needed filter,
call the provider API directly with the build's own organization and project.

## Query pitfalls

- `azdo_timeline` returns only failed records by default. Pass `all` when you
  check a passing control build, verify that a task ran, or inspect a passing
  `Monitor Helix Jobs` task that has failed work items.
- `azdo_test_results` returns only failed tests by default. Request passing
  results explicitly when you verify that a test ran and passed in a control
  build.
- `azdo_helix_jobs` (even with `filter=all`) returns only jobs found in build
  logs. Compare `helix_status` failure lists with monitor summaries, and check
  for work items that crashed or timed out before producing a TRX file.
- Grouped Azure test results can hide parameterized test cases. If the MCP
  summary omits child cases, fetch the raw result with
  `detailsToInclude=SubResults` or read the TRX file.
- `azdo_build_analysis` only reads issue links from build tags and timeline
  records. An empty `knownIssues` list does not mean Build Analysis found no
  match; check the Build Analysis check run directly.

## Focused API fallback

When MCP tools omit needed fields, use read-only `az rest` (Azure DevOps) or
`gh api` (GitHub) GET requests:

- **Azure DevOps endpoints:** see [Builds GET](https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-7.1),
  [Pipeline Runs GET](https://learn.microsoft.com/en-us/rest/api/azure/devops/pipelines/runs/get?view=azure-devops-rest-7.1),
  [Test Results GET](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/get?view=azure-devops-rest-7.1),
  [Git Diffs](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/diffs/get?view=azure-devops-rest-7.1),
  and [Git Commits](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/commits/get-commits?view=azure-devops-rest-7.1).
- **Entra token resource:** pass `--resource 499b84ac-1321-427f-aa17-267ca6975798`
  with `az rest` calls to Azure DevOps (see [Azure CLI authentication guidance](https://learn.microsoft.com/en-us/azure/devops/cli/entra-tokens?view=azure-devops)).
- **Task logs as JSON:** pass `--headers Accept=application/json` (`--output json`
  alone does not set the `Accept` header) and read the `value` line array.
