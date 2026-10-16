# `dotnet test` for browser WebAssembly

## Status and scope

Proposed design for [dotnet/sdk#54091](https://github.com/dotnet/sdk/issues/54091),
implemented for review in
[dotnet/sdk#56305](https://github.com/dotnet/sdk/pull/56305) and
[microsoft/testfx#11305](https://github.com/microsoft/testfx/pull/11305).
These implementations do not establish released packages, supported APIs or
release approval. Source links pin the implementation described here.

The scope is managed unit tests running **inside a browser**, using
`Microsoft.NET.Sdk.WebAssembly` and Microsoft.Testing.Platform (MTP).
The SDK supplies test-specific launcher selection, HTTP control and artifact
reception. The optional browser package owns browser-specific dependencies;
core MTP owns managed protocol behavior.

## Architecture

| Owner | Responsibility and primary source |
| --- | --- |
| Test application | C# tests and an [MTP-generated managed entry point][entrypoint]. The [sample][sample-project] uses aligned Framework + Adapter + Browser references, with no authored Main, HTML, JavaScript or integration targets. |
| SDK | [Select the test command][selection] after ordinary run providers; own the [authenticated gateway][gateway], cancellation policy, destination paths and [artifact limits][limits]. |
| Managed MTP | [Handshake, discovery, results, HTTP control and artifact export][connection]. It uses C# `HttpClient`, not a JavaScript results relay. |
| Browser package | [Build-only host assets][package-targets], runtime startup, private Playwright control and an [owned supervisor/worker lifecycle][supervisor]. |
| WebAssembly host | Existing WasmAppHost serves the application and Static Web Assets. The SDK's [props](../../src/WasmSdk/Sdk/Sdk.props) and [targets](../../src/WasmSdk/Sdk/Sdk.targets) compose that build pipeline; no additional asset server is introduced. |

```text
SDK -> package supervisor -> contained worker -> WasmAppHost + Playwright/browser
                                |
                                +-- private arguments / terminal result <--> boot JS

Managed browser MTP <---- authenticated HTTP ----> SDK gateway
                     results, control, artifacts
```

Pure-managed describes the application, not all browser infrastructure.
The package's [boot module][boot] imports `dotnet.js`, creates the runtime,
passes arguments, awaits managed Main and reports its terminal outcome.
Those private bindings ship with the launcher; they are not a public page
API or a second test protocol. Playwright retains its private transport;
there is no custom TCP DevTools client.

## Consumer and reproduction

The [canonical project][sample-project] is a WebAssembly executable with
`EnableMSTestRunner=true`. It references `MSTest.TestFramework` and
`MSTest.TestAdapter` directly, plus `Microsoft.Testing.Platform.Browser`,
rather than installing the additional coverage, full TRX and VSTest SDK
dependencies from the [`MSTest` metapackage][mstest-package].
The [sample test][sample-test] also writes a browser-VFS file and registers
it through `TestContext.AddResultFile`.

Use the [SDK README][sample-readme] for sample reproduction and the
[shared PowerShell 7 entry point][compatibility-runner] for composed validation,
rather than duplicating either recipe here. The sample recipe
identifies the built SDK and immutable, aligned framework/MTP/browser package
feed, verifies restored package hashes, selects an installed Chromium-family
executable and isolates restore/build state. Build-server reuse, MSBuild
worker-node reuse and shared compilation are disabled for disposable runs;
the previous environment is restored afterward. Retained host results are
outside that disposable state.

No caller invocation marker is needed. The explicit restore makes package
imports available before test-command evaluation. The sample deliberately
uses `WasmBuildNative=false` and `PublishTrimmed=false`; this does not establish
native-relink, Native AOT or trimming support.

## SDK launcher selection

The [SDK selection code][selection] first runs `ComputeRunArguments`,
including its canonical before/after hooks. It then seeds these separate
properties from the resulting ordinary run properties **only when absent**:

- `TestingPlatformRunCommand`
- `TestingPlatformRunArguments`
- `TestingPlatformRunWorkingDirectory`

Explicit empty values are retained rather than treated as missing.
The SDK then invokes the optional `ComputeTestingPlatformRunArguments`
target in a separate build request and consumes those dedicated outputs.
With no target or providers, ordinary run values remain the fallback.
Configured providers without a callable target fail instead of being ignored.

The [browser package][package-targets] defines that optional target and
registers its provider through `ComputeTestingPlatformRunArgumentsDependsOn`.
For enabled `browser-*` projects it wraps the final host command without
modifying `RunCommand`, `RunArguments` or `RunWorkingDirectory`.
Ordinary `dotnet run` and desktop launch behavior remain unchanged.

The [SDK capability][sdk-targets] is `TestingPlatformRunArgumentsVersion=1`;
the package provider requires it. There is no legacy `AfterTargets` ordering
or `DotnetTestInvocation` fallback. This is an implemented cross-repository
contract awaiting SDK merge and dependency flow, not support in older SDKs.

The package preserves the evaluated command through private URI-encoded
options and starts it without a shell. Encoding preserves arguments; it is
not secrecy. Its [worker][worker] also URL-decodes WasmAppHost's repeated
`arg=` query values in order, prepending them to SDK-supplied application
arguments while leaving the expanded SDK bootstrap last. This preserves
project-configured `TestingPlatformCommandLineArguments`, including filters.

## Host startup and managed protocol

The [package targets][package-targets] register reserved Static Web Assets
under `/_mtp/` with `AssetKind="Build"`. The worker navigates to
`/_mtp/browser-host.html`, preserving the consumer's root page and excluding
the test host assets from publication. This is not published-layout testing
or a framework-page adapter.

The [host process][host] recognizes WasmAppHost's stdout `App url:` with an
HTTP loopback URL. Generic server logs, HTTPS URLs and stderr do not establish
readiness. Early exit or timeout fails launch; no fallback server or new
readiness-file protocol is used. The compatibility gate exercises this exact
stdout dependency against the runtime pack selected by the SDK build; it is
not a structured runtime-owned readiness API.

The [SDK application][application] appends a protected HTTP bootstrap
response-file reference. The [package parser][options] expands that final
file; user-authored response files remain unsupported. The worker supplies
arguments through private top-level-page bindings and returns the managed
exit code or launcher failure. A terminal binding is neither an MTP handshake
nor proof that any tests were reported; the SDK retains result aggregation.

The [wire contract][protocol] supports protocol versions through `1.5.0`.
The newer version alone does not enable optional behavior: the corresponding
SDK handshake capabilities are required.

| SDK handshake capability | Behavior |
| --- | --- |
| `ServerControlTransport=http` (property 20) | A second authenticated MTP HTTP client polls for existing control messages, independently of the results client. |
| `FileArtifactTransfer=chunked-v1` (property 21) | Managed MTP may upload local artifact bytes before reporting host artifact metadata. |
| Transfer limits (properties 22-25) | Advertise maximum chunk, file and module bytes, and file count. The receiver remains authoritative. |

[Serializer IDs][wire-fields] 13/14 retain the control request/message
format; 15/16 add artifact transfer request/reply. Without the HTTP control
capability, MTP does not start that channel. Without usable artifact-transfer
capability/limits, it warns that export is unavailable and does not report
browser-local paths as host files. An incompatible handshake is not replaced
by a launcher-success shortcut.

## Cancellation and owned process lifecycle

The [SDK][application] completes each HTTP control poll with a pending
control message or, after 30 seconds, a `VoidResponse` meaning no command.
MTP repolls after that response. The [gateway][gateway] dispatches requests
concurrently so parked polls cannot block results or artifacts, and drains
active requests during shutdown.

MTP connects the negotiated channel to its existing
[graceful-stop capability][common-host]. SDK cancellation policy can request
managed shutdown while result/artifact communication remains live, then
force termination after its grace period. The existing
[two-stage Ctrl+C handling][ctrl-c] remains: first request cooperative
cancellation and stop scheduling; a second press forces exit.
A blocked browser thread may not process managed cancellation, so graceful
completion and artifact flushing cannot be guaranteed after force termination.

The [supervisor][supervisor] starts a package-private worker behind a startup
barrier. Before the worker can launch WasmAppHost, Playwright or Chromium,
the package establishes [process containment][containment]:

- Windows: attach the worker to an owned kill-on-close job.
- POSIX: create and verify a worker-owned process group.

The worker monitors the parent's private input pipe. Parent loss triggers
cancellation and, on POSIX, eventual process-group termination through a
[separate monitor thread][parent-monitor]. The supervisor bounds startup
and shutdown, attempts orderly cleanup, then terminates owned resources.
This provides ownership of a wedged private driver without replacing
Playwright or depending solely on its asynchronous disposal.

Containment is not a sandbox for hostile tests or a guarantee against
external tooling simultaneously applying uncatchable termination to every
supervisor and worker. Forceful shutdown may leave caller-owned temporary
files; process cleanup and the reproduction recipe's file cleanup are separate.

## Artifact export and host file ownership

Managed [MTP][connection] reads reported VFS artifacts through managed file
APIs and sends ordered chunks using the existing authenticated transport.
Requests identify the execution, instance and transfer, with a basename,
declared length, offset, bytes and final flag. Replies acknowledge the next
offset and return a host path only after commit. MTP substitutes acknowledged
host paths into the existing artifact metadata, including input-artifact paths.

The [SDK receiver][transfers] requires a valid test-host handshake and stages
data beneath SDK-selected module and transfer directories in the results
directory. Caller filenames cannot select directories. It rejects invalid
names, traversal, inconsistent lengths/offsets, repeated completed transfers,
overwrite and quota violations. Only complete ordered data is moved from
`.partial` to its final path. Pending files are cleaned on abort/disposal,
and failures remain errors rather than successful artifact reports.
The [application][application] accepts artifact paths only from its committed
transfer set; arbitrary reported browser paths are not trusted host paths.

[Default receiver limits][limits] are 1 MiB per chunk, 1 GiB per file,
4 GiB and 10,000 files per module. Positive integer environment overrides are:

- `DOTNET_CLI_TEST_ARTIFACT_MAX_FILE_BYTES`
- `DOTNET_CLI_TEST_ARTIFACT_MAX_MODULE_BYTES`
- `DOTNET_CLI_TEST_ARTIFACT_MAX_FILE_COUNT`

Invalid overrides fail explicitly. MTP honors advertised limits and retains
local safety caps; increasing a receiver limit does not promise unlimited
producer output. Quota failures cannot silently drop a file and report success.

Host configuration-file transfer and browser-side artifact merging are not
provided. Browser hosts do not advertise
[artifact postprocessors][common-host], and the
[SDK excludes WebAssembly merge jobs][postprocessing].
Exported originals remain available to host tooling; export is not a claim
that every reporting, coverage or file-dependent extension runs in a browser.

## Trust, dependencies and release gates

The model assumes trusted test projects and dependencies: their evaluated
host command executes on the machine. The SDK uses a current-user-protected
bootstrap file; the [reader][options] rejects detected reparse points and
checks Unix permissions, but is not a general hostile-file parser or complete
replacement-race defense.

Bootstrap credentials stay out of URLs and static assets. Private bindings
check the expected page, top-level frame and exact origin, not the identity
of scripts already executing there. Those scripts remain trusted.
Known bootstrap values are redacted from diagnostics and Playwright `DEBUG`
logging is disabled. The [gateway][gateway] authenticates protocol POSTs;
CORS/Private Network Access preflights are unauthenticated and can establish
the first accepted origin. There is no launcher-origin preregistration or
CSP rewriting.

The [optional package][package-project] privately references Playwright and
bundles its required managed/JavaScript dependencies and platform Node
payloads, excluding unused UI/type/font assets. Browser executables remain
external prerequisites. None of this browser payload is added to the SDK
layout or core MTP.

Distribution builds can set `TestingPlatformBrowserBundlePlaywrightNode=false`
when packing; consumers can supply an approved host Node executable through
[`TestingPlatformBrowserNodeExecutable`][node]. This is a packaging seam,
not completed source-build, offline-use or third-party intake approval.

The [validated runtime scope][package-doc] is Windows x64 and Debian Linux x64
with installed Chromium-family browsers. Other OS/architecture combinations
have compile or package-layout evidence only. SDK/runtime/MTP/browser versions
must be aligned; bundled RID payloads and the launcher's TFM do not establish
a broader supported matrix.

Official signing, macOS notarization, publication, SDK merge/dependency flow,
license/SBOM and source-build approval, and servicing ownership remain external
release gates. Arcade signing dry runs validate policy/container handling,
not officially signed artifacts. Keep one owner responsible through release
branch validation and downstream enablement.

Exact commands, package identities/hashes, exit codes and result artifacts
belong with the implementation PRs, not test-count tables in this design.
Evidence must exercise the composed SDK and packages, including failure,
legacy capabilities, cancellation, quotas, mixed modules and cleanup.

## Continuous compatibility gate

Windows/Linux composition jobs participate in each repository's ordinary
public-CI aggregate: [SDK wiring][sdk-ci] and [jobs][sdk-browser-jobs],
and [TestFX wiring][testfx-ci] and [steps][testfx-browser-steps].
SDK CI builds its current source with the full-SHA
[TestFX counterpart pin][testfx-pin]; TestFX CI builds its current source
with its full-SHA [SDK counterpart pin][sdk-pin], invoking the same SDK-owned
runner. Cross-repository changes must deliberately update these pins and
validate both compositions before merge, not substitute floating branches
or repository-local test results. Build-only or filtered CI success is not
evidence that browser acceptance ran.

The [runner][compatibility-runner] builds the real shipping SDK using
[`source-build.slnf`][sdk-product-filter] for project selection only, **not
source-build mode**. It invokes TestFX's root pack build with
[`BrowserTestingPackages.slnf`][testfx-package-filter], including the late
analyzer generator dependency, then evaluates the acceptance project's
`NetCurrent` and `TargetPath`. Windows packaging retains desktop MSBuild
for UWP assets. Both repositories use .NET build-pool images:
`windows.vs2026preview.scout.amd64.open` with Edge and
`build.ubuntu.2204.amd64.open` with Chrome. They do not delete SDK outputs
or preinstalled system tools to work around worker disk limits.

On Linux CI workers, the shared [`InstallBrowser.sh`][browser-install]
provisions Chrome only when it is absent, checking the pinned Google
primary-key fingerprint and using its signed APT repository. This helper
is restricted to disposable Azure Pipelines workers. The local
`RunBrowserTests.ps1` entry point still requires an explicitly supplied,
installed browser and does not provision one.

Strict mode (`TESTINGPLATFORM_BROWSER_REQUIRED=1`) makes missing browser,
SDK or aligned package prerequisites failures rather than inconclusive
results. The [fixture][browser-acceptance] selects the exact SDK version and
commit supplied by the runner, uses an isolated package cache, and
[compares all eight restored package SHA-512 hashes][restore-identity] with
the staged package bytes. The runner rejects dirty or drifting source,
stale/mixed package commits, reused results directories and changed package
snapshots. Commands, exit codes, source/browser identities, package hashes,
TRX, logs and binlogs are retained as evidence.

The [TRX validator][result-validator] requires a nonempty, completed passing
run with consistent counters. Every case in TestFX's maintained
[`required-tests.json`][required-tests] must occur exactly once; a missing
or duplicated required case, any failed/skipped/inconclusive result, or
missing/malformed/empty TRX fails the gate. Intentional test-application
failures and skips are scenarios asserted by passing outer acceptance cases,
not permission to skip the compatibility gate itself.

The change author owns the paired-source fix and evidence through dependency
flow. These gates continuously check the composed contract and readiness
dependency; they do not replace the external release and servicing approvals
above or expand the validated runtime matrix.

## Outside this design

Blazor/UI/component testing, framework-owned startup, WASI hosting, VSTest,
`--test-modules` without project evaluation, browser reuse, hot reload,
managed browser debugging and dynamic assembly loading remain outside scope.
Published-layout execution and alternate runtime modes need separate validation.
Host unification is discussed separately in
[dotnet/runtime#122144](https://github.com/dotnet/runtime/issues/122144) and
[dotnet/aspnetcore#67814](https://github.com/dotnet/aspnetcore/issues/67814);
this design reuses WasmAppHost rather than adding another server.

[sample-project]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/poc/browser-wasm-unit-tests/BrowserWasmTestApp.csproj
[sample-test]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/poc/browser-wasm-unit-tests/BrowserWasmTests.cs
[sample-readme]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/poc/browser-wasm-unit-tests/README.md
[mstest-package]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Package/MSTest/MSTest.csproj
[entrypoint]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.MSBuild/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.targets
[selection]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Cli/dotnet/Commands/Test/MTP/SolutionAndProjectUtility.cs
[sdk-targets]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Tasks/Microsoft.NET.Build.Tasks/sdk/Sdk.targets
[application]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs
[gateway]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Cli/dotnet/Commands/Test/MTP/IPC/HttpTestHostGateway.cs
[ctrl-c]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Cli/dotnet/Commands/Test/MTP/CtrlCCancellationManager.cs
[transfers]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Cli/dotnet/Commands/Test/MTP/FileArtifactTransferManager.cs
[limits]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Cli/dotnet/Commands/Test/MTP/FileArtifactTransferLimits.cs
[postprocessing]: https://github.com/dotnet/sdk/blob/5b0e5fbe3af389fc93edc920c9982ea0ac2c20c2/src/Cli/dotnet/Commands/Test/MTP/ArtifactPostProcessingManager.cs
[connection]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/DotnetTestConnection.cs
[common-host]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs
[protocol]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/IPC/Constants.cs
[wire-fields]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/IPC/ObjectFieldIds.cs
[package-targets]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/buildMultiTargeting/Microsoft.Testing.Platform.Browser.targets
[package-project]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/Microsoft.Testing.Platform.Browser.csproj
[package-doc]: https://github.com/microsoft/testfx/blob/b41d763f0072c6f1cd07b6d8173b1d0097ad6c0d/src/Platform/Microsoft.Testing.Platform.Browser/PACKAGE.md
[boot]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/buildMultiTargeting/assets/Microsoft.Testing.Platform.Browser.main.js
[host]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/HostProcess.cs
[options]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/BrowserLauncherOptions.cs
[worker]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/BrowserWorker.cs
[supervisor]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/BrowserSupervisor.cs
[containment]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/WorkerProcessContainment.cs
[parent-monitor]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/WorkerParentMonitor.cs
[node]: https://github.com/microsoft/testfx/blob/b6c7cac92f7eeb788ae026471239ff110207bc24/src/Platform/Microsoft.Testing.Platform.Browser/PlaywrightNodeExecutable.cs
[compatibility-runner]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/eng/browser-testing/RunBrowserTests.ps1
[sdk-ci]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/.vsts-pr.yml
[sdk-browser-jobs]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/eng/pipelines/templates/jobs/browser-testing.yml
[testfx-pin]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/eng/browser-testing/testfx.json
[sdk-product-filter]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/source-build.slnf
[result-validator]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/eng/browser-testing/AssertBrowserTestResults.ps1
[browser-install]: https://github.com/dotnet/sdk/blob/66309ab55fce95ee42e5e2236c02f7371332dbf8/eng/browser-testing/InstallBrowser.sh
[testfx-ci]: https://github.com/microsoft/testfx/blob/c81b63af69e367a826f614f5728e9f0c50d576fa/azure-pipelines.yml
[testfx-browser-steps]: https://github.com/microsoft/testfx/blob/c81b63af69e367a826f614f5728e9f0c50d576fa/eng/pipelines/steps/browser-testing.yml
[sdk-pin]: https://github.com/microsoft/testfx/blob/c81b63af69e367a826f614f5728e9f0c50d576fa/eng/browser-testing/sdk.json
[testfx-package-filter]: https://github.com/microsoft/testfx/blob/303a5d8c22e9ef955c8a7c676c9818ea6fc27e99/BrowserTestingPackages.slnf
[browser-acceptance]: https://github.com/microsoft/testfx/blob/303a5d8c22e9ef955c8a7c676c9818ea6fc27e99/test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserPackageExecutionTests.cs
[restore-identity]: https://github.com/microsoft/testfx/blob/303a5d8c22e9ef955c8a7c676c9818ea6fc27e99/test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserPackageRestoreIdentity.cs
[required-tests]: https://github.com/microsoft/testfx/blob/303a5d8c22e9ef955c8a7c676c9818ea6fc27e99/eng/browser-testing/required-tests.json
