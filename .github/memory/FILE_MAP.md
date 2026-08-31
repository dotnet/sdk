---
coverage: Significant repository directories, their roles, key entry points, and dependency relationships
---

# File Map

This map identifies stable ownership and navigation boundaries. It is intentionally not
an exhaustive project listing; [`sdk.slnx`](../../sdk.slnx) is the current project map,
and [`CODEOWNERS`](../../CODEOWNERS) is the ownership source of truth.

## Repository Root

| Path | Purpose and key files | Dependencies |
| --- | --- | --- |
| [`src/`](../../src) | Product source. Major areas are mapped below. | Consumes flowed dependencies and build infrastructure; consumed by `test/` and `src/Layout`. |
| [`test/`](../../test) | Unit, integration, end-to-end, snapshot, and Helix test infrastructure. [`TestAssets`](../../test/TestAssets) contains inputs, not tests. | Exercises `src/` through the built redist SDK and shared test frameworks. |
| [`template_feed/`](../../template_feed) | In-box project and item template sources. | Consumed by Template Engine tests and `src/Layout/redist`. |
| [`eng/`](../../eng) | Arcade configuration, dependency-flow manifests, versions, pipelines, and dogfood scripts. | Imported by root build files and CI; `eng/common` is Arcade-managed infrastructure. |
| [`build/`](../../build) | Repository-specific build targets and generated-resource support. | Imported from root `Directory.Build.*`. |
| [`scripts/`](../../scripts) | Repository maintenance and evaluation utilities, including conditional-test selection. | Used by developers, agents, and CI. |
| [`documentation/`](../../documentation) | Contributor and subsystem documentation. Start with the [Developer Guide](../../documentation/project-docs/developer-guide.md). | Must remain synchronized with product and workflow changes. |
| [`.github/`](..) | GitHub workflows, agent instructions, agents, skills, and memory. | Guides GitHub automation and AI-assisted development. |
| [`.azuredevops/`](../../.azuredevops), [`eng/pipelines`](../../eng/pipelines) | Azure DevOps pipeline entry points and shared templates. | Build and submit tests to Helix. |
| [`benchmarks/`](../../benchmarks) | Performance benchmarks for selected components. | Consumes product projects; not part of routine targeted validation. |
| `.dotnet/` | Repository-local SDK restored by the build. Invoke `.dotnet/dotnet` (`.dotnet\dotnet.exe` on Windows) when a command should resolve with the repository bootstrap SDK. | Selected by [`global.json`](../../global.json); not a product output or the SDK exercised by product tests. |
| `artifacts/` | Generated build, package, log, test, and redist outputs; never source-controlled. | Recreated by builds. |

Important root files:

- [`build.cmd`](../../build.cmd) / [`build.sh`](../../build.sh): full Arcade build entry points.
- [`test.cmd`](../../test.cmd) / [`test.sh`](../../test.sh) and
  [`restore.cmd`](../../restore.cmd) / [`restore.sh`](../../restore.sh): thin wrappers
  that forward to the build entry point.
- [`sdk.slnx`](../../sdk.slnx): full solution; `*.slnf` files are focused views.
- [`Directory.Build.props`](../../Directory.Build.props) /
  [`Directory.Build.targets`](../../Directory.Build.targets): repository-wide build policy.
- [`Directory.Packages.props`](../../Directory.Packages.props): central package versions.
- [`global.json`](../../global.json): bootstrap SDK, test runner, and MSBuild SDK versions.
- [`NuGet.config`](../../NuGet.config): approved restore sources.

## Product Source

| Path | Purpose and key files | Depends on / depended on by |
| --- | --- | --- |
| [`src/Cli`](../../src/Cli) | Managed CLI, shared AOT-safe command definitions, Native AOT bridge, template CLI integration, command resolution, telemetry, and utilities. Start with [`AGENTS.md`](../../src/Cli/AGENTS.md), [`Program.cs`](../../src/Cli/dotnet/Program.cs), and [`DotNetCommandDefinition.cs`](../../src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/DotNetCommandDefinition.cs). | Consumes NuGet/MSBuild/Template Engine and SDK utilities; consumed by redist and CLI tests. |
| [`src/Tasks`](../../src/Tasks) | Core shipping MSBuild tasks and targets, desktop build extensions, shared linked sources, resources, and repository-only `sdk-tasks`. Start with [`AGENTS.md`](../../src/Tasks/AGENTS.md). | Integrates MSBuild, NuGet, compiler/runtime packs; consumed by projects using `Microsoft.NET.Sdk`, redist, and build tests. |
| [`src/Resolvers`](../../src/Resolvers) | Installed SDK resolver, hostfxr wrapper, workload SDK resolver, and workload-manifest reader. Start with [`AGENTS.md`](../../src/Resolvers/AGENTS.md). | Loaded by MSBuild and shared with workload CLI code; consumed by redist and resolver tests. |
| [`src/Workloads`](../../src/Workloads) | Workload manifest packaging and Visual Studio insertion. | Consumed by workload CLI/resolvers and redist. |
| [`src/RazorSdk`](../../src/RazorSdk) | Razor SDK props, targets, tasks, and tool. | Layers on core SDK and Roslyn/Razor artifacts; consumed by redist and Razor tests. |
| [`src/WebSdk`](../../src/WebSdk) | Web, Worker, ProjectSystem, and Publish SDK layers. | Layers on core/Razor/static-web-asset behavior; consumed by redist and Web SDK tests. |
| [`src/StaticWebAssetsSdk`](../../src/StaticWebAssetsSdk) | Static web asset discovery, manifests, compression, endpoints, publish, and pack logic. | Integrates Web/Razor/Blazor builds; consumed by redist and SWA tests. |
| [`src/BlazorWasmSdk`](../../src/BlazorWasmSdk), [`src/WasmSdk`](../../src/WasmSdk) | Blazor WebAssembly and general WebAssembly SDK tasks, targets, and tools. | Integrate runtime/workload packs; consumed by redist and WASM tests. |
| [`src/Containers`](../../src/Containers) | Container publish tasks, image construction, registry integration, and packaging. | Imported by core `Sdk.targets`; consumed by redist and container tests. |
| [`src/Dotnet.Watch`](../../src/Dotnet.Watch) | `dotnet watch`, hot reload, browser refresh, and Aspire integration. | Integrates CLI, Roslyn, MSBuild, and ASP.NET assets; bundled by redist. |
| [`src/Dotnet.Format`](../../src/Dotnet.Format) | `dotnet format` command implementation. | Integrates CLI and Roslyn workspaces; bundled by redist. |
| [`src/Compatibility`](../../src/Compatibility) | ApiCompat, ApiDiff, GenAPI, package validation libraries, tasks, and tools. | Consumes Roslyn/MSBuild; tested under `test/Compatibility`. |
| [`src/Microsoft.CodeAnalysis.NetAnalyzers`](../../src/Microsoft.CodeAnalysis.NetAnalyzers) | CA analyzers, code fixes, packaging, tests, and documentation generation. | Consumes Roslyn APIs; package is bundled by redist. |
| [`src/TemplateEngine`](../../src/TemplateEngine) | Template abstractions, core engine, runnable-project orchestration, IDE APIs, authoring tools, and search. | Consumed by `Microsoft.TemplateEngine.Cli`, `template_feed`, and Template Engine tests. |
| [`src/Microsoft.DotNet.TemplateLocator`](../../src/Microsoft.DotNet.TemplateLocator) | Locates templates supplied by workload packs. | Consumed by template/CLI integration and redist. |
| [`src/Layout`](../../src/Layout) | Redist layout, archives, installers, and Visual Studio insertion. Start with [`AGENTS.md`](../../src/Layout/AGENTS.md) and [`redist.csproj`](../../src/Layout/redist/redist.csproj). | Depends on nearly every shipping component; produces the runnable SDK layout. |
| [`src/Common`](../../src/Common), [`src/Microsoft.DotNet.ProjectTools`](../../src/Microsoft.DotNet.ProjectTools), [`src/Microsoft.Extensions.Logging.MSBuild`](../../src/Microsoft.Extensions.Logging.MSBuild) | Cross-area utilities, project tooling, and MSBuild logging support. | Consumed by the owning product areas; verify references before changing shared code. |
| [`src/Microsoft.Net.Sdk.Compilers.Toolset`](../../src/Microsoft.Net.Sdk.Compilers.Toolset), [`src/Microsoft.Win32.Msi`](../../src/Microsoft.Win32.Msi), [`src/System.CommandLine.StaticCompletions`](../../src/System.CommandLine.StaticCompletions) | Compiler toolset packaging, Windows installer support, and shell completion generation. | Consumed by layout, CLI, or platform-specific builds and tests. |
| [`src/SourceBuild`](../../src/SourceBuild) | Source-build-specific content and integration. | Consumed by the VMR/source-only build; use source-build guidance for source-only failures. |

## Test Structure

| Path | Role |
| --- | --- |
| [`test/Microsoft.NET.TestFramework.MSTest`](../../test/Microsoft.NET.TestFramework.MSTest) | Shared `SdkTest` base, test context, conditional attributes, and helpers for MSTest projects. |
| [`test/TestAssets`](../../test/TestAssets) | Projects, packages, workloads, and files copied as test inputs. |
| [`test/dotnet.Tests`](../../test/dotnet.Tests) | Managed CLI command, parsing, help, and completion tests. |
| [`test/Microsoft.NET.Build.Tests`](../../test/Microsoft.NET.Build.Tests), [`test/Microsoft.NET.Build.Tasks.Tests`](../../test/Microsoft.NET.Build.Tasks.Tests) | Core SDK target integration tests and task unit tests. |
| [`test/Microsoft.NET.Publish.Tests`](../../test/Microsoft.NET.Publish.Tests), [`test/Microsoft.NET.Pack.Tests`](../../test/Microsoft.NET.Pack.Tests), [`test/Microsoft.NET.Restore.Tests`](../../test/Microsoft.NET.Restore.Tests) | Publish, pack, and restore behavior. |
| [`test/TemplateEngine`](../../test/TemplateEngine), [`test/dotnet-new.IntegrationTests`](../../test/dotnet-new.IntegrationTests) | Template Engine libraries, authoring tools, and `dotnet new`. |
| [`test/UnitTests.proj`](../../test/UnitTests.proj) | Test project discovery, publishing, partitioning, and Helix payload orchestration. |
| [`test/ConditionalTests.props`](../../test/ConditionalTests.props) | Source-of-truth mappings for conditional PR scopes and configured `run-tests` selection. |

See [TESTING_STRATEGY.md](TESTING_STRATEGY.md) for test-platform architecture and
canonical testing guidance.
