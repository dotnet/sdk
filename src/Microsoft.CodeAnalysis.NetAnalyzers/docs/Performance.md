# Measuring Analyzer Performance

Now that analyzers are part of the build we need a mechanism to track their performance across releases as well as build confidence regarding their use in the SDK.

## Goals

- Developers can quickly get feedback on how their change affects performance
- We can track and detect performance regressions in builds before release.

## What we do today

- Roslyn
  - Can be run on CI: **No**
  - Can be run locally with a single script: **No**
  - [Compiler Performance](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Measuring-Compiler-Performance.md)
    - compiler team has written scenarios in the dotnet/performance repo. The directions for these call for developers to clone the dotnet/performance repo and manually run the tests
    - dotnet/performance benchmarks for roslyn are [here]((https://github.com/dotnet/performance/tree/main/src/benchmarks/real-world/Roslyn))
  - [Analyzer Performance](https://microsoft.sharepoint.com/teams/managedlanguages/_layouts/15/Doc.aspx?sourcedoc={79b652be-6aa1-4feb-8d23-fa9127483ce9}&action=edit&wd=target%28Productivity%2FHelpers.one%7Caf49b9ef-72a4-4dee-9cf1-460fe552857a%2FHow%20to%20use%20AnalyzerRunner%7Cf8d125f1-83d6-47eb-8bde-09070142ceee%2F%29)
    - There is an AnalyzerRunner commandline tool checked into dotnet/roslyn that can be used to run analyzers and validate their performance. It needs to be run in a manual fashion.
- [ASP.NET](https://github.com/aspnet/Benchmarks/blob/main/scenarios/README.md)
  - Can be run on CI: **Yes**
  - Can be run locally with a single script: **No**
  - The ASP.NET team has written a tool (crank) that allows them to run benchmarks on either their local machines or remote machines using a client/server model. This does not require the user to download the dotnet/performance repository manually to run scenarios from there. Users will need to manually setup/patch runtimes with their changes but can then run them against the real benchmarks from there.
    - [Crank](https://github.com/dotnet/crank)
    - [TechEmpower Benchmarks Power BI](https://msit.powerbi.com/view?r=eyJrIjoiYTZjMTk3YjEtMzQ3Yi00NTI5LTg5ZDItNmUyMGRlOTkwMGRlIiwidCI6IjcyZjk4OGJmLTg2ZjEtNDFhZi05MWFiLTJkN2NkMDExZGI0NyIsImMiOjV9)
- Runtime
  - Can be run on CI: **Yes** CI runs require you to submit a PR against dotnet/performance
  - Can be run locally with a single script: **No**
  - The runtime team has a set of benchmarking guides that detail how to run the tests in dotnet/performance against local changes.
    - [Benchmarking](https://github.com/dotnet/performance/blob/main/docs/benchmarking-workflow-dotnet-runtime.md)
    - [Profiling](https://github.com/dotnet/performance/blob/main/docs/profiling-workflow-dotnet-runtime.md)

## Proposed Workflow

### Tests

We will have two types of tests:

#### Micro-Benchmarks

A set of micro-benchmarks (written in BenchmarkDotnet) testing how much time analyzers spend computing result. Each new analyzer that ships in the SDK is expected to have a micro-benchmark that tests

- code files that cause the analyzer to execute but not issue a diagnostic.
- code files that cause the analyzer to issue a diagnostic.

These tests are expected to live in the dotnet/roslyn-analyzers repo to make local development simpler.

#### End-to-End Tests

An end-to-end compilation test that measures how long the build takes on a large real-world project (based off existing scenarios [here](https://github.com/dotnet/performance/blob/main/docs/sdk-scenarios.md#sdk-build-throughput-scenario)). This test will be run twice, once with all mutli-core build features disabled (no `/m` is passed to msbuild, and `/parallel-` is passed to the compiler) and once with the SDK defaults enabled. The reason we will want a test with no parallelism is to make it easier to see the source of regressions. These test will not just measure how long it takes analyzers to execute but the entire SDK-based build process. It will need to collect an ETL file for investigation as well as the following metrics in a binlog file

- How much time was spend in analysis (`/p:reportanalyzer=true`)
- How long each build task took (recorded by default in the binlog file)
- Total Build Time (recorded by default in the binlog file)

This test will be added to the dotnet/performance repo to augment the build throughput scenarios that are already there.

### Local Developer Machine

There will be a simple script that a developer can run locally on their machine that will compare their current changes to what is in `main`. The tests that will be run will be local to the dotnet/roslyn-analyzers repo. It will then produce a commandline result telling the developer if there is a regression (in typical benchmarkdotnet fashion) as well as an ETL file for both before and after that can be examined.

### For Pull Requests

The same script that the user ran locally will be executed on CI using the [results comparer](https://github.com/dotnet/performance/blob/main/src/tools/ResultsComparer/README.md) tool for BenchMarkDotNet to decide if the tests have passed. There are concerns about noise here. ResultsComparer has a noise threshold that can be set which we will adjust to the correct values over time. In addition, we can run these tests on a queue with "dedicated" hardware with the "Host" dnceng pool. We will need to evaluate carefully how noisy these results are, but the hope is that we can strike a good balance of giving developers feedback on the performance of their PRs (as well as traceability in the case of a regression) while also not kill code flow.

### Weekly Cadence

Our end-to-end performance tests are run and reported on the performance dashboard automatically. Once a week someone checks in on this performance board and verifies there are no negative trends. If a regression in build times appears to be trending a high priority bug is filed and acted on.

### For Releases

We run out end-to-end build performance tests and compare with the previous release
Example: We release .NET 6 Preview 7. CTI runs the performance tests on the this new release and compares to the results that were recorded for .NET 6 Preview 6 as well as the latest .NET 5 RTM. ***NOTE:*** The goal at this stage is to look at performance from a customer perspective. If there is not experiential change then we consider this change a pass.
