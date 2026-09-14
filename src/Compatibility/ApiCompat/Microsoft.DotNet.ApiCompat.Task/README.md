# Microsoft.DotNet.ApiCompat.Task

MSBuild tasks and targets to perform API compatibility checks on assemblies and packages.

## Getting started

Add a `PackageReference` to this package and the `EnablePackageValidation` and
`ApiCompatValidateAssemblies` features become available on your project. See
[the package validation docs](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview)
for the full list of options.

```xml
<PackageReference Include="Microsoft.DotNet.ApiCompat.Task" Version="..." PrivateAssets="all" />
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
