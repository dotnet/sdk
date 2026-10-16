# Publish Build Optimization

## Scope and rollout

`dotnet publish` normally runs a full `Build` before publishing. It cannot assume
that an earlier build used the same configuration, runtime identifier, or properties.
For supported scenarios, `UseOptimizedPublish` replaces that implicit root-project
`Build` with compilation and the prerequisites needed by publish. Compilation is
not skipped. The intended deployment remains `PublishDir`.

Native AOT has used this route by default since .NET 11. The first .NET 12 expansion
adds **plain trimmed C# executable projects targeting .NET 11 or later**, subject to
all of these restrictions:

- `.NETCoreApp`, self-contained, with no target platform identifier.
- One of `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`,
  `linux-musl-arm64`, `osx-x64`, or `osx-arm64`.
- Neither `PublishSingleFile` nor `PublishReadyToRun` is enabled.
- No Web, Razor, Worker, WebAssembly, MAUI, WPF, or Windows Forms SDK scenario.
- No COM hosting/registration, IJW hosting, dynamic loading, or explicitly enabled
  serialization assembly generation.
- No ClickOnce publishing/manifests or tool packaging.

The TFM restriction is deliberate. The expansion applies to .NET 11 projects
using the .NET 12 SDK; it is not implicitly restricted to `net12.0`.
Projects targeting .NET 10 or earlier retain
the full Build route. Platform-specific mobile, browser-Wasm, Windows
desktop/native-host integration, and combined trimmed modes
remain outside this first expansion. This is an SDK graph eligibility check, not
a guarantee that every third-party package or custom target supports the route.

| Publish scenario | Default route |
| --- | --- |
| Native AOT (including its implied trimming) | Optimized; existing eligibility unchanged |
| Plain trimmed application satisfying all restrictions above | Optimized |
| Trimmed + single-file and/or ReadyToRun, without AOT | Full Build |
| Older-TFM or otherwise excluded trimmed application | Full Build |
| Ordinary framework-dependent or self-contained application | Full Build |
| ReadyToRun-only or single-file-only application | Full Build |
| `NoBuild=true` / `--no-build` | Separate no-build route |

Eligibility and public default selection are separate. Setting
`UseOptimizedPublish=true` does not force an unsupported scenario onto the optimized
route. The new restrictions do not narrow existing Native AOT routing.
See [the routing and eligibility definitions](../../src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Publish.targets).

## Prerequisites and output ownership

The common optimized prerequisites are:

```text
BuildOnlySettings -> PrepareForBuild -> PrepareResources -> Compile -> CreateSatelliteAssemblies
```

`Compile` includes reference resolution and compilation. Referenced projects still
run their normal builds and can produce files under their own `bin` directories.
Their resolved outputs can be publish inputs. This is not a change to project-instance
compilation, RID propagation, or the project-reference build graph.

The managed route also generates the root runtime configuration with the existing
SDK task, under
`$(IntermediateOutputPath)publish\$(ProjectRuntimeConfigFileName)`. It uses the normal
runtime settings, user runtimeconfig template, and input-cache logic, and records
the intermediate file for incremental builds and Clean. It does not create a
publish-specific `runtimeconfig.dev.json`.
The metadata registration does not take ownership of the published deployment in
Build's incremental-clean bookkeeping; a subsequent Build must leave that deployment intact.

Public build properties such as `ProjectRuntimeConfigFilePath` and
`ProjectRuntimeConfigDevFilePath` are **not** rebased to intermediate paths.
Explicit Build, build output groups, and reference consumers retain their build
contracts, including custom build paths. A custom `IntermediateOutputPath` moves the
managed publish runtime configuration with it; `ProjectRuntimeConfigFileName`
controls the publish filename. `ProjectRuntimeConfigFilePath` remains a build-only
destination on this route, not a source of previously generated publish metadata.

Trimmed publish generates its own dependency metadata rather than reusing an
absent or stale build deps file. Existing `PublishDepsFilePath` behavior is retained.
The root assembly, symbols, satellite assemblies, and XML documentation are collected
from their intermediate/compiler-produced sources. App.config and content retain
their normal publish item processing. Apphost generation already participates in
compilation/publish and is not duplicated.

See [runtime configuration generation](../../src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets)
and [publish file collection and dependency generation](../../src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Publish.targets).
Native compilation, ILLink, ReadyToRun compilation, and bundling operate on their
respective publish inputs, but that alone does not prove their complete prerequisite
graphs are independent of Build output.

For a clean eligible root project using default paths, optimized publish avoids
the redundant loose managed deployment in `bin\<configuration>\<tfm>\<rid>\`.
Publish output still appears under its `publish` subdirectory. Existing build files
are not deleted merely to make the root output directory empty. An explicit `Build`,
including `Build;Publish` or `Publish;Build`, still requests normal build output.
Custom output locations and project references can also produce files outside the
publish directory.

SDK extensions that need additional publish prerequisites can use the existing
`_AdditionalOptimizedPublishTargets` internal extension point. For example, the
Static Web Assets SDK contributes manifest generation because `PrepareForRun` is
skipped. That extension does not establish compatibility for all Web/Razor/Blazor
scenarios; those scenarios are excluded from the new managed expansion.

## Build-hook compatibility and opt-out

Optimized publish skips the root `Build` hooks: `BeforeBuild`, `AfterBuild`,
`BeforeTargets="Build"`, `AfterTargets="Build"`, `PreBuildEvent`, and `PostBuildEvent`.
This is an intentional behavior change for newly eligible trimmed projects using the .NET 12 SDK.
Unlike `--no-build`, optimized publish still compiles.

Generators needed by compilation should run before compilation (for example through
`BeforeTargets="CoreCompile"`), with correct incremental inputs/outputs and generated
items. **`BeforeTargets="Publish"` is too late for compilation prerequisites**:
Publish's dependencies have already executed. Post-publish work can attach to
`AfterTargets="Publish"`.

Changing a hook is not sufficient when its implementation requires a runnable build
deployment under `TargetPath`/`OutputPath`. Known coordination points include:

- ASP.NET Core OpenAPI generation in
  [Microsoft.Extensions.ApiDescription.Server.targets](https://github.com/dotnet/aspnetcore/blob/main/src/Tools/Extensions.ApiDescription.Server/src/build/Microsoft.Extensions.ApiDescription.Server.targets),
  which hooks before Build and launches against `TargetPath`.
- XML serialization generation in
  [Microsoft.XmlSerializer.Generator.targets](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.XmlSerializer.Generator/src/build/Microsoft.XmlSerializer.Generator.targets),
  which runs after Build and copies a generated assembly from `OutputPath`.

These integrations need upstream publish-aware prerequisite/output contracts.
The SDK does not detect individual packages or silently emulate their build
deployments. Projects depending on Build hooks or runnable build outputs should
opt out until their integration supports optimized publishing:

```xml
<PropertyGroup>
  <UseOptimizedPublish>false</UseOptimizedPublish>
</PropertyGroup>
```

Or:

```text
dotnet publish /p:UseOptimizedPublish=false
```

The opt-out restores the implicit full Build for both AOT and eligible trimmed
publishing. It does not override `NoBuild`.

## Further expansion

ReadyToRun, single-file, older TFMs, additional languages/RIDs, and specialized SDKs
require explicit eligibility decisions and coverage before expansion. Each needs
an audit of skipped producers, metadata paths and incremental/Clean behavior,
combined modes, and ecosystem Build hooks. Sharing intermediate assembly inputs
does not mean the same prerequisite list is sufficient.
