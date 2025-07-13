# Unified Configuration System for .NET CLI

## Overview

This specification outlines the plan to refactor the .NET CLI codebase to replace direct usage of global.json and environment variables with a unified configuration system based on Microsoft.Extensions.Configuration.IConfiguration.

## Goals

- Replace direct `Environment.GetEnvironmentVariable()` calls with a unified configuration system
- Replace direct `global.json` file reading with configuration providers
- Establish a clear configuration hierarchy and precedence
- Maintain backward compatibility with existing environment variables and global.json usage
- Provide a foundation for future configuration enhancements (e.g., dotnet.config support)

## Current State Analysis

The codebase currently has:
1. Direct `Environment.GetEnvironmentVariable()` calls throughout various classes
2. An `EnvironmentProvider` abstraction that wraps environment variable access
3. Custom `global.json` reading in several places (e.g., `GlobalJsonWorkloadSetsFile.cs`, `RuntimeConfig.cs`)
4. Some use of Microsoft.Extensions.Configuration in test infrastructure but not in the main CLI

## Configuration Hierarchy

The new unified configuration system will follow this precedence order (highest to lowest priority):

1. **Command-line arguments** (handled separately by existing System.CommandLine infrastructure)
2. **Environment variables with DOTNET_ prefix** (e.g., `DOTNET_CLI_TELEMETRY_OPTOUT`)
3. **global.json** (custom configuration provider)
4. **dotnet.config** (future enhancement - INI configuration file)

**Note:** System-level environment variables without the DOTNET_ prefix (e.g., `PATH`, `HOME`, `TEMP`) will continue to be accessed directly through the existing `IEnvironmentProvider` interface as they are not specific to .NET CLI configuration.

## Implementation Plan

### Phase 1: Infrastructure

#### 1.1 Core Configuration Builder with Strongly-Typed Configuration

Create a centralized configuration builder in the Microsoft.Extensions.Configuration.DotnetCli project:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Services/DotNetConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Services;

public class DotNetConfiguration
{
    public static IConfiguration Create(string workingDirectory = null)
    {
        var builder = new ConfigurationBuilder();

        // Priority order (last wins):
        // 1. dotnet.config (if it exists) - with section-based key mapping
        // 2. global.json (custom provider with key mapping)
        // 3. Environment variables with DOTNET_ prefix (with key mapping)
        // 4. Command line arguments (handled separately)

        workingDirectory ??= Directory.GetCurrentDirectory();

        // Add dotnet.config if it exists (future enhancement)
        var dotnetConfigPath = Path.Combine(workingDirectory, "dotnet.config");
        if (File.Exists(dotnetConfigPath))
        {
            builder.AddIniFile(dotnetConfigPath, optional: true, reloadOnChange: false);
        }

        // Add global.json with a custom configuration provider that maps keys
        builder.Add(new GlobalJsonConfigurationSource(workingDirectory));

        // Add DOTNET_ prefixed environment variables with key mapping
        builder.Add(new DotNetEnvironmentConfigurationSource());

        return builder.Build();
    }

    public static DotNetConfigurationRoot CreateTyped(string workingDirectory = null)
    {
        var configuration = Create(workingDirectory);
        return new DotNetConfigurationRoot(configuration);
    }

    // Lightweight factory for scenarios that only need basic configuration access
    public static DotNetConfigurationRoot CreateMinimal(string workingDirectory = null)
    {
        var builder = new ConfigurationBuilder();
        workingDirectory ??= Directory.GetCurrentDirectory();

        // Only add environment variables for minimal overhead
        builder.Add(new DotNetEnvironmentConfigurationSource());

        var configuration = builder.Build();
        return new DotNetConfigurationRoot(configuration);
    }
}
```

**Performance Considerations:**
- **Lazy Initialization**: All strongly-typed configuration properties use `Lazy<T>` to defer expensive binding operations until first access
- **Minimal Factory**: `CreateMinimal()` provides a lightweight option that only loads environment variables
- **Provider Ordering**: Most expensive providers (global.json file I/O) are added last to minimize impact when not needed

#### 1.2 Enhanced Global.json Configuration Provider

Create a custom configuration provider for global.json files with key mapping:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Providers/GlobalJsonConfigurationProvider.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

public class GlobalJsonConfigurationProvider : ConfigurationProvider
{
    private readonly string _path;

    private static readonly Dictionary<string, string> GlobalJsonKeyMappings = new()
    {
        ["sdk:version"] = "sdk:version",
        ["sdk:rollForward"] = "sdk:rollforward",
        ["sdk:allowPrerelease"] = "sdk:allowprerelease",
        ["msbuild-sdks"] = "msbuild:sdks",
        // Add more mappings as the global.json schema evolves
    };

    public GlobalJsonConfigurationProvider(string workingDirectory)
    {
        _path = FindGlobalJson(workingDirectory);
    }

    public override void Load()
    {
        Data.Clear();

        if (_path == null || !File.Exists(_path))
            return;

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonDocument.Parse(json);

            LoadGlobalJsonData(document.RootElement, "");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error parsing global.json at {_path}", ex);
        }
    }

    private void LoadGlobalJsonData(JsonElement element, string prefix)
    {
        foreach (var property in element.EnumerateObject())
        {
            var rawKey = string.IsNullOrEmpty(prefix)
                ? property.Name
                : $"{prefix}:{property.Name}";

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    LoadGlobalJsonData(property.Value, rawKey);
                    break;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    // Map to canonical key format
                    var canonicalKey = MapGlobalJsonKey(rawKey);
                    Data[canonicalKey] = GetValueAsString(property.Value);
                    break;
            }
        }
    }

    private string MapGlobalJsonKey(string rawKey)
    {
        // Check for exact mapping first
        if (GlobalJsonKeyMappings.TryGetValue(rawKey, out var mapped))
            return mapped;

        // For msbuild-sdks, convert to msbuild:sdks:packagename format
        if (rawKey.StartsWith("msbuild-sdks:"))
            return rawKey.Replace("msbuild-sdks:", "msbuild:sdks:");

        // Default: convert to lowercase and normalize separators
        return rawKey.ToLowerInvariant().Replace("-", ":");
    }

    private string GetValueAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    private string FindGlobalJson(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current != null)
        {
            var globalJsonPath = Path.Combine(current.FullName, "global.json");
            if (File.Exists(globalJsonPath))
                return globalJsonPath;
            current = current.Parent;
        }
        return null;
    }
}
```

#### 1.3 Environment Variable Configuration Provider with Key Mapping

Create a custom environment variable provider that maps DOTNET_ variables to canonical keys:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Providers/DotNetEnvironmentConfigurationProvider.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

public class DotNetEnvironmentConfigurationProvider : ConfigurationProvider
{
    private static readonly Dictionary<string, string> EnvironmentKeyMappings = new()
    {
        ["DOTNET_HOST_PATH"] = "dotnet:host:path",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "dotnet:cli:telemetry:optout",
        ["DOTNET_NOLOGO"] = "dotnet:cli:nologo",
        ["DOTNET_CLI_PERF_LOG"] = "dotnet:cli:perf:log",
        ["DOTNET_MULTILEVEL_LOOKUP"] = "dotnet:host:multilevel:lookup",
        ["DOTNET_ROLL_FORWARD"] = "dotnet:runtime:rollforward",
        ["DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX"] = "dotnet:runtime:rollforward:onnocandidate",
        ["DOTNET_CLI_HOME"] = "dotnet:cli:home",
        ["DOTNET_CLI_CONTEXT_VERBOSE"] = "dotnet:cli:context:verbose",
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "dotnet:cli:firsttime:skip",
        ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "dotnet:tools:addtopath",
        // Add more mappings as needed
    };

    public override void Load()
    {
        Data.Clear();

        foreach (var mapping in EnvironmentKeyMappings)
        {
            var value = Environment.GetEnvironmentVariable(mapping.Key);
            if (!string.IsNullOrEmpty(value))
            {
                Data[mapping.Value] = value;
            }
        }
    }
}

public class DotNetEnvironmentConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotNetEnvironmentConfigurationProvider();
    }
}
```

#### 1.4 Strongly-Typed Configuration Root with Lazy Initialization

Create a strongly-typed configuration service that uses lazy initialization and the configuration binding source generator:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Services/DotNetConfigurationRoot.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Services;

using Microsoft.Extensions.Configuration.DotnetCli.Models;

public class DotNetConfigurationRoot
{
    private readonly IConfiguration _configuration;

    // Lazy initialization for each configuration section
    private readonly Lazy<CliUserExperienceConfiguration> _cliUserExperience;
    private readonly Lazy<RuntimeHostConfiguration> _runtimeHost;
    private readonly Lazy<BuildConfiguration> _build;
    private readonly Lazy<SdkResolverConfiguration> _sdkResolver;
    private readonly Lazy<WorkloadConfiguration> _workload;
    private readonly Lazy<FirstTimeUseConfiguration> _firstTimeUse;
    private readonly Lazy<DevelopmentConfiguration> _development;
    private readonly Lazy<ToolConfiguration> _tool;

    public DotNetConfigurationRoot(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Initialize lazy factories - configuration binding only happens on first access
        _cliUserExperience = new Lazy<CliUserExperienceConfiguration>(() =>
            GetConfigurationSection<CliUserExperienceConfiguration>("CliUserExperience") ?? new());
        _runtimeHost = new Lazy<RuntimeHostConfiguration>(() =>
            GetConfigurationSection<RuntimeHostConfiguration>("RuntimeHost") ?? new());
        _build = new Lazy<BuildConfiguration>(() =>
            GetConfigurationSection<BuildConfiguration>("Build") ?? new());
        _sdkResolver = new Lazy<SdkResolverConfiguration>(() =>
            GetConfigurationSection<SdkResolverConfiguration>("SdkResolver") ?? new());
        _workload = new Lazy<WorkloadConfiguration>(() =>
            GetConfigurationSection<WorkloadConfiguration>("Workload") ?? new());
        _firstTimeUse = new Lazy<FirstTimeUseConfiguration>(() =>
            GetConfigurationSection<FirstTimeUseConfiguration>("FirstTimeUse") ?? new());
        _development = new Lazy<DevelopmentConfiguration>(() =>
            GetConfigurationSection<DevelopmentConfiguration>("Development") ?? new());
        _tool = new Lazy<ToolConfiguration>(() =>
            GetConfigurationSection<ToolConfiguration>("Tool") ?? new());
    }

    public IConfiguration RawConfiguration => _configuration;

    // Lazy-loaded strongly-typed configuration properties
    public CliUserExperienceConfiguration CliUserExperience => _cliUserExperience.Value;
    public RuntimeHostConfiguration RuntimeHost => _runtimeHost.Value;
    public BuildConfiguration Build => _build.Value;
    public SdkResolverConfiguration SdkResolver => _sdkResolver.Value;
    public WorkloadConfiguration Workload => _workload.Value;
    public FirstTimeUseConfiguration FirstTimeUse => _firstTimeUse.Value;
    public DevelopmentConfiguration Development => _development.Value;
    public ToolConfiguration Tool => _tool.Value;

    // Generic value access for backward compatibility
    public string? GetValue(string key, string? defaultValue = null) => _configuration[key] ?? defaultValue;
    public T GetValue<T>(string key, T defaultValue = default) => _configuration.GetValue<T>(key, defaultValue);

    private T? GetConfigurationSection<T>(string sectionName) where T : class, new()
    {
        var section = _configuration.GetSection(sectionName);
        // Uses configuration binding source generator for AOT-friendly binding
        return section.Exists() ? section.Get<T>() : null;
    }
}
```

    public T GetValue<T>(string canonicalKey, T defaultValue = default)
    {
        return Configuration.GetValue<T>(canonicalKey, defaultValue);
    }

    public bool GetBoolValue(string canonicalKey, bool defaultValue = false)
    {
        var value = Configuration[canonicalKey];
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }

    // Helper methods for common configuration values
    public bool IsTelemetryOptOut() => GetBoolValue("dotnet:cli:telemetry:optout", false);
    public bool IsNoLogo() => GetBoolValue("dotnet:cli:nologo", false);
    public string GetHostPath() => GetValue("dotnet:host:path");
    public string GetSdkVersion() => GetValue("sdk:version");
}
```

### Phase 2: Integration

#### 2.1 Update Program.cs

Update the main entry point to initialize configuration early with canonical key access:

```csharp
public class Program
{
    public static IConfiguration GlobalConfiguration { get; private set; }
    public static IConfigurationService ConfigurationService { get; private set; }

    public static int Main(string[] args)
    {
        // Initialize configuration early
        GlobalConfiguration = DotNetConfiguration.Create();
        ConfigurationService = new ConfigurationService(GlobalConfiguration);

        // Replace direct env var calls with configuration using canonical keys
        bool perfLogEnabled = ConfigurationService.GetBoolValue("dotnet:cli:perf:log", false);
        bool noLogo = ConfigurationService.IsNoLogo();

        // Continue with existing logic...
    }
}
```

#### 2.2 Configuration-Based Environment Provider with Key Mapping

Create a bridge between the new configuration system and existing `IEnvironmentProvider` interface:

```csharp
public class ConfigurationBasedEnvironmentProvider : IEnvironmentProvider
{
    private readonly IConfigurationService _configurationService;
    private readonly IEnvironmentProvider _fallbackProvider;

    // Reverse mapping from environment variable names to canonical keys
    private static readonly Dictionary<string, string> EnvironmentToCanonicalMappings = new()
    {
        ["DOTNET_HOST_PATH"] = "dotnet:host:path",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "dotnet:cli:telemetry:optout",
        ["DOTNET_NOLOGO"] = "dotnet:cli:nologo",
        ["DOTNET_CLI_PERF_LOG"] = "dotnet:cli:perf:log",
        ["DOTNET_MULTILEVEL_LOOKUP"] = "dotnet:host:multilevel:lookup",
        ["DOTNET_ROLL_FORWARD"] = "dotnet:runtime:rollforward",
        ["DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX"] = "dotnet:runtime:rollforward:onnocandidate",
        // Add more mappings as needed
    };

    public ConfigurationBasedEnvironmentProvider(
        IConfigurationService configurationService,
        IEnvironmentProvider fallbackProvider = null)
    {
        _configurationService = configurationService;
        _fallbackProvider = fallbackProvider ?? new EnvironmentProvider();
    }

    public string GetEnvironmentVariable(string name)
    {
        // For DOTNET_ prefixed variables, try configuration service first using canonical key
        if (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) &&
            EnvironmentToCanonicalMappings.TryGetValue(name, out var canonicalKey))
        {
            var configValue = _configurationService.GetValue(canonicalKey);
            if (configValue != null)
                return configValue;
        }

        // For all other variables or if not found in config, use fallback provider
        return _fallbackProvider.GetEnvironmentVariable(name);
    }

    public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
    {
        // For DOTNET_ prefixed variables, use configuration service with canonical key
        if (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) &&
            EnvironmentToCanonicalMappings.TryGetValue(name, out var canonicalKey))
        {
            return _configurationService.GetBoolValue(canonicalKey, defaultValue);
        }

        // For all other variables, use fallback provider
        return _fallbackProvider.GetEnvironmentVariableAsBool(name, defaultValue);
    }

    // Implement other members as pass-through to fallback provider
    public IEnumerable<string> ExecutableExtensions => _fallbackProvider.ExecutableExtensions;

    public string GetCommandPath(string commandName, params string[] extensions)
        => _fallbackProvider.GetCommandPath(commandName, extensions);

    public string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
        => _fallbackProvider.GetCommandPathFromRootPath(rootPath, commandName, extensions);

    public string GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions)
        => _fallbackProvider.GetCommandPathFromRootPath(rootPath, commandName, extensions);

    public string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
        => _fallbackProvider.GetEnvironmentVariable(variable, target);

    public void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)
        => _fallbackProvider.SetEnvironmentVariable(variable, value, target);
}
```

### Phase 3: Migration

#### 3.1 Systematic Replacement

Replace direct environment variable access patterns using canonical keys:

**Before:**
```csharp
var value = Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT");
bool optOut = !string.IsNullOrEmpty(value) &&
              (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1"));
```

**After (using canonical key):**
```csharp
bool optOut = ConfigurationService.GetBoolValue("dotnet:cli:telemetry:optout", false);
// Or using the helper method:
bool optOut = ConfigurationService.IsTelemetryOptOut();
```

**Before:**
```csharp
var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
```

**After (using canonical key):**
```csharp
var hostPath = ConfigurationService.GetValue("dotnet:host:path");
// Or using the helper method:
var hostPath = ConfigurationService.GetHostPath();
```

#### 3.2 Update Direct global.json Readers

Classes that currently read global.json directly should be updated to use the configuration system:

**Files to update:**
- `src/Cli/dotnet/Commands/Workload/GlobalJsonWorkloadSetFile.cs`
- `src/Cli/dotnet/RuntimeConfig.cs`
- Any other direct global.json readers found during implementation

#### 3.3 Update Environment Provider Usages

Update all instances where `IEnvironmentProvider` is used to use the new `ConfigurationBasedEnvironmentProvider`.

### Phase 4: Testing & Validation

#### 4.1 Backward Compatibility Testing

Ensure all existing functionality continues to work:
- All DOTNET_ prefixed environment variables continue to be respected
- System environment variables (PATH, HOME, etc.) continue to work through the fallback provider
- global.json files are read correctly
- Precedence rules work as expected

#### 4.2 Unit Tests

Create comprehensive unit tests for:
- `DotNetConfiguration` class
- `GlobalJsonConfigurationProvider`
- `ConfigurationService`
- `ConfigurationBasedEnvironmentProvider`

#### 4.3 Integration Tests

Add integration tests to verify:
- Configuration hierarchy works correctly for DOTNET_ prefixed variables
- System environment variables continue to work through the fallback provider
- global.json files in various directory structures are found
- Environment variable override behavior works correctly

## Package Dependencies

The following NuGet package references will need to be added to relevant projects:

- `Microsoft.Extensions.Configuration`
- `Microsoft.Extensions.Configuration.Abstractions`
- `Microsoft.Extensions.Configuration.EnvironmentVariables`
- `Microsoft.Extensions.Configuration.Json`
- `Microsoft.Extensions.Configuration.Ini`
- `Microsoft.Extensions.Configuration.Binder`

### Microsoft.Extensions.Configuration.DotnetCli Project

Create a new project `src/Microsoft.Extensions.Configuration.DotnetCli/Microsoft.Extensions.Configuration.DotnetCli.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Ini" />
  </ItemGroup>

</Project>
```

**Key Features:**
- **Configuration Binding Source Generator**: `EnableConfigurationBindingGenerator=true` enables AOT and trim-friendly configuration binding
- **Strongly-typed Configuration**: Uses `ConfigurationBinder.Get<T>()` for type-safe configuration access
- **.NET 8+ Features**: Takes advantage of C# 12 interceptors for source generation

**Note:** The configuration binding source generator provides Native AOT and trim-friendly configuration binding without reflection. This is essential for performance and compatibility with .NET's trimming and AOT compilation features.

### Project Structure

The Microsoft.Extensions.Configuration.DotnetCli project will be organized as follows:

```
src/Microsoft.Extensions.Configuration.DotnetCli/
├── Microsoft.Extensions.Configuration.DotnetCli.csproj
├── Models/
│   ├── CliUserExperienceConfiguration.cs
│   ├── RuntimeHostConfiguration.cs
│   ├── BuildConfiguration.cs
│   ├── SdkResolverConfiguration.cs
│   ├── WorkloadConfiguration.cs
│   ├── FirstTimeUseConfiguration.cs
│   ├── DevelopmentConfiguration.cs
│   └── ToolConfiguration.cs
├── Providers/
│   ├── GlobalJsonConfigurationProvider.cs
│   ├── GlobalJsonConfigurationSource.cs
│   ├── DotNetEnvironmentConfigurationProvider.cs
│   └── DotNetEnvironmentConfigurationSource.cs
└── Services/
    ├── DotNetConfiguration.cs
    ├── DotNetConfigurationRoot.cs
    ├── DotNetConfigurationService.cs
    └── DotNetConfigurationService.cs
```

This structure separates concerns into logical groupings:
- **Models/**: Strongly-typed configuration model classes optimized for source generator
- **Providers/**: Custom configuration providers for global.json and DOTNET_ environment variables
- **Services/**: Main configuration services and builders for consumer applications

## Key Mapping Reference

### Environment Variables → Canonical Keys
- `DOTNET_CLI_TELEMETRY_OPTOUT` → `dotnet:cli:telemetry:optout`
- `DOTNET_NOLOGO` → `dotnet:cli:nologo`
- `DOTNET_HOST_PATH` → `dotnet:host:path`
- `DOTNET_CLI_PERF_LOG` → `dotnet:cli:perf:log`
- `DOTNET_MULTILEVEL_LOOKUP` → `dotnet:host:multilevel:lookup`
- `DOTNET_ROLL_FORWARD` → `dotnet:runtime:rollforward`

### Global.json → Canonical Keys
```json
{
  "sdk": {
    "version": "6.0.100"
  },
  "msbuild-sdks": {
    "Microsoft.Build.Traversal": "1.0.0"
  }
}
```

Maps to canonical keys:
- `sdk:version` → `"6.0.100"`
- `msbuild:sdks:Microsoft.Build.Traversal` → `"1.0.0"`

### INI Configuration → Canonical Keys
```ini
[dotnet.cli]
telemetryOptOut=true
nologo=true

[sdk]
version=6.0.100
```

Maps to canonical keys:
- `dotnet:cli:telemetry:optout` → `"true"`
- `dotnet:cli:nologo` → `"true"`
- `sdk:version` → `"6.0.100"`

**Note:** System-level environment variables without the DOTNET_ prefix (e.g., `PATH`, `HOME`, `TEMP`, `USER`) will continue to be accessed directly through the existing `IEnvironmentProvider` interface and will not be part of the unified configuration system.

## Typed Configuration Models

### Configuration Binding Source Generator Compatibility

All configuration model classes are designed to work with the configuration binding source generator, providing AOT and trim-friendly configuration binding through `ConfigurationBinder.Get<T>()` calls.

### Functional Configuration Groupings

Based on analysis of the existing codebase, the following functional groupings have been identified for typed configuration models:

#### 1. **CLI User Experience Configuration**
Settings that control the CLI's user interface and interaction behavior:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/CliUserExperienceConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class CliUserExperienceConfiguration
{
    public bool TelemetryOptOut { get; set; } = false;
    public bool NoLogo { get; set; } = false;
    public bool ForceUtf8Encoding { get; set; } = false;
    public string? UILanguage { get; set; }
    public string? TelemetryProfile { get; set; }
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_TELEMETRY_OPTOUT` → `TelemetryOptOut`
- `DOTNET_NOLOGO` → `NoLogo`
- `DOTNET_CLI_FORCE_UTF8_ENCODING` → `ForceUtf8Encoding`
- `DOTNET_CLI_UI_LANGUAGE` → `UILanguage`
- `DOTNET_CLI_TELEMETRY_PROFILE` → `TelemetryProfile`

#### 2. **Runtime Host Configuration**
Settings that control .NET runtime host behavior:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/RuntimeHostConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class RuntimeHostConfiguration
{
    public string? HostPath { get; set; }
    public bool MultilevelLookup { get; set; } = true;
    public string? RollForward { get; set; }
    public string? RollForwardOnNoCandidateFx { get; set; }
    public string? Root { get; set; }
    public string? RootX86 { get; set; }
}
```

**Environment Variables Mapped:**
- `DOTNET_HOST_PATH` → `HostPath`
- `DOTNET_MULTILEVEL_LOOKUP` → `MultilevelLookup`
- `DOTNET_ROLL_FORWARD` → `RollForward`
- `DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX` → `RollForwardOnNoCandidateFx`
- `DOTNET_ROOT` → `Root`
- `DOTNET_ROOT(x86)` → `RootX86`

#### 3. **Build and MSBuild Configuration**
Settings that control build system behavior:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/BuildConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class BuildConfiguration
{
    public bool RunMSBuildOutOfProc { get; set; } = false;
    public bool UseMSBuildServer { get; set; } = false;
    public string? ConfigureMSBuildTerminalLogger { get; set; }
    public bool DisablePublishAndPackRelease { get; set; } = false;
    public bool LazyPublishAndPackReleaseForSolutions { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC` → `RunMSBuildOutOfProc`
- `DOTNET_CLI_USE_MSBUILD_SERVER` → `UseMSBuildServer`
- `DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER` → `ConfigureMSBuildTerminalLogger`
- `DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE` → `DisablePublishAndPackRelease`
- `DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS` → `LazyPublishAndPackReleaseForSolutions`

#### 4. **SDK Resolver Configuration**
Settings that control SDK resolution and discovery:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/SdkResolverConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class SdkResolverConfiguration
{
    public bool EnableLog { get; set; } = false;
    public string? SdksDirectory { get; set; }
    public string? SdksVersion { get; set; }
    public string? CliDirectory { get; set; }
}
```

**Environment Variables Mapped:**
- `DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG` → `EnableLog`
- `DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR` → `SdksDirectory`
- `DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER` → `SdksVersion`
- `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` → `CliDirectory`

#### 5. **Workload Configuration**
Settings that control workload management and behavior:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/WorkloadConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class WorkloadConfiguration
{
    public bool UpdateNotifyDisable { get; set; } = false;
    public int UpdateNotifyIntervalHours { get; set; } = 24;
    public bool DisablePackGroups { get; set; } = false;
    public bool SkipIntegrityCheck { get; set; } = false;
    public string[]? ManifestRoots { get; set; }
    public string[]? PackRoots { get; set; }
    public bool ManifestIgnoreDefaultRoots { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE` → `UpdateNotifyDisable`
- `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS` → `UpdateNotifyIntervalHours`
- `DOTNET_CLI_WORKLOAD_DISABLE_PACK_GROUPS` → `DisablePackGroups`
- `DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK` → `SkipIntegrityCheck`
- `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS` → `ManifestRoots`
- `DOTNETSDK_WORKLOAD_PACK_ROOTS` → `PackRoots`
- `DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS` → `ManifestIgnoreDefaultRoots`

#### 6. **First Time Use Configuration**
Settings that control first-time user experience setup:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/FirstTimeUseConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class FirstTimeUseConfiguration
{
    public bool GenerateAspNetCertificate { get; set; } = true;
    public bool AddGlobalToolsToPath { get; set; } = true;
    public bool SkipFirstTimeExperience { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_GENERATE_ASPNET_CERTIFICATE` → `GenerateAspNetCertificate`
- `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH` → `AddGlobalToolsToPath`
- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` → `SkipFirstTimeExperience`

#### 7. **Development and Debugging Configuration**
Settings that control development tools and debugging features:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/DevelopmentConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class DevelopmentConfiguration
{
    public bool PerfLogEnabled { get; set; } = false;
    public string? PerfLogCount { get; set; }
    public string? CliHome { get; set; }
    public bool ContextVerbose { get; set; } = false;
    public bool AllowTargetingPackCaching { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_CLI_PERF_LOG` → `PerfLogEnabled`
- `DOTNET_PERF_LOG_COUNT` → `PerfLogCount`
- `DOTNET_CLI_HOME` → `CliHome`
- `DOTNET_CLI_CONTEXT_VERBOSE` → `ContextVerbose`
- `DOTNETSDK_ALLOW_TARGETING_PACK_CACHING` → `AllowTargetingPackCaching`

#### 8. **Tool and Global Tool Configuration**
Settings that control global tools behavior:

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Models/ToolConfiguration.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

public sealed class ToolConfiguration
{
    public bool AllowManifestInRoot { get; set; } = false;
}
```

**Environment Variables Mapped:**
- `DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT` → `AllowManifestInRoot`

### Strongly-Typed Configuration Service with Source Generator

```csharp
// src/Microsoft.Extensions.Configuration.DotnetCli/Services/DotNetConfigurationService.cs
namespace Microsoft.Extensions.Configuration.DotnetCli.Services;

using Microsoft.Extensions.Configuration.DotnetCli.Models;

public interface DotNetConfigurationService
{
    IConfiguration RawConfiguration { get; }

    // Strongly-typed configuration access
    CliUserExperienceConfiguration CliUserExperience { get; }
    RuntimeHostConfiguration RuntimeHost { get; }
    BuildConfiguration Build { get; }
    SdkResolverConfiguration SdkResolver { get; }
    WorkloadConfiguration Workload { get; }
    FirstTimeUseConfiguration FirstTimeUse { get; }
    DevelopmentConfiguration Development { get; }
    ToolConfiguration Tool { get; }
}

// src/Microsoft.Extensions.Configuration.DotnetCli/Services/DotNetConfigurationService.cs
public class DotNetConfigurationService : DotNetConfigurationService
{
    private readonly IConfiguration _configuration;

    // Lazy initialization for each configuration section
    private readonly Lazy<CliUserExperienceConfiguration> _cliUserExperience;
    private readonly Lazy<RuntimeHostConfiguration> _runtimeHost;
    private readonly Lazy<BuildConfiguration> _build;
    private readonly Lazy<SdkResolverConfiguration> _sdkResolver;
    private readonly Lazy<WorkloadConfiguration> _workload;
    private readonly Lazy<FirstTimeUseConfiguration> _firstTimeUse;
    private readonly Lazy<DevelopmentConfiguration> _development;
    private readonly Lazy<ToolConfiguration> _tool;

    public IConfiguration RawConfiguration => _configuration;

    // Lazy-loaded strongly-typed configuration properties
    public CliUserExperienceConfiguration CliUserExperience => _cliUserExperience.Value;
    public RuntimeHostConfiguration RuntimeHost => _runtimeHost.Value;
    public BuildConfiguration Build => _build.Value;
    public SdkResolverConfiguration SdkResolver => _sdkResolver.Value;
    public WorkloadConfiguration Workload => _workload.Value;
    public FirstTimeUseConfiguration FirstTimeUse => _firstTimeUse.Value;
    public DevelopmentConfiguration Development => _development.Value;
    public ToolConfiguration Tool => _tool.Value;

    public DotNetConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Initialize lazy factories - configuration binding only happens on first access
        _cliUserExperience = new Lazy<CliUserExperienceConfiguration>(() =>
            _configuration.GetSection("CliUserExperience").Get<CliUserExperienceConfiguration>() ?? new());
        _runtimeHost = new Lazy<RuntimeHostConfiguration>(() =>
            _configuration.GetSection("RuntimeHost").Get<RuntimeHostConfiguration>() ?? new());
        _build = new Lazy<BuildConfiguration>(() =>
            _configuration.GetSection("Build").Get<BuildConfiguration>() ?? new());
        _sdkResolver = new Lazy<SdkResolverConfiguration>(() =>
            _configuration.GetSection("SdkResolver").Get<SdkResolverConfiguration>() ?? new());
        _workload = new Lazy<WorkloadConfiguration>(() =>
            _configuration.GetSection("Workload").Get<WorkloadConfiguration>() ?? new());
        _firstTimeUse = new Lazy<FirstTimeUseConfiguration>(() =>
            _configuration.GetSection("FirstTimeUse").Get<FirstTimeUseConfiguration>() ?? new());
        _development = new Lazy<DevelopmentConfiguration>(() =>
            _configuration.GetSection("Development").Get<DevelopmentConfiguration>() ?? new());
        _tool = new Lazy<ToolConfiguration>(() =>
            _configuration.GetSection("Tool").Get<ToolConfiguration>() ?? new());
    }
}
```

        RuntimeHost = new RuntimeHostConfiguration();
        configuration.GetSection("dotnet:host").Bind(RuntimeHost);

        Build = new BuildConfiguration();
        configuration.GetSection("dotnet:build").Bind(Build);

        SdkResolver = new SdkResolverConfiguration();
        configuration.GetSection("dotnet:sdkresolver").Bind(SdkResolver);

        Workload = new WorkloadConfiguration();
        configuration.GetSection("dotnet:workload").Bind(Workload);

        FirstTimeUse = new FirstTimeUseConfiguration();
        configuration.GetSection("dotnet:firsttime").Bind(FirstTimeUse);

        Development = new DevelopmentConfiguration();
        configuration.GetSection("dotnet:development").Bind(Development);

        Tool = new ToolConfiguration();
        configuration.GetSection("dotnet:tool").Bind(Tool);
    }
}
```

### Benefits of Strongly-Typed Configuration with Lazy Initialization

1. **Minimal Startup Cost**: Lazy initialization means configuration binding only happens when properties are actually accessed
2. **Native AOT and Trim-Friendly**: Source generator eliminates reflection, making the code compatible with Native AOT compilation and trimming
3. **Performance**: Generated code is faster than reflection-based binding at runtime, and lazy loading reduces unnecessary work
4. **Memory Efficiency**: Configuration sections are only allocated when needed, reducing memory pressure during startup
5. **Intellisense and Compile-time Safety**: Developers get full IDE support and compile-time checking
6. **Logical Grouping**: Related settings are grouped together functionally
7. **Type Safety**: Boolean values are actual booleans, integers are integers, etc.
8. **Default Values**: Clear default values are defined in the model classes
9. **Discoverability**: Developers can explore configuration options through the object model
10. **Validation**: Can add data annotations for validation
11. **Documentation**: Each property can have XML documentation
12. **Source Generation**: Using `ConfigurationBinder.Get<T>()` with `EnableConfigurationBindingGenerator=true` generates efficient binding code

### Usage Examples with Lazy Initialization

```csharp
// Fast startup - configuration service creation is very lightweight
var config = DotNetConfiguration.CreateTyped();

// First access triggers lazy binding of only the CliUserExperience section
if (config.CliUserExperience.TelemetryOptOut)
{
    // Skip telemetry - only this section is bound, others remain uninitialized
}

// Subsequent access to the same section is fast (cached)
bool noLogo = config.CliUserExperience.NoLogo;

// Other sections are only bound when first accessed
string? hostPath = config.RuntimeHost.HostPath; // RuntimeHost section bound here
bool enableLogs = config.SdkResolver.EnableLog; // SdkResolver section bound here

// For scenarios that only need environment variables (fastest startup):
var minimalConfig = DotNetConfiguration.CreateMinimal();
if (minimalConfig.CliUserExperience.TelemetryOptOut)
{
    // Only environment variable provider was initialized
}

// Instead of error-prone string-based access:
// bool telemetryOptOut = configService.GetBoolValue("dotnet:cli:telemetry:optout", false);
```

This approach eliminates the need to remember canonical key names and provides a much more developer-friendly API.

## Error Handling

- **Malformed global.json**: Log warning and continue without global.json configuration
- **Missing files**: Silently ignore missing optional configuration files
- **Configuration access errors**: Provide meaningful error messages and fallback to defaults

## Future Enhancements

### dotnet.config Support

A future enhancement could add support for a `dotnet.config` INI file that provides project-specific configuration:

```ini
[cli]
telemetryOptOut=true
noLogo=true

[build]
configuration=Release
```

This would map to configuration keys like:
- `cli:telemetryOptOut` → `"true"`
- `cli:noLogo` → `"true"`
- `build:configuration` → `"Release"`

### Configuration Schema Validation

Consider adding schema validation for configuration files to provide better error messages. For INI files, this would involve validating known sections and keys.

### Configuration Hot Reload

For development scenarios, consider adding support for configuration hot reload when files change.

## Breaking Changes

This refactoring should not introduce any breaking changes as it maintains backward compatibility with:
- All existing DOTNET_ prefixed environment variables
- All system environment variables (accessed through fallback provider)
- All existing global.json file structures and locations
- All existing APIs and interfaces (through adapter patterns)


## Success Criteria

- [ ] **Microsoft.Extensions.Configuration.DotnetCli project created with proper structure**
- [ ] **Configuration binding source generator enabled (`EnableConfigurationBindingGenerator=true`)**
- [ ] **All configuration models are `sealed` classes optimized for source generator**
- [ ] **Lazy initialization implemented for all configuration sections**
- [ ] **Unused configuration sections have zero runtime cost**
- [ ] **Strongly-typed configuration binding using `ConfigurationBinder.Get<T>()` throughout**
- [ ] All direct `Environment.GetEnvironmentVariable()` calls for DOTNET_ prefixed variables replaced
- [ ] All direct global.json reading replaced with configuration providers
- [ ] System environment variables continue to work through existing providers
- [ ] **Functional grouping of configuration settings implemented**
- [ ] **Type-safe configuration access with compile-time checking**
- [ ] **AOT and trim-friendly configuration system (no reflection)**
- [ ] Comprehensive test coverage for new configuration system including source generator scenarios
- [ ] All existing functionality preserved (backward compatibility)
- [ ] **Performance improved due to lazy loading and source generation**
- [ ] Documentation updated to reflect strongly-typed configuration system
