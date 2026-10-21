# Focused evidence retrieval

Choose a request for the question it answers. These are capability pointers and
correctness pitfalls, not a collection sequence or required evidence format.
Discover the installed tool's schema before calling it; names and supported
filters can vary by version.

## Match the tool to the question

| Question | Useful starting point |
| --- | --- |
| Which build/revision and pipeline failed? | PR checks from the source provider; Helix MCP `azdo_build` for the exact Azure build URL. |
| Where did execution fail? | `azdo_timeline`, `azdo_search_timeline`, or `azdo_evidence_plan`; the plan maps candidate artifacts/Helix failures without downloading them. |
| What is the actual diagnostic? | `azdo_search_log` or `azdo_log` using a log ID from the timeline; expand beyond a tail when needed. |
| Which named test or case failed? | `azdo_test_runs` and `azdo_test_results`; obtain raw child results or an existing TRX if the projection omits them. |
| Which Helix job/work item failed, and why? | `azdo_helix_jobs`, `helix_status`, `helix_work_item`, then focused `helix_logs`, `helix_search`, or file retrieval. |
| Is there an existing diagnostic artifact? | `azdo_artifacts`, `azdo_test_attachments`, or `helix_files`/`helix_find_files`; download only the relevant existing file. |
| Did this exact scenario fail without these changes? | Bounded `azdo_builds` discovery by actual pipeline/ref/time, then inspect the named execution in a chosen candidate. |
| Does an existing known-issue report cover this failure? | Original source-provider Build Analysis check report for known build error (KBE) classifications; `azdo_build_analysis` only extracts issue URL pointers from build tags/timeline. Inspect the original report and linked issue for the exact mechanism. |
| What source/input changed? | Source-provider commit/diff/file APIs at tested SHAs; raw Azure Build/Run metadata when projected inputs are insufficient. |

Helix MCP handles Azure DevOps and Helix, not every CI provider. For GitHub Actions
or another service, use that provider's available read-only tools/API rather than
forcing its identifiers through Azure/Helix tools. If a tool's schema restricts
organizations/projects or lacks a needed filter, use a focused provider API call
with the original identity; do not silently substitute a supported project.

## Preserve identity

Use full URLs when an ID alone is ambiguous. Azure build IDs, test run/result IDs,
log IDs, and Helix job IDs are different namespaces. Join them using returned
metadata, timeline links, or monitor messages, not by matching numbers.

Confirm organization/project, repository, pipeline, tested commit and attempt for
each candidate control. Project names and GUIDs are aliases only when provider
metadata proves the mapping. A friendly repository name does not prove that an
internal Azure repository is equivalent to a public GitHub mirror.

Recover historical PR target/source revisions from the tested merge metadata
when available. Validate the parent identities; do not assume every merge has
exactly two parents or that today's target ref identifies the historical base.
When unresolved, retain the gap rather than creating an introduction interval.

## Query semantics can hide decisive evidence

- `azdo_timeline` defaults to failures. Request `all` or search all results when
  verifying a successful control, actual execution, or a successful Helix monitor
  that contains failed-work-item details. A canceled task that never started is
  not an executed control.
- `azdo_test_results` defaults to failed results. Request the relevant outcomes
  explicitly when comparing a passing case. Missing results, skipped cases,
  aggregate counts, and a green build do not prove a named execution passed.
- `azdo_helix_jobs` and monitor-derived plans identify candidates, not necessarily
  every failed work item. `filter=all` returns all jobs discovered by that tool;
  it does not prove every submitted job was discovered. Compare available
  completed `helix_status` failure inventories with monitor summaries, including
  failures with no completed test result such as watchdog-aborted hangs. Preserve
  any discovery, status, pagination, or completion gaps instead of claiming
  complete Helix coverage.
- Grouped Azure results can hide parameterized children. Preserve the case
  parameters and parent relationship. If the MCP projection omits children,
  request the relevant raw result with `detailsToInclude=SubResults` or inspect
  its existing TRX. An aggregate pass cannot stand in for a named child pass.
- Logs may be tails, excerpts, or search windows. The first visible error may not
  be the initiating error. Retrieve surrounding or full relevant content only
  when needed to distinguish mechanisms; do not fetch every task log.
- Check limits, continuation/pagination, and truncation indicators. A successful
  response with omitted rows is partial evidence. Retrieve another page when it
  matters to the question; otherwise state the coverage limit.
- When claiming exhaustive test coverage, reconcile available run counters and
  completion state against acquired results. For targeted investigation, report
  the named outcomes inspected rather than implying complete run coverage.
- Typed summaries may omit repository details, trigger metadata, parameters,
  resolved resources, or counters. Use raw Build/Run metadata for fields needed
  to establish comparability. An omitted field is not zero or a known empty object.
- `azdo_build_analysis` is not the original Build Analysis known-build-error
  report. Its `knownIssues=[]` or `unmatchedFailures` fields do not establish
  that Build Analysis found no match. Follow issue pointers as leads and inspect
  the original check report before attributing a classification to Build Analysis.

## Focused API fallback

Prefer an existing authenticated tool over a new wrapper. Azure CLI `az rest`
can fill Azure MCP gaps with a read-only GET; `gh api` can fill GitHub gaps.
Use the selected build's organization/project and source repository. See the
authoritative APIs for [build metadata](https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-7.1),
[pipeline run inputs](https://learn.microsoft.com/en-us/rest/api/azure/devops/pipelines/runs/get?view=azure-devops-rest-7.1),
and [individual test results](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/get?view=azure-devops-rest-7.1).

For Azure DevOps GETs using Azure CLI's automatic authentication, include
`--resource 499b84ac-1321-427f-aa17-267ca6975798` so `az rest` requests a token
for Azure DevOps rather than trying to infer the audience from `dev.azure.com`.
See the [Azure CLI authentication guidance](https://learn.microsoft.com/en-us/azure/devops/cli/entra-tokens?view=azure-devops).

For Azure task logs via `az rest`, explicitly negotiate JSON with
`Accept=application/json`; `--output json` alone does not negotiate the response.
Decode the returned string/line array before interpreting timestamps or errors.
Preserve meaningful whitespace, assertion values, codes, versions, paths, and
endpoints; normalize only proven transport decoration or volatile identifiers.

Azure source windows can use [Git diffs](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/diffs/get?view=azure-devops-rest-7.1)
and [commit lists](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/commits/get-commits?view=azure-devops-rest-7.1).
Verify exact base/head and ancestry. For commit-list comparisons, follow the API's
documented `itemVersion` and `compareVersion` semantics, then reconcile returned
commits with the expected interval before claiming a complete range. Changed-file
metadata is not a textual patch or source inspection. Non-ancestor comparisons do
not establish linear introduction.

Use existing authorization. Do not print, save, or pass tokens in command
arguments, modify global credentials/PATH, or install tooling without applicable
approval. If access fails, record the gap and continue with independent available
evidence. Treat retained logs/artifacts as sensitive. Never send private failure
text or source to public search services.

## Lightweight evidence notes

Retain only what makes the assessment auditable: source URL or local artifact,
build/revision and scenario identity, observed outcome/mechanism, relevant input
differences, and limitations. Notes can be a concise report or session records;
there is no required JSON schema or full raw snapshot collection.

Separate facts from inference and hypotheses from verified controls. Reuse
acquired evidence. A failed request does not invalidate earlier successful
requests, and a search hit does not become an independent execution.
