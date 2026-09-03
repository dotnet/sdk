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
  state.** Its fixed `/_framework/dotnet-browser-tools` HTTP/WebSocket endpoints live
  under [`HotReloadClient/Web`](HotReloadClient/Web). All application hosts, including
  standalone Blazor WebAssembly development servers, use the shared-framework-only
  [`BrowserToolsForwarder`](Web.Middleware/BrowserToolsForwarder.cs). Keep destinations
  fixed to the trusted loopback provider address, the route root-relative, and the
  encrypted shared-secret WebSocket subprotocol intact. Do not add YARP to arbitrary
  applications.
- **Activation is app-model-specific, but the browser client is shared.** MVC and Razor
  Pages use
  [`BrowserRefreshTagHelperComponent`](Web.Middleware/BrowserRefreshTagHelperComponent.cs)
  through the built-in body TagHelper; it never runs for `.razor` root components, so
  Blazor apps rendered on the server rely on the Web SDK's build-only `afterWebStarted`
  initializer instead. Standalone and hosted Blazor WASM use a build-only watch activation
  initializer added by the WASM SDK for every supported target
  framework; it marks the app as running under watch and imports the provider-hosted
  browser client from `onRuntimeReady`, so the runtime's apply API exists before updates
  replay. On .NET 10+ the separate Hot Reload agent initializer applies managed
  updates; older target frameworks fall back to the runtime's own
  `window.Blazor._internal.applyHotReload`. Both initializers can run in the same app;
  duplicate activation is absorbed by module caching and the client's injection sentinel.
  Static/custom HTML that has no supported
  initializer requires user-provided activation; do not add build-time `index.html`
  rewriting. The browser client itself is embedded in and served by the provider, so do
  not add it back to the application's static asset graph.
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
