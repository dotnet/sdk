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

The goal is to define scopes covering a minimum of one-third to one-half of total PR test time.
Beyond that, additional scopes are a return-on-investment decision — if a scope is
straightforward to define with reliable trigger paths, it should be added; if it
requires complex dependency analysis or invasive refactoring, the effort may not be
justified.

Longer-term, better solutions may emerge — for example, AI-based test selection
(as offered by CloudTest), or improved incremental build support that avoids rebuilding
and rerunning test assemblies whose inputs have not changed. These would provide a
better experience but are aspirational today. PR velocity is the primary driver to
address this now, and this manual, declarative approach is the pragmatic solution
available to do so.

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

1. **`test/ConditionalTests.props`** — MSBuild props file that defines
   `ConditionalTestScope` items and a `GlobalTriggerPaths` property.

   Each **conditional test scope** specifies:
   - `Mechanism`: how tests are excluded (currently only `project` is supported)
   - `TestProjects`: the test `.csproj` file(s) controlled by this scope
   - `TriggerPaths`: glob patterns for source/test paths that activate this scope
   - `RunAlways`: conditions under which the scope always runs regardless of changed
     files. The only supported value today is `CI` (non-PR builds). Flowed-in package
     dependencies are handled globally: `eng/Version.Details.xml` is a `GlobalTriggerPaths`
     entry, so any dependency update forces all scopes to run — see
     [Handling flowed-in dependencies](#handling-flowed-in-dependencies).

   **Global trigger paths** are an escape mechanism: if any changed file matches a
   `GlobalTriggerPaths` pattern, all scopes are forced active and no tests are filtered.
   Use this for shared infrastructure that all code/tests depend on (e.g. the shared test
   framework assemblies).

2. **`scripts/EvaluateConditionalTestScopes.cs`** — C# script that runs before test
   submission. It reads `ConditionalTests.props`, computes the git diff against the
   target branch, and determines which scopes to skip. It outputs the
   `SkippedTestScopes` Azure DevOps pipeline variable.

3. **`test/UnitTests.proj`** — imports `ConditionalTests.props` and defines a Target
   (`RemoveSkippedConditionalTestProjects`) that removes skipped test projects from
   the `SDKCustomTestProject` item group before Helix submission.

### Decision flow

```text
Is this a PR build?
├── No (CI) → RunAlways=CI → nothing skipped, all tests run
└── Yes (PR) →
    ├── Do changed files match any GlobalTriggerPaths? → Nothing skipped, all tests run
    └── No global match → For each scope:
        ├── Do changed files match any TriggerPaths? → Scope runs (not skipped)
        └── No match → Scope skipped, test projects excluded
```

### Safe defaults

- **Local development**: `SkippedTestScopes` is not set → no projects are
  removed → all tests run.
- **Git diff failure**: the script returns an empty changed-file list → no scopes
  are skipped (safe fallback).
- **Global trigger match**: shared infrastructure changed → variable is not set →
  all tests run.
- **Non-PR builds**: `RunAlways=CI` ensures no scopes are skipped on `main` / release
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
- Test assets (e.g. `test/TestAssets/TestPackages/`) that a test suite consumes at
  runtime should also be included. These are easy to overlook because they are not
  referenced via `ProjectReference`, but changes to them can cause test failures just
  the same.

Use judgment here. If a feature has complex or far-reaching dependencies that make it
difficult to define a reliable set of trigger paths, it may not be a good candidate for
conditional filtering — it is better to run tests unconditionally than to skip them when
a dependency change would have caused a failure.

#### Handling flowed-in dependencies

Some scopes build against packages that arrive through dependency flow (Roslyn, the
runtime, MSTest, etc.) rather than source in this repo. Every dependency update — whether
a VMR flow (`dotnet/dotnet`) or an external one such as `microsoft/testfx` — rewrites
`eng/Version.Details.xml`.

Rather than asking each scope to add that file to its own trigger paths, it is a
**`GlobalTriggerPaths`** entry: any change to `eng/Version.Details.xml` forces **all**
scopes to run. This is intentional and errs on the side of caution:

- A VMR flow bumps the compiler, runtime, and other packages that essentially every test
  area depends on — a genuinely repo-wide change worth validating broadly.
- Making it global is safe-by-default: new scopes are covered automatically, with no risk
  of an author forgetting to opt in and silently skipping tests a dependency bump could break.

The trade-off is that dependency-flow PRs get no filtering savings, but those are a
minority of PRs and exactly the ones where full validation is most valuable. If this proves
too coarse, it can be tuned later — see [Future enhancements](#future-enhancements).

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

## Future enhancements

- **Per-repo dependency conditions**: `eng/Version.Details.xml` is a global trigger path, so
  *every* dependency update runs the full suite, even flows from a repo a given scope doesn't
  depend on. A finer-grained `RunAlways=Dependency:<owner>/<repo>` condition could force only the
  scopes that depend on the repo whose dependency actually changed. Identifying which repo a change
  came from is the hard part, and there are a couple of possible implementations — inspecting the
  `eng/Version.Details.xml` diff to see which dependencies changed, or detecting that the PR itself
  is a dependency flow (e.g. from its author/branch/body) and which repo it flows from. Both are
  non-trivial and somewhat fragile. This was explored but **intentionally not adopted**: there is no
  return on investment today. Only two dependency flows exist (the VMR and `microsoft/testfx`), and
  running the full suite on either is desirable anyway — a VMR flow is a large, wide-reaching change,
  and a testfx flow updates the test infrastructure itself. The machinery to distinguish them adds
  meaningful complexity for savings that, at present, do not materialize. If additional, more
  isolated dependency flows appear where broadly running tests is genuinely wasteful, revisiting a
  per-repo condition (and narrowing the global trigger) would make sense.
- **Force all tests from a PR**: Contributors may want to override conditional
  filtering and run the full test suite on a specific PR. If this proves desirable, one
  possible approach is a PR label (e.g. `run-all-tests`) that the pipeline checks before
  evaluating scopes. Locally, all tests run by default, and manually queued CI runs can
  override the `SkippedTestScopes` pipeline variable.
