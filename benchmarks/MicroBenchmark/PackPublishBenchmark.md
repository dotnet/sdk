# OrchardCore Pack/Publish Benchmark

`PackPublishBenchmark` measures real external `dotnet pack` and `dotnet publish` processes using
two independently prepared SDK artifacts.

Each BenchmarkDotNet iteration runs both cells. Their order alternates to reduce time-dependent
machine drift:

```text
Before, After
After, Before
Before, After
...
```

The benchmark records two values for every process:

- Total process wall-clock time.
- `dotnet.cli.process_start_to_msbuild_submission.duration`, emitted by the SDK before its first
  MSBuild invocation.

Both SDK artifacts must contain the metric introduced by
[dotnet/sdk#55499](https://github.com/dotnet/sdk/pull/55499).

The collector removes its startup-hook environment variables before the CLI starts MSBuild child
processes. Persistent MSBuild servers and reusable nodes therefore retain their normal behavior and
do not inherit per-invocation metric output paths.

## Prepare the SDK artifacts

Create immutable Before and After SDK installations before running the benchmark. Changes from
MSBuild or NuGet are part of SDK artifact construction and are not committed to this benchmark:

1. Build both SDKs with the #55499 metric.
2. Apply the optimization under test only to the After SDK.
3. If needed, replace the MSBuild assemblies or NuGet assemblies/targets in the appropriate SDK
   artifact.
4. Record the exact SDK, MSBuild, NuGet, and OrchardCore commit SHAs with the results.

Separate OrchardCore worktrees may be configured per cell when the SDKs produce incompatible
intermediate outputs.

## Reproduce the combined optimization measurement

Both cells must contain #55499 so both emit the pre-submission metric:

| Cell | SDK branch | Release-property discovery | NuGet Pack |
| --- | --- | --- | --- |
| Before | `dev/veronikao/pack-publish-benchmark-before` | Full evaluation with isolated contexts | Targets before NuGet/NuGet.Client#7603 |
| After | `dev/veronikao/pack-publish-benchmark-after` | Properties-only evaluation with a Shared context | Targets from NuGet/NuGet.Client#7603 |

This includes:

- [dotnet/msbuild#14290](https://github.com/dotnet/msbuild/pull/14290)
- [dotnet/sdk#55271](https://github.com/dotnet/sdk/pull/55271)
- [dotnet/sdk#55426](https://github.com/dotnet/sdk/pull/55426)
- [NuGet/NuGet.Client#7603](https://github.com/NuGet/NuGet.Client/pull/7603)

The branches document the two SDK code states. For strict total wall-clock measurements, use one
experimental SDK binary with local-only stage and context-policy selectors. Using the same binaries
for both cells avoids unrelated binary and incremental-build differences.

Use separate OrchardCore worktrees for the two cells so they do not share `bin` or `obj`.
NuGet#7603 changes only `NuGet.Build.Tasks.Pack.targets`: copy the baseline target from the measured
SDK and apply NuGet#7603 to a copy of that same file. Do not substitute a complete target from an
unrelated NuGet build because its task parameters may not match the SDK's Pack task assembly.

```json
{
  "before": {
    "dotnetPath": "C:\\perf\\sdk-experiment\\dotnet.exe",
    "orchardCoreRoot": "C:\\OrchardCore-before",
    "workingDirectory": "C:\\OrchardCore-before",
    "environmentVariables": {
      "DOTNET_CLI_RELEASE_PROPERTY_EVALUATION_STAGE": "Full",
      "DOTNET_CLI_RELEASE_PROPERTY_EVALUATION_CONTEXT_POLICY": "Isolated"
    },
    "packArguments": [
      "-p:CustomBeforeMicrosoftCommonProps=C:\\perf\\nuget-pack\\override.props",
      "-p:NuGetBuildTasksPackTargets=C:\\perf\\nuget-pack\\Pack.baseline.targets"
    ]
  },
  "after": {
    "dotnetPath": "C:\\perf\\sdk-experiment\\dotnet.exe",
    "orchardCoreRoot": "C:\\OrchardCore-after",
    "workingDirectory": "C:\\OrchardCore-after",
    "environmentVariables": {
      "DOTNET_CLI_RELEASE_PROPERTY_EVALUATION_STAGE": "Properties",
      "DOTNET_CLI_RELEASE_PROPERTY_EVALUATION_CONTEXT_POLICY": "Shared"
    },
    "packArguments": [
      "-p:CustomBeforeMicrosoftCommonProps=C:\\perf\\nuget-pack\\override.props",
      "-p:NuGetBuildTasksPackTargets=C:\\perf\\nuget-pack\\Pack.modified.targets"
    ]
  }
}
```

## Configuration

Set `DOTNET_SDK_PACK_PUBLISH_BENCHMARK_CONFIG` to an absolute JSON file path:

```json
{
  "orchardCoreRoot": "C:\\OrchardCore",
  "publishFramework": "net10.0",
  "resultsPath": "C:\\perf\\pack-publish-results-{runId}.csv",
  "before": {
    "dotnetPath": "C:\\perf\\sdk-before\\dotnet.exe",
    "environmentVariables": {},
    "packArguments": [],
    "publishArguments": [],
    "timeoutMinutes": 30
  },
  "after": {
    "dotnetPath": "C:\\perf\\sdk-after\\dotnet.exe",
    "environmentVariables": {},
    "packArguments": [],
    "publishArguments": [],
    "timeoutMinutes": 30
  }
}
```

Paths may be absolute or relative to the configuration file. Each cell may override
`orchardCoreRoot` and `workingDirectory`.

`{runId}` is replaced with one identifier shared by all benchmark processes in a run. Use it to
prevent results from different runs being mixed in one CSV. If the placeholder is omitted, the CSV
contains a `RunId` column that can be used to separate appended runs.

Use environment variables or additional arguments for local-only policy selectors:

```json
{
  "before": {
    "environmentVariables": {
      "EXPERIMENTAL_FEATURE": null
    }
  },
  "after": {
    "environmentVariables": {
      "EXPERIMENTAL_FEATURE": "1"
    }
  }
}
```

A `null` value removes an inherited environment variable.

## Run

From the repository root:

```powershell
.\build.cmd -restore

$env:DOTNET_SDK_PACK_PUBLISH_BENCHMARK_CONFIG = "C:\perf\orchard-config.json"
dotnet run --project benchmarks\MicroBenchmark\MicroBenchmark.csproj -c Release -- `
  --pack-publish
```

Validate one pair without running the complete BenchmarkDotNet job:

```powershell
dotnet run --project benchmarks\MicroBenchmark\MicroBenchmark.csproj -c Release -- `
  --pack-publish-smoke

# Or validate one operation:
dotnet run --project benchmarks\MicroBenchmark\MicroBenchmark.csproj -c Release -- `
  --pack-publish-smoke Pack
```

The benchmark performs:

- Restore and Release Build preparation outside measurement for both cells.
- Three paired warmup iterations.
- Twelve paired measured iterations.
- Full `OrchardCore.slnx` Pack commands.
- Publish commands over a generated solution containing the projects under
  `src/OrchardCore.Modules` and `src/OrchardCore.Themes`.
- Default MSBuild Server and node-reuse behavior.

BenchmarkDotNet reports the combined duration of each pair. Use `resultsPath` for the individual
Before and After process measurements. The CSV distinguishes warmup and measured rows and records
the loaded `Microsoft.DotNet.Cli.Utils` path and SHA-256 hash.

## Report results

For each command, report:

- Before and After median total wall-clock time.
- Before and After median time before the first MSBuild submission.
- Median paired differences and paired win counts.
- The exact project population and all component commit SHAs.

Keep the raw CSV and BenchmarkDotNet artifacts. Do not infer evaluation counts from either timing
metric; collect MSBuild evaluation counters separately when required.
