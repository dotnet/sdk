---
coverage: User-facing and extension surfaces across the CLI, MSBuild SDKs, tasks, resolvers, tools, templates, analyzers, and libraries
---

# API Map

This product repository has several contract types rather than one public library API.
Treat command names/options/help, MSBuild properties/items/targets/tasks, SDK resolver
behavior, package contents, templates, diagnostics, and shipped library APIs as public
surfaces. This map points to authoritative registries; it does not duplicate every
command or MSBuild property.

## CLI Surface

The authoritative root command and option registry is
[`DotNetCommandDefinition.cs`](../../src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/DotNetCommandDefinition.cs).
[`Parser.cs`](../../src/Cli/dotnet/Parser.cs) attaches managed and AOT actions, and
[`Program.cs`](../../src/Cli/dotnet/Program.cs) handles external-command and file-based-app
fallback.

| Surface | Examples | Definition / implementation |
| --- | --- | --- |
| Build and project lifecycle | `build`, `restore`, `publish`, `pack` | Definitions under [`Microsoft.DotNet.Cli.Definitions/Commands`](../../src/Cli/Microsoft.DotNet.Cli.Definitions/Commands); handlers under [`dotnet/Commands`](../../src/Cli/dotnet/Commands) |
| Project structure | `new`, `project`, `package`, `add`, `remove` | Shared definitions plus managed Template Engine/NuGet integrations |
| Tools and workloads | `tool`, `workload`, `sdk` | Shared definitions; handlers under [`dotnet/Commands`](../../src/Cli/dotnet/Commands) |
| Global options | `--info`, `--version`, `--list-sdks`, `--list-runtimes` | [`DotNetCommandDefinition`](../../src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/DotNetCommandDefinition.cs) |
| External commands | `dotnet-<name>` executable resolution | [`Program.ExecuteExternalCommand`](../../src/Cli/dotnet/Program.cs) and [`CommandFactory`](../../src/Cli/dotnet/CommandFactory) |

Changing command definitions can affect managed CLI behavior, Native AOT behavior,
`--help`, completions, telemetry, and snapshots. Follow
[`src/Cli/AGENTS.md`](../../src/Cli/AGENTS.md) and the [`add-cli-command` skill](../skills/add-cli-command/SKILL.md).

## MSBuild SDK Entry Points

| SDK surface | Entry points | Purpose |
| --- | --- | --- |
| `Microsoft.NET.Sdk` | [`src/Tasks/Microsoft.NET.Build.Tasks/sdk/Sdk.props`](../../src/Tasks/Microsoft.NET.Build.Tasks/sdk/Sdk.props), [`Sdk.targets`](../../src/Tasks/Microsoft.NET.Build.Tasks/sdk/Sdk.targets) | Core SDK evaluation, language target selection, cross-targeting, build, pack, publish, container, and ApiCompat imports |
| Razor | [`src/RazorSdk/Sdk`](../../src/RazorSdk/Sdk) | Razor compilation and project defaults |
| Web / Worker / Publish / ProjectSystem | [`src/WebSdk`](../../src/WebSdk) | ASP.NET Core web/worker project defaults, publishing, and project-system integration |
| Static Web Assets | [`src/StaticWebAssetsSdk/Sdk`](../../src/StaticWebAssetsSdk/Sdk) | Static asset build, publish, pack, compression, and endpoint integration |
| Blazor WebAssembly | [`src/BlazorWasmSdk/Sdk`](../../src/BlazorWasmSdk/Sdk) | Blazor WebAssembly build and publish integration |
| WebAssembly | [`src/WasmSdk/Sdk`](../../src/WasmSdk/Sdk) | General WebAssembly SDK integration |

For these surfaces, imported `.props` and `.targets`, documented MSBuild properties and
items, target names/hooks, task parameters, diagnostics, and output layout may be
consumed by project files, NuGet packages, IDEs, or other SDKs. Search the import chain
and tests before renaming or reordering them.

## MSBuild Tasks and Diagnostics

Shipping task classes and target registrations live under
[`src/Tasks/Microsoft.NET.Build.Tasks`](../../src/Tasks/Microsoft.NET.Build.Tasks).
Top-level targets are in its [`targets`](../../src/Tasks/Microsoft.NET.Build.Tasks/targets)
directory, with task registration in
[`Microsoft.NET.Sdk.Common.targets`](../../src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.Common.targets).

Public diagnostic contracts are sourced from
[`src/Tasks/Common/Resources/Strings.resx`](../../src/Tasks/Common/Resources/Strings.resx).
NETSDK numbers, message formats, localization directives, and conditions are observable
contracts. Use the task-specific workflow in
[`src/Tasks/AGENTS.md`](../../src/Tasks/AGENTS.md#build-diagnostics-netsdk-errors--warnings--info).

## SDK Resolver Extension Points

| Type | Location | Contract |
| --- | --- | --- |
| `DotNetMSBuildSdkResolver` | [`MSBuildSdkResolver.cs`](../../src/Resolvers/Microsoft.DotNet.MSBuildSdkResolver/MSBuildSdkResolver.cs) | Resolves installed .NET SDK location/version and returns properties/items/environment to MSBuild |
| `WorkloadSdkResolver` | [`WorkloadSdkResolver.cs`](../../src/Resolvers/Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver/WorkloadSdkResolver.cs) | Resolves workload SDKs from installed manifests |
| Workload manifest model/reader | [`Microsoft.NET.Sdk.WorkloadManifestReader`](../../src/Resolvers/Microsoft.NET.Sdk.WorkloadManifestReader) | Shared manifest parsing consumed by resolvers and CLI workload code |
| hostfxr interop | [`Microsoft.DotNet.NativeWrapper`](../../src/Resolvers/Microsoft.DotNet.NativeWrapper) | Runtime-owned native contract used to resolve SDKs |

Resolver loading shape, assembly versions, dependencies, and P/Invoke signatures are
compatibility-sensitive; see [`src/Resolvers/AGENTS.md`](../../src/Resolvers/AGENTS.md).

## Other Contract Types

Use [FILE_MAP.md](FILE_MAP.md#product-source) for subsystem locations and dependency
relationships. The additional observable contracts are:

| Surface | Contract |
| --- | --- |
| In-box templates | Template identity, symbols, defaults, generated content, and post-actions |
| Template Engine libraries and tools | Shipped APIs tracked by [`PublicAPI.Shipped.txt`](../../src/TemplateEngine/Microsoft.TemplateEngine.Abstractions/PublicAPI.Shipped.txt) / [`PublicAPI.Unshipped.txt`](../../src/TemplateEngine/Microsoft.TemplateEngine.Abstractions/PublicAPI.Unshipped.txt) in participating projects |
| Container publish integration | MSBuild properties/items/targets, image metadata, diagnostics, and registry behavior |
| `dotnet watch` / `dotnet format` | CLI contract, output, file watching/hot reload behavior, and bundled tool layout |
| ApiCompat, ApiDiff, GenAPI, package validation | Tool commands/options, MSBuild task parameters, package-validation rules, and library APIs |
| .NET analyzers | CA diagnostic IDs, descriptors, options, generated docs/SARIF, and code-fix behavior |
| Project tools and container libraries | Public APIs tracked for [project tools](../../src/Microsoft.DotNet.ProjectTools/PublicAPI.Shipped.txt) and [each container TFM](../../src/Containers/Microsoft.NET.Build.Containers/PublicAPI/net12.0/PublicAPI.Shipped.txt), with corresponding unshipped files |

When a project has [`PublicAPI.Shipped.txt`](../../src/Microsoft.DotNet.ProjectTools/PublicAPI.Shipped.txt)
and [`PublicAPI.Unshipped.txt`](../../src/Microsoft.DotNet.ProjectTools/PublicAPI.Unshipped.txt),
update them through the owning project's established analyzer workflow rather than
bypassing the public API analyzer.

## Distribution Contract

The redist layout is itself a product contract. Its composition starts in
[`redist.csproj`](../../src/Layout/redist/redist.csproj); the
[`Bundled*.targets` and `Generate*.targets`](../../src/Layout/redist/targets) define
which SDKs, tools, templates, analyzers, manifests, and dependencies ship and where they
are placed. Package or layout changes must be validated through the full repository
build because a standalone redist project build can consume stale or missing components.
