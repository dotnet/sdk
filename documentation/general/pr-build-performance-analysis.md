# SDK PR Build Performance Analysis

## Overview

Analysis of the SDK public PR build pipeline (definition 101, `dotnet-sdk-public-ci`) to identify performance improvement opportunities, focusing on Helix test shard distribution.

## Pipeline Statistics (20 recent PR builds)

| Metric | Value |
|---|---|
| Execution time p50 | 80.6 min |
| Execution time mean | 81.2 min |
| Execution time range | 50–116 min |
| Queue time p50 | 0.5 min |
| Queue time mean | 26.2 min (severe outliers) |

## Critical Path

The **TestBuild: linux (x64)** job is consistently the critical path, taking 95.4 min in the slow build vs 56.9 min in the fast build. Within this job, Helix work items run across ~100 machines with ~7.4x parallelism, meaning long individual shards directly extend the critical path.

## Key Findings

### dotnet.Tests.dll Shard Imbalance

The `AssemblyScheduler` partitions test assemblies into Helix work items by IL-visible public method count per class, with a limit of 16 methods per shard. It never splits a class across shards.

**Problem:** `RunFileTests` was a single class with ~150 IL methods. The scheduler placed it entirely in shard 19 alongside `RunCommandTests` (4 methods), creating a 42-minute shard while all other dotnet.Tests.dll shards completed in under 8 minutes.

| Shard | Duration | Tests | Classes |
|---|---|---|---|
| Shard 19 | **42.1 min** | 234 | RunCommandTests, RunFileTests |
| Shard 11 | 7.0 min | 169 | SdkInfoProviderTests, WorkloadsInfoProviderTests, GivenDotnetPackageAdd |
| Shard 16 | 4.3 min | 62 | GivenDotnetRootEnv, GivenDotnetRunBuildsCsproj |
| All others | <3 min each | varies | varies |

### Microsoft.NET.Publish.Tests.dll Shard Imbalance

Similarly, the `GivenThatWeWantToRunILLink` classes dominate:

| Shard | Duration | Tests | Classes |
|---|---|---|---|
| Shard 9 | **21.2 min** | 153 | ILLink2, ILLink3 |
| Shard 8 | **16.7 min** | 133 | PublishWithIfDifferent, PublishWithoutConflicts, ILLink1 |
| Shard 5 | 7.9 min | 85 | PublishASelfContainedApp, PublishASingleFileApp |
| All others | <6 min each | varies | varies |

## Improvement Opportunities (Ranked)

| # | Opportunity | Expected Savings | Effort |
|---|---|---|---|
| 1 | Reduce queue time outliers (infra) | ~25 min (p90) | Medium |
| 2 | **Split RunFileTests into multiple classes** | **~30 min** | **Low** |
| 3 | Further split ILLink classes | ~15 min | Low |
| 4 | Add time-aware scheduling to AssemblyScheduler | ~10 min | Medium |
| 5 | Parallelize restore + build within legs | ~5 min | Medium |
| 6 | Cache NuGet packages across Helix work items | ~3 min | Medium |
| 7 | Reduce TestBuild leg count on non-critical platforms | ~10 min | Low |
| 8 | Skip unchanged test assemblies | Variable | High |
| 9 | Pre-warm Helix machines | ~2 min | Medium |
| 10 | Binary log analysis for build-time targets | ~5 min | Medium |

## Implementation: RunFileTests Split (Opportunity #2)

### Problem

`RunFileTests` was a 6,210-line class with ~150 public methods. The `AssemblyScheduler` (16-method limit) cannot split within a class, so it all ended up in one shard taking 42 minutes.

### Solution

Split into 5 test classes inheriting from a shared `RunFileTestBase`:

| Class | Tests | Description |
|---|---|---|
| `RunFileTests` | 31 | Path resolution, precedence, stdin, multifile |
| `RunFileTests_BuildOptions` | 28 | Working dir, Dir.Build.props, arguments, binary log, verbosity, resources |
| `RunFileTests_BuildCommands` | 28 | Restore, build, publish, pack, clean, launch profiles |
| `RunFileTests_Directives` | 24 | Defines, package/SDK/project refs, include directive, user secrets, CSC arguments |
| `RunFileTests_CscOnlyAndApi` | 42 | Up-to-date checks, csc-only mode, API tests, entry points, MSBuild get |

`RunFileTestBase` holds shared static fields, helper methods (`DirectiveError`, `VerifyBinLogEvaluationDataCount`), and the `Build()` instance method.

**Total test count preserved:** 153 methods (123 Fact + 27 Theory + 3 PlatformSpecificFact).

### Expected Impact

The single 42-minute shard should distribute across ~5 shards of ~8–10 minutes each, reducing the critical path of **TestBuild: linux (x64)** by approximately 30 minutes.

### How the Scheduler Works

The `AssemblyScheduler` (in `test/HelixTasks/AssemblyScheduler.cs`) scans IL metadata for public test classes and their method counts. Classes are sorted alphabetically, then accumulated into partitions. When the running method count reaches the limit (16 for TestBuild, 32 for FullFramework), a new partition is started. The class that triggers the limit is included in the current partition, not moved to the next.

`SDKCustomCreateXUnitWorkItemsWithTestExclusion` (in `test/HelixTasks/`) creates Helix work items from these partitions, using `--filter "ClassName1|ClassName2"` for each shard.

## Next Steps

- **Opportunity #3:** Further split `GivenThatWeWantToRunILLink1/2/3` classes in `Microsoft.NET.Publish.Tests` to reduce the 21-minute and 17-minute shards.
- **Opportunity #4:** Consider adding time-based weighting to the `AssemblyScheduler` to account for tests that are individually slow (e.g., ILLink tests that build and publish full apps).
