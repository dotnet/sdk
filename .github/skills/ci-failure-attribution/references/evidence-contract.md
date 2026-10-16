# Evidence contract

The C# file-based apps use the repository-selected .NET SDK; they have no package
dependencies and do not require PowerShell. All JSON outputs use `schemaVersion: 1`.
Unknown fields remain `null` or explicit gaps; they are not replaced by successful
empty results. Paths supplied to the scripts are local invocation artifacts.

`evidence.json` contains:

| Field | Meaning |
| --- | --- |
| `mode`, `repository` | `pr` or `build`; repository is `dotnet/sdk`. |
| `selectedBuildIds` | Exact builds being assessed, separate from historical controls. |
| `pr` | PR metadata, or null for standalone builds. Current PR metadata is not historical identity. |
| `checks` | PR check inventory with `coverage` (`assessed` or `unassessed`) and source URLs; empty in standalone mode. |
| `builds` | Selected and historical builds with observations and acquisition completeness. |
| `acquisitions` | Command description, snapshot path, producer exit status, `ok`, and `complete`. |
| `changeWindows` | Tested base/head, baseline build, ancestry status, commit list, diff path, and completeness. |
| `history` | Explicit query bounds; a bounded history is not an exhaustive failure history. |
| `issueSearches`, `buildAnalysis` | Search candidates and original check reports, not automatic matches or causal verdicts. |
| `error` | Collection failure, when present; preserved as an offline comparison coverage gap. |

The [collector](../scripts/CiEvidence.cs) marks a completed check assessed only when
its supported Azure build URL matches a selected organization, project, and build
ID. A numeric ID match alone is insufficient. Unsupported URLs and uncollected
build contexts remain unassessed, including when an explicit build is selected.

Timeline acquisition explicitly requests `--filter all --json`, not the producer's
default failure-only view, so successful job/task executions remain available as
controls. Replay snapshots must match that all-record request. Do not relabel
older failure-only recordings as full timelines; acquire fresh all-record snapshots.
Timeline observations retain start time, producer outcome, and `executionVerified`.
Pending or never-started canceled/abandoned records remain unknown observations,
not executed controls or source boundaries. Cancellation requires a real start
time to establish execution; older timelines without execution provenance cannot
establish controls.

Fatal acquisition exceptions are retained with `ok: false` and `complete: false`.
Their snapshot and producer exit status are `null` when unavailable, not invented
success/failure exit codes. Aborted collection marks selected builds incomplete.
`--timeout-seconds` applies one deadline to native producer exit and both redirected
output streams. Descendants retaining stdout or stderr handles cannot make output
draining wait indefinitely; a timeout remains an explicit failed acquisition.

Each build has `id`, `url`, `repository`, `organization`, `project`, `definitionId`,
`branch`, `commit`, `prHeadSha`, `targetSha`, `reason`, `queuedAt`, `parameters`,
`resources`, `complete`, and `observations`. Source identity and missing configuration
are preserved even when retrieval is partial.
The [parameter decoder](../scripts/CiEvidence.cs) accepts Azure's JSON-encoded
parameter objects and object-valued metadata. It decodes and recursively scrubs
credential fields before persisting build-metadata snapshots or dossier parameters.
Offline comparison also accepts either representation in older dossiers.
Parameter objects compare structurally: serialization whitespace and key order are
not input changes, while case-distinct keys and non-secret values remain distinct.
Null, empty/whitespace strings, and encoded JSON `null` remain unavailable parameters,
not known empty objects. Malformed JSON and non-object parameter values fail
explicitly without echoing their contents or saving the unsafe metadata snapshot.

The [collector](../scripts/CiEvidence.cs) also retains `testRuns` metadata, including
producer state and counters.
Test results are acquired through authenticated `az rest` GET requests with
`detailsToInclude=SubResults` and explicit 200-result pagination. The raw
[Azure result contract](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/list?view=azure-devops-rest-7.1)
retains `resultGroupType` and recursive `subResults`, unlike hlx's typed projection.
Aggregate results use `kind: test-group` and remain context. Child observations
include their display-name hierarchy and `parentResultId`; an aggregate pass
cannot stand in for an unexecuted parameterized case. Missing children of grouped
results remain explicit hierarchy gaps. Run counters reconcile top-level results
only, not their additional child observations.
Raw result observations set `hierarchyVerified: true`. Older projected results
without hierarchy provenance remain readable but cannot establish case-level controls.
A run must report `Completed` and zero `incompleteTests` to establish complete run
coverage. Non-completed runs and missing state/counters remain explicit gaps, even
when the build has completed; already acquired individual results remain available.
Acquired tests use IDs of the form `test:RUN_ID:RESULT_ID`. Duplicate result IDs,
missing counters, and inconsistent counts remain coverage gaps even when another
timeline failure accounts for the build result. Full retrieval requires distinct
result counts to equal `totalTests` and passed counts to equal `passedTests`.
The normalized failed count must cover at least `failedTests`; it can also include
error/timeout outcomes.
`testResultsIncludePassed: false` records diagnostic-only retrieval above the result
limit. It still checks advertised failures, but intentionally omits total/passed
count reconciliation and retains an explicit incomplete-coverage gap. Older
dossiers without this flag are checked as full retrievals.
When `testRuns` metadata is present, offline comparison checks run state and result
counts independently of build completeness flags, including historical controls.
Older dossiers without `testRuns` remain readable.
`targetBranchHint` comes from current PR metadata; for historical validations it
is only a retrieval hint, not proof of the historical target ref. The historical
target SHA must be independently recovered from the tested merge commit.
If identical numeric build IDs occur across different organization/projects,
collect those contexts separately; the adapter fails rather than conflating them.

Each observation has `id`, `kind`, `name`, `lane`, `outcome`, `mechanism`, `url`,
and `contextOnly`. Outcomes are `Passed`, `Failed`, `Skipped`, or `Unknown`.
Named tests include parameterized titles in their identity. Job/task parents stay
visible as context; only the agent, with supporting evidence, groups root mechanisms.
Scenario names and lanes must contain actual identities, not null, empty, or
whitespace-only values. A composite test name containing only the ` | ` separator
is unknown; either a populated automated name or title is sufficient. Job/task
lanes require populated components, and the collector leaves a task's lane unknown
when its parent chain does not resolve to a job rather than using the task as its
own job. Acquired observations remain in the dossier with explicit
`Unverified scenario identity` gaps. Offline comparison checks these identities
independently of completeness flags in both selected and historical builds,
including older dossiers without additional identity metadata.
An unknown/empty mechanism never establishes a matching failure signature.
Failed or canceled builds without any failed or unknown observations retain an
explicit missing-failure-evidence gap, including pipeline validation/startup
failures without jobs. An empty timeline alone is not a gap when structured test
or Helix results account for the failure.

History queries are bounded, queue-time-ordered candidates, not a guaranteed count
of completed builds. The collector filters completed executions locally because
Azure rejects combining that ordering with its completed-status filter.
GitHub comparison file lists reaching the 300-file cap make the change window
incomplete, even when all commits and a diff were acquired. Do not claim exhaustive
changed-file coverage from a capped list.

`comparison.json` preserves every failed observation, its raw mechanism and
signature, control outcomes, candidate limitations, prior matching occurrences,
and executed passing controls. An eligible control requires known, nonempty,
matching repository, organization/project, and pipeline identity plus an executed
matching scenario/lane with known names and lanes. Missing identity disqualifies
both passing and matching failure controls, not merely their confidence.
Scenario lookup indexes use exact kind/name/lane identities and retain every
execution under each identity; repeated executions are not collapsed into one outcome.
Eligible controls are **not** automatically equivalent: platform, queue, toolchain,
parameter/resource differences, and test selection still need verification.
`observedMixedOutcomes` is not a "flaky and unrelated" verdict.
`independentMatchingOccurrences` excludes same-source executions, matching PR
heads, PR-triggered builds, and `refs/pull/` validations in both invocation modes.
Selecting a PR validation with `--build` alone does not turn earlier attempts into
independent evidence, even when their merge SHAs differ. Their ordinary matching
history and candidate source boundaries remain available.
The [signature implementation](../scripts/CiEvidence.cs) normalizes GUIDs only in
known volatile contexts: labeled Helix job, correlation/activity/request IDs and
Helix job URLs. It normalizes leading log timestamps, not assertion timestamps.
Stable GUIDs such as COM CLSIDs and other expected/actual values remain distinct.
Signatures normalize CRLF transport line endings to LF but preserve spaces, tabs,
line breaks, and boundary whitespace. Task/test diagnostic extraction also preserves
that whitespace; different assertion values must not become matching failures.
The collector decodes Azure task log JSON strings before signature normalization;
unsupported response shapes remain explicit gaps rather than becoming log text.

Online acquisition records snapshots under `snapshots`. A `--replay-directory`
containing those snapshots replays commands without running `gh` or `hlx`.
Helix console downloads additionally have a `.console.json` sidecar containing
the scrubbed final 120 lines and source URL; include it in a replay. The collector
never reads a local path from replayed command output. Missing console sidecars
remain explicit coverage gaps. Work-item passes support work-item comparisons,
not individual assertions absent from structured test results.
Missing replay commands fail explicitly. Replays are marked as such and never
claimed as live service validation. Scrubbing credentials can make an artifact
unusable for authentication; never attempt to replay authenticated URLs.
Helix work-item lanes include both the physical queue and sorted submission
contexts from the Azure parent job and tagged test runs. Queue/name equality
alone cannot equate ordinary and FullFramework streams. The original queue and
submission contexts are retained separately; unknown submission identity cannot
establish a control.

`CiEvidence.cs collect` exits nonzero on hard errors or incomplete mandatory evidence,
while retaining a partial dossier when possible. Offline comparison can inspect
that dossier, but cannot establish complete coverage or negative conclusions.
Compare metadata contract errors also fail explicitly. Successful acquisition of
failed CI is not a script failure.

## Azure DevOps authentication

`hlx azdo auth-status --json` reports the resolved authentication source without
exposing credentials. Anonymous access can retrieve public build metadata while
test-run/result endpoints still require authentication; metadata success is not
proof of test-result access.

Collector child processes disable interactive Git credential prompts with
`GIT_TERMINAL_PROMPT=0` and `GCM_INTERACTIVE=never`. This permits existing
noninteractive credentials without starting a new sign-in; it also avoids
credential-manager prompts blocking public Helix queries. These settings are
process-scoped, not user/machine configuration.

If hlx remains anonymous despite an existing Azure CLI sign-in, acquire a DevOps
bearer token for resource `499b84ac-1321-427f-aa17-267ca6975798` and pass it through
the child process environment. `AZDO_TOKEN` is supported by hlx. Do not initiate
interactive login without approval, persist the token, print it, put it in native
command arguments, or modify user/machine environment variables.

```powershell
$previousToken = $env:AZDO_TOKEN
try {
    $token = (& az account get-access-token `
        --resource 499b84ac-1321-427f-aa17-267ca6975798 `
        --query accessToken --output tsv --only-show-errors | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $token) {
        throw 'Azure CLI did not return a DevOps access token.'
    }
    $env:AZDO_TOKEN = $token
    $token = $null
    dotnet "$skill\scripts\CiEvidence.cs" -- collect --build $buildUrl `
        --hlx-path $hlx --output-directory $evidence
} finally {
    $env:AZDO_TOKEN = $previousToken
    $token = $null
}
```

Use the existing authenticated context directly when hlx already resolves one.
Successful token acquisition does not prove project permissions: check collection
exit status and acquisition completeness as usual.
