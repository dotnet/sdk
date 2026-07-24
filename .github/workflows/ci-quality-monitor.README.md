# CI Quality Investigator 🕵️

## Summary

An agentic workflow that investigates CI evidence and files
actionable build, test, and infrastructure issues in the .NET SDK repository.

Eligible issues can then be assigned to Copilot by the [Issue Monster](https://github.com/dotnet/sdk/pull/55243) for a proposed fix.

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
6. `cookie` is applied only when a finding is eligible for Issue Monster.
  Stable live incidents and technical or infrastructure debt receive their
  corresponding trusted labels.
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
| Monitoring category | The provenance class used to interpret a failure: `stable-branch`, `infra-pr`, or `developer-pr`. |
| Monitoring priority | The response policy derived from trusted build and PR metadata: `HIGH`, `MED`, or `LOW`. Manual callers and the AI agent cannot choose it. |
| Actionable mechanism | A specific root failure after generic parent failures, dependency cancellations, artifact cascades, and duplicates have been removed. |
| Recurrence | The same stable test and failure mechanism observed on independent refs. Repeated attempts of one PR are not independent recurrence because that PR may deterministically contain the defect. |

## Workflow Intent

The workflow finds actionable, previously untracked CI-quality problems without
treating every red PR as a repository incident.

It must:

1. Collect bounded public Azure DevOps and Helix evidence deterministically
	before AI runs.
2. Derive monitoring category and priority from trusted Azure and GitHub
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

## Monitoring Category

| Category | Included builds | Interpretation | Implementation status |
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

## Trigger Category

| Trigger | Candidate monitoring categories | Policy |
| --- | --- | --- |
| Azure `check_suite: completed` | Stable branch; infrastructure PR | For a non-success `azure-pipelines` suite, resolve and verify the definition `101` build. A direct allowlisted stable-branch failure is HIGH. Planned MED support may accept verified codeflow PR failures. Ordinary open PR failures do not file in the HIGH-only milestone. |
| `pull_request: closed` with `merged == true` | Stable branch; infrastructure PR lifecycle | A merge is an evidence and lifecycle event, not a failure by itself. Link the PR to its final Azure validation. If that final validation failed and the target is allowlisted as stable, create a HIGH candidate. A successful PR build creates no incident. Compare tested and landed trees when claiming exact landed-content validation. |
| Daily routine | Stable branch; planned developer PR | Reconcile missed stable-branch check-suite events and poll only branches verified to have direct public branch CI. Detect a branch head for which Azure created no build record after two daily polls. The planned LOW extension will use this same run to select at most three newest unprocessed, completed, non-draft failures from distinct PRs and apply the independent-recurrence policy before AI may file anything. |
| Manual dispatch with `build_id` | Any category | Accept any completed registered public build for investigation. Derive category and priority from trusted metadata; the caller cannot promote a build. Manual runs may intentionally bypass automatic processing state for repeatable evaluation. |
| Fork-only evaluation push | Test harness only | Disposable validation mechanism. It is not a production monitoring category or production trigger policy. |

## Audit Processing and Promotion

Automatic deduplication uses both the Azure build attempt and its trusted audit
context:

```text
<build ID>:<finish time>:<result>|<monitoring category>:<context identity>
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
| Check-suite event | Resolve and verify the Azure build, derive its monitoring category and context identity, and mark that audit key processed before AI. Redelivery of the suite is suppressed. |
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

## Issue Category

The collector can classify the following failure surfaces. The examples are
historical public builds used to validate evidence collection; inclusion here
does not retroactively assign production priority.

| Issue category | Example | Potential issue description | Detection path in the evaluation |
| --- | --- | --- | --- |
| YAML pre-flight | [Build 1521345](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1521345) | `.vsts-pr.yml` was rejected before jobs started because `useFullyQualifiedTestName` was not a valid template parameter. | Azure validation result; neutral Azure check suite; manually replayed through the collector. |
| Checkout or setup | [Build 1523420](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1523420) | Checkout failed across multiple jobs after `git fetch` exited with code 128. | Azure timeline and task-log evidence; manually replayed through the collector. |
| Restore or feed | [Build 1523525](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1523525) | NuGet restore failed with HTTP 503; named tests also reported NU1301 against the dotnet6 feed. | Azure task logs and Helix TRX evidence; manually replayed through the collector. |
| Compiler break | [Build 1525235](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525235) | `CS0114` in `RunReadyToRunCompiler.TaskEnvironment` failed multiple build legs before tests. | Azure timeline and logs; manually replayed through the collector. |
| Stable release build break | [Build 1522972](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1522972) | On `release/8.0.4xx`, `SignToolTask` failed with `MSB4018` because `sn.exe` produced an exec-format error on Linux and macOS. | Direct stable-branch Azure build; schedule/manual evaluation. This is representative of HIGH stable-branch monitoring. |
| Multiple named tests | [Build 1525292](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525292) | Four named tests failed; two package tests shared an HTTP 503 mechanism and required mechanism-aware grouping. | Helix work-item APIs and TRX parsing on a direct `main` build. |
| Helix hang or host crash | [Build 1524763](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1524763), [PR #55431](https://github.com/dotnet/sdk/pull/55431) | `BrowserDiagnostics` remained active for 50 minutes, the watchdog captured hang dumps, 83 streamed results contained no failed assertion, and the test host exited 137/work-item exit 7. A potential issue should describe the proximate watchdog termination separately from the still-unknown underlying hang. | Azure check suite identified the PR build; the deterministic collector recovered Helix console, TRX totals, exit codes, and dump links; AI produced the bounded RCA. Under the planned developer-PR policy this would require independent recurrence before a KBE could be filed. |
| Pipeline not triggered | Synthetic heartbeat case | A registered direct-CI branch head remained without any Azure build record across two daily polls. The head must first be at least 90 minutes old, so daily scheduling makes the practical detection latency approximately 24–48 hours. Invalid YAML with a recorded Azure build is not this category. | Daily GitHub-to-Azure heartbeat only; no completion event can exist when no Azure run exists. |
| Internal or official CI | `dnceng/internal`, including definition `286` | Not scanned. Internal evidence may contain credentials or require authentication and is outside the public monitor registry. | None. |

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
- Manual evaluation does not change trusted monitoring category or priority.
