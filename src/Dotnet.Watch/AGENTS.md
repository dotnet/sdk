# dotnet watch Agent Instructions

Guidance for changes under `src/Dotnet.Watch` (the `dotnet watch` tool and Hot
Reload).

## Where things live

| Path | Role |
|---------|------|
| `dotnet-watch` | The tool executable and CLI surface. Its command/options are defined in `CommandLine/DotnetWatchCommandDefinition.cs`. |
| `Watch` (`Microsoft.DotNet.HotReload.Watch`) | Core watcher library: file-set computation, process launching, Hot Reload, app models. |
| `DotNetWatchTasks` | MSBuild task bundled into the tool for design-time file collection. |
| `DotNetDeltaApplier`, `Web.Middleware`, `BrowserRefresh` | Assemblies injected into compatible non-Gateway app models through the hosting-startup path. |
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
- **Standalone .NET 11+ Blazor WASM uses a Gateway-native browser-tools path.**
  [`BlazorWebAssemblyAppModel.cs`](Watch/AppModels/BlazorWebAssemblyAppModel.cs)
  configures the Gateway's YARP route instead of injecting BrowserRefresh startup
  hooks. The provider endpoints and generation-scoped replay live under
  [`HotReloadClient/Web`](HotReloadClient/Web), while the WebAssembly initializer
  discovers the provider before enabling runtime mutability. Keep the reserved route
  root-relative (outside the application's Gateway path base), require the encrypted
  shared-secret WebSocket subprotocol, and include the generation identity in live
  managed-update messages. Preserve the initializer's injected-script fallback and the
  legacy hosting-startup configurator for other app models and older target frameworks.
- **`CompilationHandler` drives Roslyn** via
  `Microsoft.CodeAnalysis.ExternalAccess.HotReload`; unsupported edits fall
  back to a full rebuild + restart.

## Tests

- `test/dotnet-watch.Tests`. Parallelism is **ClassLevel by design** —
  don't switch to method-level; these are heavy process-spawning tests and it causes
  Helix timeouts.
- `InProcTestWatcher` runs the watcher in-process with a mocked process launcher.
