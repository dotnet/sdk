# `dotnet test` for browser WebAssembly

## Status

Proposed.

This document is a discussion draft for
[dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091). It has been
updated from a working cross-repository prototype. The prototype validates the
ownership and launch boundaries described as **current prototype** below; it
does not make the package, properties, JavaScript API, or support policy a
committed product contract.

## Decision summary

Browser-hosted Microsoft.Testing.Platform (MTP) support should be an optional
testing package rather than a new browser stack in the .NET SDK:

- `Microsoft.Testing.Platform.Browser`, implemented in
  [microsoft/testfx](https://github.com/microsoft/testfx), owns the
  test-specific MSBuild targets, default browser host assets, Playwright
  launcher, browser diagnostics, and launcher cleanup.
- The package composes with the project's existing WebAssembly host. It first
  lets the project evaluate its normal
  [`ComputeRunArguments`](../../src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets#L1462-L1485),
  captures `RunCommand`, `RunArguments`, and `RunWorkingDirectory`, and then
  replaces the outer run command with its launcher.
- MTP running in the browser connects directly to the authenticated SDK
  [`HttpTestHostGateway`](../../src/Cli/dotnet/Commands/Test/MTP/IPC/HttpTestHostGateway.cs).
  Playwright and the WebAssembly application host do not proxy or interpret
  discovery or test results.
- Framework-owned pages integrate through
  `globalThis.testingPlatformBrowser`, contract version 1, with
  `getArguments()` and `complete(exitCode)`.
- A framework can disable the package-generated `index.html` and JavaScript
  supervisor and provide its own page while retaining the same launcher.
- The existing WebAssembly host, including the Blazor Gateway, remains
  test-agnostic. No MTP, configuration, artifact, cancellation, or browser
  automation route is added to it.

This boundary keeps browser and Playwright servicing in TestFX, preserves the
SDK's existing test protocol and result presentation, and avoids another
Static Web Assets server.

## Validated prototype and proposed product

The distinction between implemented prototype behavior and future
productization is important.

### Current prototype

The prototype proves that:

1. An ordinary `PackageReference` to
   `Microsoft.Testing.Platform.Browser` can opt a `browser-wasm` MTP
   application into browser launch.
2. Build-transitive TestFX targets can wrap the host produced by the evaluated
   project without changing the host or requiring the SDK to identify a
   particular server executable.
3. A package-owned page can start the runtime, pass MTP arguments into
   `runMain`, and report the managed exit code.
4. A Blazor-owned page can disable the generated assets and use the same
   versioned page API.
5. `dotnet test`, test discovery, filtering, live output, and results can flow
   directly between browser MTP and the SDK HTTP gateway.
6. The launcher can start the unchanged packaged Blazor Gateway, observe its
   current console readiness message, and leave all test behavior outside the
   Gateway.
7. The package can capture bounded browser console, page-error,
   request-failure, crash, host-output, and cleanup diagnostics.

The locally packed prototype reviewed on September 14, 2026 is 205,268,810
bytes (about 196 MiB or 205 MB). It deliberately makes
`Microsoft.Playwright` a private dependency and packs Playwright's
platform-specific Node drivers and JavaScript payloads into the package. It
does not include browser binaries.

### Proposed product

Before productization, the teams still need to decide:

- how the optional package is published, versioned, serviced, and represented
  in source-build and the VMR;
- whether the cross-platform Playwright payload is split or otherwise reduced;
- the installed-browser support policy and browser-selection experience;
- the stable generic host readiness contract;
- which launcher options deserve first-class `dotnet test` options rather than
  MSBuild properties;
- the preview scope for artifacts and cooperative cancellation.

The prototype property and contract names are therefore documented here to
make the implementation reviewable, not to reserve them permanently.

## Scope

The package is framework-neutral and applies only to MTP applications whose
runtime identifier starts with `browser-`. The initial scenario is
`browser-wasm`.

It supports both:

- a general WebAssembly test application using the package's default page and
  JavaScript supervisor; and
- a UI framework application that owns its page and explicitly composes the
  browser testing API.

WASI is a different host model and is out of scope. VSTest mode, host-side
Playwright UI/E2E tests, dynamic assembly loading from the browser virtual file
system, managed browser debugging, and browser process reuse are also out of
scope for the initial design.

## Existing foundation

- The SDK already selects authenticated HTTP for `browser-*` and `wasi-*`
  MTP test modules, starts a loopback endpoint, and writes the endpoint and
  bearer token to an owner-only response file. See
  [`TestApplication.AppendTestHostTransportArguments`](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs#L409-L432),
  [`CreateHttpTransportResponseFile`](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs#L436-L500),
  and [dotnet/sdk#55672](https://github.com/dotnet/sdk/pull/55672).
- The SDK gateway implements the existing `dotnettestcli` request/reply
  protocol, authentication, CORS origin pinning, and Private Network Access
  preflight handling. The browser package consumes that contract without
  changing it. See
  [`HttpTestHostGateway`](../../src/Cli/dotnet/Commands/Test/MTP/IPC/HttpTestHostGateway.cs).
- MTP supports the `dotnettestcli` protocol over authenticated HTTP. See
  [microsoft/testfx#10143](https://github.com/microsoft/testfx/pull/10143).
- TRX generation can run in the single-threaded browser WebAssembly virtual
  file system. See
  [microsoft/testfx#10324](https://github.com/microsoft/testfx/pull/10324).
  Persisting those files to the host is separate work.
- Runtime and ASP.NET are discussing broader WebAssembly host convergence.
  That work can provide a better shared readiness contract, but it is no
  longer a prerequisite for browser testing because the package wraps the host
  selected by each project. See
  [dotnet/runtime#122144](https://github.com/dotnet/runtime/issues/122144) and
  [dotnet/aspnetcore#67814](https://github.com/dotnet/aspnetcore/issues/67814).

## Project shape

An illustrative general WebAssembly test project is:

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net11.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <EnableMSTestRunner>true</EnableMSTestRunner>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="&lt;version&gt;" />
    <PackageReference Include="Microsoft.Testing.Platform.Browser"
                      Version="&lt;preview-version&gt;" />
  </ItemGroup>
</Project>
```

The package defaults to enabled only for `browser-*` runtime identifiers.
Referencing it does not alter an ordinary desktop test application.

The prototype is driven through existing commands:

```dotnetcli
dotnet test --project MyTests.Browser
dotnet test --project MyTests.Browser --list-tests
dotnet test --project MyTests.Browser --filter FullyQualifiedName~BrowserTests
```

Browser executable selection and additional Chromium arguments are currently
MSBuild properties. A future CLI design may add browser-specific options, but
this proposal does not claim names or behavior for options that do not yet
exist.

## Components and ownership

| Concern | Owner |
| --- | --- |
| Browser testing package, test-specific targets, generated default page/supervisor, Playwright launcher, browser discovery, diagnostics, and launcher cleanup | `microsoft/testfx` |
| Project evaluation, `ComputeRunArguments`, MTP invocation, authenticated HTTP gateway, result handling, terminal presentation, and SDK result directories | `dotnet/sdk` |
| Application host, Static Web Assets behavior, and framework-owned page lifecycle | The selected WebAssembly SDK/framework host |
| Browser runtime and documented runtime JavaScript APIs | `dotnet/runtime` |

The package must not duplicate SDK protocol handling, Static Web Assets
serving, or framework startup. Conversely, the SDK must not acquire a
Playwright dependency merely because it invokes the package-provided launcher.

## Evaluated host wrapping

The package integrates after `ComputeRunArguments`:

1. The project's SDK and framework targets compute their normal `RunCommand`,
   `RunArguments`, and `RunWorkingDirectory`.
2. The browser package captures all three values, preserving empty and quoted
   arguments.
3. The package replaces the outer run command with its .NET launcher.
4. The launcher starts the captured host, waits for its origin, launches an
   isolated headless Chromium-family browser, and navigates to the configured
   path.

Explicit package properties may override the captured host command,
arguments, or working directory for hosts that do not participate in the
normal run protocol.

This is intentionally host-agnostic. The launcher does not special-case
`blazor-gateway.dll`, a dev-server assembly name, or a launch profile. The
Blazor proof of concept starts the same packaged Gateway that `dotnet run`
selected and adds no test routes or middleware.

## Host readiness

A machine-readable launch-info contract is preferable to localized console
parsing. The prototype lets the launcher choose a new temporary path and
passes it to the child host through
`TESTINGPLATFORM_BROWSER_LAUNCH_INFO_FILE`. A participating host atomically
creates the owner-only file:

```json
{
  "version": 1,
  "url": "http://127.0.0.1:54321/"
}
```

Only a loopback HTTP or HTTPS URL is accepted.

The current Blazor Gateway does not implement this contract. For compatibility,
the launcher also recognizes the existing ASP.NET Core
`Now listening on: <URL>` message from captured output. This fallback is part
of the current compatibility story; the RFC must not claim that browser
testing waits exclusively on launch-info or that host unification blocks a
preview.

Productization should define a generic launch-info contract usable by
`dotnet run`, browser testing, tools, and multiple WebAssembly hosts. It should
remain host information, not a test-specific Gateway feature. The console
fallback can be retired only after the supported host matrix reliably
implements that contract.

## Browser page API

Before navigation, the launcher installs this API on the top-level page at the
expected loopback origin:

```js
globalThis.testingPlatformBrowser = {
    contractVersion: 1,
    getArguments(): string[],
    complete(exitCode: number): void
};
```

The contract is:

- `contractVersion` is `1`. A framework-owned page rejects versions it does
  not support.
- `getArguments()` returns a new frozen array containing the MTP application
  arguments prepared by `dotnet test`, including the SDK HTTP response-file
  contents expanded by the launcher. The page passes them to the managed test
  application and must not log, persist, or put them in a URL.
- `complete(exitCode)` reports the final managed application exit code to the
  launcher. It accepts an integer, is one-shot, and does not carry discovery,
  test results, or artifacts.

The underlying Playwright binding is private launcher transport, not a public
page API.

### Package-owned page

By default the package supplies `index.html` and a JavaScript supervisor. The
supervisor validates contract version 1, calls `getArguments()`, starts the
runtime with those arguments, runs the managed entry point, reports errors to
the browser console, and calls `complete(exitCode)`.

### Framework-owned page

A framework sets:

```xml
<TestingPlatformBrowserGenerateHostAssets>
  false
</TestingPlatformBrowserGenerateHostAssets>
```

and provides its own page and startup code. Its adapter follows the same
contract:

```js
const api = globalThis.testingPlatformBrowser;
if (api?.contractVersion !== 1) {
    throw new Error('testingPlatformBrowser contract version 1 is required.');
}

let exitCode;
let failure;
try {
    const { runMain } = await dotnet
        .withApplicationArguments(...api.getArguments())
        .create();
    exitCode = await runMain();
}
catch (error) {
    failure = error;
    exitCode = 1;
    console.error(error instanceof Error ? error.stack ?? error.message : String(error));
}

api.complete(exitCode);
if (failure !== undefined) {
    throw failure;
}
```

The framework continues to own page rendering, its boot script, and Static Web
Assets. The browser package owns only the test bootstrap and completion
boundary.

## MTP result path

The browser MTP application connects directly to the SDK's existing
`HttpTestHostGateway`:

```text
dotnet test
  |
  | owner-only response file: endpoint + bearer token
  v
Microsoft.Testing.Platform.Browser launcher
  |                                  |
  | starts evaluated host            | launches browser and injects arguments
  v                                  v
WebAssembly application host ---> browser page ---> MTP
                                                   |
                                                   | authenticated dotnettestcli HTTP
                                                   v
                                          SDK HttpTestHostGateway
```

The application host serves the application and its Static Web Assets. The SDK
gateway serves the MTP protocol. The Playwright launcher controls browser
lifetime and observes diagnostics. None is a result proxy for another.

The SDK gateway remains the authority for authentication, protocol
serialization, result handling, and display. The package does not replace the
current HTTP protocol with WebSocket and does not require new test endpoints
in the application host.

## Secure bootstrap

The SDK writes its HTTP endpoint and bearer token to an owner-only response
file. The endpoint and token are not direct process arguments; the
response-file path is included with the other test application arguments. The
browser launcher:

1. expands the response file in its own process;
2. validates that the endpoint is loopback HTTP and that a bearer token is
   present;
3. retains the token in memory;
4. injects the MTP arguments through Playwright before application startup;
5. redacts the token and protocol endpoint path from launcher, host, and
   browser diagnostics.

The token is not placed in the browser URL, static assets, or an
unauthenticated remote-debugging port. Code in the top-level test page can
obtain the arguments because it must start MTP, so the design narrows rather
than eliminates that trust boundary.

The SDK gateway's existing CORS behavior pins the first accepted browser
origin. The prototype does not add a pre-navigation origin-registration
operation or any other gateway API.

The package rejects user browser arguments that could replace Playwright's
private debugging transport or isolated user-data directory. Productization
still requires a dedicated threat model and security review of response-file
handling, Playwright transport, origin checks, diagnostics redaction, and
process ownership.

## Browser and Playwright support

The prototype launches an installed Chromium-family browser in headless mode.
It discovers common Edge, Chrome, and Chromium locations or accepts an
explicit executable. It does not download a browser during `dotnet test`.

Playwright provides:

- browser launch through its private transport;
- an isolated non-persistent browser context;
- navigation and top-level-page bindings;
- console, page-error, failed-request, crash, and disconnect diagnostics;
- deterministic browser/context closure.

Playwright does not provide the MTP result protocol, host Static Web Assets,
managed artifact export, or cooperative cancellation of blocked managed code.

The current package self-contains private Playwright runtime files for Windows
x64, Linux x64/Arm64, and macOS x64/Arm64. This explains its approximately
200 MB prototype size and creates material source-build, offline,
supply-chain, platform-signing, servicing, and package-acquisition work.
Possible solutions include RID-specific packages or acquisition, but this RFC
does not select one.

Firefox and WebKit are not part of the prototype. Playwright's patched Firefox
and WebKit builds must not be presented as installed branded Firefox or Safari.

## Failure model and cleanup

The launcher remains outside the WebAssembly runtime, so it can distinguish:

- a managed test result reported through MTP and `complete`;
- a host process that exits before readiness;
- browser console or page errors;
- a browser crash or disconnect;
- a test completion timeout;
- launcher or cleanup failure.

It captures bounded diagnostics, owns the host process tree, and controls the
Playwright browser lifetime. Disposal closes the Playwright binding, context,
and browser and kills the host process tree when necessary.

A synchronous infinite loop on the single browser WebAssembly thread cannot
observe managed cancellation or flush final results. The outer launcher can
still enforce a deadline and terminate the owned processes.

## Artifacts

Host persistence of browser virtual-file-system artifacts is not implemented
by the prototype. A TRX extension may produce a file inside the browser VFS,
but the SDK must not present that browser path as a persisted host artifact.

An artifact sink that transfers bytes and metadata directly from MTP to an
SDK-owned destination remains desirable. It should be capability-negotiated,
bounded, authenticated, and designed in TestFX with the SDK result
materialization contract. Existing file-only producers could then open their
VFS file and copy it through the same sink.

Artifact export is not automatically a preview blocker. It becomes a blocker
only if the agreed preview contract promises physical TRX files, attachments,
diagnostic files, or another artifact that cannot be delivered. A preview may
instead document those outputs as unsupported while retaining live discovery,
output, and results.

## Cancellation

Cooperative browser cancellation is not implemented by the prototype. Today,
the SDK can eventually force-kill the launcher process tree, and the launcher
performs browser and host cleanup when it receives a cancellation signal and
can still run its disposal path. Neither path can ask a yielding MTP
application in the page to flush a graceful final summary.

A future design may use SDK-to-launcher local control followed by Playwright
evaluation of a versioned page/MTP cancellation hook. It does not require a
second browser result transport. Regardless of that design, forced cleanup is
still required for a crashed runtime or synchronous non-yielding test.

Cooperative cancellation is not automatically a preview blocker. It becomes a
blocker only if the preview promises graceful Ctrl+C or timeout completion.
External timeout and deterministic process cleanup are required even when
cooperative cancellation is deferred.

## Compatibility

- Existing non-browser projects remain unchanged.
- The optional package activates only for `browser-*` unless explicitly
  enabled or disabled.
- The package consumes the evaluated project's host instead of requiring one
  SDK-owned host implementation.
- Hosts with the generic launch-info contract are preferred; supported legacy
  hosts may use console readiness as a compatibility fallback.
- Framework-owned pages negotiate the versioned browser page API.
- Older MTP/SDK combinations fail through package activation, argument, or
  transport diagnostics rather than silently switching to another result
  protocol.
- The package's Playwright and Node payload can be serviced independently of
  the .NET SDK.

The supported matrix must eventually name compatible SDK, TestFX package,
target framework, host, operating system, architecture, and browser versions.

## Productization plan

### Phase 0: validated prototype

Implemented in the cross-repository prototype:

- optional TestFX browser package;
- build-transitive host wrapping after `ComputeRunArguments`;
- package-owned page and JavaScript supervisor;
- framework-owned page opt-out and version 1 API;
- unchanged Blazor Gateway host;
- direct browser MTP connection to the SDK HTTP gateway;
- installed Chromium-family browser launch;
- bounded diagnostics and process-tree cleanup;
- machine-readable launch-info support plus console readiness fallback.

### Phase 1: preview hardening

Required before a preview:

- publish and version the optional package;
- define SDK/TestFX compatibility and failure diagnostics;
- select a package-size and platform-distribution strategy;
- validate offline restore, source-build, VMR, signing, and servicing;
- validate installed-browser discovery and enterprise policy behavior on the
  supported operating-system/architecture matrix;
- complete security review of bootstrap, Playwright transport, redaction,
  origin validation, and cleanup;
- run end-to-end `dotnet test`, discovery, filtering, failure, timeout, and
  framework-owned-page tests;
- document unsupported preview features, including artifacts or cooperative
  cancellation if they remain deferred.

The preview does not require a new application server or changes to the Blazor
Gateway.

### Later phases

- Standardize a generic host launch-info/readiness contract and migrate hosts
  from console fallback.
- Add direct artifact export if physical result files are in scope.
- Add cooperative cancellation if graceful cancellation is in scope.
- Evaluate first-class CLI browser options, headed mode, additional browsers,
  browser reuse, and `dotnet watch test`.

## Test plan

### Current prototype coverage

- `dotnet test` runs a test inside `browser-wasm` through the SDK HTTP gateway.
- `--list-tests` discovers browser tests.
- `--filter` selects browser tests.
- evaluated host arguments preserve empty and quoted values.
- framework-owned pages use contract version 1.
- disabling generated host assets leaves only framework-owned assets.
- desktop applications referencing the package remain unaffected.
- package contents include private cross-platform Playwright Node payloads
  without a public `Microsoft.Playwright` package dependency.
- the bearer token and Playwright protocol trace are absent from captured
  command output.

### Product validation

- passing, failing, skipped, filtered, and zero-test runs;
- help and unsupported-option behavior;
- host launch-info and console-fallback compatibility;
- host exit, browser crash/disconnect, page error, request failure, test
  timeout, and cleanup;
- token redaction, owner-only files, exact-origin injection, CORS/PNA, and
  rejected browser arguments;
- Windows, Linux, and macOS on each packaged architecture;
- installed Edge, Chrome, and Chromium versions in the support policy;
- offline/source-build/VMR package construction and use;
- parallel projects and multi-targeting;
- framework-owned pages, including Blazor;
- artifact and cooperative-cancellation tests only when those capabilities are
  added to the preview contract.

## Open questions

1. What package versioning and release vehicle should TestFX use?
2. How should the approximately 200 MB cross-platform Playwright payload be
   split, reduced, or acquired while preserving offline and source-build
   requirements?
3. What generic launch-info schema and compatibility lifetime should replace
   console readiness across WebAssembly hosts?
4. Which installed browsers, versions, operating systems, and architectures
   are supported?
5. Which browser settings remain MSBuild properties and which become
   first-class `dotnet test` options?
6. Are physical TRX and attachments required for the first preview?
7. Is cooperative cancellation required for the first preview, or are external
   timeout and forced cleanup sufficient?
8. Which target-framework and host bands must each package version support?

## Rejected alternatives

### Put the Playwright launcher in the SDK

The prototype demonstrates that a TestFX-owned optional package can provide
the launcher and targets while reusing SDK evaluation and transport. Moving
Playwright into the SDK would couple browser cadence, package size, and Node
servicing to the SDK without improving the result path.

### Add another SDK browser-WASM server

Rejected. The package wraps the host already selected by the project. The
application host continues to serve its own Static Web Assets.

### Add test routes to the Blazor Gateway

Rejected. The Gateway remains a production application host. MTP protocol and
test orchestration stay in the SDK gateway and TestFX package respectively.

### Route MTP results through Playwright or the application host

Rejected. Browser MTP already connects directly to the SDK authenticated HTTP
gateway. An additional proxy would add failure, compatibility, and security
surfaces without providing a required capability.

### Replace the primary HTTP transport with WebSocket

Rejected for the current design. Existing authenticated HTTP is sufficient for
browser-to-SDK discovery, output, and results. A future reverse cancellation
path can use launcher control and Playwright evaluation without replacing the
primary protocol.

### Put credentials in the page URL or static assets

Rejected because URLs and static files leak through history, logs, developer
tools, caches, and unrelated local readers.

## Appendix: Blazor composition

Blazor validates the framework-owned-page path rather than requiring a
Blazor-specific test server.

The project:

1. references `Microsoft.Testing.Platform.Browser`;
2. sets `TestingPlatformBrowserGenerateHostAssets=false`;
3. retains the normal Blazor Gateway produced by `ComputeRunArguments`;
4. adapts its page startup to `globalThis.testingPlatformBrowser`;
5. passes `getArguments()` into its managed MTP application; and
6. calls `complete(exitCode)` after MTP finishes.

The production Blazor Gateway remains unchanged and test-agnostic. Future
Blazor productization may provide a framework-owned adapter or template, but
it should compose the TestFX package contract rather than duplicate launcher,
transport, or Gateway behavior.
