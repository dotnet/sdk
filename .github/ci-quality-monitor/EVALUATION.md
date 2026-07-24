# Historical Evaluation Builds

These public `dotnet-sdk-public-ci` builds provide repeatable manual-dispatch
evaluation points for the CI Quality Monitor. The collector reads build data
from `dotnet/sdk` Azure DevOps while the staged workflow runs and previews any
issue in `nagilson/sdk`.

| Category | Build | Source | Verified evidence |
| --- | --- | --- | --- |
| Pipeline YAML | [1521345](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1521345) | PR 55404 | Zero-duration build, no timeline, `Unexpected parameter 'useFullyQualifiedTestName'`. |
| Setup/checkout | [1523420](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1523420) | PR 55429 | Checkout failed across multiple jobs after `git fetch` exit 128. |
| Restore/feed | [1525302](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525302) | PR 55399 | Arcade SDK acquisition failed when the dotnet10 feed returned HTTP 503. |
| Compiler build break | [1525235](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525235) | PR 55339 | `CS0114` in `RunReadyToRunCompiler.TaskEnvironment` failed multiple legs before tests. |
| Release build break | [1522972](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1522972) | `release/8.0.4xx` | `MSB4018` SignToolTask failure; `sn.exe` had an exec-format error on Linux/macOS. Build [1522971](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1522971) is the equivalent `release/8.0.1xx` case. |
| Multiple named tests | [1525292](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1525292) | `main` | Four named failures from Helix TRX; two package tests share an HTTP 503 mechanism. |
| Helix hang/host crash | [1524763](https://dev.azure.com/dnceng-public/public/_build/results?buildId=1524763) | PR 55431 | `BrowserDiagnostics` hung for 50 minutes, hang dumps were captured, and the host crashed with work-item exit 7. |

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
category is absent.

## Synthetic-Only Cases

- `pipeline-not-triggered`: a missing pipeline run has no build ID. Validate it
  through the two-poll heartbeat unit test.
- `cascade`: no verified artifact-cascade failure was found in the sampled
  recent definition 101 window. Validate cascade suppression through the
  root/cascade unit test rather than claiming an unverified historical run.