# Microsoft.NET.Publish Project Capabilities

This document describes the Project Capabilities provided by the .NET Publishing targets (`Microsoft.NET.Publish`).

## Overview

The Publishing targets define capabilities that reflect various publish-time optimizations and configurations. These capabilities are conditionally added based on MSBuild properties that control how an application is published.

All capabilities in this file are **conditional** - they're only added when specific publish properties are set.

## Capabilities

### IsAotCompatible

**When Added:**
- When `$(IsAotCompatible)` == `true`

**Source:** `Microsoft.NET.Publish.targets`

**Purpose:**
Indicates that the project has been marked as compatible with Native Ahead-of-Time (AOT) compilation. This is a library/package authorship feature that signals the code is designed to work in AOT scenarios.

**Enables:**
- AOT analyzer warnings and guidance
- Tooling that validates AOT compatibility
- NuGet package metadata indicating AOT compatibility
- IDE features for AOT-compatible libraries

**Related:**
- Libraries set `IsAotCompatible=true` to indicate they've been tested with Native AOT
- When `PublishAot=true` is set, `IsAotCompatible` is not automatically set to true - it must be explicitly declared by the library author

**Example:**
```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

---

### IsTrimmable

**When Added:**
- When `$(IsTrimmable)` == `true`
- OR when `$(IsAotCompatible)` == `true` (IsTrimmable is implied by IsAotCompatible)

**Source:** `Microsoft.NET.Publish.targets`

**Purpose:**
Indicates that the project/library has been marked as compatible with IL trimming. This signals that the code has been designed and tested to work correctly when unused code is removed by the IL Linker.

**Enables:**
- Trim analyzer warnings and guidance
- Tooling that validates trim compatibility
- NuGet package metadata indicating trim compatibility
- Trim warnings for consumers using trim-incompatible APIs

**Related:**
- Libraries set `IsTrimmable=true` to indicate they're trim-safe
- Native AOT requires trimming, so `IsAotCompatible=true` implies `IsTrimmable=true`
- Supported in .NET 6.0 and later

**Example:**
```xml
<PropertyGroup>
  <IsTrimmable>true</IsTrimmable>
</PropertyGroup>
```

---

### PublishAot

**When Added:**
- When `$(PublishAot)` == `true`

**Source:** `Microsoft.NET.Publish.targets`

**Purpose:**
Indicates that the application is configured to be published with Native Ahead-of-Time (AOT) compilation. Native AOT compiles the application to native code ahead of time, producing a standalone executable with no JIT requirement.

**Enables:**
- Native AOT compilation during publish
- AOT-specific analyzers and warnings
- IDE features for AOT publishing
- Optimized build configuration for AOT
- Container base image selection (uses runtime-deps instead of runtime)

**Related:**
- Available in .NET 7.0+ (preview), production-ready in .NET 8.0+
- Automatically enables `PublishTrimmed=true`
- Results in faster startup, smaller memory footprint, and smaller app size
- Not compatible with all .NET features (reflection, dynamic code generation)

**Example:**
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

**Publish command:**
```bash
dotnet publish -c Release
```

---

### PublishReadyToRun

**When Added:**
- When `$(PublishReadyToRun)` == `true`

**Source:** `Microsoft.NET.Publish.targets`

**Purpose:**
Indicates that the application is configured to be published with ReadyToRun (R2R) compilation. R2R pre-compiles IL assemblies to native code, improving startup time while maintaining cross-platform IL for fallback.

**Enables:**
- ReadyToRun compilation during publish
- Faster application startup
- IDE publish profile features
- Reduced JIT overhead at runtime

**Related:**
- Available in .NET Core 3.0+
- Results in larger deployment size (contains both R2R and IL code)
- Improves startup performance with minimal compatibility issues
- Can be combined with trimming for additional size reduction

**Example:**
```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

---

### PublishSingleFile

**When Added:**
- When `$(PublishSingleFile)` == `true`

**Source:** `Microsoft.NET.Publish.targets`

**Purpose:**
Indicates that the application is configured to be published as a single executable file. All application files (assemblies, native libraries, etc.) are bundled into a single binary.

**Enables:**
- Single-file publish functionality
- Single-file analyzers and warnings
- IDE publish profile support
- Simplified deployment (one file to distribute)
- Container optimization (fewer layers)

**Related:**
- Available in .NET Core 3.0+ (improved in .NET 5+, further improved in .NET 6+)
- .NET 6+ extracts to memory by default (no extraction to disk)
- Can be combined with `PublishTrimmed` and `PublishReadyToRun`
- Requires `SelfContained=true` or `RuntimeIdentifier` to be set

**Example:**
```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
</PropertyGroup>
```

---

### PublishTrimmed

**When Added:**
- When `$(PublishTrimmed)` == `true`

**Source:** `Microsoft.NET.Publish.targets`

**Purpose:**
Indicates that the application is configured to be published with IL trimming enabled. The IL Linker analyzes the application and removes unused code, reducing deployment size.

**Enables:**
- IL trimming during publish
- Trim analyzers and warnings during build
- IDE features for trim configuration
- Smaller deployment size
- Faster startup (less code to load)

**Related:**
- Available in .NET Core 3.0+ (preview), production-ready in .NET 5+
- Significantly improved in .NET 6+ with trim analyzers
- Required by `PublishAot=true`
- Requires careful testing - may break code that relies on reflection or dynamic code loading
- Supports trim customization via attributes and XML descriptors

**Example:**
```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <SelfContained>true</SelfContained>
</PropertyGroup>
```

**With trim mode:**
```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode> <!-- or 'full' -->
</PropertyGroup>
```

---

## Summary Table

| Capability | Trigger Property | Minimum TFM | Purpose |
|------------|------------------|-------------|---------|
| `IsAotCompatible` | `IsAotCompatible=true` | .NET 7.0 | Library AOT compatibility marker |
| `IsTrimmable` | `IsTrimmable=true` or `IsAotCompatible=true` | .NET 6.0 | Library trim compatibility marker |
| `PublishAot` | `PublishAot=true` | .NET 7.0 | Native AOT compilation |
| `PublishReadyToRun` | `PublishReadyToRun=true` | .NET Core 3.0 | ReadyToRun pre-compilation |
| `PublishSingleFile` | `PublishSingleFile=true` | .NET Core 3.0 | Single-file bundling |
| `PublishTrimmed` | `PublishTrimmed=true` | .NET Core 3.0 | IL trimming |

---

## Capability Relationships

### Automatic Implications

Some publish capabilities automatically enable others:

```
PublishAot=true
  └─> PublishTrimmed=true (automatic)
       └─> IsTrimmable=true (for self-validation)

IsAotCompatible=true
  └─> IsTrimmable=true (automatic, defined in targets)
```

### Common Combinations

**Optimized ASP.NET Core:**
```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <PublishSingleFile>true</PublishSingleFile>
  <PublishReadyToRun>true</PublishReadyToRun>
  <SelfContained>true</SelfContained>
</PropertyGroup>
```

**Maximum Performance (Native AOT):**
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <PublishSingleFile>true</PublishSingleFile>
  <!-- PublishTrimmed is automatically enabled -->
</PropertyGroup>
```

**Library Declaration:**
```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
  <!-- IsTrimmable is automatically enabled -->
</PropertyGroup>
```

---

## Multi-Targeting and Warnings

The publish targets include logic to suppress warnings for correctly multi-targeted projects:

- Projects that multi-target (e.g., `net6.0;net8.0`) might trigger warnings about unsupported features
- If the project includes a TFM that supports the feature (e.g., `net7.0` for AOT), warnings are suppressed
- This prevents noise for libraries that correctly provide both old and new TFM support

**Related Properties:**
- `_FirstTargetFrameworkToSupportTrimming`: `net6.0`
- `_FirstTargetFrameworkToSupportAot`: `net7.0`
- `_FirstTargetFrameworkToSupportSingleFile`: `net6.0`

---

## See Also

- [Project Capabilities Overview](../project-capabilities.md)
- [Microsoft.NET.Sdk Capabilities](Microsoft.NET.Sdk.md)
- [Microsoft.NET.Build.Containers Capabilities](Microsoft.NET.Build.Containers.md)
- [Trim self-contained deployments](https://learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained)
- [Native AOT deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [ReadyToRun compilation](https://learn.microsoft.com/dotnet/core/deploying/ready-to-run)
- [Single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file/)
