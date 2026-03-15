# Project Capabilities in the .NET SDK

## Overview

Project Capabilities are MSBuild items (`ProjectCapability`) that provide declarative metadata about what features, functionality, and behaviors a project supports. These capabilities are used by:

- **IDEs and editors** (like Visual Studio) to enable/disable UI features and customize the project experience
- **CLI tools** (like `dotnet run`, `dotnet publish`, Container publishing) to determine if specific operations are supported
- **Build tools and analyzers** to adapt their behavior based on project characteristics

Project Capabilities are defined in MSBuild SDK targets files and are added to projects automatically based on the SDK, target framework, project properties, and other conditions.

## Documentation Organization

This directory contains detailed documentation for Project Capabilities provided by each SDK in this repository:

- **[Microsoft.NET.Sdk Capabilities](project-capabilities/Microsoft.NET.Sdk.md)** - Core .NET SDK capabilities
- **[Microsoft.NET.Sdk.Web Capabilities](project-capabilities/Microsoft.NET.Sdk.Web.md)** - ASP.NET Core Web SDK capabilities
- **[Microsoft.NET.Sdk.Worker Capabilities](project-capabilities/Microsoft.NET.Sdk.Worker.md)** - Worker Service SDK capabilities
- **[Microsoft.NET.Sdk.Razor Capabilities](project-capabilities/Microsoft.NET.Sdk.Razor.md)** - Razor SDK capabilities
- **[Microsoft.NET.Build.Containers Capabilities](project-capabilities/Microsoft.NET.Build.Containers.md)** - Container build capabilities
- **[Microsoft.NET.Publish Capabilities](project-capabilities/Microsoft.NET.Publish.md)** - Publishing-related capabilities

## Using Project Capabilities

### In MSBuild

Project Capabilities are automatically added by the SDK based on project configuration. You generally don't need to add them manually.

### Querying Capabilities

Tools can query project capabilities through the MSBuild project system interfaces or by evaluating the project and reading the `ProjectCapability` items.

### Contributing

When adding a new `ProjectCapability` item to any SDK in this repository:

1. Add the capability to the appropriate targets file
2. Document it in the corresponding documentation file listed above
3. Include:
   - The capability name
   - When/why it's added (conditions)
   - What features or experiences it enables
   - Any relevant links or additional context

## External References

- [Visual Studio Project System Capabilities](https://github.com/microsoft/vsprojectsystem/blob/master/doc/overview/project_capabilities.md) - General overview of project capabilities in Visual Studio
- [.NET Project System Documentation](https://github.com/dotnet/project-system)

## History

Project Capabilities have been part of the .NET SDK since early versions, but comprehensive documentation was added in .NET 10 (this documentation) to improve discoverability and maintainability.
