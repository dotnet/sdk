# `dotnet test` for browser WebAssembly

## Status and scope

Proposed design for [dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091),
with published experiments in
[dotnet/sdk#56305](https://github.com/dotnet/sdk/pull/56305) and
[microsoft/testfx#11305](https://github.com/microsoft/testfx/pull/11305).
These are reviewable implementations, not released packages, a supported
preview, or commitments to the experimental properties below.

The scope is managed unit tests running **inside a browser**, using
`Microsoft.NET.Sdk.WebAssembly` and Microsoft.Testing.Platform (MTP).
The SDK experiment adds only a sample and reproduction guidance; it adds
no production SDK code.

## Architecture

Keep browser-specific dependencies and behavior in the optional TestFX
package `Microsoft.Testing.Platform.Browser`. Reuse the existing WebAssembly
application host and MTP HTTP transport.

| Owner | Responsibility and primary source |
| --- | --- |
| Test application | C# tests, framework references and an [MTP-generated managed entry point][entrypoint]. The [sample project][sample-project] and [test][sample-test] need no authored Main, HTML, JavaScript, or interop for launch and results. |
| Browser package | [Build integration][package-targets], host assets, [Playwright launch][browser], diagnostics and cleanup. It wraps the evaluated host command, not a new web server. |
| WebAssembly host | Serve the application and its Static Web Assets. The SDK's [props](../../src/WasmSdk/Sdk/Sdk.props) and [targets](../../src/WasmSdk/Sdk/Sdk.targets) compose that build pipeline; the experiment launches the existing WasmAppHost. |
| Managed MTP and SDK | MTP's [connection][mtp-connection] and [C# HTTP client][http-client] exchange the existing framed protocol with the SDK's [HTTP gateway](../../src/Cli/dotnet/Commands/Test/MTP/IPC/HttpTestHostGateway.cs). Neither Playwright nor package JavaScript relays test results. |

```text
SDK -> package launcher -> evaluated WasmAppHost -> browser application
          |                                         |
          +-- private arguments / terminal result --+

Managed browser MTP <---- authenticated HTTP ----> SDK test gateway
```

Pure-managed describes the application, not all browser infrastructure.
The package's [boot module][boot] imports `dotnet.js`, creates the runtime,
passes application arguments, awaits managed Main and reports its terminal
outcome. External supervision is needed when the page cannot make progress.
There is no custom TCP DevTools client, JavaScript results protocol, or
additional test-aware asset server.

## Consumer and reproduction

The [canonical project][sample-project] is a WebAssembly executable with
[`EnableMSTestRunner=true`][mstest-targets]. The experiment disables native relinking and
trimming with `WasmBuildNative=false` and `PublishTrimmed=false`; it does not
establish support for those modes.

The sample uses aligned `MSTest.TestFramework` and `MSTest.TestAdapter`
references plus the browser package rather than the [`MSTest` metapackage][mstest-package].
The browser path does not need its additional reporting, coverage and VSTest
SDK dependencies. Keep package versions separate from references and align
the MTP/framework packages with the browser package under review.

Use the [SDK sample README][sample-readme] as the single reproduction recipe,
not a second copy of its commands here. Reproduction must identify the built
SDK, compatible runtime/workload assets, exact locally packed packages and
installed Chromium executable. Pin SDK selection, isolate package and build
state, disable build-server and MSBuild worker-node reuse, stop on restore
failure, and verify the restored package identity.
The explicit restore is needed before `dotnet test` evaluates the package's
`ComputeRunArguments` integration. A machine-specific local package path or
a successful restore alone is not proof of that identity.

## Launch and result contract

### Experimental activation

The [package target][package-targets] wraps `RunCommand`, `RunArguments` and
`RunWorkingDirectory` after
[`ComputeRunArguments`](../../src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets)
only when browser support is enabled, the RID starts with `browser-`, and
the caller supplies `DotnetTestInvocation=true`. This is an explicit PoC
marker, **not a reserved SDK property or automatic product behavior**.
Unmarked queries and desktop execution are not wrapped.

The wrapper preserves the evaluated host command through private URI-encoded
launcher options and starts it without a shell. Encoding is for argument
preservation, not secrecy. A custom target that overwrites the run properties
later can bypass this wrapper; arbitrary host/target composition is not
supported by the experiment. A supported activation mechanism requires a
separate SDK/TestFX decision, not more package target-ordering machinery.

### Host and browser startup

The [package assets][package-targets] participate in the existing Static Web
Assets graph under `/_mtp/`. The launcher navigates to
`/_mtp/browser-host.html`, leaving the consumer's root page alone.
`AssetKind="Build"` keeps these test host assets out of publication.
This is not published-layout test execution or a framework-page adapter.

The [host process][host] recognizes WasmAppHost's stdout `App url:` line
with an HTTP loopback URL. It does not infer readiness from generic server
logs, HTTPS URLs or stderr, and it fails on early exit or timeout.
No readiness-file protocol or fallback server is introduced.

The [launcher][browser] requires an explicitly selected installed
Chromium-family executable and creates a headless browser with a fresh
context and page. It does not acquire browsers, search for alternatives,
or expose arbitrary browser arguments.

### Managed execution and completion

The SDK's
[`TestApplication`](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs)
selects HTTP transport for browser modules and appends a protected bootstrap
response-file reference. The [package parser][options] expands the final SDK
bootstrap before launching the host; earlier user-authored response files
are unsupported.

WasmAppHost's readiness URL can also carry application arguments as repeated
`arg=` query values, including `TestingPlatformCommandLineArguments`.
The [parser][options] URL-decodes those values in order, and the
[launcher][launcher] prepends them to the SDK-supplied application arguments,
leaving the expanded bootstrap last. This preserves project-configured
filters as well as command-line arguments; the [acceptance test][acceptance]
checks that an unmatched project-configured filter produces zero tests.

The [private page bindings][browser] provide application arguments and accept
one terminal result from the [boot module][boot]. They ship together inside
the package; they are not a public framework API. MTP owns its handshake,
discovery, filtering and result messages through [managed HTTP][http-client].
The launcher returns the [managed exit code][terminal-result], while a
reported bootstrap error fails the launch. A terminal binding is not an
MTP handshake or evidence that any tests were reported; the SDK retains
command-level result handling.

## Security boundary

This runs **trusted test projects and their dependencies**. The package
executes their evaluated host command. An invocation marker, loopback URL or
isolated browser context is not a sandbox for hostile tests.

The SDK creates its bootstrap response file with create-new semantics and
current-user access on Windows or user-only permissions on Unix. The package
[reader][options] is specific to that bootstrap, not a general untrusted
response-file parser. It rejects a detected symbolic link or reparse point
and checks Unix access permissions, but these checks are not a complete
ownership or replacement-race defense.

Bearer credentials are passed through private bindings rather than URLs or
static assets. Bindings validate the expected page, top-level frame and host
origin. These checks do not authenticate individual scripts executing in
that page: the runtime, tests and same-page code remain trusted with the
bootstrap. The package [redacts known bootstrap values][diagnostics] from
its diagnostic buffer and disables Playwright `DEBUG` logging; this is not
a promise to sanitize arbitrary output from untrusted code.

The existing SDK
[gateway](../../src/Cli/dotnet/Commands/Test/MTP/IPC/HttpTestHostGateway.cs)
authenticates protocol POSTs and handles CORS/Private Network Access
preflights. Preflights are unauthenticated, and the first accepted origin is
pinned when none was supplied; the launcher does not preregister its origin.
Origin checks and bearer authentication are distinct. This experiment adds
no CSP rewriting or new gateway security protocol.

## Dependencies and distribution

The [browser package project][package-project] privately references
`Microsoft.Playwright` and bundles the host-side launcher, its managed
dependencies and Playwright's Node driver payload. Browser binaries are
separate prerequisites. Unused Playwright Vite UI and TypeScript assets are
excluded. A pure-managed test application therefore does not mean the
overall launch stack has no JavaScript or Node dependency.

Keep that payload out of the SDK layout, core MTP and ordinary test
applications that do not opt in. Private NuGet dependencies still require
payload, licensing, signing, source-build, offline-use and servicing
decisions. Bundling platform payloads is not evidence that those platforms
work. The launcher's target framework and roll-forward policy likewise do
not define a supported test-application framework matrix.

## Failure handling and limits

The [launcher][launcher] monitors completion, host exit, browser disconnect,
unhandled page errors and page crash, with separate startup and completion timeouts. Its
[browser][browser] and [host][host] cleanup paths bound individual waits and
attempt to release owned resources. Bounded waiting alone does not terminate
a wedged driver or prove that every child process was reaped; successful
cleanup must be demonstrated on the real process tree, including failure
and cancellation paths.

Graceful SDK-to-browser-managed-MTP cancellation is not implemented by this
package. Forceful browser/host cleanup cannot guarantee managed `finally`
execution or artifact flushing. Console cancellation, parent termination
and arbitrary process signals are not interchangeable guarantees.

Browser VFS files are not host files. There is no host-input transfer or
artifact export, and the launcher does not implement a general report,
configuration or coverage-option denylist. Do not infer host report
persistence from live results. A preview must explicitly define which
file-dependent options are supported and how unsupported use fails.

Other non-goals are Blazor/UI/component testing, framework-owned startup,
WASI, VSTest, `--test-modules` without project evaluation, browser reuse,
hot reload, managed browser debugging and dynamic assembly loading.
Non-Windows browser execution, published-layout execution and alternate
runtime modes require their own composed validation before being advertised.

## Evidence and preview gate

Source links below pin inspected implementations; they are not evidence
that a later package or branch passed. Exact commands, SDK/package/browser
identities, package hashes, exit codes and result artifacts belong with the
two implementation PRs, rather than test-count tables in this design.

The implementation PRs track local validation and CI separately. Neither a
successful build on another platform nor a signing-policy dry run establishes
real-browser support or officially signed binaries. Evidence must use the
corresponding built SDK and packaged TestFX implementation together; earlier
local runs do not validate fixes made afterward.

A supported preview needs an agreed activation mechanism, an explicit
SDK/runtime/MTP/browser compatibility matrix, verified package distribution,
and real end-to-end evidence for success, discovery/filtering, failure,
zero results, incompatible or missing handshakes, cancellation and cleanup.
Keep an owner responsible for dependency flow, release-branch validation
and servicing before downstream enablement.

None of those requirements calls for a new results relay, public page
protocol, browser acquisition manager or asset server. Host unification is
a separate discussion in
[dotnet/runtime#122144](https://github.com/dotnet/runtime/issues/122144) and
[dotnet/aspnetcore#67814](https://github.com/dotnet/aspnetcore/issues/67814).
The narrow experiment reuses WasmAppHost rather than claiming to solve that
broader hosting design.

[sample-project]: https://github.com/dotnet/sdk/blob/d89eaef953deb9cce526d54f410e3b8a6650d784/poc/browser-wasm-unit-tests/BrowserWasmTestApp.csproj
[sample-test]: https://github.com/dotnet/sdk/blob/d89eaef953deb9cce526d54f410e3b8a6650d784/poc/browser-wasm-unit-tests/BrowserWasmTests.cs
[sample-readme]: https://github.com/dotnet/sdk/blob/d89eaef953deb9cce526d54f410e3b8a6650d784/poc/browser-wasm-unit-tests/README.md
[mstest-package]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Package/MSTest/MSTest.csproj
[mstest-targets]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Adapter/MSTest.TestAdapter/buildTransitive/common/MSTest.TestAdapter.targets
[entrypoint]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.MSBuild/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.targets
[package-project]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/Microsoft.Testing.Platform.Browser.csproj
[package-targets]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/buildMultiTargeting/Microsoft.Testing.Platform.Browser.targets
[browser]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/ChromiumBrowser.cs
[boot]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/buildMultiTargeting/assets/Microsoft.Testing.Platform.Browser.main.js
[host]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/HostProcess.cs
[options]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/BrowserLauncherOptions.cs
[terminal-result]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/BrowserTerminalResult.cs
[launcher]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/Program.cs
[diagnostics]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform.Browser/DiagnosticBuffer.cs
[mtp-connection]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/DotnetTestConnection.cs
[http-client]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/Transport/DotnetTestHttpClient.cs
[acceptance]: https://github.com/microsoft/testfx/blob/cf6005560b9a43c40bd756e14792f4533ca00dae/test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserPackageExecutionTests.cs
