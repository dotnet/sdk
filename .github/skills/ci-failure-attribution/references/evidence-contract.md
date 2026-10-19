# Evidence contract

The C# file-based apps use the repository-selected .NET SDK; they have no package
dependencies and do not require PowerShell. Build them with an invocation-specific
`--artifacts-path` and both `-p:ImportDirectoryBuildProps=false` and
`-p:ImportDirectoryBuildTargets=false`, as shown in [the skill](../SKILL.md).
File-app [default properties](../../../../src/Cli/Microsoft.DotNet.FileBasedPrograms/VirtualProjectBuilder.cs)
still enable nullable analysis and implicit usings without repository imports.
Repository imports otherwise override the SDK's binary/intermediate locations;
`--output` alone isolates only binaries. Verify physical `bin` and `obj` outputs
under the requested artifacts path, not merely a successful command exit.
Set process-scoped `TEMP`, `TMP`, and `XDG_DATA_HOME` to an invocation-local directory
as well: file-app build markers use the OS temporary/local-data location independently
of MSBuild paths. Both `dotnet run` and `dotnet build` write these source-keyed markers,
even with `--no-cache`; simultaneous builds otherwise contend on `build-success.cache`.
The harness likewise isolates its child collector build's intermediate and temporary
paths and checks that compiled and restore outputs exist under the requested root.
All JSON outputs use `schemaVersion: 1`.
Unknown fields remain `null` or explicit gaps; they are not replaced by successful
empty results. Paths supplied to the scripts are local invocation artifacts.
The scripts do not redact credentials. Snapshots, logs, errors, diffs, and reports
retain credential-bearing values present in the collected evidence. Treat these
artifacts as sensitive and review them before sharing.

`evidence.json` contains:

| Field | Meaning |
| --- | --- |
| `mode`, `repository` | `pr` or `build`; repository is `dotnet/sdk`. |
| `selectedBuildIds` | Exact builds being assessed, separate from historical controls. |
| `pr` | PR metadata, or null for standalone builds. Current PR metadata is not historical identity. |
| `checks` | PR check inventory with `coverage` (`assessed`, `unsupported`, `skipped`, or `unassessed`), a reason, and source URLs; empty in standalone mode. |
| `builds` | Selected and historical builds with observations, acquisition completeness, and collected semantic `coverageGaps`. |
| `acquisitions` | Command description, snapshot path, producer exit status, `ok`, `complete`, attempt count, and transient errors. |
| `changeWindows` | Tested base/head, baseline build, ancestry status, commit list, diff path, and completeness. |
| `history` | Explicit query bounds; a bounded history is not an exhaustive failure history. |
| `issueSearches`, `buildAnalysis` | Search candidates and original check reports, not automatic matches or causal verdicts. |
| `error` | Collection failure, when present; preserved as an offline comparison coverage gap. |
| `collectionStage` | Last checkpoint/collection phase; a checkpoint is partial evidence, not a completed collection. |
| `groupDetails` | Global detail acquisition/time budgets, candidate/requested/omitted counts, and measured phase time. |

Each collected build stores semantic validation findings in `coverageGaps` and
marks itself incomplete when any are present. Comparison reports those findings
without recording them as failed acquisitions.
`observationsComplete: false` explicitly identifies builds whose observations have
not finished acquisition; successful build metadata alone cannot make them complete.
The collector saves an atomic `evidence.json` checkpoint after build metadata,
each top-level result run, each build's independent evidence, and before/after the
bounded group-detail phase. Interrupted collection can therefore retain usable
top-level evidence even before all hierarchy details or change windows exist.
For older schema-version-1 dossiers without this field, offline comparison
computes the same semantic coverage checks from the retained build data.

The [collector](../scripts/CiEvidence.cs) marks a completed check assessed only when
its supported Azure build URL matches a selected organization, project, and build
ID. A numeric ID match alone is insufficient. Unsupported URLs and uncollected
build contexts remain unassessed, including when an explicit build is selected.
Azure project-name/GUID aliases are matched only using the name/ID mapping returned
by the raw Build GET (`project` or `definition.project`). Builds retain this mapping
as `projectName` and `projectId`, while `project` preserves the requested URL context.
The requested project must match that metadata. Check coverage, discovered-build
reuse, and offline control comparison accept the verified aliases within the same
organization; different authoritative project IDs remain different even when names
match. When neither compared build supplies a verified mapping, older evidence
requires matching project strings rather than inferring aliases. Alias URLs for the same selected
build do not create duplicate selected IDs or change windows.

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
An Azure GET timeout is retried once after a one-second delay. A repeated timeout
is retained with a null snapshot and producer exit code, and callers continue
acquiring independent evidence when the affected data is not required to identify
the build. Successful retries record the attempt count and transient timeout without
making the final acquisition incomplete.

Each build has `id`, `url`, `repository`, `organization`, `project`, `definitionId`,
`branch`, `commit`, `prHeadSha`, `targetSha`, `reason`, `queuedAt`, `parameters`,
`templateParameters`, `resources`, `complete`, and `observations`. Source identity and missing configuration
are preserved even when retrieval is partial.
Build identity and queue parameters come from the same raw Azure Build GET;
history rows are discovery candidates, not authoritative build metadata.
Template parameters and resolved resources come from the matching pipeline Run
GET. Build/source and pipeline/run identities must match. Missing or
unsupported metadata remains explicit; it is never converted to an empty object.
Snapshots retain only the relevant raw input fields, not unrelated `variables`
or expanded `finalYaml`. Comparison checks template parameters independently.
The [parameter decoder](../scripts/CiEvidence.cs) accepts Azure's JSON-encoded
parameter objects and object-valued metadata. It decodes them before persisting
build-metadata snapshots or dossier parameters, preserving all parameter values.
Offline comparison also accepts either representation in older dossiers.
Parameter objects compare structurally: serialization whitespace and key order are
not input changes, while case-distinct keys and all values remain distinct.
Null, empty/whitespace strings, and encoded JSON `null` remain unavailable parameters,
not known empty objects. Malformed JSON and non-object parameter values fail
explicitly without echoing their contents or saving the invalid metadata snapshot.

The [collector](../scripts/CiEvidence.cs) retains raw `testRuns` metadata from
Azure REST, using hlx only to enumerate run IDs. Missing counters remain absent,
not the typed producer's default zeros. Raw run IDs/build associations must match
the requested evidence. An unknown total uses diagnostic-only result retrieval
with explicit coverage gaps, not an invented zero-test run.
Each run also records `acquiredResultCounts` from distinct top-level result
observations. When a raw `failedTests` counter is absent, complete acquired
outcomes reconcile to `totalTests`, and every outcome is known, the collected
results establish the observed failure count without fabricating the missing
producer counter. Otherwise the missing counter remains an explicit coverage gap.
Test results are acquired through authenticated `az rest` GET requests. Top-level
results omit subresult details and use explicit 1000-result pagination. All builds'
top-level rows, counters, timelines, Helix observations, and artifact inventories
are acquired before optional group details. Grouped results use single-result
`SubResults` requests within a global `--group-detail-limit` (default 20, maximum
1000) and `--group-detail-seconds` budget (default 120, maximum 3600). Failed or
unknown groups in selected builds are paired with matching historical group
candidates before unrelated passing groups. Matching here schedules acquisition;
it does not establish control equivalence. The request limit counts logical
acquisitions, each of which may retry once after an Azure timeout. The elapsed
budget is checked between acquisitions; the in-flight request/retry is bounded
by `--timeout-seconds` and may finish after the phase budget.
Omitted groups retain their raw top-level outcomes and explicit hierarchy gaps,
not invented child passes or complete case-level controls. This reduces serial
page requests without unbounded group-detail fan-out. The raw
[Azure list API](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/list?view=azure-devops-rest-7.1)
allows up to 1000 rows without details, while the
[single-result API](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/get?view=azure-devops-rest-7.1)
supports `SubResults`. Raw results retain `resultGroupType` and recursive
`subResults`, unlike hlx's typed projection.
Full retrieval does not specify an outcome filter, so valid unknown/unexecuted
states are not silently omitted. Diagnostic-only retrieval excludes `Passed`
but includes all other documented Azure outcomes. Observations retain the raw
`producerOutcome`; None/Unspecified/Paused/InProgress remain unknown and
NotImpacted remains skipped, never an executed pass.
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
Structured test lanes remove only the producer's terminal `(attempt N)` annotation
and volatile Helix job tag. Raw run names and parsed `runAttempt` remain provenance,
while OS/framework/configuration text remains part of the scenario identity.
Scenario names and lanes must contain actual identities, not null, empty, or
whitespace-only values. A composite test name containing only the ` | ` separator
is unknown; either a populated automated name or title is sufficient. Job/task
lanes require populated components, and the collector leaves a task's lane unknown
when its parent chain does not resolve to a job rather than using the task as its
own job. Acquired observations remain in the dossier with explicit
`Unverified scenario identity` gaps. Offline comparison checks these identities
independently of completeness flags in both selected and historical builds,
including older dossiers without additional identity metadata.
Helix statuses with a missing/empty queue still retain known work-item outcomes,
console excerpts, and artifact inventories. Their lanes remain empty with an
explicit gap; both queue and submission context are required for controls.
An unknown/empty mechanism never establishes a matching failure signature.
Failed or canceled builds without any failed or unknown observations retain an
explicit missing-failure-evidence gap, including pipeline validation/startup
failures without jobs. An empty timeline alone is not a gap when structured test
or Helix results account for the failure.

History queries are bounded, queue-time-ordered candidates, not a guaranteed count
of completed builds. The collector filters completed executions locally because
Azure rejects combining that ordering with its completed-status filter.
Issue search budgets count up to six distinct full open/closed query strings,
not observations or cache hits. Each query appears once in `issueSearches`;
repeated mechanisms do not prevent later diagnostics from using the remaining budget.
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
It requires a known non-PR trigger and a heads/tags ref; null or unknown trigger
provenance is not proof of independence. Direct manual execution of the tested
PR head also remains PR evidence even without a populated `prHeadSha` field.
Selecting a PR validation with `--build` alone does not turn earlier attempts into
independent evidence, even when their merge SHAs differ. Their ordinary matching
history and candidate source boundaries remain available.
The [signature implementation](../scripts/CiEvidence.cs) normalizes GUIDs only in
known volatile contexts: labeled Helix job, correlation/activity/request IDs and
Helix job URLs.
Versioned `/api/YYYY-MM-DD/jobs/GUID` URLs and the official parenthesized monitor
job format normalize the job GUID only; API versions, queues, work-item names,
and other diagnostic content remain meaningful. It normalizes leading timestamps
only while extracting known task/console log
segments, retaining the canonical text as `signatureMechanism` alongside the raw
mechanism. Assertion messages, including timestamp-only multiline expected values,
are not treated as transport logs. Timestamp-only log lines are retained as values.
Stable GUIDs such as COM CLSIDs and other expected/actual values remain distinct.
Temporary/workspace normalization recognizes absolute path-token roots only,
not interior `tmp`, `var/tmp`, or workspace-like segments. Unix paths retain
case sensitivity; Windows root matching remains case-insensitive.
Signatures normalize CRLF transport line endings to LF but preserve spaces, tabs,
line breaks, and boundary whitespace. Task/test diagnostic extraction also preserves
that whitespace; different assertion values must not become matching failures.
The collector decodes Azure task log JSON strings before signature normalization;
unsupported response shapes remain explicit gaps rather than becoming log text.

Online acquisition records snapshots under `snapshots`. A `--replay-directory`
containing those snapshots replays commands without running `gh`, `hlx`, or `az`.
Helix console downloads additionally have a `.console.json` sidecar containing
the final 120 lines and source URL; include it in a replay. The collector
never reads a local path from replayed command output. Missing console sidecars
remain explicit coverage gaps. Work-item passes support work-item comparisons,
not individual assertions absent from structured test results.
Missing replay commands fail explicitly. Replays are marked as such and never
claimed as live service validation. Replay uses local snapshots, not authenticated
source URLs.
Helix work-item lanes include both the physical queue and sorted submission
contexts from the Azure parent job and tagged test runs. Queue/name equality
alone cannot equate ordinary and FullFramework streams. The original queue and
submission contexts are retained separately; unknown submission identity cannot
establish a control.
Helix/queue-test task logs are acquired in full for job discovery, including
successful historical tasks. Diagnostic observations still retain only their
bounded failure excerpts. Missing discovery logs remain explicit gaps; an empty
diagnostic tail is not proof that no Helix work ran.
Job discovery recognizes the official `Work item 'X' in job 'queue (GUID)' failed`
monitor message as well as tagged IDs and job URLs; a separate URL is not required.

`CiEvidence.cs collect` exits nonzero on hard errors or incomplete mandatory evidence,
while retaining a partial dossier when possible. Offline comparison can inspect
that dossier, but cannot establish complete coverage or negative conclusions.
Compare metadata contract errors also fail explicitly. Successful acquisition of
failed CI is not a script failure.

## Azure DevOps authentication

Raw Azure REST requests use Azure CLI's existing signed-in context with the
DevOps resource explicitly selected. They do not initiate login. hlx may resolve
a different context; the bridge below applies to its separate requests.

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
    # Use the invocation-local compiler environment from the skill's collection example.
    dotnet run --file "$skill\scripts\CiEvidence.cs" --artifacts-path $appArtifacts `
        -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false `
        -- collect --build $buildUrl `
        --hlx-path $hlx --output-directory $evidence
} finally {
    $env:AZDO_TOKEN = $previousToken
    $token = $null
}
```

Use the existing authenticated context directly when hlx already resolves one.
Successful token acquisition does not prove project permissions: check collection
exit status and acquisition completeness as usual.
