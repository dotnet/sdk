---
name: add-dotnet-aot-command
description: >
  Include a dotnet CLI command or feature in the Native AOT CLI (src/Cli/dotnet-aot)
  and prove it works. USE FOR: enabling a command/option in dotnet-aot, adding source
  files to AotSourceFiles.props, gating AOT-incompatible code with #if CLI_AOT, building
  and NativeAOT-publishing dotnet-aot, writing/updating the AOT parser + integration
  tests, running the local dn harness in AOT mode and comparing it to the managed CLI.
  DO NOT USE FOR: resolving IL trim/AOT analyzer warnings (use dotnet-aot-compat),
  running dotnet.Tests incrementally (use incremental-test), or pure managed CLI work.
license: MIT
---

# add-dotnet-aot-command

How to include a `dotnet` command or feature in the Native AOT CLI (`src/Cli/dotnet-aot`), keep the
AOT surface small, validate it, and run it through the local `dn` harness.

> Paths use `$(SdkTargetFramework)` = `net11.0` and `win-x64`; adjust for other TFMs/RIDs.

## When to use

- Make `dotnet <x>` work in dotnet-aot, or enable an option/section in the AOT path.
- Add source files to `AotSourceFiles.props`.
- Run `dn` in AOT mode, or compare AOT vs managed output.
- Diagnose why `dotnet test` fails for `dotnet-aot.Tests`.

**Not for:** IL trim/AOT warnings (use **dotnet-aot-compat**); managed `dotnet.Tests` runs (use
**incremental-test**); managed-only changes with no AOT impact.

## How the AOT CLI is assembled

`dotnet-aot` does **not** reference `dotnet.csproj`. It is a shared native library (`NativeLib=Shared`,
`PublishAot=true`, `IsAotCompatible=true`) that **cherry-picks source files** from `src/Cli/dotnet/` via
`src/Cli/dotnet-aot/AotSourceFiles.props`. That `.props` is imported by **both** `dotnet-aot.csproj` and
`test/dotnet-aot.Tests/dotnet-aot.Tests.csproj`, so the tests compile the exact same command surface as
the shipping binary.

Compile constants (both projects): `CLI_AOT` gates AOT-only vs managed-only code in shared files (`#if
CLI_AOT` / `#if !CLI_AOT`); `DotnetCsproj` is also defined and can pull in extra closure (see Gotchas).

Dispatch: `dotnet-aot/NativeEntryPoint.cs` (`dotnet_execute`) is P/Invoked by the `dn` host (`src/Cli/dn`):

- `DOTNET_CLI_ENABLEAOT=true`: parse in-process, run `FirstRunExperience.Setup`, and if
  `parseResult.CanBeInvoked()` run the command **in-process**. A command still needing the managed CLI
  throws `CommandNotAvailableInAotException` to fall through.
- Otherwise / on fall-through: host `{sdkDir}/dotnet.dll` via hostfxr (same source, JIT-compiled).

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

- **In-repo:** read `SdkPaths.SdkDirectory` (in `Microsoft.DotNet.Cli.Utils`), which resolves the
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
"the SDK directory" to the above. Not-yet-routed sites: `FormatForwardingApp`, `FsiForwardingApp`,
`VSTestForwardingApp`, `ProjectFactory` / `ProjectToolsCommandResolver`, `VBCSCompilerServer`,
`CSharpCompilerCommand`, `MSBuildForwardingAppWithoutLogging`, `DotnetFiles.SdkRootFolder`. Details:
`src/Cli/dotnet-aot/SdkRootResolution.md`.

## Procedure

1. **Find the call site** in shared source (a command parser, `Parser.cs`, `ParserOptionActions.cs`) and
   remove/narrow its `#if !CLI_AOT` guard.
2. **Add the source closure** to `AotSourceFiles.props` in a labeled per-command `<ItemGroup>` (follow the
   file's header rules; reuse the "Common AOT scaffolding" group). Only add files under `src/Cli/dotnet/`
   that aren't already in a referenced assembly or the `.props`. Windows/COM files go in a
   `Condition="'$(TargetOS)' == 'windows'"` group.
3. **Add package references** the command needs (in `AotSourceFiles.props` if both binary and tests need
   them; confirm the runtime asset flows - see the `Microsoft.Build` gotcha).
4. **Build managed dotnet-aot first** - fast, and surfaces `CS0246`/`CS0103` closure gaps without ILC. Let
   the compiler drive the closure: `.\.dotnet\dotnet build src\Cli\dotnet-aot\dotnet-aot.csproj -c Debug`
5. **Publish as NativeAOT** to surface IL warnings (resolve per **dotnet-aot-compat**):
   `.\.dotnet\dotnet publish src\Cli\dotnet-aot\dotnet-aot.csproj -r win-x64 -c Debug`. ILC only analyzes
   the reachable closure - don't preemptively suppress warnings that never appear.
6. **Keep the AOT surface small.** Gate heavy subsystems (workload installer, NuGet engine, MSI/COM IPC)
   under `#if CLI_AOT` and build only the read-only path you need (mirror `WorkloadInstallDetector`, which
   builds the record repository directly with no installer). Gate installer-coupled interfaces under
   `#if !CLI_AOT`, with an AOT-only construction path under `#if CLI_AOT`.
7. **Confirm the managed CLI still builds** (the `#else` branches must stay intact):
   `.\.dotnet\dotnet build src\Cli\dotnet\dotnet.csproj -c Debug`

## Gotchas

- **MSBuild XML comments can't contain `--`** (`MSB4024`). Reword; never end a comment with `-`.
- **The `Microsoft.Build` runtime asset doesn't flow transitively** - `Cli.Utils` references it
  `ExcludeAssets="runtime" PrivateAssets="all"`, so dotnet-aot has no `Microsoft.Build.dll` at ILC time.
  If you reach a `Microsoft.Build.*` API, add `<PackageReference Include="Microsoft.Build" />` to the AOT
  closure.
- **`DotnetCsproj` is defined for dotnet-aot**, so adding a shared file can pull in extra `#if DotnetCsproj`
  closure. Inline the small helper you need under `#if CLI_AOT` instead.
- **Don't pass `-noRestore` with `-getItem`** - the response file already appends it (`MSB1001`).
- **`dotnet test` does NOT work for `dotnet-aot.Tests`** (Microsoft.Testing.Platform, not VSTest). Run the
  built `.exe` directly (see below).
- **Existing tests may assert AOT _exclusions_** - enabling a feature can mean **inverting** a
  `DoesNotContain` assertion. Search the test project first.
- **PowerShell git/gh quoting:** single-quote messages/titles containing backticks or `$(...)`.

## Validate & test

Tests live in `test/dotnet-aot.Tests`: `AotParserTests` (in-process parser/command behavior) and
`AotIntegrationTests` (end-to-end against the real `dn`; skips if `dn` isn't in the layout).

Run the suite as a native AOT binary (the real ILC / COM / P-Invoke check) with
`test/dotnet-aot.Tests/run-aot-tests.ps1`. To iterate on one test, build the test project and run
`dotnet-aot.Tests.exe --filter "FullyQualifiedName~<name>"` (MTP runs as an executable; `dotnet test`
doesn't work). `IL3053` rollups for test-only assemblies (FluentAssertions, TestPlatform.ObjectModel,
DataContractSerialization) are not product warnings.

Assert real **values**, not just headers, so a trim regression that blanks a line is caught - e.g.
`stdout.Should().MatchRegex(@"MSBuild version:\s+\S");`.

## Run the local `dn` harness in AOT mode

Use `src/Cli/dn/run-dn.ps1` (don't inline the steps). It publishes `dotnet-aot` (NativeAOT) and `dn`,
builds the managed `dotnet` CLI, assembles them into the `dn` publish dir, points `DOTNET_ROOT` at the
repo's `.dotnet`, and runs `dn <command>` with `DOTNET_CLI_ENABLEAOT` toggled. **Tell the user these
steps** so they can reproduce it.

```powershell
src\Cli\dn\run-dn.ps1 -Command "--info"                    # through the AOT binary
src\Cli\dn\run-dn.ps1 -Command "--info" -Mode Compare      # AOT vs managed diff (parity)
src\Cli\dn\run-dn.ps1 -Command "workload --info" -NoBuild  # reuse the assembled layout
```

- `DOTNET_CLI_ENABLEAOT=true` runs in-process in `dotnet-aot.dll`; unset, `dn` hosts the copied
  `dotnet.dll`. `-Mode Compare` diffs the captured output (`artifacts/log/dn-aot.txt`, `dn-managed.txt`).
- `dn` finds the .NET root from `DOTNET_ROOT` (set to `.dotnet`); the publish dir isn't a full SDK.
- The AOT path runs `FirstRunExperience.Setup` first; if it can't complete, it defers to the managed CLI.
- `Commit` and workloads reflect the `DOTNET_ROOT` layout - both paths read the same root, so they agree.

The VS Code tasks `publish-and-copy-dn-aot` + `copy-all-deps` do the same build/assemble.

## Related skills

- **dotnet-aot-compat** - resolve the IL trim/AOT warnings this surfaces.
- **incremental-test** - run the managed `dotnet.Tests` against the redist SDK layout.
