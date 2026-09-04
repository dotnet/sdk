# dotnet watch Agent Instructions

Guidance for changes under `src/Dotnet.Watch` (the `dotnet watch` tool and Hot
Reload).

## Where things live

| Path | Role |
|---------|------|
| `dotnet-watch` | The tool executable and CLI surface. Its command/options are defined in `CommandLine/DotnetWatchCommandDefinition.cs`. |
| `Watch` (`Microsoft.DotNet.HotReload.Watch`) | Core watcher library: file-set computation, process launching, Hot Reload, app models. |
| `DotNetWatchTasks` | MSBuild task bundled into the tool for design-time file collection. |
| `DotNetDeltaApplier`, `Web.Middleware`, `BrowserRefresh` | Assemblies injected into supported running apps via `DOTNET_STARTUP_HOOKS`; modern standalone Blazor WebAssembly uses the browser initializer instead. |
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
- **Standalone Blazor WebAssembly bootstrap differs by TFM.** .NET 11+ receives the
  browser-refresh WebSocket endpoints and public key in a launch URL fragment. The
  `Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js` initializer consumes
  and removes that fragment and retains applied deltas in watcher-scoped session storage
  for page reloads. Older, hosted, initializer-disabled, and non-automatically-launched
  apps retain the injected middleware path. Never place the browser-generated shared
  secret in the launch URL.
- **`CompilationHandler` drives Roslyn** via
  `Microsoft.CodeAnalysis.ExternalAccess.HotReload`; unsupported edits fall
  back to a full rebuild + restart.

## Tests

- `test/dotnet-watch.Tests`. Parallelism is **ClassLevel by design** —
  don't switch to method-level; these are heavy process-spawning tests and it causes
  Helix timeouts.
- `InProcTestWatcher` runs the watcher in-process with a mocked process launcher.
