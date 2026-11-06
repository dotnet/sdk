---
name: add-dotnet-aot-command
description: >
  Add, enable, or review a dotnet CLI command or feature in the Native AOT CLI
  (src/Cli/dotnet-aot) and prove its compatibility. USE FOR: migrating a command or
  option to AOT, reviewing an AOT migration PR, defining conservative eligibility and
  managed fallback, changing AotSourceFiles.props or AotDependencies.props, validating
  AOT/managed parity, NativeAOT-publishing tests, checking binary-size impact, or using
  the dn harness and separated SDK layout. DO NOT USE FOR: resolving IL trim/AOT
  analyzer warnings alone (use dotnet-aot-compat), running dotnet.Tests incrementally
  (use incremental-test), or pure managed CLI work.
license: MIT
---

# add-dotnet-aot-command

Use this workflow to author or review a Native AOT CLI migration. The goal is not merely to make a
command compile in the native library. The goal is to preserve the managed CLI contract, fall back
before the AOT path commits unsupported work, and prove the actual native artifact and layout.

Before changing or reviewing code, verify the current behavior in the owning source and tests. Then read:

- `src/Cli/dotnet-aot/DESIGN.md` for the current host and fallback architecture.
- `src/Cli/dotnet-aot/SdkRootResolution.md` when code resolves SDK-relative content.

Commands below assume PowerShell from the repository root. Adjust the RID for the host. Do not hard-code
the target framework; the projects and harness discover `$(SdkTargetFramework)`.

## Non-negotiable contracts

1. **One CLI, two execution bubbles.** Reuse shared definitions, implementations, helpers, and
   resources. Do not create an AOT-only parser or command copy when the managed owner can be linked.
2. **Eligibility is an allowlist.** Unknown options, ambiguous operands, dynamic parser extensions,
   and unsupported combinations fall back to managed execution.
3. **Fallback has a commit point.** Complete every fallback predicate before command output, command
   telemetry ownership, file/cache mutation, restore, build, or process launch. Split probing from
   execution or buffer output if necessary.
4. **Precedence is behavior.** Preserve ordering among built-ins, external/tool commands, projects,
   explicit files, positional files, and shorthand forms.
5. **The native artifact is the test subject.** Managed builds and in-process tests do not replace a
   clean Native AOT publish, native test run, assembled `dn` run, or platform run.
6. **Size is part of the change.** Report the native-library delta and largest rooted dependencies for
   meaningful closure changes.

## How the AOT CLI is assembled

`dotnet-aot` is a Native AOT shared library (`NativeLib=Shared`) loaded by the host. It exports
`dotnet_execute`; `NativeEntryPoint` either invokes shared CLI code in-process or hosts the existing
managed `dotnet.dll` through hostfxr. `DOTNET_CLI_ENABLEAOT=false` is the explicit opt-out. Unsupported
command shapes use `CommandNotAvailableInAotException` or an earlier entry-point decision to request
managed fallback.

The closure has two owners:

- `AotSourceFiles.props`: linked source files and generated/embedded resources. It is imported by the
  product and test projects so they compile the same command surface.
- `AotDependencies.props`: package/project references, framework references, runtime feature switches,
  and dependency-owned target imports shared by product and tests.

Keep `dotnet-aot.csproj` focused on native compiler/linker and project configuration. `CLI_AOT` gates
the smallest incompatible portions of shared files; `#if !CLI_AOT` must leave the managed path intact.
`DotnetCsproj` is also defined and can expose an unexpected transitive source closure.

Types already available (do **not** re-add their sources): `Microsoft.DotNet.Cli.Utils`,
`Microsoft.DotNet.Configurer`, `Microsoft.DotNet.Cli.Definitions`, `Microsoft.DotNet.ProjectTools`,
`Microsoft.DotNet.NativeWrapper`, `Microsoft.NET.Sdk.WorkloadManifestReader`. `Cli.Utils` grants
`InternalsVisibleTo` to `dotnet-aot` and `dotnet-aot.Tests`, so its `internal` types (including the
CsWin32 `Windows.Win32.*` COM types and helpers `ComScope`, `BSTR`, `HRESULT`, `CLSID`) are usable
without re-wiring CsWin32.

## Resolving the versioned SDK root (do NOT use BCL path APIs)

The muxer loads `dotnet-aot.dll` directly from the versioned SDK directory (e.g. `.../sdk/11.0.100/`),
but inside that process the BCL "where am I" APIs do **not** point there:

- `AppContext.BaseDirectory`, `Environment.ProcessPath`, `Process.GetCurrentProcess().MainModule` -> the
  **muxer / install root**.
- `Assembly.Location` -> the **empty string** (ILC hard-errors with `IL3000`).

So deriving an SDK-relative path (`MSBuild.dll`, `Sdks/`, `DotnetTools/`, targets) from
`AppContext.BaseDirectory` or a dll path is **wrong** in the AOT bubble. Instead:

- **In-repo:** read `SdkPaths.SdkDirectory` (in `Microsoft.DotNet.Cli.CoreUtils`), which resolves the
  `Microsoft.DotNet.Sdk.Root` AppContext value -> SDK assembly directory -> `AppContext.BaseDirectory`
  (once, cached).
- `NativeEntryPoint.ExecuteCore` resolves the SDK directory once (host `sdk_dir`, else self-locating the
  `dotnet-aot` module via `SdkRootLocator`) and **publishes it as the `Microsoft.DotNet.Sdk.Root`
  AppContext value** for the compiled-in assemblies.
- **Out-of-repo code** (MSBuild tasks, NuGet, runtime - no `Cli.Utils` reference) replicates the contract
  inline: read the `Microsoft.DotNet.Sdk.Root` AppContext value first, else the existing BCL logic.

  ```csharp
  string sdkDirectory =
      AppContext.GetData("Microsoft.DotNet.Sdk.Root") is string sdkRoot && sdkRoot.Length > 0
          ? sdkRoot
          : /* existing logic, e.g. AppContext.BaseDirectory */;
  ```

When bringing a command into AOT, switch any `AppContext.BaseDirectory` / `Assembly.Location` used as
"the SDK directory" to the above contract. Search current call sites rather than maintaining a static
list in this skill; update `SdkRootResolution.md` when the contract or ownership changes.

## Author workflow

### 1. Measure the managed contract

Run the managed CLI before editing and capture a behavior matrix:

- Successful, help, malformed, missing, and ambiguous forms.
- Every option/operand shape intended for AOT, one unknown option, and nearby managed-only forms.
- Exit code, stdout, stderr, created/changed/deleted files, cache state, restore/build invocations, and
  child processes.
- External-command, project, explicit-file, positional-file, and shorthand collisions where relevant.
- First-run, telemetry opt-out, cancellation, SDK selection, and existing-artifact state where relevant.

Measure behavior; do not infer it from option names or a reading of the implementation.

### 2. Define eligibility before implementation

Write a table in the PR notes or tests for every supported and unsupported shape. Name the exact signal
available before output or mutation:

| Shape | Result | Pre-commit signal | Required evidence |
| --- | --- | --- | --- |
| Supported | Handle in AOT | Complete allowlisted parse plus supported input/service state | Native success and managed parity |
| Known unsupported | Fall back | Explicit option/input/service predicate | Entry-point fallback assertion and real managed result |
| Unknown or ambiguous | Fall back | Not in the allowlist | Negative parser/entry-point test |
| Failure after commit | Return AOT result/error | Eligibility already established | Native failure/output/side-effect parity |

If the signal requires output-producing or mutating work, introduce a probe/execute split or buffer the
output until AOT commits. Do not use a broad catch to replay an invocation after work begins.

### 3. Reuse the managed owner

Trace the definition to the code that directly computes or mutates behavior. Prefer, in order:

1. Existing referenced assembly/API.
2. Existing shared source and resources.
3. A pure helper extracted without changing its contract.
4. A narrow `CLI_AOT` guard around the incompatible member or action.
5. Explicit managed fallback for dynamic/reflection-heavy or otherwise unsupported behavior.

Keep fallback local to the owning command action. Do not remove definitions from the command tree or
teach `NativeEntryPoint` command-specific semantics when `CommandNotAvailableInAotException` suffices.

### 4. Extend the closure in the owning props file

- Add source and resources to `AotSourceFiles.props` in the existing owner group or a labeled
  command-specific group. Reuse common scaffolding; duplicate `Compile`/`EmbeddedResource` items fail.
- Add package/project/framework references, runtime feature switches, and dependency target imports to
  `AotDependencies.props`. Normal package versions remain centrally managed.
- Put Windows-only source/dependencies under `Condition="'$(TargetOS)' == 'windows'"`.
- Keep native compiler/linker configuration in `dotnet-aot.csproj`.

Inspect each dependency for trim/AOT warnings, reflection/dynamic code, serialization contracts,
resources/localization, build assets, static initialization, process-global state, native libraries,
and binary-size contribution.

### 5. Preserve both preprocessor views

Condition the smallest incompatible block. Compare the managed and AOT views for lost comments,
changed accessibility, relaxed nullable/analyzer settings, altered resources, and unrelated cleanup.
Run the existing managed tests for every shared implementation changed.

### 6. Add tests that identify the selected path

Add the smallest applicable layers:

1. Parser/definition tests for accepted and rejected syntax.
2. Entry-point tests that observe handled versus managed fallback before a real host transition.
3. Shared command tests for host-independent semantics.
4. Native-published tests for the actual AOT closure.
5. `dn` integration for native loading, hostfxr fallback, output, and process state.
6. Separated-layout tests for SDK-root consumers.
7. Managed/AOT parity tests for output, exit code, side effects, and work performed.

Make the fixture capable of reaching the asserted branch. Mutate an eligibility predicate or the input
shape and confirm the focused test fails for the intended reason. A green skipped native test is not
native evidence.

### 7. Update contracts and report evidence

Update `DESIGN.md`, `SdkRootResolution.md`, resources/help, tests, and this skill when the change alters
a documented invariant. Prefer decision rules over static command inventories.

The completion report must name supported/unsupported shapes, commit point, shared files, new dependency
roots, warning treatment, exact validation executed, skipped tests, parity results, platform coverage,
and binary-size delta.

## Closure and implementation gotchas

- **MSBuild XML comments cannot contain `--`** (`MSB4024`) or end with `-`.
- **`Microsoft.Build` does not flow transitively from `Cli.Utils`** because its reference excludes the
  runtime asset. Keep required direct MSBuild packages and target imports in `AotDependencies.props`.
- **`DotnetCsproj` is defined for dotnet-aot.** A newly linked file can expose extra conditional code.
  Inspect that closure and narrow the owning conditional; do not copy a helper merely to avoid tracing it.
- **Do not pass `-noRestore` with `-getItem`.** The response file already appends it (`MSB1001`).
- **`dotnet-aot.Tests` uses Microsoft.Testing.Platform.** Invoke the
  [`run-tests`](../run-tests/SKILL.md) skill for a
  focused managed iteration and use `run-aot-tests.ps1` for native execution.
- **Existing tests may assert exclusions.** Search before enabling a command; an intentional
  `DoesNotContain` can need a carefully justified inversion.
- **A Roslyn pragma is not native-publish evidence.** ILC can report the same trim/AOT warning during
  publish. Use **dotnet-aot-compat**, keep suppression scope narrow, and reference an existing tracking
  issue for temporary dependency warnings. Ask the user before creating a new issue.
- **A shared Native AOT library is not a standalone AOT app.** On each affected OS, check interaction
  with native libraries and process-global state already loaded by the host.
- **Flat layouts can hide SDK-root defects.** Use `-Layout Separated`; add `-SelfLocate` to exercise the
  native-module fallback.

## Reviewer workflow

For a review, also follow the repository **code-review** skill's findings-first output rules. Use this
rubric to find AOT-specific defects.

### 1. Reconstruct both execution paths

Trace host arguments and SDK root, parser/action selection, every eligibility predicate, first observable
effect, AOT execution/telemetry ownership, and managed host transition. If any fallback predicate follows
stdout/stderr, mutation, restore/build, cache work, or process launch, flag the commit-point violation.

### 2. Compare with the managed owner

Look for copied definitions, bodies, validators, resources, path logic, diagnostics, and exception
mapping. Require a concrete reason not to share. Review both sides of every changed `CLI_AOT` block for
lost managed behavior, comments, tests, or analyzer coverage.

### 3. Attack the allowlist

Try an unknown option, a managed-only option, wrong operand counts, ambiguous discovery, dynamic help,
existing artifacts, and command/project/file/external collisions. Require clean managed fallback before
AOT output or mutation, and require tests to observe which path ran.

### 4. Audit the closure and size

Verify source/resources and dependencies are in their respective props files. Check package build assets,
feature switches, reflection, serializers, direct P/Invoke, platform-native libraries, static state, and
warning suppressions. Request clean-build and native-publish evidence. For size growth, inspect rooted
dependency paths rather than only changed source lines.

### 5. Validate the tests

Confirm native artifacts existed and ran, skipped counts are reported, process streams cannot deadlock,
paths are platform-neutral, process state is restored, fixtures reach the branch, and assertions inspect
real values rather than headers. Check pre-existing managed tests when code moved behind conditionals.

### 6. Challenge claims with the validation matrix

Compilation does not prove ILC compatibility. Native publish does not prove native tests ran. Flat `dn`
does not prove SDK-root behavior. `-Mode Compare` compares captured output but does not by itself prove
exit-code or side-effect parity. One host RID does not prove other platforms. Require evidence matching
each claim and label unresolved questions as questions rather than defects.

Common blockers:

- Denylist eligibility or unknown options handled in AOT.
- Fallback after output, telemetry ownership, or non-idempotent work.
- AOT-only copies of managed behavior or resources.
- Package/project references in `AotSourceFiles.props`.
- Incremental-only build evidence after closure changes.
- Native tests skipped because `dn` or the published executable was absent.
- Flat-layout SDK-root claims.
- Unexplained warning suppression, feature switch, or binary-size increase.
- Open-PR behavior described as current `main` behavior.

## Validation ladder

Run the cheapest discriminating check after each edit, then broaden. Invoke the
[`run-tests`](../run-tests/SKILL.md) skill when
the request explicitly asks to select or run SDK tests.

### 1. Focused parser/entry-point test

Invoke the [`run-tests`](../run-tests/SKILL.md) skill for
`test/dotnet-aot.Tests/dotnet-aot.Tests.csproj` with a
filter for the affected test. It builds the project, resolves the evaluated `TargetPath`,
and invokes the Microsoft.Testing.Platform application without assuming its generated
executable is on `PATH`.

Record whether the test asserts AOT handling, fallback, or semantics. Do not call this a native run.

### 2. Managed and clean closure builds

```powershell
.\.dotnet\dotnet.exe build src\Cli\dotnet\dotnet.csproj -c Debug
.\.dotnet\dotnet.exe clean src\Cli\dotnet-aot\dotnet-aot.csproj -c Debug
.\.dotnet\dotnet.exe clean test\dotnet-aot.Tests\dotnet-aot.Tests.csproj -c Debug
.\.dotnet\dotnet.exe build src\Cli\dotnet-aot\dotnet-aot.csproj -c Debug
.\.dotnet\dotnet.exe build test\dotnet-aot.Tests\dotnet-aot.Tests.csproj -c Debug
```

The clean builds are mandatory after source/resource/dependency closure changes; stale intermediates have
previously hidden missing includes.

### 3. Product Native AOT publish

```powershell
.\.dotnet\dotnet.exe publish src\Cli\dotnet-aot\dotnet-aot.csproj -r win-x64 -c Debug
```

This is the ILC/linker check. Investigate product warnings separately from known test-only rollups. Run
on each affected OS rather than cross-publishing and assuming native execution.

### 4. Native-published test suite

```powershell
.\test\dotnet-aot.Tests\run-aot-tests.ps1 -Trx
```

The script publishes the MTP test application, product `dotnet-aot` library, and `dn` host with Native
AOT. It loads the test-built library while using a complete installed SDK for SDK-relative assets and
managed fallback, then runs the suite and can emit a TRX. Report executed, passed, failed, and skipped
counts; required `dn` integration tests must not be skipped.

### 5. Real `dn` integration and parity

Use `run-dn.ps1`; do not duplicate its publish/copy logic:

```powershell
.\src\Cli\dn\run-dn.ps1 -Command "<supported invocation>" -Mode Compare
.\src\Cli\dn\run-dn.ps1 -Command "<unsupported invocation>" -Mode Aot -NoBuild
```

The script publishes `dotnet-aot` and `dn`, builds/copies managed `dotnet.dll`, sets `DOTNET_ROOT`, and
toggles `DOTNET_CLI_ENABLEAOT`. `Compare` writes `artifacts/log/dn-aot.txt` and `dn-managed.txt` and diffs
captured output. Separately compare exit code, files/cache, restore/build count, and child processes when
the command can affect them. Set `DOTNET_AOT_TEST_DN_PATH` for focused integration tests against the
assembled harness.

### 6. Separated SDK-root layout

```powershell
.\src\Cli\dn\run-dn.ps1 -Command "<invocation>" -Mode Compare -Layout Separated
.\src\Cli\dn\run-dn.ps1 -Command "<invocation>" -Mode Compare -Layout Separated -SelfLocate
```

The first exercises the host-provided versioned SDK directory. The second makes `dn` pass an empty
`sdk_dir` so `dotnet-aot` must self-locate its native module. Use this for every new SDK-relative path.

### 7. Size and platform evidence

Record the before/after native library byte size and inspect the dependency graph for meaningful changes.
Use the AOT size-analysis workflow when available. Run native publish, native tests, and relevant `dn`
scenarios on each affected supported OS/architecture; cross-OS publish is not an execution result.

## Completion report

Report:

- Supported and deliberately unsupported shapes.
- Eligibility/fallback signal and first commit point.
- Shared source/resources and dependency roots changed.
- Focused, managed, clean-build, native-publish, native-test, `dn`, separated-layout, and platform checks
  actually run, including skipped counts.
- Managed/AOT differences in output, exit code, side effects, and work performed.
- Native binary size delta, major contributors, warning/feature-switch treatment, and remaining risk.

## Related skills

- **code-review** - findings-first review presentation for PR or local changes.
- **dotnet-aot-compat** - resolve IL trim/AOT warnings surfaced by native publish.
- **incremental-test** - run managed `dotnet.Tests` against the redist SDK layout.
- [**run-tests**](../run-tests/SKILL.md) - select and run SDK tests through the diagnostic-preserving local entry point.
