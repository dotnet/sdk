# Microsoft.DotNet.FileBasedPrograms Source Package

This is a source package containing shared code for [file-based programs](../../../documentation/general/dotnet-run-file.md) support. This package is intended only for internal use by .NET components.

## Usage in Consuming Projects

To use this package in your project, add the following to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.DotNet.FileBasedPrograms" GeneratePathProperty="true" />
  <EmbeddedResource Include="$(PkgMicrosoft_DotNet_FileBasedPrograms)\contentFiles\cs\any\FileBasedProgramsResources.resx"
                    GenerateSource="true"
                    Namespace="Microsoft.DotNet.FileBasedPrograms" />
</ItemGroup>
```
