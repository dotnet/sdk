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
runtime identifier. UI-framework integration is out of scope and discussed
only in an appendix.

The following constraints are established:

- Browser-hosted Microsoft.Testing.Platform (MTP) uses the existing
  authenticated HTTP `dotnettestcli` transport for live test messages.
- Per-run credentials do not belong in URLs, ordinary process arguments,
  static assets, launch profiles, or logs.
- The browser must be supervised from outside the WebAssembly runtime so
  crashes and synchronous hangs can still be detected and force-terminated.
- Browser-WASM server unification, or extraction of one shared host component,
  is a prerequisite. `dotnet test` must not add another static-file/dev server
  beside the existing runtime and framework hosts.

The proposed first implementation uses Playwright and supports headless
Chrome and Edge. Playwright is a browser-control layer; it does not replace
the MTP result protocol, graceful managed cancellation, or the managed bridge
needed to export browser artifacts.

WASI is a separate host model and is out of scope. The current SDK happens to
select HTTP for `wasi-*` runtime identifiers, but this design does not define
WASI build, launch, or result behavior.

## Summary

`dotnet test` should be able to:

1. Build a `browser-wasm` MTP test application.
2. Generate the test page and JavaScript supervisor as Static Web Assets served
   by the shared browser-WASM host.
3. Start that host and launch an isolated browser through Playwright.
4. Run MTP inside the browser and stream discovery, output, and per-test
   results to the SDK over the existing authenticated HTTP transport.
5. Detect managed runtime failures through Playwright even when MTP can no
   longer report.
6. Upload physical TRX files and attachments directly where possible, with a
   browser VFS compatibility path for file-only producers.
7. Cancel cooperatively when possible and force-close the browser when the
   single WebAssembly thread cannot respond.
8. Clean up every process, profile, endpoint, response file, and partial
   artifact created for the run.

Phase 1 targets `Microsoft.NET.Sdk.WebAssembly` test applications.
They cover business logic, networking, runtime JavaScript interop, and other
tests that need the real browser runtime but not a UI framework.

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
- `dotnet watch` hot reload already ships SDK-owned managed and JavaScript
  assets into browser WebAssembly applications. Its fixed per-TFM agent is
  prior art for the versioning boundary in this proposal. See
  [`TargetFrameworks.props`](../../src/WasmSdk/Sdk/TargetFrameworks.props) and
  [`Sdk.targets`](../../src/WasmSdk/Sdk/Sdk.targets).
- Runtime and ASP.NET are tracking unification of the general WASM dev server
  and Blazor Gateway. This proposal depends on that work producing one reusable
  host implementation or host contract.
  See [dotnet/runtime#122144](https://github.com/dotnet/runtime/issues/122144)
  and [dotnet/aspnetcore#67814](https://github.com/dotnet/aspnetcore/issues/67814).

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

## Non-goals

- WASI.
- VSTest; this design applies to MTP mode.
- UI-framework component/application testing and host-side Playwright UI/E2E
  authoring.
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
Test names, regular expressions, globs, categories, attributes, and other
framework filters use the same MTP/framework options as non-browser runs; the
browser launcher neither defines nor translates a separate filter language.

## Browser support and Playwright

### Initial support

Phase 1 supports installed stable Chrome and Edge channels on Windows, macOS,
and Linux. At each .NET feature release, the SDK pins a Playwright version and
validates the current Chrome/Edge Stable or Extended Stable baseline and newer
supported channels. The baseline is refreshed for each .NET feature release;
servicing updates may refresh Playwright when required for evergreen-browser
compatibility. The launcher reports the detected versions and an actionable
error for unsupported executables.

Firefox is deferred because Playwright requires its patched Firefox build and
does not support the branded installed Firefox. Playwright WebKit is also
deferred and must not be described as Safari. Node, V8, and other JavaScript
engines are separate host kinds, not browser choices.

Playwright does not provide the console-forwarding behavior required by the
existing Firefox WebAssembly harness. Supporting Firefox therefore also needs
the xharness-style browser-side console bridge; it is not enabled merely by
selecting Playwright Firefox.

### What Playwright provides

The first implementation uses Playwright rather than a homegrown browser
driver for:

- browser launch and installed Chrome/Edge channels;
- headless/headed mode and advanced browser arguments;
- isolated non-persistent contexts;
- navigation and page lifetime;
- page-scoped bootstrap/completion bindings;
- Chrome/Edge browser console, page error, unhandled rejection, crash, and
  disconnect events;
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
| Shared browser-WASM host implementation and static-web-assets serving behavior | Runtime/ASP.NET server-unification work |
| Browser MTP client, test-result protocol, artifact-content protocol, and managed cancellation client | `microsoft/testfx` |
| Documented browser runtime/JavaScript APIs | `dotnet/runtime` |

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

The WebAssembly targets describe the browser test host through evaluated
MSBuild properties. `RunCommand`, `RunArguments`, and `RunWorkingDirectory`
identify the shared browser-WASM host process; the SDK does not implement or
identify a server by executable name.

Those values must be set by WasmSdk/framework targets that also advertise a
supported `DotnetTestHostContractVersion`. User `RunCommand`, `StartAction`,
launch-profile URL/environment, and command-line overrides do not become the
test host implicitly; they are ignored or produce an actionable conflict
diagnostic.

Proposed additional values:

| Property | Meaning |
| --- | --- |
| `DotnetTestTransport` | `pipe` or `http`; explicit value takes precedence over RID inference. |
| `DotnetTestHostKind` | `process`, `browser`, or `device`. |
| `DotnetTestHostContractVersion` | Shared host launch/readiness contract understood by the project. |

For `browser`, the SDK starts its Playwright launcher, which starts the
project-declared shared host using `RunCommand` and `RunArguments`. The
launcher receives an owner-only configuration file containing that command,
the selected browser and arguments, deadline, HTTP bootstrap response-file
path, and an owner-only launch-info-file path.

The generated test page and JavaScript supervisor are Static Web Assets. They
flow through the normal endpoint manifest so the shared host owns import maps,
fingerprinting/integrity, MIME types, compression variants, SPA fallback, and
other serving behavior. COOP/COEP requirements are also represented in
build-generated endpoint metadata rather than duplicated in host middleware.

MTP arguments remain after a `--` sentinel. The launcher expands only the
bootstrap file named in its protected configuration; arbitrary user `@`
arguments are not expanded.

`--test-modules` is unsupported for `browser-wasm` in Phase 1. It bypasses
project evaluation, and browser WebAssembly cannot dynamically load and unload
arbitrary test modules from host paths.

## Server readiness

The shared host contract is a prerequisite for Phase 1. It must cover both
general and framework browser-WASM applications without adding test
infrastructure to the production Gateway.

The SDK passes the host:

```text
--urls http://127.0.0.1:0
--dotnet-launch-info-file <owner-only path>
```

On `ApplicationStarted`, the host atomically writes a versioned JSON document:

```json
{
  "contractVersion": 1,
  "pid": 1234,
  "urls": ["http://127.0.0.1:54321"],
  "basePath": "/",
  "testPath": "/_dotnet-test/",
  "capabilities": [
    "static-assets-endpoints",
    "spa-fallback",
    "coop-coep"
  ]
}
```

`testPath` is a build-emitted reserved Static Web Assets endpoint for the
generated test page. It must not be satisfied by SPA fallback; otherwise an
incorrect route could return an unrelated application page with HTTP 200 and
degrade into an opaque bootstrap timeout.

The host binds loopback port zero directly and publishes the actual address; it
must not probe a free port, release it, and bind later. The launcher waits for
the atomic launch-info file and, when advertised, a health endpoint. It never
scrapes localized console output for readiness.

The launcher creates the launch-info directory with owner-only permissions and
requires the output file not to exist. It rejects symlinks/reparse points,
non-loopback URLs, unexpected base/test paths, duplicate writes, and a PID that
does not match the child it launched before registering any origin.

The host contract uses `major.minor` versioning:

- the project property declares the minimum contract it expects to launch;
- the launch-info document declares the contract actually implemented by the
  child;
- the SDK accepts the supported major version and ignores unknown optional
  capabilities/minor additions;
- a missing required capability, lower version, or unknown major version fails
  with an actionable host/SDK compatibility diagnostic;
- failure to create the launch-info file within the startup deadline reports
  the expected contract plus bounded host stdout/stderr.

Browser testing is available only for target-framework/host bands that ship a
compatible shared host. SDK N does not promise to retrofit the feature onto
every older supported TFM whose host predates this contract.

The launcher registers the byte-identical origin with the SDK gateway before
navigating. `127.0.0.1` and `localhost` are not interchangeable for origin
checks.

The shared host serves the normal Static Web Assets endpoints manifest rather
than reimplementing MIME tables, import maps, fallback routes, or compression.
Phase 1 uses loopback HTTP and avoids certificate provisioning. HTTPS and
application certificate testing belong to a separate host/UI scenario; the
launcher must not silently use Playwright's context-wide certificate bypass.

Test-only MTP, config, artifact, origin-registration, and cancellation
endpoints remain on the SDK's authenticated control plane. They do not become
production routes in the shared host or Blazor Gateway.
The SDK control plane serves no application assets: no Static Web Assets
routes, import maps, MIME handling, SPA fallback, compression, or COOP/COEP.

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

MTP HTTP requests are still subject to CORS, Private Network Access (where
applicable), and the page's CSP `connect-src` policy. The SDK endpoint handles
the browser's unauthenticated, origin-pinned CORS/PNA `OPTIONS` preflight; the
subsequent protocol requests carry authentication. The generated Phase 1 page
explicitly permits its exact SDK endpoint. A future framework integration must
not silently bypass an application's CSP: it either composes an explicit test
policy or fails with a targeted diagnostic. Playwright injection alone does
not solve CORS or `connect-src`.

The SDK HTTP gateway needs an authenticated pre-navigation origin-registration
operation. It succeeds only before the first protocol frame and a supported
shared-host launch.

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

### Failure diagnostics

Every startup phase and timeout produces a failure envelope even when managed
code cannot respond. It includes, when available:

- the phase and elapsed time;
- shared-host bind/start/probe errors and Playwright driver/browser exit
  codes or operating-system termination signals;
- the last active test names reported by MTP
  `TestInProgressMessages`;
- bounded shared-host stdout/stderr and launch diagnostics;
- bounded browser console, page errors, unhandled promise rejections, failed
  requests, and the final URL;
- browser/driver disconnect and crash information;
- the selected browser family/version and relevant launch arguments, with
  secrets redacted;
- optional Playwright trace and screenshot paths.

MTP emits and awaits a per-test-start breadcrumb before invoking each test
body. The SDK records it immediately, so a synchronous hang or abrupt crash
can identify the last started test without waiting for the next periodic
progress update. `TestInProgressMessages` can carry this signal, but its
producer must publish at the test boundary rather than rely only on periodic
sampling.

This evidence distinguishes common infrastructure classes such as resource
exhaustion, shared-memory/inode limits, server-port failures, DNS/network
timeouts, certificate failures, missing or mismatched browser/driver versions,
GPU/container failures, process signals, runtime aborts, and WebAssembly
memory corruption. Diagnostics must not infer a specific cause from an
ambiguous symptom alone; for example, `Failed to fetch` is not sufficient
evidence to report out-of-memory.

The launcher has short phase-specific startup/control timeouts in addition to
the user-visible test timeout. It must never wait for the full test timeout
after the server, driver, browser, or page has already failed.

## Live MTP results

The browser MTP application connects directly to the SDK's existing
`HttpTestHostGateway`. Playwright and the shared host do not proxy or
interpret MTP results.

The existing HTTP protocol continues to carry handshake, help, discovery,
per-test results and output, session events, artifact metadata, and display
messages. Replacing this already-merged primary channel with WebSocket would
add compatibility and security work without enabling a current requirement.
Results stream as MTP publishes test-node updates during execution; the SDK
does not wait for the complete run before displaying them.

All browser-side MTP code remains valid on single-threaded WebAssembly:

- asynchronous `fetch`/promise continuations only;
- no dedicated threads or blocking waits;
- one primary protocol request in flight;
- bounded frames and artifact chunks.

The test framework and adapter must remain cooperative with the browser event
loop. They await asynchronous test methods and insert an asynchronous yield
between test cases. Authors should prefer asynchronous APIs for browser tests.
The framework cannot transform a synchronous test method into an asynchronous
one: a synchronous method that never yields can still block the runtime,
networking, progress messages, and graceful cancellation until Playwright
forces the page closed.

## Artifact export

Writing every artifact to the browser VFS first is not a requirement. The
preferred design gives MTP extensions a host-provided artifact sink that
uploads bytes directly to a separate authenticated SDK artifact endpoint
through managed `HttpClient`.

Artifacts upload as soon as their producer finalizes them. The SDK writes the
upload to a restrictive temporary file and exposes it as a completed artifact
only after declared length/hash validation succeeds. Incremental TRX
serialization during the run would require separate testfx design work because
the final document contains run-level summary data.

Bounded ordered chunk requests are the Phase 2 baseline:

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

Independent artifact-transfer support is handshake-capability gated. A
chunked protocol adds an explicit length-prefixed byte-array primitive; binary
content is not converted through UTF-8 strings.

The artifact endpoint has an independent connection and concurrency limit, so
a large upload does not block the single in-flight live-result request. The
producer must finish and receive the final artifact acknowledgement before it
publishes the corresponding final session completion and before the page
closes.

A single streaming request body is only a future optimization where the
browser handler and host endpoint support it. Browser response streaming does
not imply request streaming; the current loopback HTTP/1.1 gateway cannot be
treated as supporting streamed request bodies.

Existing file-only extensions remain compatible: they write through managed
`System.IO` into the browser VFS, then MTP opens the file with managed APIs and
uploads it through the same sink before the page closes. This VFS use is file
I/O only. Test assemblies are statically referenced and registered at build
time; the design does not load or unload assemblies from the VFS.

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

Playwright evaluation could invoke a browser-specific export function, but it
cannot read the managed VFS without additional managed/JavaScript code and is
unavailable after a fatal page/runtime failure. A managed artifact sink is
preferred because it works for other sandboxed MTP hosts, streams during the
run, and preserves test/session artifact metadata. Playwright remains a
possible diagnostic or compatibility fallback rather than the primary artifact
channel.

Until transfer support ships, live results work but the SDK must not claim
that a browser VFS path is a persisted host artifact.

## Cancellation and timeouts

The primary HTTP channel is host-initiated request/reply and cannot push
SDK-originated cancellation while the browser is idle. Since Playwright is a
required launcher dependency, the proposed reverse path uses Playwright rather
than adding a browser WebSocket:

1. The parent SDK command sends a control message to its desktop launcher over
   local IPC.
2. The launcher uses page-scoped Playwright evaluation to call an injected
   JavaScript cancellation function.
3. JavaScript invokes a versioned managed MTP cancellation hook while the
   runtime is alive and yielding.

This removes a second browser transport and its authentication/CORS/CSP
surface. It also lets the launcher detect an absent/crashed runtime and move
directly to forced cleanup.

Cancellation behavior:

1. The first Ctrl+C, `--timeout`, or run-policy limit sends cooperative
   cancellation through the launcher.
2. MTP cancels the session and flushes results/artifacts when its event loop can
   run.
3. Playwright evaluation uses a short command timeout, well below the test and
   SDK process-kill deadlines.
4. If evaluation fails, times out, or the page cannot finish, the launcher
   applies a bounded Playwright page/context close deadline.
5. If close does not complete, the launcher unconditionally kills its owned
   browser/driver process group or job object.
6. A second Ctrl+C skips directly to the force-kill step.

Cooperative cancellation is best effort. A synchronous infinite loop or fatal
runtime crash cannot produce a graceful MTP summary regardless of whether the
signal uses Playwright, HTTP, or WebSocket. Playwright remains valuable because
it reports crash/disconnect events and enforces an external timeout; a hang is
identified by deadline expiry rather than an immediate browser event.

## Host and browser path handling

Host paths passed today through `--results-directory`, `--config-file`, and
`--diagnostic-output-directory` are meaningless inside the browser VFS.

For Phase 1:

- Results use the direct managed artifact sink where supported. File-only
  extensions use a generated VFS root and upload before shutdown.
- For `--config-file`, the SDK reads and validates the host file before launch,
  then serves its content from a per-run authenticated read-only endpoint.
  Browser MTP receives the endpoint through the protected bootstrap and loads
  it with managed `HttpClient`; the browser never receives the host path.
- Diagnostic files use the artifact sink. Until a diagnostic producer supports
  that sink, `--diagnostic-output-directory` fails before launch with an
  actionable unsupported-option diagnostic.
- User arguments after `--` are not path-translated.
- Browser-originated paths are never printed as persisted host paths.

## Parallelism and cleanup

Each test application receives a unique MTP endpoint/token, application
origin, Playwright context, temporary profile, execution ID, and artifact
namespace. The desktop SDK-to-launcher control channel is per-run and
restricted to the launching user/process boundary.

`--max-parallel-test-modules` governs concurrency. The default may need to be
lower than ordinary process tests because browsers are expensive; this remains
an open performance decision.

The launcher owns the shared-host child process plus Playwright/browser
cleanup. The parent SDK command owns MTP listeners and response files. Owned
processes remain in a process group/job object so the SDK can force-clean them.
A later invocation may delete only stale profiles carrying the launcher's
ownership marker.

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
- Browser launch is enabled only when the project advertises a supported
  `DotnetTestHostContractVersion`; older dev hosts fail with an actionable
  prerequisite diagnostic rather than falling back to an SDK static server.
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

### Phase 0: shared browser-WASM host prerequisite

**runtime/ASP.NET hosting**

- Unify the existing browser-WASM dev hosts or extract one shared host
  implementation used by general and framework projects.
- Serve the Static Web Assets endpoints manifest, including import maps,
  fingerprint/integrity metadata, MIME types, compression variants, SPA
  fallback, and build-provided COOP/COEP headers.
- Bind loopback port zero and atomically publish the versioned launch-info
  document.
- Keep test-only MTP/config/artifact/control endpoints out of the production
  host.

**dotnet/sdk**

- Generate the test page/supervisor as Static Web Assets consumed by the shared
  host.
- Emit required COOP/COEP endpoint metadata at build time, gated by the
  WebAssembly threading/cross-origin-isolation properties.
- Define the launcher-side contract and compatibility tests without
  implementing another static-file server.

Exit criteria:

- General and framework `browser-wasm` projects use the same host
  implementation/contract.
- The SDK can obtain a machine-readable origin without scraping stdout.
- No test-only route is added to the production Blazor Gateway.
- The shared host is available from the product layout/package in offline and
  source-build scenarios without downloading another server at test time.
- Supported SDK/TFM/host combinations have an explicit compatibility matrix.

### Phase 1: general browser-WASM unit tests

**dotnet/sdk**

- Add common testing targets to `WasmSdk`, conditioned on `browser-wasm`.
- Generate and version the test page and JavaScript supervisor.
- Consume the unified shared host and versioned launch-info/readiness contract.
- Add the Playwright launcher, Chrome/Edge discovery, browser arguments,
  console/error/crash capture, process ownership, and cleanup.
- Add the bundle-location contract and protected bootstrap.
- Keep the existing MTP HTTP primary transport.
- Validate source-build, offline, servicing, and browser-version requirements.

**microsoft/testfx**

- Keep browser MTP compatible with the existing HTTP transport.
- Publish and await a per-test-start progress breadcrumb before invoking the
  test body.
- Await asynchronous tests and cooperatively yield between test cases.
- Add an authenticated remote configuration input for the SDK-provided
  `--config-file` endpoint.
- Document generated-host requirements and completion ordering.

Exit criteria:

- `dotnet test`, `--list-tests`, help, filters, and live results pass headlessly
  on installed Chrome and Edge across Windows, macOS, and Linux.
- Managed failure, page crash, and synchronous timeout produce distinct
  diagnostics.
- Crash/timeout diagnostics identify the last test that began execution.
- No credential appears in URLs, process listings, console output, or logs.
- No browser is downloaded during `dotnet test`.

### Phase 2: artifact export

- Add a managed direct-upload artifact sink in testfx using negotiated bounded
  chunks.
- Evaluate a single streaming request only as an optional future optimization
  on browser/TFM and gateway combinations that support request streaming.
- Keep managed VFS reads as the compatibility path for existing file-only
  artifact producers.
- Materialize and validate browser artifacts in the SDK.
- Complete security review and threat-model tests.

Exit criteria:

- `--report-trx` creates a physical TRX in the requested SDK directory.
- Attachments survive browser shutdown.
- Parallel or malicious transfers cannot overwrite or escape host output.

### Phase 3: graceful reverse control

- Define per-run SDK-to-launcher local control and a versioned JavaScript/MTP
  cancellation hook invoked through Playwright evaluation.
- Wire Ctrl+C and test-run policy cancellation.
- Configure a short Playwright command timeout before forced page closure.
- Retain process-tree force cleanup for blocked or crashed runtimes.

### Phase 4: broader hosts and browsers

- Evaluate Playwright Firefox and WebKit builds.
- Evaluate Node/V8 as separate host kinds.
- Consider browser-process reuse and `dotnet watch test`.

## Test plan

### SDK and testfx

- SDK N against every supported earlier target framework.
- Generated page/JS payload selection and protocol negotiation.
- Chrome/Edge discovery, minimum-version validation, browser arguments, and
  unsupported executable diagnostics.
- Source-build/offline launcher activation without browser downloads.
- Response-file permissions, redaction, origin/CORS/PNA/CSP enforcement, and
  cleanup.
- Browser console, page error, crash, disconnect, and synchronous timeout.
- HTTP live-result ordering and Playwright-evaluated cancellation.
- Per-test-start crash breadcrumbs and cooperative yields between tests.
- Authenticated remote config loading, direct artifact upload, bounded chunks,
  managed VFS compatibility, binary serialization, quotas, path
  canonicalization, traversal/symlink defenses, partial-file cleanup, and
  correlation.
- Parallel port-bind retries and HTTP/HTTPS certificate behavior.
- Evidence-based diagnostics for resource, network, server, browser/driver,
  process-signal, and runtime failures.
- Parallel projects, multi-targeting, and process-tree cleanup.
- `--no-build`.

### End to end

- Passing, failing, skipped, filtered, retried, and zero-test runs.
- `--list-tests` text and JSON.
- Extension help.
- TRX and attachment export.
- Ctrl+C and policy cancellation.
- Fatal managed runtime crash and non-yielding test timeout.

## Open questions

1. Should the Playwright launcher ship in `dotnet/sdk` or a testfx-owned tool?
2. What package/source-build model makes Playwright acceptable in the SDK?
3. Which repository/package owns the unified host, and what exact
   launch-info/capability contract can SDK and framework versions support?
4. Is `WasmEnableTestHost` the right opt-in name, and when is it inferred?
5. Should browser CLI reuse `--device` or use `--browser`/`--list-browsers`?
6. Which Stable/Extended Stable Chrome/Edge baseline and Playwright servicing
   cadence are supportable?
7. Which browser arguments must be rejected to preserve isolation?
8. What testfx artifact-sink API supports direct upload while preserving
   existing file-only extensions, and can TRX be serialized incrementally?
9. Can MTP configuration loading become asynchronous and accept an
   authenticated remote input without changing existing process hosts?
10. What artifact quotas are appropriate?
11. What default browser-module concurrency is safe?
12. Which target-framework bands must SDK N support?

## Rejected alternatives

### Replace the primary MTP HTTP transport with WebSocket

Rejected for the current design because authenticated HTTP request/reply is
already implemented and sufficient for host-initiated live results.

### Add a browser WebSocket only for reverse control

Rejected while Playwright is required. SDK-to-launcher local control plus
Playwright evaluation avoids another browser transport and remains able to
detect a dead page. It still cannot make a synchronous blocked runtime
cooperate, so force-close remains necessary.

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

## Appendix: out-of-scope UI-framework considerations

UI and component testing are intentionally outside the implementation plan
above. They are different test applications, not wrappers around the
in-browser MTP unit-test host:

- Host-side Playwright UI/E2E tests run beside the browser and drive clicks,
  screenshots, and application behavior.
- Browser-hosted MTP tests run managed test methods inside the page.
- Component-focused tools such as
  [bUnit](https://github.com/bUnit-dev/bUnit) may inform future fixture,
  rendering, query, and assertion ergonomics, but do not define the real
  browser lifetime or interop model.

`Microsoft.NET.Sdk.BlazorWebAssembly` and other UI frameworks build on the
general WebAssembly SDK, so they can adopt the common foundation later. A
future Blazor-specific proposal may cover only browser-side tests that require
the Blazor renderer or Blazor JavaScript interop. It would need a
renderer-ready/completion hook because a Blazor application's
`Program.Main` normally runs for the application lifetime.

The production Blazor Gateway remains unchanged and must not acquire test
endpoints or browser automation. Phase 0 depends on the existing work to unify
the general WASM dev server with the Gateway in
[dotnet/runtime#122144](https://github.com/dotnet/runtime/issues/122144) and
[dotnet/aspnetcore#67814](https://github.com/dotnet/aspnetcore/issues/67814).
