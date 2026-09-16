# `dotnet test` for browser WebAssembly

## Status

Proposed. This is a discussion draft for
[dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091), informed by a
working cross-repository proof of concept. It is not a package availability
announcement, supported preview, or commitment to the experimental property
names and implementation details below.

**The current SDK experiment changes no production SDK code.** Its four files
live under `poc/browser-wasm-unit-tests`. The README's direct PowerShell recipe
explicitly supplies `DotnetTestInvocation=true`; this is an experimental
package activation marker, not a reserved product SDK property.

## Decision summary

Browser-hosted Microsoft.Testing.Platform (MTP) support should be an optional
TestFX package, not another browser stack in the .NET SDK.

- The application is a `Microsoft.NET.Sdk.WebAssembly` executable containing
  C# tests and package references. MTP generates its managed entry point.
  No Blazor, Razor, `IJSRuntime`, `[JSImport]`, or application-authored HTML or
  JavaScript is needed for launch, discovery, or results.
- `Microsoft.Testing.Platform.Browser` owns test-specific build integration,
  default host assets, the Playwright launcher, diagnostics, and cleanup.
  It wraps the project's evaluated host instead of supplying another server.
- The WebAssembly SDK's `WasmAppHost` builds on the existing asset pipeline
  and serves the application. It remains test-agnostic.
- Managed MTP uses C# `HttpClient` to communicate directly with the SDK's
  authenticated HTTP test gateway. Neither the application host, Playwright,
  nor package JavaScript interprets or relays discovery or results.
- Minimal package-owned JavaScript boots the browser runtime, passes
  argv-equivalent arguments, awaits managed Main, and reports its terminal
  exit code or a bootstrap failure to the external launcher.

The experiment deliberately supports a narrow host/browser combination.
Framework-owned pages, including Blazor composition, are future scenarios,
not an implemented public API or a prerequisite for this model.

## Primary-source evidence

The current snapshots are:

- **SDK:** `f0ed2c30ac480554b22964997ac6eac0ad9ae845`.
  The experiment consists only of `BrowserWasmTestApp.csproj`,
  `BrowserWasmTests.cs`, `Directory.Packages.props`, and `README.md` under
  `poc/browser-wasm-unit-tests`: four files and 120 added lines.
- **TestFX:** `e6d8e4e0ccbdacb56db71313fcff328c73182b08`, following
  `d0eefc044`. The final fixtures use only `EnableMSTestRunner=true` to
  enable the runner and assert `IsTestingPlatformApplication=true`.
  Build-only host assets are attributed to the consuming project.

TestFX source is identified by commit and path because the cited experiment
is local/unpublished. SDK experiment paths are also snapshot citations, not
files added by this documentation PR. In the following table, package paths
are relative to `src/Platform/Microsoft.Testing.Platform.Browser/` in TestFX
at the commit above.

| Behavior | Inspected primary source |
| --- | --- |
| Pure-managed consumer, package versions and exact SDK invocation | SDK `poc/browser-wasm-unit-tests/BrowserWasmTestApp.csproj`, `BrowserWasmTests.cs`, `Directory.Packages.props`, `README.md` |
| Explicit experimental activation | SDK `src/Cli/dotnet/Commands/Test/MTP/SolutionAndProjectUtility.cs`; the invocation marker is supplied by the PoC README recipe instead |
| Optional package, private Playwright dependency and bundled driver layout | TestFX package `Microsoft.Testing.Platform.Browser.csproj` |
| Evaluated host wrapping and Static Web Assets registration | Package `buildMultiTargeting/Microsoft.Testing.Platform.Browser.props` and `Microsoft.Testing.Platform.Browser.targets` |
| URI-encoded launch options, SDK response-file expansion and bootstrap validation | Package `BrowserLauncherOptions.cs` |
| Host command execution, HTTP stdout readiness and host cleanup | Package `HostProcess.cs` |
| Private page bindings, terminal result, browser diagnostics and cleanup | Package `ChromiumBrowser.cs`, `BrowserRunMonitor.cs`, `Program.cs`, `DiagnosticBuffer.cs` |
| Page and dynamic runtime boot module | Package `buildMultiTargeting/assets/index.html` and `Microsoft.Testing.Platform.Browser.main.js` |
| Managed HTTP protocol | TestFX `src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/DotnetTestConnection.cs` and `Transport/DotnetTestHttpClient.cs` |
| Package acceptance coverage | TestFX `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserPackageExecutionTests.cs` |
| Parser, readiness, redaction and wait-cancellation coverage | TestFX `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/BrowserLauncherOptionsTests.cs` |

### Reported validation

For these simplified snapshots, the implementation owner reports:

| Validation | Result |
| --- | --- |
| TestFX browser package acceptance tests | 6/6 passed |
| SDK PoC execution | 1 passed |
| SDK PoC discovery | 1 discovered |

These are reported local implementation results, not tests rerun by this
documentation-only PR or a cross-platform support guarantee. The TestFX
acceptance fixture has three tests, including an intentional failure and an
ignored test; the SDK PoC has one passing browser assertion and no ignored
test. Their discovery counts are not interchangeable. Earlier validation
counts are not evidence for this final snapshot.

## Existing product foundation

The PoC consumes the following existing functionality without changing it:

- The SDK evaluates
  [`ComputeRunArguments`](../../src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets)
  and executes `RunCommand`, `RunArguments`, and `RunWorkingDirectory`.
- [`TestApplication`](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs)
  selects HTTP transport for `browser-*` and `wasi-*` modules, starts the
  gateway, and writes bootstrap arguments to a protected response file.
- [`HttpTestHostGateway`](../../src/Cli/dotnet/Commands/Test/MTP/IPC/HttpTestHostGateway.cs)
  implements authenticated binary request/reply, CORS origin pinning and
  Private Network Access preflight handling.
- The WebAssembly SDK
  [`props`](../../src/WasmSdk/Sdk/Sdk.props) and
  [`targets`](../../src/WasmSdk/Sdk/Sdk.targets) compose with Static Web
  Assets and import the WebAssembly build logic. The default RID is
  `browser-wasm`.

The MTP-side HTTP implementation is in the TestFX sources cited above.
Runtime networking may internally use browser infrastructure; C# owns the
MTP protocol, serializers, authentication header and request/reply lifecycle.
No app-authored interop or JavaScript protocol adapter is involved.

## Project shape and reproduction

The canonical application contains a project and one C# test. This excerpt
matches the SDK PoC's meaningful build settings, including the explicit
native-build and trimming exclusions:

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <TargetFramework>$(SdkTargetFramework)</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest" />
    <PackageReference Include="Microsoft.Testing.Platform.Browser" />
  </ItemGroup>
</Project>
```

Package versions are separate from project references, in the local
`Directory.Packages.props`:

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="MSTest" Version="$(MSTestVersion)" />
    <PackageVersion Include="Microsoft.Testing.Platform.Browser"
                    Version="$(BrowserWasmMtpPackageVersion)" />
  </ItemGroup>
</Project>
```

This is a repository-context sample: `SdkTargetFramework`, `MSTestVersion`,
and central package management come from repository configuration;
`BrowserWasmMtpPackageVersion` is passed by the README recipe. A standalone project
must select compatible framework/package versions and its package-management
configuration explicitly. The example is not evidence for trimmed or
native-relinked builds.

The only PoC test, `RunsInsideBrowserWasm`, asserts
`OperatingSystem.IsBrowser()`. It provides no custom Main, HTML, JavaScript,
or application-to-launcher bridge. Current TestFX acceptance explicitly
checks that `EnableMSTestRunner=true` results in
`IsTestingPlatformApplication=true` for both browser and desktop fixtures;
no additional runner-enablement property is needed.

### Run the inspected experiment

Use an SDK checkout containing the cited PoC commit and a complete Debug
redist SDK, plus a locally packed TestFX browser package from the cited
snapshot. Select an installed Chromium-family browser explicitly. Run the
[direct PowerShell recipe in the pinned PoC README](https://github.com/dotnet/sdk/blob/f0ed2c30ac480554b22964997ac6eac0ad9ae845/poc/browser-wasm-unit-tests/README.md)
from the SDK checkout root, replacing its package-source and browser paths
and package version with the actual local values.

The recipe:

1. Resolves the project and built Debug redist executable and requires exactly
   one SDK directory in that redist.
2. Creates temporary run state outside the repository's `global.json`, with
   a `global.json` pinned to that SDK version/root, `rollForward=disable`,
   and the `Microsoft.Testing.Platform` test runner.
3. Sets `NUGET_PACKAGES` to the temporary package directory and isolates
   project extensions, intermediate output and build output under the run
   directory. Setting only a restore path is insufficient: `NuGetPackageRoot`
   can otherwise resolve a stale package from the shared cache.
4. Sets `DOTNET_CLI_USE_MSBUILD_SERVER=0` so test evaluation observes package
   imports generated by the preceding restore.
5. Supplies `DotnetTestInvocation=true`, the local package version/source and
   `TestingPlatformBrowserExecutable`, then explicitly restores before test.

With the variables and property array established by that recipe, the core
commands are:

```powershell
& $dotnet restore $project @properties
& $dotnet test --project $project --no-restore @properties
```

Check that restore succeeded before interpreting the test result. Add
`--list-tests` to the test command for discovery. The explicit restore makes
the experimental package imports available before `dotnet test` evaluates
`ComputeRunArguments`; `--no-restore` applies only to that subsequent command.

The README's `finally` block restores the previous environment values and
working directory and removes temporary run state. It does not mutate the
redist SDK or add a process supervisor. The package does not acquire the
browser. This direct invocation recipe is reproduction guidance, not a
product launch or file-option policy.

## Current experimental contracts

### Invocation marker, not a reserved SDK contract

The package's `_ConfigureTestingPlatformBrowserRun` target runs after
`ComputeRunArguments` when the package is enabled, the RID starts with
`browser-`, and `DotnetTestInvocation` equals `true`.

The PoC explicitly passes `-p:DotnetTestInvocation=true`. That is the only
invocation marker supplied by the recipe and consumed by the package.
It is not a reserved product SDK global or a negotiated product capability.

The package's `PACKAGE.md` shorthand about the SDK setting the marker must
be read in this PoC context: it is supplied explicitly by the README recipe.
Bare product `dotnet test` does not acquire browser wrapping solely from
installing this package. A future supported activation mechanism needs an
explicit SDK/TestFX decision; the PoC does not commit that design.

Unmarked `ComputeRunArguments` queries and desktop projects are unwrapped.
This is covered by current acceptance tests. It does not imply a supported
ordinary-run browser testing experience. Without the private launcher
bindings the package boot module uses an empty argument array and can start
Main, but no SDK HTTP bootstrap or result connection is supplied that way.

### Evaluated-host handoff

The package captures the evaluated `RunCommand`, `RunArguments`, and
`RunWorkingDirectory`, falling back to the project directory for an empty
working directory. It replaces the outer command with `dotnet exec` of the
package launcher. The launcher receives one private option representation:

| Launcher option | Value |
| --- | --- |
| `--host-command-uri` | URI-encoded original command |
| `--host-arguments-uri` | URI-encoded original argument string |
| `--host-working-directory-uri` | URI-encoded original working directory |
| `--browser-executable-uri` | URI-encoded explicit browser executable |
| `--startup-timeout-seconds` | Positive startup timeout |
| `--completion-timeout-seconds` | Positive completion timeout |

`--` separates these launcher options from MTP application arguments,
including the SDK bootstrap response-file reference. URI encoding preserves
the handoff; it is not encryption and must not be used to conceal secrets in
the command line.

`HostProcess` supplies the decoded host string directly to
`ProcessStartInfo.Arguments` with `UseShellExecute=false`. There is no custom
host tokenizer, shell command composition, alternate config-file/base64
format, host override, or invocation-config Clean target. The package uses
`AfterTargets="ComputeRunArguments"`, not `CustomAfterDirectoryBuildTargets`.
A project target that also runs after `ComputeRunArguments` can subsequently
replace the wrapper; that composition is explicitly unsupported by the PoC.
TestFX `PACKAGE.md` proposes SDK-owned launcher selection for a preview,
rather than additional package target-ordering machinery. That is future
product design, not a current SDK implementation.

### Host assets are Static Web Assets

When enabled and `WasmMainJSPath` is unset, the package selects its boot module
and, if unset, its HTML page. It copies both into an intermediate directory,
registers them with `DefineStaticWebAssets` and
`DefineStaticWebAssetEndpoints`, and records the copied assets in
`FileWrites`. `SourceId="$(PackageId)"` attributes these computed assets to
the consuming project, not to the browser package's identity.

That registration is necessary for the tested WasmAppHost serving path:
setting `WasmMainJSPath`/`WasmMainHTMLPath` alone is not enough to expose the
page and module and resulted in 404s during the experiment. Do not remove
the Static Web Assets integration as redundant property wiring.

The current assets are registered with `AssetKind="Build"`. Acceptance checks
their consumer identity in the build manifest and their absence from the
publish manifest and published output. This intentionally excludes the test
host assets from publication; it is not published-layout browser test support.
If a project has already selected a
`WasmMainJSPath`, the package does not take ownership of its page; no public
framework-page adapter or generated-assets opt-out contract is supplied.

### Host readiness and browser selection

The launcher recognizes only the tested WasmAppHost stdout form:

```text
App url: http://127.0.0.1:<port>/
```

The parser permits whitespace/case variation but requires an HTTP loopback
URL. It ignores HTTPS `App url:` lines, `Debug at url:` and
`Now listening on:`. Stderr is diagnostic output, not a readiness channel.
Early host exit, non-loopback readiness and readiness timeout are failures.
The browser navigates to the reported URL; no configurable URL-path override
is supported.

There is no launch-info file, environment variable, versioned JSON readiness
schema, readiness-directory DACL, or generic server fallback. WasmAppHost
has no new test-specific route or responsibility. Generic readiness is a
possible future host contract, not an implemented Gateway feature.

`TestingPlatformBrowserExecutable` must name an existing installed
Chromium-family executable. The package does not search for browsers,
download them, fall back from an invalid configured path, or expose arbitrary
browser arguments. It launches headless Chromium through Playwright's
private transport and creates a fresh isolated context/page.

### Private boot and terminal-result bindings

Launcher and boot module ship together. Their bindings are internal package
details, not a public/versioned framework API:

| Private binding | Responsibility |
| --- | --- |
| `__mtpBrowserGetArguments` | Return the prepared MTP argument array |
| `__mtpBrowserComplete` | Receive `{ exitCode }` or `{ exitCode, error }` |

Calls are checked against the expected page, top-level frame, and exact host
origin. Navigation outside that origin is rejected. The launcher validates
the terminal result's integer exit code and optional string error; the first
accepted terminal result wins. It does not promise that a later duplicate
can retroactively fail an already-completed run.

The package module obtains arguments, dynamically imports
`./_framework/dotnet.js`, calls `withApplicationArguments(...args).create()`,
awaits `runMain()`, and awaits terminal-result delivery. Dynamic import is
inside its error-handling path so runtime import/startup failures can be
reported through the same terminal channel.

There is no `globalThis.testingPlatformBrowser` API, contract-version
negotiation, `reportFatalError` framework extension, or app-authored bridge.
Managed MTP owns draining its protocol before Main returns; JavaScript only
awaits Main. It cannot acknowledge individual results or artifacts.

## Protocol ownership and security boundaries

The flow has two distinct channels:

```text
SDK -- evaluated run command + protected HTTP bootstrap --> package launcher
package launcher -- starts --> WasmAppHost -- serves --> browser runtime
package launcher <-- private arguments / terminal result --> package boot JS
managed browser MTP <-- authenticated binary HTTP request/reply --> SDK gateway
```

Discovery, filtering, progress and results use managed MTP's existing
serializers and C# `HttpClient`. Playwright's terminal-result binding provides
process-style completion, not a second test protocol. The SDK remains the
consumer of MTP results; the launcher need not know whether a managed exit
code represents failed tests or another managed application failure.

**Pure-managed does not mean JavaScript-free browser infrastructure.** The
application needs only C# tests and package references. Package-owned JS is
still needed to import the runtime, instantiate it, pass argv, invoke and
await Main, and signal its outcome. External supervision is necessary
because a crashed or hung page cannot supervise itself. Tests/extensions
that intentionally exercise browser APIs may use interop, but `[JSImport]`
is not required for launch or results.

The existing SDK creates the bootstrap response file with a current-user
Windows ACL or Unix user-read/write mode, using create-new semantics. The
launcher expands the SDK's line-oriented file format and validates a
loopback authenticated HTTP `dotnettestcli` bootstrap before starting the
host. This response file is distinct from the removed launcher configuration
and readiness files.

The package response-file reader checks Unix group/other permission bits and
opens with exclusive sharing; on Windows it does not independently validate
the file ACL. Do not turn those checks into a claim of general hostile-file,
ownership, symlink, replacement-race or bounded-input hardening.

The bearer token stays out of URLs and static assets. The package redacts
bootstrap values in bounded diagnostics and disables Playwright's `DEBUG`
channel logging, which could otherwise include binding payloads. It uses no
unauthenticated DevTools TCP endpoint. Existing gateway CORS/PNA behavior is
unchanged; there is no added origin-registration API or dynamic CSP protocol.

For any preview, preserve authenticated loopback transport, private
bootstrap handling, exact-origin/top-level binding checks, isolated browser
state and cleanup restricted to owned resources. These are real boundaries;
an invocation marker is not authentication or a sandbox for untrusted tests.
Review error paths and platform-specific enforcement before shipment rather
than treating a successful local run as a security certification.

## Failure, cancellation and artifacts

`TestingPlatformBrowserStartupTimeoutSeconds` defaults to 60 seconds;
`TestingPlatformBrowserCompletionTimeoutSeconds` defaults to 600 seconds.
Readiness and browser startup are separate waits, not one combined wall-clock
deadline. The launcher monitors terminal completion, host exit, browser
disconnect and page crash. Cancellation delivered to the launcher interrupts
its completion wait and enters cleanup.

The simplified implementation retains bounded asynchronous cleanup:
binding disposal, browser/context close, host-exit confirmation and host
output-reader completion each have five-second limits. It attempts to kill
the owned host process tree and records cleanup failures in diagnostics.
These are per-operation bounds, not a five-second total cleanup guarantee.
`Playwright.CreateAsync()` and synchronous `Playwright.Dispose()` still have
no explicit deadline at their call sites. End-to-end force-termination and
real cancellation behavior need validation before a supported preview.

Graceful SDK-to-managed-MTP cancellation is not implemented. Forceful
cleanup cannot guarantee managed `finally` execution or artifact flushing.
Normal console cancellation and arbitrary process signals must not be
presented as identical guarantees; the reproduction recipe adds no
independent process-tree manager.

Browser VFS files are not physical host artifacts. There is no artifact
export or host-input transfer. Unlike the earlier experiment, **the current
package has no MTP option denylist**: report, configuration, diagnostics,
results-path and coverage options are not proactively rejected by this
launcher. Files may remain in the browser VFS, and host paths may be
unusable. Live results are not proof of report persistence.

A preview needs a clearly bounded supported option surface and honest
diagnostics for unsupported host-file behavior. Whether to add rejection,
mapping or transfer belongs to a future design; the earlier eighteen-option
rejection table is not the implementation.

## Packaging and current coverage

The optional package privately depends on Playwright and packs the launcher,
managed dependencies and five Node platform payloads: Windows x64, Linux
x64/arm64 and macOS x64/arm64. It does not include browser binaries. Its
launcher targets `net8.0` with major roll-forward; that packaging choice is
not the test application's framework support matrix.

Earlier local packages were approximately 200 MB. That is historical
prototype sizing, not a measurement of this exact final package or a promised
product size. The payload is not added to the SDK layout. Package
distribution, source-build, signing, offline use, servicing and actual
supported platform/browser versions are unresolved.

The current maintained coverage includes:

| Area | Current assertions |
| --- | --- |
| Pure-managed package execution | No app HTML/JS/wwwroot; pass, intentional failure, skip, filter and discovery through the SDK HTTP gateway |
| Nonactivation | Unmarked browser `ComputeRunArguments` and desktop execution remain unwrapped |
| Runner activation | Browser and desktop fixtures evaluate `IsTestingPlatformApplication=true` with only `EnableMSTestRunner=true` |
| Asset ownership and publication | Consumer `SourceId`, build-only page/module assets, and exclusion from publish manifest/output |
| Package layout | Private Playwright/Node payload, dynamic-import boot module/private bindings, no removed `.After.targets` |
| Argument handling | URI round-trip, newlines/special characters, SDK bootstrap response-file expansion, invalid bootstrap rejection |
| Readiness and diagnostics | Exact HTTP `App url:` parsing, rejection of non-loopback URL, diagnostic redaction and output bounds |
| Cancellation | Linked run-monitor cancellation stops its wait promptly; this unit case is not an end-to-end process cleanup test |

The reported counts are in [Reported validation](#reported-validation).
Custom host composition, option denylist and framework API tests are not
current coverage. Broader concurrency, late-target composition, `--no-build`,
published-layout execution, trimming/native modes and non-Windows execution must be
validated or explicitly excluded from a supported preview.

## Minimum experiment and credible preview

The minimum experiment is the current narrow path: pure C# application,
generated MTP Main, package assets, evaluated WasmAppHost, explicitly selected
Chromium, private runtime boot/completion and existing authenticated HTTP.
The SDK change is only a sample, local package-version file and reproduction
documentation.

A credible preview would additionally require:

1. A supported invocation/activation decision between SDK and TestFX, without
   assuming the removed reserved properties already exist.
2. A published, reviewable package and reproducible SDK/workload/browser
   prerequisites with an explicitly tested platform matrix.
3. Reliable terminal-state, timeout, crash and cancellation behavior,
   including real owned-process cleanup evidence and remaining deadline gaps.
4. A declared host-file/report policy, bootstrap/security review, and coverage
   for the build/run modes actually advertised.

It need not first introduce a generic host readiness file, public page API,
artifact protocol, browser acquisition manager or Blazor adapter.
WASI, VSTest, `--test-modules` without project evaluation, dynamic assembly
loading, browser process reuse, managed browser debugging and UI/E2E testing
are outside this experiment.

## Open design questions

- **Activation:** should the SDK expose a supported test-invocation signal,
  or should another existing extension mechanism select the launcher? The
  explicit PoC property is not the answer by default.
- **Host compatibility:** is a narrow, version-pinned WasmAppHost stdout
  contract sufficient for preview? A generic readiness mechanism should be
  owned by the host ecosystem only if broader consumers justify it.
- **Distribution:** which SDK/TFM/browser/OS combinations ship, and how are
  private Playwright payload size, offline use and servicing handled?
- **File semantics and cancellation:** which unsupported features should
  fail explicitly before launch, and when are artifact/input transfer and
  graceful managed cancellation worth a separate contract?
- **Framework composition:** if demanded later, how can a framework own its
  page without duplicating runtime startup? No public adapter is implemented
  or reserved by the private bindings in this PoC.

Related host discussions include
[dotnet/runtime#122144](https://github.com/dotnet/runtime/issues/122144) and
[dotnet/aspnetcore#67814](https://github.com/dotnet/aspnetcore/issues/67814).
They are context for future convergence, not prerequisites that the current
PoC implements.

## Alternatives not needed for this experiment

- SDK-owned Playwright or browser boot assets would put browser-specific
  dependencies and servicing in the wrong component.
- A new test-aware WebAssembly server would duplicate the evaluated host and
  Static Web Assets pipeline.
- A JavaScript results relay or extra WebSocket protocol would duplicate
  managed MTP's existing authenticated HTTP transport.
- A public framework API, general launcher configuration language, host
  overrides or automatic browser discovery would expand compatibility
  obligations without improving the canonical C# experiment.
- Requiring Blazor would conflate general managed browser testing with
  framework UI/component testing.

The proposed direction is therefore to keep managed MTP and the existing
host boundaries, and productize only the smallest package/SDK interaction
that a supported browser-testing scenario actually needs.
