# Microsoft.NET.Sdk Project Capabilities

This document describes the Project Capabilities provided by the core .NET SDK (`Microsoft.NET.Sdk`).

## Capabilities

### CrossPlatformExecutable

**When Added:**
- Target Framework: `.NETCoreApp`
- Project Type: Executable (`OutputType` is `Exe` or `WinExe`)

**Source:** `Microsoft.NET.Sdk.targets`

**Purpose:**
Indicates that this project produces a cross-platform executable application. This capability signals that the project can be run on multiple operating systems (Windows, Linux, macOS) without recompilation.

**Enables:**
- Cross-platform debugging and deployment experiences in IDEs
- Platform-specific tooling adaptations
- Multi-platform testing and validation

**Example Condition:**
```xml
<ItemGroup Condition="'$(TargetFrameworkIdentifier)' == '.NETCoreApp' and '$(_IsExecutable)' == 'true'">
  <ProjectCapability Include="CrossPlatformExecutable" />
</ItemGroup>
```

---

### NativeAOT

**When Added:**
- Target Framework: `.NETCoreApp` 8.0 or higher
- Not using WPF: `$(UseWPF)` != `true`
- Not using Windows Forms: `$(UseWindowsForms)` != `true`

**Source:** `Microsoft.NET.Sdk.targets`

**Purpose:**
Indicates that this project can be published with Native AOT (Ahead-of-Time) compilation. This capability is automatically added for eligible .NET 8+ projects to signal that Native AOT publishing properties should be available in the IDE.

**Enables:**
- Native AOT project properties in Visual Studio
- IDE guidance for Native AOT compatibility
- Tooling that adapts to Native AOT scenarios

**Notes:**
- WPF and Windows Forms projects are excluded because they are not currently compatible with Native AOT
- This capability enables UI features; it does not automatically enable Native AOT compilation (you must still set `PublishAot=true`)

**Example Condition:**
```xml
<ItemGroup Condition="'$(UseWPF)' != 'true'
           and '$(UseWindowsForms)' != 'true'
           and '$(TargetFrameworkIdentifier)' == '.NETCoreApp'
           and $([MSBuild]::VersionGreaterThanOrEquals('$(_TargetFrameworkVersionWithoutV)', '8.0'))">
  <ProjectCapability Include="NativeAOT"/>
</ItemGroup>
```

---

### SupportsComputeRunCommand

**When Added:**
- Always added by `Microsoft.NET.Sdk`

**Source:** `Microsoft.NET.Sdk.targets`

**Purpose:**
Signals that this project supports the target-based run invocation protocol powered by the `ComputeRunArguments` target. This allows tools and extensions to customize how the project is run by overriding the `ComputeRunArguments` target to compute `RunCommand`, `RunArguments`, and `RunWorkingDirectory` properties.

**Enables:**
- IDE "Run" and "Debug" customization
- `dotnet run` command integration
- Tooling that needs to programmatically determine how to launch the application

**Related Targets:**
- `ComputeRunArguments` - Placeholder target that tools can override to compute run-related properties

**Example Usage:**
Tools can override `ComputeRunArguments` to customize the run experience:

```xml
<Target Name="ComputeRunArguments">
  <PropertyGroup>
    <RunCommand>myapp</RunCommand>
    <RunArguments>--custom-arg</RunArguments>
    <RunWorkingDirectory>$(MSBuildProjectDirectory)/custom-path</RunWorkingDirectory>
  </PropertyGroup>
</Target>
```

---

### GenerateDocumentationFile

**When Added:**
- Always added by `Microsoft.NET.Sdk`

**Source:** `Microsoft.NET.Sdk.BeforeCommon.targets`

**Purpose:**
Indicates that the project supports generating XML documentation files. This capability enables the IDE to show the option to generate an XML documentation file without requiring the user to manually specify the filename.

**Enables:**
- XML documentation generation UI in Visual Studio project properties
- Simplified documentation file configuration
- IntelliSense support for generated documentation

**Related Properties:**
- `GenerateDocumentationFile` - When set to `true`, generates an XML documentation file
- `DocumentationFile` - Path to the generated documentation file (automatically computed if not specified)

---

### SupportsHotReload

**When Added:**
- Language: C# or Visual Basic
- Target Framework: `.NETCoreApp` 6.0 or higher
- Not explicitly disabled: `$(SupportsHotReload)` != `false`

**Source:** 
- `Microsoft.NET.Sdk.CSharp.targets` (for C#)
- `Microsoft.NET.Sdk.VisualBasic.targets` (for Visual Basic)

**Purpose:**
Indicates that the project supports Hot Reload, which allows code changes to be applied to a running application without restarting it.

**Enables:**
- Hot Reload functionality in Visual Studio and `dotnet watch`
- Real-time code editing experience
- Faster development iteration cycles

**Notes:**
- Projects can opt-out by setting `SupportsHotReload=false`
- Available in .NET 6.0 and later

---

### ReferenceManagerAssemblies (Removed)

**When Removed:**
- Target Framework: `.NETCoreApp`

**Source:** `Microsoft.NET.Sdk.targets`

**Purpose:**
For .NET Core projects, the `ReferenceManagerAssemblies` capability is explicitly removed. This signals that the traditional Reference Manager assembly selection UI should not be shown, as .NET Core projects use PackageReference and SDK-style references instead.

**Effect:**
- Hides legacy assembly reference UI in Visual Studio
- Encourages use of NuGet packages and SDK references
- Simplifies the reference management experience for modern .NET projects

**Example:**
```xml
<ItemGroup Condition="'$(TargetFrameworkIdentifier)' == '.NETCoreApp'">
  <ProjectCapability Remove="ReferenceManagerAssemblies" />
</ItemGroup>
```

---

## Summary Table

| Capability | Always Added | Conditional | Purpose |
|------------|--------------|-------------|---------|
| `CrossPlatformExecutable` | No | .NET Core executables | Indicates cross-platform executable |
| `NativeAOT` | No | .NET 8+ (non-WPF/WinForms) | Enables Native AOT UI features |
| `SupportsComputeRunCommand` | Yes | - | Supports customizable run protocol |
| `GenerateDocumentationFile` | Yes | - | Supports XML documentation generation |
| `SupportsHotReload` | No | C#/VB .NET 6+ | Enables Hot Reload functionality |
| `ReferenceManagerAssemblies` (removed) | N/A | .NET Core projects | Hides legacy reference UI |

---

## See Also

- [Project Capabilities Overview](../project-capabilities.md)
- [Microsoft.NET.Sdk.Web Capabilities](Microsoft.NET.Sdk.Web.md)
- [Microsoft.NET.Publish Capabilities](Microsoft.NET.Publish.md)
