# `dotnet test` for Blazor WebAssembly

## Status

Proposed.

This document is a discussion draft for
[dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091). Names of new
packages, properties, options, and protocol fields are placeholders until the
owning teams agree on them.

## Decision status

The following constraints are already established:

- The Blazor Gateway is a production component, comparable to YARP. It remains
  unaware of MTP, browser automation, test completion, and artifact collection.
- Browser-hosted MTP uses the existing authenticated HTTP `dotnettestcli`
  transport for live messages.
- Per-run credentials do not belong in URLs, ordinary process arguments,
  static assets, launch profiles, or logs.

Everything else in this document is a proposal for discussion. In particular,
the SDK-owned browser launcher, Playwright, browser CLI surface, dedicated
test-project shape, artifact-content protocol, and HTTP control channel remain
open decisions.

## Summary

`dotnet test` should be able to build a Blazor WebAssembly test project, start
its local server, launch a selected browser, run Microsoft.Testing.Platform
(MTP) inside that browser, stream results to the SDK, export test artifacts,
and close every process it started.

The proposed design is:

1. Treat the browser as the execution device and reuse the existing
   `ComputeAvailableDevices`, `DeployToDevice`, and `ComputeRunArguments`
   orchestration.
2. Add an SDK-owned browser test launcher. It starts the existing production
   Blazor Gateway as a child process and owns the browser lifecycle.
3. Keep the existing authenticated HTTP `dotnettestcli` transport for live
   MTP messages.
4. Inject the HTTP bootstrap into the page through an origin-validated browser
   binding. Do not place the endpoint or bearer token in a URL, process
   argument, static asset, or launch profile.
5. Extend the versioned MTP protocol to transfer artifact bytes from the
   browser virtual file system to the SDK.
6. Add a second authenticated HTTP long-poll endpoint for SDK-to-browser
   cancellation, mirroring the existing named-pipe control channel.

The proposed first project shape is a dedicated Blazor WebAssembly test
application that references a Razor class library containing the components
under test. Whether the first release must also run tests embedded in an
arbitrary production Blazor application remains open.

## Existing foundation

The following pieces already exist:

- MTP and MSTest can execute on single-threaded `browser-wasm`. The
  [BrowserPlayground sample](https://github.com/microsoft/testfx/tree/main/samples/BrowserPlayground)
  demonstrates both Node-hosted and browser-hosted execution.
- MTP supports the `dotnettestcli` binary protocol over authenticated HTTP.
  See [microsoft/testfx#10143](https://github.com/microsoft/testfx/pull/10143).
- TRX generation works on single-threaded WebAssembly.
  See [microsoft/testfx#10324](https://github.com/microsoft/testfx/pull/10324).
- The SDK hosts an authenticated loopback HTTP endpoint for each browser or
  WASI test module and passes its endpoint and token through an owner-only
  response file. See [dotnet/sdk#55672](https://github.com/dotnet/sdk/pull/55672)
  and [`TestApplication.cs`](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs).
- `dotnet test` already evaluates `ComputeAvailableDevices`, deploys when
  necessary, and uses `ComputeRunArguments` per project and target framework.
  See [`dotnet-run-for-maui.md`](dotnet-run-for-maui.md).
- The Blazor Gateway already serves static web assets, exposes health checks,
  and supports per-application configuration endpoints. See
  [`BlazorGateway.cs`](https://github.com/dotnet/aspnetcore/blob/c580e61aabc662528d6b6527012fc77609f108db/src/Components/Gateway/src/BlazorGateway.cs)
  and its
  [MSBuild targets](https://github.com/dotnet/aspnetcore/blob/c580e61aabc662528d6b6527012fc77609f108db/src/Components/Gateway/src/build/Microsoft.AspNetCore.Components.Gateway.targets).
- The Blazor team has clarified that the Gateway is a production component
  and should not contain test infrastructure. See the
  [discussion on dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091#issuecomment-5395458076).

The current gap is orchestration. For a Blazor WebAssembly test project,
`dotnet test` starts the Blazor Gateway and appends the HTTP response file to
its arguments. The Gateway correctly does only its production job: serving the
application. A separate test launcher must consume the test bootstrap, launch
a browser, supply those values to MTP in the page, and own cleanup. The current
probe documents this behavior in
[`BlazorWasmTestApp/README.md`](../../test/TestAssets/TestProjects/BlazorWasmTestApp/README.md).

## Goals

- `dotnet test` runs tests in a real browser-hosted `browser-wasm` runtime.
- `--list-tests`, extension help, filters, retry, live output, and per-test
  results continue to use the normal MTP protocol.
- The user can select a browser and headed or headless execution.
- Browser discovery and selection work consistently on Windows, macOS, and
  Linux.
- The SDK receives physical TRX files and other requested artifacts in its
  normal results directory.
- Ctrl+C, `--timeout`, `--maximum-failed-tests`, startup failure, browser
  failure, and SDK termination clean up the browser, temporary profile,
  Gateway, HTTP listeners, and response files.
- Parallel projects and target frameworks use isolated ports, browser
  contexts, profiles, tokens, and artifact transfers.
- The MTP bearer token never appears in command-line values, URLs, static
  files, browser history, launch settings, or normal logs.
- The design remains extensible to non-Blazor browser test hosts.

## Non-goals

- VSTest support. This design applies to MTP mode.
- Managed debugging inside the browser.
- Installing browsers as part of `dotnet test`.
- Supporting remote browsers in the first version.
- Reusing an already-running personal browser profile.
- Automatically turning every existing Blazor application into a test host.
- Defining how component tests should construct application services or test
  fixtures. The test framework and test project own that behavior.

## Proposed user experience

### Project shape

The initial experience uses a dedicated Blazor WebAssembly test project. It
contains or references the tests and opts into the browser test host:

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    <BlazorWebAssemblyTestHost>true</BlazorWebAssemblyTestHost>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="&lt;version&gt;" />
    <ProjectReference Include="..\MyApp.Components\MyApp.Components.csproj" />
  </ItemGroup>
</Project>
```

`BlazorWebAssemblyTestHost` is a proposed name. The implementation may instead
use a project capability supplied by a testing package or template.
The final template also references whichever package supplies the test runner
component and Blazor test-host targets; that package name is intentionally not
invented in this draft.

The application still starts the normal Blazor renderer. A test package adds
a root component or startup service that begins MTP after the first render,
registers the test assemblies, and reports completion through the launcher
binding. The exact renderer-ready hook is a Blazor-owned contract to define;
the launcher must not assume that invoking `Program.Main` returns when tests
finish.

A future template could create this shape:

```dotnetcli
dotnet new blazorwasmtest -o MyApp.BrowserTests
```

### Commands

The following CLI uses the existing device surface for illustration. Whether
the final UX uses `--device` or browser-specific aliases is an open question.
The default command discovers available local browsers, selects one, and runs
the tests:

```dotnetcli
dotnet test --project MyApp.BrowserTests
```

Browser discovery is visible through the existing command:

```dotnetcli
dotnet test --project MyApp.BrowserTests --list-devices
```

Example output:

```text
Available devices:

  1. chrome - Google Chrome (Browser, Available)
  2. edge - Microsoft Edge (Browser, Available)
```

Selection reuses `--device`:

```dotnetcli
dotnet test --project MyApp.BrowserTests --device chrome
```

A new workload-neutral `--headless` option is proposed:

```dotnetcli
dotnet test --project MyApp.BrowserTests --device chrome --headless
```

`--headless` is forwarded as an MSBuild property to device discovery,
deployment, and run-argument computation. Projects without a headless-capable
device report that the selected device does not support it.

An explicit executable path remains an advanced MSBuild override rather than
a new SDK-wide browser option:

```dotnetcli
dotnet test --project MyApp.BrowserTests \
  -p:BlazorTestBrowserExecutable=/path/to/chrome --headless
```

Whether `--browser`/`--list-browsers` aliases would be clearer than the
generic device terminology is an open UX question.

All arguments after `--` remain MTP/test-application arguments:

```dotnetcli
dotnet test --project MyApp.BrowserTests --device chrome --headless -- \
  --treenode-filter "/*/*/*/CounterTests/*"
```

### Defaults

- One discovered browser is selected automatically.
- Multiple browsers prompt in an interactive terminal.
- Multiple browsers in a non-interactive environment fail with the available
  choices and an example using `--device`.
- Headed mode is proposed as the default for interactive use.
- The non-interactive default is open. CI can always pass `--headless`
  explicitly; the SDK should not infer a particular CI provider.
- Each invocation uses a new temporary browser profile. The user's normal
  profile is never read or modified.

## Components and ownership

| Concern | Proposed owner |
| --- | --- |
| Project evaluation, device selection, HTTP gateway, browser test launcher, browser process/context lifetime, result presentation, output paths, cancellation policy | `dotnet/sdk` |
| Production Gateway executable, static assets, and a stable process-launch contract | `dotnet/aspnetcore` |
| Browser-capable MTP client, artifact-content protocol, HTTP control client | `microsoft/testfx` |
| WebAssembly runtime argument APIs and JavaScript interop primitives | `dotnet/runtime` |
| Renderer-ready hook and Blazor test project/template integration | `dotnet/aspnetcore`, with the selected test framework integration |
| Browser boot module and bootstrap/completion binding contract | Proposed `dotnet/sdk` launcher package plus `microsoft/testfx` |

### An SDK-owned browser test launcher

The Blazor Gateway remains a production server used by `dotnet run`,
deployment, and Aspire. Browser automation and MTP bootstrap handling live in
an SDK-owned launcher, tentatively:

```text
dotnet-test-browser-host
```

The launcher starts `blazor-gateway.dll` as a child process using the
`RunCommand` and `RunArguments` already produced by the Blazor targets. It does
not reference the Gateway assembly or require a public Gateway hosting API.
It additionally owns:

- browser discovery and launch;
- a temporary browser profile/context;
- in-memory page bootstrap;
- startup, completion, and failure coordination;
- graceful browser and Gateway shutdown.

Keeping this in the SDK prevents a browser-driver dependency and test-only
surface from entering the production Gateway package or Blazor component.

## Generic SDK launch contract

The SDK does not identify Blazor by inspecting the command name. Projects
describe their test launch contract through MSBuild properties produced by
`ComputeRunArguments`. The existing `RunCommand`, `RunArguments`, and
`RunWorkingDirectory` remain the production server launch contract.

Proposed properties:

| Property | Meaning |
| --- | --- |
| `DotnetTestTransport` | `pipe` or `http`; removes the permanent need to infer transport from the RID. |
| `DotnetTestHostKind` | Diagnostic/capability value such as `process`, `browser`, or `device`. |

`dotnet test` reads these values after `ComputeRunArguments`. Existing projects
remain unchanged because all values are optional.

The Blazor testing targets provide:

```xml
<DotnetTestTransport>http</DotnetTestTransport>
<DotnetTestHostKind>browser</DotnetTestHostKind>
```

For `browser`, the SDK starts its launcher instead of starting `RunCommand`
directly. The launcher receives an owner-only configuration file containing:

```text
server command and arguments
selected browser and headless mode
SDK shutdown deadline
HTTP bootstrap response-file path
```

The SDK passes MTP arguments after a `--` sentinel. The launcher parses only
its configuration before `--`, treats everything after it as opaque MTP input,
expands exactly the bootstrap file named by its configuration, and supplies
the resulting array to the browser. User arguments beginning with `@` are not
expanded by the launcher.

`--test-modules` bypasses project evaluation and therefore cannot discover a
browser launcher. Combining it with a `browser-wasm` module is an explicit
error in the first version.

## Browser discovery and launch

### Discovery

Blazor testing targets implement `ComputeAvailableDevices` and return
`@(Devices)` with:

| Metadata | Example |
| --- | --- |
| Identity | `chrome` |
| `Description` | `Google Chrome 140.0` |
| `Type` | `Browser` |
| `Status` | `Available` |
| `RuntimeIdentifier` | `browser-wasm` |
| `ExecutablePath` | Host-specific absolute path, consumed by the Blazor targets rather than printed by default |
| `SupportsHeadless` | `true` |

Discovery uses platform registration first and conventional installation
paths second. An explicit `BlazorTestBrowserExecutable` bypasses discovery.
The SDK's current `DeviceItem` reads only identity, description, type, status,
and runtime identifier. Supporting SDK-side validation and display of
`SupportsHeadless` requires extending that generic device contract; otherwise
the Blazor `ComputeRunArguments` target must validate the selected browser.

### Driver

The browser must be owned by the SDK launcher. Shell-opening a URL, as
`dotnet watch` does in
[`BrowserLauncher.cs`](../../src/Dotnet.Watch/Watch/Browser/BrowserLauncher.cs),
is insufficient because it cannot reliably:

- create an isolated profile;
- inject secrets before application scripts run;
- select headless mode;
- report navigation failures;
- close only the browser instance created for the test.

The recommended first implementation is a Playwright-backed driver in the
separate SDK launcher:

- use an installed Chrome or Edge channel, or an explicit executable path;
- do not download a browser during `dotnet test`;
- create an isolated browser context;
- expose origin-validated bootstrap and completion bindings;
- close the context and browser deterministically.

The driver and browser must remain attached to a process group or Windows job
object owned by the launcher. They must not detach into an existing
browser instance. This preserves the SDK's second-Ctrl+C process-tree kill as a
last-resort cleanup path.

If Firefox is included in the first release, its installed version must be
compatible with the selected browser driver. Otherwise discovery is limited to
the supported Chromium browsers.

A custom browser-process/CDP implementation would reduce the package
dependency but would recreate launch, protocol, compatibility, and cleanup
logic. Playwright also ships a prebuilt Node driver, which affects
source-build/VMR requirements, and a serviced SDK may need to work with newer
evergreen browser versions than its pinned driver was tested against. The
driver should therefore ship in the separate tool package, not the base
Gateway package, and must be evaluated for offline source-build and servicing
compatibility.

## Secure bootstrap

### Requirements

The existing owner-only response file contains:

```text
--server dotnettestcli
--dotnet-test-transport http
--dotnet-test-http-endpoint <loopback endpoint>
--dotnet-test-http-token <bearer token>
```

The SDK passes only `@<response-file-path>` to the test-host process. The
endpoint path and token must not be copied into:

- the browser URL or query string;
- browser command-line arguments;
- static JavaScript or JSON;
- `launchSettings.json`;
- environment variables inherited by unrelated child processes;
- information or diagnostic logs.

### Proposed flow

1. The SDK launcher reads the response-file path from its owner-only
   configuration, without logging its contents, and keeps the values in
   memory.
2. It starts the Blazor Gateway child process on an exact loopback origin and
   waits for its liveness endpoint.
3. Before launching the browser, it makes an authenticated registration
   request to the SDK HTTP gateway with the exact Gateway origin. The SDK pins
   CORS to that origin.
4. It creates an isolated browser context and exposes a
   `__getDotnetTestBootstrap` binding.
5. The binding returns bootstrap data only when the caller is the top-level
   page at the registered Gateway origin. Calls from child frames or a
   different origin fail.
6. The test boot module calls the binding once, then calls:

   ```javascript
   dotnet.withApplicationArguments(...bootstrap.arguments)
   ```

7. The binding invalidates its stored bootstrap after the first successful
   read.
8. MTP creates its HTTP client and connects directly to the SDK HTTP gateway.

The origin registration is a small SDK-owned endpoint adjacent to the binary
protocol endpoint:

```http
POST <per-run-endpoint>/origin
Authorization: Bearer <per-run-token>
Content-Type: application/json

{"origin":"http://127.0.0.1:<gateway-port>"}
```

`HttpTestHostGateway` must explicitly route and allow this subpath; the current
gateway accepts only the exact binary-protocol path and `POST`. Registration
succeeds only before the first protocol frame. An authenticated registration
overrides an origin tentatively selected by a preflight; registration with a
different origin or after the handshake fails.

For compatibility, a browser host without the new launcher may continue to
pin its origin through the first valid CORS preflight. The SDK launcher always
registers explicitly. The driver navigates to the byte-identical origin it
registered (`127.0.0.1` and `localhost` are not interchangeable).

The token remains observable to the test page's own code and browser
developer tools because that code must send it in the Authorization header.
It is also part of the MTP application arguments in the current API. The SDK
therefore redacts the token from captured stdout/stderr and every inbound
protocol string before rendering or tracing it, in addition to its existing
process-argument redaction. The security goal is to avoid disclosure outside
the isolated local test context, not to hide a credential from the code that
uses it.

An unauthenticated Gateway configuration endpoint is not sufficient. Any local
process that finds the port could retrieve the MTP bearer token. A one-time
URL nonce has the same disclosure problems as putting the bearer token in the
URL. If a browser-driver dependency is rejected, the alternative needs an
equivalent authenticated, out-of-band handoff and an explicit threat-model
review.

The browser context rejects top-level navigation away from the registered
Gateway origin while the run is active. Subresource requests required by the
tests may be allowed, but they never receive the bootstrap binding's return
value.

## Blazor test runner and completion

The standard Blazor application starts normally. The test package supplies a
root component or startup service that begins after the renderer is ready:

1. Read MTP arguments through `__getDotnetTestBootstrap`.
2. Build MTP with those arguments and explicitly register the test assemblies.
3. Run tests while the Blazor renderer and browser event loop remain active.
4. Invoke `__dotnetTestComplete(exitCode)` when MTP and artifact transfers
   finish.

This follows the current
[`BlazorWasmTestApp`](../../test/TestAssets/TestProjects/BlazorWasmTestApp/)
probe, which starts MTP from a rendered component rather than replacing the
Blazor entry point.

The browser driver exposes both bindings only to its isolated context and
validates that completion comes from the same top-level Gateway origin.
`__dotnetTestComplete` completes a task in the SDK launcher; it is not a
public HTTP shutdown endpoint.

Completion ordering is:

1. MTP publishes `TestSessionEnd` and waits for its acknowledgement.
2. MTP flushes registered artifacts, including any negotiated content
   transfers, and waits for their acknowledgements.
3. The runner component obtains the final MTP exit code.
4. The runner invokes the completion binding.
5. The SDK launcher closes the browser context.
6. The SDK launcher stops the Blazor Gateway child process.
7. The launcher exits with the MTP exit code.
8. The SDK observes process exit and renders the normal summary.

If the binding disappears or navigation crashes, the browser driver reports a
host failure and the process exits non-zero with captured browser diagnostics.

## Live results

The browser MTP application connects directly to the SDK's existing
`HttpTestHostGateway`. The Gateway and SDK launcher do not proxy or interpret
test results.

The existing protocol continues to carry:

- handshake and execution mode;
- discovery;
- per-test results and output;
- session start/end;
- artifact metadata;
- MTP display and Azure DevOps messages.

This preserves `--list-tests`, help, retry, output rendering, and future MTP
features without a Blazor-specific result protocol.

All browser-side transport and artifact code must remain valid on
single-threaded WebAssembly:

- use asynchronous browser `fetch`/promise continuations;
- do not create dedicated threads or use blocking waits;
- keep only one primary protocol request in flight;
- bound each artifact chunk so the SDK's currently buffered HTTP frame does
  not require loading a complete artifact into memory.

## Host and browser path translation

The SDK currently appends host filesystem paths for `--results-directory`,
`--config-file`, and `--diagnostic-output-directory`. Those paths are not
usable inside the browser virtual file system.

For `DotnetTestHostKind=browser` in the first version:

- The SDK retains the host results and diagnostic directories but passes
  generated VFS paths such as `/tmp/dotnet-test/<execution-id>/results` to
  MTP.
- Artifact-content transfer maps files under those VFS roots back to the
  retained host directories.
- `--config-file` fails before launch with an actionable unsupported-option
  diagnostic. A later version can add an MTP in-memory configuration contract.
- `--diagnostic-output-directory` fails before launch until MTP can report its
  diagnostic logs as transferable artifacts.
- Arbitrary user arguments after `--` are not path-translated.

The host path is never exposed to the browser as an apparent writable
location, and the SDK never prints a browser VFS path as if it were a
persisted result.

## Artifact transfer

### Problem

MTP can create TRX and other files on `browser-wasm`, and
`FileArtifactMessages` tells the SDK their paths. Those paths refer to the
browser's in-memory virtual file system. The SDK process cannot open them, and
they disappear when the page closes.

### Proposed protocol extension

Add a negotiated, chunked artifact-content transfer to the existing
`dotnettestcli` protocol. It should not be a Blazor Gateway upload API because
artifact production is an MTP concern and the SDK owns results-directory
layout and post-processing.

Proposed messages:

```text
FileArtifactTransferStart
  TransferId
  ExecutionId
  InstanceId
  SourcePath
  DisplayName
  Description
  TestUid
  TestDisplayName
  SessionUid
  Kind
  Length
  Sha256

FileArtifactTransferChunk
  TransferId
  Sequence
  Bytes (length-prefixed opaque binary)

FileArtifactTransferEnd
  TransferId
```

Behavior:

- The feature is gated by a new negotiated protocol version or handshake
  capability. Independent features use handshake capabilities rather than
  assuming they ship in the same protocol version.
- The shared serializer gains explicit `WriteBytes`/`ReadBytes` primitives;
  binary content is not converted through UTF-8 strings.
- MTP sends metadata, ordered chunks, and an end marker over the existing
  authenticated HTTP request/reply channel.
- Start metadata is a superset of `FileArtifactMessage`. `SourcePath`
  correlates the transfer with any metadata-only message for the same VFS
  file; the successfully materialized host path supersedes that VFS path.
- The SDK defers `ArtifactAdded` output and post-processing registration for a
  browser artifact until its transfer completes.
- Chunks are bounded, for example at 1 MiB, so the browser and SDK do not
  buffer a complete coverage file.
- The SDK chooses the destination beneath the resolved results directory. It
  never trusts a browser-provided absolute path.
- The SDK writes to a temporary file, validates length and SHA-256, then
  atomically moves the file into place.
- After materialization, the SDK records the host path for terminal output and
  artifact post-processing.
- An interrupted or invalid transfer is deleted and reported as an artifact
  failure without silently producing a partial file.
- Size and aggregate-run limits protect the SDK from unbounded browser
  uploads.
- All transfers are acknowledged before the runner reports completion and
  before the launcher exits.

Desktop hosts continue to report shared-filesystem paths and do not send file
contents.

This phase materializes browser files only. Artifact merge/post-processing
remains disabled until a desktop merge host can load the required
post-processor extensions and consume the materialized files. Relaunching the
browser module with host filesystem paths is not a valid merge strategy.

Until this protocol ships, live per-test results work but `--report-trx` must
warn that the physical file cannot be exported. The command must not claim a
host-side TRX path that does not exist.

## Cancellation and timeouts

### Problem

The HTTP primary channel is host-initiated request/reply. The SDK cannot push
a cancellation message while the browser is otherwise idle. The named-pipe
path uses a separate reverse control channel, but the SDK intentionally omits
that pipe from an HTTP handshake.

### Proposed HTTP control channel

Add a second authenticated loopback listener per test application. Its
endpoint and token are returned as proposed `ServerControlHttpEndpoint` and
`ServerControlHttpToken` properties in the successful SDK handshake response,
after protocol/capability negotiation. They are not placed unconditionally in
the launch response file, because an older MTP command-line parser would
reject unknown pre-handshake options.

After the handshake, MTP starts an independent long-poll request carrying the
existing `WaitForServerControlRequest`. The control listener:

- keeps the request pending while no control signal exists;
- replies with `ServerControlMessage(CancelSession)` for Ctrl+C, timeout, or
  run-policy truncation;
- accepts a replacement long poll after an unknown control kind;
- treats endpoint disposal as cancellation;
- is a separate listener and request lock from the primary data gateway, so a
  parked control request never blocks test-result messages;
- accepts requests concurrently with the primary listener; and
- completes all pending control responses before disposal joins its listener
  task.
- bounds each long poll with a server-side deadline and a no-op response so a
  page reload, browser crash, or intermediary timeout cannot permanently
  occupy the control listener.

On cancellation:

1. MTP requests cooperative session cancellation.
2. Tests receive their normal cancellation token.
3. MTP flushes final results and artifacts.
4. The page reports completion to the browser driver.
5. If the browser does not finish within the launcher deadline, the SDK
   launcher closes the browser and exits. The launcher deadline is passed
   explicitly and is shorter than the SDK process-kill grace period.
6. A second Ctrl+C retains the current force-kill behavior for the process
   tree.

This reuses the existing control message semantics while changing only its
transport. The SDK must wire the first Ctrl+C to
`RequestSessionCancellation` for HTTP modules; today that method is entered by
test-run policy cancellation, while console-hosted MTP processes normally
receive Ctrl+C themselves. The browser cannot receive that console signal.
The launcher receives the signal, suppresses its first default termination,
and waits for control-channel cancellation and graceful flush. The second
Ctrl+C remains the SDK's force-kill path.

Polling or long-polling on the primary endpoint, sharing the primary
listener's request lock, and a public Gateway shutdown API are rejected
because they block result traffic or expand the exposed surface.

## Server readiness and failure handling

The SDK launcher starts `blazor-gateway.dll` as a child process:

1. Remove inherited URL/HTTPS and OTLP configuration that could alter the
   isolated test server.
2. Choose a loopback port and launch with an exact
   `http://127.0.0.1:<port>` URL.
3. Disable HTTPS redirection, HSTS, telemetry export, and other production
   behaviors that can change the origin or create unrelated traffic.
4. Probe the existing liveness endpoint, retrying the entire launch with a new
   port if binding lost a race.
5. Register that exact origin with the SDK gateway.
6. Navigate the browser only after registration succeeds.

Failures identify their phase—browser discovery, server startup, browser
launch, navigation, bootstrap, MTP handshake/run, artifact export, or
shutdown—without standardizing final diagnostic wording in this proposal.

Browser console errors, page exceptions, and navigation failures are retained
in a bounded diagnostic tail. They are printed automatically on host startup
failure and included in diagnostic logs otherwise.

## Parallelism and isolation

Each `TestApplication` receives:

- a unique SDK HTTP data endpoint and bearer token;
- after Phase 3, a unique HTTP control endpoint and bearer token;
- a unique Blazor Gateway port;
- a unique browser context and temporary profile;
- a unique artifact-transfer namespace;
- a unique execution ID.

The existing `--max-parallel-test-modules` and `TestTfmsInParallel` settings
govern how many browser instances run concurrently. The implementation must
not serialize all Blazor tests globally.

The SDK launcher owns cleanup of its browser context and Gateway child. The
parent SDK command owns cleanup of its HTTP listeners and response file.

## `--no-build`, solutions, and launch profiles

- `--no-build` still starts the Gateway and browser. It skips compilation, not
  deployment or launch.
- Browser discovery happens per project/target framework, like other devices.
- An explicit `--device` remains invalid with `--solution` under the current
  device contract. A browser-specific option could remove that limitation
  because browser IDs are machine-wide; this is part of the CLI UX decision.
- Browser test hosts ignore production `launchSettings.json`, including
  inherited `applicationUrl`, `ASPNETCORE_URLS`, environment variables, and
  command-line arguments. Test execution needs deterministic loopback binding
  and isolated browser settings.
- A future test launch profile may provide non-secret defaults such as browser
  name, headed/headless mode, viewport, or additional browser arguments.
  Tokens and endpoint values are always generated per run.

## Compatibility and fallback

- Projects that do not provide the new test launch properties preserve current
  behavior.
- HTTP remains selected from `browser-*`/`wasi-*` RIDs until projects flow
  `DotnetTestTransport`; the explicit property then takes precedence.
- Older MTP versions that do not support HTTP fail through the existing
  transport/handshake diagnostics.
- Older SDKs ignore the project capability and cannot provide this experience;
  the testing package should report a minimum-SDK diagnostic during build.
- Artifact-content and HTTP-control features are independently negotiated.
  Live results still work when either optional feature is unavailable.
- The stale standalone exit-code approach in
  [dotnet/sdk#55389](https://github.com/dotnet/sdk/pull/55389) is not used for
  Blazor. It may remain useful for non-browser WASI/Node scenarios that do not
  need a live SDK connection.

## Implementation plan

### Phase 1: Test-host lifecycle

**dotnet/aspnetcore**

- Preserve the production-only Gateway boundary and its process-launch
  contract.
- Define the renderer-ready hook used by the Blazor test runner component.
- Add the testing package assets and proposed dedicated test-project template.

**dotnet/sdk**

- Add optional test-specific run properties and explicit transport capability.
- Forward `--headless` through device orchestration.
- Add the browser test launcher, browser discovery/driver abstraction, and
  owner-only launcher configuration.
- Start the project-provided `RunCommand` as the production server child
  without Blazor-specific command-name checks.
- Implement isolated headed/headless launch, readiness, bootstrap/completion
  bindings, bounded diagnostics, process grouping, stale-profile cleanup, and
  deterministic shutdown.
- Define browser-host path translation and reject unsupported host-file
  options.
- Ignore launch profiles for browser test hosts.
- Add authenticated pre-navigation origin registration, origin-aware
  redaction, and compatibility with preflight-pinned direct hosts.

**microsoft/testfx**

- Keep the current HTTP client and browser-safe runtime behavior.
- Document the protected application-argument/bootstrap contract.

Exit criteria:

- `dotnet test`, `--list-tests`, help, filters, and live results pass in a
  headed and headless Chromium browser on all three desktop operating systems.
- No bootstrap secret appears in URLs, process listings, or logs.
- Browser, Gateway, SDK listeners, profile, and response file are cleaned up
  after success and failure.

### Phase 2: Artifact export

**microsoft/testfx**

- Define and emit the negotiated chunked artifact-content messages.

**dotnet/sdk**

- Receive, validate, materialize, and report browser artifacts.
- Keep browser artifact merge/post-processing disabled initially, even after
  materialization. Enabling it requires a separate desktop merge host with the
  relevant post-processor extensions; relaunching the browser module as the
  merge host is not valid.

Exit criteria:

- `--report-trx` creates a physical TRX in the requested SDK results directory.
- Per-test/session attachments and coverage files survive browser shutdown.
- Parallel transfers cannot overwrite one another or escape the results
  directory.

### Phase 3: Graceful cancellation

**microsoft/testfx and dotnet/sdk**

- Define separately negotiated HTTP control handshake properties and the
  long-poll transport.
- Reuse `WaitForServerControlRequest` and `ServerControlMessage`.
- Add graceful cancellation followed by bounded force cleanup.
- Wire first Ctrl+C to the HTTP control path.

Exit criteria:

- Ctrl+C, `--timeout`, and `--maximum-failed-tests` stop browser tests,
  preserve final results/artifacts when possible, and leave no processes
  behind.

### Phase 4: Broader browser and project support

- Decide Firefox/WebKit support and browser installation guidance.
- Decide whether to add `--browser` aliases and solution-wide selection.
- Evaluate an opt-in mode for tests embedded in an existing production Blazor
  application.
- Add template documentation and CI guidance.

## Test plan

### SDK tests

- MSBuild test-launch property fallback and precedence.
- HTTP selection from explicit capability and RID fallback.
- Browser device and headless-property forwarding.
- Response-file permissions, redaction, and cleanup.
- Host/VFS path translation and unsupported host-file diagnostics.
- Launch-profile suppression for browser hosts.
- Token redaction across process output and inbound protocol strings.
- Authenticated origin registration and legacy preflight compatibility.
- HTTP data/control endpoint authentication, CORS/PNA, ordering, cancellation,
  and disposal.
- Artifact binary serialization, correlation, ordering, limits, hashes, path
  sanitization, partial-file cleanup, and materialization.
- Multi-project and multi-targeting isolation.

### Blazor tests

- Browser discovery on Windows, macOS, and Linux.
- Headed and headless launch.
- Server readiness and navigation failures.
- Browser-binding bootstrap with no token in the URL or logs.
- Origin registration, cross-origin frame rejection, and navigation blocking.
- Renderer-ready MTP startup with component rendering still active.
- Completion, browser crash, page crash, timeout, and forced cleanup.
- Static web asset and configuration behavior remains unchanged for normal
  Gateway usage.

### End-to-end tests

- Passing, failing, skipped, filtered, retried, and zero-test runs.
- `--list-tests` in text and JSON modes.
- Extension help.
- Live output and browser console diagnostics.
- TRX and attachment export.
- Ctrl+C and policy cancellation.
- Parallel test projects using the same browser.
- `--no-build`.

At least one lane should run against an installed stable Chrome or Edge
without downloading a browser during the test.

## Open questions for discussion

1. Should the browser launcher ship in `dotnet/sdk` as proposed, or in a
   testfx-owned tool while the SDK only orchestrates it?
2. Is Playwright an acceptable SDK/tool dependency if it uses installed
   browsers and never downloads one during `dotnet test`, given its prebuilt
   Node driver, source-build requirements, and evergreen-browser compatibility?
3. Should browser selection reuse `--device`, or should `dotnet test` expose
   `--browser`, `--browser-path`, and `--list-browsers`?
4. Should headed or headless mode be the default in non-interactive
   environments?
5. Is the first supported project shape a dedicated browser test project, or
   must the first release also support tests embedded in an arbitrary
   production Blazor application?
6. Should artifact bytes extend the MTP protocol as proposed, or should a
   browser-specific host upload API be accepted?
7. What per-artifact and per-run transfer limits are appropriate for TRX,
   coverage, dumps, and attachments?
8. Should HTTP control use a separate token, or reuse the primary token on its
   mandatory separate listener?
9. Which browsers are required for the first supported release?
10. Should the generic `@(Devices)`/`DeviceItem` contract expose capabilities
    such as `SupportsHeadless`, or should each project's
    `ComputeRunArguments` target validate them?
11. Can the Playwright driver/browser process hierarchy be guaranteed to stay
    in the SDK-launched process tree on every platform, or does the test-host
    tool need an additional process-group/job-object cleanup mechanism?
12. Should browser modules have a lower default concurrency than ordinary test
    processes, or is `--max-parallel-test-modules` sufficient?
13. Is RID-based HTTP transport selection a permanent compatibility fallback
    or a transitional path toward the explicit `DotnetTestTransport`
    property?

## Rejected alternatives

### Put MTP arguments in the page query string

Rejected because the bearer token would appear in browser history, developer
tools, URL logs, and potentially proxy logs.

### Serve the bearer token from the existing unauthenticated configuration endpoint

Rejected because another local process could retrieve it and impersonate the
test host.

### Open the system default browser through shell association

Rejected as the only implementation because it cannot guarantee isolation,
headless operation, in-memory injection, or deterministic cleanup.

### Use only the process exit code

Rejected for Blazor because authenticated HTTP already supports live
discovery, results, output, and help. Exit-code-only execution loses important
MTP behavior and does not solve browser launch or cleanup.

### Upload artifacts through the general Blazor Gateway

Rejected as the default design because artifact production, results-directory
layout, and post-processing belong to MTP and the SDK. A protocol extension
also benefits other sandboxed test hosts.

### Put HTTP control on the primary listener

Rejected because a parked long-poll request would block the primary gateway's
single request pipeline and prevent test results from flowing.
