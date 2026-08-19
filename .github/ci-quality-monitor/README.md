# CI Quality Investigator

The CI Quality Investigator is a GitHub Agentic Workflow that reviews public
Azure DevOps builds and creates issues for actionable, previously untracked SDK
build, test, and CI-integration failures. It does not require an Azure DevOps
service hook or credential. The workflow uses the GitHub Actions token for Copilot
inference through the scoped `copilot-requests: write` permission and keeps all
repository writes in GitHub AW safe-output jobs. See [`DESIGN.md`](DESIGN.md)
for policy, state semantics, and safety invariants.

## Architecture

The source workflow is
[`ci-quality-monitor.md`](../workflows/ci-quality-monitor.md). A deterministic
job runs before the agent:

1. Restore the processed-build ledger from the newest matching GitHub Actions
   cache entry.
2. Read [`pipelines.json`](pipelines.json) and query the latest 20 completed
   builds for each allowlisted pipeline and branch.
3. During daily branch polling, exclude PR builds and builds already present in
  the ledger. Event paths separately verify direct stable-branch failures and
  final PR validation associated with a merged PR.
4. Collect bounded timeline, public Helix work-item, TRX, artifact, and log
  evidence for new failures. TRX evidence includes aggregate result counts;
  hang/crash evidence includes the active-test line, host exit code, watchdog
  sequence, and dump-capture failures when present.
5. Save the updated ledger under a run-specific immutable cache key and a
  branch-scoped durable artifact checkpoint.
6. Skip the agent when there are no selected failed builds or actionable
  pipeline-health observations.
7. Give the agent a structured dossier when investigation is required.

The collector code follows these boundaries:

- `CiEvidenceCollector` coordinates one dossier and owns the shared Azure client
  cache. It delegates rather than implementing selection or evidence parsing.
- `BuildCandidateSelector` decides which manual, event, or scheduled builds are
  eligible and records their processed state.
- `FailureEvidenceCollector` retrieves and assembles timeline, task-log, test,
  related-build, and Helix evidence for one selected failure.
- `PipelineHealthMonitor` compares GitHub branch heads with recent Azure builds
  and records heartbeat observations.
- `collector-policy.mjs` owns pure build matching, heartbeat, timeline, and
  recurrence rules. These computations remain separate from the collector's
  external I/O and mutable state.
- `AzureDevOpsClient`, `HelixEvidenceClient`, and `HttpClient` own external
  communication. A client instance binds its endpoint context and HTTP
  dependency once; the Helix client also converts retrieved artifacts into
  Helix observations.
- Classification, parsing, fingerprints, normalization, and KBE matching remain
  pure functions. Serialized pipeline, observation, candidate, and dossier
  shapes are declared in [`types.d.ts`](types.d.ts); policy limits are collected
  in [`constants.mjs`](constants.mjs).

[`collect-ci-evidence.mjs`](collect-ci-evidence.mjs) is the CLI entry point and
test export surface. The run implementation lives in [`collector.mjs`](collector.mjs).

The workflow runs one daily routine and can be dispatched manually with a public
Azure DevOps build ID. The daily routine reconciles stable-branch events and
heartbeat state. Manual dispatch ignores the processed-build ledger for the
selected build, which makes repeatable validation possible.

## Public Data Boundary

Anonymous access currently works for public SDK build metadata and timeline
records, including structured task issues. Public Helix work-item APIs also
provide exit codes, console logs, TRX files, binlogs, and dumps; the collector
uses those artifacts to recover named test failures or classify result-less
timeouts and crashes. It retains recovered TRX pass/fail totals even when no
assertion failed, and extracts bounded hang/watchdog, host-exit, and dump-capture
details so a wrapper crash is not mistaken for a test assertion. Anonymous AzDO test-run queries return `404`, and direct
AzDO build-log downloads return `500` for the tested public SDK builds. The
collector records unavailable evidence and never invents missing test details.

Azure DevOps authentication is outside the current public-data boundary.

## Failure Model

The collector models each observation on independent axes:

  `source-checkout`, `dependency-restore`, `compilation`, `signing`, or
  `test-execution`
  `process-crash`
  validation, task logs, Helix TRX, console output, exit codes, or dumps

them and keeps observation provenance independent from both. Artifact download
cascades and generic Helix monitor parents are context only.
The durable policy and vocabulary are maintained in [`DESIGN.md`](DESIGN.md).
Each failure also exposes `issueCandidates`, the actionable observations from
the selected current build. Related-build observations are recurrence context
and cannot directly anchor an issue. Non-actionable current observations are
used to derive observations but are not duplicated in the agent dossier.
Named tests retain per-test fingerprints and a separate mechanism fingerprint.
Different tests can share one issue only when their mechanism fingerprints and
stable evidence match. The same test can therefore map to multiple issues when
it fails for different reasons. KBE recurrence requires the same test and
mechanism on a different commit; retries of one commit do not count.

Fingerprints are generated locally from phase, failure type, component, and
normalized mechanism; they are not downloaded from Azure or Helix. Evidence
normalization removes volatile GUIDs, timestamps, and machine-specific paths
and bounds text size. It is domain-specific stability and data minimization,
not HTML or command sanitization.

A branch heartbeat compares the registered GitHub head with recent AzDO builds.
It tolerates batched CI and records a miss only after the head is at least 90
minutes old. Reporting requires misses in two consecutive daily routines, so
ordinary detection latency is approximately 24–48 hours; 90 minutes is not the
reporting SLA.

## Relationship to `ci-analysis`

The monitor follows the same core investigation rules as the `ci-analysis`
skill: classify every failure independently, recover test results from crashed
or canceled Helix work items, suppress dependency cascades, cross-reference
existing issues per failure, and avoid calling a failure flaky,
infrastructure-owned, PR-related, or safe to retry without evidence.

The two workflows have different operating constraints. `ci-analysis` is an
interactive PR investigation that can query Build Analysis, PR metadata and
changed files, target-branch builds, build progression, binlogs, and additional
AzDO or Helix data. This scheduled monitor pays the retrieval cost once in its
deterministic collector and gives the agent a bounded public dossier. It does
not imply that Build Analysis, target-branch behavior, PR correlation, or a
binlog was checked when those facts are absent.

`ci-analysis` also routes broad engineering-service failures to
`dotnet/dnceng`. The monitor's constrained output can create issues only in the
SDK repository, so production runs report only repository-specific tests,
product build breaks, and SDK-owned CI integrations. A broad Azure DevOps,
Helix, machine-pool, or external-feed outage becomes a no-op with an explicit
routing reason rather than a misplaced SDK issue.

Every proposed issue must therefore contain a bounded root cause analysis with
the observed facts, the most specific supported causal chain, a confidence
level, alternatives or unknowns, and the next discriminating check. Recurrence
can establish that a failure is flaky, but it does not by itself establish why
the failure occurs. Checks that need broader context are recorded as suggested
investigation rather than represented as completed analysis.

The bounded dossier intentionally does not include dump contents, source-level
test mapping, PR changed-file correlation, tested-versus-landed tree comparison,
target-branch comparison, Build Analysis status, or full multi-commit
progression. Those are deeper interactive checks. The issue RCA must identify them as missing evidence when they are
needed to move from a high-confidence proximate cause, such as a watchdog
terminating a hung test host, to the underlying product or infrastructure cause.

## State and Bootstrap

Branch polling state is keyed by Azure DevOps organization, project, definition
ID, and branch. Automatic investigation state additionally retains trusted audit
contexts such as direct stable-branch delivery or merged-PR promotion. Each
entry retains up to 100 keys. Build attempt keys contain build ID, finish time,
and result, so a retried attempt that updates an existing build ID can be
analyzed again. Every daily poll re-reads the latest 20 builds to tolerate builds
finishing out of queue order.

The collector restores state through two layers before deciding whether to run
AI:

1. A branch-scoped Actions cache is the fast path. Immutable run-specific keys
  restore the most recent prefix match.
2. If the cache is missing or evicted, the collector restores the newest
  non-expired `ci-quality-state` artifact from the same branch. Checkpoints are
  retained for 30 days.

The new checkpoint is uploaded by the collector job before agent activation.
This gives scheduled runs **at-most-once automatic AI delivery** per processing
key: if inference, detection, or issue application later fails, the next
scheduled run does not automatically spend tokens on the same completed build.
Use manual dispatch with `build_id` for an intentional retry; manual collection
bypasses the processed-build ledger.

When no state can be restored, the run is marked as bootstrap. Bootstrap records
the current window and gathers at most one historical failure, but the
deterministic collector emits `should_run=false`, so no agent job is created.
This prevents a lost cache and artifact checkpoint from spending AI credits or
creating a burst of historical issues.

The checkpoint contains no credentials or untrusted executable content. It is
JSON build metadata only. Workflow concurrency queues scheduled runs under one
group, preventing two collectors from claiming the same newly completed build
at once.

## Issue Policy

Issue creation requires:

- substantially the same stable test/crash/timeout failure in the current build
  and at least one recent build from a different commit, or one specific deterministic
  build/YAML break after a passing build
- at least two distinct searches for existing open or recently closed issues
- evidence that the problem is SDK-owned rather than broad Helix or Azure
  DevOps infrastructure
- for test KBEs, a collector-generated Build Analysis `ErrorMessage` validated
  against the original TRX lines using Arcade's ordered `String.Contains`
  semantics
- an evidence-bounded root cause analysis that separates observed facts from
  inference, states `High`, `Medium`, or `Low` confidence, identifies remaining
  alternatives or unknowns, and leads with the next discriminating check

The agent follows two issue paths using GitHub AW's native `create-issue` safe
output:

- Ordinary build, YAML, heartbeat, Helix crash/timeout, and infrastructure
  issues must not request `Known Build Error` or contain Build Analysis JSON.
- A recurring named-test KBE may request `Known Build Error` only when its
  collector-generated pattern has been validated
  against the original TRX and the same test/mechanism appeared in a prior
  build. The agent must copy the collector-generated `## Error Message` values
  verbatim rather than constructing a pattern.

For both paths, the prompt requires `Build Information`, `Failure History`,
`Error Details`, `Root Cause Analysis`, and `Suggested Investigation`. The RCA
must explicitly include observed evidence, assessment, confidence, and
alternatives or unknowns. The body also carries the exact collector observation
fingerprint as a visible Build Information item so the agent can search for an
existing issue before filing. Native title deduplication is a second,
approximate safeguard.

GitHub AW applies the title prefix, fixed `agentic-workflows` and `cookie`
labels, and limit of three issue writes per run. Every filed issue therefore
enters the Issue Monster queue automatically. After issue creation succeeds,
the conclusion job dispatches Issue Monster once for each created issue number
so assignment does not wait for the scheduled queue scan. The agent may request
only the additional diagnostic labels allowlisted by the workflow.

## Adding Pipelines or Branches

Add entries to [`pipelines.json`](pipelines.json). Every entry must identify an
exact public organization, project, definition ID, repository, and fully
qualified branch name.

Only register branches that run post-merge public CI. A branch included only in
an Azure DevOps `pr` trigger has no continuous branch builds for this monitor to
observe. In particular, the current public SDK pipeline continuously builds
`main`; release branches should be added individually after their public
post-merge trigger is confirmed.

## Validation

Run the deterministic tests:

```powershell
node --test .github/ci-quality-monitor/test/*.test.mjs
```

Collect a known public build manually:

```powershell
node .github/ci-quality-monitor/collect-ci-evidence.mjs `
  --registry .github/ci-quality-monitor/pipelines.json `
  --output artifacts/tmp/ci-quality-monitor/dossier.json `
  --build-id 1523365
```

Compile the agentic workflow after any source change:

```powershell
& 'C:\Program Files\GitHub CLI\gh.exe' aw compile `
  .github/workflows/ci-quality-monitor.md
```

The generated `.lock.yml` must be committed with its source workflow.
