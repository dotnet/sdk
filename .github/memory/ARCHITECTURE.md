---
coverage: Product boundaries, major components, process entry points, and build and distribution flow
---

# Architecture

`dotnet/sdk` produces the managed and Native AOT portions of the `dotnet` CLI, the
MSBuild SDK logic that builds .NET projects, related tools and templates, and the
layout that combines those components into a runnable SDK. The complete local product
is assembled under `artifacts/bin/redist/<configuration>/dotnet`; see the
[`redist` project](../../src/Layout/redist/redist.csproj) and its
[layout imports](../../src/Layout/redist/targets/Directory.Build.targets).

## Ownership Boundaries

Symptoms visible through `dotnet` CLI are not necessarily implemented here. Establish the
owning component before updating the SDK.

| Repository | Owns |
| --- | --- |
| [`dotnet/runtime`](https://github.com/dotnet/runtime) | CLR and Mono, the BCL, native host/muxer and apphost, runtime/reference packs, NativeAOT, and ILLink |
| [`dotnet/roslyn`](https://github.com/dotnet/roslyn) | C# and Visual Basic compilers, compiler diagnostics, compiler servers, and compiler code generation |
| [`dotnet/fsharp`](https://github.com/dotnet/fsharp) | F# compiler and F#-specific tooling |
| [`dotnet/msbuild`](https://github.com/dotnet/msbuild) | MSBuild engine, evaluation/execution semantics, logging, and core tasks |
| [`NuGet/NuGet.Client`](https://github.com/NuGet/NuGet.Client) | Restore, package resolution, protocols, and NuGet-owned MSBuild tasks |
| [`dotnet/project-system`](https://github.com/dotnet/project-system) | Visual Studio-specific project-system behavior |
| [`dotnet/dotnet`](https://github.com/dotnet/dotnet) | VMR synchronization and integrated product build; normal component development remains in the owning repository |

Do not infer ownership from a diagnostic ID or generated code alone. C# and Visual Basic
compiler diagnostics and compiler-emitted code belong to Roslyn, but analyzers and source
generators belong to the repository that implements them, such as `dotnet/runtime` for
runtime-library generators or `dotnet/sdk` for SDK analyzers.

## Key Abstractions

### Shared CLI Definition Tree

[`DotNetCommandDefinition`](../../src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/DotNetCommandDefinition.cs)
is the command registry. [`Parser.cs`](../../src/Cli/dotnet/Parser.cs) attaches managed
or AOT actions to that common tree. Keep definitions dependency-light and AOT-safe;
managed-only behavior is excluded from the AOT build with `#if !CLI_AOT`.

### MSBuild SDK Import Pair

A project using `Sdk="Microsoft.NET.Sdk"` imports
[`Sdk.props`](../../src/Tasks/Microsoft.NET.Build.Tasks/sdk/Sdk.props) before project
evaluation and [`Sdk.targets`](../../src/Tasks/Microsoft.NET.Build.Tasks/sdk/Sdk.targets)
after it. Those files select language and cross-targeting imports, then compose the
shipping targets and tasks. Specialized SDKs expose the same `Sdk.props`/`Sdk.targets`
shape; see [API_MAP.md](API_MAP.md#msbuild-sdk-entry-points).

### `dotnet watch` Browser-Tool Activation

`dotnet watch` owns browser-tool availability. The browser authenticates the provider with
an RSA public key that the build pins into the application, so **the executable
browser-tools JavaScript must come from the application's own build output and never from
the provider being authenticated**; downloading the client from that provider would make
the authentication meaningless.

[`BrowserRefreshServerFactory`](../../src/Dotnet.Watch/Watch/Browser/BrowserRefreshServerFactory.cs)
creates one RSA keypair per `dotnet watch` invocation, before any project is built. Only
the base64 `SubjectPublicKeyInfo` half reaches MSBuild, through the reserved
`DotNetWatchBrowserToolsPublicKey` property alongside `DotNetWatchBrowserTools`; see
[`ReservedBuildProperties`](../../src/Dotnet.Watch/Watch/Build/ReservedBuildProperties.cs),
[`EvaluationResult.GetGlobalBuildProperties`](../../src/Dotnet.Watch/Watch/Build/EvaluationResult.cs)
and [`BuildEvaluator`](../../src/Dotnet.Watch/dotnet-watch/Watch/BuildEvaluator.cs). The
private key stays in the watch process; the 32-byte secret the browser generates is never
persisted and travels only RSA-OAEP encrypted as the WebSocket subprotocol.

[`Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets`](../../src/StaticWebAssetsSdk/Targets/Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets)
turns those two properties into build-only static web assets under
`obj/<configuration>/<tfm>/dotnet-watch/`: the SDK-specific activation initializer, the
[browser-tools client](../../src/StaticWebAssetsSdk/Targets/DotNetWatch/dotnet-watch-browser-tools.js),
and a configuration module generated from a
[checked-in template](../../src/StaticWebAssetsSdk/Targets/DotNetWatch/dotnet-watch-browser-tools.config.js.template)
that pins the public key and the fixed `/_framework/dotnet-browser-tools` route. The assets
are `AssetKind=Build` with `CopyToPublishDirectory=Never`, are tracked through `FileWrites`,
and are written only when their content changes so that the stable per-invocation key keeps
rebuilds incremental. Publish output and plain builds contain none of them. Apps that
disable `StaticWebAssetsEnabled` or `JSModulesEnabled` therefore cannot receive browser
tools.

The [WebAssembly SDK](../../src/WasmSdk/Sdk/Sdk.targets) and the
[Web SDK](../../src/WebSdk/Web/Targets/Sdk.Server.targets) opt in by naming their asset
prefix and initializer
([WebAssembly module](../../src/WasmSdk/Sdk/DotNetWatch/Microsoft.NET.Sdk.WebAssembly.DotNetWatch.lib.module.js),
[Web module](../../src/WebSdk/Web/Targets/DotNetWatch/Microsoft.NET.Sdk.Web.DotNetWatch.lib.module.js)).
The WebAssembly initializer signals the Hot Reload agent through the watch-private
`__DOTNET_WATCH_BROWSER_TOOLS` runtime configuration variable rather than a shared global
or the legacy `__ASPNETCORE_BROWSER_TOOLS` switch, and must not capture globals at module
evaluation because Blazor runs every `onRuntimeConfigLoaded` before any `onRuntimeReady`
and does not guarantee initializer load order. MVC and Razor Pages responses are activated
by
[`BrowserRefreshTagHelperComponent`](../../src/Dotnet.Watch/Web.Middleware/BrowserRefreshTagHelperComponent.cs),
which does not run for `.razor` root components. Activating more than once is harmless:
module imports are cached per URL and the browser client keeps its own injection sentinel.

Application hosts reach the provider through the shared
[`BrowserToolsForwarder`](../../src/Dotnet.Watch/Web.Middleware/BrowserToolsForwarder.cs),
which
[`WebApplicationAppModel`](../../src/Dotnet.Watch/Watch/AppModels/WebApplicationAppModel.cs)
installs through the hosting-startup path. Standalone WebAssembly projects are the
exception: they are served by the Blazor Gateway, a separate YARP host that does not
activate ASP.NET Core hosting startups, so
[`BlazorWebAssemblyAppModel`](../../src/Dotnet.Watch/Watch/AppModels/BlazorWebAssemblyAppModel.cs)
configures a gateway reverse-proxy route to the provider through `ReverseProxy__*`
environment variables instead.

The provider serves no JavaScript. Its remaining HTTP surface is the `/connect` WebSocket
and `/clear-cache`; see
[`BrowserToolsEndpointRouter`](../../src/Dotnet.Watch/HotReloadClient/Web/BrowserToolsEndpointRouter.cs).
There is no session descriptor, protocol version negotiation, HTTP replay endpoint, or wire
level generation id: replay is serialized on the authenticated WebSocket, which sends the
current snapshot first and releases live messages only after the browser acknowledges it.
That gate is per connection, so the provider fans out to connected browsers in parallel and
one slow or unacknowledged browser cannot delay delivery to the others. All supported target
frameworks use this contract; there is no parallel legacy
response-rewriting path.

### Resolver Plugins

MSBuild loads [`DotNetMSBuildSdkResolver`](../../src/Resolvers/Microsoft.DotNet.MSBuildSdkResolver/MSBuildSdkResolver.cs)
to select an SDK installation and
[`WorkloadSdkResolver`](../../src/Resolvers/Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver/WorkloadSdkResolver.cs)
to resolve workload-provided SDKs. Resolver state must be thread-safe and work in both
.NET MSBuild and Visual Studio/.NET Framework hosts; see
[`src/Resolvers/AGENTS.md`](../../src/Resolvers/AGENTS.md).

### Redist Composition

[`redist.csproj`](../../src/Layout/redist/redist.csproj) creates build-ordering edges to
the CLI, tasks, resolvers, specialized SDKs, workloads, tools, templates, and analyzers.
Its `Bundled*.targets` files declare what ships, while `Generate*.targets` files control
layout and packaging. This is composition, not the normal home for product behavior.

## Primary Build Flow

1. The native host selects an SDK. The managed CLI starts in
   [`Program.Main`](../../src/Cli/dotnet/Program.cs), while supported Native AOT paths
   start in [`NativeEntryPoint`](../../src/Cli/dotnet-aot/NativeEntryPoint.cs).
2. Both paths use the shared command tree. The managed parser attaches handlers;
   unsupported AOT operations continue in the managed CLI.
3. Built-in commands execute their handlers. Unmatched managed commands are resolved as
   external `dotnet-*` commands, with file-based C# execution as another fallback.
4. Build-family commands forward to MSBuild. MSBuild loads the SDK resolvers, then imports
   `Sdk.props`, evaluates the project, and imports `Sdk.targets`.
5. Shipping targets invoke SDK tasks and integrate compiler, NuGet, runtime, and workload
   artifacts owned by this and adjacent repositories.
6. The repository build compiles the components first, then the redist project copies them
   into the runnable SDK layout.

## Lifecycle Boundaries

The CLI has three equal entry points: managed CLI, Native AOT CLI, and MSBuild logger.
Code reachable from the logger must not assume `Program.Main` initialized process-wide
state. Treat `BuildStarted`/`BuildFinished` as request boundaries; `Shutdown` ends one
logger instance, not necessarily the process. The canonical details are in
[`src/Cli/AGENTS.md`](../../src/Cli/AGENTS.md#sdk-process-entry-points).
