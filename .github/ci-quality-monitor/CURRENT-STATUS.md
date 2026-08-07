# Current Status

The CI Quality Investigator uses three independent axes:

- `phase`: where execution stopped
- `failureType`: what happened
- `evidenceSources`: how the conclusion was established

All 38 unit tests pass, all seven historical builds pass the local empirical
gate, and `gh aw compile` reports no errors or warnings. Commit `d9970b845d`
adds artifact-derived `helix-trx` and `helix-dump` sources and makes those
sources part of the scenario gate and issue grader.

## Current Taxonomy-v2 Results

| Scenario | Run | Issue | Taxonomy | Evidence sources | Score |
| --- | --- | --- | --- | --- | ---: |
| YAML validation | [30147087520](https://github.com/nagilson/sdk/actions/runs/30147087520) | [#80](https://github.com/nagilson/sdk/issues/80) | `pipeline-validation / configuration-error` | `azure-validation` | 9/10 |
| Checkout | [30293483832](https://github.com/nagilson/sdk/actions/runs/30293483832) | [#81](https://github.com/nagilson/sdk/issues/81) | `source-checkout / source-unavailable` | `azure-timeline` | 9/10 |
| NuGet feed | [30294832494](https://github.com/nagilson/sdk/actions/runs/30294832494) | [#82](https://github.com/nagilson/sdk/issues/82) | `dependency-restore / network-failure` | `azure-timeline`, `azure-task-log` | 10/10 |
| Compiler break | [30295583961](https://github.com/nagilson/sdk/actions/runs/30295583961) | [#83](https://github.com/nagilson/sdk/issues/83) | `compilation / compiler-error` | `azure-timeline`, `azure-task-log` | 10/10 |
| Signing break | [30296262335](https://github.com/nagilson/sdk/actions/runs/30296262335) | [#84](https://github.com/nagilson/sdk/issues/84) | `signing / tool-execution-error` | `azure-timeline`, `azure-task-log` | 10/10 |
| Multiple named tests | [30299485941](https://github.com/nagilson/sdk/actions/runs/30299485941) | [#85](https://github.com/nagilson/sdk/issues/85) | `test-execution / network-failure` | `helix-trx` | 10/10 |
| Hang/watchdog | [30302460317](https://github.com/nagilson/sdk/actions/runs/30302460317) | [#87](https://github.com/nagilson/sdk/issues/87) | `test-execution / timeout` | `helix-console`, `process-exit-code`, `helix-trx`, `helix-dump` | 10/10 |

`10/10` means the issue passed every required structure, taxonomy, provenance,
mechanism, RCA-bounding, and guidance-consistency check. `9/10` means the same
contract passed, but the available source evidence cannot establish the exact
sub-cause without the next investigation step.

The agent did not reverse or contradict the collector taxonomy in the completed
taxonomy-v2 runs. Lower scores for YAML and checkout reflect bounded evidence,
not category errors: YAML has no executed timeline, and checkout lacks the raw
fatal git diagnostic beneath exit code 128.

## Superseded Repair Attempts

| Attempt set | Runs/issues | Why superseded |
| --- | --- | --- |
| Taxonomy-v1 serial evaluation | Runs [30134772222](https://github.com/nagilson/sdk/actions/runs/30134772222) through [30144976771](https://github.com/nagilson/sdk/actions/runs/30144976771); issues #71, #73, #75-#79 | Proved end-to-end issue creation and deduplication, but used the earlier flat category model. |
| Hang taxonomy-v2 attempt | [30300351445](https://github.com/nagilson/sdk/actions/runs/30300351445), archived [#86](https://github.com/nagilson/sdk/issues/86) | RCA cited hang dumps, but structured `evidenceSources` omitted `helix-trx` and `helix-dump`. |
| Corrected no-op attempt | [30301418510](https://github.com/nagilson/sdk/actions/runs/30301418510) | Collector supplied all four sources and all jobs passed; native semantic dedup emitted a no-op because taxonomy-v1 #79 still tracked the same failure. |

Taxonomy-v1 #79 and the incomplete taxonomy-v2 #86 are archived. Replacement
issue #87 passed every reusable grader check. Its agent trace retained
`test-execution / timeout`, recovered from one shell-safety block without
changing the analysis, and emitted the four required evidence sources. All jobs
in run 30302460317 completed successfully.
