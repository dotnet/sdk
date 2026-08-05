# Microsoft.DotNet.GenAPI.Task

MSBuild tasks and targets to emit Roslyn-based source code from input assemblies.

## Getting started

Add a `PackageReference` to this package and the `GenAPITask` becomes
available to your project. The package also wires up an integration target
that runs automatically during build (after the compiler is invoked) when
the `GenAPIGenerateReferenceAssemblySource` MSBuild property is set to
`true`. The generated reference source is written to `GenAPITargetPath`
(defaults to `$(TargetDir)$(TargetName).cs`).

```xml
<PackageReference Include="Microsoft.DotNet.GenAPI.Task" Version="..." PrivateAssets="all" />

<PropertyGroup>
  <GenAPIGenerateReferenceAssemblySource>true</GenAPIGenerateReferenceAssemblySource>
</PropertyGroup>
```

## Requirements

This package ships only a .NET (Core) implementation of the MSBuild task. When
the task is invoked from desktop `MSBuild.exe` (i.e. inside Visual Studio), the
task is loaded into a .NET task host using MSBuild's `Runtime="NET"` support.

That support requires:

- **MSBuild 18.0 or later** (Visual Studio 2026 / `MSBuild.exe` 18.0+).
- Equivalently, **.NET SDK 10.0.100 or later**.

If the package is consumed under an older MSBuild / SDK, the task will fail to
load with `MSB4175` ("The task factory ... could not be loaded") or with a
`NETHostTaskLoad_Failed` error from the .NET task host. Upgrade Visual Studio
or the .NET SDK to a supported version to resolve.
