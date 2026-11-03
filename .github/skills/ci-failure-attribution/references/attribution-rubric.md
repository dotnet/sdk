# Attribution and confidence

## Independent axes

| Attribution | Required support |
| --- | --- |
| PR-related / New-commit-related | A supported link from changed behavior to the observed mechanism. Executed comparable controls strengthen it. Intermittency does not exclude it. |
| Pre-existing | The same material mechanism in comparable evidence without the PR changes or before the stated standalone window. Note unresolved exacerbation by new changes. |
| External infrastructure | Evidence of an external CI service, agent, queue, or transport mechanism, after accounting for changed source/configuration that could produce similar symptoms. |
| Inconclusive | Missing controls, incompatible runs, conflicting signals, incomplete identity/coverage, or no causal link. State the next discriminating check. |

Set **Behavior** to **Intermittent**, **Deterministic**, or **Unknown** based on
run history or a clear deterministic error.

`Pre-existing` applies to the commit range under test. If the previous matching
build already failed with the same error before the new commits were added, mark
the failure as `Pre-existing` for this commit range, even if an earlier build
introduced it.

## Evidence sufficiency and sampling

Answer three separate questions:

1. Did this failure happen before the change?
2. Does this failure happen intermittently?
3. Did the change cause the failure or make it happen more often?

Proving (1) or (2) does not answer (3):

- A matching failure without the PR changes proves the problem existed before,
  and one pass plus one failure under matching conditions proves it is
  intermittent. Neither result proves that the PR had no effect.
- If code and input checks do not rule out the PR, check more matching builds
  (both passes and failures) and record how many runs you checked.
- If retention limits, time limits, or missing controls prevent enough
  comparisons, report `pre-existence demonstrated; PR impact unassessed` or
  `observed intermittency; PR impact unresolved`.

## Qualitative confidence

Set **Confidence** to **High**, **Medium**, or **Low**:

- **High:** clear logs, artifacts, and matching control builds support the
  verdict, and you checked other likely causes.
- **Medium:** the evidence supports one explanation, but a control build, causal
  step, or alternative cause is still unverified.
- **Low:** the evidence points to one explanation, but comparisons are indirect
  or key artifacts are missing. If the evidence does not favor one cause over
  another, set **Attribution** to `Inconclusive`.

You can have high confidence in the immediate symptom (for example, a watchdog
killed a hung process) and low confidence in the root cause (why the process
hung). Report both when they differ.

## Comparability and change windows

Before using another build as a control, confirm that the relevant settings
match: repository, organization, project, pipeline, job, OS, RID, build
configuration, Helix queue, test shard, dependencies, toolset, pipeline
parameters, and commits. Because PR builds and rolling builds can run different
jobs or test sets in the same pipeline, check the pipeline YAML at the tested
commit.

- **Pull requests:** compare against the target commit from the tested merge
  commit. Do not use the target branch's current head.
- **Standalone builds:** check both the previous matching build and the last
  passing build. Use git history to verify commit order, because builds can
  finish out of commit order.
- **Rolling builds:** diff the full commit range and connect a specific change
  to the error before naming a single commit. If you cannot narrow it to one
  commit, report the full commit range.
- **Same-SHA scheduled, manual, or repeated runs:** note that no code changed,
  then check dependencies, external resources, pipeline parameters, machine
  state, and existing code bugs.

## Evidence traps

- **Build Analysis checks:** confirm that the Build Analysis report covers the
  exact build and failure. A green check that was manually overridden can still
  hide unmatched failures. If a branch is missing an expected Build Analysis
  report, note that gap.
- **Generic parent errors and cascades:** generic `Work Item` failures, exit
  codes, `Monitor Helix Jobs` failures, and missing-artifact cascades are
  symptoms. Dig into the child logs to find the root error. Group failures only
  when they share the same root error.
- **CI infrastructure vs. dependency or config errors:** a package feed HTTP
  401/403, DNS error, expired credential, or package vulnerability error is not
  automatically an Azure DevOps or Helix outage. Check if the PR or commit range
  changed feeds, URLs, credentials, or package versions, or if earlier builds
  failed the same way. If you cannot tell, mark the failure `Inconclusive` and
  list the next check. Use `External infrastructure` only for confirmed CI
  service, agent, queue, or network outages.
- **Comparing errors across runs:** ignore run-specific values such as work-item
  IDs, machine names, and temp paths. Treat differences in error codes, file
  paths, versions, URLs, expected/actual values, COM CLSIDs, assertion
  timestamps, or stack traces as different failures.

The CI Quality Investigator workflow also separates build phase, failure type,
evidence source, and confidence; see its [decision process](../../../workflows/ci-quality-monitor.md#decision-process)
and [failure model](../../../ci-quality-monitor/DESIGN.md#failure-model). Its
automated issue-filing rules do not apply to interactive investigations.

## Example: rolling batch after an existing failure

Build A passes a test, build B fails with error M, and build C fails with error
M after three new commits. Build B proves that error M existed before build C's
commits. Mark error M as **Pre-existing** for build C (finding which commit
between A and B introduced error M is a separate task).
