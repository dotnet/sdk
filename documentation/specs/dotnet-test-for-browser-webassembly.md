# `dotnet test` for browser WebAssembly

## Status

Proposed.

This document is a discussion draft for
[dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091). Names of new
properties, options, packages, and protocol fields are placeholders until the
owning teams agree on them.

## Scope and decision status

The testing foundation belongs to the general
`Microsoft.NET.Sdk.WebAssembly` SDK and is limited to the `browser-wasm`
runtime identifier. Blazor, Uno, Avalonia, and other browser UI frameworks can
build thin integrations on that common layer.

The following constraints are established:

- The Blazor Gateway is a production component, comparable to YARP. It remains
  unaware of MTP, browser automation, test completion, and artifact collection.
  See the
  [Blazor team feedback](https://github.com/dotnet/sdk/issues/54091#issuecomment-5395458076).
- Browser-hosted Microsoft.Testing.Platform (MTP) uses the existing
  authenticated HTTP `dotnettestcli` transport for live test messages.
- Per-run credentials do not belong in URLs, ordinary process arguments,
  static assets, launch profiles, or logs.
- The browser must be supervised from outside the WebAssembly runtime so
  crashes and synchronous hangs can still be detected and force-terminated.

The proposed first implementation uses Playwright and supports headless
Chrome and Edge. Playwright is a browser-control layer; it does not replace
the MTP result protocol, graceful managed cancellation, or browser virtual
filesystem (VFS) artifact export.

WASI is a separate host model and is out of scope. The current SDK happens to
select HTTP for `wasi-*` runtime identifiers, but this design does not define
WASI build, launch, or result behavior.

## Summary

`dotnet test` should be able to:

1. Build a `browser-wasm` MTP test application.
2. Generate and serve a versioned test page and JavaScript supervisor.
3. Launch an isolated browser through Playwright.
4. Run MTP inside the browser and stream discovery, output, and per-test
   results to the SDK over the existing authenticated HTTP transport.
5. Detect managed runtime failures through Playwright even when MTP can no
   longer report.
6. Export physical TRX files and attachments from the browser VFS.
7. Cancel cooperatively when possible and force-close the browser when the
   single WebAssembly thread cannot respond.
8. Clean up every process, profile, endpoint, response file, and partial
   artifact created for the run.

Phase 1 targets non-Blazor `Microsoft.NET.Sdk.WebAssembly` test applications.
They cover business logic, networking, runtime JavaScript interop, and other
tests that need the real browser runtime but not a UI framework.

Blazor component testing is a later layer. A Blazor application has a
long-lived startup sequence and renderer, so it cannot use the same
`Program.Main` completion model without an explicit renderer-ready and
completion contract.

## Existing foundation

- MTP and MSTest execute on single-threaded `browser-wasm`. The
  [BrowserPlayground sample](https://github.com/microsoft/testfx/tree/main/samples/BrowserPlayground)
  demonstrates browser and Node hosting.
- MTP supports the versioned `dotnettestcli` binary protocol over
  authenticated HTTP.
  See [microsoft/testfx#10143](https://github.com/microsoft/testfx/pull/10143).
- TRX generation works in the browser VFS on single-threaded WebAssembly.
  See [microsoft/testfx#10324](https://github.com/microsoft/testfx/pull/10324).
- The SDK hosts an authenticated loopback HTTP endpoint and supplies its
  endpoint and token through an owner-only response file.
  See [dotnet/sdk#55672](https://github.com/dotnet/sdk/pull/55672) and
  [`TestApplication.cs`](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs).
- `Microsoft.NET.Sdk.BlazorWebAssembly` imports
  `Microsoft.NET.Sdk.WebAssembly`; the general layer is therefore available to
  Blazor without making other frameworks depend on Blazor.
- `dotnet watch` hot reload already ships SDK-owned managed and JavaScript
  assets into browser WebAssembly applications. Its fixed per-TFM agent is
  prior art for the versioning boundary in this proposal. See
  [`TargetFrameworks.props`](../../src/WasmSdk/Sdk/TargetFrameworks.props) and
  [`Sdk.targets`](../../src/WasmSdk/Sdk/Sdk.targets).
- The current Blazor probe demonstrates MTP inside a rendered component but
  also documents the missing host lifecycle.
  See [`BlazorWasmTestApp`](../../test/TestAssets/TestProjects/BlazorWasmTestApp/).

## Goals

- Support `dotnet test`, `--list-tests`, extension help, filtering, retry,
  live output, and per-test results in `browser-wasm`.
- Keep framework-neutral work in `Microsoft.NET.Sdk.WebAssembly`.
- Use Playwright for browser launch, contexts, navigation, JavaScript
  bindings, console forwarding, page errors, crash detection, and cleanup.
- Run headlessly by default while allowing an explicit headed debugging mode.
- Use installed Chrome or Edge without downloading a browser during
  `dotnet test`.
- Receive physical TRX files and attachments in the SDK results directory.
- Preserve useful failure diagnostics if the managed runtime crashes or stops
  yielding.
- Keep contracts versioned across SDK, runtime, and testfx release bands.
- Allow future UI-framework integrations without changing the common MTP
  transport.

## Non-goals

- WASI.
- VSTest; this design applies to MTP mode.
- Blazor component or application UI testing in Phase 1.
- Host-side Playwright UI/E2E test authoring in Phase 1.
- Installing browsers or operating-system dependencies during `dotnet test`.
- Remote browsers, mobile-device emulation, or multiple tabs in Phase 1.
- Branded Safari. Playwright provides a patched WebKit build, not Safari.
- Dynamic loading of test assemblies from the browser VFS.
- Unloading test assemblies or reusing one runtime instance for multiple test
  modules.
- `dotnet watch test` with in-page hot reload or repeated test sessions.
- Managed debugging inside the browser.

## Execution models

### Phase 1: browser WebAssembly unit tests

The project targets `Microsoft.NET.Sdk.WebAssembly` and `browser-wasm`.
Test assemblies are statically referenced and registered at build time. The
test host does not load assemblies from host paths or the browser VFS.

The WebAssembly testing targets own the generated `index.html` and `main.js`
used for test execution. Copying the SDK-to-page bootstrap into every project
would make the contract impossible to service. A user-supplied
`WasmMainJSPath` or test `index.html` either composes through an explicit
extension point or produces an actionable conflict diagnostic.

The JavaScript supervisor starts the runtime and MTP main assembly, reports
the managed exit code to Playwright, and remains responsible for JavaScript
errors, runtime aborts, and launcher deadlines. It never assumes managed code
can report after the runtime exits.

### Later phase: Blazor component tests

The standard Blazor application and renderer start normally. A thin
Blazor-owned integration begins MTP only after the renderer is ready and
reports completion before the launcher closes the page.

This enables component rendering and Blazor-specific JavaScript interop. It is
more complex than the Phase 1 host because Blazor's `Program.Main` normally
awaits the application lifetime and does not return when tests finish.

The production Blazor Gateway remains unchanged. If it is the
project-provided server, the SDK launcher starts it as a child process using
the existing `RunCommand` and `RunArguments`.

### Relationship to Playwright UI tests

Browser-hosted MTP tests and Playwright UI tests have different execution
models:

- In this design, test code runs inside WebAssembly. Playwright is the external
  launcher and supervisor.
- In a traditional UI/E2E project, Playwright test code runs on the host and
  drives an application page.

Using Playwright here creates reusable browser infrastructure for future UI
work, but it does not automatically make host-side Playwright APIs callable
from tests running inside WebAssembly. A future UI layer must define whether
tests run on the host, inside the browser, or across an explicit bridge.

## Proposed project shape

An illustrative Phase 1 project is:

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net11.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <SelfContained>true</SelfContained>

    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    <WasmEnableTestHost>true</WasmEnableTestHost>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="&lt;version&gt;" />
    <ProjectReference Include="..\MyTests\MyTests.csproj" />
  </ItemGroup>
</Project>
```

`WasmEnableTestHost` is a proposed general name, consistent with
`WasmEnableHotReload`. The targets may infer it from
`IsTestProject`/`IsTestingPlatformApplication` and `browser-wasm`, while the
property remains an explicit override.

The project owns its managed entry point and registers the referenced test
assembly:

```csharp
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => [typeof(SomeTest).Assembly]);

using ITestApplication application = await builder.BuildAsync();
return await application.RunAsync();
```

## Proposed command-line experience

The final naming is open, but browser-specific terms are clearer than treating
a browser as a mobile device:

```dotnetcli
dotnet test --project MyTests.Browser
dotnet test --project MyTests.Browser --list-browsers
dotnet test --project MyTests.Browser --browser chrome
dotnet test --project MyTests.Browser --browser edge --headed
dotnet test --project MyTests.Browser --browser chrome \
  --browser-arg=--use-fake-device-for-media-stream
```

Proposed defaults:

- Phase 1 is headless by default in interactive and non-interactive sessions.
  A blank unit-test page provides no useful headed UI.
- `--headed` exists for debugging browser startup, developer-tools output, or
  a future progress application.
- One compatible installed browser is selected automatically.
- Multiple compatible browsers in a non-interactive environment require
  `--browser`.
- The launcher uses a new isolated browser context/profile and never modifies
  the user's normal profile.
- `--browser-arg` is repeatable. Arguments that disable process ownership,
  remote-debugging isolation, profile isolation, or security guarantees are
  rejected. Values are redacted when they contain known secrets.

All arguments after `--` remain MTP/test-application arguments.

## Browser support and Playwright

### Initial support

Phase 1 supports installed stable Chrome and Edge channels on Windows, macOS,
and Linux. The launcher validates the browser family and a tested minimum
version before launch and reports an actionable error for unsupported
executables.

Firefox is deferred because Playwright requires its patched Firefox build and
does not support the branded installed Firefox. Playwright WebKit is also
deferred and must not be described as Safari. Node, V8, and other JavaScript
engines are separate host kinds, not browser choices.

### What Playwright provides

The first implementation uses Playwright rather than a homegrown browser
driver for:

- browser launch and installed Chrome/Edge channels;
- headless/headed mode and advanced browser arguments;
- isolated non-persistent contexts;
- navigation and page lifetime;
- page-scoped bootstrap/completion bindings;
- browser console, page error, unhandled rejection, crash, and disconnect
  events;
- screenshots and traces for launcher failures;
- deterministic context and browser closure.

Playwright stays alive outside the managed runtime. It can therefore report a
page crash, preserve browser diagnostics, and close a page after managed code
has stopped responding.

### What Playwright does not provide

Playwright does not:

- replace the MTP protocol used for discovery and test results;
- make a synchronous infinite WebAssembly loop observe cancellation;
- make MTP flush final results after a fatal runtime crash;
- read a managed in-memory VFS file without a managed/JavaScript export bridge;
- turn browser-hosted MTP tests into host-side Playwright UI tests;
- control branded Safari;
- remove SDK/runtime/testfx compatibility requirements.

### Ship gates

Playwright introduces a prebuilt Node driver and a separately serviced
dependency. Its preferred Chromium, Firefox, and WebKit binaries normally
require downloads and consume hundreds of megabytes. The SDK must not perform
those downloads implicitly.

Before shipping, the owning teams must prove:

- offline and source-build/VMR compatibility;
- acceptable package and installed-size impact;
- compatibility with the SDK servicing lifetime and evergreen Chrome/Edge;
- operation with installed browsers under supported enterprise policies;
- reliable process-group/job-object cleanup on every operating system;
- a browser-version validation and support policy.

## Components and ownership

| Concern | Proposed owner |
| --- | --- |
| Browser-WASM testing targets, generated page/supervisor assets, project evaluation, browser CLI, Playwright launcher, HTTP gateway, result presentation, output paths, and cancellation policy | `dotnet/sdk` (`WasmSdk` and CLI) |
| Browser MTP client, test-result protocol, artifact-content protocol, and managed cancellation client | `microsoft/testfx` |
| WasmAppHost and documented browser runtime/JavaScript APIs | `dotnet/runtime` |
| Blazor renderer-ready/completion integration and template | `dotnet/aspnetcore` |
| Production Blazor Gateway | `dotnet/aspnetcore`, unchanged |
| Uno/Avalonia/other framework-specific renderer integration | Framework owner |

## Versioning across SDK and runtime

The SDK and runtime are versioned and serviced independently. Any SDK-owned
managed or JavaScript payload injected into a target application is therefore
a versioned, backward-compatible contract.

The browser hot-reload agent is prior art:

- assets live in the general `WasmSdk`;
- agent TFMs are intentionally fixed to the oldest supported target;
- targets select the appropriate payload from `TargetFrameworkVersion`;
- the payload uses documented runtime APIs and negotiates capabilities.

The test payload follows the same rules:

- build per supported target-framework band;
- select the oldest compatible payload for that band;
- version the launcher-to-page bootstrap and completion contract;
- negotiate optional features rather than assuming SDK and runtime versions
  match;
- use documented runtime APIs only in Phase 1;
- require an explicit cross-repository contract and compatibility plan before
  depending on new runtime behavior;
- test SDK N against every still-supported earlier target framework.

Phase 1 should not require a new runtime API. If investigation proves one is
needed, its versioning and fallback belong in the runtime contract rather than
an SDK probe of private JavaScript state.

## Generic SDK launch contract

Projects describe their test host through values produced by
`ComputeRunArguments`. Existing `RunCommand`, `RunArguments`, and
`RunWorkingDirectory` remain the project server-launch contract.

Proposed additional values:

| Property | Meaning |
| --- | --- |
| `DotnetTestTransport` | `pipe` or `http`; explicit value takes precedence over RID inference. |
| `DotnetTestHostKind` | `process`, `browser`, or `device`. |
| `DotnetTestApplicationUrl` | Optional exact browser origin when known before launch. |

For `browser`, the SDK starts its Playwright launcher instead of starting
`RunCommand` directly. The launcher receives an owner-only configuration file
containing the server command/arguments, selected browser, browser arguments,
deadline, and HTTP bootstrap response-file path.

MTP arguments remain after a `--` sentinel. The launcher expands only the
bootstrap file named in its protected configuration; arbitrary user `@`
arguments are not expanded.

`--test-modules` is unsupported for `browser-wasm` in Phase 1. It bypasses
project evaluation, and browser WebAssembly cannot dynamically load and unload
arbitrary test modules from host paths.

## Server readiness

The project-provided server may be WasmAppHost, the Blazor Gateway, or a
framework-specific host. The launcher must not inspect the command name.

The final readiness contract remains open. It must provide the exact browser
origin without parsing localized human output. Options include:

- a project-provided `DotnetTestApplicationUrl`;
- a protected endpoint/port file written by the child server;
- an SDK-selected loopback port forwarded through project-owned launch
  arguments.

The launcher registers the byte-identical origin with the SDK gateway before
navigating. `127.0.0.1` and `localhost` are not interchangeable for origin
checks.

Framework hosts remain responsible for their production configuration.
Test launch must disable redirects, telemetry exporters, inherited URL
settings, or other behavior that changes the selected origin or creates
unrelated traffic.

## Secure bootstrap and CSP

The existing owner-only response file contains the MTP HTTP endpoint and
bearer token. The SDK passes only its path to the Playwright launcher through
another owner-only launcher configuration.

Playwright exposes a page-scoped `__getDotnetTestBootstrap` binding before
navigation. It returns values only to the top-level frame at the registered
origin and invalidates the values after use. The bootstrap never appears in:

- the page URL or query string;
- browser command-line arguments;
- static JavaScript or JSON;
- launch settings;
- environment inherited by unrelated child processes;
- normal logs.

The token remains observable to code in the test page because that code must
send it. The SDK redacts the token from captured stdout/stderr, browser
console, page errors, traces, and inbound protocol strings before rendering or
persisting them.

MTP HTTP requests are still subject to the page's CSP `connect-src` policy.
The generated Phase 1 page explicitly permits its exact SDK endpoint. A future
framework integration must not silently bypass an application's CSP: it
either composes an explicit test policy or fails with a targeted diagnostic.
Playwright injection alone does not solve `connect-src`.

The SDK HTTP gateway needs an authenticated pre-navigation origin-registration
operation. It succeeds only before the first protocol frame. Legacy direct
browser hosts may retain first-preflight origin pinning for compatibility.

This bootstrap and all host-file operations require a security review.

## JavaScript supervision and failure model

The generated JavaScript module, controlled by Playwright, is the supervisor.
Managed test code is not.

For a Phase 1 test host:

1. Acquire the protected bootstrap through the Playwright binding.
2. Create the runtime with the MTP arguments.
3. Subscribe to JavaScript errors, unhandled rejections, runtime abort/exit,
   browser console, and page crash.
4. Run the managed MTP entry point.
5. Report its exit code through the Playwright completion binding.

The exact use of `runMain` versus `runMainAndExit` must follow the supported
runtime API for the targeted TFM. The contract is that MTP finishes live
messages and artifact transfers before managed exit, while the JavaScript
supervisor remains able to report the final exit code. No managed callback is
required after runtime exit.

For a later Blazor integration, `Program.Main` does not return. The
renderer-aware MTP component reports completion through the binding, while
Playwright remains the out-of-process deadline and crash supervisor.

Failure classes remain distinct:

- **Test failure:** MTP completes and returns a test exit code.
- **Managed runtime failure:** JavaScript/Playwright reports abort, exit, or
  page failure; final managed artifacts may be unavailable.
- **Synchronous hang:** the sole WebAssembly thread stops yielding. Managed
  HTTP, managed WebSocket, JavaScript scheduled on the same page thread, and
  cancellation tokens cannot make progress. Playwright enforces the external
  deadline and force-closes the page/browser.
- **Browser failure:** Playwright reports crash/disconnect and preserves the
  bounded console/error tail and optional trace/screenshot.

## Live MTP results

The browser MTP application connects directly to the SDK's existing
`HttpTestHostGateway`. Playwright and the project server do not proxy or
interpret MTP results.

The existing HTTP protocol continues to carry handshake, help, discovery,
per-test results and output, session events, artifact metadata, and display
messages. Replacing this already-merged primary channel with WebSocket would
add compatibility and security work without enabling a current requirement.

All browser-side MTP code remains valid on single-threaded WebAssembly:

- asynchronous `fetch`/promise continuations only;
- no dedicated threads or blocking waits;
- one primary protocol request in flight;
- bounded frames and artifact chunks.

## Artifact export

MTP writes TRX and attachments through managed `System.IO` APIs into the
browser VFS. `FileArtifactMessages` reports paths in that VFS, which the host
SDK cannot open and which disappear when the page closes.

This VFS use is file I/O only. Test assemblies are statically referenced and
registered at build time; the design does not load or unload assemblies from
the VFS.

The proposed reusable solution extends `dotnettestcli` with a negotiated,
chunked artifact-content transfer. MTP reads each artifact with managed file
APIs and sends:

```text
FileArtifactTransferStart
  TransferId, ExecutionId, InstanceId, SourcePath
  DisplayName, Description, TestUid, TestDisplayName, SessionUid, Kind
  Length, Sha256

FileArtifactTransferChunk
  TransferId, Sequence, Bytes

FileArtifactTransferEnd
  TransferId
```

The shared serializer adds an explicit length-prefixed byte-array primitive.
Independent artifact-transfer support is handshake-capability gated.

The SDK:

- chooses the destination below the resolved results directory;
- never accepts a browser-supplied host destination;
- canonicalizes and validates all generated paths;
- rejects traversal, rooted paths, reserved names, symlink/reparse-point
  escapes, duplicate transfer IDs, unexpected chunks, and quota violations;
- writes a new temporary file with restrictive permissions;
- validates declared length and SHA-256;
- atomically moves the validated file into place;
- deletes incomplete or invalid transfers;
- applies per-file and aggregate-run limits;
- does not invoke post-processing until materialization succeeds.

This design needs a dedicated threat model and security review.

Playwright could implement a browser-specific VFS extraction bridge, but it
cannot read the managed VFS without additional managed/JavaScript code. A
protocol feature is preferable because it also works for other sandboxed MTP
hosts and preserves test/session artifact metadata.

Until transfer support ships, live results work but the SDK must not claim
that a browser VFS path is a persisted host artifact.

## Cancellation and timeouts

The primary HTTP channel is host-initiated request/reply and cannot push
SDK-originated cancellation while the browser is idle.

The proposed reverse-control transport is a separate authenticated WebSocket,
using the existing `WaitForServerControlRequest` and
`ServerControlMessage(CancelSession)` semantics where possible. This follows
the browser/mobile hot-reload precedent and avoids inventing HTTP polling.

The browser initiates the WebSocket connection. Because browser WebSockets
cannot set an arbitrary Authorization header, authentication needs a separate
design, such as the public-key/encrypted-subprotocol pattern used by hot
reload. Control support is independently negotiated in the handshake.

The primary result channel remains HTTP. A managed `ClientWebSocket` is a
candidate implementation for reverse control only; it does not make blocked
or crashed managed code responsive.

Cancellation behavior:

1. The first Ctrl+C, `--timeout`, or run-policy limit sends cooperative
   cancellation over the control channel.
2. MTP cancels the session and flushes results/artifacts when its event loop can
   run.
3. The launcher uses a shorter internal deadline than the SDK process-kill
   grace period.
4. If the page cannot finish, Playwright closes the context/browser.
5. A second Ctrl+C force-kills the owned process group.

Cooperative cancellation is best effort. A synchronous infinite loop or fatal
runtime crash cannot produce a graceful MTP summary regardless of whether the
transport is HTTP, WebSocket, or a Playwright binding.

## Host and browser path handling

Host paths passed today through `--results-directory`, `--config-file`, and
`--diagnostic-output-directory` are meaningless inside the browser VFS.

For Phase 1:

- Results use a generated VFS root mapped back through artifact transfer.
- `--config-file` fails before launch until MTP has an in-memory configuration
  contract.
- `--diagnostic-output-directory` fails before launch until diagnostic files
  are transferable artifacts.
- User arguments after `--` are not path-translated.
- Browser-originated paths are never printed as persisted host paths.

## Parallelism and cleanup

Each test application receives a unique MTP endpoint/token, application
origin, Playwright context, temporary profile, execution ID, and artifact
namespace. The future reverse-control channel receives independent
credentials.

`--max-parallel-test-modules` governs concurrency. The default may need to be
lower than ordinary process tests because browsers are expensive; this remains
an open performance decision.

The launcher owns Playwright, browser, and project-server cleanup. The parent
SDK command owns MTP listeners and response files. Owned processes remain in a
process group/job object so the SDK can force-clean them. A later invocation
may delete only stale profiles carrying the launcher's ownership marker.

## `dotnet watch` and repeated runs

`dotnet watch test` currently restarts test processes rather than providing
the `run` command's in-page hot reload. Adding browser-runtime reuse would be a
separate cross-cutting feature.

Phase 1 uses one test module per page load. There is no assembly unload, so a
repeat run reloads the page or creates a new context. The bootstrap and
protocol must not permanently encode "one browser process equals one test
session", preserving a future option to reuse the outer browser process.

## Compatibility and fallback

- Existing non-browser projects remain unchanged.
- Explicit `DotnetTestTransport` takes precedence over current
  `browser-*`/`wasi-*` RID inference. WASI remains outside this design.
- Older MTP versions fail through existing transport/handshake diagnostics.
- Optional artifact and reverse-control features are independently
  capability-negotiated.
- The SDK-injected page payload is selected per supported target-framework
  band.
- Phase 1 does not require a new runtime API.
- Node/V8 execution and the standalone work in
  [dotnet/sdk#55389](https://github.com/dotnet/sdk/pull/55389) are separate host
  scenarios.

## Implementation plan

### Phase 1: general browser-WASM unit tests

**dotnet/sdk**

- Add common testing targets to `WasmSdk`, conditioned on `browser-wasm`.
- Generate and version the test page and JavaScript supervisor.
- Add the Playwright launcher, Chrome/Edge discovery, browser arguments,
  console/error/crash capture, process ownership, and cleanup.
- Add the generic launch/readiness contract and protected bootstrap.
- Keep the existing MTP HTTP primary transport.
- Validate source-build, offline, servicing, and browser-version requirements.

**microsoft/testfx**

- Keep browser MTP compatible with the existing HTTP transport.
- Document generated-host requirements and completion ordering.

Exit criteria:

- `dotnet test`, `--list-tests`, help, filters, and live results pass headlessly
  on installed Chrome and Edge across Windows, macOS, and Linux.
- Managed failure, page crash, and synchronous timeout produce distinct
  diagnostics.
- No credential appears in URLs, process listings, console output, or logs.
- No browser is downloaded during `dotnet test`.

### Phase 2: artifact export

- Add managed VFS reads and negotiated chunked artifact transfer in testfx.
- Materialize and validate browser artifacts in the SDK.
- Complete security review and threat-model tests.

Exit criteria:

- `--report-trx` creates a physical TRX in the requested SDK directory.
- Attachments survive browser shutdown.
- Parallel or malicious transfers cannot overwrite or escape host output.

### Phase 3: graceful reverse control

- Define an authenticated browser-initiated WebSocket control contract.
- Reuse MTP control semantics and independently negotiate support.
- Wire Ctrl+C and test-run policy cancellation.
- Retain Playwright deadlines and process-tree force cleanup.

### Phase 4: Blazor component integration

- Define a renderer-ready/completion hook.
- Add a thin Blazor wrapper/template over the common WasmSdk targets.
- Start the production Gateway only through its existing process contract.
- Validate component rendering and Blazor JavaScript interop tests.

### Phase 5: broader hosts and browsers

- Evaluate Playwright Firefox and WebKit builds.
- Evaluate Node/V8 as separate host kinds.
- Consider browser-process reuse and `dotnet watch test`.
- Let other UI frameworks add renderer-specific wrappers.

## Test plan

### SDK and testfx

- SDK N against every supported earlier target framework.
- Generated page/JS payload selection and protocol negotiation.
- Chrome/Edge discovery, minimum-version validation, browser arguments, and
  unsupported executable diagnostics.
- Source-build/offline launcher activation without browser downloads.
- Response-file permissions, redaction, origin/CSP enforcement, and cleanup.
- Browser console, page error, crash, disconnect, and synchronous timeout.
- HTTP result ordering and WebSocket control authentication/cancellation.
- Managed VFS reads, binary serialization, quotas, path canonicalization,
  traversal/symlink defenses, partial-file cleanup, and correlation.
- Parallel projects, multi-targeting, and process-tree cleanup.
- `--no-build`.

### End to end

- Passing, failing, skipped, filtered, retried, and zero-test runs.
- `--list-tests` text and JSON.
- Extension help.
- TRX and attachment export.
- Ctrl+C and policy cancellation.
- Fatal managed runtime crash and non-yielding test timeout.
- Later Blazor renderer/component scenarios.

## Open questions

1. Should the Playwright launcher ship in `dotnet/sdk` or a testfx-owned tool?
2. What package/source-build model makes Playwright acceptable in the SDK?
3. What is the stable generic server-readiness contract?
4. Is `WasmEnableTestHost` the right opt-in name, and when is it inferred?
5. Should browser CLI reuse `--device` or use `--browser`/`--list-browsers`?
6. What minimum Chrome/Edge versions and servicing policy are supportable?
7. Which browser arguments must be rejected to preserve isolation?
8. Should artifact bytes use the MTP protocol or a browser-specific bridge?
9. What artifact quotas are appropriate?
10. What authentication should the reverse-control WebSocket use?
11. What default browser-module concurrency is safe?
12. Which target-framework bands must SDK N support?

## Rejected alternatives

### Make the Blazor Gateway a test runner

Rejected because the Gateway is a production component and should not contain
test infrastructure.

### Put browser testing only in the Blazor SDK

Rejected because browser WebAssembly is the common runtime layer used by
Blazor and other frameworks.

### Replace the primary MTP HTTP transport with WebSocket

Rejected for the current design because authenticated HTTP request/reply is
already implemented and sufficient for host-initiated live results. WebSocket
is considered only for reverse control.

### Treat Playwright as a replacement for MTP

Rejected because Playwright controls the browser but does not provide MTP
discovery/results, graceful managed cancellation, or VFS artifact semantics.

### Put credentials in the page URL or static configuration

Rejected because URLs leak through browser history, developer tools, and logs,
and unauthenticated static configuration can be read by another local process.

### Load test assemblies from the VFS

Rejected because test assemblies must be resolved at build time and assembly
unload is unavailable for the intended repeat/isolation model.

### Upload artifacts to arbitrary host paths supplied by the browser

Rejected because it creates traversal, overwrite, and privilege-boundary
risks. The SDK chooses and validates every host destination.
