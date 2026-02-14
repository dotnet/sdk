# Microsoft.NET.Sdk.Web Project Capabilities

This document describes the Project Capabilities provided by the ASP.NET Core Web SDK (`Microsoft.NET.Sdk.Web`).

## Capabilities

### DotNetCoreWeb

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Web`

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Indicates that this project is an ASP.NET Core web application. This is the primary capability that identifies a project as a web project.

**Enables:**
- Web-specific tooling and features in Visual Studio
- ASP.NET Core project templates and scaffolding
- Web-specific debugging and deployment experiences
- Browser-based debugging capabilities

---

### AspNetCore

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Web`

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Indicates that this project uses the ASP.NET Core framework.

**Enables:**
- ASP.NET Core-specific features and tooling
- Framework-specific IntelliSense and code analysis
- ASP.NET Core middleware and service configuration support

---

### Web

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Web`

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
General web project capability that signals web-related features should be enabled.

**Enables:**
- Web-related IDE features
- HTTP/HTTPS launch profiles
- Web server integration (Kestrel, IIS Express)

---

### AppServicePublish

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Web`

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Indicates that this project supports publishing to Azure App Service.

**Enables:**
- Azure App Service publish profiles in Visual Studio
- Right-click publish to Azure
- Azure deployment tooling integration

---

### AspNetCoreInProcessHosting

**When Added:**
- Target Framework: `.NETCoreApp` 3.0 or higher
- Using `Microsoft.NET.Sdk.Web`

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Indicates that this project supports ASP.NET Core in-process hosting with IIS. In-process hosting runs the ASP.NET Core application in the same process as the IIS worker process, which can provide better performance.

**Enables:**
- In-process hosting configuration UI
- IIS in-process hosting model selection
- Optimized IIS deployment options

**Related Properties:**
- `AspNetCoreHostingModel` - Defaults to `inprocess` for .NET Core 3.0+
- `AspNetCoreModuleName` - Defaults to `AspNetCoreModuleV2` for .NET Core 3.0+

**Example Condition:**
```xml
<ItemGroup Condition="'$(TargetFrameworkIdentifier)' == '.NETCoreApp' And '$(_TargetFrameworkVersionWithoutV)' != '' And '$(_TargetFrameworkVersionWithoutV)' >= '3.0'">
  <ProjectCapability Include="AspNetCoreInProcessHosting" />
</ItemGroup>
```

---

### SupportHierarchyContextSvc

**When Added:**
- When using `Microsoft.NET.Sdk.Web` (if not already added by Visual Studio)

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Enables hierarchical context service support in Visual Studio, which allows the project system to provide context-specific features and behaviors.

**Enables:**
- Advanced project hierarchy features
- Context-sensitive tooling
- Enhanced Solution Explorer integration

**Note:** This capability is conditionally imported only if not already provided by Visual Studio's `Microsoft.Web.Designtime.targets`.

---

### DynamicDependentFile

**When Added:**
- When using `Microsoft.NET.Sdk.Web` (if not already added by Visual Studio)

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Enables dynamic dependent file tracking, which allows files to be automatically associated with their "parent" files in the project.

**Enables:**
- Automatic file nesting in Solution Explorer
- Code-behind file associations
- Generated file dependencies

**Example:**
- `MyPage.cshtml` and `MyPage.cshtml.cs` are automatically nested
- Generated files are nested under their source files

---

### DynamicFileNesting

**When Added:**
- When using `Microsoft.NET.Sdk.Web` (if not already added by Visual Studio)

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Enables dynamic file nesting rules that automatically organize related files in Solution Explorer.

**Enables:**
- Automatic nesting of related files (e.g., TypeScript/JavaScript files with their compiled output)
- Customizable nesting patterns
- Cleaner Solution Explorer organization

**Related:**
- Works in conjunction with `DynamicDependentFile` and `WebNestingDefaults`

---

### LocalUserSecrets

**When Added:**
- When `$(GenerateUserSecretsAttribute)` == `true`
- Using `Microsoft.NET.Sdk.Web`

**Source:** `Microsoft.NET.Sdk.Web.ProjectSystem.targets`

**Purpose:**
Indicates that this project supports ASP.NET Core User Secrets for managing sensitive configuration data during development.

**Enables:**
- "Manage User Secrets" context menu in Visual Studio
- User Secrets configuration UI
- Safe storage of development secrets outside of source control

**Related:**
- Requires `Microsoft.Extensions.Configuration.UserSecrets` package (or implied by framework)
- Secrets are stored in `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>` (Windows) or `~/.microsoft/usersecrets/<user_secrets_id>` (macOS/Linux)

**Notes:**
- Older versions of `Microsoft.Extensions.Configuration.UserSecrets` (1.x) did not include the capability, so it's added by the SDK
- Newer versions include the capability in their own MSBuild targets

---

## Summary Table

| Capability | Always Added | Conditional | Purpose |
|------------|--------------|-------------|---------|
| `DotNetCoreWeb` | Yes | - | Primary web project identifier |
| `AspNetCore` | Yes | - | ASP.NET Core framework indicator |
| `Web` | Yes | - | General web project features |
| `AppServicePublish` | Yes | - | Azure App Service publishing |
| `AspNetCoreInProcessHosting` | No | .NET Core 3.0+ | IIS in-process hosting support |
| `SupportHierarchyContextSvc` | Yes* | Not in VS designtime | Hierarchy context service |
| `DynamicDependentFile` | Yes* | Not in VS designtime | Dynamic dependent file tracking |
| `DynamicFileNesting` | Yes* | Not in VS designtime | File nesting rules |
| `LocalUserSecrets` | No | When user secrets configured | User Secrets management |

\* These capabilities are added by the SDK if not already provided by Visual Studio's design-time targets.

---

## Default Properties Set by Web SDK

When using `Microsoft.NET.Sdk.Web`, several properties have special default values:

- **`RunWorkingDirectory`**: Defaults to `$(MSBuildProjectDirectory)` (can be disabled with `EnableDefaultRunWorkingDirectory=false`)
- **`AspNetCoreHostingModel`**: Defaults to `inprocess` for .NET Core 3.0+
- **`AspNetCoreModuleName`**: Defaults to `AspNetCoreModuleV2` for .NET Core 3.0+
- **`EnableUnsafeBinaryFormatterSerialization`**: Defaults to `false` for security

---

## See Also

- [Project Capabilities Overview](../project-capabilities.md)
- [Microsoft.NET.Sdk Capabilities](Microsoft.NET.Sdk.md)
- [Microsoft.NET.Sdk.Worker Capabilities](Microsoft.NET.Sdk.Worker.md)
- [Microsoft.NET.Sdk.Razor Capabilities](Microsoft.NET.Sdk.Razor.md)
- [ASP.NET Core Documentation](https://learn.microsoft.com/aspnet/core/)
