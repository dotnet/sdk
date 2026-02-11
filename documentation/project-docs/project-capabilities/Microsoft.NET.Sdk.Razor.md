# Microsoft.NET.Sdk.Razor Project Capabilities

This document describes the Project Capabilities provided by the Razor SDK (`Microsoft.NET.Sdk.Razor`).

## Overview

The Razor SDK enables Razor compilation and tooling support for projects that use Razor views, pages, or components (Blazor). It's used by ASP.NET Core MVC, Razor Pages, and Blazor applications.

## Capabilities

### DotNetCoreRazor

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Razor`

**Source:** `Microsoft.NET.Sdk.Razor.DesignTime.targets`

**Purpose:**
Defines the generic .NET Core 'Razor' capability, indicating that this project uses Razor and has access to Razor-specific tooling and features.

**Enables:**
- Razor syntax highlighting and IntelliSense
- Razor code generation and compilation
- Tag Helper support
- Design-time build support for Razor files

**Notes:**
- This capability doesn't depend on the version of the runtime/toolset
- Version-specific capabilities are defined by runtime packages

---

### DotNetCoreRazorConfiguration

**When Added:**
- When targeting Razor language version 3.0 or newer
- Condition: `$(_Targeting30OrNewerRazorLangVersion)` == `true`

**Source:** `Microsoft.NET.Sdk.Razor.DesignTime.targets`

**Purpose:**
Indicates that the project can understand and use the Razor language service configuration provided by runtime/toolset packages. This capability was introduced in Razor 2.1 and signals support for advanced Razor language service features.

**Enables:**
- Advanced Razor language service configuration
- Component model support (Blazor)
- Enhanced IntelliSense and tooling features
- Razor compiler configuration

**Notes:**
- Only added for projects targeting Razor 3.0+ language versions
- Enables more sophisticated Razor editing experiences in Visual Studio

---

### WebNestingDefaults

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Razor`

**Source:** `Microsoft.NET.Sdk.Razor.DesignTime.targets`

**Purpose:**
Activates the set of nesting behaviors for files in Solution Explorer that are commonly used in web projects.

**Enables:**
- Automatic nesting of related files (e.g., `Index.razor.css` nested under `Index.razor`)
- Configuration file nesting (e.g., `appsettings.Development.json` under `appsettings.json`)
- Standard web project file organization

**Example Nesting:**
- `MyComponent.razor` → `MyComponent.razor.cs` (code-behind)
- `MyComponent.razor` → `MyComponent.razor.css` (scoped CSS)
- `appsettings.json` → `appsettings.Development.json`, `appsettings.Production.json`

---

### SupportsTypeScriptNuGet

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Razor`

**Source:** `Microsoft.NET.Sdk.Razor.DesignTime.targets`

**Purpose:**
Indicates that the project has tooling support for TypeScript files managed via NuGet packages.

**Enables:**
- TypeScript compilation and tooling
- Integration with TypeScript NuGet packages
- TypeScript IntelliSense and type checking
- Coordinated build processes for TypeScript and Razor

---

### SupportHierarchyContextSvc

**When Added:**
- When the project is NOT using `Microsoft.NET.Sdk.Web`
- Condition: `$(UsingMicrosoftNETSdkWeb)` == empty

**Source:** `Microsoft.NET.Sdk.Razor.DesignTime.targets`

**Purpose:**
Enables hierarchical context service support in Visual Studio. This capability is conditionally imported only if not already provided by the Web SDK.

**Enables:**
- Advanced project hierarchy features
- Context-sensitive tooling
- Enhanced Solution Explorer integration

**Notes:**
- The Web SDK already includes this capability, so Razor SDK only adds it for non-web projects (e.g., Blazor WebAssembly standalone, Razor class libraries)

---

### DynamicDependentFile

**When Added:**
- When the project is NOT using `Microsoft.NET.Sdk.Web`
- Condition: `$(UsingMicrosoftNETSdkWeb)` == empty

**Source:** `Microsoft.NET.Sdk.Razor.DesignTime.targets`

**Purpose:**
Enables dynamic dependent file tracking. This capability is conditionally imported only if not already provided by the Web SDK.

**Enables:**
- Automatic file nesting in Solution Explorer
- Code-behind and generated file associations
- Dependencies between Razor files and their outputs

**Notes:**
- Essential for Blazor component code-behind files
- Automatically nests `.razor.cs` files under `.razor` files

---

### DynamicFileNesting

**When Added:**
- When the project is NOT using `Microsoft.NET.Sdk.Web`
- Condition: `$(UsingMicrosoftNETSdkWeb)` == empty

**Source:** `Microsoft.NET.Sdk.Razor.DesignTime.targets`

**Purpose:**
Enables dynamic file nesting rules. This capability is conditionally imported only if not already provided by the Web SDK.

**Enables:**
- Automatic nesting of related files
- Customizable nesting patterns
- Cleaner project organization

---

## Summary Table

| Capability | Always Added | Conditional | Purpose |
|------------|--------------|-------------|---------|
| `DotNetCoreRazor` | Yes | - | Core Razor support indicator |
| `DotNetCoreRazorConfiguration` | No | Razor 3.0+ | Advanced language service config |
| `WebNestingDefaults` | Yes | - | Web-style file nesting |
| `SupportsTypeScriptNuGet` | Yes | - | TypeScript NuGet support |
| `SupportHierarchyContextSvc` | No | Non-Web SDK projects only | Hierarchy context service |
| `DynamicDependentFile` | No | Non-Web SDK projects only | Dependent file tracking |
| `DynamicFileNesting` | No | Non-Web SDK projects only | File nesting rules |

---

## Project Types Using Razor SDK

### ASP.NET Core MVC and Razor Pages

Typically use `Microsoft.NET.Sdk.Web`, which implicitly includes Razor SDK functionality:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

### Blazor Server

Uses `Microsoft.NET.Sdk.Web` with Blazor packages:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

### Blazor WebAssembly

Explicitly uses `Microsoft.NET.Sdk.Razor` or `Microsoft.NET.Sdk.BlazorWebAssembly`:

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

### Razor Class Libraries

Uses `Microsoft.NET.Sdk.Razor` to create reusable component libraries:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>
</Project>
```

---

## Blazor Partial Classes Support

The Razor SDK includes special handling for Blazor partial classes to ensure proper compilation:

- **Target:** `_RemoveRazorDeclartionsFromCompile`
- **Purpose:** Prevents older SDKs from adding declaration files to the compile list during design-time builds
- **Effect:** Ensures all compilation work is done in-memory in modern Visual Studio versions

---

## See Also

- [Project Capabilities Overview](../project-capabilities.md)
- [Microsoft.NET.Sdk.Web Capabilities](Microsoft.NET.Sdk.Web.md)
- [Microsoft.NET.Sdk Capabilities](Microsoft.NET.Sdk.md)
- [Razor Pages Documentation](https://learn.microsoft.com/aspnet/core/razor-pages/)
- [Blazor Documentation](https://learn.microsoft.com/aspnet/core/blazor/)
- [Razor Class Libraries](https://learn.microsoft.com/aspnet/core/razor-pages/ui-class)
