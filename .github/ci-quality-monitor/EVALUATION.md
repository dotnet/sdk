# Historical Evaluation Builds

These public `dotnet-sdk-public-ci` builds provide repeatable manual-dispatch
evaluation points for the CI Quality Monitor. The collector reads build data
from `dotnet/sdk` Azure DevOps while the staged workflow runs and previews any
issue in `nagilson/sdk`.

| Scenario | Build | Source | Expected taxonomy and evidence |
| --- | --- | --- | --- |
| Pipeline YAML | [1521345](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1521345) | PR 55404 | `pipeline-validation / configuration-error`; `azure-validation` identifies the unexpected parameter. |
| Setup/checkout | [1523420](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1523420) | PR 55429 | `source-checkout / source-unavailable`; `azure-timeline` reports git fetch exit 128. |
| Restore/feed | [1523525](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1523525) | PR 55431 | `dependency-restore / network-failure`; timeline and task log show HTTP 503. |
| Compiler build break | [1525235](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525235) | PR 55339 | `compilation / compiler-error`; timeline and task log identify `CS0114`. |
| Release build break | [1522972](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1522972) | `release/8.0.4xx` | `signing / tool-execution-error`; timeline and task log identify `SignToolTask` and `sn.exe`. |
| Multiple named tests | [1525292](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525292) | `main` | `test-execution / network-failure`; Helix TRX establishes shared HTTP 503 failures. |
| Helix hang/host crash | [1524763](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1524763) | PR 55431 | `test-execution / timeout`; Helix console, process exit code, TRX, and dumps establish the hang. |

Manual-dispatch the workflow with one build ID at a time and wait for that run
to finish before starting the next. The workflow concurrency group permits one
running and one pending run; GitHub cancels an older pending run when another is
queued. PR and release builds are accepted for manual evaluation even though
scheduled monitoring remains restricted to registered rolling branches.

Run all historical classifications locally:

```powershell
node .github/ci-quality-monitor/evaluate-builds.mjs
```

The command writes one dossier per build and `summary.json` under
`artifacts/tmp/ci-quality-monitor/evaluations` and exits nonzero when an expected
phase, failure type, evidence source, or mechanism is absent.

## Long-term Taxonomy Decision

Keep the three axes. They answer independent questions: `phase` locates the
failed operation, `failureType` describes the causal mechanism, and
`evidenceSources` records the observations supporting that conclusion.
Actionability, ownership, priority, and `monitoringScope` remain policy metadata,
not additional failure categories.

Evidence sources are intentionally observation-dependent: the same mechanism may
have a task log in one run and only timeline evidence in another. They are
therefore required by scenario-specific evaluation where appropriate, but are
excluded from stable failure fingerprints. Two production hardening items remain:
move collector retrieval failures such as `evidence-unavailable` out of the
mechanism axis, and measure `unknown-error`/`unknown` fallbacks before adding new
named categories.

## Taxonomy-v2 Fork Evaluation

The current evaluation runs the three-axis collector and native GitHub AW issue
output against all seven scenarios. Each issue contains the selected build,
exact failure fingerprint, required evidence sections, bounded root cause
analysis, and fork-evaluation labels. The scored matrix and live replacement
status are maintained in [CURRENT-STATUS.md](CURRENT-STATUS.md).

## Superseded Taxonomy-v1 Attempts

| Scenario | Workflow run | Evaluation issue |
| --- | --- | --- |
| Pipeline YAML | [30134772222](https://github.com/nagilson/sdk/actions/runs/30134772222) | [nagilson/sdk#71](https://github.com/nagilson/sdk/issues/71) |
| Setup/checkout | [30135725391](https://github.com/nagilson/sdk/actions/runs/30135725391) | [nagilson/sdk#73](https://github.com/nagilson/sdk/issues/73) |
| Restore/feed | [30136527251](https://github.com/nagilson/sdk/actions/runs/30136527251) | [nagilson/sdk#75](https://github.com/nagilson/sdk/issues/75) |
| Compiler build break | [30136852103](https://github.com/nagilson/sdk/actions/runs/30136852103) | [nagilson/sdk#76](https://github.com/nagilson/sdk/issues/76) |
| Release build break | [30137125115](https://github.com/nagilson/sdk/actions/runs/30137125115) | [nagilson/sdk#77](https://github.com/nagilson/sdk/issues/77) |
| Multiple named tests | [30137407258](https://github.com/nagilson/sdk/actions/runs/30137407258) | [nagilson/sdk#78](https://github.com/nagilson/sdk/issues/78) |
| Helix hang/host crash | [30144976771](https://github.com/nagilson/sdk/actions/runs/30144976771) | [nagilson/sdk#79](https://github.com/nagilson/sdk/issues/79) |

Duplicate suppression was verified twice against YAML build `1521345` after
issue #71 existed. Automatic push run
[30135628726](https://github.com/nagilson/sdk/actions/runs/30135628726) and manual
dispatch run [30135696008](https://github.com/nagilson/sdk/actions/runs/30135696008)
both completed successfully with activation, agent, detection, and safe-output
jobs skipped and no additional issue created.

## Synthetic-Only Cases

- `pipeline-not-triggered`: a missing pipeline run has no build ID. Validate it
  through the two-poll heartbeat unit test.
- `cascade`: no verified artifact-cascade failure was found in the sampled
  recent definition 101 window. Validate cascade suppression through the
  root/cascade unit test rather than claiming an unverified historical run.