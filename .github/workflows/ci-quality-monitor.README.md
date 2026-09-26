# CI Quality Investigator 🕵️

## Summary

An agentic workflow that investigates CI evidence and files
actionable build, test, and infrastructure issues in the .NET SDK repository.

Eligible issues are dispatched to the [Issue Monster](https://github.com/dotnet/sdk/pull/55243) immediately after creation so Copilot can propose a fix.

This specification defines the investigator's monitoring policy and separates
current behavior from planned categories and priorities.

## Expected Impact

- Reduce engineering time spent discovering and investigating CI failures.
- Reduce the time required to detect a CI failure.
- Reduce the time/SLA from failure detection to a proposed fix.
- Reduce the operational burden, compute cost, and elapsed time consumed by
  broken or repeatedly retried CI.
- Accelerate development by reducing the time that CI failures block PR merges.
- Reduce accumulated build, test, and infrastructure technical debt.

## Success Criteria

1. The workflow does not overwhelm repository contributors with duplicate,
  speculative, or low-quality issues.
2. Every filed issue includes a reasonable root cause analysis and empirical,
  verifiable build evidence with links to the relevant build, task, test,
  console output, or artifact.
3. The workflow does not file an issue when no actionable failure exists, when
  the signal is only a downstream cascade, or when an existing issue already
  covers the same mechanism.
4. Automatic delivery does not spend AI tokens re-analyzing an audit context
  that has already been processed. A retry or trusted priority promotion is
  audited only under the context-qualified rules below.
5. PR analysis does not spend tokens or file repository-wide findings for failures
  caused only by the PR's own changes.
6. Production monitoring applies `cookie` so filed issues are eligible for Issue
  Monster, applies `live-build-incident` to every filed issue, and dispatches
  Issue Monster with each successfully created issue number. Technical or
  infrastructure debt receives its corresponding allowlisted label.
7. Test flakes are filed only as validated Known Build Errors. A KBE match must
  not hide or permit unrelated failures in the same CI run.
8. The investigator detects pipeline YAML rejection, including failures that
  occur before any job or timeline record exists.
9. The investigator detects Helix work-item hangs and crashes and distinguishes
  them from test assertion failures or post-test harness failures.
10. Added token spend is feasibly under the budget of net savings from the workflow.
11. The investigator detects named test failures and preserves enough evidence
   to distinguish independent test mechanisms.
12. Event-driven paths begin investigation when CI completes unsuccessfully,
   without waiting for a maintainer to notice or request an investigation.
13. The workflow has potential to be broadly applied at an organizational level to scale reduced costs & SLA impact.

## Terms

| Term | Definition |
| --- | --- |
| Public SDK pipeline | Azure DevOps definition `101`, `dotnet-sdk-public-ci`, in `dnceng-public/public`. Its YAML is [`.vsts-pr.yml`](../../.vsts-pr.yml). |
| Stable branch | An explicitly allowlisted integration or servicing branch. The intended set is `main`, `release/dnup`, and `release/<major>.0.<band>xx` for bands 1 through 4 and supported majors 8 through 11. Branch existence does not imply that direct branch CI is enabled. |
| Direct stable-branch build | An Azure build whose source is `refs/heads/<stable-branch>`, rather than `refs/pull/<number>/merge`. |
| PR validation build | A definition `101` build of GitHub's synthetic `refs/pull/<number>/merge` ref. It tests the PR head merged into the target branch state available when Azure queued the build. |
| Infrastructure PR | An automated integration PR. The planned first subtype is Maestro codeflow: sender `dotnet-maestro`, a `darc-*` head branch, and a source-code or dependency-flow title. Branding and automated interbranch merge PRs may be added later. |
| Developer PR | A non-infrastructure development PR, whether authored by a person or a coding agent. Backports are also treated as developer PRs unless a later policy explicitly enrolls them. |
| Monitoring scope | The provenance class used to interpret a failure: `stable-branch`, `infra-pr`, or `developer-pr`. |
| Monitoring priority | The response policy derived from trusted build and PR metadata: `HIGH`, `MED`, or `LOW`. Manual callers and the AI agent cannot choose it. |
| Actionable mechanism | A specific root failure after generic parent failures, dependency cancellations, artifact cascades, and duplicates have been removed. |
| Recurrence | The same stable test and failure mechanism observed on independent refs. Repeated attempts of one PR are not independent recurrence because that PR may deterministically contain the defect. |

## Workflow Intent

The workflow finds actionable, previously untracked CI-quality problems without
treating every red PR as a repository incident.

It must:

1. Collect bounded public Azure DevOps and Helix evidence deterministically
	before AI runs.
2. Derive monitoring scope and priority from trusted Azure and GitHub
	metadata.
3. Separate failures in integrated branch content from failures that may have
	been introduced by an open PR.
4. Suppress duplicate issues, generic parent failures, and downstream cascades.
5. Require an evidence-bounded root cause analysis in every filed issue.
6. Mark each automatic build attempt processed before AI runs so event and schedule
	delivery cannot spend AI twice.
7. Keep internal or official CI outside scope until an authenticated internal
	evidence path is deliberately designed.

The current implementation milestone is **HIGH priority only**. MED and LOW are
specified below for future expansion and to clarify design intent.

## Monitoring Scope

| Scope | Included builds | Interpretation | Implementation status |
| --- | --- | --- | --- |
| Stable branch | Failed direct builds on allowlisted stable branches; a failed final PR validation linked by a merged-PR event to an allowlisted stable target | Content is integrated, or a failed validation was nevertheless followed by integration. Treat actionable mechanisms as live incidents. A merge event alone is not a failure. | **Current milestone** |
| Infrastructure PR | Automated integration PRs. Start with verified Maestro codeflow PRs only. Branding and interbranch merge PRs are later extensions. | A failure can be flow infrastructure, a flake, or a valid incompatibility in incoming changes. It is not equivalent to a stable-branch incident. | Planned |
| Developer PR | Completed, non-draft PR builds not classified as infrastructure PRs | The PR itself is a plausible cause. Do not infer a flake from repeated attempts of that PR. | Planned |

Stable targets and direct-CI polling are separate concepts. `stableBranches`
defines which target branches receive stable-branch semantics. `branches`
defines which branches actually have direct definition `101` builds and may be
polled or checked for a missing-run heartbeat. Do not add a branch to heartbeat
polling until direct branch CI is verified; otherwise the monitor would report a
false outage.

### Merged PR Evidence

A final PR build validates a synthetic merge created from the PR head and the
target branch state available when Azure queued the build. The target branch can
change before the PR actually merges, so that build does not automatically prove
that the exact content which landed was validated.

The merged-PR trigger links the final PR build to the landed commit. The workflow
may describe the PR build as exact landed-content evidence only when their Git
trees match. If they differ, the PR result remains useful historical context but
does not replace a direct build of the stable branch. PR
[#55280](https://github.com/dotnet/sdk/pull/55280) is a verified example where
the target branch moved between validation and merge.

The current collector records merge metadata but does not perform that tree
comparison. Therefore current issues must treat exact landed-content validation
as unknown unless another evidence source establishes it.

## Trigger Category

| Trigger | Candidate monitoring scopes | Policy |
| --- | --- | --- |
| Azure `check_suite: completed` | Stable branch; infrastructure PR | For a non-success `azure-pipelines` suite, resolve and verify the definition `101` build. A direct allowlisted stable-branch failure is HIGH. Planned MED support may accept verified codeflow PR failures. Ordinary open PR failures do not file in the HIGH-only milestone. |
| `pull_request: closed` with `merged == true` | Stable branch; infrastructure PR lifecycle | A merge is an evidence and lifecycle event, not a failure by itself. Link the PR to its final Azure validation. If that final validation failed and the target is allowlisted as stable, create a HIGH candidate. A successful PR build creates no incident. The current collector does not compare tested and landed trees, so it cannot claim exact landed-content validation. |
| Daily routine | Stable branch; planned developer PR | Reconcile missed stable-branch check-suite events and poll only branches verified to have direct public branch CI. Detect a branch head for which Azure created no build record after two daily polls. The planned LOW extension will use this same run to select at most three newest unprocessed, completed, non-draft failures from distinct PRs and apply the independent-recurrence policy before AI may file anything. |
| Manual dispatch with `build_id` | Diagnostic only | Accept any completed registered public build for repeatable investigation and bypass automatic processing state. The manual path follows normal ownership and recurrence rules, does not assign a production monitoring scope or priority, and therefore cannot promote a build to HIGH. |

## Audit Processing and Promotion

Automatic deduplication uses both the Azure build attempt and its trusted audit
context:

```text
<build ID>:<finish time>:<result>|<monitoring scope>:<context identity>
```

| Audit context | Context identity | Re-audit rule |
| --- | --- | --- |
| Direct stable branch | `stable-direct:<full branch ref>` | Audit once at HIGH for that Azure attempt. Event and daily reconciliation share this key. |
| Infrastructure PR | `infra-pr:<PR number>` | Audit once at MED while the PR is open. Repeated delivery and repeated analysis of the same Azure attempt are suppressed. |
| Developer PR | `developer-pr:<PR number>` | Audit once at LOW after the daily sampler and independent-recurrence gate select it. |
| Merged into stable branch | `stable-merge:<PR number>:<landed commit SHA>` | Permit one HIGH audit even if the same Azure attempt was already processed under an infrastructure-PR or developer-PR context. Redelivery of the same merge event is suppressed. |

Finish time and result distinguish an updated Azure retry that reuses a build ID
from the earlier attempt. Context identity distinguishes a meaningful priority
promotion from duplicate delivery of the same evidence.

Before AI runs, the collector records the audit key in its pipeline state. State
is restored first from the newest Actions cache and, if that is unavailable,
from the newest non-expired `ci-quality-state` artifact for the workflow branch.
The updated checkpoint is uploaded before agent activation. An inference or
issue-output failure therefore does not cause the next automatic delivery to
spend AI on the same audit context again.

| Delivery path | Processing behavior |
| --- | --- |
| Check-suite event | Resolve and verify the Azure build, derive its monitoring scope and context identity, and mark that audit key processed before AI. Redelivery of the suite is suppressed. |
| Daily stable-branch reconciliation | Derive the same `stable-direct` key as event delivery, so a build seen through either path is audited once. |
| Updated Azure retry | A changed finish time or result creates a distinct audit key and is eligible in the same context. |
| Manual `build_id` | Bypass automatic processing state for repeatable investigation, while still deriving category and priority from trusted metadata. |

The merged-PR event usually does not have a new Azure run. It locates the
final completed definition `101` PR build by PR number and final head SHA, then
create the `stable-merge` audit context from trusted GitHub merge metadata. This
is an intentional promotion audit, not an unrestricted rerun. Only a transition
to a higher-priority context permits reuse of the Azure evidence; events at the
same or lower priority remain processed.

The promotion audit coordinates with issue deduplication. The issue body carries
the exact collector failure fingerprint as visible Build Information, and the agent searches for that value
before filing. If an issue already tracks the mechanism, the agent does not open
an indistinguishable duplicate. Native title deduplication provides an
additional approximate safeguard. Under the current minimal implementation,
automatic relabeling of an existing lower-priority issue is not enforced; the
agent reports the existing issue instead. If no issue exists, the HIGH audit
creates the live incident normally.

## Monitoring Priority

| Priority | Category and filing policy | Trusted labels | Implementation status |
| --- | --- | --- | --- |
| HIGH | Stable-branch incidents. File every distinct actionable root mechanism after duplicate and cascade suppression. A successful build or merge event never creates an issue. If one run exceeds the issue limit, the implementation must aggregate or explicitly report overflow rather than silently discard mechanisms. | `agentic-workflows`, `Test Debt`, `live-build-incident` | **Current milestone** |
| MED | Verified codeflow PR findings. File only when analysis indicates a CI-quality or integration-infrastructure problem rather than a valid incompatibility in incoming changes. A non-automated commit added to the codeflow PR disqualifies automatic filing. Maintain at most one open finding issue per open codeflow PR and close or resolve it when that PR closes. | `agentic-workflows`, `Test Debt`, planned `infrastructure-ai-finding` | Planned |
| LOW | Developer PR findings. KBE only; no ordinary build, setup, restore, or infrastructure issue. Require the same named test and mechanism on at least two independent refs within a bounded window, excluding the current PR. A proposed starting window is the newest 60 completed builds within 14 days. | `agentic-workflows`, `Known Build Error`, `Test Debt` | Planned |

"Always file" for HIGH means every distinct **actionable root mechanism**, not
every red task or every merge. Generic Helix monitors, dependency cancellations,
artifact download cascades, duplicate fingerprints, and successful events remain
non-issues.

For MED lifecycle management, the issue should carry a trusted hidden PR marker.
New findings update the existing issue for that PR. A PR-close workflow may close
that linked issue, but must not close a cross-PR KBE merely because one codeflow
PR closed.

## Failure Taxonomy

Issue boundaries follow materially different causal mechanisms, not a flat list
of CI task names. Phase and evidence source provide context but do not by
themselves create separate issues. One package-service outage may surface during
restore and inside test wrappers; those observations can form one incident when
the endpoint and stable mechanism match. A compiler diagnostic and a timeout
remain separate even when both happen under a build stage.

### Phases

`phase` identifies the operation that was active when the failure surfaced. It
does not identify the cause and does not by itself determine issue boundaries.
The current collector emits the following values:

| Phase | Meaning |
| --- | --- |
| `pipeline-scheduling` | GitHub and Azure build history indicate that an expected pipeline run was not queued. |
| `pipeline-validation` | Azure rejected the pipeline definition or expanded YAML before execution began. |
| `pipeline-startup` | Azure created a failed build record without validation diagnostics or executable timeline records. |
| `source-checkout` | Repository fetch, checkout, or source availability failed. |
| `environment-setup` | Agent, container, tool acquisition, installation, or other prerequisite setup failed. |
| `dependency-restore` | NuGet restore, package resolution, feed access, or package policy evaluation failed. |
| `compilation` | Compiler, MSBuild, SDK build task, or another build operation failed. |
| `signing` | Signing or signature-tool execution failed. |
| `artifact-transfer` | A produced or required build artifact could not be found or transferred. This is normally cascade context rather than an actionable root. |
| `test-orchestration` | Test dispatch or a parent Helix-monitor operation reported a downstream failure. This is normally context when a specific child failure exists. |
| `test-execution` | A test assertion, test-host failure, timeout, crash, or execution-time dependency failure surfaced while tests were running. |
| `test-post-processing` | Tests completed, but result processing or harness shutdown subsequently failed. |
| `unknown` | Available evidence cannot locate the failed operation more precisely. This fallback must not be promoted to a specific phase by inference. |

### Failure Types

`failureType` describes the observed mechanism independently of where it
surfaced. The current collector emits the following values:

| Failure type | Meaning |
| --- | --- |
| `missing-execution` | An expected pipeline or execution record is absent. Heartbeat observations become actionable only after the configured consecutive-miss policy. |
| `configuration-error` | Invalid pipeline, YAML, template, or configuration input prevented normal execution. |
| `source-unavailable` | Required repository content or a source ref could not be fetched or found. |
| `authentication-failure` | Credentials were missing, rejected, forbidden, or unauthorized. |
| `network-failure` | A remote dependency failed through throttling, service unavailability, connection failure, or another transport error. |
| `package-policy-error` | Restore was blocked by package policy, such as a reported vulnerability. |
| `package-resolution-error` | NuGet or restore could not resolve or retrieve required packages for a reason not classified more specifically. |
| `compiler-error` | A C# compiler diagnostic caused compilation to fail. |
| `build-task-error` | An MSBuild, NETSDK, or other build-task diagnostic failed the build without a more specific mechanism classification. |
| `tool-execution-error` | A required setup, signing, or build tool failed to execute successfully. |
| `test-assertion` | Structured test results contain one or more named failed tests. |
| `timeout` | Test execution exceeded its allowed duration or a watchdog identified a hang. |
| `process-crash` | A test host or related process crashed, asserted, overflowed its stack, or produced crash evidence. |
| `process-termination` | A process ended through an external or otherwise unexplained termination code without evidence sufficient to classify a crash or timeout. |
| `harness-error` | Tests completed, but the test harness or post-processing path failed afterward. |
| `infrastructure-unavailable` | Required test infrastructure, machine capacity, device, or agent connection was unavailable. |
| `artifact-missing` | A required artifact was absent. The collector normally treats this as a downstream cascade rather than an actionable root. |
| `downstream-failure` | A parent orchestration task reports failure because a more specific child operation failed. The collector treats it as context. |
| `evidence-unavailable` | Evidence retrieval itself failed, so the underlying build or test mechanism is not established. This is non-actionable. |
| `unknown-error` | The operation failed, but available evidence does not support a more specific mechanism. |

### Examples

| Example | Phase | Failure type | Evidence sources |
| --- | --- | --- | --- |
| YAML pre-flight [1521345](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1521345) | `pipeline-validation` | `configuration-error` | Azure validation |
| Checkout [1523420](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1523420) | `source-checkout` | `source-unavailable` | Azure timeline and task log |
| NuGet 503 [1523525](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1523525) | `dependency-restore`; wrapped occurrences at `test-execution` | `network-failure` | Azure task log; Helix TRX |
| Compiler break [1525235](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525235) | `compilation` | `compiler-error` | Azure task log (`CS0114`) |
| Signing tool break [1522972](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1522972) | `signing` | `tool-execution-error` | Azure task log (`MSB4018`, `sn.exe`) |
| Named tests [1525292](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525292) | `test-execution` | `test-assertion` or wrapped `network-failure` | Helix TRX |
| Hang/watchdog [1524763](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1524763) | `test-execution` | `timeout` | Helix console, exit code, and dump |
| Pipeline not triggered | `pipeline-scheduling` | `missing-execution` | GitHub branch and Azure build history |

## Scope Boundaries

- The registry currently covers public `dotnet/sdk` definition `101`; internal
  definition `286` is not scanned.
- A stable branch may be recognized as a stable merge target without having
  direct branch CI. Such a branch must not be heartbeat-polled until direct CI
  is verified.
- HIGH is the only production filing policy in the current milestone.
- Infrastructure PR MED support starts with Maestro codeflow only. Branding,
  automated interbranch merge, backport, and other bot PR policies require
  separate enrollment decisions.
- Developer PR LOW support requires independent recurrence data and bounded
  daily sampling before it can be enabled.
- Manual investigation bypasses automatic processing state but assigns no
  production monitoring scope or priority.
