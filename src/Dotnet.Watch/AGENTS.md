# dotnet watch Agent Instructions

Guidance for changes under `src/Dotnet.Watch` (the `dotnet watch` tool and Hot
Reload).

## Where things live

| Path | Role |
|---------|------|
| `dotnet-watch` | The tool executable and CLI surface. Its command/options are defined in `CommandLine/DotnetWatchCommandDefinition.cs`. |
| `Watch` (`Microsoft.DotNet.HotReload.Watch`) | Core watcher library: file-set computation, process launching, Hot Reload, app models. |
| `DotNetWatchTasks` | MSBuild task bundled into the tool for design-time file collection. |
| `DotNetDeltaApplier`, `Web.Middleware`, `BrowserRefresh` | Assemblies injected into compatible app models through the hosting-startup path. |
| `HotReloadAgent.*`, `HotReloadClient`, `AspireService` | Shared code consumed via `.projitems`.|

## Conventions & gotchas

- **Shared source via `.projitems`.** Several folders share code through
  `*.projitems` imported into multiple projects (not NuGet packages). Before
  refactoring shared files, check every importer.
- **`Watch/RuntimeDependencies.props` controls tool output layout** (the
  `hotreload/<tfm>/…` paths). It must stay in sync with `GetStartupHookPath` in
  `Watch/AppModels/HotReloadAppModel.cs` — a mismatch makes the agent silently fail
  to load and breaks tests.
- **Hot Reload protocol differs per app model.** .NET Core apps use a binary
  named-pipe protocol; Blazor WASM uses JSON over WebSocket. Each protocol has its
  own `HotReloadClient` subclass (e.g. `DefaultHotReloadClient`,
  `WebAssemblyHotReloadClient`); a new app model may need its own implementation.
- **The browser-tools provider owns the browser protocol and replay
  state.** Its fixed `/_framework/dotnet-browser-tools` endpoints live under
  [`HotReloadClient/Web`](HotReloadClient/Web) and are reduced to `connect` (WebSocket)
  and `clear-cache`; the provider serves no JavaScript, no session descriptor, no protocol
  version, and no HTTP replay. Most application hosts reach it through the
  shared-framework-only [`BrowserToolsForwarder`](Web.Middleware/BrowserToolsForwarder.cs)
  installed by the hosting-startup path. Standalone Blazor WebAssembly is the exception:
  it is served by the Blazor Gateway, a separate YARP host that does not activate ASP.NET
  Core hosting startups, so
  [`BlazorWebAssemblyAppModel`](Watch/AppModels/BlazorWebAssemblyAppModel.cs) configures a
  gateway reverse-proxy route through `ReverseProxy__*` environment variables instead. Keep
  destinations fixed to the trusted loopback provider address, the route root-relative, and
  the encrypted shared-secret WebSocket subprotocol intact. Do not add YARP to arbitrary
  applications.
- **The browser authenticates the provider, so the provider must not serve code.**
  `dotnet watch` creates one RSA keypair per invocation in
  [`BrowserRefreshServerFactory`](Watch/Browser/BrowserRefreshServerFactory.cs) before any
  project is built, and only the public half travels to MSBuild through
  [`ReservedBuildProperties`](Watch/Build/ReservedBuildProperties.cs). The build pins that
  key into an application-hosted configuration module and the client is an application
  static asset; see
  [`Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets`](../StaticWebAssetsSdk/Targets/Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets).
  Never move executable browser-tools code back into the provider, never let the private
  key or the browser's shared secret reach disk, a build property, or a log, and keep the
  generated assets build only.
- **Replay lives in the WebSocket handshake.** The provider sends the current snapshot as
  the first message on an accepted connection and releases live messages for that
  connection only after the browser acknowledges it, so there is no wire-level generation
  id. The internal epoch in
  [`AbstractBrowserRefreshServer`](HotReloadClient/Web/AbstractBrowserRefreshServer.cs)
  exists only to drop work produced by a superseded client; do not surface it on the wire.
- **Activation is app-model-specific, but the browser client is shared.** MVC and Razor
  Pages use
  [`BrowserRefreshTagHelperComponent`](Web.Middleware/BrowserRefreshTagHelperComponent.cs)
  through the built-in body TagHelper; it never runs for `.razor` root components, so
  Blazor apps rendered on the server rely on the Web SDK's build-only `afterWebStarted`
  initializer instead. Standalone and hosted Blazor WASM use a build-only watch activation
  initializer added by the WASM SDK for every supported target
  framework; from `onRuntimeConfigLoaded` it sets the watch-private activation variable
  `__DOTNET_WATCH_BROWSER_TOOLS` and `DOTNET_MODIFIABLE_ASSEMBLIES`, and from
  `onRuntimeReady` it imports the application-hosted configuration module. Blazor guarantees
  every `onRuntimeConfigLoaded`
  callback runs before any `onRuntimeReady` callback and passes the same config object to
  all initializers, so pass *activation state* between initializer modules through that
  config rather
  than through `globalThis` or a cross-module import: initializer load order is unspecified
  and each module's URL is subject to fingerprinting. Do not use
  `__ASPNETCORE_BROWSER_TOOLS` as that handshake, because .NET 8 interprets it as a request
  to load the removed application-hosted `blazor-hotreload.js`. On .NET 10+ the
  separate Hot Reload agent initializer applies managed
  updates; older target frameworks fall back to the runtime's own
  `window.Blazor._internal.applyHotReload`. Both initializers can run in the same app;
  duplicate activation is absorbed by module caching and the client's injection sentinel.
  Static/custom HTML that has no supported
  initializer requires user-provided activation; do not add build-time `index.html`
  rewriting. Apps that disable `StaticWebAssetsEnabled` or `JSModulesEnabled` cannot host
  the client and therefore cannot receive browser tools.
- **The replay handshake waits for the Hot Reload agent through a `globalThis`
  rendezvous.** The provider sends the replay snapshot exactly once, so applying it before
  the agent installed `window.Blazor._internal.applyHotReloadDeltas` would drop those
  updates while the browser still acknowledged success. Both `onRuntimeReady` callbacks run
  unordered, so the agent publishes
  `globalThis.__DOTNET_WATCH_HOT_RELOAD_AGENT = { ready, setReady }` synchronously before
  its first `await` and resolves it in a `finally`, including on its disabled early-return
  path, and the client awaits it before replaying. This is the one case that cannot use the
  runtime config object, because the value is a promise that only exists once
  `onRuntimeReady` has started. The helper is duplicated in
  [`Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js`](HotReloadAgent.WebAssembly.Browser/wwwroot/Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js)
  and in the Static Web Assets SDK's `dotnet-watch-browser-tools.js`; keep the two in sync.
  The wait is bounded and best effort so runtimes that install the apply API through their
  own bootstrap, or pages that never boot WebAssembly, degrade to a logged warning instead
  of a reload loop.
- **.NET 9 WebAssembly is the one target framework that needs
  `__ASPNETCORE_BROWSER_TOOLS`.** Its `WebAssemblyHotReload` creates the Hot Reload agent
  only inside `InitializeAsync`, which the runtime calls only when that variable is set, so
  without it `applyHotReloadDeltas` returns an empty log and applies nothing while the
  browser still reports success. The
  [initializer template](../WasmSdk/Sdk/DotNetWatch/Microsoft.NET.Sdk.WebAssembly.DotNetWatch.lib.module.js.template)
  therefore has a `__RUNTIME_HOT_RELOAD_AGENT__` placeholder that
  [`Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets`](../StaticWebAssetsSdk/Targets/Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets)
  substitutes with `true` only when `TargetFrameworkVersion` equals `9.0`. .NET 8 must not
  get it (it would import the removed `blazor-hotreload.js`) and .NET 10+ must not get it
  (the SDK ships its own agent). That initialization path also fetches
  `/_framework/blazor-hotreload` for previously applied deltas, so
  [`HostingStartup`](Web.Middleware/HostingStartup.cs) answers that route locally with an
  empty update array: replay belongs to the authenticated WebSocket, and serving deltas over
  an unauthenticated route would be a security regression. The route is not forwarded to the
  provider, and its only consumer is the .NET 9 WebAssembly runtime.
- **The forwarder preserves the provider's pre-upgrade rejection.** The provider answers an
  invalid encrypted subprotocol with a status-only HTTP 400 *before* accepting the
  WebSocket. [`BrowserToolsForwarder`](Web.Middleware/BrowserToolsForwarder.cs) opts into
  `ClientWebSocketOptions.CollectHttpResponseDetails` and, on a failed upgrade, relays
  `ClientWebSocket.HttpStatusCode` only when it is an error status, falling back to 502
  otherwise. The narrowing matters: that property is recorded before the upgrade response is
  validated, so a handshake that failed *after* the provider switched protocols reports 101,
  and relaying it would make the application announce an upgrade it never performed. Both
  members are .NET 7+ while the
  assembly targets `net6.0`, so they are reached through cached reflection. Do not widen this
  into a blanket status mapping and do not swallow genuine transport failures.
- **Server hosting startup is intentionally thin.**
  [`HostingStartup.cs`](Web.Middleware/HostingStartup.cs) registers only the MVC/Razor
  TagHelper component and the reserved forwarder for the modern path. The BrowserRefresh
  assembly still appears in `DOTNET_STARTUP_HOOKS` so the out-of-application hosting
  startup assembly can be resolved, but its
  [`StartupHook`](Web.Middleware/StartupHook.cs) is a no-op. Managed server updates
  continue to use the separate `Microsoft.Extensions.DotNetDeltaApplier` startup hook.
  Do not restore response rewriting, application-hosted browser scripts, or parallel
  legacy endpoints.
- **`CompilationHandler` drives Roslyn** via
  `Microsoft.CodeAnalysis.ExternalAccess.HotReload`; unsupported edits fall
  back to a full rebuild + restart.

## Tests

- `test/dotnet-watch.Tests`. Parallelism is **ClassLevel by design** —
  don't switch to method-level; these are heavy process-spawning tests and it causes
  Helix timeouts.
- `InProcTestWatcher` runs the watcher in-process with a mocked process launcher.
