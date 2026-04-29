# Microsoft.NET.Sdk.Worker Project Capabilities

This document describes the Project Capabilities provided by the .NET Worker Service SDK (`Microsoft.NET.Sdk.Worker`).

## Overview

The Worker Service SDK is designed for creating long-running background services, such as Windows Services, Linux daemons, or Kubernetes-hosted workers. These projects typically run continuously and perform background tasks.

## Capabilities

### DotNetCoreWorker

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Worker`

**Source:** `Microsoft.NET.Sdk.Worker.targets`

**Purpose:**
Indicates that this project is a .NET Worker Service application. This is the primary capability that identifies a project as a worker service.

**Enables:**
- Worker Service-specific tooling and templates
- Background service debugging experiences
- Worker-specific project properties and configurations
- IDE integration for worker service patterns

**Related:**
- Worker services typically use `IHostedService` or `BackgroundService` base classes
- Projects are configured to run as console applications but are designed for long-running background work

---

### SupportHierarchyContextSvc

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Worker`

**Source:** `Microsoft.NET.Sdk.Worker.targets`

**Purpose:**
Enables hierarchical context service support in Visual Studio, which allows the project system to provide context-specific features and behaviors.

**Enables:**
- Advanced project hierarchy features in the IDE
- Context-sensitive tooling
- Enhanced Solution Explorer integration

---

### DynamicDependentFile

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Worker`

**Source:** `Microsoft.NET.Sdk.Worker.targets`

**Purpose:**
Enables dynamic dependent file tracking, which allows files to be automatically associated with their "parent" files in the project.

**Enables:**
- Automatic file nesting in Solution Explorer
- Code-behind file associations
- Generated file dependencies
- Cleaner project organization

**Example:**
- Configuration files and their environment-specific overrides can be nested
- Generated files are nested under their source files

---

### DynamicFileNesting

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Worker`

**Source:** `Microsoft.NET.Sdk.Worker.targets`

**Purpose:**
Enables dynamic file nesting rules that automatically organize related files in Solution Explorer.

**Enables:**
- Automatic nesting of related files
- Customizable nesting patterns
- Cleaner Solution Explorer organization
- Better visual organization of project files

---

### LocalUserSecrets

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Worker`

**Source:** `Microsoft.NET.Sdk.Worker.targets`

**Purpose:**
Indicates that this project supports ASP.NET Core User Secrets for managing sensitive configuration data during development.

**Enables:**
- "Manage User Secrets" context menu in Visual Studio
- User Secrets configuration UI
- Safe storage of development secrets outside of source control

**Notes:**
- Secrets are stored in `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>` (Windows) or `~/.microsoft/usersecrets/<user_secrets_id>` (macOS/Linux)
- User Secrets ID is typically stored in the project file as `<UserSecretsId>`
- Requires the `Microsoft.Extensions.Configuration.UserSecrets` package (usually included via `Microsoft.Extensions.Hosting`)

---

### WebNestingDefaults

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Worker`

**Source:** `Microsoft.NET.Sdk.Worker.targets`

**Purpose:**
Applies the default file nesting rules commonly used in web projects. This provides familiar file organization patterns even though Worker projects are not web applications.

**Enables:**
- Common file nesting patterns (e.g., `appsettings.Development.json` nested under `appsettings.json`)
- Consistent file organization with web projects
- Standard configuration file grouping

**Related:**
- Works in conjunction with `DynamicFileNesting` and `DynamicDependentFile`

---

### DynamicFileNestingEnabled

**When Added:**
- Always added when using `Microsoft.NET.Sdk.Worker`

**Source:** `Microsoft.NET.Sdk.Worker.targets`

**Purpose:**
Explicitly signals that dynamic file nesting is enabled and active for this project.

**Enables:**
- Confirms file nesting behavior is active
- Used by IDE to determine whether to apply nesting rules
- Ensures consistent file organization experience

---

## Summary Table

| Capability | Always Added | Conditional | Purpose |
|------------|--------------|-------------|---------|
| `DotNetCoreWorker` | Yes | - | Primary worker service identifier |
| `SupportHierarchyContextSvc` | Yes | - | Hierarchy context service |
| `DynamicDependentFile` | Yes | - | Dynamic dependent file tracking |
| `DynamicFileNesting` | Yes | - | File nesting rules |
| `LocalUserSecrets` | Yes | - | User Secrets management |
| `WebNestingDefaults` | Yes | - | Web-style file nesting patterns |
| `DynamicFileNestingEnabled` | Yes | - | File nesting enabled flag |

---

## Common Use Cases

### Windows Services

Worker projects can be configured to run as Windows Services:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="..." />
</ItemGroup>
```

Use `UseWindowsService()` in your `Program.cs`:
```csharp
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services => { ... })
    .Build();
```

### Linux Systemd Services

Worker projects can be configured to run as Linux systemd services:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting.Systemd" Version="..." />
</ItemGroup>
```

Use `UseSystemd()` in your `Program.cs`:
```csharp
IHost host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureServices(services => { ... })
    .Build();
```

### Kubernetes-Hosted Workers

Worker projects work well as long-running containers in Kubernetes without modification. Just ensure:
- Proper logging configuration
- Health check endpoints if needed
- Graceful shutdown handling

---

## Publishing Considerations

Worker Services can be published using the same mechanisms as other .NET applications:

- **Framework-dependent**: Requires .NET runtime on target
- **Self-contained**: Includes runtime, no dependencies
- **Single-file**: All files bundled into one executable
- **Native AOT**: Ahead-of-time compilation (requires compatible code)

The Worker SDK automatically imports `Microsoft.NET.Sdk.Publish` when `$(WorkerSdkImportPublishSdk)` is `true`, enabling all publishing capabilities.

---

## See Also

- [Project Capabilities Overview](../project-capabilities.md)
- [Microsoft.NET.Sdk Capabilities](Microsoft.NET.Sdk.md)
- [Microsoft.NET.Sdk.Web Capabilities](Microsoft.NET.Sdk.Web.md)
- [Microsoft.NET.Publish Capabilities](Microsoft.NET.Publish.md)
- [Worker Services in .NET](https://learn.microsoft.com/dotnet/core/extensions/workers)
- [Windows Services](https://learn.microsoft.com/dotnet/core/extensions/windows-service)
