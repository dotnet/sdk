# CI Quality Monitor

The CI Quality Monitor is a scheduled GitHub Agentic Workflow that polls public
Azure DevOps builds and previews issues for recurring, previously untracked SDK
build and test failures. It does not require an Azure DevOps service hook or
credential. The initial experiment uses the GitHub Actions token for Copilot
inference through `copilot-requests: write`; migrate back to the shared PAT pool
before enabling the workflow in dotnet/sdk production.

## Architecture

The source workflow is
[`ci-quality-monitor.md`](../workflows/ci-quality-monitor.md). A deterministic
job runs before the agent:

1. Restore the processed-build ledger from the newest matching GitHub Actions
   cache entry.
2. Read [`pipelines.json`](pipelines.json) and query the latest 20 completed
   builds for each allowlisted pipeline and branch.
3. Exclude PR builds and builds already present in the ledger.
4. Collect bounded timeline, public Helix work-item, TRX, artifact, and log
  evidence for new failures.
5. Save the updated ledger under a run-specific immutable cache key.
6. Skip the agent when there are no new failed builds.
7. Give the agent a structured dossier when investigation is required.

The workflow runs every 30 minutes and can be dispatched manually with a public
Azure DevOps build ID. Manual dispatch ignores the processed-build ledger for
the selected build, which makes repeatable validation possible.

## Public Data Boundary

Anonymous access currently works for public SDK build metadata and timeline
records, including structured task issues. Public Helix work-item APIs also
provide exit codes, console logs, TRX files, binlogs, and dumps; the collector
uses those artifacts to recover named test failures or classify result-less
timeouts and crashes. Anonymous AzDO test-run queries return `404`, and direct
AzDO build-log downloads return `500` for the tested public SDK builds. The
collector records unavailable evidence and never invents missing test details.

Adding Azure DevOps authentication later would improve non-Helix test evidence,
but is not required for the initial experiment.

## Failure Model

The collector emits independent observations for pipeline configuration,
startup, setup, restore, build, test, and Helix work-item failures. Artifact
download cascades and generic Helix monitor parents are context only.

Named tests retain per-test signatures and a separate mechanism signature.
Different tests can share one issue only when their mechanism signatures and
stable evidence match. The same test can therefore map to multiple issues when
it fails for different reasons.

A branch heartbeat compares the registered GitHub head with recent AzDO builds.
It tolerates batched CI, waits 90 minutes, and requires two consecutive misses
before reporting that a pipeline did not start.

## State and Bootstrap

State is keyed by Azure DevOps organization, project, definition ID, and branch.
Each entry retains up to 100 build IDs, while every poll re-reads the latest 20
builds to tolerate builds finishing out of queue order.

When no state can be restored, the run is marked as bootstrap. Bootstrap records
the current window and gathers at most one historical failure, but policy
requires the agent to call `noop`. This prevents a lost or expired cache from
creating a burst of historical issues.

## Issue Policy

Issue creation is initially configured in staged mode. A preview requires:

- substantially the same stable test/crash/timeout failure in the current build
  and at least one recent failed build, or one specific deterministic
  build/YAML break after a passing build
- at least two distinct searches for existing open or recently closed issues
- evidence that the problem is SDK-owned rather than broad Helix or Azure
  DevOps infrastructure
- a specific Build Analysis `ErrorMessage` copied from accessible evidence

Previews receive the existing `agentic-workflows` and `Known Build Error`
labels. The monitor never applies `cookie`. The normal issue-triage workflow can
add `Test Debt`, an area label, and `cookie` when the issue describes bounded
work suitable for Issue Monster.

At most three distinct mechanism issues are previewed per run. After maintainers
review staged output quality, remove `staged: true` from the workflow safe
outputs to enable issue creation.

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
node --test .github/ci-quality-monitor/collect-ci-evidence.test.mjs
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