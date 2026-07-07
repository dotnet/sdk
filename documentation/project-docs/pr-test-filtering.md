# PR Test Filtering

Not all tests need to run on every PR. This document describes the framework for
conditionally running tests based on what changes in a pull request — skipping expensive
or unrelated test suites when the source paths they cover have not changed, while always
running the full suite on CI builds (e.g. `main`, release branches).

## Motivation

The dotnet/sdk repository is consistently one of the top consumers of Helix compute
resources — typically second only to dotnet/runtime. A single PR validation run consumes
on the order of **2,000 minutes of Helix compute** across all platforms and test legs.
This has real consequences:

- **Cost**: The cumulative Helix compute consumed by SDK builds is substantial. Every PR
  build runs the full test suite across multiple platforms (Windows x64, Linux x64,
  macOS arm64, .NET Framework), and each minute of unnecessary test execution adds up.
- **Queue pressure**: Helix compute capacity is finite and shared across the .NET
  organization. There are frequent periods where agent availability is constrained and
  queue times become significant. When SDK PRs occupy agents running tests that are
  irrelevant to the change, jobs across the organization wait longer to start.
- **PR velocity**: Even when agents are available, running all tests unconditionally means
  PRs take longer to validate than necessary, slowing down the development loop.

Conditionally running tests based on what changed in a PR is an effective mechanism to
reduce compute consumption. However, there is no single "smoking gun" test suite whose
removal would drastically change the picture. Instead, the gains come from collectively
applying conditional filtering across many areas. Some of the initial candidates include:

- Filtering **TemplateEngine** tests saves on the order of **40 minutes** per PR.
- Filtering **NetAnalyzers** tests saves on the order of **160 minutes** per PR.
- Filtering **dotnet-watch** tests saves on the order of **120 minutes** per PR.
- Filtering **ILLink** tests saves on the order of **100 minutes** per PR.
- Filtering **ApiCompat/ApiDiff** tests saves on the order of **30 minutes** per PR.

Even these five scopes together represent only ~22% of the total 2,000-minute budget.
Meaningful overall reduction requires applying this pattern broadly across many test
areas — but each scope added compounds the savings and frees capacity for the rest of
the organization.

The goal is to define scopes covering a minimum of one-third tp one-half of total PR test time.
Beyond that, additional scopes are a return-on-investment decision — if a scope is
straightforward to define with reliable trigger paths, it should be added; if it
requires complex dependency analysis or invasive refactoring, the effort may not be
justified.

## Filtering mechanisms

There are several levels at which tests can be conditionally excluded:

### Project-level filtering

Entire test projects are excluded from Helix submission. This is the coarsest and
simplest mechanism — the `.csproj` is removed from the set of projects sent to Helix,
so none of its tests run. Best suited for subsystems that have their own dedicated test
project(s) (e.g. TemplateEngine tests).

### Class-level filtering (future)

Individual test classes are excluded via `[TestCategory]` attributes and MSTest filter
expressions. The test project still runs on Helix, but specific classes are skipped.
This is useful when expensive tests (e.g. ILLink trimming tests) live in a shared test
assembly alongside cheaper tests. Not yet implemented.

### Method-level filtering (future)

Similar to class-level but targeting individual test methods. Likely too granular to be
practical in most cases. Not yet implemented.

## Current implementation

The framework currently supports **project-level filtering** with a **RunAlways=CI**
condition. The definitive source of truth for what scopes exist and how they are
configured is:

> **[`test/ConditionalTests.props`](../../test/ConditionalTests.props)**

Refer to that file for the list of active scopes, their trigger paths, and which test
projects they control.

### How it works

1. **`test/ConditionalTests.props`** — MSBuild props file that defines `ConditionalTestScope`
   items. Each scope specifies:
   - `Mechanism`: how tests are excluded (currently only `project` is supported)
   - `TestProjects`: the test `.csproj` file(s) controlled by this scope
   - `TriggerPaths`: glob patterns for source/test paths that activate this scope
   - `RunAlways`: conditions under which the scope always runs (currently `CI`).
     A future enhancement could add dependency flow PRs as a condition — for example,
     `RunAlways=DependencyFlow` to always run on any dependency flow PR, or a more
     targeted `RunAlways=DependencyFlow:dotnet/dotnet` to only force-run when the flow
     originates from a specific repository.

2. **`scripts/EvaluateConditionalTestScopes.cs`** — C# script that runs before test
   submission. It reads `ConditionalTests.props`, computes the git diff against the
   target branch, and determines which scopes are active. It outputs the
   `ActiveConditionalTestScopes` Azure DevOps pipeline variable.

3. **`test/UnitTests.proj`** — imports `ConditionalTests.props` and defines a Target
   (`RemoveInactiveConditionalTestProjects`) that removes inactive test projects from
   the `SDKCustomTestProject` item group before Helix submission.

### Decision flow

```text
Is this a PR build?
├── No (CI) → All scopes active, all tests run
└── Yes (PR) → For each scope:
    ├── Do changed files match any TriggerPaths? → Scope active
    └── No match → Scope inactive, test projects excluded
```

### Safe defaults

- **Local development**: `ActiveConditionalTestScopes` is not set → no projects are
  removed → all tests run.
- **Git diff failure**: the script returns an empty changed-file list → all scopes
  activate (safe fallback).
- **Non-PR builds**: `RunAlways=CI` ensures all scopes are active on `main` / release
  branches.

## Adding a new scope

1. Add a `<ConditionalTestScope>` item in `test/ConditionalTests.props`.
2. That's it — the evaluation script and `UnitTests.proj` are generic and require no
   per-scope changes.

Example:

```xml
<ConditionalTestScope Include="MyFeature">
  <Mechanism>project</Mechanism>
  <TestProjects>MyFeature.Tests\MyFeature.Tests.csproj</TestProjects>
  <TriggerPaths>
    src/MyFeature/**;
    test/MyFeature.Tests/**
  </TriggerPaths>
  <RunAlways>CI</RunAlways>
</ConditionalTestScope>
```

### Choosing trigger paths

Trigger paths are not simply "the folder containing the feature's source code." When
defining a scope, consider whether changes to **dependencies** of that feature should
also trigger its tests. For example:

- If `dotnet-watch` has a `ProjectReference` to `Microsoft.DotNet.Cli.Definitions`, a
  change to that project could break watch behavior — so the watch scope's trigger paths
  should include both `src/Dotnet.Watch/**` and
  `src/Cli/Microsoft.DotNet.Cli.Definitions/**`.
- Shared infrastructure or utility projects that multiple features depend on may need to
  appear in multiple scopes' trigger paths.

Use judgment here. If a feature has complex or far-reaching dependencies that make it
difficult to define a reliable set of trigger paths, it may not be a good candidate for
conditional filtering — it is better to run tests unconditionally than to skip them when
a dependency change would have caused a failure.

## Design principles

- **Single source of truth**: `test/ConditionalTests.props` defines everything about a
  scope — trigger paths, projects, and conditions. Adding or removing a scope is a
  one-file change.
- **Safe by default**: when in doubt, tests run. The system only skips tests when it has
  positive evidence that no relevant files changed.
- **No extra build legs**: filtering happens within the existing build/test pipeline.
  Projects are excluded from Helix submission but the build leg itself is unchanged,
  avoiding the compute cost of building everything twice.
- **Generic infrastructure**: `UnitTests.proj` and the evaluation script have no
  knowledge of specific scopes. All scope-specific configuration lives in the props file.
