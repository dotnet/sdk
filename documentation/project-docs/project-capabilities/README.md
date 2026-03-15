# Project Capabilities Documentation

This directory contains detailed documentation for Project Capabilities provided by each MSBuild SDK in the .NET SDK repository.

## What are Project Capabilities?

Project Capabilities (`ProjectCapability` MSBuild items) are declarative metadata that indicate what features, functionality, and behaviors a project supports. They're used by IDEs, CLI tools, and analyzers to customize the development experience.

## Documentation Files

- **[Microsoft.NET.Sdk.md](Microsoft.NET.Sdk.md)** - Core .NET SDK capabilities
  - `CrossPlatformExecutable`, `NativeAOT`, `SupportsComputeRunCommand`, `GenerateDocumentationFile`, `SupportsHotReload`

- **[Microsoft.NET.Sdk.Web.md](Microsoft.NET.Sdk.Web.md)** - ASP.NET Core Web SDK capabilities
  - `DotNetCoreWeb`, `AspNetCore`, `Web`, `AppServicePublish`, `AspNetCoreInProcessHosting`, `LocalUserSecrets`, file nesting capabilities

- **[Microsoft.NET.Sdk.Worker.md](Microsoft.NET.Sdk.Worker.md)** - Worker Service SDK capabilities
  - `DotNetCoreWorker`, `LocalUserSecrets`, file nesting capabilities

- **[Microsoft.NET.Sdk.Razor.md](Microsoft.NET.Sdk.Razor.md)** - Razor SDK capabilities
  - `DotNetCoreRazor`, `DotNetCoreRazorConfiguration`, `WebNestingDefaults`, `SupportsTypeScriptNuGet`

- **[Microsoft.NET.Build.Containers.md](Microsoft.NET.Build.Containers.md)** - Container build capabilities
  - `NetSdkOCIImageBuild`

- **[Microsoft.NET.Publish.md](Microsoft.NET.Publish.md)** - Publishing-related capabilities
  - `IsAotCompatible`, `IsTrimmable`, `PublishAot`, `PublishReadyToRun`, `PublishSingleFile`, `PublishTrimmed`

## Quick Reference

### Finding Documentation

To find documentation for a specific capability:
1. Identify which SDK defines the capability (look at the targets file where it's added)
2. Open the corresponding documentation file above
3. Search for the capability name (each has its own section with a heading)

### Adding New Capabilities

When adding a new `ProjectCapability` item:
1. Add it in the appropriate targets file
2. Document it in the corresponding file in this directory
3. Include: name, conditions, purpose, what it enables, and any related information

See the [SDK PR Guide](../SDK-PR-guide.md#adding-new-projectcapability-items) for full details.

### Automated Detection

A GitHub Actions workflow automatically detects new `ProjectCapability` items in pull requests and reminds contributors to update documentation. See [.github/workflows/detect-project-capabilities.yml](../../../.github/workflows/detect-project-capabilities.yml).

## See Also

- [Project Capabilities Overview](../project-capabilities.md) - Main documentation entry point
- [Visual Studio Project System Capabilities](https://github.com/microsoft/vsprojectsystem/blob/master/doc/overview/project_capabilities.md) - General VS project system docs
- [.NET Project System Documentation](https://github.com/dotnet/project-system) - Project system implementation
