# CI Quality Investigator Design

## Goals

The CI Quality Investigator detects actionable failures in public `dotnet/sdk`
CI, gathers bounded evidence before invoking an agent, and avoids filing issues
for duplicate signals or downstream cascades.

The design prioritizes these invariants:

1. Deterministic code selects builds and collects evidence before AI runs.
2. Automatic delivery processes a build attempt and audit context at most once.
3. Issue candidates represent root mechanisms from the selected build, not
   related-build context or generic parent failures.
4. Every issue is grounded in links and evidence present in the collector
   dossier.
5. Repository issues are limited to SDK-owned product and CI-integration
   failures. Broad Azure DevOps, Helix, machine-pool, and external-service
   incidents are not filed in this repository.

## System Boundary

The source workflow is
[`../workflows/ci-quality-monitor.md`](../workflows/ci-quality-monitor.md).
[`collector.mjs`](collector.mjs) coordinates build selection and evidence
collection. Azure DevOps, Helix, and GitHub clients own external communication;
policy and parsing modules remain independent from transport.

The collector uses anonymous public Azure DevOps and Helix endpoints. Missing
or inaccessible evidence remains explicit in the dossier and must not be
replaced with an inferred failure detail.

## Delivery Paths

The workflow supports four delivery paths:

| Trigger | Behavior |
| --- | --- |
| Completed Azure check suite | Resolve the public SDK build and inspect a failed direct stable-branch build. |
| Merged pull request | Locate the final public SDK validation and inspect it only when the PR merged into an allowlisted stable target after a failed validation. |
| Daily schedule | Reconcile missed stable-branch events and check registered branch heartbeats. |
| Manual dispatch | Inspect the requested completed public build without consulting the automatic processed-build ledger. |

The current automatic policy covers stable-branch incidents. Open pull request
failures are not treated as repository-wide incidents because the pull request
itself is a plausible cause.

## Failure Model

Each observation has independent fields:

- `phase` identifies where execution stopped, such as pipeline validation,
  source checkout, dependency restore, compilation, signing, or test execution.
- `failureType` identifies what happened, such as a configuration error,
  network failure, compiler error, test assertion, timeout, or process crash.
- `evidenceSources` identifies how the conclusion was established, such as an
  Azure validation diagnostic, task log, Helix TRX, console output, exit code,
  or dump.

Phase does not imply cause. A network failure may occur during restore or inside
a test wrapper, and a test-execution failure may be an assertion, timeout,
process crash, or infrastructure outage.

Observations classified as downstream failures, missing artifacts caused by an
earlier failure, or unavailable evidence are retained as context but cannot
anchor an issue. Related builds establish recurrence only; they do not produce
current issue candidates.

## Fingerprints And Recurrence

Fingerprints are generated from normalized phase, failure type, component, and
mechanism. Normalization removes volatile identifiers, timestamps, and
machine-specific paths. Evidence source order is not part of identity.

Named tests retain both a per-test fingerprint and a mechanism fingerprint.
Known Build Error recurrence requires the same test and mechanism on a
different commit. Retries of one commit do not establish recurrence.

## Processing State

Automatic processing keys combine the Azure build attempt with its trusted
audit context. Finish time and result distinguish an updated Azure retry that
reuses a build ID. Stable direct-build and merged-PR contexts are distinct so a
failed validation can be reconsidered after its content is integrated.

State restoration uses a branch-scoped Actions cache first and a durable
workflow artifact checkpoint second. The collector uploads the updated state
before agent activation. If inference or issue application later fails, a
scheduled run does not automatically spend tokens on the same processing key
again. Manual dispatch intentionally bypasses this ledger.

When neither state source is available, the collector bootstraps its build
window without activating the agent. This prevents state loss from causing a
burst of historical investigations.

## Scope Configuration

[`pipelines.json`](pipelines.json) is the allowlist for public pipeline and
branch monitoring. Register a branch only after verifying that it runs direct
post-merge public CI. A branch used only as a pull request target must not be
heartbeat-polled.

Internal or authenticated CI is outside this workflow's data boundary until an
explicit credential and evidence design is added.
