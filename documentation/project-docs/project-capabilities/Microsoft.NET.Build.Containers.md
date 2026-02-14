# Microsoft.NET.Build.Containers Project Capabilities

This document describes the Project Capabilities provided by the .NET Container Build support (`Microsoft.NET.Build.Containers`).

## Overview

The .NET SDK includes built-in support for building container images (OCI/Docker images) directly from .NET applications without requiring a Dockerfile. This functionality is available in .NET SDK 7.0.100 and later.

## Capabilities

### NetSdkOCIImageBuild

**When Added:**
- Always added when the Container targets are imported (typically by default in SDK 7.0.100+)

**Source:** `Microsoft.NET.Build.Containers.targets`

**Purpose:**
Indicates that this project has the capability to build OCI (Open Container Initiative) compliant container images directly from the SDK, without requiring a Dockerfile.

**Enables:**
- `dotnet publish /t:PublishContainer` command
- Visual Studio publish to container registries
- Container build integration in IDEs
- Automatic base image selection
- Container configuration through MSBuild properties
- Publishing to local Docker/Podman or remote registries

**Related Properties:**

```xml
<PropertyGroup>
  <!-- Enable/disable container support (enabled by default for non-library projects) -->
  <EnableSdkContainerSupport>true</EnableSdkContainerSupport>
  
  <!-- Container configuration -->
  <ContainerRepository>myapp</ContainerRepository>
  <ContainerImageTag>1.0.0</ContainerImageTag>
  <ContainerRegistry>docker.io</ContainerRegistry>
  
  <!-- Base image (auto-selected if not specified) -->
  <ContainerBaseImage>mcr.microsoft.com/dotnet/runtime:8.0</ContainerBaseImage>
  
  <!-- Runtime identifier for the container -->
  <ContainerRuntimeIdentifier>linux-x64</ContainerRuntimeIdentifier>
  
  <!-- Optional: Local container tool (Docker or Podman) -->
  <LocalRegistry>Docker</LocalRegistry>
</PropertyGroup>
```

---

## Container Build Features

### Automatic Base Image Selection

When `ContainerBaseImage` is not specified, the SDK automatically selects an appropriate base image based on:

- Target framework version
- Whether the app is self-contained or framework-dependent
- Whether Native AOT or trimming is enabled
- Runtime identifier (RID)
- Framework references (e.g., ASP.NET Core)

**Example automatic selections:**
- ASP.NET Core app → `mcr.microsoft.com/dotnet/aspnet:8.0`
- Console app → `mcr.microsoft.com/dotnet/runtime:8.0`
- Self-contained app → `mcr.microsoft.com/dotnet/runtime-deps:8.0`
- AOT app → `mcr.microsoft.com/dotnet/runtime-deps:8.0` (smaller, no runtime needed)

### Supported Target Frameworks

Container building is supported for:
- .NET 7.0 and later (SDK 7.0.100-preview.7 or later)
- Both framework-dependent and self-contained deployments
- Native AOT applications

### Publishing Workflow

**Build and push to a container registry:**
```bash
dotnet publish -c Release -r linux-x64 /t:PublishContainer
```

**Customize container properties:**
```bash
dotnet publish /t:PublishContainer \
  -p ContainerRepository=myapp \
  -p ContainerImageTag=1.2.3 \
  -p ContainerRegistry=myregistry.azurecr.io
```

**Push to Docker local registry:**
```bash
dotnet publish /t:PublishContainer
# Image available in local Docker: myapp:latest
```

### Visual Studio Integration

When using `WebPublishMethod=Container` or the default container publish profile:

1. Visual Studio detects the `NetSdkOCIImageBuild` capability
2. Container publishing UI becomes available
3. Right-click project → Publish → Container Registry
4. Publish profiles can be created for different registries

---

## Container Queries

The Container SDK can query specific project capabilities to optimize the container build:

### DotNetCoreWeb Detection

The container build system checks for the `DotNetCoreWeb` capability to determine if a project is an ASP.NET Core application:

- **If detected:** Uses ASP.NET Core-specific base images (e.g., `mcr.microsoft.com/dotnet/aspnet`)
- **Otherwise:** Uses runtime-only base images (e.g., `mcr.microsoft.com/dotnet/runtime`)

### DotNetCoreWorker Detection

Similarly, it checks for `DotNetCoreWorker` to identify worker service projects and configure appropriate defaults.

---

## Container Configuration

### Environment Variables

```xml
<ItemGroup>
  <ContainerEnvironmentVariable Include="ASPNETCORE_URLS" Value="http://+:8080" />
  <ContainerEnvironmentVariable Include="DOTNET_RUNNING_IN_CONTAINER" Value="true" />
</ItemGroup>
```

### Exposed Ports

```xml
<ItemGroup>
  <ContainerPort Include="8080" Type="tcp" />
  <ContainerPort Include="8081" Type="tcp" />
</ItemGroup>
```

### Container Labels

```xml
<ItemGroup>
  <ContainerLabel Include="org.opencontainers.image.description" Value="My .NET Application" />
  <ContainerLabel Include="org.opencontainers.image.source" Value="https://github.com/myorg/myapp" />
</ItemGroup>
```

### Entry Point and Command

```xml
<PropertyGroup>
  <ContainerEntrypoint>dotnet;MyApp.dll</ContainerEntrypoint>
  <ContainerEntrypointArgs>--urls;http://+:8080</ContainerEntrypointArgs>
</PropertyGroup>
```

---

## SDK Version Requirements

### Version Check

The container targets include a version check to ensure compatibility:

- **Minimum version:** .NET SDK 7.0.100-preview.7
- **Error:** If using an older SDK, `CONTAINER002` error is raised when publishing with `WebPublishMethod=Container`

**Version detection logic:**
```xml
<_IsSDKContainerAllowedVersion>
  Condition="$([MSBuild]::VersionGreaterThan($(NetCoreSdkVersion), 7.0.100))
             OR ( $([MSBuild]::VersionEquals($(NetCoreSdkVersion), 7.0.100))
                  AND (
                       $(NETCoreSdkVersion.Contains('-preview.7'))
                       OR $(NETCoreSdkVersion.Contains('-rc'))
                       OR $(NETCoreSdkVersion.Contains('-')) == false
                      )
                )">true
</_IsSDKContainerAllowedVersion>
```

---

## Summary Table

| Capability | Always Added | Conditional | Purpose |
|------------|--------------|-------------|---------|
| `NetSdkOCIImageBuild` | Yes | - | OCI/Docker container build support |

---

## Common Scenarios

### ASP.NET Core Web Application

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ContainerRepository>mywebapp</ContainerRepository>
    <ContainerImageTag>$(Version)</ContainerImageTag>
  </PropertyGroup>
</Project>
```

```bash
dotnet publish -c Release /t:PublishContainer
```

### Worker Service

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ContainerRepository>myworker</ContainerRepository>
  </PropertyGroup>
</Project>
```

### Self-Contained with Trimming

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <SelfContained>true</SelfContained>
    <PublishTrimmed>true</PublishTrimmed>
    <ContainerRepository>mytrimmedapp</ContainerRepository>
  </PropertyGroup>
</Project>
```

### Native AOT

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <ContainerRepository>myaotapp</ContainerRepository>
  </PropertyGroup>
</Project>
```

---

## See Also

- [Project Capabilities Overview](../project-capabilities.md)
- [Microsoft.NET.Sdk.Web Capabilities](Microsoft.NET.Sdk.Web.md)
- [Microsoft.NET.Sdk.Worker Capabilities](Microsoft.NET.Sdk.Worker.md)
- [Microsoft.NET.Publish Capabilities](Microsoft.NET.Publish.md)
- [.NET SDK Container Building](https://learn.microsoft.com/dotnet/core/docker/publish-as-container)
- [Containerize a .NET app](https://learn.microsoft.com/dotnet/core/docker/build-container)
