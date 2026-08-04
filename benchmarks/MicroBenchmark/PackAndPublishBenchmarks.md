# OrchardCore Pack and Publish Benchmarks

The benchmark project contains two independent scenarios:

- `PackBenchmark`: one external `dotnet pack` process per iteration.
- `PublishBenchmark`: one external `dotnet publish` process per iteration.

Each scenario measures one SDK configuration. Run it once for Before and once for After, then
compare the two result CSVs.

Each measured process records:

- Total process wall-clock time.
- `dotnet.cli.process_start_to_msbuild_submission.duration`.
- The loaded `Microsoft.DotNet.Cli.Utils` path and SHA-256.

The SDK under test must contain the metric introduced by
[dotnet/sdk#55499](https://github.com/dotnet/sdk/pull/55499).

## Defaults

All benchmark options are optional:

| Option | Default |
| --- | --- |
| `--dotnet` | `dotnet` resolved from `PATH` |
| `--orchard-core` | Current directory |
| `--working-directory` | OrchardCore root |
| `--label` | `Default` |
| `--publish-framework` | `net10.0` |
| `--results` | `BenchmarkDotNet.Artifacts/{benchmark}-{label}-{runId}.csv` |
| `--timeout-minutes` | `30` |

## Combined optimization A/B

The combined comparison uses:

- Before:
  [`dev/veronikao/pack-publish-benchmark-before-pre-partial`](https://github.com/OvesN/sdk/tree/dev/veronikao/pack-publish-benchmark-before-pre-partial)
- After:
  [`dev/veronikao/pack-publish-benchmark-after`](https://github.com/OvesN/sdk/tree/dev/veronikao/pack-publish-benchmark-after)

The Before branch starts from `51511d9796875967598c369a822db2637d1704e1`, the parent of SDK
#55271, then cherry-picks only the metric and benchmark commits. It uses the original full
`ProjectInstance` evaluation path and isolated contexts.

The After branch uses Properties-only evaluation and a Shared evaluation context.

The comparison includes:

- [dotnet/msbuild#14290](https://github.com/dotnet/msbuild/pull/14290), adopted by
  [dotnet/sdk#55271](https://github.com/dotnet/sdk/pull/55271)
- [dotnet/sdk#55426](https://github.com/dotnet/sdk/pull/55426)

MSBuild#14274 is excluded because OrchardCore solution Pack/Publish does not meaningfully exercise
that Restore optimization.

Use separate OrchardCore worktrees for Before and After so they do not share `bin` or `obj`.

## Run Pack

Before:

```powershell
dotnet run --project benchmarks\MicroBenchmark\MicroBenchmark.csproj -c Release -- `
  --pack `
  --dotnet <before-dotnet-executable> `
  --orchard-core <before-orchardcore-worktree> `
  --label Before `
  --results <results-directory>\pack-Before-{runId}.csv
```

After:

```powershell
dotnet run --project benchmarks\MicroBenchmark\MicroBenchmark.csproj -c Release -- `
  --pack `
  --dotnet <after-dotnet-executable> `
  --orchard-core <after-orchardcore-worktree> `
  --label After `
  --results <results-directory>\pack-After-{runId}.csv
```

## Run Publish

Before:

```powershell
dotnet run --project benchmarks\MicroBenchmark\MicroBenchmark.csproj -c Release -- `
  --publish `
  --dotnet <before-dotnet-executable> `
  --orchard-core <before-orchardcore-worktree> `
  --label Before `
  --results <results-directory>\publish-Before-{runId}.csv
```

After:

```powershell
dotnet run --project benchmarks\MicroBenchmark\MicroBenchmark.csproj -c Release -- `
  --publish `
  --dotnet <after-dotnet-executable> `
  --orchard-core <after-orchardcore-worktree> `
  --label After `
  --results <results-directory>\publish-After-{runId}.csv
```

## Smoke commands

Replace `--pack` with `--pack-smoke`, or `--publish` with `--publish-smoke`, to execute one measured
child process without a complete BenchmarkDotNet job.

## Benchmark behavior

Each complete run performs:

- Restore and Release Build preparation outside measurement.
- Three warmup iterations.
- Twelve measured iterations.
- Implicit Restore and a no-op Build in measured commands.
- Default MSBuild Server and node-reuse behavior.
- BenchmarkDotNet's high-performance power plan for the run.

Pack uses the full `OrchardCore.slnx`. Publish generates a solution containing projects under
`src/OrchardCore.Modules` and `src/OrchardCore.Themes`.

## Compare results

```powershell
.\benchmarks\MicroBenchmark\CompareCommandBenchmarkResults.ps1 `
  -BeforeCsv <pack-before-csv> `
  -AfterCsv <pack-after-csv>

.\benchmarks\MicroBenchmark\CompareCommandBenchmarkResults.ps1 `
  -BeforeCsv <publish-before-csv> `
  -AfterCsv <publish-after-csv>
```

The script reports median total wall-clock and pre-submission durations, plus absolute and
percentage reductions.
