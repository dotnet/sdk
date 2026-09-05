# Solution-level target-framework selection

## Status

**Proposal**

Tracking issue: [dotnet/sdk#47093](https://github.com/dotnet/sdk/issues/47093)

## Summary

The `--framework` option currently applies one global `TargetFramework` value to every project in a solution.

This behavior fails when a project inside the solution does not directly declare the requested framework. The failure is commonly `NETSDK1005`.

This proposal changes the behavior of `--framework` for solution and traversal builds.

Each project in the container (solution/traversal/solution filter) gets one nearest-compatible target framework. Projects without a compatible target framework are not directly included in the initial build graph.

Normal project-reference negotiation continues after this selection. A dependency can therefore build for an additional target framework when its parent requires one.

## Decision summary

| Question | Proposed answer |
| --- | --- |
| Does direct project `--framework` change? | No |
| What does solution `--framework` select? | One nearest-compatible framework for each direct project |
| Must a direct project declare the requested framework? | Yes, at least one direct project must match exactly |
| What happens to an incompatible direct project? | The orchestrator omits it and reports it |
| Can a project build for another framework transitively? | Yes, when a project reference requires it |
| Does this add a new CLI option? | No |
| Does explicit `/p:TargetFramework` change? | No |
| Does solution selection use `AssetTargetFallback`? | No |
| Does a container with no direct exact match build? | No |

## Motivation

Consider this solution:

```text
App.Tests.csproj       TargetFrameworks=net9.0;net10.0
Legacy.Tests.csproj    TargetFramework=net9.0
```

The following command currently passes `TargetFramework=net10.0` to both projects:

```console
dotnet test Repo.slnx --framework net10.0
```

`Legacy.Tests.csproj` does not declare `net10.0`. The build fails before any tests run.

The requested framework has two purposes at the container level:

1. Select one nearest-compatible framework from each direct project.
2. Identify the runtime that commands should use when they run built assets.

Project builds already negotiate target frameworks across project references. Solution builds do not apply the same protocol to solution entries.

The solution should therefore select `App.Tests.csproj (net10.0)` and `Legacy.Tests.csproj (net9.0)`.

The exact `net10.0` entry establishes that this operation runs assets on the requested runtime. The `net9.0` entry remains valid through framework compatibility.

Now consider a solution that contains only these projects:

```text
Legacy.Tests.csproj    TargetFramework=net9.0
Portable.Tests.csproj  TargetFramework=netstandard2.0
```

Both frameworks can be compatible with a `net10.0` consumer. However, neither project produces an asset that runs on the requested `net10.0` runtime.

The following command must therefore fail:

```console
dotnet test Repo.slnx --framework net10.0
```

Nearest-compatible selection is valid only when at least one direct project declares the requested framework.

This design aligns solution entries with project-reference negotiation without weakening the runtime intent of `--framework`.

## Goals

This proposal has these goals:

1. Make solution-level `--framework` select a compatible project-graph slice.
2. Require at least one direct project that declares the desired target framework.
3. Select one target framework for each direct solution entry.
4. Preserve normal target-framework negotiation for project references.
5. Use the same compatibility rules as the project-reference protocol.
6. Support `.sln`, `.slnx`, solution filters, and traversal projects.
7. Make `dotnet test` follow the same selection behavior as `dotnet build`.
8. Give clear information when projects are not selected.
9. Add no cost when the user does not specify `--framework`.

## Non-goals

This proposal does not change `--framework` for a direct project invocation.

This proposal does not build every compatible target framework from each project.

This proposal does not add target-framework selection to `dotnet restore`.

This proposal does not define runtime-identifier negotiation.

This proposal does not change explicit `/p:TargetFramework=...` MSBuild behavior.

This proposal does not make static graph build the default.

## Terminology

The **desired target framework** is the value that the user passes to `--framework`.

The **selected target framework** is the nearest compatible framework that a project declares.

A **direct entry** is a project that the solution or traversal project schedules directly.

A **project-reference entry** is a project that another project schedules through a `ProjectReference`.

## Current behavior

The common CLI option forwards `--framework` as a global MSBuild property:

```text
--property:TargetFramework=<value>
```

See [`CommonOptions.CreateFrameworkOption`](../../src/Cli/Microsoft.DotNet.Cli.Definitions/Common/CommonOptions.cs).

MSBuild global properties override project properties. The solution build therefore forces the same framework onto every direct entry.

The implicit restore excludes `TargetFramework` and restores all declared target frameworks.

See [`RestoringCommand`](../../src/Cli/dotnet/Commands/Restore/RestoringCommand.cs).

The later build still forces one framework onto all projects. An incompatible direct entry then fails against its restored assets.

## Proposed user experience

### Direct project invocation

Direct project behavior does not change:

```console
dotnet build App.csproj --framework net10.0
```

`App.csproj` must declare `net10.0`.

Project references can select their nearest compatible frameworks through the existing project-reference protocol.

### Solution or traversal invocation

For a solution or traversal input, `--framework` specifies the desired target framework:

```console
dotnet build Repo.slnx --framework net10.0
```

The orchestrator performs these actions:

1. Read the target frameworks that each direct entry declares.
2. Verify that at least one direct entry declares the desired target framework.
3. For each direct entry, select an exact match when one exists.
4. Otherwise, select the nearest compatible framework for that entry.
5. Exclude an entry when no compatible framework exists.
6. Build each selected project configuration.

This operation selects projects, not all compatible target frameworks.

The command fails when no direct entry declares the desired target framework.

Compatibility alone cannot satisfy this requirement. For example, a container with only `net9.0` projects cannot satisfy a `net10.0` request.

Commands that run built assets use the requested framework as a runtime signal. Running only `net9.0` assets would not satisfy a `net10.0` request.

For example, this command fails when every direct project targets only `net9.0` or `netstandard2.0`:

```console
dotnet test Repo.slnx --framework net10.0
```

Those frameworks can be compatible with `net10.0`, but none represents the requested `net10.0` runtime.

### Example

Consider this graph:

```text
Repo.slnx
├── a.csproj  TargetFrameworks=net9.0;net10.0
└── b.csproj  TargetFramework=net9.0
    └── ProjectReference to a.csproj
```

This command requests `net10.0`:

```console
dotnet build Repo.slnx --framework net10.0
```

The solution selects these direct entries:

```text
a.csproj (net10.0)
b.csproj (net9.0)
```

The reference from `b.csproj` also requires `a.csproj (net9.0)`.

The complete build graph is:

```text
a.csproj (net10.0)
b.csproj (net9.0) -> a.csproj (net9.0)
```

`a.csproj` builds twice because two graph paths require different target frameworks.

This result is correct. Filtering applies only to direct entries and does not override project-reference requirements.

### Incompatible projects

The command omits a direct SDK project when none of its target frameworks are compatible.

The command prints a summary of omitted projects and their declared target frameworks.

The command fails with a clear SDK diagnostic when no compatible SDK projects remain.

The command also fails when compatible projects remain but no direct project declares the desired target framework.

Projects without target-framework metadata are not considered incompatible. The orchestrator preserves these projects without a forced `TargetFramework`.

This rule protects native and other non-SDK project types.

### Help text

The `--framework` description must distinguish project and solution behavior.

Suggested text:

> The target framework for a project. For a solution, select the nearest compatible framework for each project.

## Compatibility rules

The solution orchestrator must use the same framework compatibility rules as project references.

MSBuild obtains declared frameworks through the `GetTargetFrameworks` protocol.

See [MSBuild's project-reference protocol](https://github.com/dotnet/msbuild/blob/e45cc3de8a44d5b92cdad6e0ca7f8a5852c2afbd/documentation/ProjectReference-Protocol.md).

NuGet currently selects the nearest compatible framework for project references.

See [`GetReferenceNearestTargetFrameworkTask`](https://github.com/NuGet/NuGet.Client/blob/d6ddaa20a79e748b56a78c1f3522a13edb87255e/src/NuGet.Core/NuGet.Build.Tasks/GetReferenceNearestTargetFrameworkTask.cs).

The requested short target-framework name must resolve to its framework and platform monikers before selection.

Solution entry selection uses standard framework compatibility without `AssetTargetFallback`.

A solution has no consuming project that can supply one authoritative fallback list.

Normal project references continue to apply their parent project's `AssetTargetFallback`.

The implementation must not create a second framework compatibility algorithm.

## CLI property flow

The CLI must distinguish an explicit `--framework` option from a project-defined property.

The existing command-line sentinel pattern can provide this distinction:

```text
--property:TargetFramework=net10.0
--property:_CommandLineDefinedTargetFramework=true
--property:_CommandLineTargetFramework=net10.0
```

Project inputs continue to consume `TargetFramework` directly.

Solution and traversal inputs treat the command-line value as the desired target framework.

They must not pass the original global `TargetFramework` unchanged to every child project.

An explicit `/p:TargetFramework=net10.0` remains a global property override. It does not opt into solution negotiation.

The implicit restore must continue without the command-line target-framework constraint. This behavior makes all project assets available for selection.

The separate restore must remove all three command-line target-framework properties.

When both `--framework` and `/p:TargetFramework` are present, `--framework` keeps its current precedence.

The private `_CommandLineTargetFramework` value ensures that MSBuild argument ordering cannot change the desired framework.

## Orchestration design

The implementation must provide one reusable target-framework selection operation.

Solution metaprojects and `Microsoft.Build.Traversal` must consume the same operation.

The operation has these inputs:

- desired target framework
- direct project entries
- configuration and platform metadata
- per-entry global properties

The operation has these outputs:

- selected project entries
- selected target framework for each compatible entry
- direct entries that exactly match the desired target framework
- omitted incompatible entries
- unchanged entries without target-framework metadata

### Query declared frameworks

The operation calls `GetTargetFrameworks` for each direct project.

The call must remove `TargetFramework`, `RuntimeIdentifier`, and `SelfContained`.

The remaining property set must match the existing project-reference call when possible. Matching enables MSBuild result reuse.

See [`_GetProjectReferenceTargetFrameworkProperties`](https://github.com/dotnet/msbuild/blob/e45cc3de8a44d5b92cdad6e0ca7f8a5852c2afbd/src/Tasks/Microsoft.Common.CurrentVersion.targets#L1755-L1978).

### Select a framework

The operation applies the existing NuGet nearest-framework rules to each returned project entry.

Before nearest-framework selection, the operation verifies that at least one direct entry declares the desired target framework.

This exact-match gate applies independently to each container project. A solution, solution filter, or traversal project must have an exact direct match.

The current NuGet task reports an error when no compatible framework exists.

Solution selection needs a filtering result instead. The SDK and NuGet teams must agree on one reusable contract.

Possible contracts include:

1. Add a non-error filtering mode to the NuGet task.
2. Add a new NuGet task that separates compatible and incompatible entries.
3. Add an SDK task that uses the same public NuGet compatibility APIs.

The chosen contract must preserve NuGet compatibility and fallback behavior.

### Annotate selected entries

The orchestrator removes the original command-line `TargetFramework` from child project calls.

For a multi-targeted project, it sets `TargetFramework` to the selected framework.

For a single-targeted project, it lets the project use its declared framework.

The existing protocol uses `SetTargetFramework` and `UndefineProperties` metadata for these operations.

### Omit incompatible entries

The orchestrator removes incompatible projects only from the direct entry list.

It does not change each project's `ProjectReference` items.

Normal project-reference negotiation can still schedule a removed direct entry through another graph path.

## Solution integration

MSBuild generates a solution metaproject for command-line solution builds.

The generated project imports SDK-owned solution targets from `SolutionFile/ImportAfter`.

See [`Microsoft.NET.Sdk.Solution.targets`](../../src/Tasks/Microsoft.NET.Build.Extensions.Tasks/msbuildExtensions-ver/SolutionFile/ImportAfter/Microsoft.NET.Sdk.Solution.targets).

The SDK can use this import to run selection before aggregate solution targets.

The integration must cover aggregate targets that accept a framework selection.

The first implementation covers these solution targets:

- `Build`
- `Rebuild`
- `Clean`
- `VSTest`
- `_MTPBuild`

`Publish` needs separate validation before it uses the same selection operation.

Per-project solution targets need equivalent behavior:

```console
dotnet build Repo.slnx --framework net10.0 --target ProjectName
```

The implementation must also preserve solution configuration exclusions and solution dependency metadata.

## Traversal project integration

`Microsoft.Build.Traversal` schedules its `ProjectReference` items directly.

Traversal targets already consume per-reference `SetTargetFramework` metadata.

See [`Traversal.targets`](https://github.com/microsoft/MSBuildSdks/blob/main/src/Traversal/Sdk/Traversal.targets).

The traversal SDK should call the shared selection operation before it invokes its child projects.

Traversal support can ship separately from the .NET SDK implementation.

Nested traversal projects must not select or test the same project twice for the same global-property set.

Microsoft Testing Platform traversal deduplication must include the selected target framework.

Its key becomes project path, configuration, platform, and selected target framework.

## `dotnet test` integration

Both test implementations must consume the same selected project set.

The VSTest path uses solution and traversal MSBuild targets.

The Microsoft Testing Platform path also expands solutions and traversal projects in CLI code.

See [`SolutionAndProjectUtility`](../../src/Cli/dotnet/Commands/Test/MTP/SolutionAndProjectUtility.cs).

The Microsoft Testing Platform path must apply equivalent selection before it creates test modules.

It must execute the shared `GetTargetFrameworks` operation for each direct project.

Reading only the evaluated `TargetFrameworks` property is insufficient because a target can contribute framework metadata.

The implementation can invoke the shared operation through the existing MSBuild build session.

Both test paths must consume equivalent protocol outputs.

`--framework` continues to select one test target framework per direct test project.

Source dependencies can build additional frameworks through normal project references.

The `Test` target is an orchestration target. Project references do not normally invoke `Test` on dependencies.

This difference is important when a project appears through multiple direct traversal paths.

## Command scope

The proposal applies when a command accepts `--framework` and accepts a solution or traversal input.

The first implementation covers `build`, `clean`, and both `test` paths.

`publish` needs separate validation because solution publish has additional restrictions.

`pack` does not currently expose the common `--framework` option. It is outside this proposal.

`run` remains a project operation. It is outside this proposal.

## Diagnostics

The feature needs these diagnostics:

### Invalid desired framework

Fail when the desired framework cannot be parsed.

### No compatible projects

Fail when the input contains SDK projects but none are compatible.

The diagnostic must name the desired framework and the input solution or traversal project.

### No direct exact match

Fail when no direct project declares the desired target framework.

The diagnostic must name the desired framework and the container project.

The diagnostic should list the target frameworks declared by the direct projects.

This error applies even when one or more direct projects declare a nearest-compatible framework.

### Omitted projects

Print one summary when some direct entries are incompatible.

The summary must include each omitted project and its declared target frameworks.

### Project query failure

Preserve the current build error when `GetTargetFrameworks` fails.

Do not classify a failed query as an incompatible project.

### Existing asset errors

Improve `NETSDK1005` guidance when an explicit command-line framework caused the mismatch.

The new selection should prevent this error for supported solution and traversal scenarios.

## Performance

Selection adds one `GetTargetFrameworks` request for each direct entry.

This work occurs only when the user specifies `--framework`.

The requests must run in parallel when the build allows parallel work.

The query must match the project-reference protocol property set. This requirement maximizes MSBuild result reuse.

Multi-targeted projects can require one inner evaluation per declared framework.

Some graphs will build a project for multiple frameworks after selection. This result can increase total build time.

Performance validation must include large solutions with mixed target frameworks.

The implementation must measure:

- orchestration time before project builds start
- project evaluation count
- project build count
- total build time
- behavior with and without `--framework`

There must be no measurable regression when `--framework` is absent.

## Static graph builds

Static graph build represents each target-framework configuration as a separate graph node.

This model can select entry nodes before execution. It can also preserve independently negotiated dependency nodes.

Static graph support needs an MSBuild design because graph construction occurs before SDK targets execute.

The initial implementation must not silently ignore solution selection during graph build.

It must either support the behavior or report a clear unsupported-mode diagnostic.

## Compatibility

### Existing successful builds

A solution where every project directly declares the requested framework keeps the same selected frameworks.

### Existing failed builds

A mixed-framework solution can change from failure to a successful subset build.

This change is intentional. The omitted-project summary makes the new behavior visible.

A solution without a direct exact match continues to fail, even when all direct projects are compatible with the requested framework.

### Explicit MSBuild properties

`/p:TargetFramework=...` keeps its existing global-property semantics.

This rule provides an escape hatch for callers that require the current force behavior.

### Non-SDK projects

A project that does not implement `GetTargetFrameworks` remains in the direct entry list.

The orchestrator removes the solution-level `TargetFramework` before it invokes that project.

### Visual Studio

Visual Studio does not use the command-line solution metaproject for normal builds.

This proposal changes command-line orchestration only.

## Alternatives

### Keep the current global property behavior

This option is simple and preserves current failures.

It does not support mixed-framework solutions.

### Filter only exact matches

This option selects projects that directly declare the desired framework.

It rejects compatible projects that a normal project reference can consume.

This difference preserves the current solution and project inconsistency.

### Build every compatible framework

This option can schedule many frameworks from each project.

It conflicts with the current meaning of `--framework`, which selects one framework.

### Add `--compatible-framework`

A new option avoids changing `--framework`.

The existing solution behavior is already unusable for mixed-framework inputs. A second option also creates two competing framework-selection models.

This proposal therefore changes solution-level `--framework`.

### Select only graph tips

The orchestrator could select projects without incoming project-reference edges.

This option can reduce duplicate direct builds. It also requires complete and correct graph construction before selection.

Solutions do not currently identify application, test, or package entry projects.

Targets such as `Test` can require direct entries that are not build-graph tips.

This option remains future work.

### Add a framework-list command

A command could list each project's declared frameworks for scripts.

This option moves orchestration into each caller and does not fix normal CLI behavior.

## Open questions

1. Should omitted projects produce a message or a warning during the first release?
2. Which `publish` scenarios can safely use project filtering?
3. How should solution project metaproject entries participate in selection?
4. Which repository owns the reusable compatibility task?
5. How should Microsoft Testing Platform share selection results with MSBuild?
6. How should static graph builds express desired entry frameworks?
7. Should a feature switch preserve the old solution behavior during rollout?

## Test plan

### Framework selection

- Exact target-framework match
- Nearest compatible target-framework match
- Nearest compatible projects with no direct exact match
- Cross-family match from `.NET` to `.NET Standard`
- No compatible target framework
- Invalid desired target framework
- Platform-specific target frameworks
- Asset fallback

### Graph behavior

- One project with multiple target frameworks
- Mixed-framework solution
- Direct project and transitive reference to different framework instances
- Diamond references
- Nested traversal projects
- Project excluded by solution configuration
- Solution dependency metadata

### Project types

- SDK project
- Single-targeted project
- Multi-targeted project
- Native project
- Project without `GetTargetFrameworks`
- Project represented by a generated metaproject

### Commands

- `dotnet build`
- `dotnet clean`
- `dotnet publish`
- VSTest `dotnet test`
- Microsoft Testing Platform `dotnet test`
- Per-project solution targets

### Input formats

- `.sln`
- `.slnx`
- solution filter
- `Microsoft.Build.Traversal`
- static graph build

### Restore

- Implicit restore includes assets for all declared frameworks.
- `--no-restore` uses existing complete assets.
- Explicit `/p:TargetFramework` preserves current behavior.

### Performance

- Large solution without `--framework`
- Large solution with exact matches
- Large solution with mixed compatible frameworks
- Large solution with many multi-targeted projects

## Ownership

The implementation crosses these component boundaries:

| Area | Owner | Responsibility |
| --- | --- | --- |
| CLI | `dotnet/sdk` | Mark an explicit `--framework` and preserve restore behavior |
| Solution targets | `dotnet/sdk` and `dotnet/msbuild` | Select and annotate direct solution entries |
| Compatibility | `NuGet/NuGet.Client` | Define nearest-framework selection behavior |
| Traversal SDK | `microsoft/MSBuildSdks` | Apply the shared operation to traversal entries |
| Test orchestration | `dotnet/sdk` | Keep VSTest and Microsoft Testing Platform behavior aligned |

NuGet review is required before the compatibility contract is final.
