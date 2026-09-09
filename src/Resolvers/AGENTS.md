# Resolvers Agent Instructions

Guidance for changes under `src/Resolvers` — the MSBuild **SDK resolvers** and the
workload manifest reader.

## Where things live

| Project | Role |
|---------|------|
| `Microsoft.DotNet.MSBuildSdkResolver` | The `SdkResolver` MSBuild loads to find the .NET SDK. |
| `Microsoft.DotNet.SdkResolver` | Core resolution logic (`NETCoreSdkResolver`). |
| `Microsoft.DotNet.NativeWrapper` | P/Invoke wrapper over **hostfxr**. |
| `Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver` | The `SdkResolver` for workload SDKs. |
| `Microsoft.NET.Sdk.WorkloadManifestReader` | Parses/indexes workload manifest JSON; shared by the resolvers **and** the CLI. |

## Conventions & gotchas

- **Shared code is *linked*, not referenced.** `MSBuildSdkResolver` pulls the
  `NativeWrapper`, `SdkResolver`, and `WorkloadManifestReader` sources in via links
  and compiles them **into itself** (to minimize DLLs loaded into MSBuild).
- **Two target frameworks, two hosts.** net472 loads into VS/`MSBuild.exe`;
  `$(SdkTargetFramework)` loads into the .NET MSBuild. The **resolver projects'
  (`MSBuildSdkResolver`, `SdkResolver`) net472 build is gated to `DotNetBuildPass == 2`**
  (it depends on other verticals). Exercise both paths.
- **hostfxr interop is a runtime contract.** `NativeWrapper` uses `LibraryImport`
  under `#if NET` and `DllImport` with manual x86/x64/arm64 preload on net472.
  Changing P/Invoke signatures must stay back-compatible and coordinate with
  dotnet/runtime.
- **Dependencies are frozen to MSBuild's binding redirects.** `AssemblyVersion` is
  pinned and a `VerifyDependencies` build step checks the exact expected package
  versions — **coordinate with the MSBuild team before bumping/adding any dependency**.
