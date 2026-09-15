# `dotnet test` for browser WebAssembly

## Status

Proposed.

This document is a discussion draft for
[dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091). It has been
updated from a working cross-repository prototype. The prototype validates the
ownership and launch boundaries described as **current prototype** below; it
does not make the package, properties, JavaScript API, or support policy a
committed product contract.

The package commits cited below are local and unpushed. This RFC is not an
announcement of an available NuGet package; publishing reviewable source and
exact package/SDK prerequisites is part of preview release preparation.

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
8. The package wraps only SDK-marked test invocations with HTTP bootstrap
   contract version 1 and a unique invocation ID, preserving ordinary run and
   unmarked host queries.
9. Unsupported file input/output options fail before host or browser launch,
   rather than treating browser VFS files as physical host artifacts.
10. SDK-reserved evaluation globals cannot be overridden by differently cased
    user properties; invocation-specific launcher settings do not collide.
11. Cancellation delivered to the launcher after browser startup interrupts
    its completion wait and enters cleanup without waiting for the full test
    timeout.

The locally packed prototype is approximately 200 MB, varying by build
configuration. This is a measured prototype size, not a product-size promise
or an addition to the SDK layout. It makes `Microsoft.Playwright` a private
dependency and packs Playwright's platform-specific Node drivers and
JavaScript payloads into the package. It does not include browser binaries.

### Primary-source snapshots

The implementation claims above refer to the following inspected snapshots.
Unpublished commits are identified by repository, commit, and path instead of
GitHub links that a reader cannot resolve. TestFX package paths in this table
are relative to `src/Platform/Microsoft.Testing.Platform.Browser/`.

| Claim | Primary source |
| --- | --- |
| Optional package, private Playwright dependency, five Node payloads, and launcher runtime configuration | `microsoft/testfx` commit `086b0636d9fa0fe400351a54dc4c1cbb6bd4196d`: `Microsoft.Testing.Platform.Browser.csproj` |
| Private Playwright transport and diagnostic redaction | `microsoft/testfx` commit `066ab8d55f34537834719b5cb846e1ddc173a21f`: `ChromiumBrowser.cs`, `DiagnosticBuffer.cs` |
| Default page/supervisor opt-out | `microsoft/testfx` commit `083be6dabd3f098de1242a9b8e3da57636875d7b`: `buildMultiTargeting/Microsoft.Testing.Platform.Browser.props`, `buildMultiTargeting/Microsoft.Testing.Platform.Browser.targets`, `PACKAGE.md` |
| Evaluated host wrapping, page API v1, and readiness fallback | `microsoft/testfx` commit `ab878a385322d6ec82846055d9c7baa6fb4bf1d1`: `buildMultiTargeting/Microsoft.Testing.Platform.Browser.targets`, `BrowserLauncherOptions.cs`, `ChromiumBrowser.cs`, `HostProcess.cs`, `PACKAGE.md` |
| Package-owned browser assets | Same TestFX snapshot: `buildMultiTargeting/assets/index.html`, `buildMultiTargeting/assets/Microsoft.Testing.Platform.Browser.main.js` |
| Package consumption and framework-owned Blazor page | `dotnet/sdk` commits `054ab8eeb93626cd0a2d214ea4032acfe3f934b9` and `bbde2cfa89b2248709352efc09e71c8be406a154`: `test/TestAssets/TestProjects/BlazorWasmTestApp/BlazorWasmTestApp.csproj`, `wwwroot/index.html`, `BrowserTestRunner.cs` |
| Normal SDK test invocation of the consumer | Same SDK snapshots: `poc/browser-wasm-unit-tests/run-dotnet-test.mjs` and `README.md` |
| Private launch-info directory, opened-handle Unix permission checks, and same-origin URL resolution | `microsoft/testfx` commit `651b5c36e`: `HostProcess.cs`, `BrowserLauncherOptions.cs`, `PACKAGE.md` |
| Fatal page integration errors and late host/argument wrapping fixes | `microsoft/testfx` commits `50932dcdd` and `760ee9e3b6d5b57bf78caf74cd2f098e17b668bd`: `ChromiumBrowser.cs`, `BrowserLauncherOptions.cs`, `buildMultiTargeting/Microsoft.Testing.Platform.Browser.After.targets` |
| SDK invocation markers | `dotnet/sdk` commit `72f63c5add8f20c9577de7c6a67c1f2d0a2aef72`: `src/Cli/dotnet/Commands/Test/CliConstants.cs`, `src/Cli/dotnet/Commands/Test/MTP/SolutionAndProjectUtility.cs` |
| Implemented invocation/version gate and unsupported preview options | `microsoft/testfx` commit `89beb277539699e4842674d79d443259e3d6f37a`: `buildMultiTargeting/Microsoft.Testing.Platform.Browser.After.targets`, `BrowserLauncherOptions.cs`, `PACKAGE.md` |
| Reserved evaluation-global markers and unique invocation ID | `dotnet/sdk` commit `03c8d0a7d91f3bf1b6a1170d5a716a4c5471f9e3`: `src/Cli/dotnet/Commands/Test/CliConstants.cs`, `src/Cli/dotnet/Commands/Test/MTP/SolutionAndProjectUtility.cs`, `test/dotnet.Tests/CommandTests/Test/GivenDotnetTestBuildsAndRunsTests.cs` |
| Final real-consumer invocation | Same SDK snapshot: `test/TestAssets/TestProjects/BlazorWasmTestApp/BlazorWasmTestApp.csproj`, `poc/browser-wasm-unit-tests/run-dotnet-test.mjs`, `poc/browser-wasm-unit-tests/run-browser-tests.mjs` |
| Invocation-specific configuration and linked launcher cancellation | `microsoft/testfx` commit `fc15c640ea620dbd7330eb2e8e35afbc39144cf7`: `buildMultiTargeting/Microsoft.Testing.Platform.Browser.After.targets`, `buildMultiTargeting/Microsoft.Testing.Platform.Browser.targets`, `BrowserRunMonitor.cs`, `Program.cs`, `PACKAGE.md` |

The final SDK `03c8d0a7d9` and TestFX `fc15c640` snapshots supersede the earlier
implementation snapshots where behavior changed. Earlier TestFX `89beb277`
coverage was reported as 49 passing unit tests with two Windows-skipped cases,
eight passing acceptance tests, and successful full packing. The final SDK
contract test, real package execution/discovery/filtering, unsupported-results
rejection, and standalone clean rebuild were also reported passing. These
are reported local results, not a cross-platform support guarantee;
non-Windows CI evidence remains outstanding. The prototype sources are not
part of this documentation-only PR.

### Proposed product

Before productization, the teams still need to decide:

- how the optional package is published, versioned, serviced, and represented
  in source-build and the VMR;
- whether the cross-platform Playwright payload is split or otherwise reduced;
- the installed-browser support policy and browser-selection experience;
- the stable generic host readiness contract;
- which launcher options deserve first-class `dotnet test` options rather than
  MSBuild properties;
- future artifact transfer and cooperative managed cancellation, which the
  current live-results preview does not provide.

The prototype property and contract names are therefore documented here to
make the implementation reviewable, not to reserve them permanently.

## Scope

The package is framework-neutral. Its prototype targets use a `browser-*`
condition, but the supported scenario proposed here is exactly `browser-wasm`;
that condition is not evidence for other browser RIDs.

It supports both:

- a general WebAssembly test application using the package's default page and
  JavaScript supervisor; and
- a UI framework application that owns its page and explicitly composes the
  browser testing API.

WASI is a different host model and is out of scope. VSTest mode, host-side
Playwright UI/E2E tests, dynamic assembly loading from the browser virtual file
system, managed browser debugging, and browser process reuse are also out of
scope for the initial design.

The framework-owned page seam is in scope; UI/component testing behavior and
Blazor-specific renderer or JavaScript interop testing remain separate work.
`--test-modules` is also outside this project/package launch model: it bypasses
the evaluated targets and cannot infer the browser host or bundle from a DLL.
Tests are statically referenced and registered at build time; a new run uses a
fresh page/runtime, not assembly unloading.

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

This is illustrative XML with placeholder package versions, not a currently
restoreable public-package recipe. It assumes MTP mode for `dotnet test` and
an MTP-compatible SDK and MSTest version.

The package defaults to enabled only for `browser-*` runtime identifiers;
launcher substitution additionally requires the SDK invocation markers below.
Referencing it does not alter an ordinary desktop test application or replace
the framework host for ordinary `dotnet run`.

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

The SDK/package preview contract uses these exact MSBuild properties:

| Property | Meaning |
| --- | --- |
| `DotnetTestInvocation=true` | Marks the SDK's MTP test invocation, not an ordinary run or standalone host query. |
| `DotnetTestHttpBootstrapVersion=1` | Declares support for the launcher bootstrap contract; it is not the MTP wire-protocol version or browser page API version. |
| `DotnetTestInvocationId` | A fresh 32-character hexadecimal GUID for each SDK project evaluation, used to isolate launcher configuration filenames. |

These are SDK-supplied invocation properties, not project opt-ins or
credentials. SDK `03c8d0a7d9` supplies them as reserved evaluation globals
before imports and `ComputeRunArguments` are evaluated. Its case-insensitive
property dictionary preserves the reserved values when merging user-supplied
or collection globals, including mixed-case override attempts. The ID is
generated with `Guid.NewGuid().ToString("N")`; it is not an authentication token.
This replaces the execution-time marker assignment in `72f63c5add`.

With `TestingPlatformBrowserEnabled=true` and a browser RID, the package
validates a marked invocation and rejects a missing bootstrap version or any
value other than `1` with an actionable MSBuild diagnostic. It also rejects
an absent invocation ID or one that does not match `^[0-9A-Fa-f]{32}$` before
capturing or replacing the host command. An absent or false
`DotnetTestInvocation` leaves the framework command unchanged, even if a
bootstrap version was supplied. An older SDK that supplies neither marker
does not activate this launcher; the version error is not a universal
older-SDK detection mechanism.

For a supported marked invocation, the package integrates after
`ComputeRunArguments`:

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

In the prototype, an empty override means "use the computed value"; computed
empty and quoted arguments are preserved.

This is intentionally host-agnostic. The launcher does not special-case
`blazor-gateway.dll`, a dev-server assembly name, or a launch profile. The
Blazor proof of concept starts the same packaged Gateway that `dotnet run`
selected and adds no test routes or middleware.

The invocation gate is implemented, including maintained ordinary-run,
plain-query, bootstrap-version, and multi-target activation coverage. Late
framework host providers and quoted, empty, and multiline arguments are also
covered. Package activation and SDK marker production have separate coverage:
TestFX acceptance tests explicitly supply the invocation/version/ID properties,
while the SDK contract regression verifies automatic reserved values despite
mixed-case overrides. The real consumer uses `dotnet test --project` and an
explicit `browser-wasm` RID. Its validation harness uses a run-local linked
redist SDK overlay rather than mutating the shared redist installation.

The package writes
`Microsoft.Testing.Platform.Browser.$(DotnetTestInvocationId).launch` beneath
the project's intermediate path. Distinct SDK evaluations therefore use
distinct settings files even for the same project/output path. Maintained
acceptance coverage creates concurrent queries with different IDs and checks
that their arguments remain isolated. MSBuild `Clean` removes both the legacy
fixed-name file and invocation-specific files; this is not a claim of
per-run deletion or isolation for unrelated build outputs.

## Host readiness

A machine-readable launch-info contract is preferable to localized console
parsing. The launcher creates a fresh private directory and passes a path
inside it to the child host through
`TESTINGPLATFORM_BROWSER_LAUNCH_INFO_FILE`. A participating host atomically
creates the owner-only file:

```json
{
  "version": 1,
  "url": "http://127.0.0.1:54321/"
}
```

Only a loopback HTTP or HTTPS URL is accepted.

The private directory uses mode `0700` on Unix and a protected
current-user-only DACL on Windows. The launcher rejects a launch-info path
marked as a symbolic link/reparse point and checks Unix permissions on the
opened file handle. Windows protection relies on the containing directory's
DACL, not an independent file ACL check. The host must create the file
atomically at the supplied path and must not replace the containing directory.
These implemented protections supersede the earlier random-temporary-path
design; they do not constitute a blanket security or non-Windows CI claim.

The current Blazor Gateway does not implement this contract. For compatibility,
the launcher also recognizes the existing ASP.NET Core
`Now listening on: <URL>` message from captured output. This fallback is part
of the current compatibility story; the RFC must not claim that browser
testing waits exclusively on launch-info or that host unification blocks a
preview.

The file is checked before waiting for stdout, but once console readiness is
accepted the launcher does not wait for a later launch-info file. Version 1 of
this experimental schema is separate from version 1 of the browser page API.

Productization should define a generic launch-info contract usable by
`dotnet run`, browser testing, tools, and multiple WebAssembly hosts. It should
remain host information, not a test-specific Gateway feature. The console
fallback can be retired only after the supported host matrix reliably
implements that contract.

Configured URL resolution now rejects a result outside the host origin.
The eventual generic host contract must also define base paths, redirects,
and asset readiness: a fallback page returning HTTP 200 is not evidence that
the test bootstrap exists. Let the host bind loopback port zero atomically
where supported; do not assume a probe-and-release port remains free. HTTPS
requires a trusted certificate and compatible browser policy, not a silent
context-wide certificate bypass.

## Browser page API

Before navigation, the launcher installs this API on the top-level page at the
expected loopback origin:

```js
globalThis.testingPlatformBrowser = {
    contractVersion: 1,
    getArguments() { /* returns MTP arguments */ },
    complete(exitCode) { /* reports the managed exit code */ }
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

Completion means the MTP operation has finished, not merely that navigation
succeeded. The page must await live protocol replies and any supported
artifact acknowledgements before calling `complete`, and the launcher must
forward the managed exit code. Preserve the SDK's existing
[missing-session-end check](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs#L248-L260)
for executed sessions; browser completion must not turn a prematurely exited
test run into success. Help and discovery have their own completion paths.
Missing/duplicate completion, page crash, and a disconnect before completion
need bounded, non-success outcomes.

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

and provides its own page and startup code. For a general WebAssembly page
that owns runtime creation and whose managed main returns, its adapter follows
the same contract (after importing `dotnet` from `./_framework/dotnet.js`):

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

This `runMain` example is not a Blazor startup recipe: a framework whose main
continues for the application's lifetime must call `complete` when its MTP
run finishes, not wait for the application to exit. The Blazor consumer does
that through `BrowserTestRunner.cs`; see
[Primary-source snapshots](#primary-source-snapshots).

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
2. validates that the endpoint is loopback HTTP or HTTPS and that a bearer
   token is present;
3. retains the token in memory;
4. injects the MTP arguments through Playwright before application startup;
5. redacts the token and protocol endpoint path from launcher, host, and
   browser diagnostics.

The token is not placed in the browser URL, static assets, or an
unauthenticated remote-debugging port. Code in the top-level test page can
obtain the arguments because it must start MTP, so the design narrows rather
than eliminates that trust boundary.

The SDK gateway's existing CORS behavior pins the first accepted browser
origin, including an unauthenticated preflight. The prototype does not add a
pre-navigation origin-registration operation or any other gateway API.

The page's CSP `connect-src`, CORS/PNA preflights, and browser network policy
still apply to MTP requests; Playwright argument injection bypasses none of
them. A framework-owned page must permit the selected SDK endpoint explicitly
or fail with an actionable diagnostic, not disable CSP. The preview does not
implement dynamic CSP composition or a race-free pre-navigation origin
registration contract. These need validation in the selected preview matrix.

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
- browser/context close operations, with deadline enforcement and fallback
  cleanup remaining the launcher's responsibility.

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

The implemented fatal-integration-error bridge completes the pending wait
with a failure instead of waiting for the completion timeout. Maintained
acceptance coverage exercises that path. It is distinct from the Playwright
page-crash event, which records a diagnostic in the inspected snapshot.
Observation/cleanup paths are not proof of prompt termination in every
failure mode. Release validation must cover bounded cleanup and forced
termination if Playwright closure or the launcher stops responding; no
cleanup path may kill unrelated user processes.

A synchronous infinite loop on the single browser WebAssembly thread cannot
observe managed cancellation or flush final results. The outer launcher can
still enforce a deadline and terminate the owned processes.

Await asynchronous test bodies and yield between cases to keep the browser
event loop responsive; a framework cannot make a synchronous infinite loop
cooperative. Record a per-test-start breadcrumb before invoking the test body
so crash/hang diagnostics can identify the last started test. The current
launcher alone does not establish that MTP producer guarantee.

Diagnostics should retain phase, elapsed time, exit code/signal, and bounded
host/browser output. They must not guess a cause from an ambiguous symptom
such as `Failed to fetch`. Screenshots/traces are optional future diagnostics,
not present coverage or substitutes for a test result; their capture must
preserve bootstrap redaction.

## Artifacts

Host persistence of browser virtual-file-system artifacts is not implemented
by the prototype. A TRX extension may produce a file inside the browser VFS,
but the SDK must not present that browser path as a persisted host artifact.

An artifact sink that transfers bytes and metadata directly from MTP to an
SDK-owned destination remains desirable. It should be capability-negotiated,
bounded, authenticated, and designed in TestFX with the SDK result
materialization contract. Existing file-only producers could then open their
VFS file and copy it through the same sink.

The SDK must select destinations under its results directory, reject
traversal/symlink escapes and overwrite collisions, validate length/hash and
per-file/run quotas, and remove incomplete transfers. Transfer must not block
live results. Use bounded chunks as the baseline; browser response streaming
does not establish request-streaming support for the loopback gateway.

The current preview deliberately supports live execution/discovery results
without physical artifact export. The launcher now rejects the following
options after response-file expansion and before starting the host/browser:

| Rejected surface | Exact options |
| --- | --- |
| Configuration and host paths | `--config-file`, `--settings`, `--results-directory` |
| File diagnostics | `--diagnostic`, `--diagnostic-file-prefix`, `--diagnostic-output-directory` |
| Report artifacts | `--report-trx`, `--report-trx-filename`, `--report-html`, `--report-html-filename`, `--report-junit`, `--report-junit-filename`, `--report-ctrf`, `--report-ctrf-filename` |
| Coverage artifacts | `--coverage`, `--coverage-output`, `--coverage-output-format`, `--coverage-settings` |

Matching is case-insensitive, accepts one or two leading hyphens, and
recognizes names before `=` or `:` as well as separate-value syntax. The
diagnostic names the unsupported option. Ordinary help, discovery, and
filtering options remain available; this list is not a claim that every
third-party extension option is browser-compatible.

Input/output transfer is deferred, not implemented by rejecting these options.
Remote configuration remains future TestFX/SDK work, not an existing Gateway
feature; user extension arguments are not automatically path-translated.

## Cancellation

Launcher-wait cancellation is implemented in TestFX `fc15c640`.
`Program.cs` passes its run cancellation token to `BrowserRunMonitor.WaitAsync`,
which links that token to the browser-completion, host-exit, and browser-exit
waits. Cancellation delivered after browser startup exits the completion race
and enters the existing disposal paths instead of waiting for the configured
completion timeout. A unit regression exercises cancellation of the monitor's
long-running waits and requires prompt termination.

This closes the unlinked running-test wait in `89beb277`; it does not add
cooperative managed MTP cancellation. The SDK retains force-kill paths, and
the launcher retains startup/completion deadlines and cleanup. Force-killing
the launcher cannot be assumed to execute its `finally` blocks. Neither
launcher cancellation nor force-kill asks browser MTP to flush a graceful
final summary.

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
- Launcher wrapping requires a browser RID, the enabled package, and
  `DotnetTestInvocation=true` with `DotnetTestHttpBootstrapVersion=1` and a
  valid, unique `DotnetTestInvocationId`.
  Ordinary run/plain queries retain the framework host.
- The package consumes the evaluated project's host instead of requiring one
  SDK-owned host implementation.
- Hosts with the generic launch-info contract are preferred; supported legacy
  hosts may use console readiness as a compatibility fallback.
- Framework-owned pages validate browser page API version 1; optional
  capability negotiation is future work.
- A marked invocation with missing/unsupported bootstrap version or
  missing/malformed invocation ID fails before launch; an SDK with no
  invocation marker does not activate wrapping.
  MTP protocol negotiation remains independent.
- The package's Playwright and Node payload can be serviced independently of
  the .NET SDK.

The supported matrix must eventually name compatible SDK, TestFX package,
target framework, host, operating system, architecture, and browser versions.
The preview desktop launcher targets `net8.0` with major roll-forward; that
does not define the browser application's minimum TFM. SDK, package, launcher
runtime, page API, host readiness, and MTP protocol versions must be considered
separately rather than assumed to match.

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
- machine-readable launch-info support plus console readiness fallback;
- SDK-marked invocation/bootstrap-version validation and ordinary-run
  preservation;
- reserved evaluation-global markers and a fresh invocation ID, with
  mixed-case override protection;
- invocation-specific launcher configuration and MSBuild cleanup;
- linked launcher cancellation during the running-test wait;
- pre-launch rejection of the file/report/coverage options listed above;
- private launch-info directories, same-origin URL resolution, fatal page
  integration-error handling, and late host/argument wrapping fixes.

### Phase 1: shipment and platform validation

The invocation, configuration-isolation, launcher-wait cancellation, and
unsupported-option gates are implemented, not open design tasks. Remaining
shipment work is:

- publish reviewable source and version the optional package;
- publish the compatible SDK/TestFX version matrix, including the SDK's
  evaluation-global marker implementation;
- select a package-size and platform-distribution strategy;
- validate offline restore, source-build, VMR, signing, and servicing;
- obtain non-Windows CI evidence and validate installed-browser discovery and
  enterprise policy behavior across the supported platform matrix;
- complete the shipment security review and extend lifecycle and concurrency
  regression evidence across the supported platforms.

Passing local unit/acceptance tests and packing do not establish the whole
shipment matrix. The final snapshots address the previously identified
evaluation-global placement, shared launcher-settings filename, and unlinked
completion-wait gaps; broad platform validation remains a shipment concern.

The preview does not require a new application server or changes to the Blazor
Gateway.

### Later phases

- Standardize a generic host launch-info/readiness contract and migrate hosts
  from console fallback.
- Add direct artifact export and remote input transfer before enabling the
  rejected preview file/report/coverage surface.
- Add cooperative managed cancellation; external timeout/force cleanup is not
  a graceful MTP summary.
- Evaluate first-class CLI browser options, headed mode, additional browsers,
  browser reuse, and `dotnet watch test`.

## Test plan

### Current prototype coverage

The TestFX `fc15c640` snapshot in [Primary-source snapshots](#primary-source-snapshots)
contains `BrowserPackageExecutionTests.cs` under
`test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/`
and `BrowserLauncherOptionsTests.cs` under
`test/UnitTests/Microsoft.Testing.Extensions.UnitTests/`. The browser
integration tests report inconclusive when Node or a browser is missing; that
outcome is not execution evidence. The earlier `89beb277` run counts are
recorded above, not presented as counts for the expanded final suite.
TestFX tests supply invocation properties explicitly; the SDK
`ComputeRunArgumentsReceivesReservedDotnetTestInvocationContract` regression
at `03c8d0a7d9` separately verifies automatic evaluation-global marker and ID
delivery despite mixed-case override attempts. The final real package and
standalone consumer checks were reported passing, not rerun for this RFC.

- `dotnet test` runs a test inside `browser-wasm` through the SDK HTTP gateway.
- `--list-tests` discovers browser tests.
- filtered passing, skipped, and deliberately failing browser runs preserve
  their result and exit behavior.
- evaluated host arguments preserve empty and quoted values.
- framework-owned pages use contract version 1.
- disabling generated host assets leaves only framework-owned assets.
- desktop applications referencing the package remain unaffected.
- ordinary `dotnet run` and unmarked `ComputeRunArguments` retain the
  framework host; marked queries reject missing or unsupported bootstrap
  versions, missing/malformed invocation IDs, and activate only the browser
  inner target in multi-target builds.
- concurrent queries with distinct invocation IDs keep their configuration
  arguments separate; MSBuild cleanup removes invocation-specific files.
- cancellation interrupts the linked launcher completion monitor promptly.
- unsupported config/path/diagnostic/report/coverage options are rejected
  before launch, including single-hyphen and alternate value delimiters.
- fatal framework-page integration errors fail without waiting for the full
  completion timeout.
- package contents include private cross-platform Playwright Node payloads
  without a public `Microsoft.Playwright` package dependency.
- the bearer token and Playwright protocol trace are absent from captured
  command output.

### Release regression matrix

The implemented cases above remain regression requirements, not unfinished
features. Extend their evidence to the shipment matrix and deferred features:

- passing, failing, skipped, filtered, zero-test, help, and unsupported-option
  behavior on every supported SDK/browser combination;
- host launch-info and console-fallback compatibility;
- Windows ACL and Unix permissions, link/redirect/base-path rejection, and
  invalid or concurrently written launch-info;
- host exit, browser crash/disconnect, page error, request failure, test
  timeout, and cleanup;
- token redaction, response-file expansion bounds, owner-only files,
  exact-origin injection, CORS/PNA/CSP, and rejected browser arguments;
- Windows regression coverage and new Linux/macOS CI evidence on each
  supported architecture;
- installed Edge, Chrome, and Chromium versions in the support policy;
- offline/source-build/VMR package construction and use;
- parallel projects, same-output-path concurrency, and multi-targeting;
- automatic SDK marker delivery, `--no-build`, repeated evaluation, ordinary
  `dotnet run`/build/publish, conflicting generated/custom assets, and
  unsupported module-only launch;
- framework-owned pages, including Blazor;
- completion ordering, premature zero exit, per-test-start crash breadcrumbs,
  and cancellation/forced cleanup at every phase;
- artifact/input-transfer and cooperative-managed-cancellation tests when
  those deferred capabilities are added.

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
6. What artifact/input-transfer contract permits enabling the currently
   rejected file/report/coverage options in a later release?
7. What versioned managed cancellation hook adds graceful MTP completion
   beyond external timeout and forced cleanup?
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
